from __future__ import annotations

import json
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

    import pytest

from scripts import check_glossary_consistency


def _write_glossary(path: Path) -> None:
    _ = path.write_text(
        (
            "English,Japanese,Short,Notes,Status\n"
            "Golgotha,ゴルゴタ,,approved term,approved\n"
            "Kyakukya,キャクキャ,,confirmed term,confirmed\n"
            "Baetyls,ベテル,,confirmed term,confirmed\n"
            "DraftOnly,下書き,,not authoritative,draft\n"
            "Barathrumites,バラサラム派,,placeholder term,approved\n"
        ),
        encoding="utf-8",
    )


def _write_entries(path: Path, entries: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    _ = path.write_text(json.dumps({"entries": entries}, ensure_ascii=False), encoding="utf-8")


def _write_xml(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    _ = path.write_text(content, encoding="utf-8")


def _write_baseline(path: Path, *occurrences: dict[str, str]) -> None:
    _ = path.write_text(
        json.dumps(
            {
                "version": 1,
                "allowed_occurrences": list(occurrences),
            },
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )


def test_loads_only_confirmed_and_approved_rows_with_japanese_terms(tmp_path: Path) -> None:
    """Only approved/confirmed glossary rows are authoritative."""
    glossary_path = tmp_path / "glossary.csv"
    _write_glossary(glossary_path)

    terms = check_glossary_consistency.load_glossary_terms(glossary_path)

    assert [term.english for term in terms] == ["Golgotha", "Kyakukya", "Baetyls", "Barathrumites"]


def test_json_scans_translated_text_values_not_source_keys(tmp_path: Path) -> None:
    """JSON dictionary source keys are not scanned as localized text."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {"key": "Golgotha", "text": "ゴルゴタ"},
            {"key": "Translated source", "text": "Travel to Golgotha."},
        ],
    )

    result = check_glossary_consistency.check_paths([localization], glossary_path=glossary_path, baseline_path=None)

    assert [issue.location for issue in result.issues] == ["entry[2].text"]
    assert result.issues[0].term == "Golgotha"


def test_draft_glossary_rows_do_not_block_residue(tmp_path: Path) -> None:
    """Draft glossary rows remain review material, not gate inputs."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [{"key": "Source", "text": "DraftOnly remains review-only."}],
    )

    result = check_glossary_consistency.check_paths([localization], glossary_path=glossary_path, baseline_path=None)

    assert result.issues == []


def test_xml_scans_localized_visible_text_and_attributes_only(tmp_path: Path) -> None:
    """XML source files and structural source ID attributes are ignored."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_xml(
        localization / "Factions.xml",
        '<factions><faction Name="Baetyls" DisplayName="Baetyls" /></factions>',
    )
    _write_xml(
        localization / "Factions.jp.xml",
        '<factions><faction Name="Baetyls" DisplayName="ベテル" /></factions>',
    )
    _write_xml(
        localization / "Books.jp.xml",
        '<books><book Title="Kyakukya"><page>Golgotha appears here.</page></book></books>',
    )

    result = check_glossary_consistency.check_paths([localization], glossary_path=glossary_path, baseline_path=None)

    assert [(issue.term, issue.relative_path, issue.location) for issue in result.issues] == [
        ("Kyakukya", "Books.jp.xml", "/books[1]/book[1]/@Title"),
        ("Golgotha", "Books.jp.xml", "/books[1]/book[1]/page[1]/text()"),
    ]


def test_xml_scans_mixed_content_child_tails(tmp_path: Path) -> None:
    """Visible XML mixed-content tails after inline children are scanned."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_xml(localization / "Mixed.jp.xml", '<root><p><stat Name="X" />Golgotha</p></root>')

    result = check_glossary_consistency.check_paths([localization], glossary_path=glossary_path, baseline_path=None)

    assert [(issue.term, issue.relative_path, issue.location) for issue in result.issues] == [
        ("Golgotha", "Mixed.jp.xml", "/root[1]/p[1]/stat[1]/tail()"),
    ]


def test_xml_keeps_visible_qud_markup_text_but_ignores_game_text_placeholders(tmp_path: Path) -> None:
    """Visible markup bodies are scanned, while runtime variables are ignored."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_xml(
        localization / "Conversations.jp.xml",
        (
            "<conversations>"
            "<conversation>"
            "<text>=factionaddress:Barathrumites= is a runtime placeholder.</text>"
            "<text>{{M|Kyakukya}} is visible markup residue.</text>"
            "</conversation>"
            "</conversations>"
        ),
    )

    result = check_glossary_consistency.check_paths([localization], glossary_path=glossary_path, baseline_path=None)

    assert [(issue.term, issue.location) for issue in result.issues] == [
        ("Kyakukya", "/conversations[1]/conversation[1]/text[2]/text()"),
    ]


def test_baseline_suppresses_current_occurrence_and_reports_stale_entries(tmp_path: Path) -> None:
    """The reviewable baseline suppresses exact matches and reports stale rows."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    baseline_path = tmp_path / "baseline.json"
    _write_glossary(glossary_path)
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [{"key": "Source", "text": "Golgotha remains for now."}],
    )
    _write_baseline(
        baseline_path,
        {
            "term": "Golgotha",
            "path": "Dictionaries/demo.ja.json",
            "location": "entry[1].text",
            "text": "Golgotha remains for now.",
        },
        {
            "term": "Kyakukya",
            "path": "Dictionaries/demo.ja.json",
            "location": "entry[2].text",
            "text": "Kyakukya used to be here.",
        },
    )

    result = check_glossary_consistency.check_paths(
        [localization],
        glossary_path=glossary_path,
        baseline_path=baseline_path,
    )

    assert [(issue.kind, issue.term, issue.location) for issue in result.issues] == [
        ("BASELINE", "Kyakukya", "entry[2].text"),
    ]


def test_full_localization_scan_reports_deleted_file_baseline_entries(tmp_path: Path) -> None:
    """Full-root scans report stale baseline rows for deleted or renamed files."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    baseline_path = tmp_path / "baseline.json"
    _write_glossary(glossary_path)
    _write_entries(localization / "Dictionaries" / "active.ja.json", [{"key": "Source", "text": "ゴルゴタ"}])
    _write_baseline(
        baseline_path,
        {
            "term": "Golgotha",
            "path": "Dictionaries/deleted.ja.json",
            "location": "entry[1].text",
            "text": "Golgotha used to be here.",
        },
    )

    full_result = check_glossary_consistency.check_paths(
        [localization],
        glossary_path=glossary_path,
        baseline_path=baseline_path,
    )
    partial_result = check_glossary_consistency.check_paths(
        [localization / "Dictionaries" / "active.ja.json"],
        glossary_path=glossary_path,
        baseline_path=baseline_path,
    )

    assert [(issue.kind, issue.relative_path, issue.location) for issue in full_result.issues] == [
        ("BASELINE", "Dictionaries/deleted.ja.json", "entry[1].text"),
    ]
    assert partial_result.issues == []


def test_cli_reports_raw_english_residue(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """The CLI exits non-zero and prints glossary guidance for new residue."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [{"key": "Source", "text": "Golgotha remains untranslated."}],
    )

    exit_code = check_glossary_consistency.main(
        [str(localization), "--glossary", str(glossary_path), "--no-baseline"],
    )

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "[RAW_ENGLISH]" in captured.out
    assert "expected Japanese='ゴルゴタ'" in captured.out
