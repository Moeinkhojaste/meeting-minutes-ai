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
from app.providers.local_llm import LocalLLMProvider  # noqa: E402
from tests.phase3_helpers import (  # noqa: E402
    FakeGemini,
    FakeLocalMinutes,
    FakeLocalStt,
    raw_transcript,
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
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes()
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    result = service.process(AUDIO, "fast")

    assert result.status == "completed"
    assert gemini.transcription_calls == 1
    assert gemini.minutes_calls == 1
    assert local_stt.calls == 0
    assert local_minutes.calls == 0


def test_missing_key_falls_back_for_transcription_and_minutes() -> None:
    gemini = FakeGemini(
        transcription_error=provider_error("GEMINI_KEY_MISSING"),
        minutes_error=provider_error("GEMINI_KEY_MISSING"),
    )
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes()
    service = PipelineService(settings(gemini_api_key=""), gemini, local_stt, local_minutes)

    result = service.process(AUDIO, "quality")

    assert result.status == "completed"
    assert local_stt.calls == 1
    assert local_minutes.calls == 1
    assert result.transcriptionMetadata.actualProvider == "faster-whisper"
    assert result.transcriptionMetadata.actualModel == "large-v3-turbo"
    assert result.transcriptionMetadata.fallbackUsed is True
    assert result.minutesMetadata is not None
    assert result.minutesMetadata.actualProvider == "local-llm"
    assert result.minutesMetadata.actualModel == "qwen2.5:3b-instruct"
    assert result.minutesMetadata.fallbackUsed is True


def test_stage_two_gemini_failure_falls_back_to_local_llm() -> None:
    gemini = FakeGemini(minutes_error=provider_error("GEMINI_QUOTA"))
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes()
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    result = service.generate_minutes(raw_transcript(), "fast")

    assert result.metadata.actualProvider == "local-llm"
    assert result.metadata.actualModel == "qwen2.5:3b-instruct"
    assert result.metadata.fallbackUsed is True
    assert result.metadata.fallbackReason == "GEMINI_QUOTA"
    assert local_minutes.calls == 1
    assert local_stt.calls == 0


def test_stage_two_gemini_failure_full_process_completes_with_local_llm() -> None:
    gemini = FakeGemini(minutes_error=provider_error("GEMINI_QUOTA"))
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes()
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    result = service.process(AUDIO, "fast")

    assert result.status == "completed"
    assert result.rawTranscript.segments
    assert result.cleanedTranscript is not None
    assert result.minutes is not None
    assert result.stageError is None
    assert result.transcriptionMetadata.actualProvider == "gemini"
    assert result.minutesMetadata is not None
    assert result.minutesMetadata.actualProvider == "local-llm"
    assert local_minutes.calls == 1


def test_both_minutes_providers_failed_returns_partial_in_process() -> None:
    gemini = FakeGemini(minutes_error=provider_error("GEMINI_QUOTA"))
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes(
        ProviderError(
            "LOCAL_LLM_FAILED",
            "Local LLM service is unavailable.",
            retryable=True,
            fallback_reason="LOCAL_LLM_UNAVAILABLE",
        )
    )
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    result = service.process(AUDIO, "fast")

    assert result.status == "partial"
    assert result.rawTranscript.segments
    assert result.cleanedTranscript is None
    assert result.minutes is None
    assert result.stageError is not None
    assert result.stageError.code == "ALL_MINUTES_PROVIDERS_FAILED"
    assert local_minutes.calls == 1


def test_both_minutes_providers_failed_raises_safe_error_in_generate_minutes() -> None:
    gemini = FakeGemini(minutes_error=provider_error("GEMINI_QUOTA"))
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes(
        ProviderError(
            "LOCAL_LLM_FAILED",
            "Local LLM service is unavailable.",
            retryable=True,
            fallback_reason="LOCAL_LLM_UNAVAILABLE",
        )
    )
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    with pytest.raises(AppError) as captured:
        service.generate_minutes(raw_transcript(), "fast")

    assert captured.value.code == "ALL_MINUTES_PROVIDERS_FAILED"
    assert captured.value.status_code == 502


def test_both_transcription_providers_failed_is_safe() -> None:
    gemini = FakeGemini(transcription_error=provider_error())
    local_stt = FakeLocalStt(
        ProviderError(
            "LOCAL_STT_FAILED",
            "Local transcription failed.",
            retryable=False,
            fallback_reason="LOCAL_STT_UNAVAILABLE",
        )
    )
    local_minutes = FakeLocalMinutes()
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    with pytest.raises(AppError) as captured:
        service.transcribe(AUDIO, "fast")

    assert captured.value.code == "ALL_TRANSCRIPTION_PROVIDERS_FAILED"
    assert "test-key" not in captured.value.message


def test_quality_mode_never_switches_mode_during_fallback() -> None:
    gemini = FakeGemini(transcription_error=provider_error(), minutes_error=provider_error())
    local_stt = FakeLocalStt()
    local_minutes = FakeLocalMinutes()
    service = PipelineService(settings(), gemini, local_stt, local_minutes)

    stt_result = service.transcribe(AUDIO, "quality")
    assert stt_result.metadata.requestedMode == "quality"
    assert stt_result.metadata.primaryModel == "gemini-3.6-flash"
    assert stt_result.metadata.actualModel == "large-v3-turbo"

    minutes_result = service.generate_minutes(raw_transcript(), "quality")
    assert minutes_result.metadata.requestedMode == "quality"
    assert minutes_result.metadata.primaryModel == "gemini-3.6-flash"
    assert minutes_result.metadata.actualModel == "qwen2.5:3b-instruct"
