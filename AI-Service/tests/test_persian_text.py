"""Tests for Persian text normalization."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

AI_SERVICE_DIRECTORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_DIRECTORY))

from persian_text import normalize_persian_text, remove_whitespace  # noqa: E402


class PersianTextTests(unittest.TestCase):
    def test_arabic_characters_are_converted_to_persian(self) -> None:
        self.assertEqual(normalize_persian_text("يكي ك"), "یکی ک")

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

    def test_empty_text_stays_empty(self) -> None:
        self.assertEqual(normalize_persian_text(""), "")

    def test_remove_whitespace_removes_all_unicode_whitespace(self) -> None:
        self.assertEqual(remove_whitespace("الف ب\tپ\nت"), "الفبپت")


if __name__ == "__main__":
    unittest.main()
