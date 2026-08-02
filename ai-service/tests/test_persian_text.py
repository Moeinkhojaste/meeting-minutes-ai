"""Tests for Persian text normalization."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from persian_text import (  # noqa: E402
    create_text_variants,
    normalize_persian_text,
    remove_whitespace,
)


class PersianTextTests(unittest.TestCase):
    def test_arabic_characters_are_converted_to_persian(self) -> None:
        self.assertEqual(normalize_persian_text("يكى ك"), "یکی ک")

    def test_persian_and_ascii_punctuation_are_removed(self) -> None:
        self.assertEqual(
            normalize_persian_text("سلام، دنیا! حالت چطور است؟"),
            "سلام دنیا حالت چطور است",
        )

    def test_repeated_unicode_whitespace_is_collapsed(self) -> None:
        self.assertEqual(
            normalize_persian_text("  متن\t فارسی\nآزمایشی  "),
            "متن فارسی آزمایشی",
        )

    def test_zero_width_non_joiner_becomes_word_boundary(self) -> None:
        self.assertEqual(normalize_persian_text("ثبت‌نام"), "ثبت نام")

    def test_arabic_diacritics_and_tatweel_are_removed(self) -> None:
        self.assertEqual(normalize_persian_text("سَــلام"), "سلام")

    def test_latin_text_is_lowercased(self) -> None:
        self.assertEqual(normalize_persian_text("Whisper CUDA"), "whisper cuda")

    def test_persian_and_arabic_digits_become_ascii(self) -> None:
        self.assertEqual(normalize_persian_text("سال ۲۰۲۶ و ٢٠٢٥"), "سال 2026 و 2025")

    def test_spoken_numbers_are_not_rewritten(self) -> None:
        self.assertEqual(normalize_persian_text("سه"), "سه")

    def test_raw_text_is_preserved_beside_normalized_text(self) -> None:
        source = "  يک‌متن!  "

        variants = create_text_variants(source)

        self.assertEqual(variants.raw, source)
        self.assertEqual(variants.normalized, "یک متن")

    def test_empty_text_stays_empty(self) -> None:
        self.assertEqual(normalize_persian_text(""), "")

    def test_remove_whitespace_removes_all_unicode_whitespace(self) -> None:
        self.assertEqual(remove_whitespace("الف ب\tپ\nت"), "الفبپت")


if __name__ == "__main__":
    unittest.main()
