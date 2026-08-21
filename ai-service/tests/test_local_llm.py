"""Unit tests for LocalLLMProvider."""

from __future__ import annotations

import json
import urllib.error
from pathlib import Path
import sys

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

import pytest  # noqa: E402

from app.config import Settings  # noqa: E402
from app.errors import ProviderError  # noqa: E402
from app.providers.local_llm import LocalLLMProvider  # noqa: E402
from tests.phase3_helpers import generated_minutes, raw_transcript, settings  # noqa: E402


def _make_mock_response(content: str) -> bytes:
    payload = {
        "id": "chatcmpl-test",
        "object": "chat.completion",
        "created": 1234567890,
        "model": "qwen2.5:3b-instruct",
        "choices": [
            {
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": content,
                },
                "finish_reason": "stop",
            }
        ],
    }
    return json.dumps(payload).encode("utf-8")


def test_local_llm_successful_generation() -> None:
    expected = generated_minutes()
    response_bytes = _make_mock_response(expected.model_dump_json())

    recorded_calls: list[tuple[str, bytes, dict[str, str], float]] = []

    def mock_post(url: str, data: bytes, headers: dict[str, str], timeout: float) -> bytes:
        recorded_calls.append((url, data, headers, timeout))
        return response_bytes

    config = settings(
        local_llm_base_url="http://127.0.0.1:11434/v1",
        local_fast_minutes_model="qwen2.5:3b-instruct",
        local_llm_api_key="secret-token",
        local_llm_timeout_seconds=60.0,
    )
    provider = LocalLLMProvider(config, http_poster=mock_post)

    transcript = raw_transcript()
    result, model = provider.generate_minutes(transcript, "fast")

    assert model == "qwen2.5:3b-instruct"
    assert len(result.cleanedTranscript.segments) == 1
    assert result.minutes.summary == expected.minutes.summary
    assert len(recorded_calls) == 1

    url, body, headers, timeout = recorded_calls[0]
    assert url == "http://127.0.0.1:11434/v1/chat/completions"
    assert headers["Authorization"] == "Bearer secret-token"
    assert headers["Content-Type"] == "application/json"
    assert timeout == 60.0

    payload = json.loads(body.decode("utf-8"))
    assert payload["model"] == "qwen2.5:3b-instruct"
    assert payload["response_format"] == {"type": "json_object"}


def test_local_llm_quality_mode_selects_quality_model() -> None:
    expected = generated_minutes()
    response_bytes = _make_mock_response(expected.model_dump_json())

    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        return response_bytes

    config = settings(
        local_quality_minutes_model="qwen2.5:7b-instruct",
    )
    provider = LocalLLMProvider(config, http_poster=mock_post)
    _result, model = provider.generate_minutes(raw_transcript(), "quality")

    assert model == "qwen2.5:7b-instruct"


def test_local_llm_strips_markdown_code_fences() -> None:
    expected = generated_minutes()
    wrapped_content = f"```json\n{expected.model_dump_json()}\n```"
    response_bytes = _make_mock_response(wrapped_content)

    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        return response_bytes

    provider = LocalLLMProvider(settings(), http_poster=mock_post)
    result, _model = provider.generate_minutes(raw_transcript(), "fast")

    assert len(result.cleanedTranscript.segments) == 1
    assert result.minutes.summary == expected.minutes.summary


def test_local_llm_unavailable_maps_to_safe_provider_error() -> None:
    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        raise urllib.error.URLError("Connection refused")

    provider = LocalLLMProvider(settings(), http_poster=mock_post)

    with pytest.raises(ProviderError) as exc_info:
        provider.generate_minutes(raw_transcript(), "fast")

    assert exc_info.value.code == "LOCAL_LLM_FAILED"
    assert exc_info.value.fallback_reason == "LOCAL_LLM_UNAVAILABLE"
    assert exc_info.value.retryable is True


def test_local_llm_timeout_maps_to_safe_provider_error() -> None:
    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        raise TimeoutError("Request timed out")

    provider = LocalLLMProvider(settings(), http_poster=mock_post)

    with pytest.raises(ProviderError) as exc_info:
        provider.generate_minutes(raw_transcript(), "fast")

    assert exc_info.value.code == "LOCAL_LLM_FAILED"
    assert exc_info.value.fallback_reason == "LOCAL_LLM_TIMEOUT"
    assert exc_info.value.retryable is True


def test_local_llm_http_server_error_maps_to_retryable_provider_error() -> None:
    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        raise urllib.error.HTTPError(
            url="http://localhost:11434/v1/chat/completions",
            code=503,
            msg="Service Unavailable",
            hdrs={},  # type: ignore[arg-type]
            fp=None,
        )

    provider = LocalLLMProvider(settings(), http_poster=mock_post)

    with pytest.raises(ProviderError) as exc_info:
        provider.generate_minutes(raw_transcript(), "fast")

    assert exc_info.value.code == "LOCAL_LLM_FAILED"
    assert exc_info.value.fallback_reason == "LOCAL_LLM_SERVER_ERROR"
    assert exc_info.value.retryable is True


def test_local_llm_invalid_json_maps_to_invalid_output_provider_error() -> None:
    response_bytes = _make_mock_response("Not a JSON object at all")

    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        return response_bytes

    provider = LocalLLMProvider(settings(), http_poster=mock_post)

    with pytest.raises(ProviderError) as exc_info:
        provider.generate_minutes(raw_transcript(), "fast")

    assert exc_info.value.code == "LOCAL_LLM_FAILED"
    assert exc_info.value.fallback_reason == "LOCAL_LLM_INVALID_OUTPUT"
    assert exc_info.value.retryable is False


def test_local_llm_unknown_evidence_id_fails_reference_validation() -> None:
    hallucinated = generated_minutes()
    # Modify evidence to reference non-existent segment
    hallucinated.minutes.decisions[0].evidenceSegmentIds = ["seg-9999"]
    response_bytes = _make_mock_response(hallucinated.model_dump_json())

    def mock_post(_url: str, _data: bytes, _headers: dict[str, str], _timeout: float) -> bytes:
        return response_bytes

    provider = LocalLLMProvider(settings(), http_poster=mock_post)

    with pytest.raises(ProviderError) as exc_info:
        provider.generate_minutes(raw_transcript(), "fast")

    assert exc_info.value.code == "LOCAL_LLM_FAILED"
    assert exc_info.value.fallback_reason == "LOCAL_LLM_INVALID_OUTPUT"
