"""Issue #404 — Mechanimist Preacher / High Sermon Prefix/Frozen localization."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

from scripts.validate_xml import validate_xml_file

REPO_ROOT = Path(__file__).resolve().parents[2]
CREATURES_XML = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "ObjectBlueprints" / "Creatures.jp.xml"

EXPECTED_BOOKS = {"Preacher1", "Preacher2", "Preacher3", "Preacher4", "HighSermon"}


@pytest.fixture(scope="session")
def preacher_parts() -> list[ET.Element]:
    """Parse Creatures.jp.xml once per session and yield every <part Name="Preacher">."""
    root = ET.parse(CREATURES_XML).getroot()  # noqa: S314 -- local repository XML
    return [part for part in root.iter("part") if part.attrib.get("Name") == "Preacher"]


def _has_non_ascii(value: str) -> bool:
    return any(ord(ch) > 127 for ch in value)


def test_preacher_book_set_is_exact(preacher_parts: list[ET.Element]) -> None:
    """Issue #404: every Preacher part is one of the five known books."""
    books = {part.attrib.get("Book", "") for part in preacher_parts}
    assert books == EXPECTED_BOOKS, (
        f"Expected Preacher books {EXPECTED_BOOKS}, got {books}. "
        "A new Preacher entry surfaced in the data; review and translate before merging."
    )


@pytest.mark.parametrize("book", sorted(EXPECTED_BOOKS))
def test_preacher_prefix_is_japanese_and_ends_with_w_quote(book: str, preacher_parts: list[ET.Element]) -> None:
    """Prefix translated to Japanese, still ends with `{{W|'` so the C# Postfix can close the span."""
    parts = [p for p in preacher_parts if p.attrib.get("Book") == book]
    assert parts, f"Preacher Book={book!r} not found"
    for part in parts:
        prefix = part.attrib.get("Prefix", "")
        assert prefix.endswith("{{W|'"), f"Prefix for Book={book!r} must end with {{{{W|'. Got: {prefix!r}"
        assert _has_non_ascii(prefix), f"Prefix for Book={book!r} must contain Japanese. Got: {prefix!r}"


@pytest.mark.parametrize("book", sorted(EXPECTED_BOOKS))
def test_preacher_postfix_is_explicit(book: str, preacher_parts: list[ET.Element]) -> None:
    """Postfix='}}' is declared explicitly so validate_xml's balance check passes without baseline help."""
    parts = [p for p in preacher_parts if p.attrib.get("Book") == book]
    assert parts
    for part in parts:
        postfix = part.attrib.get("Postfix")
        assert postfix == "'}}", f"Postfix for Book={book!r} must be exactly '}}}}. Got: {postfix!r}"


@pytest.mark.parametrize("book", sorted(EXPECTED_BOOKS))
def test_preacher_frozen_is_japanese(book: str, preacher_parts: list[ET.Element]) -> None:
    """Frozen attribute must contain Japanese."""
    parts = [p for p in preacher_parts if p.attrib.get("Book") == book]
    assert parts
    for part in parts:
        frozen = part.attrib.get("Frozen", "")
        assert _has_non_ascii(frozen), f"Frozen for Book={book!r} must contain Japanese. Got: {frozen!r}"


def test_creatures_xml_has_no_unbalanced_color_warning() -> None:
    """validate_xml.py must produce no 'Unbalanced color code' warning on Creatures.jp.xml."""
    result = validate_xml_file(CREATURES_XML)
    color_warnings = [w for w in result.warnings if "Unbalanced color code" in w]
    assert color_warnings == [], (
        f"Expected no unbalanced-color warnings; got: {color_warnings}. "
        'Make sure every <part Name="Preacher"> declares Postfix="\'}}".'
    )
