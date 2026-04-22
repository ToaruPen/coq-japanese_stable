"""Tests for the diff_localization module."""

import json
from pathlib import Path

import pytest

from scripts.diff_localization import _extract_generic_entries, main


def _write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def _write_bytes(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def test_summary_output_with_full_coverage(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Summary reports 100% when all base entries are translated."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_text(
        base_dir / "ObjectBlueprints" / "Items.xml",
        '<objects><object Name="A"/><object Name="B"/></objects>',
    )
    _write_text(
        base_dir / "Conversations.xml",
        '<conversations><conversation ID="C1"/></conversations>',
    )
    _write_text(
        mod_dir / "ObjectBlueprints" / "Items.jp.xml",
        '<objects><object Name="A"/><object Name="B"/></objects>',
    )
    _write_text(
        mod_dir / "Conversations.jp.xml",
        '<conversations><conversation ID="C1"/></conversations>',
    )

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "ObjectBlueprints" in captured.out
    assert "Conversations" in captured.out
    assert "100.0%" in captured.out


def test_summary_output_with_partial_coverage(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Summary reports partial coverage when only some entries are translated."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_text(
        base_dir / "ObjectBlueprints" / "Items.xml",
        '<objects><object Name="A"/><object Name="B"/><object Name="C"/></objects>',
    )
    _write_text(
        mod_dir / "ObjectBlueprints" / "Items.jp.xml",
        '<objects><object Name="A"/></objects>',
    )

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "ObjectBlueprints" in captured.out
    assert "33.3%" in captured.out


def test_missing_only_filters_untranslated_entries(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Missing-only output shows untranslated entries and omits removed entries."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_text(
        base_dir / "Conversations.xml",
        '<conversations><conversation ID="A"/><conversation ID="B"/></conversations>',
    )
    _write_text(
        mod_dir / "Conversations.jp.xml",
        '<conversations><conversation ID="A"/><conversation ID="Z"/></conversations>',
    )

    result = main(["--missing-only", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "B" in captured.out
    assert "Z" not in captured.out
    assert "removed" not in captured.out


def test_json_path_writes_valid_json(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """JSON output file is written with expected keys."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"
    json_path = tmp_path / "report" / "coverage.json"

    _write_text(
        base_dir / "Skills.xml",
        '<skills><skill Name="SkillA"/></skills>',
    )
    _write_text(
        mod_dir / "Skills.jp.xml",
        '<skills><skill Name="SkillA"/></skills>',
    )

    result = main(
        [
            "--summary",
            "--json-path",
            str(json_path),
            "--base-dir",
            str(base_dir),
            "--mod-dir",
            str(mod_dir),
        ],
    )
    _ = capsys.readouterr()

    assert result == 0
    payload = json.loads(json_path.read_text(encoding="utf-8"))
    assert "categories" in payload
    assert "totals" in payload
    assert payload["totals"]["coverage_percent"] == 100.0


def test_error_when_base_directory_missing(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI exits non-zero with clear error when base directory is missing."""
    mod_dir = tmp_path / "Localization"
    mod_dir.mkdir()

    result = main(["--base-dir", str(tmp_path / "MissingBase"), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 1
    assert "Base directory not found" in captured.err


def test_error_when_mod_directory_missing(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI exits non-zero with clear error when mod directory is missing."""
    base_dir = tmp_path / "Base"
    _write_text(base_dir / "Skills.xml", '<skills><skill Name="A"/></skills>')

    result = main(["--base-dir", str(base_dir), "--mod-dir", str(tmp_path / "MissingMod")])
    captured = capsys.readouterr()

    assert result == 1
    assert "Mod directory not found" in captured.err


def test_empty_translation_file_handled_gracefully(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Empty translation XML file is treated as no translated entries."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_text(base_dir / "Skills.xml", '<skills><skill Name="A"/></skills>')
    _write_text(mod_dir / "Skills.jp.xml", "")

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Skills" in captured.out
    assert "0.0%" in captured.out


def test_file_with_no_matching_entries_reports_zero_coverage(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Coverage is 0% when translation IDs do not match base IDs."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_text(
        base_dir / "Conversations.xml",
        '<conversations><conversation ID="BaseOnly"/></conversations>',
    )
    _write_text(
        mod_dir / "Conversations.jp.xml",
        '<conversations><conversation ID="ModOnly"/></conversations>',
    )

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Conversations" in captured.out
    assert "0.0%" in captured.out


def test_books_parse_error_falls_back_to_regex(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Books.xml parse errors fall back to regex-based ID extraction."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_bytes(
        base_dir / "Books.xml",
        b'<books><book ID="BookA"><page>bad\x08text</page></book></books>',
    )
    _write_text(
        mod_dir / "Books.jp.xml",
        '<books><book ID="BookA"/></books>',
    )

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Books" in captured.out
    assert "100.0%" in captured.out
    assert "WARNING" in captured.err


def test_object_blueprints_parse_error_falls_back_to_regex(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """ObjectBlueprints XML parse errors recover object Name attributes."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_bytes(
        base_dir / "ObjectBlueprints" / "Items.xml",
        b'<objects><object Name="A"><part Value="bad\x08text"/></object><object Name="B"/></objects>',
    )
    _write_text(
        mod_dir / "ObjectBlueprints" / "Items.jp.xml",
        '<objects><object Name="A"/></objects>',
    )

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "ObjectBlueprints" in captured.out
    assert "50.0%" in captured.out
    assert "recovered 2 object Name entries" in captured.err


def test_generic_parse_error_falls_back_to_id_or_name_regex(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Generic XML parse errors recover ID/Name attributes instead of skipping the file."""
    base_dir = tmp_path / "Base"
    mod_dir = tmp_path / "Localization"

    _write_bytes(base_dir / "Skills.xml", b'<skills><skill Name="A">bad\x08text</skill><skill Name="B"/></skills>')
    _write_text(mod_dir / "Skills.jp.xml", '<skills><skill Name="A"/></skills>')

    result = main(["--summary", "--base-dir", str(base_dir), "--mod-dir", str(mod_dir)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Skills" in captured.out
    assert "50.0%" in captured.out
    assert "recovered 2 ID/Name entries" in captured.err


def test_generic_entries_returns_empty_set_on_no_id_or_name(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """_extract_generic_entries returns empty set and warns for XML with no ID/Name attributes."""
    xml_path = tmp_path / "NoAttrs.xml"
    _write_text(xml_path, "<root><item/><item/></root>")

    result = _extract_generic_entries(xml_path)
    captured = capsys.readouterr()

    assert result == set()
    assert "WARNING" in captured.err
    assert "No ID or Name attributes found" in captured.err


def test_generic_entries_compat_xml_like_returns_empty_set(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """_extract_generic_entries returns empty set for Compat.xml-like structure (Old/New attrs only)."""
    xml_path = tmp_path / "Compat.xml"
    _write_text(
        xml_path,
        '<compat><skill Old="Axe_ChargingStrike" New="Cudgel_ChargingStrike"/></compat>',
    )

    result = _extract_generic_entries(xml_path)
    captured = capsys.readouterr()

    assert result == set()
    assert "WARNING" in captured.err
