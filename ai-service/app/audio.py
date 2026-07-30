"""Audio inspection, normalization, and temporary-file handling."""

from __future__ import annotations

import json
import subprocess
import tempfile
from collections.abc import Iterator
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path

from app.config import Settings
from app.errors import InvalidInputError

GEMINI_MIME_TYPES = {
    "wav": "audio/wav",
    "mp3": "audio/mp3",
    "aiff": "audio/aiff",
    "aac": "audio/aac",
    "ogg": "audio/ogg",
    "flac": "audio/flac",
}
NORMALIZE_FORMATS = {"m4a", "webm", "wma"}


@dataclass(frozen=True)
class AudioInspection:
    format_name: str
    codec_name: str
    duration_seconds: float


@dataclass(frozen=True)
class PreparedAudio:
    path: Path
    mime_type: str
    size_bytes: int
    duration_seconds: float


@contextmanager
def prepare_audio(path: Path, settings: Settings) -> Iterator[PreparedAudio]:
    """Validate audio and normalize unsupported containers when required."""
    temporary_path: Path | None = None
    try:
        if not path.exists() or not path.is_file():
            raise InvalidInputError("AUDIO_NOT_FOUND", "Audio file was not found.")
        size = path.stat().st_size
        if size == 0:
            raise InvalidInputError("EMPTY_AUDIO", "Audio file is empty.")
        if size > settings.max_audio_bytes:
            raise InvalidInputError(
                "AUDIO_TOO_LARGE", "Audio file exceeds the configured size limit."
            )

        detected_format = detect_magic(path)
        inspection = inspect_with_ffprobe(path, settings.ffprobe_path)
        if inspection.duration_seconds > settings.max_audio_duration_seconds:
            raise InvalidInputError(
                "AUDIO_TOO_LONG",
                "Audio duration exceeds the configured duration limit.",
            )

        normalize = (
            detected_format in NORMALIZE_FORMATS
            or detected_format not in GEMINI_MIME_TYPES
            or (detected_format == "ogg" and inspection.codec_name != "vorbis")
        )
        prepared_path = path
        prepared_format = detected_format
        if normalize:
            temporary_path = _temporary_flac_path()
            _normalize_to_flac(
                path,
                temporary_path,
                settings.ffmpeg_path,
            )
            normalized_inspection = inspect_with_ffprobe(
                temporary_path, settings.ffprobe_path
            )
            prepared_path = temporary_path
            prepared_format = "flac"
            inspection = normalized_inspection

        yield PreparedAudio(
            path=prepared_path,
            mime_type=GEMINI_MIME_TYPES[prepared_format],
            size_bytes=prepared_path.stat().st_size,
            duration_seconds=inspection.duration_seconds,
        )
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def detect_magic(path: Path) -> str:
    """Identify an accepted audio container from its bytes, not its suffix."""
    with path.open("rb") as source:
        header = source.read(64)
    if len(header) < 4:
        raise InvalidInputError("INVALID_AUDIO", "Audio header is invalid.")
    if header[:4] == b"RIFF" and header[8:12] == b"WAVE":
        return "wav"
    if header[:4] == b"fLaC":
        return "flac"
    if header[:4] == b"OggS":
        return "ogg"
    if header[:4] == b"FORM" and header[8:12] in {b"AIFF", b"AIFC"}:
        return "aiff"
    if header[0] == 0xFF and header[1] & 0xF6 in {0xF0, 0xF2}:
        return "aac"
    if header[:3] == b"ID3" or (
        header[0] == 0xFF and header[1] & 0xE0 == 0xE0
    ):
        return "mp3"
    if header[:4] == b"\x1aE\xdf\xa3":
        return "webm"
    if header[:16] == bytes.fromhex("3026b2758e66cf11a6d900aa0062ce6c"):
        return "wma"
    if b"ftyp" in header[4:32]:
        return "m4a"
    raise InvalidInputError(
        "UNSUPPORTED_AUDIO",
        "Audio content is not a supported container.",
    )


def inspect_with_ffprobe(path: Path, ffprobe_path: str) -> AudioInspection:
    """Confirm an audio stream, codec, duration, and decodability."""
    command = [
        ffprobe_path,
        "-v",
        "error",
        "-show_entries",
        "format=format_name,duration:stream=codec_type,codec_name,duration",
        "-of",
        "json",
        str(path),
    ]
    try:
        result = subprocess.run(
            command,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=30,
        )
        payload = json.loads(result.stdout)
    except (
        OSError,
        subprocess.CalledProcessError,
        subprocess.TimeoutExpired,
        json.JSONDecodeError,
    ) as error:
        raise InvalidInputError(
            "CORRUPT_AUDIO", "Audio could not be decoded safely."
        ) from error

    audio_streams = [
        stream
        for stream in payload.get("streams", [])
        if stream.get("codec_type") == "audio"
    ]
    if not audio_streams:
        raise InvalidInputError(
            "NO_AUDIO_STREAM", "The uploaded file has no audio stream."
        )
    duration_value = payload.get("format", {}).get("duration")
    if duration_value in {None, "N/A"}:
        duration_value = audio_streams[0].get("duration")
    try:
        duration = float(duration_value)
    except (TypeError, ValueError) as error:
        raise InvalidInputError(
            "INVALID_AUDIO_DURATION", "Audio duration could not be determined."
        ) from error
    if duration <= 0:
        raise InvalidInputError(
            "INVALID_AUDIO_DURATION", "Audio duration must be greater than zero."
        )
    return AudioInspection(
        format_name=str(payload.get("format", {}).get("format_name", "")),
        codec_name=str(audio_streams[0].get("codec_name", "")),
        duration_seconds=duration,
    )


def _temporary_flac_path() -> Path:
    handle = tempfile.NamedTemporaryFile(
        prefix="meeting-minutes-normalized-",
        suffix=".flac",
        delete=False,
    )
    path = Path(handle.name)
    handle.close()
    return path


def _normalize_to_flac(source: Path, target: Path, ffmpeg_path: str) -> None:
    command = [
        ffmpeg_path,
        "-v",
        "error",
        "-y",
        "-i",
        str(source),
        "-vn",
        "-c:a",
        "flac",
        str(target),
    ]
    try:
        subprocess.run(
            command,
            check=True,
            capture_output=True,
            timeout=120,
        )
    except (
        OSError,
        subprocess.CalledProcessError,
        subprocess.TimeoutExpired,
    ) as error:
        raise InvalidInputError(
            "AUDIO_NORMALIZATION_FAILED",
            "Audio could not be normalized safely.",
        ) from error
