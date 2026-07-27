"""Evaluate a timestamped STT transcript against a human reference."""

from __future__ import annotations

import argparse
import json
import re
import sys
from importlib.metadata import version
from pathlib import Path
from typing import Sequence

from jiwer import process_characters, process_words

from persian_text import normalize_persian_text, remove_whitespace

TIMESTAMP_PREFIX_PATTERN = re.compile(
    r"^\s*\[\d{2}:\d{2}:\d{2}(?:\.\d{3})?"
    r"\s*-->\s*"
    r"\d{2}:\d{2}:\d{2}(?:\.\d{3})?\]\s*"
)


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description=(
            "Calculate Persian WER and whitespace-independent CER for a "
            "human reference and a timestamped STT transcript."
        )
    )
    parser.add_argument(
        "reference_path",
        type=Path,
        help="UTF-8 human reference transcript without timestamps",
    )
    parser.add_argument(
        "hypothesis_path",
        type=Path,
        help="UTF-8 generated transcript, optionally with segment timestamps",
    )
    parser.add_argument(
        "--output",
        type=Path,
        help="Optional path for the JSON result",
    )
    return parser.parse_args(arguments)


def strip_segment_timestamps(text: str) -> str:
    """Remove a timestamp prefix from every transcript line."""
    return "\n".join(
        TIMESTAMP_PREFIX_PATTERN.sub("", line)
        for line in text.splitlines()
    )


def calculate_metrics(reference: str, hypothesis: str) -> dict[str, object]:
    """Normalize two transcripts and return reproducible WER/CER details."""
    normalized_reference = normalize_persian_text(reference)
    normalized_hypothesis = normalize_persian_text(
        strip_segment_timestamps(hypothesis)
    )
    reference_characters = remove_whitespace(normalized_reference)
    hypothesis_characters = remove_whitespace(normalized_hypothesis)

    word_result = process_words(normalized_reference, normalized_hypothesis)
    character_result = process_characters(
        reference_characters,
        hypothesis_characters,
    )

    return {
        "schemaVersion": 1,
        "library": {
            "name": "jiwer",
            "version": version("jiwer"),
        },
        "normalization": {
            "unicodeForm": "NFKC",
            "arabicToPersianCharacters": ["ي→ی", "ى→ی", "ك→ک"],
            "punctuationRemoved": True,
            "zeroWidthNonJoinerAsSpace": True,
            "whitespaceCollapsed": True,
            "cerWhitespaceExcluded": True,
        },
        "metrics": {
            "wer": word_result.wer,
            "cer": character_result.cer,
        },
        "counts": {
            "referenceWords": len(normalized_reference.split()),
            "hypothesisWords": len(normalized_hypothesis.split()),
            "referenceCharacters": len(reference_characters),
            "hypothesisCharacters": len(hypothesis_characters),
        },
        "wordErrors": {
            "hits": word_result.hits,
            "substitutions": word_result.substitutions,
            "deletions": word_result.deletions,
            "insertions": word_result.insertions,
        },
        "characterErrors": {
            "hits": character_result.hits,
            "substitutions": character_result.substitutions,
            "deletions": character_result.deletions,
            "insertions": character_result.insertions,
        },
    }


def evaluate_files(
    reference_path: Path,
    hypothesis_path: Path,
) -> dict[str, object]:
    """Read two UTF-8 transcript files and calculate their metrics."""
    _validate_text_file(reference_path, "Reference")
    _validate_text_file(hypothesis_path, "Hypothesis")
    return calculate_metrics(
        reference_path.read_text(encoding="utf-8"),
        hypothesis_path.read_text(encoding="utf-8"),
    )


def _validate_text_file(path: Path, label: str) -> None:
    """Raise a helpful error when an input is not a readable local file."""
    if not path.exists():
        raise ValueError(f"{label} file does not exist: {path}")
    if not path.is_file():
        raise ValueError(f"{label} path is not a file: {path}")


def main(arguments: Sequence[str] | None = None) -> int:
    """Run the evaluation command."""
    _configure_utf8_console()
    args = parse_arguments(arguments)
    reference_path = args.reference_path.expanduser().resolve()
    hypothesis_path = args.hypothesis_path.expanduser().resolve()

    try:
        result = evaluate_files(reference_path, hypothesis_path)
        rendered_result = json.dumps(result, ensure_ascii=False, indent=2)
        print(rendered_result)
        if args.output:
            output_path = args.output.expanduser().resolve()
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_text(
                rendered_result + "\n",
                encoding="utf-8",
            )
            print(f"Result saved to: {output_path}")
    except (OSError, UnicodeError, ValueError) as error:
        print(f"Error: {error}")
        return 1

    return 0


def _configure_utf8_console() -> None:
    """Use UTF-8 for Persian output on supported Python consoles."""
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
