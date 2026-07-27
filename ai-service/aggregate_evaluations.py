"""Create transcript-free aggregate STT evaluation JSON and CSV outputs."""

from __future__ import annotations

import argparse
import csv
import json
import statistics
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

ALLOWED_SPLITS = {"development", "final-test"}
CSV_FIELDS = [
    "recordingId",
    "split",
    "rawWer",
    "rawCer",
    "normalizedWer",
    "normalizedCer",
    "rawWordSubstitutions",
    "rawWordDeletions",
    "rawWordInsertions",
    "normalizedWordSubstitutions",
    "normalizedWordDeletions",
    "normalizedWordInsertions",
]


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="Aggregate private STT results without transcript text.",
    )
    parser.add_argument("--dataset-version", required=True)
    parser.add_argument(
        "--entry",
        action="append",
        required=True,
        metavar="RECORDING_ID:SPLIT:RESULT_JSON",
    )
    parser.add_argument("--json-output", type=Path, required=True)
    parser.add_argument("--csv-output", type=Path, required=True)
    return parser.parse_args(arguments)


def build_record(
    recording_id: str,
    split: str,
    evaluation: Mapping[str, Any],
) -> dict[str, str | float | int]:
    """Extract only non-sensitive numeric fields from one evaluator result."""
    if not recording_id.strip():
        raise ValueError("recordingId cannot be empty.")
    if split not in ALLOWED_SPLITS:
        raise ValueError(f"Unsupported split: {split}.")
    metrics = _mapping(evaluation.get("metrics"), "metrics")
    errors = _mapping(evaluation.get("errors"), "errors")
    raw_metrics = _mapping(metrics.get("raw"), "metrics.raw")
    normalized_metrics = _mapping(
        metrics.get("normalized"),
        "metrics.normalized",
    )
    raw_words = _mapping(
        _mapping(errors.get("raw"), "errors.raw").get("words"),
        "errors.raw.words",
    )
    normalized_words = _mapping(
        _mapping(errors.get("normalized"), "errors.normalized").get("words"),
        "errors.normalized.words",
    )

    return {
        "recordingId": recording_id,
        "split": split,
        "rawWer": _number(raw_metrics.get("wer"), "metrics.raw.wer"),
        "rawCer": _number(raw_metrics.get("cer"), "metrics.raw.cer"),
        "normalizedWer": _number(
            normalized_metrics.get("wer"),
            "metrics.normalized.wer",
        ),
        "normalizedCer": _number(
            normalized_metrics.get("cer"),
            "metrics.normalized.cer",
        ),
        "rawWordSubstitutions": _integer(
            raw_words.get("substitutions"),
            "errors.raw.words.substitutions",
        ),
        "rawWordDeletions": _integer(
            raw_words.get("deletions"),
            "errors.raw.words.deletions",
        ),
        "rawWordInsertions": _integer(
            raw_words.get("insertions"),
            "errors.raw.words.insertions",
        ),
        "normalizedWordSubstitutions": _integer(
            normalized_words.get("substitutions"),
            "errors.normalized.words.substitutions",
        ),
        "normalizedWordDeletions": _integer(
            normalized_words.get("deletions"),
            "errors.normalized.words.deletions",
        ),
        "normalizedWordInsertions": _integer(
            normalized_words.get("insertions"),
            "errors.normalized.words.insertions",
        ),
    }


def aggregate_records(
    dataset_version: str,
    records: Sequence[Mapping[str, str | float | int]],
) -> dict[str, Any]:
    """Build macro averages and preserve only approved aggregate fields."""
    if not dataset_version.strip():
        raise ValueError("datasetVersion cannot be empty.")
    if not records:
        raise ValueError("At least one evaluation record is required.")
    unique_ids = {str(record["recordingId"]) for record in records}
    if len(unique_ids) != len(records):
        raise ValueError("recordingId values must be unique.")

    metric_fields = ("rawWer", "rawCer", "normalizedWer", "normalizedCer")
    error_fields = tuple(field for field in CSV_FIELDS if "Word" in field)
    return {
        "schemaVersion": 1,
        "datasetVersion": dataset_version,
        "recordCount": len(records),
        "splitCounts": {
            split: sum(record["split"] == split for record in records)
            for split in sorted(ALLOWED_SPLITS)
        },
        "macroAverage": {
            field: statistics.fmean(float(record[field]) for record in records)
            for field in metric_fields
        },
        "errorTotals": {
            field: sum(int(record[field]) for record in records)
            for field in error_fields
        },
        "records": list(records),
    }


def parse_entry(value: str) -> tuple[str, str, Path]:
    """Parse a Windows-safe entry by splitting only the first two colons."""
    parts = value.split(":", 2)
    if len(parts) != 3:
        raise ValueError(
            "Entry must use RECORDING_ID:SPLIT:RESULT_JSON format."
        )
    recording_id, split, result_path = parts
    return recording_id, split, Path(result_path)


def write_outputs(
    aggregate: Mapping[str, Any],
    json_output: Path,
    csv_output: Path,
) -> None:
    """Write reusable UTF-8 JSON and CSV without private transcript text."""
    json_output.parent.mkdir(parents=True, exist_ok=True)
    csv_output.parent.mkdir(parents=True, exist_ok=True)
    json_output.write_text(
        json.dumps(aggregate, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    with csv_output.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=CSV_FIELDS)
        writer.writeheader()
        writer.writerows(aggregate["records"])


def _mapping(value: Any, field_name: str) -> Mapping[str, Any]:
    if not isinstance(value, Mapping):
        raise ValueError(f"{field_name} must be an object.")
    return value


def _number(value: Any, field_name: str) -> float:
    if not isinstance(value, int | float) or isinstance(value, bool):
        raise ValueError(f"{field_name} must be numeric.")
    return float(value)


def _integer(value: Any, field_name: str) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value < 0:
        raise ValueError(f"{field_name} must be a non-negative integer.")
    return value


def main(arguments: Sequence[str] | None = None) -> int:
    """Run the aggregate-output command."""
    args = parse_arguments(arguments)
    try:
        records = []
        for entry_value in args.entry:
            recording_id, split, result_path = parse_entry(entry_value)
            result = json.loads(
                result_path.expanduser().resolve().read_text(encoding="utf-8")
            )
            records.append(
                build_record(
                    recording_id,
                    split,
                    _mapping(result, "evaluation"),
                )
            )
        aggregate = aggregate_records(args.dataset_version, records)
        write_outputs(
            aggregate,
            args.json_output.expanduser().resolve(),
            args.csv_output.expanduser().resolve(),
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        print(f"Error: {error}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
