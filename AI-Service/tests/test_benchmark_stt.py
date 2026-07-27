"""Tests for the controlled STT benchmark helpers."""

from __future__ import annotations

import argparse
import sys
import tempfile
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from benchmark_stt import (  # noqa: E402
    parse_arguments,
    summarize_values,
    validate_benchmark_request,
)
from transcribe import SUPPORTED_MODEL_NAMES  # noqa: E402


class BenchmarkSttTests(unittest.TestCase):
    def test_all_comparison_models_are_selected_by_default(self) -> None:
        args = parse_arguments(
            [
                "sample.wav",
                "reference.txt",
                "--output",
                "result.json",
                "--dataset-version",
                "dev-v1",
            ]
        )

        self.assertEqual(args.models, list(SUPPORTED_MODEL_NAMES))

    def test_unverified_reference_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp_name:
            audio_path = Path(temp_name) / "sample.wav"
            reference_path = Path(temp_name) / "reference.txt"
            audio_path.touch()
            reference_path.touch()
            args = argparse.Namespace(
                reference_verified=False,
                warmups=1,
                runs=3,
                audio_path=audio_path,
                reference_path=reference_path,
                dataset_version="dev-v1",
            )

            with self.assertRaisesRegex(ValueError, "independently verify"):
                validate_benchmark_request(args)

    def test_three_measured_runs_are_required(self) -> None:
        with tempfile.TemporaryDirectory() as temp_name:
            audio_path = Path(temp_name) / "sample.wav"
            reference_path = Path(temp_name) / "reference.txt"
            audio_path.touch()
            reference_path.touch()
            args = argparse.Namespace(
                reference_verified=True,
                warmups=1,
                runs=2,
                audio_path=audio_path,
                reference_path=reference_path,
                dataset_version="dev-v1",
            )

            with self.assertRaisesRegex(ValueError, "At least 3"):
                validate_benchmark_request(args)

    def test_summary_reports_mean_variation_and_range(self) -> None:
        summary = summarize_values([1.0, 2.0, 3.0])

        self.assertEqual(summary["mean"], 2.0)
        self.assertAlmostEqual(summary["standardDeviation"], 0.8164965809)
        self.assertEqual(summary["minimum"], 1.0)
        self.assertEqual(summary["maximum"], 3.0)


if __name__ == "__main__":
    unittest.main()
