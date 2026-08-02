"""Existing CUDA faster-whisper adapter for transcription fallback."""

from __future__ import annotations

from app.audio import PreparedAudio
from app.config import ProcessingMode, Settings
from app.errors import ProviderError
from app.schemas import RawSegment, RawTranscript
from transcribe import transcribe_segments


class FasterWhisperProvider:
    """Use the existing CUDA-only transcription implementation."""

    name = "faster-whisper"

    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def transcribe(
        self, audio: PreparedAudio, mode: ProcessingMode
    ) -> tuple[RawTranscript, str]:
        model = self._settings.local_stt_model(mode)
        try:
            segments = transcribe_segments(audio.path, model)
            transcript = RawTranscript(
                segments=[
                    RawSegment(
                        id=f"seg-{index:04d}",
                        speaker=None,
                        startMilliseconds=max(0, round(segment.start_seconds * 1000)),
                        endMilliseconds=max(1, round(segment.end_seconds * 1000)),
                        language=segment.language,
                        text=segment.text,
                    )
                    for index, segment in enumerate(segments, start=1)
                ]
            )
            return transcript, model
        except Exception as error:
            raise ProviderError(
                "LOCAL_STT_FAILED",
                "Local transcription failed.",
                retryable=False,
                fallback_reason="LOCAL_STT_UNAVAILABLE",
            ) from error
