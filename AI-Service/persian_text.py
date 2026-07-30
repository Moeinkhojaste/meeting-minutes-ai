"""Text preparation rules used by the Persian STT evaluation."""

from __future__ import annotations

import re
import unicodedata

ARABIC_TO_PERSIAN_TRANSLATION = str.maketrans(
    {
        "ي": "ی",
        "ى": "ی",
        "ك": "ک",
    }
)
WHITESPACE_PATTERN = re.compile(r"\s+")
ZERO_WIDTH_NON_JOINER = "\u200c"


def normalize_persian_text(text: str) -> str:
    """Return a conservative, repeatable form of Persian text for scoring."""
    normalized = unicodedata.normalize("NFKC", text)
    normalized = normalized.translate(ARABIC_TO_PERSIAN_TRANSLATION)
    normalized = normalized.replace(ZERO_WIDTH_NON_JOINER, " ")
    normalized = "".join(
        " " if _is_punctuation(character) else character
        for character in normalized
    )
    return WHITESPACE_PATTERN.sub(" ", normalized).strip()


def remove_whitespace(text: str) -> str:
    """Remove all Unicode whitespace from text for CER calculation."""
    return WHITESPACE_PATTERN.sub("", text)


def _is_punctuation(character: str) -> bool:
    """Return whether Unicode classifies a character as punctuation."""
    return unicodedata.category(character).startswith("P")
