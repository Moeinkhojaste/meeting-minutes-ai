from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
REPOSITORY_DIRECTORY = AI_SERVICE_DIRECTORY.parent
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from dataset_manifest import load_and_validate_manifest, validate_manifest  # noqa: E402


class DatasetManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        fixture_path = (
            REPOSITORY_DIRECTORY
            / "Datasets"
            / "manifest.example.v1.json"
        )
        cls.fixture_path = fixture_path
        cls.manifest = json.loads(fixture_path.read_text(encoding="utf-8"))

    def test_generated_fixture_meets_split_minimums(self) -> None:
        self.assertEqual(
            load_and_validate_manifest(self.fixture_path),
            {"development": 5, "final-test": 3},
        )

    def test_duplicate_recording_id_is_rejected(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        manifest["recordings"][1]["recordingId"] = (
            manifest["recordings"][0]["recordingId"]
        )

        with self.assertRaisesRegex(ValueError, "Duplicate recordingId"):
            validate_manifest(manifest)

    def test_unlocked_final_recording_is_rejected(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        manifest["recordings"][-1]["locked"] = False

        with self.assertRaisesRegex(ValueError, "must be locked"):
            validate_manifest(manifest)

    def test_recorded_audio_requires_local_consent_record(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        recording = manifest["recordings"][0]
        recording["sourceType"] = "recorded"

        with self.assertRaisesRegex(ValueError, "local consent record"):
            validate_manifest(manifest)

    def test_private_media_cannot_be_marked_committed(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        manifest["privacy"]["mediaCommitted"] = True

        with self.assertRaisesRegex(ValueError, "mediaCommitted"):
            validate_manifest(manifest)

    def test_required_variety_is_enforced(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        for recording in manifest["recordings"]:
            recording["speakers"]["overlap"] = "none"

        with self.assertRaisesRegex(ValueError, "overlapping speech"):
            validate_manifest(manifest)


if __name__ == "__main__":
    unittest.main()
