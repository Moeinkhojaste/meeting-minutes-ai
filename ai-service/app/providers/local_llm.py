"""Local LLM provider (Ollama / OpenAI-compatible) for meeting minutes generation."""

from __future__ import annotations

import json
import re
import urllib.error
import urllib.request
from collections.abc import Callable
from typing import Any

from pydantic import ValidationError

from app.config import ProcessingMode, Settings
from app.errors import ProviderError
from app.schemas import MinutesGeneration, RawTranscript

HttpPoster = Callable[[str, bytes, dict[str, str], float], bytes]


def _default_http_post(
    url: str,
    data: bytes,
    headers: dict[str, str],
    timeout: float,
) -> bytes:
    request = urllib.request.Request(url, data=data, headers=headers, method="POST")
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


class LocalLLMProvider:
    """Local OpenAI/Ollama-compatible chat completion provider for minutes generation."""

    name = "local-llm"

    def __init__(
        self,
        settings: Settings,
        *,
        http_poster: HttpPoster | None = None,
    ) -> None:
        self._settings = settings
        self._http_poster = http_poster or _default_http_post

    def generate_minutes(
        self, transcript: RawTranscript, mode: ProcessingMode
    ) -> tuple[MinutesGeneration, str]:
        model = self._settings.local_minutes_model(mode)

        # Format transcript compactly to minimize prompt tokens and prefill time
        lines = [
            f"[{seg.id}]" + (f" ({seg.speaker}):" if seg.speaker else "") + f" {seg.text}"
            for seg in transcript.segments
        ]
        transcript_text = "\n".join(lines)

        template_structure = json.dumps(
            {
                "cleanedTranscript": {
                    "schemaVersion": 1,
                    "segments": [
                        {
                            "id": "clean-0001",
                            "text": "متن تمیز شده و رسمی",
                            "sourceRawSegmentIds": ["seg-0001"],
                        }
                    ],
                },
                "minutes": {
                    "schemaVersion": 1,
                    "title": "عنوان جلسه",
                    "date": None,
                    "participants": [{"name": "نام", "evidenceSegmentIds": ["seg-0001"]}],
                    "summary": "خلاصه مذاکرات",
                    "topics": [
                        {
                            "title": "موضوع اصلی",
                            "summary": "شرح موضوع",
                            "evidenceSegmentIds": ["seg-0001"],
                        }
                    ],
                    "decisions": [
                        {
                            "text": "تصمیم گرفته شده",
                            "evidenceSegmentIds": ["seg-0001"],
                        }
                    ],
                    "actionItems": [
                        {
                            "task": "شرح کار",
                            "assignee": "مسئول یا null",
                            "deadline": "مهلت یا null",
                            "evidenceSegmentIds": ["seg-0001"],
                        }
                    ],
                    "openQuestions": [],
                    "uncertainties": [],
                },
            },
            ensure_ascii=False,
        )

        system_message = (
            "You clean transcripts and generate evidence-grounded meeting minutes in Persian.\n"
            "Clean every segment and extract title, summary, participants, topics, decisions, action items, and uncertainties.\n"
            "Every sourceRawSegmentIds and evidenceSegmentIds MUST be an existing ID from the input (e.g. seg-0001).\n"
            "Return ONLY valid JSON matching this exact object structure:\n"
            f"{template_structure}"
        )
        user_message = f"<untrusted_transcript>\n{transcript_text}\n</untrusted_transcript>"

        payload = {
            "model": model,
            "messages": [
                {"role": "system", "content": system_message},
                {"role": "user", "content": user_message},
            ],
            "response_format": {"type": "json_object"},
            "temperature": 0.1,
        }

        url = f"{self._settings.local_llm_base_url.rstrip('/')}/chat/completions"
        headers = {"Content-Type": "application/json"}
        if self._settings.local_llm_api_key:
            headers["Authorization"] = f"Bearer {self._settings.local_llm_api_key}"

        request_body = json.dumps(payload).encode("utf-8")

        try:
            raw_response = self._http_poster(
                url,
                request_body,
                headers,
                self._settings.local_llm_timeout_seconds,
            )
            response_json = json.loads(raw_response.decode("utf-8"))
            content = _extract_content(response_json)
            generated = _parse_and_normalize_minutes(content, transcript)
            generated.validate_references(transcript)
            return generated, model
        except ProviderError:
            raise
        except urllib.error.HTTPError as error:
            status = error.code
            raise ProviderError(
                "LOCAL_LLM_FAILED",
                f"Local LLM service returned HTTP {status}.",
                retryable=status >= 500,
                fallback_reason="LOCAL_LLM_SERVER_ERROR"
                if status >= 500
                else "LOCAL_LLM_ERROR",
            ) from error
        except TimeoutError as error:
            raise ProviderError(
                "LOCAL_LLM_FAILED",
                "Local LLM request timed out.",
                retryable=True,
                fallback_reason="LOCAL_LLM_TIMEOUT",
            ) from error
        except (urllib.error.URLError, ConnectionError, OSError) as error:
            raise ProviderError(
                "LOCAL_LLM_FAILED",
                "Local LLM service is unavailable.",
                retryable=True,
                fallback_reason="LOCAL_LLM_UNAVAILABLE",
            ) from error
        except (ValidationError, ValueError, json.JSONDecodeError, KeyError) as error:
            raise ProviderError(
                "LOCAL_LLM_FAILED",
                "Local LLM returned invalid structured output.",
                retryable=False,
                fallback_reason="LOCAL_LLM_INVALID_OUTPUT",
            ) from error
        except Exception as error:
            raise ProviderError(
                "LOCAL_LLM_FAILED",
                "Local LLM processing failed.",
                retryable=False,
                fallback_reason="LOCAL_LLM_ERROR",
            ) from error


