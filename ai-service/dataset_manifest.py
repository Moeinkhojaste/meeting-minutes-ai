"""Validate the private evaluation dataset manifest without reading media."""

from __future__ import annotations

import argparse
import json
import re
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any, cast

RECORDING_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]{2,63}$")
SHA256_PATTERN = re.compile(r"^[a-f0-9]{64}$")
SPLITS = {"development", "final-test"}
SOURCE_TYPES = {"recorded", "generated"}
SPEECH_STYLES = {"read", "monologue", "conversational"}
OVERLAP_LEVELS = {"none", "light", "material"}
NOISE_LEVELS = {"clean", "light", "moderate", "heavy"}
USAGE_RIGHTS = {
    "evaluation-only",
    "private-project",
    "redistributable-generated",
}
PUBLICATION_STATUSES = {
    "private",
    "anonymized-metadata-only",
    "redistributable",
}


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="Validate a Meeting Minutes AI dataset manifest.",
    )
    parser.add_argument("manifest_path", type=Path)
    return parser.parse_args(arguments)


def validate_manifest(manifest: Mapping[str, Any]) -> dict[str, int]:
    """Validate required fields and return non-sensitive split counts."""
    _require(manifest.get("schemaVersion") == 1, "schemaVersion must be 1.")
    _require_text(manifest.get("datasetVersion"), "datasetVersion")
    _require(
        manifest.get("status") in {"draft", "locked"},
        "status must be draft or locked.",
    )

    privacy = _mapping(manifest.get("privacy"), "privacy")
    _require(
        privacy.get("mediaCommitted") is False,
        "privacy.mediaCommitted must be false.",
    )
    _require(
        privacy.get("transcriptsCommitted") is False,
        "privacy.transcriptsCommitted must be false.",
    )

    recordings_value = manifest.get("recordings")
    _require(
        isinstance(recordings_value, list),
        "recordings must be an array.",
    )
    recordings = cast(list[Any], recordings_value)
    recording_ids: set[str] = set()
    split_counts = {"development": 0, "final-test": 0}
    validated_recordings: list[Mapping[str, Any]] = []
    for index, value in enumerate(recordings):
        recording = _mapping(value, f"recordings[{index}]")
        _validate_recording(recording, index, recording_ids)
        validated_recordings.append(recording)
        split = str(recording["split"])
        split_counts[split] += 1

    _require(
        split_counts["development"] >= 5,
        "At least five development recordings are required.",
    )
    _require(
        split_counts["final-test"] >= 3,
        "At least three final-test recordings are required.",
    )
    if manifest.get("status") == "locked":
        _require(
            all(
                recording.get("locked") is True
                for recording in recordings
                if recording.get("split") == "final-test"
            ),
            "Every final-test recording must be locked.",
        )
    _validate_required_variety(validated_recordings)
    return split_counts


def _validate_recording(
    recording: Mapping[str, Any],
    index: int,
    recording_ids: set[str],
) -> None:
    prefix = f"recordings[{index}]"
    recording_id_value = recording.get("recordingId")
    _require(
        isinstance(recording_id_value, str)
        and RECORDING_ID_PATTERN.fullmatch(recording_id_value) is not None,
        f"{prefix}.recordingId is invalid.",
    )
    recording_id = cast(str, recording_id_value)
    _require(
        recording_id not in recording_ids,
        f"Duplicate recordingId: {recording_id}.",
    )
    recording_ids.add(recording_id)
    _require(
        recording.get("sourceType") in SOURCE_TYPES,
        f"{prefix}.sourceType is invalid.",
    )
    _require(
        recording.get("speechStyle") in SPEECH_STYLES,
        f"{prefix}.speechStyle is invalid.",
    )
    sha256 = recording.get("sha256")
    _require(
        isinstance(sha256, str) and SHA256_PATTERN.fullmatch(sha256) is not None,
        f"{prefix}.sha256 must be 64 lowercase hexadecimal characters.",
    )

    audio = _mapping(recording.get("audio"), f"{prefix}.audio")
    _require(
        audio.get("format") in {"wav", "flac", "mp3", "m4a"},
        f"{prefix}.audio.format is invalid.",
    )
    _require_positive_number(
        audio.get("durationSeconds"),
        f"{prefix}.audio.durationSeconds",
    )
    _require_positive_integer(
        audio.get("sampleRateHz"),
        f"{prefix}.audio.sampleRateHz",
    )
    _require_positive_integer(audio.get("channels"), f"{prefix}.audio.channels")

    speakers = _mapping(recording.get("speakers"), f"{prefix}.speakers")
    _require_positive_integer(speakers.get("count"), f"{prefix}.speakers.count")
    _require_nonempty_text_list(
        speakers.get("languages"),
        f"{prefix}.speakers.languages",
    )
    _require_nonempty_text_list(
        speakers.get("accents"),
        f"{prefix}.speakers.accents",
    )
    _require(
        speakers.get("overlap") in OVERLAP_LEVELS,
        f"{prefix}.speakers.overlap is invalid.",
    )

    capture = _mapping(recording.get("capture"), f"{prefix}.capture")
    _require_text(capture.get("microphone"), f"{prefix}.capture.microphone")
    _require(
        capture.get("noise") in NOISE_LEVELS,
        f"{prefix}.capture.noise is invalid.",
    )

    rights = _mapping(recording.get("rights"), f"{prefix}.rights")
    _require(
        rights.get("consentDocumented") is True,
        f"{prefix}.rights.consentDocumented must be true.",
    )
    _require(
        rights.get("consentRecord")
        in {"local-record", "not-required-generated"},
        f"{prefix}.rights.consentRecord is invalid.",
    )
    _require(
        rights.get("usageRights") in USAGE_RIGHTS,
        f"{prefix}.rights.usageRights is invalid.",
    )
    _require(
        rights.get("publicationStatus") in PUBLICATION_STATUSES,
        f"{prefix}.rights.publicationStatus is invalid.",
    )
    if recording.get("sourceType") == "recorded":
        _require(
            rights.get("consentRecord") == "local-record",
            f"{prefix} recorded audio requires a local consent record.",
        )

    annotations = _mapping(
        recording.get("annotations"),
        f"{prefix}.annotations",
    )
    for field_name in (
        "referenceVerified",
        "timestampsVerified",
        "speakersVerified",
    ):
        _require(
            isinstance(annotations.get(field_name), bool),
            f"{prefix}.annotations.{field_name} must be boolean.",
        )

    split = recording.get("split")
    _require(split in SPLITS, f"{prefix}.split is invalid.")
    _require(
        isinstance(recording.get("locked"), bool),
        f"{prefix}.locked must be boolean.",
    )
    if split == "final-test":
        _require(
            recording.get("locked") is True,
            f"{prefix} final-test recordings must be locked.",
        )


