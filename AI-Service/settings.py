"""Configuration helpers shared by the current AI-service CLIs."""

from __future__ import annotations

import os
from collections.abc import Mapping
from pathlib import Path

DEFAULT_ENV_FILE = Path(__file__).resolve().parents[1] / ".env"
STT_MODEL_ENVIRONMENT_NAME = "MM_AI_STT_MODEL"


def read_dotenv(path: Path) -> dict[str, str]:
    """Read simple NAME=VALUE entries without changing process environment."""
    if not path.exists():
        return {}

    values: dict[str, str] = {}
    for line_number, raw_line in enumerate(
        path.read_text(encoding="utf-8").splitlines(),
        start=1,
    ):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            raise ValueError(
                f"Invalid .env entry at line {line_number}: expected NAME=VALUE."
            )
        name, value = line.split("=", 1)
        name = name.strip()
        if not name:
            raise ValueError(
                f"Invalid .env entry at line {line_number}: empty name."
            )
        values[name] = _unquote(value.strip())
    return values


def resolve_setting(
    cli_value: str | None,
    environment_name: str,
    default: str,
    *,
    environment: Mapping[str, str] | None = None,
    env_file: Path = DEFAULT_ENV_FILE,
) -> str:
    """Resolve CLI, environment, .env, then default precedence."""
    if cli_value:
        return cli_value

    current_environment = environment if environment is not None else os.environ
    environment_value = current_environment.get(environment_name)
    if environment_value:
        return environment_value

    dotenv_value = read_dotenv(env_file).get(environment_name)
    return dotenv_value if dotenv_value else default


def _unquote(value: str) -> str:
    if len(value) >= 2 and (
        (value[0] == value[-1] == '"')
        or (value[0] == value[-1] == "'")
    ):
        return value[1:-1]
    return value
