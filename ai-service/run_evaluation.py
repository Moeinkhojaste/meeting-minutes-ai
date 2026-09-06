"""End-to-end evaluation runner for Meeting Minutes AI.

Computes:
1. WER (Word Error Rate - Raw & Normalized)
2. DER (Diarization Error Rate)
3. CER (Character Error Rate - Raw & Normalized)
4. Precision (Decisions, Action Items, Participants, Assignees, Deadlines, Overall)
5. Recall (Decisions, Action Items, Participants, Assignees, Deadlines, Overall)
6. F1 Score (Decisions, Action Items, Participants, Assignees, Deadlines, Overall)
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Sequence
from pathlib import Path
from typing import Any

from app.evaluation import EvaluationBundle, evaluate_pipeline


def parse_args(args: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run evaluation on meeting transcription and structured minutes."
    )
    parser.add_argument(
        "--file",
        type=Path,
        help="Path to a single benchmark pair JSON file.",
    )
    parser.add_argument(
        "--suite",
        type=Path,
        default=Path("Datasets/benchmarks"),
        help="Directory containing benchmark pair JSON files (default: Datasets/benchmarks).",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("Evaluation/results"),
        help="Directory to save JSON and Markdown reports (default: Evaluation/results).",
    )
    return parser.parse_args(args)


def evaluate_benchmark_dict(data: dict[str, Any]) -> dict[str, Any]:
    name = data.get("name", "unnamed_meeting")
    language = data.get("language", "unknown")
    description = data.get("description", "")

    ref_bundle = EvaluationBundle.model_validate(data["reference"])
    hyp_bundle = EvaluationBundle.model_validate(data["hypothesis"])

    raw_eval = evaluate_pipeline(ref_bundle, hyp_bundle)

    # Calculate macro extraction averages across populated categories
    ext = raw_eval["extraction"]
    categories = ["decisions", "actionItems", "participants", "assignees", "deadlines"]
    f1_list = []
    precision_list = []
    recall_list = []
    for cat in categories:
        metrics = ext.get(cat, {})
        # Only include categories where ground truth or hypothesis had items
        if metrics.get("truePositives", 0) + metrics.get("falsePositives", 0) + metrics.get("falseNegatives", 0) > 0:
            precision_list.append(metrics.get("precision", 0.0))
            recall_list.append(metrics.get("recall", 0.0))
            f1_list.append(metrics.get("f1", 0.0))

    macro_p = sum(precision_list) / len(precision_list) if precision_list else 0.0
    macro_r = sum(recall_list) / len(recall_list) if recall_list else 0.0
    macro_f1 = sum(f1_list) / len(f1_list) if f1_list else 0.0

    return {
        "name": name,
        "language": language,
        "description": description,
        "metrics": {
            "wer_raw": raw_eval["transcription"]["rawWer"],
            "wer_normalized": raw_eval["transcription"]["normalizedWer"],
            "cer_raw": raw_eval["transcription"]["rawCer"],
            "cer_normalized": raw_eval["transcription"]["normalizedCer"],
            "der": raw_eval["transcription"]["diarizationErrorRate"],
            "speaker_accuracy": raw_eval["transcription"]["speakerAttributionAccuracy"],
            "timestamp_mae_ms": raw_eval["transcription"]["timestampMeanAbsoluteErrorMilliseconds"],
            "extraction": {
                "decisions": ext["decisions"],
                "actionItems": ext["actionItems"],
                "participants": ext["participants"],
                "assignees": ext["assignees"],
                "deadlines": ext["deadlines"],
                "micro_overall": ext["overall"],
                "macro_overall": {
                    "precision": macro_p,
                    "recall": macro_r,
                    "f1": macro_f1,
                },
            },
            "hallucination_rate": ext["hallucinationRate"],
            "missing_rate": ext["missingInformationRate"],
        },
        "raw_pipeline_evaluation": raw_eval,
    }


def generate_markdown_report(results: list[dict[str, Any]]) -> str:
    lines: list[str] = [
        "# Meeting Minutes AI — Evaluation Report",
        "",
        f"**Generated:** Evaluation run across {len(results)} benchmark sample(s).",
        "",
        "## 1. Speech-to-Text & Diarization Metrics (WER, CER, DER)",
        "",
        "| Sample | Language | Raw WER | Normalized WER | Raw CER | Normalized CER | DER | Speaker Acc |",
        "| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |",
    ]

    for item in results:
        m = item["metrics"]
        name = item["name"]
        lang = item["language"]
        raw_wer = f"{m['wer_raw']:.2%}"
        norm_wer = f"{m['wer_normalized']:.2%}"
        raw_cer = f"{m['cer_raw']:.2%}"
        norm_cer = f"{m['cer_normalized']:.2%}"
        der = f"{m['der']:.2%}" if m["der"] is not None else "N/A"
        spk = f"{m['speaker_accuracy']:.2%}" if m["speaker_accuracy"] is not None else "N/A"
        lines.append(f"| {name} | {lang} | {raw_wer} | **{norm_wer}** | {raw_cer} | **{norm_cer}** | **{der}** | {spk} |")

    lines.extend([
        "",
        "## 2. Information Extraction Metrics (Precision, Recall, F1)",
        "",
        "| Sample | Entity Category | Precision | Recall | F1 Score | TP | FP | FN |",
        "| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |",
    ])

    for item in results:
        name = item["name"]
        ext = item["metrics"]["extraction"]
        cats = [
            ("Decisions", ext["decisions"]),
            ("Action Items", ext["actionItems"]),
            ("Participants", ext["participants"]),
            ("Assignees", ext["assignees"]),
            ("Deadlines", ext["deadlines"]),
            ("Overall (Micro)", ext["micro_overall"]),
        ]
        for cat_name, val in cats:
            p = f"{val.get('precision', 0.0):.2%}"
            r = f"{val.get('recall', 0.0):.2%}"
            f1 = f"**{val.get('f1', 0.0):.2%}**"
            tp = val.get("truePositives", 0)
            fp = val.get("falsePositives", 0)
            fn = val.get("falseNegatives", 0)
            lines.append(f"| {name} | {cat_name} | {p} | {r} | {f1} | {tp} | {fp} | {fn} |")

    lines.extend([
        "",
        "## 3. Metrics Reference Guide",
        "",
        "- **WER (Word Error Rate)**: Measures speech recognition substitution, deletion, and insertion errors at word level. (↓ lower is better, target < 20%).",
        "- **CER (Character Error Rate)**: Measures character level errors, crucial for Persian spacing and phonetic variations. (↓ lower is better, target < 10%).",
        "- **DER (Diarization Error Rate)**: Evaluates speaker identification accuracy across time segments. (↓ lower is better, target < 20%).",
        "- **Precision**: Proportion of extracted items that were factually present in the meeting. (↑ higher is better, target > 80%).",
        "- **Recall**: Proportion of true meeting items captured by the model. (↑ higher is better, target > 80%).",
        "- **F1 Score**: Harmonic mean of Precision and Recall, measuring balanced extraction performance. (↑ higher is better, target > 0.80).",
        "",
    ])

    return "\n".join(lines)


def print_console_summary(results: list[dict[str, Any]]) -> None:
    print("\n" + "=" * 70)
    print("           MEETING MINUTES AI — EVALUATION RESULTS")
    print("=" * 70)

    for item in results:
        m = item["metrics"]
        ext = m["extraction"]
        print(f"\n[SAMPLE] {item['name']} ({item['language'].upper()})")
        print(f" Description: {item['description']}")
        print(" " + "-" * 66)
        print(" 1. Audio / STT Performance:")
        der_str = f"{m['der']:.2%}" if m["der"] is not None else "N/A"
        print(f"    - WER: Raw: {m['wer_raw']:.2%}  |  Normalized: {m['wer_normalized']:.2%}")
        print(f"    - CER: Raw: {m['cer_raw']:.2%}  |  Normalized: {m['cer_normalized']:.2%}")
        print(f"    - DER (Diarization Error Rate): {der_str}")
        if m["speaker_accuracy"] is not None:
            print(f"    - Speaker Attribution Accuracy: {m['speaker_accuracy']:.2%}")
        if m["timestamp_mae_ms"] is not None:
            print(f"    - Timestamp MAE: {m['timestamp_mae_ms']:.1f} ms")

        print(" 2. Information Extraction Performance (P / R / F1):")
        for cat, label in [
            ("decisions", "Decisions"),
            ("actionItems", "Action Items"),
            ("participants", "Participants"),
            ("assignees", "Assignees"),
            ("deadlines", "Deadlines"),
            ("micro_overall", "Overall (Micro)"),
        ]:
            val = ext[cat]
            print(
                f"    - {label:<16}: P = {val.get('precision', 0.0):.2%}  |  "
                f"R = {val.get('recall', 0.0):.2%}  |  "
                f"F1 = {val.get('f1', 0.0):.2%}"
            )
    print("\n" + "=" * 70 + "\n")


def main(args: Sequence[str] | None = None) -> int:
    parsed = parse_args(args)
    files_to_eval: list[Path] = []

    if parsed.file:
        if not parsed.file.exists():
            print(f"Error: File not found: {parsed.file}", file=sys.stderr)
            return 1
        files_to_eval.append(parsed.file)
    else:
        suite_dir = parsed.suite
        if not suite_dir.exists() or not suite_dir.is_dir():
            print(f"Error: Benchmark suite directory not found: {suite_dir}", file=sys.stderr)
            return 1
        files_to_eval = sorted(suite_dir.glob("*.json"))
        if not files_to_eval:
            print(f"Error: No .json benchmark files found in {suite_dir}", file=sys.stderr)
            return 1

    results: list[dict[str, Any]] = []
    for f in files_to_eval:
        try:
            content = json.loads(f.read_text(encoding="utf-8"))
            res = evaluate_benchmark_dict(content)
            results.append(res)
        except Exception as err:
            print(f"Error evaluating {f}: {err}", file=sys.stderr)
            return 1

    print_console_summary(results)

    # Save reports
    output_dir = parsed.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)

    json_path = output_dir / "evaluation_report.json"
    json_path.write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Saved full JSON evaluation report to: {json_path}")

    md_report = generate_markdown_report(results)
    md_path = output_dir / "evaluation_summary.md"
    md_path.write_text(md_report + "\n", encoding="utf-8")
    print(f"Saved Markdown evaluation report to: {md_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
