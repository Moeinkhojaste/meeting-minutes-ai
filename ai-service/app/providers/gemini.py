"""Gemini transcription and minutes provider."""

from __future__ import annotations

import json
from collections.abc import Callable
from typing import Any

from google import genai
from google.genai import types
from pydantic import ValidationError

from app.audio import PreparedAudio
from app.config import ProcessingMode, Settings
from app.errors import ProviderError
from app.prompts import (
    MINUTES_PROMPT_PREFIX,
    MINUTES_PROMPT_SUFFIX,
    MINUTES_SYSTEM_INSTRUCTION,
    TRANSCRIPTION_PROMPT,
    TRANSCRIPTION_SYSTEM_INSTRUCTION,
)
from app.schemas import (
    CleanedSegment,
    CleanedTranscript,
    MeetingMinutes,
    MinutesGeneration,
    RawTranscript,
    Topic,
    Uncertainty,
)

ClientFactory = Callable[[str, Settings], Any]


class GeminiProvider:
    """Schema-constrained Gemini adapter with no automatic retries."""

    name = "gemini"

    def __init__(
        self,
        settings: Settings,
        *,
        client_factory: ClientFactory | None = None,
    ) -> None:
        self._settings = settings
        self._client_factory = client_factory or _create_client

    def transcribe(
        self, audio: PreparedAudio, mode: ProcessingMode
    ) -> tuple[RawTranscript, str]:
        client = self._client()
        model = self._settings.gemini_model(mode)
        uploaded_name: str | None = None
        try:
            if audio.size_bytes <= self._settings.gemini_inline_limit_bytes:
                media: Any = types.Part.from_bytes(
                    data=audio.path.read_bytes(),
                    mime_type=audio.mime_type,
                )
            else:
                try:
                    uploaded = client.files.upload(
                        file=audio.path,
                        config={"mime_type": audio.mime_type},
                    )
                except Exception as error:
                    raise _provider_error(
                        error,
                        fallback_reason="GEMINI_FILES_API_FAILURE",
                    ) from error
                uploaded_name = uploaded.name
                media = uploaded

            response = client.models.generate_content(
                model=model,
                contents=[TRANSCRIPTION_PROMPT, media],
                config=types.GenerateContentConfig(
                    system_instruction=TRANSCRIPTION_SYSTEM_INSTRUCTION,
                    response_mime_type="application/json",
                    response_json_schema=RawTranscript.model_json_schema(),
                    max_output_tokens=65536,
                ),
            )
            transcript = _parse_response(response, RawTranscript)
            return transcript, model
        except ProviderError:
            raise
        except Exception as error:
            raise _provider_error(error) from error
        finally:
            if uploaded_name:
                try:
                    client.files.delete(name=uploaded_name)
                except Exception:
                    pass

    def generate_minutes(
        self, transcript: RawTranscript, mode: ProcessingMode
    ) -> tuple[MinutesGeneration, str]:
        client = self._client()
        model = self._settings.gemini_model(mode)
        transcript_json = transcript.model_dump_json()
        prompt = (
            f"{MINUTES_PROMPT_PREFIX}\n{transcript_json}\n"
            f"{MINUTES_PROMPT_SUFFIX}"
        )
        try:
            response = client.models.generate_content(
                model=model,
                contents=prompt,
                config=types.GenerateContentConfig(
                    system_instruction=MINUTES_SYSTEM_INSTRUCTION,
                    response_mime_type="application/json",
                    response_json_schema=MinutesGeneration.model_json_schema(),
                    max_output_tokens=65536,
                ),
            )
            generated = _parse_response(response, MinutesGeneration)
            try:
                generated.validate_references(transcript)
            except ValueError as error:
                raise ProviderError(
                    "GEMINI_PROVIDER_FAILED",
                    "Gemini returned invalid structured output.",
                    retryable=False,
                    fallback_reason="GEMINI_INVALID_OUTPUT",
                ) from error
            return generated, model
        except ProviderError:
            raise
        except Exception as error:
            raise _provider_error(error) from error

    def _client(self) -> Any:
        if not self._settings.gemini_api_key:
            raise ProviderError(
                "GEMINI_UNAVAILABLE",
                "Gemini is not configured.",
                retryable=False,
                fallback_reason="GEMINI_KEY_MISSING",
            )
        try:
            return self._client_factory(
                self._settings.gemini_api_key,
                self._settings,
            )
        except Exception as error:
            raise _provider_error(error) from error


