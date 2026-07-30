"""Configuration and schema invariants for the Phase 3 service."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
from pydantic import ValidationError

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from app.config import load_settings, parse_mode  # noqa: E402
from app.schemas import (  # noqa: E402
    CleanedSegment,
    CleanedTranscript,
    MeetingMinutes,
    MinutesGeneration,
    RawSegment,
    RawTranscript,
)
from tests.phase3_helpers import generated_minutes, raw_transcript  # noqa: E402


def test_mode_and_model_mapping() -> None:
    settings = load_settings(environment={}, env_file=Path("missing.env"))

    assert settings.default_mode == "fast"
    assert settings.gemini_model("fast") == "gemini-3.5-flash-lite"
    assert settings.gemini_model("quality") == "gemini-3.6-flash"
    assert settings.local_stt_model("fast") == "small"
    assert settings.local_stt_model("quality") == "large-v3-turbo"


def test_invalid_default_mode_is_rejected() -> None:
    with pytest.raises(ValueError, match="MM_AI_DEFAULT_MODE"):
        load_settings(
            environment={"MM_AI_DEFAULT_MODE": "turbo"},
            env_file=Path("missing.env"),
        )


def test_parse_mode_uses_default_and_rejects_unknown() -> None:
    assert parse_mode(None, "quality") == "quality"
    with pytest.raises(ValueError, match="mode"):
        parse_mode("other", "fast")


def test_raw_timestamps_must_be_ordered() -> None:
    with pytest.raises(ValidationError, match="timestamp ordered"):
        RawTranscript(
            segments=[
                RawSegment(
                    id="seg-0001",
                    startMilliseconds=100,
                    endMilliseconds=200,
                    language="fa",
                    text="one",
                ),
                RawSegment(
                    id="seg-0002",
                    startMilliseconds=50,
                    endMilliseconds=250,
                    language="fa",
                    text="two",
                ),
            ]
        )


def test_raw_segment_end_must_follow_start() -> None:
    with pytest.raises(ValidationError, match="segment end"):
        RawSegment(
            id="seg-0001",
            startMilliseconds=100,
            endMilliseconds=100,
            language="fa",
            text="text",
        )


def test_schema_version_other_than_one_is_rejected() -> None:
    with pytest.raises(ValidationError, match="less than or equal to 1"):
        RawTranscript(schemaVersion=2, segments=[])


def test_unknown_cleaned_source_is_rejected() -> None:
    generated = generated_minutes()
    generated.cleanedTranscript.segments[0].sourceRawSegmentIds = ["seg-9999"]
    with pytest.raises(ValueError, match="unknown source"):
        generated.validate_references(raw_transcript())


def test_unknown_minutes_evidence_is_rejected() -> None:
    generated = generated_minutes()
    generated.minutes.decisions[0].evidenceSegmentIds = ["seg-9999"]
    with pytest.raises(ValueError, match="unknown evidence"):
        generated.validate_references(raw_transcript())


def test_missing_facts_can_remain_null_or_empty() -> None:
    generated = MinutesGeneration(
        cleanedTranscript=CleanedTranscript(
            segments=[
                CleanedSegment(
                    id="clean-0001",
                    text="گفتگو",
                    sourceRawSegmentIds=["seg-0001"],
                )
            ]
        ),
        minutes=MeetingMinutes(),
    )

    generated.validate_references(raw_transcript())
    assert generated.minutes.title is None
    assert generated.minutes.actionItems == []
