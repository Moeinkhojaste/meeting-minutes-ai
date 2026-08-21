"""Stage orchestration and metadata construction."""

from __future__ import annotations

import time
from datetime import UTC, datetime
from uuid import uuid4

from app.audio import PreparedAudio
from app.config import ProcessingMode, Settings
from app.errors import AppError, ProviderError
from app.prompts import (
    LOCAL_MINUTES_PROMPT_VERSION,
    MINUTES_PROMPT_VERSION,
    TRANSCRIPTION_PROMPT_VERSION,
)
from app.providers.base import MinutesProvider, PrimaryProvider, TranscriptionProvider
from app.schemas import (
    EmbeddedStageError,
    MinutesResponse,
    ProcessResponse,
    ProviderName,
    RawTranscript,
    StageMetadata,
    TranscriptionResponse,
)


class PipelineService:
    """Coordinate Gemini-first STT and Gemini-first minutes generation with local fallbacks."""

    def __init__(
        self,
        settings: Settings,
        gemini: PrimaryProvider,
        local_stt: TranscriptionProvider,
        local_minutes: MinutesProvider,
    ) -> None:
        self._settings = settings
        self._gemini = gemini
        self._local_stt = local_stt
        self._local_minutes = local_minutes

    def transcribe(
        self,
        audio: PreparedAudio,
        mode: ProcessingMode,
        *,
        correlation_id: str | None = None,
    ) -> TranscriptionResponse:
        correlation_id = correlation_id or str(uuid4())
        primary_model = self._settings.gemini_model(mode)
        started_at = datetime.now(UTC)
        started = time.perf_counter()
        fallback_reason: str | None = None
        try:
            transcript, actual_model = self._gemini.transcribe(audio, mode)
            actual_provider: ProviderName = "gemini"
            fallback_used = False
        except ProviderError as gemini_error:
            fallback_reason = gemini_error.fallback_reason
            try:
                transcript, actual_model = self._local_stt.transcribe(audio, mode)
            except ProviderError as local_error:
                raise AppError(
                    "ALL_TRANSCRIPTION_PROVIDERS_FAILED",
                    "Transcription failed with all configured providers.",
                    status_code=502,
                    retryable=gemini_error.retryable or local_error.retryable,
                ) from local_error
            actual_provider = "faster-whisper"
            fallback_used = True

        completed_at = datetime.now(UTC)
        metadata = StageMetadata(
            stage="transcription",
            requestedMode=mode,
            primaryModel=primary_model,
            actualProvider=actual_provider,
            actualModel=actual_model,
            fallbackUsed=fallback_used,
            fallbackReason=fallback_reason,
            promptVersion=TRANSCRIPTION_PROMPT_VERSION,
            schemaVersion=1,
            startedAt=started_at,
            completedAt=completed_at,
            durationMilliseconds=_elapsed_milliseconds(started),
        )
        return TranscriptionResponse(
            rawTranscript=transcript,
            metadata=metadata,
            correlationId=correlation_id,
        )

    def generate_minutes(
        self,
        transcript: RawTranscript,
        mode: ProcessingMode,
        *,
        correlation_id: str | None = None,
    ) -> MinutesResponse:
        correlation_id = correlation_id or str(uuid4())
        primary_model = self._settings.gemini_model(mode)
        started_at = datetime.now(UTC)
        started = time.perf_counter()
        fallback_reason: str | None = None
        try:
            generated, actual_model = self._gemini.generate_minutes(transcript, mode)
            actual_provider: ProviderName = "gemini"
            fallback_used = False
            prompt_version = MINUTES_PROMPT_VERSION
        except ProviderError as gemini_error:
            fallback_reason = gemini_error.fallback_reason
            try:
                generated, actual_model = self._local_minutes.generate_minutes(
                    transcript, mode
                )
            except ProviderError as local_error:
                raise AppError(
                    "ALL_MINUTES_PROVIDERS_FAILED",
                    "Minutes generation failed with all configured providers.",
                    status_code=502,
                    retryable=gemini_error.retryable or local_error.retryable,
                ) from local_error
            actual_provider = "local-llm"
            fallback_used = True
            prompt_version = LOCAL_MINUTES_PROMPT_VERSION

        completed_at = datetime.now(UTC)
        metadata = StageMetadata(
            stage="minutes",
            requestedMode=mode,
            primaryModel=primary_model,
            actualProvider=actual_provider,
            actualModel=actual_model,
            fallbackUsed=fallback_used,
            fallbackReason=fallback_reason,
            promptVersion=prompt_version,
            schemaVersion=1,
            startedAt=started_at,
            completedAt=completed_at,
            durationMilliseconds=_elapsed_milliseconds(started),
        )
        return MinutesResponse(
            cleanedTranscript=generated.cleanedTranscript,
            minutes=generated.minutes,
            metadata=metadata,
            correlationId=correlation_id,
        )

    def process(
        self,
        audio: PreparedAudio,
        mode: ProcessingMode,
        *,
        correlation_id: str | None = None,
    ) -> ProcessResponse:
        correlation_id = correlation_id or str(uuid4())
        started = time.perf_counter()
        transcription = self.transcribe(
            audio,
            mode,
            correlation_id=correlation_id,
        )
        try:
            minutes = self.generate_minutes(
                transcription.rawTranscript,
                mode,
                correlation_id=correlation_id,
            )
        except (AppError, ProviderError) as error:
            return ProcessResponse(
                status="partial",
                rawTranscript=transcription.rawTranscript,
                cleanedTranscript=None,
                minutes=None,
                transcriptionMetadata=transcription.metadata,
                minutesMetadata=None,
                stageError=EmbeddedStageError(
                    code=error.code,
                    message=error.message,
                    retryable=error.retryable,
                ),
                correlationId=correlation_id,
                totalDurationMilliseconds=_elapsed_milliseconds(started),
            )

        return ProcessResponse(
            status="completed",
            rawTranscript=transcription.rawTranscript,
            cleanedTranscript=minutes.cleanedTranscript,
            minutes=minutes.minutes,
            transcriptionMetadata=transcription.metadata,
            minutesMetadata=minutes.metadata,
            stageError=None,
            correlationId=correlation_id,
            totalDurationMilliseconds=_elapsed_milliseconds(started),
        )


def _elapsed_milliseconds(started: float) -> int:
    return max(0, round((time.perf_counter() - started) * 1000))
