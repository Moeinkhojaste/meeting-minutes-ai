"""Actual-content audio validation and cleanup tests."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from app.audio import detect_magic, prepare_audio  # noqa: E402
from app.errors import InvalidInputError  # noqa: E402
from tests.phase3_helpers import settings, write_wav  # noqa: E402


def test_wav_is_detected_from_magic_not_suffix(tmp_path: Path) -> None:
    path = tmp_path / "meeting.bin"
    write_wav(path)

    assert detect_magic(path) == "wav"


def test_invalid_magic_is_rejected_before_provider(tmp_path: Path) -> None:
    path = tmp_path / "meeting.wav"
    path.write_bytes(b"this is not audio")

    with pytest.raises(InvalidInputError) as captured:
        with prepare_audio(path, settings()):
            pass

    assert captured.value.code == "UNSUPPORTED_AUDIO"


def test_size_limit_is_enforced_before_ffprobe(tmp_path: Path) -> None:
    path = tmp_path / "meeting.wav"
    write_wav(path)

    with pytest.raises(InvalidInputError) as captured:
        with prepare_audio(path, settings(max_audio_bytes=10)):
            pass

    assert captured.value.code == "AUDIO_TOO_LARGE"


def test_valid_wav_is_inspected_by_ffprobe(tmp_path: Path) -> None:
    path = tmp_path / "meeting.wav"
    write_wav(path)

    with prepare_audio(path, settings()) as prepared:
        assert prepared.mime_type == "audio/wav"
        assert prepared.duration_seconds > 0
        assert prepared.path == path

    assert path.exists()


def test_duration_limit_is_enforced(tmp_path: Path) -> None:
    path = tmp_path / "meeting.wav"
    write_wav(path)

    with pytest.raises(InvalidInputError) as captured:
        with prepare_audio(path, settings(max_audio_duration_seconds=0.01)):
            pass

    assert captured.value.code == "AUDIO_TOO_LONG"


def test_m4a_is_normalized_and_temporary_file_is_deleted(
    tmp_path: Path,
) -> None:
    wav_path = tmp_path / "source.wav"
    m4a_path = tmp_path / "meeting.m4a"
    write_wav(wav_path)
    subprocess.run(
        [
            "ffmpeg",
            "-v",
            "error",
            "-y",
            "-i",
            str(wav_path),
            "-c:a",
            "aac",
            str(m4a_path),
        ],
        check=True,
    )

    with prepare_audio(m4a_path, settings()) as prepared:
        normalized_path = prepared.path
        assert prepared.mime_type == "audio/flac"
        assert normalized_path != m4a_path
        assert normalized_path.exists()

    assert not normalized_path.exists()
    assert m4a_path.exists()


def test_aac_magic_is_not_misclassified_as_mp3(tmp_path: Path) -> None:
    wav_path = tmp_path / "source.wav"
    aac_path = tmp_path / "meeting.aac"
    write_wav(wav_path)
    subprocess.run(
        [
            "ffmpeg",
            "-v",
            "error",
            "-y",
            "-i",
            str(wav_path),
            "-c:a",
            "aac",
            "-f",
            "adts",
            str(aac_path),
        ],
        check=True,
    )

    assert detect_magic(aac_path) == "aac"
