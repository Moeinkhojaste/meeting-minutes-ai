"""Structured Phase 3 evaluation without copying transcript text to results."""

from __future__ import annotations

from typing import Any, cast

from pydantic import Field

from app.schemas import MeetingMinutes, RawTranscript, StrictModel
from evaluate_transcript import calculate_metrics
from persian_text import normalize_persian_text


class EvaluationBundle(StrictModel):
    rawTranscript: RawTranscript
    minutes: MeetingMinutes


class HumanEvaluation(StrictModel):
    coverage: int = Field(ge=1, le=5)
    factualConsistency: int = Field(ge=1, le=5)
    readability: int = Field(ge=1, le=5)


def evaluate_pipeline(
    reference: EvaluationBundle,
    hypothesis: EvaluationBundle,
    *,
    transcription_latency_seconds: float | None = None,
    minutes_latency_seconds: float | None = None,
    human_evaluation: HumanEvaluation | None = None,
) -> dict[str, Any]:
    """Calculate transcript, diarization, extraction, and evidence metrics."""
    reference_text = "\n".join(
        segment.text for segment in reference.rawTranscript.segments
    )
    hypothesis_text = "\n".join(
        segment.text for segment in hypothesis.rawTranscript.segments
    )
    text_metrics = calculate_metrics(reference_text, hypothesis_text)
    metric_sets = cast(dict[str, dict[str, float]], text_metrics["metrics"])
    aligned = _aligned_segments(
        reference.rawTranscript,
        hypothesis.rawTranscript,
    )
    speaker_correct = sum(
        reference_segment.speaker == hypothesis_segment.speaker
        for reference_segment, hypothesis_segment in aligned
    )
    timestamp_errors = [
        abs(reference_segment.startMilliseconds - hypothesis_segment.startMilliseconds)
        for reference_segment, hypothesis_segment in aligned
    ] + [
        abs(reference_segment.endMilliseconds - hypothesis_segment.endMilliseconds)
        for reference_segment, hypothesis_segment in aligned
    ]

    reference_decisions = _facts(
        (item.text, item.evidenceSegmentIds)
        for item in reference.minutes.decisions
    )
    hypothesis_decisions = _facts(
        (item.text, item.evidenceSegmentIds)
        for item in hypothesis.minutes.decisions
    )
    reference_actions = _facts(
        (item.task, item.evidenceSegmentIds)
        for item in reference.minutes.actionItems
    )
    hypothesis_actions = _facts(
        (item.task, item.evidenceSegmentIds)
        for item in hypothesis.minutes.actionItems
    )
    decision_metrics = _extraction_metrics(
        reference_decisions,
        hypothesis_decisions,
    )
    action_metrics = _extraction_metrics(
        reference_actions,
        hypothesis_actions,
    )
    reference_all = reference_decisions | reference_actions
    hypothesis_all = hypothesis_decisions | hypothesis_actions
    matched_all = set(reference_all) & set(hypothesis_all)

    return {
        "schemaVersion": 1,
        "transcription": {
            "rawWer": metric_sets["raw"]["wer"],
            "rawCer": metric_sets["raw"]["cer"],
            "normalizedWer": metric_sets["normalized"]["wer"],
            "normalizedCer": metric_sets["normalized"]["cer"],
            "speakerAttributionAccuracy": (
                speaker_correct / len(aligned) if aligned else None
            ),
            "diarizationErrorRate": _diarization_error_rate(
                reference.rawTranscript,
                hypothesis.rawTranscript,
            ),
            "timestampMeanAbsoluteErrorMilliseconds": (
                sum(timestamp_errors) / len(timestamp_errors)
                if timestamp_errors
                else None
            ),
        },
        "extraction": {
            "decisions": decision_metrics,
            "actionItems": action_metrics,
            "evidenceAccuracy": _evidence_accuracy(
                reference_all,
                hypothesis_all,
                matched_all,
            ),
            "hallucinationRate": (
                (len(hypothesis_all) - len(matched_all)) / len(hypothesis_all)
                if hypothesis_all
                else 0.0
            ),
            "missingInformationRate": (
                (len(reference_all) - len(matched_all)) / len(reference_all)
                if reference_all
                else 0.0
            ),
        },
        "latencySeconds": {
            "transcription": transcription_latency_seconds,
            "minutes": minutes_latency_seconds,
            "total": (
                transcription_latency_seconds + minutes_latency_seconds
                if transcription_latency_seconds is not None
                and minutes_latency_seconds is not None
                else None
            ),
        },
        "humanEvaluation": (
            human_evaluation.model_dump() if human_evaluation else None
        ),
    }


def _aligned_segments(
    reference: RawTranscript,
    hypothesis: RawTranscript,
) -> list[tuple[Any, Any]]:
    hypothesis_by_id = {segment.id: segment for segment in hypothesis.segments}
    return [
        (segment, hypothesis_by_id[segment.id])
        for segment in reference.segments
        if segment.id in hypothesis_by_id
    ]


def _facts(items: Any) -> dict[str, frozenset[str]]:
    return {
        normalize_persian_text(text): frozenset(evidence)
        for text, evidence in items
    }


def _extraction_metrics(
    reference: dict[str, frozenset[str]],
    hypothesis: dict[str, frozenset[str]],
) -> dict[str, float]:
    true_positives = len(set(reference) & set(hypothesis))
    precision = true_positives / len(hypothesis) if hypothesis else 0.0
    recall = true_positives / len(reference) if reference else 0.0
    f1 = (
        2 * precision * recall / (precision + recall)
        if precision + recall
        else 0.0
    )
    return {"precision": precision, "recall": recall, "f1": f1}


def _evidence_accuracy(
    reference: dict[str, frozenset[str]],
    hypothesis: dict[str, frozenset[str]],
    matched: set[str],
) -> float | None:
    if not matched:
        return None
    correct = sum(
        bool(reference[key] & hypothesis[key])
        for key in matched
    )
    return correct / len(matched)


def _diarization_error_rate(
    reference: RawTranscript,
    hypothesis: RawTranscript,
) -> float | None:
    if not any(segment.speaker for segment in reference.segments):
        return None
    try:
        from pyannote.core import Annotation, Segment
        from pyannote.metrics.diarization import DiarizationErrorRate
    except ImportError:
        return None

    reference_annotation = Annotation()
    hypothesis_annotation = Annotation()
    for segment in reference.segments:
        if segment.speaker:
            reference_annotation[
                Segment(
                    segment.startMilliseconds / 1000,
                    segment.endMilliseconds / 1000,
                )
            ] = segment.speaker
    for segment in hypothesis.segments:
        if segment.speaker:
            hypothesis_annotation[
                Segment(
                    segment.startMilliseconds / 1000,
                    segment.endMilliseconds / 1000,
                )
            ] = segment.speaker
    return float(DiarizationErrorRate()(reference_annotation, hypothesis_annotation))
