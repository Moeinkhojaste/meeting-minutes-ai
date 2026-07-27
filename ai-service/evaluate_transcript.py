"""Evaluate a timestamped STT transcript against a human reference."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections.abc import Sequence
from importlib.metadata import version
from pathlib import Path

from jiwer import process_characters, process_words

from persian_text import create_text_variants, remove_whitespace

TIMESTAMP_PREFIX_PATTERN = re.compile(
    r"^\s*\[\d{2}:\d{2}:\d{2}(?:\.\d{3})?"
    r"\s*-->\s*"
    r"\d{2}:\d{2}:\d{2}(?:\.\d{3})?\]\s*"
)
REFERENCE_SPEAKER_LABEL_PATTERN = re.compile(r"^\s*[^:\n]{1,40}:\s*")


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
    parser.add_argument(
        "--normalized-reference-output",
        type=Path,
        help="Optional path for the derived normalized reference text",
    )
    parser.add_argument(
        "--normalized-hypothesis-output",
        type=Path,
        help="Optional path for the derived normalized hypothesis text",
    )
    parser.add_argument(
        "--reference-speaker-labels",
        action="store_true",
        help=(
            "Remove an explicit speaker-label prefix ending in ':' from "
            "each reference line before scoring."
        ),
    )
    return parser.parse_args(arguments)


def strip_segment_timestamps(text: str) -> str:
    """Remove a timestamp prefix from every transcript line."""
    return "\n".join(
        TIMESTAMP_PREFIX_PATTERN.sub("", line)
        for line in text.splitlines()
    )


def strip_reference_speaker_labels(text: str) -> str:
    """Remove one explicit speaker annotation from each reference line."""
    return "\n".join(
        REFERENCE_SPEAKER_LABEL_PATTERN.sub("", line)
        for line in text.splitlines()
    )


def calculate_metrics(
    reference: str,
    hypothesis: str,
    *,
    reference_speaker_labels: bool = False,
) -> dict[str, object]:
    """Return raw and normalized WER/CER without mutating either input."""
    scoring_reference = (
        strip_reference_speaker_labels(reference)
        if reference_speaker_labels
        else reference
    )
    raw_hypothesis = strip_segment_timestamps(hypothesis)
    reference_variants = create_text_variants(scoring_reference)
    hypothesis_variants = create_text_variants(raw_hypothesis)
    raw = _calculate_metric_set(
        reference_variants.raw.strip(),
        hypothesis_variants.raw.strip(),
        exclude_character_whitespace=False,
    )
    normalized = _calculate_metric_set(
        reference_variants.normalized,
        hypothesis_variants.normalized,
        exclude_character_whitespace=True,
    )

    return {
        "schemaVersion": 2,
        "library": {
            "name": "jiwer",
            "version": version("jiwer"),
        },
        "normalization": {
            "unicodeForm": "NFKC",
            "arabicToPersianCharacters": ["ي→ی", "ى→ی", "ك→ک"],
            "arabicDiacriticsRemoved": True,
            "tatweelRemoved": True,
            "punctuationAsSpace": True,
            "zeroWidthNonJoinerAsSpace": True,
            "whitespaceCollapsed": True,
            "latinLowercased": True,
            "persianAndArabicDigitsToAscii": True,
            "spokenNumbersRewritten": False,
        },
        "metrics": {
            "raw": raw["metrics"],
            "normalized": normalized["metrics"],
        },
        "counts": {
            "raw": raw["counts"],
            "normalized": normalized["counts"],
        },
        "errors": {
            "raw": raw["errors"],
            "normalized": normalized["errors"],
        },
        "scoring": {
            "raw": {
                "timestampPrefixesRemoved": True,
                "referenceSpeakerLabelsRemoved": reference_speaker_labels,
                "outerWhitespaceTrimmed": True,
                "contentNormalized": False,
                "cerWhitespaceExcluded": False,
            },
            "normalized": {
                "timestampPrefixesRemoved": True,
                "referenceSpeakerLabelsRemoved": reference_speaker_labels,
                "policyApplied": True,
                "cerWhitespaceExcluded": True,
            },
        },
    }


def _calculate_metric_set(
    reference: str,
    hypothesis: str,
    *,
    exclude_character_whitespace: bool,
) -> dict[str, object]:
    """Calculate one internally consistent WER/CER metric set."""
    reference_characters = (
        remove_whitespace(reference)
        if exclude_character_whitespace
        else reference
    )
    hypothesis_characters = (
        remove_whitespace(hypothesis)
        if exclude_character_whitespace
        else hypothesis
    )
    word_result = process_words(reference, hypothesis)
    character_result = process_characters(
        reference_characters,
        hypothesis_characters,
    )

    return {
        "metrics": {
            "wer": word_result.wer,
            "cer": character_result.cer,
        },
        "counts": {
            "referenceWords": len(reference.split()),
            "hypothesisWords": len(hypothesis.split()),
            "referenceCharacters": len(reference_characters),
            "hypothesisCharacters": len(hypothesis_characters),
        },
        "errors": {
            "words": {
                "hits": word_result.hits,
                "substitutions": word_result.substitutions,
                "deletions": word_result.deletions,
                "insertions": word_result.insertions,
            },
            "characters": {
                "hits": character_result.hits,
                "substitutions": character_result.substitutions,
                "deletions": character_result.deletions,
                "insertions": character_result.insertions,
            },
        },
    }


def evaluate_files(
    reference_path: Path,
    hypothesis_path: Path,
    *,
    reference_speaker_labels: bool = False,
) -> dict[str, object]:
    """Read two UTF-8 transcript files and calculate their metrics."""
    _validate_text_file(reference_path, "Reference")
    _validate_text_file(hypothesis_path, "Hypothesis")
    return calculate_metrics(
        reference_path.read_text(encoding="utf-8"),
        hypothesis_path.read_text(encoding="utf-8"),
        reference_speaker_labels=reference_speaker_labels,
    )


def write_normalized_text(
    source_path: Path,
    output_path: Path,
    *,
    remove_timestamps: bool,
    remove_speaker_labels: bool = False,
) -> None:
    """Write a normalized derivative while leaving the raw source unchanged."""
    resolved_source = source_path.resolve()
    resolved_output = output_path.expanduser().resolve()
    if resolved_output == resolved_source:
        raise ValueError("Normalized output cannot overwrite its raw source.")

    raw_text = resolved_source.read_text(encoding="utf-8")
    if remove_timestamps:
        raw_text = strip_segment_timestamps(raw_text)
    if remove_speaker_labels:
        raw_text = strip_reference_speaker_labels(raw_text)
    normalized_text = create_text_variants(raw_text).normalized
    resolved_output.parent.mkdir(parents=True, exist_ok=True)
    resolved_output.write_text(normalized_text + "\n", encoding="utf-8")


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
        result = evaluate_files(
            reference_path,
            hypothesis_path,
            reference_speaker_labels=args.reference_speaker_labels,
        )
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
        if args.normalized_reference_output:
            write_normalized_text(
                reference_path,
                args.normalized_reference_output,
                remove_timestamps=False,
                remove_speaker_labels=args.reference_speaker_labels,
            )
        if args.normalized_hypothesis_output:
            write_normalized_text(
                hypothesis_path,
                args.normalized_hypothesis_output,
                remove_timestamps=True,
            )
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
