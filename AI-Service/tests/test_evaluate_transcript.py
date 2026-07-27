"""Tests for the STT evaluation command."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from evaluate_transcript import (  # noqa: E402
    calculate_metrics,
    strip_segment_timestamps,
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

        self.assertAlmostEqual(result["metrics"]["wer"], 1 / 3)
        self.assertAlmostEqual(result["metrics"]["cer"], 4 / 10)
        self.assertEqual(result["wordErrors"]["substitutions"], 1)
        self.assertEqual(result["counts"]["referenceWords"], 3)
        self.assertEqual(result["counts"]["referenceCharacters"], 10)

    def test_equivalent_persian_forms_have_zero_error(self) -> None:
        result = calculate_metrics(
            "يک متن، فارسی!",
            "[00:00:00.000 --> 00:00:01.000] یک متن فارسی",
        )

        self.assertEqual(result["metrics"]["wer"], 0)
        self.assertEqual(result["metrics"]["cer"], 0)

    def test_empty_inputs_have_zero_error(self) -> None:
        result = calculate_metrics("", "")

        self.assertEqual(result["metrics"]["wer"], 0)
        self.assertEqual(result["metrics"]["cer"], 0)


if __name__ == "__main__":
    unittest.main()