def _create_client(api_key: str, settings: Settings) -> genai.Client:
    return genai.Client(
        api_key=api_key,
        http_options=types.HttpOptions(
            timeout=int(settings.request_timeout_seconds * 1000),
            retry_options=types.HttpRetryOptions(attempts=1),
        ),
    )


def _parse_response(response: Any, schema: type[Any]) -> Any:
    text = getattr(response, "text", None)
    if not text:
        reason = "GEMINI_SAFETY_BLOCK" if _was_blocked(response) else (
            "GEMINI_INVALID_OUTPUT"
        )
        raise ProviderError(
            "GEMINI_PROVIDER_FAILED",
            "Gemini did not return a usable response.",
            retryable=False,
            fallback_reason=reason,
        )
    try:
        return schema.model_validate_json(text)
    except (ValidationError, ValueError, json.JSONDecodeError) as error:
        raise ProviderError(
            "GEMINI_PROVIDER_FAILED",
            "Gemini returned invalid structured output.",
            retryable=False,
            fallback_reason="GEMINI_INVALID_OUTPUT",
        ) from error


def _was_blocked(response: Any) -> bool:
    feedback = getattr(response, "prompt_feedback", None)
    return bool(feedback and getattr(feedback, "block_reason", None))


def _provider_error(
    error: Exception,
    *,
    fallback_reason: str | None = None,
) -> ProviderError:
    status = getattr(error, "code", None) or getattr(error, "status_code", None)
    if fallback_reason is None:
        if status in {401, 403}:
            fallback_reason = "GEMINI_AUTH_OR_REGION"
        elif status == 429:
            fallback_reason = "GEMINI_QUOTA"
        elif isinstance(error, TimeoutError):
            fallback_reason = "GEMINI_TIMEOUT"
        elif isinstance(status, int) and status >= 500:
            fallback_reason = "GEMINI_SERVER_ERROR"
        else:
            fallback_reason = "GEMINI_NETWORK_OR_PROVIDER"
    return ProviderError(
        "GEMINI_PROVIDER_FAILED",
        "Gemini processing failed.",
        retryable=fallback_reason
        in {"GEMINI_TIMEOUT", "GEMINI_SERVER_ERROR", "GEMINI_NETWORK_OR_PROVIDER"},
        fallback_reason=fallback_reason,
    )


def _create_local_minutes_fallback(raw: RawTranscript) -> MinutesGeneration:
    cleaned_segments = [
        CleanedSegment(
            id=f"clean-{index:04d}",
            text=segment.text,
            sourceRawSegmentIds=[segment.id],
        )
        for index, segment in enumerate(raw.segments, start=1)
    ]

    full_text = " ".join(s.text for s in raw.segments).strip()
    summary = full_text if full_text else "No transcript speech content detected."
    all_raw_ids = [s.id for s in raw.segments]
    evidence = all_raw_ids[:1] if all_raw_ids else []

    topics = (
        [
            Topic(
                title="Discussion Summary",
                summary=summary,
                evidenceSegmentIds=evidence,
            )
        ]
        if evidence
        else []
    )

    return MinutesGeneration(
        cleanedTranscript=CleanedTranscript(segments=cleaned_segments),
        minutes=MeetingMinutes(
            title="Generated Meeting Minutes",
            date=None,
            participants=[],
            summary=summary,
            topics=topics,
            decisions=[],
            actionItems=[],
            openQuestions=[],
            uncertainties=[
                Uncertainty(
                    field="geminiKey",
                    description=(
                        "Minutes generated via local fallback pipeline "
                        "(GEMINI_API_KEY is not configured)."
                    ),
                    evidenceSegmentIds=[],
                )
            ]
            if not raw.segments
            else [],
        ),
    )
