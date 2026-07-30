"""Safe application errors shared by API and CLI surfaces."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class AppError(Exception):
    """An expected failure safe to expose to callers."""

    code: str
    message: str
    status_code: int = 500
    retryable: bool = False

    def __str__(self) -> str:
        return self.message


class InvalidInputError(AppError):
    def __init__(self, code: str, message: str) -> None:
        super().__init__(code, message, status_code=422, retryable=False)


class ProviderError(AppError):
    """A safe provider failure used for fallback decisions."""

    def __init__(
        self,
        code: str,
        message: str,
        *,
        retryable: bool,
        fallback_reason: str,
    ) -> None:
        super().__init__(code, message, status_code=502, retryable=retryable)
        self.fallback_reason = fallback_reason
