"""Command-line access to the same Phase 3 pipeline used by FastAPI."""

from __future__ import annotations

import argparse
import json
import sys
import tempfile
from collections.abc import Sequence
from pathlib import Path

from pydantic import BaseModel, ValidationError

from app.audio import prepare_audio
from app.config import load_settings, parse_mode
from app.errors import AppError
from app.orchestration import PipelineService
from app.providers.gemini import GeminiProvider
from app.providers.local_stt import FasterWhisperProvider
from app.schemas import MinutesRequest, ProcessResponse, SafeError

PARTIAL_RESULT_EXIT_CODE = 2


def parse_arguments(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Meeting Minutes AI service CLI")
    subparsers = parser.add_subparsers(dest="command", required=True)
    for name in ("transcribe", "process"):
        command = subparsers.add_parser(name)
        command.add_argument("audio_path", type=Path)
        command.add_argument("--mode", choices=("fast", "quality"))
        command.add_argument("--output", type=Path, required=True)
    minutes = subparsers.add_parser("minutes")
    minutes.add_argument("transcript_path", type=Path)
    minutes.add_argument("--mode", choices=("fast", "quality"))
    minutes.add_argument("--output", type=Path, required=True)
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    args = parse_arguments(arguments)
    settings = load_settings()
    pipeline = PipelineService(
        settings,
        GeminiProvider(settings),
        FasterWhisperProvider(settings),
    )
    correlation_id = "cli"
    try:
        result: BaseModel
        mode = parse_mode(args.mode, settings.default_mode)
        if args.command in {"transcribe", "process"}:
            audio_path = args.audio_path.expanduser().resolve()
            with prepare_audio(audio_path, settings) as prepared:
                result = (
                    pipeline.transcribe(prepared, mode, correlation_id=correlation_id)
                    if args.command == "transcribe"
                    else pipeline.process(prepared, mode, correlation_id=correlation_id)
                )
        else:
            request = MinutesRequest.model_validate_json(
                args.transcript_path.read_text(encoding="utf-8")
            )
            result = pipeline.generate_minutes(
                request.rawTranscript,
                mode,
                correlation_id=correlation_id,
            )
        _write_json(args.output, result.model_dump(mode="json"))
        if isinstance(result, ProcessResponse) and result.status == "partial":
            print("Processing partially completed; raw transcript was preserved.")
            return PARTIAL_RESULT_EXIT_CODE
        print("Processing completed.")
        return 0
    except (AppError, OSError, ValueError, ValidationError) as error:
        safe = (
            error
            if isinstance(error, AppError)
            else AppError("INVALID_REQUEST", "Request could not be completed.", 422)
        )
        payload = SafeError(
            code=safe.code,
            message=safe.message,
            correlationId=correlation_id,
            retryable=safe.retryable,
        )
        print(payload.model_dump_json(), file=sys.stderr)
        return 1


def _write_json(path: Path, payload: dict[str, object]) -> None:
    resolved = path.expanduser().resolve()
    resolved.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(
        mode="w",
        encoding="utf-8",
        dir=resolved.parent,
        prefix=".meeting-minutes-",
        suffix=".tmp",
        delete=False,
    ) as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
        temporary = Path(handle.name)
    temporary.replace(resolved)


if __name__ == "__main__":
    raise SystemExit(main())
