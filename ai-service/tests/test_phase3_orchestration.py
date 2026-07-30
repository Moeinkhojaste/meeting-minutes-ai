"""Gemini-first and independent fallback orchestration tests."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from app.audio import PreparedAudio  # noqa: E402
from app.errors import AppError, ProviderError  # noqa: E402
from app.orchestration import PipelineService  # noqa: E402
from app.providers.gemini import GeminiProvider  # noqa: E402
from tests.phase3_helpers import (  # noqa: E402
    FakeGemini,
    FakeLocalStt,
    settings,
)

AUDIO = PreparedAudio(Path("unused.wav"), "audio/wav", 10, 1.0)


def provider_error(reason: str = "GEMINI_KEY_MISSING") -> ProviderError:
    return ProviderError(
        "GEMINI_PROVIDER_FAILED",
        "Gemini processing failed.",
        retryable=False,
        fallback_reason=reason,
    )


def test_primary_success_uses_exactly_two_gemini_calls() -> None:
    gemini = FakeGemini()
    local = FakeLocalStt()
    service = PipelineService(settings(), gemini, local)

    result = service.process(AUDIO, "fast")

    assert result.status == "completed"
    assert gemini.transcription_calls == 1
    assert gemini.minutes_calls == 1
    assert local.calls == 0


def test_missing_key_falls_back_only_for_transcription() -> None:
    gemini = FakeGemini(transcription_error=provider_error())
    local = FakeLocalStt()
    service = PipelineService(settings(gemini_api_key=""), gemini, local)

    result = service.process(AUDIO, "quality")

    assert result.status == "completed"
    assert local.calls == 1
    assert gemini.minutes_calls == 1
    assert result.transcriptionMetadata.actualProvider == "faster-whisper"
    assert result.transcriptionMetadata.actualModel == "large-v3-turbo"


def test_real_missing_key_path_preserves_local_raw_result_as_partial() -> None:
    current_settings = settings(gemini_api_key="")
    local = FakeLocalStt()
    service = PipelineService(
        current_settings,
        GeminiProvider(current_settings),
        local,
    )

    result = service.process(AUDIO, "fast")

    assert result.status == "partial"
    assert result.rawTranscript.segments
    assert result.transcriptionMetadata.actualProvider == "faster-whisper"
    assert result.transcriptionMetadata.fallbackReason == "GEMINI_KEY_MISSING"
    assert result.stageError is not None
    assert result.stageError.code == "GEMINI_UNAVAILABLE"


def test_stage_two_failure_returns_partial_raw_transcript() -> None:
    gemini = FakeGemini(minutes_error=provider_error("GEMINI_QUOTA"))
    local = FakeLocalStt()
    service = PipelineService(settings(), gemini, local)

    result = service.process(AUDIO, "fast")

    assert result.status == "partial"
    assert result.rawTranscript.segments
    assert result.cleanedTranscript is None
    assert result.minutes is None
    assert result.stageError is not None
    assert local.calls == 0


def test_stage_two_never_invokes_local_stt() -> None:
    gemini = FakeGemini(minutes_error=provider_error())
    local = FakeLocalStt()
    service = PipelineService(settings(), gemini, local)

    with pytest.raises(ProviderError):
        service.generate_minutes(gemini.transcribe(AUDIO, "fast")[0], "fast")
    assert local.calls == 0


def test_both_transcription_providers_failed_is_safe() -> None:
    gemini = FakeGemini(transcription_error=provider_error())
    local = FakeLocalStt(
        ProviderError(
            "LOCAL_STT_FAILED",
            "Local transcription failed.",
            retryable=False,
            fallback_reason="LOCAL_STT_UNAVAILABLE",
        )
    )
    service = PipelineService(settings(), gemini, local)

    with pytest.raises(AppError) as captured:
        service.transcribe(AUDIO, "fast")

    assert captured.value.code == "ALL_TRANSCRIPTION_PROVIDERS_FAILED"
    assert "test-key" not in captured.value.message


def test_quality_mode_never_switches_mode_during_fallback() -> None:
    gemini = FakeGemini(transcription_error=provider_error())
    local = FakeLocalStt()
    service = PipelineService(settings(), gemini, local)

    result = service.transcribe(AUDIO, "quality")

    assert result.metadata.requestedMode == "quality"
    assert result.metadata.primaryModel == "gemini-3.6-flash"
    assert result.metadata.actualModel == "large-v3-turbo"
