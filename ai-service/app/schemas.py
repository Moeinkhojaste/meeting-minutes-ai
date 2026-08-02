"""Strict Pydantic contracts for the Phase 3 pipeline."""

from __future__ import annotations

from datetime import datetime
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.config import ProcessingMode


class StrictModel(BaseModel):
    model_config = ConfigDict(extra="forbid")


class RawSegment(StrictModel):
    id: str = Field(pattern=r"^seg-[0-9]{4,}$")
    speaker: str | None = None
    startMilliseconds: int = Field(ge=0)
    endMilliseconds: int = Field(ge=1)
    language: str = Field(min_length=2, max_length=32)
    text: str = Field(min_length=1)

    @model_validator(mode="after")
    def validate_interval(self) -> RawSegment:
        if self.endMilliseconds <= self.startMilliseconds:
            raise ValueError("segment end must be after segment start")
        return self


class RawTranscript(StrictModel):
    schemaVersion: int = Field(default=1, ge=1, le=1)
    segments: list[RawSegment]

    @model_validator(mode="after")
    def validate_segments(self) -> RawTranscript:
        ids: set[str] = set()
        previous_start = -1
        for segment in self.segments:
            if segment.id in ids:
                raise ValueError("raw segment IDs must be unique")
            if segment.startMilliseconds < previous_start:
                raise ValueError("raw segments must be timestamp ordered")
            ids.add(segment.id)
            previous_start = segment.startMilliseconds
        return self


class CleanedSegment(StrictModel):
    id: str = Field(pattern=r"^clean-[0-9]{4,}$")
    text: str = Field(min_length=1)
    sourceRawSegmentIds: list[str] = Field(min_length=1)


class CleanedTranscript(StrictModel):
    schemaVersion: int = Field(default=1, ge=1, le=1)
    segments: list[CleanedSegment]


class Participant(StrictModel):
    name: str = Field(min_length=1)
    evidenceSegmentIds: list[str] = Field(min_length=1)


class Topic(StrictModel):
    title: str = Field(min_length=1)
    summary: str = Field(min_length=1)
    evidenceSegmentIds: list[str] = Field(min_length=1)


class Decision(StrictModel):
    text: str = Field(min_length=1)
    evidenceSegmentIds: list[str] = Field(min_length=1)


class ActionItem(StrictModel):
    task: str = Field(min_length=1)
    assignee: str | None = None
    deadline: str | None = None
    evidenceSegmentIds: list[str] = Field(min_length=1)


class OpenQuestion(StrictModel):
    text: str = Field(min_length=1)
    evidenceSegmentIds: list[str] = Field(min_length=1)


class Uncertainty(StrictModel):
    field: str = Field(min_length=1)
    description: str = Field(min_length=1)
    evidenceSegmentIds: list[str] = Field(default_factory=list)


class MeetingMinutes(StrictModel):
    schemaVersion: int = Field(default=1, ge=1, le=1)
    title: str | None = None
    date: str | None = None
    participants: list[Participant] = Field(default_factory=list)
    summary: str = ""
    topics: list[Topic] = Field(default_factory=list)
    decisions: list[Decision] = Field(default_factory=list)
    actionItems: list[ActionItem] = Field(default_factory=list)
    openQuestions: list[OpenQuestion] = Field(default_factory=list)
    uncertainties: list[Uncertainty] = Field(default_factory=list)


class MinutesGeneration(StrictModel):
    cleanedTranscript: CleanedTranscript
    minutes: MeetingMinutes

    def validate_references(self, raw: RawTranscript) -> None:
        raw_ids = {segment.id for segment in raw.segments}
        clean_ids: set[str] = set()
        for segment in self.cleanedTranscript.segments:
            if segment.id in clean_ids:
                raise ValueError("cleaned segment IDs must be unique")
            if not set(segment.sourceRawSegmentIds).issubset(raw_ids):
                raise ValueError("cleaned transcript contains an unknown source ID")
            clean_ids.add(segment.id)

        evidence_groups = [
            item.evidenceSegmentIds
            for collection in (
                self.minutes.participants,
                self.minutes.topics,
                self.minutes.decisions,
                self.minutes.actionItems,
                self.minutes.openQuestions,
                self.minutes.uncertainties,
            )
            for item in collection
        ]
        if any(not set(group).issubset(raw_ids) for group in evidence_groups):
            raise ValueError("meeting minutes contain an unknown evidence ID")


ProviderName = Literal["gemini", "faster-whisper"]


class StageMetadata(StrictModel):
    stage: Literal["transcription", "minutes"]
    requestedMode: ProcessingMode
    primaryProvider: Literal["gemini"] = "gemini"
    primaryModel: str
    actualProvider: ProviderName
    actualModel: str
    fallbackUsed: bool
    fallbackReason: str | None = None
    promptVersion: str
    schemaVersion: int = Field(default=1, ge=1, le=1)
    startedAt: datetime
    completedAt: datetime
    durationMilliseconds: int = Field(ge=0)


class TranscriptionResponse(StrictModel):
    rawTranscript: RawTranscript
    metadata: StageMetadata
    correlationId: str


class SafeError(StrictModel):
    code: str
    message: str
    correlationId: str
    retryable: bool


class EmbeddedStageError(StrictModel):
    code: str
    message: str
    retryable: bool


class MinutesRequest(StrictModel):
    rawTranscript: RawTranscript
    mode: ProcessingMode = "fast"


class MinutesResponse(StrictModel):
    cleanedTranscript: CleanedTranscript
    minutes: MeetingMinutes
    metadata: StageMetadata
    correlationId: str


class ProcessResponse(StrictModel):
    status: Literal["completed", "partial"]
    rawTranscript: RawTranscript
    cleanedTranscript: CleanedTranscript | None
    minutes: MeetingMinutes | None
    transcriptionMetadata: StageMetadata
    minutesMetadata: StageMetadata | None
    stageError: EmbeddedStageError | None
    correlationId: str
    totalDurationMilliseconds: int = Field(ge=0)

    @model_validator(mode="after")
    def validate_status(self) -> ProcessResponse:
        if self.status == "completed":
            if (
                self.cleanedTranscript is None
                or self.minutes is None
                or self.minutesMetadata is None
                or self.stageError is not None
            ):
                raise ValueError("completed result is missing stage-two output")
        elif (
            self.cleanedTranscript is not None
            or self.minutes is not None
            or self.stageError is None
        ):
            raise ValueError("partial result has inconsistent stage-two output")
        return self
