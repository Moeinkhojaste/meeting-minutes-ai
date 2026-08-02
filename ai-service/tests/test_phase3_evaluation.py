"""Structured Phase 3 evaluation tests."""

from __future__ import annotations

import sys
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from app.evaluation import (  # noqa: E402
    EvaluationBundle,
    HumanEvaluation,
    evaluate_pipeline,
)
from tests.phase3_helpers import (  # noqa: E402
    generated_minutes,
    raw_transcript,
)


def bundle() -> EvaluationBundle:
    generated = generated_minutes()
    return EvaluationBundle(
        rawTranscript=raw_transcript(),
        minutes=generated.minutes,
    )


def test_identical_output_has_perfect_extraction_and_evidence() -> None:
    result = evaluate_pipeline(
        bundle(),
        bundle(),
        transcription_latency_seconds=1.0,
        minutes_latency_seconds=2.0,
    )

    assert result["transcription"]["normalizedWer"] == 0.0
    assert result["extraction"]["decisions"]["f1"] == 1.0
    assert result["extraction"]["evidenceAccuracy"] == 1.0
    assert result["extraction"]["hallucinationRate"] == 0.0
    assert result["latencySeconds"]["total"] == 3.0


def test_missing_and_hallucinated_information_are_measured() -> None:
    reference = bundle()
    hypothesis = bundle()
    hypothesis.minutes.decisions[0].text = "یک تصمیم ساختگی"

    result = evaluate_pipeline(reference, hypothesis)

    assert result["extraction"]["decisions"]["precision"] == 0.0
    assert result["extraction"]["decisions"]["recall"] == 0.0
    assert result["extraction"]["hallucinationRate"] == 1.0
    assert result["extraction"]["missingInformationRate"] == 1.0


def test_timestamp_and_speaker_metrics_are_recorded() -> None:
    reference = bundle()
    hypothesis = bundle()
    hypothesis.rawTranscript.segments[0].startMilliseconds = 100
    hypothesis.rawTranscript.segments[0].endMilliseconds = 1200

    result = evaluate_pipeline(reference, hypothesis)

    assert result["transcription"]["speakerAttributionAccuracy"] == 1.0
    assert result["transcription"]["timestampMeanAbsoluteErrorMilliseconds"] == 150.0


def test_human_evaluation_is_numeric_only() -> None:
    result = evaluate_pipeline(
        bundle(),
        bundle(),
        human_evaluation=HumanEvaluation(
            coverage=4,
            factualConsistency=5,
            readability=4,
        ),
    )

    assert result["humanEvaluation"]["factualConsistency"] == 5
    assert "notes" not in result["humanEvaluation"]
