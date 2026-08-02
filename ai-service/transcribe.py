"""Create a timestamped Persian transcript with faster-whisper."""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path

from settings import (
    DEFAULT_ENV_FILE,
    STT_MODEL_ENVIRONMENT_NAME,
    resolve_setting,
)

DEFAULT_MODEL_NAME = "large-v3-turbo"
SUPPORTED_MODEL_NAMES = ("small", "medium", "large-v3-turbo")
LANGUAGE = "fa"
DEVICE = "cuda"
COMPUTE_TYPE = "float16"
SUPPORTED_AUDIO_EXTENSIONS = {
    ".aac",
    ".flac",
    ".m4a",
    ".mp3",
    ".ogg",
    ".wav",
    ".webm",
    ".wma",
}
DLL_DIRECTORY_HANDLES: list[object] = []


@dataclass(frozen=True)
class TranscribedSegment:
    """A provider-neutral faster-whisper segment."""

    start_seconds: float
    end_seconds: float
    text: str
    language: str


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description=(
            "Transcribe a local Persian audio file with the selected Whisper "
            "checkpoint and "
            "write segment timestamps to a UTF-8 text file."
        )
    )
    parser.add_argument("audio_path", type=Path, help="Path to the input audio file")
    parser.add_argument(
        "--output",
        type=Path,
        help=(
            "Path for the transcript (default: AUDIO_NAME.transcript.txt "
            "beside the audio file)"
        ),
    )
    parser.add_argument(
        "--model",
        choices=SUPPORTED_MODEL_NAMES,
        default=None,
        help=(
            "Whisper checkpoint used for the controlled STT comparison "
            f"(default after configuration precedence: {DEFAULT_MODEL_NAME})"
        ),
    )
    parser.add_argument(
        "--env-file",
        type=Path,
        default=DEFAULT_ENV_FILE,
        help="Optional .env file used after process environment variables.",
    )
    return parser.parse_args(arguments)


def validate_audio_path(audio_path: Path) -> None:
    """Raise a helpful error when the input is not a supported local file."""
    if not audio_path.exists():
        raise ValueError(f"Audio file does not exist: {audio_path}")
    if not audio_path.is_file():
        raise ValueError(f"Audio path is not a file: {audio_path}")
    if audio_path.suffix.lower() not in SUPPORTED_AUDIO_EXTENSIONS:
        extensions = ", ".join(sorted(SUPPORTED_AUDIO_EXTENSIONS))
        raise ValueError(
            f"Unsupported audio extension '{audio_path.suffix}'. "
            f"Supported extensions: {extensions}"
        )


def default_output_path(audio_path: Path) -> Path:
    """Return the default transcript path for an audio file."""
    return audio_path.with_name(f"{audio_path.stem}.transcript.txt")


def validate_output_path(audio_path: Path, output_path: Path) -> None:
    """Prevent the transcript from overwriting the source audio."""
    if audio_path == output_path:
        raise ValueError("The transcript output path cannot be the input audio path.")


def format_timestamp(seconds: float) -> str:
    """Format seconds as HH:MM:SS.mmm."""
    milliseconds = max(0, round(seconds * 1000))
    hours, remainder = divmod(milliseconds, 3_600_000)
    minutes, remainder = divmod(remainder, 60_000)
    whole_seconds, milliseconds = divmod(remainder, 1000)
    return f"{hours:02}:{minutes:02}:{whole_seconds:02}.{milliseconds:03}"


def configure_windows_nvidia_dlls() -> None:
    """Make NVIDIA DLLs installed in the virtual environment discoverable."""
    if sys.platform != "win32":
        return

    site_packages = Path(sys.prefix) / "Lib" / "site-packages"
    dll_directories = (
        site_packages / "nvidia" / "cublas" / "bin",
        site_packages / "nvidia" / "cuda_nvrtc" / "bin",
        site_packages / "nvidia" / "cudnn" / "bin",
    )
    for dll_directory in dll_directories:
        if dll_directory.is_dir():
            DLL_DIRECTORY_HANDLES.append(
                os.add_dll_directory(str(dll_directory))
            )


