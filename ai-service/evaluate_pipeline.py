"""Evaluate structured transcript and minutes output against a reference."""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Sequence
from pathlib import Path

from app.evaluation import EvaluationBundle, evaluate_pipeline


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Evaluate structured Phase 3 output without copying content."
    )
    parser.add_argument("reference_path", type=Path)
    parser.add_argument("hypothesis_path", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--transcription-latency-seconds", type=float)
    parser.add_argument("--minutes-latency-seconds", type=float)
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    args = parse_arguments(arguments)
    try:
        reference = EvaluationBundle.model_validate_json(
            args.reference_path.read_text(encoding="utf-8")
        )
        hypothesis = EvaluationBundle.model_validate_json(
            args.hypothesis_path.read_text(encoding="utf-8")
        )
        result = evaluate_pipeline(
            reference,
            hypothesis,
            transcription_latency_seconds=args.transcription_latency_seconds,
            minutes_latency_seconds=args.minutes_latency_seconds,
        )
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(result, indent=2) + "\n",
            encoding="utf-8",
        )
        print("Evaluation completed.")
        return 0
    except (OSError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
