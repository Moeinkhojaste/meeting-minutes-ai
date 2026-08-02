"""Guard the explicit no-local-LLM boundary for Phase 3."""

from __future__ import annotations

import sys
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = AI_SERVICE_DIRECTORY.parent
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from app.cli import parse_arguments  # noqa: E402


def test_cli_exposes_all_three_commands_and_modes() -> None:
    transcribe = parse_arguments(
        ["transcribe", "meeting.wav", "--mode", "quality", "--output", "out.json"]
    )
    minutes = parse_arguments(
        ["minutes", "raw.json", "--mode", "fast", "--output", "out.json"]
    )
    process = parse_arguments(
        ["process", "meeting.wav", "--mode", "fast", "--output", "out.json"]
    )

    assert (transcribe.command, minutes.command, process.command) == (
        "transcribe",
        "minutes",
        "process",
    )


def test_phase3_has_no_local_minutes_provider_or_runtime_dependency() -> None:
    provider_names = {
        path.name for path in (AI_SERVICE_DIRECTORY / "app" / "providers").glob("*.py")
    }
    requirements = (
        (AI_SERVICE_DIRECTORY / "requirements.txt").read_text(encoding="utf-8")
        + (AI_SERVICE_DIRECTORY / "requirements-dev.txt").read_text(encoding="utf-8")
    ).lower()

    assert "local_minutes" not in provider_names
    for forbidden in ("ollama", "transformers", "llama-cpp", "vllm", "qwen"):
        assert forbidden not in requirements


def test_local_minutes_configuration_is_absent() -> None:
    example = (REPOSITORY_ROOT / ".env.example").read_text(encoding="utf-8")

    assert "MM_AI_LOCAL_FAST_MINUTES_MODEL" not in example
    assert "MM_AI_LOCAL_QUALITY_MINUTES_MODEL" not in example
    assert "OLLAMA_HOST" not in example
    assert "MM_AI_OLLAMA_MODEL" not in example
