"""Typed configuration for the Phase 3 AI service."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, cast

from settings import DEFAULT_ENV_FILE, read_dotenv

ProcessingMode = Literal["fast", "quality"]

DEFAULT_FAST_GEMINI_MODEL = "gemini-3.5-flash-lite"
DEFAULT_QUALITY_GEMINI_MODEL = "gemini-3.6-flash"
DEFAULT_FAST_LOCAL_STT_MODEL = "small"
DEFAULT_QUALITY_LOCAL_STT_MODEL = "large-v3-turbo"
DEFAULT_FAST_LOCAL_MINUTES_MODEL = "qwen2.5:3b-instruct"
DEFAULT_QUALITY_LOCAL_MINUTES_MODEL = "qwen2.5:3b-instruct"
DEFAULT_LOCAL_LLM_BASE_URL = "http://localhost:11434/v1"


@dataclass(frozen=True)
class Settings:
    """Runtime settings resolved from process environment and ignored `.env`."""

    gemini_api_key: str
    default_mode: ProcessingMode
    gemini_fast_model: str
    gemini_quality_model: str
    max_audio_bytes: int
    max_audio_duration_seconds: float
    gemini_inline_limit_bytes: int
    local_fast_stt_model: str
    local_quality_stt_model: str
    ffmpeg_path: str
    ffprobe_path: str
    request_timeout_seconds: float
    local_llm_base_url: str = DEFAULT_LOCAL_LLM_BASE_URL
    local_fast_minutes_model: str = DEFAULT_FAST_LOCAL_MINUTES_MODEL
    local_quality_minutes_model: str = DEFAULT_QUALITY_LOCAL_MINUTES_MODEL
    local_llm_api_key: str = ""
    local_llm_timeout_seconds: float = 300.0

    def gemini_model(self, mode: ProcessingMode) -> str:
        return self.gemini_fast_model if mode == "fast" else self.gemini_quality_model

    def local_stt_model(self, mode: ProcessingMode) -> str:
        return (
            self.local_fast_stt_model
            if mode == "fast"
            else self.local_quality_stt_model
        )

    def local_minutes_model(self, mode: ProcessingMode) -> str:
        return (
            self.local_fast_minutes_model
            if mode == "fast"
            else self.local_quality_minutes_model
        )


def load_settings(
    *,
    environment: dict[str, str] | None = None,
    env_file: Path = DEFAULT_ENV_FILE,
) -> Settings:
    """Load settings without mutating the process environment."""
    source = environment if environment is not None else dict(os.environ)
    dotenv = read_dotenv(env_file)

    def value(name: str, default: str = "") -> str:
        if name in source:
            return source[name]
        if name in dotenv:
            return dotenv[name]
        return default

    default_mode_value = value("MM_AI_DEFAULT_MODE", "fast").lower()
    if default_mode_value not in {"fast", "quality"}:
        raise ValueError("MM_AI_DEFAULT_MODE must be 'fast' or 'quality'.")

    return Settings(
        gemini_api_key=value("GEMINI_API_KEY"),
        default_mode=cast(ProcessingMode, default_mode_value),
        gemini_fast_model=value("MM_AI_GEMINI_FAST_MODEL", DEFAULT_FAST_GEMINI_MODEL),
        gemini_quality_model=value(
            "MM_AI_GEMINI_QUALITY_MODEL", DEFAULT_QUALITY_GEMINI_MODEL
        ),
        max_audio_bytes=_positive_int(
            value("MM_AI_MAX_AUDIO_BYTES", "524288000"),
            "MM_AI_MAX_AUDIO_BYTES",
        ),
        max_audio_duration_seconds=_positive_float(
            value("MM_AI_MAX_AUDIO_DURATION_SECONDS", "5400"),
            "MM_AI_MAX_AUDIO_DURATION_SECONDS",
        ),
        gemini_inline_limit_bytes=_positive_int(
            value("MM_AI_GEMINI_INLINE_LIMIT_BYTES", "20971520"),
            "MM_AI_GEMINI_INLINE_LIMIT_BYTES",
        ),
        local_fast_stt_model=value(
            "MM_AI_LOCAL_FAST_STT_MODEL", DEFAULT_FAST_LOCAL_STT_MODEL
        ),
        local_quality_stt_model=value(
            "MM_AI_LOCAL_QUALITY_STT_MODEL",
            DEFAULT_QUALITY_LOCAL_STT_MODEL,
        ),
        ffmpeg_path=value("MM_AI_FFMPEG_PATH", "ffmpeg"),
        ffprobe_path=value("MM_AI_FFPROBE_PATH", "ffprobe"),
        request_timeout_seconds=_positive_float(
            value("MM_AI_REQUEST_TIMEOUT_SECONDS", "300"),
            "MM_AI_REQUEST_TIMEOUT_SECONDS",
        ),
        local_llm_base_url=value(
            "MM_AI_LOCAL_LLM_BASE_URL", DEFAULT_LOCAL_LLM_BASE_URL
        ),
        local_fast_minutes_model=value(
            "MM_AI_LOCAL_FAST_MINUTES_MODEL", DEFAULT_FAST_LOCAL_MINUTES_MODEL
        ),
        local_quality_minutes_model=value(
            "MM_AI_LOCAL_QUALITY_MINUTES_MODEL",
            DEFAULT_QUALITY_LOCAL_MINUTES_MODEL,
        ),
        local_llm_api_key=value("MM_AI_LOCAL_LLM_API_KEY", ""),
        local_llm_timeout_seconds=_positive_float(
            value("MM_AI_LOCAL_LLM_TIMEOUT_SECONDS", "300"),
            "MM_AI_LOCAL_LLM_TIMEOUT_SECONDS",
        ),
    )


def parse_mode(value: str | None, default: ProcessingMode) -> ProcessingMode:
    """Validate an API or CLI processing mode."""
    candidate = (value or default).lower()
    if candidate not in {"fast", "quality"}:
        raise ValueError("mode must be 'fast' or 'quality'.")
    return cast(ProcessingMode, candidate)


def _positive_int(value: str, name: str) -> int:
    try:
        parsed = int(value)
    except ValueError as error:
        raise ValueError(f"{name} must be an integer.") from error
    if parsed <= 0:
        raise ValueError(f"{name} must be greater than zero.")
    return parsed


def _positive_float(value: str, name: str) -> float:
    try:
        parsed = float(value)
    except ValueError as error:
        raise ValueError(f"{name} must be a number.") from error
    if parsed <= 0:
        raise ValueError(f"{name} must be greater than zero.")
    return parsed
