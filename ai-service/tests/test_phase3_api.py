"""FastAPI endpoint and safe-error contract tests."""

from __future__ import annotations

import sys
from pathlib import Path

from fastapi.testclient import TestClient

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from app.api import create_app  # noqa: E402
from app.errors import ProviderError  # noqa: E402
from app.orchestration import PipelineService  # noqa: E402
from tests.phase3_helpers import (  # noqa: E402
    FakeGemini,
    FakeLocalStt,
    FakePipeline,
    raw_transcript,
    settings,
    wav_bytes,
)


def client(pipeline: object | None = None) -> TestClient:
    return TestClient(
        create_app(settings=settings(), pipeline=pipeline or FakePipeline())
    )


def error_fields(payload: dict[str, object]) -> set[str]:
    return set(payload)


def test_health_endpoints() -> None:
    test_client = client()

    assert test_client.get("/health/live").json() == {"status": "live"}
    ready = test_client.get("/health/ready")
    assert ready.status_code == 200
    assert ready.json()["status"] == "ready"


def test_missing_key_health_is_degraded_not_unusable() -> None:
    test_client = TestClient(
        create_app(settings=settings(gemini_api_key=""), pipeline=FakePipeline())
    )

    response = test_client.get("/health/ready")

    assert response.status_code == 200
    assert response.json()["status"] == "degraded"
    assert response.json()["transcription"]["localFallbackConfigured"] is True


def test_transcription_endpoint_accepts_audio_and_mode() -> None:
    response = client().post(
        "/v1/transcriptions",
        files={"audio": ("meeting.wav", wav_bytes(), "audio/wav")},
        data={"mode": "quality"},
    )

    assert response.status_code == 200
    assert response.json()["rawTranscript"]["segments"][0]["id"] == "seg-0001"
    assert "X-Correlation-ID" in response.headers


def test_minutes_endpoint_accepts_strict_raw_transcript() -> None:
    response = client().post(
        "/v1/minutes",
        json={
            "rawTranscript": raw_transcript().model_dump(mode="json"),
            "mode": "fast",
        },
    )

    assert response.status_code == 200
    assert response.json()["minutes"]["decisions"]


def test_process_endpoint_returns_complete_result() -> None:
    response = client().post(
        "/v1/process",
        files={"audio": ("meeting.wav", wav_bytes(), "audio/wav")},
        data={"mode": "fast"},
    )

    assert response.status_code == 200
    assert response.json()["status"] == "completed"


def test_process_returns_partial_raw_transcript_when_minutes_fail() -> None:
    gemini = FakeGemini(
        minutes_error=ProviderError(
            "GEMINI_PROVIDER_FAILED",
            "Gemini processing failed.",
            retryable=False,
            fallback_reason="GEMINI_QUOTA",
        )
    )
    pipeline = PipelineService(settings(), gemini, FakeLocalStt())

    response = client(pipeline).post(
        "/v1/process",
        files={"audio": ("meeting.wav", wav_bytes(), "audio/wav")},
        data={"mode": "fast"},
    )

    assert response.status_code == 200
    assert response.json()["status"] == "partial"
    assert response.json()["rawTranscript"]["segments"]
    assert response.json()["minutes"] is None


def test_minutes_failure_returns_exact_safe_error_without_local_llm() -> None:
    gemini = FakeGemini(
        minutes_error=ProviderError(
            "GEMINI_PROVIDER_FAILED",
            "Gemini processing failed.",
            retryable=False,
            fallback_reason="GEMINI_KEY_MISSING",
        )
    )
    pipeline = PipelineService(settings(), gemini, FakeLocalStt())

    response = client(pipeline).post(
        "/v1/minutes",
        json={
            "rawTranscript": raw_transcript().model_dump(mode="json"),
            "mode": "quality",
        },
    )

    assert response.status_code == 502
    assert error_fields(response.json()) == {
        "code",
        "message",
        "correlationId",
        "retryable",
    }
    assert pipeline._local_stt.calls == 0


def test_invalid_mode_returns_exact_safe_error_without_provider() -> None:
    class ProviderMustNotRun:
        def process(self, *_args: object, **_kwargs: object) -> object:
            raise AssertionError("provider was invoked for invalid input")

    response = client(ProviderMustNotRun()).post(
        "/v1/process",
        files={"audio": ("meeting.wav", wav_bytes(), "audio/wav")},
        data={"mode": "turbo"},
    )

    assert response.status_code == 422
    assert error_fields(response.json()) == {
        "code",
        "message",
        "correlationId",
        "retryable",
    }


def test_unexpected_failure_does_not_expose_exception_details() -> None:
    class UnexpectedFailure:
        def generate_minutes(self, *_args: object, **_kwargs: object) -> object:
            raise RuntimeError("secret key, prompt, transcript, and private path")

    test_client = TestClient(
        create_app(settings=settings(), pipeline=UnexpectedFailure()),
        raise_server_exceptions=False,
    )
    response = test_client.post(
        "/v1/minutes",
        json={
            "rawTranscript": raw_transcript().model_dump(mode="json"),
            "mode": "fast",
        },
    )

    assert response.status_code == 500
    assert response.json()["code"] == "INTERNAL_ERROR"
    assert "secret" not in response.text
    assert "transcript" not in response.text.lower()


def test_invalid_audio_returns_safe_error() -> None:
    response = client().post(
        "/v1/transcriptions",
        files={"audio": ("meeting.wav", b"not audio", "audio/wav")},
    )

    assert response.status_code == 422
    assert response.json()["code"] == "UNSUPPORTED_AUDIO"
    assert "not audio" not in response.text


def test_request_has_no_consent_requirement() -> None:
    response = client().post(
        "/v1/transcriptions",
        files={"audio": ("private.wav", wav_bytes(), "audio/wav")},
    )

    assert response.status_code == 200
    assert "consent" not in response.text.lower()


def test_missing_route_uses_safe_error_shape() -> None:
    response = client().get("/missing")

    assert response.status_code == 404
    assert error_fields(response.json()) == {
        "code",
        "message",
        "correlationId",
        "retryable",
    }
