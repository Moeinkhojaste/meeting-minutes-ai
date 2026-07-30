"""Provider interfaces used by the orchestration layer."""

from __future__ import annotations

from typing import Protocol

from app.audio import PreparedAudio
from app.config import ProcessingMode
from app.schemas import MinutesGeneration, RawTranscript


class TranscriptionProvider(Protocol):
    name: str

    def transcribe(
        self, audio: PreparedAudio, mode: ProcessingMode
    ) -> tuple[RawTranscript, str]: ...


class MinutesProvider(Protocol):
    name: str

    def generate_minutes(
        self, transcript: RawTranscript, mode: ProcessingMode
    ) -> tuple[MinutesGeneration, str]: ...


class PrimaryProvider(TranscriptionProvider, MinutesProvider, Protocol):
    """Provider supporting both pipeline stages."""
