from __future__ import annotations

import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from aggregate_evaluations import (  # noqa: E402
    aggregate_records,
    build_record,
    parse_entry,
    write_outputs,
)
from evaluate_transcript import calculate_metrics  # noqa: E402


class AggregateEvaluationTests(unittest.TestCase):
    def setUp(self) -> None:
        evaluation = calculate_metrics("سلام دنیا", "سلام جهان")
        self.record = build_record(
            "fixture-dev-001",
            "development",
            evaluation,
        )

    def test_record_contains_no_transcript_text_or_paths(self) -> None:
        serialized = json.dumps(self.record, ensure_ascii=False)

        self.assertNotIn("سلام", serialized)
        self.assertNotIn("reference", serialized.lower())
        self.assertNotIn("path", serialized.lower())

    def test_aggregate_calculates_macro_metrics_and_error_totals(self) -> None:
        aggregate = aggregate_records("fixture-v1", [self.record])

        self.assertEqual(aggregate["recordCount"], 1)
        self.assertEqual(aggregate["splitCounts"]["development"], 1)
        self.assertEqual(
            aggregate["macroAverage"]["normalizedWer"],
            self.record["normalizedWer"],
        )
        self.assertEqual(
            aggregate["errorTotals"]["normalizedWordSubstitutions"],
            1,
        )

    def test_duplicate_recording_ids_are_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "must be unique"):
            aggregate_records("fixture-v1", [self.record, self.record])

    def test_entry_parser_preserves_windows_drive_colon(self) -> None:
        recording_id, split, path = parse_entry(
            r"dev-001:development:C:\results\dev-001.json"
        )

        self.assertEqual(recording_id, "dev-001")
        self.assertEqual(split, "development")
        self.assertEqual(path, Path(r"C:\results\dev-001.json"))

    def test_json_and_csv_outputs_have_only_approved_fields(self) -> None:
        aggregate = aggregate_records("fixture-v1", [self.record])
        with tempfile.TemporaryDirectory() as temporary_directory:
            json_path = Path(temporary_directory) / "aggregate.json"
            csv_path = Path(temporary_directory) / "aggregate.csv"
            write_outputs(aggregate, json_path, csv_path)

            restored = json.loads(json_path.read_text(encoding="utf-8"))
            with csv_path.open(encoding="utf-8", newline="") as stream:
                rows = list(csv.DictReader(stream))

        self.assertEqual(restored["datasetVersion"], "fixture-v1")
        self.assertEqual(rows[0]["recordingId"], "fixture-dev-001")
        self.assertNotIn("transcript", json.dumps(restored).lower())


if __name__ == "__main__":
    unittest.main()
