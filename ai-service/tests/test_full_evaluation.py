"""Comprehensive unit tests for the 6 core evaluation metrics:
WER, DER, CER, Precision, Recall, and F1 Score.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

AI_SERVICE_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIR))

from app.evaluation import EvaluationBundle, evaluate_pipeline
from app.schemas import ActionItem, Decision, MeetingMinutes, Participant, RawSegment, RawTranscript
from run_evaluation import evaluate_benchmark_dict


def sample_bundle(
    *,
    speaker1: str = "Alice",
    speaker2: str = "Bob",
    seg1_text: str = "We approve the budget for next month.",
    seg2_text: str = "I will write the test cases by Friday.",
    decisions: list[str] | None = None,
    action_tasks: list[tuple[str, str | None, str | None]] | None = None,
    participants: list[str] | None = None,
) -> EvaluationBundle:
    raw = RawTranscript(
        segments=[
            RawSegment(
                id="seg-0001",
                speaker=speaker1,
                startMilliseconds=0,
                endMilliseconds=5000,
                language="en",
                text=seg1_text,
            ),
            RawSegment(
                id="seg-0002",
                speaker=speaker2,
                startMilliseconds=5000,
                endMilliseconds=10000,
                language="en",
                text=seg2_text,
            ),
        ]
    )

    decs = [
        Decision(text=d, evidenceSegmentIds=["seg-0001"])
        for d in (decisions if decisions is not None else ["We approve the budget for next month."])
    ]

    tasks = action_tasks if action_tasks is not None else [
        ("Write the test cases", "Bob", "Friday")
    ]
    actions = [
        ActionItem(task=t, assignee=a, deadline=d, evidenceSegmentIds=["seg-0002"])
        for t, a, d in tasks
    ]

    parts = [
        Participant(name=p, evidenceSegmentIds=["seg-0001"])
        for p in (participants if participants is not None else ["Alice", "Bob"])
    ]

    minutes = MeetingMinutes(
        title="Planning Meeting",
        date="2026-09-06",
        summary="Brief summary",
        participants=parts,
        decisions=decs,
        actionItems=actions,
    )

    return EvaluationBundle(rawTranscript=raw, minutes=minutes)


def test_identical_bundles_produce_perfect_zero_errors_and_ones_scores() -> None:
    bundle = sample_bundle()
    result = evaluate_pipeline(bundle, bundle)

    # 1. WER & CER
    assert result["transcription"]["rawWer"] == 0.0
    assert result["transcription"]["normalizedWer"] == 0.0
    assert result["transcription"]["rawCer"] == 0.0
    assert result["transcription"]["normalizedCer"] == 0.0

    # 2. DER
    assert result["transcription"]["diarizationErrorRate"] == 0.0
    assert result["transcription"]["speakerAttributionAccuracy"] == 1.0

    # 3. Precision, Recall, F1 for extraction
    ext = result["extraction"]
    for category in ["decisions", "actionItems", "participants", "assignees", "deadlines", "overall"]:
        assert ext[category]["precision"] == 1.0
        assert ext[category]["recall"] == 1.0
        assert ext[category]["f1"] == 1.0


def test_wer_and_cer_detect_transcription_mutations() -> None:
    ref = sample_bundle(seg1_text="The quick brown fox jumps over the lazy dog.")
    hyp = sample_bundle(seg1_text="The quick brown dog jumps over lazy dog.")

    result = evaluate_pipeline(ref, hyp)

    assert result["transcription"]["rawWer"] > 0.0
    assert result["transcription"]["rawCer"] > 0.0


def test_der_detects_speaker_confusion() -> None:
    # Reference has Alice for seg1 and Alice for seg2
    ref = sample_bundle(speaker1="Alice", speaker2="Alice")
    # Hypothesis confuses seg2 as Bob
    hyp = sample_bundle(speaker1="Alice", speaker2="Bob")

    result = evaluate_pipeline(ref, hyp)

    assert result["transcription"]["diarizationErrorRate"] is not None
    assert result["transcription"]["diarizationErrorRate"] > 0.0
    assert result["transcription"]["speakerAttributionAccuracy"] == 0.5


def test_precision_recall_f1_with_false_positive_and_false_negative() -> None:
    # Reference has 2 decisions: D1 and D2
    ref = sample_bundle(decisions=["Approve budget", "Hire senior engineer"])
    # Hypothesis has 2 decisions: D1 and D3 (D3 is false positive, D2 is false negative)
    hyp = sample_bundle(decisions=["Approve budget", "Buy new laptops"])

    result = evaluate_pipeline(ref, hyp)
    dec_metrics = result["extraction"]["decisions"]

    # TP = 1 (Approve budget), FP = 1 (Buy new laptops), FN = 1 (Hire senior engineer)
    assert dec_metrics["truePositives"] == 1
    assert dec_metrics["falsePositives"] == 1
    assert dec_metrics["falseNegatives"] == 1
    assert dec_metrics["precision"] == 0.5
    assert dec_metrics["recall"] == 0.5
    assert dec_metrics["f1"] == 0.5


def test_evaluate_benchmark_dict_on_persian_and_english_files() -> None:
    repo_root = Path(__file__).resolve().parents[2]
    bench_dir = repo_root / "Datasets" / "benchmarks"

    persian_file = bench_dir / "persian_sprint_meeting.json"
    assert persian_file.exists()
    fa_data = json.loads(persian_file.read_text(encoding="utf-8"))
    fa_eval = evaluate_benchmark_dict(fa_data)

    assert fa_eval["name"] == "persian_sprint_meeting"
    assert fa_eval["language"] == "fa"
    # Diarization error rate is minimal because speakers are correctly identified (under 5% with slight boundary jitter)
    assert fa_eval["metrics"]["der"] < 0.05
    # Normalized WER and CER are low
    assert fa_eval["metrics"]["wer_normalized"] < 0.20
    assert fa_eval["metrics"]["cer_normalized"] < 0.10
    # Decisions and Actions F1 are 1.0
    assert fa_eval["metrics"]["extraction"]["decisions"]["f1"] == 1.0
    assert fa_eval["metrics"]["extraction"]["actionItems"]["f1"] == 1.0

    english_file = bench_dir / "english_product_meeting.json"
    assert english_file.exists()
    en_data = json.loads(english_file.read_text(encoding="utf-8"))
    en_eval = evaluate_benchmark_dict(en_data)

    assert en_eval["name"] == "english_product_meeting"
    assert en_eval["language"] == "en"
    assert en_eval["metrics"]["der"] < 0.05
    assert en_eval["metrics"]["wer_normalized"] < 0.20
    assert en_eval["metrics"]["extraction"]["decisions"]["f1"] == 1.0
