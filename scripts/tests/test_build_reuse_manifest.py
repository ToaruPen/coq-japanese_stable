"""Tests for the build_reuse_manifest module."""
# ruff: noqa: SLF001

from pathlib import Path
from unittest.mock import patch

import pytest

from scripts import build_reuse_manifest as reuse


class TestLookupContainerText:
    """Tests for sentence-level container lookup fallbacks."""

    def test_returns_sentence_level_match_instead_of_full_paragraph(self) -> None:
        """Containment fallback returns the aligned sentence, not the whole paragraph."""
        container = reuse._ReuseContainer(
            english_key=reuse._match_key("Alpha. Beta target sentence. Gamma."),
            english_text="Alpha. Beta target sentence. Gamma.",
            japanese_text="アルファ。ベータ対象文。ガンマ。",
        )

        result = reuse._lookup_container_text(reuse._match_key("Beta target sentence."), [container])

        assert result == "ベータ対象文。"

    def test_returns_none_when_sentence_alignment_is_not_confident(self) -> None:
        """Containment fallback disables itself when sentence counts cannot be aligned."""
        container = reuse._ReuseContainer(
            english_key=reuse._match_key("Alpha. Beta target sentence."),
            english_text="Alpha. Beta target sentence.",
            japanese_text="アルファとベータ対象文。",
        )

        result = reuse._lookup_container_text(reuse._match_key("Beta target sentence."), [container])

        assert result is None


class TestBookAlignment:
    """Tests for translated book alignment validation."""

    def test_build_books_map_raises_on_page_count_mismatch(self) -> None:
        """A translated book with mismatched pages fails fast."""
        english_books = {"book-1": ["Page one.", "Page two."]}
        japanese_books = {"book-1": ["1ページ目。"]}

        with pytest.raises(ValueError, match=r"^Books page count mismatch for 'book-1': en=2 ja=1$"):
            reuse._build_books_map(english_books, japanese_books)

    def test_build_book_containers_raises_on_page_count_mismatch(self) -> None:
        """Container fallbacks share the same fail-fast page-count validation."""
        english_books = {"book-1": ["Page one.", "Page two."]}
        japanese_books = {"book-1": ["1ページ目。"]}

        with pytest.raises(ValueError, match=r"^Books page count mismatch for 'book-1': en=2 ja=1$"):
            reuse._build_book_containers(english_books, japanese_books)


def test_main_resolves_relative_paths_against_project_root(tmp_path: Path) -> None:
    """Relative CLI paths are resolved against the repository root."""
    with (
        patch("scripts.build_reuse_manifest._find_project_root", return_value=tmp_path),
        patch("scripts.build_reuse_manifest._validate_file") as validate_file,
        patch("scripts.build_reuse_manifest._validate_directory") as validate_directory,
        patch("scripts.build_reuse_manifest._parse_corpus_sentences", return_value=([], [])),
        patch("scripts.build_reuse_manifest._parse_books_xml", return_value={}),
        patch("scripts.build_reuse_manifest._build_books_map", return_value={}),
        patch("scripts.build_reuse_manifest._build_book_containers", return_value=[]),
        patch("scripts.build_reuse_manifest._build_excerpts_map", return_value={}),
        patch("scripts.build_reuse_manifest._build_excerpt_containers", return_value=[]),
    ):
        result = reuse.main(
            [
                "--corpus-raw",
                "raw.txt",
                "--books-en",
                "Books.xml",
                "--books-ja",
                "Mods/QudJP/Localization/Books.jp.xml",
                "--corpus-dir",
                "Mods/QudJP/Localization/Corpus",
                "--output",
                "scripts/reuse_manifest.json",
            ]
        )

    assert result == 0
    validate_file.assert_any_call((tmp_path / "raw.txt").resolve(), label="Raw corpus")
    validate_file.assert_any_call((tmp_path / "Books.xml").resolve(), label="English Books XML")
    validate_file.assert_any_call(
        (tmp_path / "Mods/QudJP/Localization/Books.jp.xml").resolve(),
        label="Japanese Books XML",
    )
    validate_directory.assert_called_once_with((tmp_path / "Mods/QudJP/Localization/Corpus").resolve(), label="Corpus")
    assert (tmp_path / "scripts/reuse_manifest.json").exists()