def verify_cuda_support() -> None:
    """Verify CTranslate2 can actually use CUDA with float16."""
    try:
        import ctranslate2
    except ModuleNotFoundError as error:
        raise RuntimeError(
            "CTranslate2 is not installed. Run "
            "'python -m pip install -r ai-service/requirements.txt'."
        ) from error

    try:
        compute_types = ctranslate2.get_supported_compute_types(DEVICE)
    except RuntimeError as error:
        raise RuntimeError(
            "CTranslate2 cannot access CUDA on this computer. "
            "The prototype will not silently switch to CPU."
        ) from error

    if COMPUTE_TYPE not in compute_types:
        supported = ", ".join(sorted(compute_types))
        raise RuntimeError(
            f"CTranslate2 CUDA does not support {COMPUTE_TYPE}. "
            f"Reported compute types: {supported or 'none'}"
        )


def transcribe(
    audio_path: Path,
    output_path: Path,
    model_name: str = DEFAULT_MODEL_NAME,
) -> None:
    """Run Persian transcription and write timestamped UTF-8 segments."""
    segments = transcribe_segments(audio_path, model_name)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    transcript_lines = [
        (
            f"[{format_timestamp(segment.start_seconds)} --> "
            f"{format_timestamp(segment.end_seconds)}] {segment.text}"
        )
        for segment in segments
    ]

    output_path.write_text(
        "\n".join(transcript_lines) + ("\n" if transcript_lines else ""),
        encoding="utf-8",
    )
    language = segments[0].language if segments else LANGUAGE
    print(f"Language: {language}")
    print(f"Transcript saved to: {output_path.resolve()}")


def transcribe_segments(
    audio_path: Path,
    model_name: str = DEFAULT_MODEL_NAME,
) -> list[TranscribedSegment]:
    """Run faster-whisper and return structured segments without writing."""
    configure_windows_nvidia_dlls()

    try:
        from faster_whisper import WhisperModel
    except ModuleNotFoundError as error:
        raise RuntimeError(
            "faster-whisper is not installed. Run "
            "'python -m pip install -r ai-service/requirements.txt'."
        ) from error

    verify_cuda_support()
    print(f"Loading Whisper checkpoint: {model_name}")
    print(f"Inference device: {DEVICE} ({COMPUTE_TYPE})")
    model = WhisperModel(
        model_name,
        device=DEVICE,
        compute_type=COMPUTE_TYPE,
    )

    segments, info = model.transcribe(
        str(audio_path),
        language=LANGUAGE,
        task="transcribe",
    )

    transcript_segments: list[TranscribedSegment] = []
    for segment in segments:
        text = segment.text.strip()
        if text:
            transcript_segments.append(
                TranscribedSegment(
                    start_seconds=float(segment.start),
                    end_seconds=float(segment.end),
                    text=text,
                    language=str(info.language or LANGUAGE),
                )
            )
    return transcript_segments


def main(arguments: Sequence[str] | None = None) -> int:
    """Run the command-line program."""
    args = parse_arguments(arguments)
    audio_path = args.audio_path.expanduser().resolve()
    output_path = (
        args.output.expanduser().resolve()
        if args.output
        else default_output_path(audio_path)
    )

    try:
        model_name = resolve_setting(
            args.model,
            STT_MODEL_ENVIRONMENT_NAME,
            DEFAULT_MODEL_NAME,
            env_file=args.env_file.expanduser().resolve(),
        )
        if model_name not in SUPPORTED_MODEL_NAMES:
            supported = ", ".join(SUPPORTED_MODEL_NAMES)
            raise ValueError(
                f"Unsupported configured model '{model_name}'. "
                f"Supported checkpoints: {supported}"
            )
        validate_audio_path(audio_path)
        validate_output_path(audio_path, output_path)
        transcribe(audio_path, output_path, model_name)
    except RuntimeError as error:
        print(f"Error: {error}", file=sys.stderr)
        print("No CPU fallback was attempted.", file=sys.stderr)
        return 1
    except (OSError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
