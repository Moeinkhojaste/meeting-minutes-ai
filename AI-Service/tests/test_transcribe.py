"""Tests for the Persian transcription CLI helpers."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from settings import (  # noqa: E402
    STT_MODEL_ENVIRONMENT_NAME,
    resolve_setting,
)
from transcribe import (  # noqa: E402
    DEFAULT_MODEL_NAME,
    default_output_path,
    format_timestamp,
    parse_arguments,
    validate_audio_path,
    validate_output_path,
)


class TranscribeHelpersTests(unittest.TestCase):
    def test_default_model_is_evidence_selected_checkpoint(self) -> None:
        arguments = parse_arguments(["meeting.wav"])

        model_name = resolve_setting(
            arguments.model,
            STT_MODEL_ENVIRONMENT_NAME,
            DEFAULT_MODEL_NAME,
            environment={},
            env_file=Path("missing.env"),
        )

        self.assertEqual(model_name, "large-v3-turbo")

    def test_supported_comparison_model_can_be_selected(self) -> None:
        arguments = parse_arguments(
            ["meeting.wav", "--model", "large-v3-turbo"]
        )

        self.assertEqual(arguments.model, "large-v3-turbo")

    def test_model_configuration_precedence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            env_file = Path(temporary_directory) / ".env"
            env_file.write_text(
                f"{STT_MODEL_ENVIRONMENT_NAME}=small\n",
                encoding="utf-8",
            )

            self.assertEqual(
                resolve_setting(
                    "large-v3-turbo",
                    STT_MODEL_ENVIRONMENT_NAME,
                    DEFAULT_MODEL_NAME,
                    environment={STT_MODEL_ENVIRONMENT_NAME: "medium"},
                    env_file=env_file,
                ),
                "large-v3-turbo",
            )
            self.assertEqual(
                resolve_setting(
                    None,
                    STT_MODEL_ENVIRONMENT_NAME,
                    DEFAULT_MODEL_NAME,
                    environment={STT_MODEL_ENVIRONMENT_NAME: "medium"},
                    env_file=env_file,
                ),
                "medium",
            )
            self.assertEqual(
                resolve_setting(
                    None,
                    STT_MODEL_ENVIRONMENT_NAME,
                    DEFAULT_MODEL_NAME,
                    environment={},
                    env_file=env_file,
                ),
                "small",
            )

    def test_format_timestamp_includes_milliseconds(self) -> None:
        self.assertEqual(format_timestamp(3_661.234), "01:01:01.234")

    def test_default_output_path_is_beside_audio(self) -> None:
        audio_path = Path("recordings") / "meeting.mp3"
        expected = Path("recordings") / "meeting.transcript.txt"
        self.assertEqual(default_output_path(audio_path), expected)

    def test_missing_audio_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "does not exist"):
            validate_audio_path(Path("missing-audio.mp3"))

    def test_unsupported_extension_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            text_file = Path(temporary_directory) / "meeting.txt"
            text_file.touch()

            with self.assertRaisesRegex(ValueError, "Unsupported audio extension"):
                validate_audio_path(text_file)

    def test_output_cannot_overwrite_source_audio(self) -> None:
        audio_path = Path("meeting.mp3").resolve()

        with self.assertRaisesRegex(ValueError, "cannot be the input audio"):
            validate_output_path(audio_path, audio_path)


if __name__ == "__main__":
    unittest.main()
