"""Tests for the STT evaluation command."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from evaluate_transcript import (  # noqa: E402
    calculate_metrics,
    strip_segment_timestamps,
    write_normalized_text,
)


class EvaluateTranscriptTests(unittest.TestCase):
    def test_segment_timestamps_are_removed_line_by_line(self) -> None:
        transcript = (
            "[00:00:00.000 --> 00:00:01.250] سلام دنیا\n"
            "[00:00:01.250 --> 00:00:02.000] خط دوم"
        )
        self.assertEqual(
            strip_segment_timestamps(transcript),
            "سلام دنیا\nخط دوم",
        )

    def test_plain_text_without_timestamps_is_preserved(self) -> None:
        self.assertEqual(strip_segment_timestamps("سلام دنیا"), "سلام دنیا")

    def test_known_word_and_character_error_rates(self) -> None:
        result = calculate_metrics(
            "من کتاب دارم",
            "[00:00:00.000 --> 00:00:02.000] من دفتر دارم",
        )

        normalized_metrics = result["metrics"]["normalized"]
        normalized_errors = result["errors"]["normalized"]
        normalized_counts = result["counts"]["normalized"]
        self.assertAlmostEqual(normalized_metrics["wer"], 1 / 3)
        self.assertAlmostEqual(normalized_metrics["cer"], 4 / 10)
        self.assertEqual(normalized_errors["words"]["substitutions"], 1)
        self.assertEqual(normalized_counts["referenceWords"], 3)
        self.assertEqual(normalized_counts["referenceCharacters"], 10)

    def test_equivalent_persian_forms_have_zero_error(self) -> None:
        result = calculate_metrics(
            "يک متن، فارسی ۲۰۲۶!",
            "[00:00:00.000 --> 00:00:01.000] یک متن فارسی 2026",
        )

        self.assertGreater(result["metrics"]["raw"]["wer"], 0)
        self.assertGreater(result["metrics"]["raw"]["cer"], 0)
        self.assertEqual(result["metrics"]["normalized"]["wer"], 0)
        self.assertEqual(result["metrics"]["normalized"]["cer"], 0)

    def test_empty_inputs_have_zero_error(self) -> None:
        result = calculate_metrics("", "")

        self.assertEqual(result["metrics"]["raw"]["wer"], 0)
        self.assertEqual(result["metrics"]["raw"]["cer"], 0)
        self.assertEqual(result["metrics"]["normalized"]["wer"], 0)
        self.assertEqual(result["metrics"]["normalized"]["cer"], 0)

    def test_schema_records_both_scoring_policies(self) -> None:
        result = calculate_metrics("متن", "متن")

        self.assertEqual(result["schemaVersion"], 2)
        self.assertFalse(result["scoring"]["raw"]["contentNormalized"])
        self.assertFalse(result["scoring"]["raw"]["cerWhitespaceExcluded"])
        self.assertTrue(
            result["scoring"]["normalized"]["cerWhitespaceExcluded"]
        )

    def test_normalized_derivative_does_not_change_raw_source(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            directory = Path(temporary_directory)
            source = directory / "raw.txt"
            output = directory / "normalized.txt"
            raw = "[00:00:00.000 --> 00:00:01.000] كِتاب\u200cها ۱۲"
            source.write_text(raw, encoding="utf-8")

            write_normalized_text(
                source,
                output,
                remove_timestamps=True,
            )

            self.assertEqual(source.read_text(encoding="utf-8"), raw)
            self.assertEqual(output.read_text(encoding="utf-8"), "کتاب ها 12\n")

    def test_normalized_derivative_cannot_overwrite_raw_source(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            source = Path(temporary_directory) / "raw.txt"
            source.write_text("متن", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "cannot overwrite"):
                write_normalized_text(
                    source,
                    source,
                    remove_timestamps=False,
                )


if __name__ == "__main__":
    unittest.main()