def _extract_content(response_json: dict[str, Any]) -> str:
    choices = response_json.get("choices")
    if not choices or not isinstance(choices, list):
        raise ValueError("Invalid LLM response: missing choices array.")
    message = choices[0].get("message")
    if not message or not isinstance(message, dict):
        raise ValueError("Invalid LLM response: missing message object.")
    content = message.get("content")
    if not content or not isinstance(content, str):
        raise ValueError("Invalid LLM response: empty content.")
    return content


def _parse_and_normalize_minutes(content: str, transcript: RawTranscript) -> MinutesGeneration:
    text = content.strip()
    # Strip markdown code blocks if present (e.g. ```json ... ```)
    if text.startswith("```"):
        text = re.sub(r"^```(?:json)?\s*", "", text, flags=re.IGNORECASE)
        text = re.sub(r"\s*```$", "", text)
        text = text.strip()

    raw_dict = json.loads(text)
    if not isinstance(raw_dict, dict):
        raise ValueError("LLM output is not a JSON object.")

    valid_ids = {seg.id for seg in transcript.segments}
    raw_id_list = [seg.id for seg in transcript.segments]

    # Strip unexpected root keys
    allowed_root = {"cleanedTranscript", "minutes"}
    for k in list(raw_dict.keys()):
        if k not in allowed_root:
            raw_dict.pop(k)

    # Normalize cleanedTranscript
    cleaned = raw_dict.get("cleanedTranscript")
    if not isinstance(cleaned, dict):
        cleaned = {"schemaVersion": 1, "segments": []}
        raw_dict["cleanedTranscript"] = cleaned
    cleaned["schemaVersion"] = 1

    allowed_cleaned = {"schemaVersion", "segments"}
    for k in list(cleaned.keys()):
        if k not in allowed_cleaned:
            cleaned.pop(k)

    segments = cleaned.get("segments", [])
    norm_segments: list[dict[str, Any]] = []
    for i, seg in enumerate(segments):
        if not isinstance(seg, dict):
            continue
        seg_id = str(seg.get("id") or f"clean-{i+1:04d}")
        seg_text = str(seg.get("text") or "")
        raw_sources = seg.get("sourceRawSegmentIds")
        if not isinstance(raw_sources, list) or not raw_sources:
            norm_sources = [raw_id_list[i] if i < len(raw_id_list) else raw_id_list[0]]
        else:
            norm_sources = [
                str(s).replace("clean-", "seg-")
                for s in raw_sources
                if str(s).replace("clean-", "seg-") in valid_ids
            ]
            if not norm_sources:
                norm_sources = [raw_id_list[i] if i < len(raw_id_list) else raw_id_list[0]]
        norm_segments.append(
            {
                "id": seg_id,
                "text": seg_text,
                "sourceRawSegmentIds": norm_sources,
            }
        )

    # Fallback to 1-to-1 if no cleaned segments were produced
    if not norm_segments:
        norm_segments = [
            {
                "id": f"clean-{i+1:04d}",
                "text": seg.text,
                "sourceRawSegmentIds": [seg.id],
            }
            for i, seg in enumerate(transcript.segments)
        ]
    cleaned["segments"] = norm_segments

    # Normalize minutes
    minutes = raw_dict.get("minutes")
    if not isinstance(minutes, dict):
        minutes = {}
        raw_dict["minutes"] = minutes
    minutes["schemaVersion"] = 1

    allowed_min = {
        "schemaVersion",
        "title",
        "date",
        "participants",
        "summary",
        "topics",
        "decisions",
        "actionItems",
        "openQuestions",
        "uncertainties",
    }
    if "actionItems" not in minutes and "actions" in minutes:
        minutes["actionItems"] = minutes.pop("actions")
    for k in list(minutes.keys()):
        if k not in allowed_min:
            minutes.pop(k)

    minutes["title"] = str(minutes.get("title") or "صورت‌جلسه")
    minutes["summary"] = str(minutes.get("summary") or "")
    minutes["date"] = str(minutes["date"]) if minutes.get("date") else None

    def fix_ev(item: dict[str, Any]) -> list[str]:
        ev = item.get("evidenceSegmentIds")
        if not isinstance(ev, list):
            return []
        res: list[str] = []
        for e in ev:
            e_str = str(e).replace("clean-", "seg-")
            if e_str in valid_ids and e_str not in res:
                res.append(e_str)
        return res

    for coll, key_field in [("topics", "title"), ("decisions", "text")]:
        items = minutes.get(coll, [])
        norm_items: list[dict[str, Any]] = []
        if isinstance(items, list):
            for it in items:
                if isinstance(it, dict):
                    norm_items.append(
                        {
                            key_field: str(it.get(key_field) or ""),
                            "summary": str(it.get("summary") or "") if coll == "topics" else None,
                            "evidenceSegmentIds": fix_ev(it),
                        }
                        if coll == "topics"
                        else {
                            key_field: str(it.get(key_field) or ""),
                            "evidenceSegmentIds": fix_ev(it),
                        }
                    )
                elif isinstance(it, str) and it.strip():
                    norm_items.append(
                        {
                            key_field: it.strip(),
                            "summary": "" if coll == "topics" else None,
                            "evidenceSegmentIds": [],
                        }
                        if coll == "topics"
                        else {
                            key_field: it.strip(),
                            "evidenceSegmentIds": [],
                        }
                    )
        minutes[coll] = norm_items

    norm_parts: list[dict[str, Any]] = []
    parts = minutes.get("participants", [])
    if isinstance(parts, list):
        for p in parts:
            if isinstance(p, dict) and p.get("name"):
                norm_parts.append(
                    {
                        "name": str(p["name"]),
                        "evidenceSegmentIds": fix_ev(p),
                    }
                )
            elif isinstance(p, str) and p.strip():
                norm_parts.append(
                    {
                        "name": p.strip(),
                        "evidenceSegmentIds": [],
                    }
                )
    minutes["participants"] = norm_parts

    norm_actions: list[dict[str, Any]] = []
    actions = minutes.get("actionItems", [])
    if isinstance(actions, list):
        for a in actions:
            if isinstance(a, dict):
                task = str(
                    a.get("task")
                    or a.get("name")
                    or a.get("description")
                    or a.get("item")
                    or ""
                )
                if task:
                    norm_actions.append(
                        {
                            "task": task,
                            "assignee": str(a["assignee"]) if a.get("assignee") else None,
                            "deadline": str(a["deadline"]) if a.get("deadline") else None,
                            "evidenceSegmentIds": fix_ev(a),
                        }
                    )
            elif isinstance(a, str) and a.strip():
                norm_actions.append(
                    {
                        "task": a.strip(),
                        "assignee": None,
                        "deadline": None,
                        "evidenceSegmentIds": [],
                    }
                )
    minutes["actionItems"] = norm_actions

    oq = minutes.get("openQuestions")
    minutes["openQuestions"] = (
        [
            {"text": str(q.get("text") if isinstance(q, dict) else q), "evidenceSegmentIds": fix_ev(q) if isinstance(q, dict) else []}
            for q in oq
            if (isinstance(q, dict) and q.get("text")) or (isinstance(q, str) and q.strip())
        ]
        if isinstance(oq, list)
        else []
    )

    unc = minutes.get("uncertainties")
    minutes["uncertainties"] = (
        [
            {
                "field": str(u.get("field") or "general"),
                "description": str(u.get("description") or ""),
                "evidenceSegmentIds": fix_ev(u),
            }
            for u in unc
            if isinstance(u, dict) and u.get("description")
        ]
        if isinstance(unc, list)
        else []
    )

    return MinutesGeneration.model_validate(raw_dict)

