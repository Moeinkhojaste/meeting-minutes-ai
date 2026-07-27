"""Text preparation rules used by the Persian STT evaluation."""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass

ARABIC_TO_PERSIAN_TRANSLATION = str.maketrans(
    {
        "ي": "ی",
        "ى": "ی",
        "ك": "ک",
    }
)
DIGIT_TRANSLATION = str.maketrans(
    {
        **{persian: str(index) for index, persian in enumerate("۰۱۲۳۴۵۶۷۸۹")},
        **{arabic: str(index) for index, arabic in enumerate("٠١٢٣٤٥٦٧٨٩")},
    }
)
WHITESPACE_PATTERN = re.compile(r"\s+")
ZERO_WIDTH_NON_JOINER = "\u200c"
TATWEEL = "\u0640"
ARABIC_DIACRITIC_RANGES = (
    (0x0610, 0x061A),
    (0x064B, 0x065F),
    (0x0670, 0x0670),
    (0x06D6, 0x06ED),
)


@dataclass(frozen=True)
class TextVariants:
    """Keep an immutable original string beside its normalized derivative."""

    raw: str
    normalized: str


def create_text_variants(text: str) -> TextVariants:
    """Preserve the input exactly and calculate normalization separately."""
    return TextVariants(raw=text, normalized=normalize_persian_text(text))


def normalize_persian_text(text: str) -> str:
    """Return a conservative, repeatable form of Persian text for scoring."""
    normalized = unicodedata.normalize("NFKC", text)
    normalized = normalized.translate(ARABIC_TO_PERSIAN_TRANSLATION)
    normalized = normalized.translate(DIGIT_TRANSLATION)
    normalized = normalized.lower()
    normalized = normalized.replace(ZERO_WIDTH_NON_JOINER, " ")
    normalized = normalized.replace(TATWEEL, "")
    normalized = "".join(
        character
        for character in normalized
        if not _is_arabic_diacritic(character)
    )
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


def _is_arabic_diacritic(character: str) -> bool:
    """Return whether a character is an Arabic combining diacritic."""
    codepoint = ord(character)
    return (
        unicodedata.category(character) == "Mn"
        and any(start <= codepoint <= end for start, end in ARABIC_DIACRITIC_RANGES)
    )
