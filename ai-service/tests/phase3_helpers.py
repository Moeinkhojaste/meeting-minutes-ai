"""Shared Phase 3 test doubles and schema fixtures."""

from __future__ import annotations

import io
import wave
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from app.config import Settings
from app.errors import ProviderError
from app.schemas import (
    CleanedSegment,
    CleanedTranscript,
    MeetingMinutes,
    MinutesGeneration,
    MinutesResponse,
    ProcessResponse,
    RawSegment,
    RawTranscript,
    StageMetadata,
    TranscriptionResponse,
)


def settings(**overrides: Any) -> Settings:
    values: dict[str, Any] = {
        "gemini_api_key": "test-key",
        "default_mode": "fast",
        "gemini_fast_model": "gemini-3.5-flash-lite",
        "gemini_quality_model": "gemini-3.6-flash",
        "max_audio_bytes": 524_288_000,
        "max_audio_duration_seconds": 5400.0,
        "gemini_inline_limit_bytes": 20_971_520,
        "local_fast_stt_model": "small",
        "local_quality_stt_model": "large-v3-turbo",
        "ffmpeg_path": "ffmpeg",
        "ffprobe_path": "ffprobe",
        "request_timeout_seconds": 300.0,
        "local_llm_base_url": "http://localhost:11434/v1",
        "local_fast_minutes_model": "qwen2.5:3b-instruct",
        "local_quality_minutes_model": "qwen2.5:3b-instruct",
        "local_llm_api_key": "",
        "local_llm_timeout_seconds": 300.0,
    }
    values.update(overrides)
    return Settings(**values)


def raw_transcript() -> RawTranscript:
    return RawTranscript(
        segments=[
            RawSegment(
                id="seg-0001",
                speaker="Speaker 1",
                startMilliseconds=0,
                endMilliseconds=1000,
                language="fa",
                text="تصمیم گرفتیم گزارش آماده شود.",
            )
        ]
    )


def generated_minutes() -> MinutesGeneration:
    return MinutesGeneration(
        cleanedTranscript=CleanedTranscript(
            segments=[
                CleanedSegment(
                    id="clean-0001",
                    text="تصمیم گرفتیم گزارش آماده شود.",
                    sourceRawSegmentIds=["seg-0001"],
                )
            ]
        ),
        minutes=MeetingMinutes(
            title=None,
            date=None,
            summary="گزارش باید آماده شود.",
            decisions=[
                {
                    "text": "گزارش آماده شود.",
                    "evidenceSegmentIds": ["seg-0001"],
                }
            ],
        ),
    )


def metadata(stage: str = "transcription") -> StageMetadata:
    now = datetime.now(UTC)
    return StageMetadata(
        stage=stage,
        requestedMode="fast",
        primaryModel="gemini-3.5-flash-lite",
        actualProvider="gemini",
        actualModel="gemini-3.5-flash-lite",
        fallbackUsed=False,
        fallbackReason=None,
        promptVersion="test-v1",
        startedAt=now,
        completedAt=now,
        durationMilliseconds=1,
    )


def wav_bytes(seconds: float = 0.1) -> bytes:
    output = io.BytesIO()
    with wave.open(output, "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(16_000)
        audio.writeframes(b"\x00\x00" * int(16_000 * seconds))
    return output.getvalue()


class FakeGemini:
    name = "gemini"

    def __init__(
        self,
        *,
        transcription_error: ProviderError | None = None,
        minutes_error: ProviderError | None = None,
    ) -> None:
        self.transcription_error = transcription_error
        self.minutes_error = minutes_error
        self.transcription_calls = 0
        self.minutes_calls = 0

    def transcribe(self, _audio: Any, mode: str) -> tuple[RawTranscript, str]:
        self.transcription_calls += 1
        if self.transcription_error:
            raise self.transcription_error
        model = "gemini-3.5-flash-lite" if mode == "fast" else "gemini-3.6-flash"
        return raw_transcript(), model

    def generate_minutes(
        self, transcript: RawTranscript, mode: str
    ) -> tuple[MinutesGeneration, str]:
        self.minutes_calls += 1
        if self.minutes_error:
            raise self.minutes_error
        assert transcript == raw_transcript()
        model = "gemini-3.5-flash-lite" if mode == "fast" else "gemini-3.6-flash"
        return generated_minutes(), model


class FakeLocalStt:
    name = "faster-whisper"

    def __init__(self, error: ProviderError | None = None) -> None:
        self.error = error
        self.calls = 0

    def transcribe(self, _audio: Any, mode: str) -> tuple[RawTranscript, str]:
        self.calls += 1
        if self.error:
            raise self.error
        return raw_transcript(), "small" if mode == "fast" else "large-v3-turbo"


class FakeLocalMinutes:
    name = "local-llm"

    def __init__(self, error: ProviderError | None = None) -> None:
        self.error = error
        self.calls = 0

    def generate_minutes(
        self, transcript: RawTranscript, mode: str
    ) -> tuple[MinutesGeneration, str]:
        self.calls += 1
        if self.error:
            raise self.error
        assert transcript == raw_transcript()
        model = "qwen2.5:3b-instruct"
        return generated_minutes(), model


class FakePipeline:
    def transcribe(
        self, _audio: Any, _mode: Any, *, correlation_id: str
    ) -> TranscriptionResponse:
        return TranscriptionResponse(
            rawTranscript=raw_transcript(),
            metadata=metadata(),
            correlationId=correlation_id,
        )

    def generate_minutes(
        self, _transcript: Any, _mode: Any, *, correlation_id: str
    ) -> MinutesResponse:
        generated = generated_minutes()
        return MinutesResponse(
            cleanedTranscript=generated.cleanedTranscript,
            minutes=generated.minutes,
            metadata=metadata("minutes"),
            correlationId=correlation_id,
        )

    def process(
        self, _audio: Any, _mode: Any, *, correlation_id: str
    ) -> ProcessResponse:
        generated = generated_minutes()
        return ProcessResponse(
            status="completed",
            rawTranscript=raw_transcript(),
            cleanedTranscript=generated.cleanedTranscript,
            minutes=generated.minutes,
            transcriptionMetadata=metadata(),
            minutesMetadata=metadata("minutes"),
            stageError=None,
            correlationId=correlation_id,
            totalDurationMilliseconds=2,
        )


def write_wav(path: Path) -> None:
    path.write_bytes(wav_bytes())