def _validate_required_variety(recordings: Sequence[Mapping[str, Any]]) -> None:
    """Require the Phase 2 coverage categories across the complete dataset."""
    _require(
        any(recording["capture"]["noise"] == "clean" for recording in recordings),
        "Dataset must include clean speech.",
    )
    _require(
        any(
            recording["capture"]["noise"] in {"moderate", "heavy"}
            for recording in recordings
        ),
        "Dataset must include noisy speech.",
    )
    _require(
        any(
            recording["speechStyle"] == "conversational"
            for recording in recordings
        ),
        "Dataset must include conversational speech.",
    )
    _require(
        any(recording["speakers"]["count"] >= 2 for recording in recordings),
        "Dataset must include multiple speakers.",
    )
    accents = {
        accent
        for recording in recordings
        for accent in recording["speakers"]["accents"]
    }
    _require(len(accents) >= 2, "Dataset must include accent variation.")
    _require(
        any(
            recording["speakers"]["overlap"] == "material"
            for recording in recordings
        ),
        "Dataset must include overlapping speech.",
    )


def load_and_validate_manifest(path: Path) -> dict[str, int]:
    """Load one UTF-8 JSON manifest and validate it."""
    if not path.is_file():
        raise ValueError(f"Manifest does not exist: {path}")
    value = json.loads(path.read_text(encoding="utf-8"))
    return validate_manifest(_mapping(value, "manifest"))


def _mapping(value: Any, field_name: str) -> Mapping[str, Any]:
    _require(isinstance(value, Mapping), f"{field_name} must be an object.")
    return cast(Mapping[str, Any], value)


def _require_text(value: Any, field_name: str) -> None:
    _require(
        isinstance(value, str) and bool(value.strip()),
        f"{field_name} must be non-empty text.",
    )


def _require_nonempty_text_list(value: Any, field_name: str) -> None:
    _require(
        isinstance(value, list)
        and bool(value)
        and all(isinstance(item, str) and bool(item.strip()) for item in value),
        f"{field_name} must contain non-empty text values.",
    )


def _require_positive_integer(value: Any, field_name: str) -> None:
    _require(
        isinstance(value, int) and not isinstance(value, bool) and value > 0,
        f"{field_name} must be a positive integer.",
    )


def _require_positive_number(value: Any, field_name: str) -> None:
    _require(
        isinstance(value, int | float)
        and not isinstance(value, bool)
        and value > 0,
        f"{field_name} must be a positive number.",
    )


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def main(arguments: Sequence[str] | None = None) -> int:
    """Run the manifest validator."""
    args = parse_arguments(arguments)
    try:
        counts = load_and_validate_manifest(args.manifest_path.resolve())
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        print(f"Error: {error}")
        return 1
    print(
        "Manifest valid: "
        f"{counts['development']} development, "
        f"{counts['final-test']} final-test recordings."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
