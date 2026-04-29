from __future__ import annotations

import json
from typing import TYPE_CHECKING

import pytest

if TYPE_CHECKING:
    from pathlib import Path

from scripts import check_glossary_consistency


def _write_glossary_rows(path: Path, *rows: str) -> None:
    _ = path.write_text(
        "English,Japanese,Short,Notes,Status\n" + "\n".join(rows) + "\n",
        encoding="utf-8",
    )


def _write_glossary(path: Path) -> None:
    _write_glossary_rows(
        path,
        "Golgotha,ゴルゴタ,,approved term,approved",
        "Kyakukya,キャクキャ,,confirmed term,confirmed",
        "Baetyls,ベテル,,confirmed term,confirmed",
        "DraftOnly,下書き,,not authoritative,draft",
        "Barathrumites,バラサラム派,,placeholder term,approved",
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


def _check_with_baseline(tmp_path: Path, baseline_path: Path) -> None:
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_entries(localization / "Dictionaries" / "demo.ja.json", [{"key": "Source", "text": "ゴルゴタ"}])
    _ = check_glossary_consistency.check_paths(
        [localization],
        glossary_path=glossary_path,
        baseline_path=baseline_path,
    )


def test_loads_only_confirmed_and_approved_rows_with_japanese_terms(tmp_path: Path) -> None:
    """Only approved/confirmed glossary rows are authoritative."""
    glossary_path = tmp_path / "glossary.csv"
    _write_glossary(glossary_path)

    terms = check_glossary_consistency.load_glossary_terms(glossary_path)

    assert [term.english for term in terms] == ["Golgotha", "Kyakukya", "Baetyls", "Barathrumites"]


def test_duplicate_active_glossary_english_terms_are_rejected(tmp_path: Path) -> None:
    """Authoritative English terms must be unique after trim/casefold normalization."""
    glossary_path = tmp_path / "glossary.csv"
    _write_glossary_rows(
        glossary_path,
        "Golgotha,ゴルゴタ,,approved term,approved",
        " golgotha ,別訳,,confirmed duplicate,confirmed",
    )

    with pytest.raises(ValueError, match="Duplicate active glossary English term 'golgotha'") as exc_info:
        _ = check_glossary_consistency.load_glossary_terms(glossary_path)

    message = str(exc_info.value)
    assert "Duplicate active glossary English term 'golgotha'" in message
    assert str(glossary_path) in message


def test_draft_duplicate_glossary_english_term_does_not_block(tmp_path: Path) -> None:
    """Draft duplicate rows do not block a single authoritative term."""
    glossary_path = tmp_path / "glossary.csv"
    _write_glossary_rows(
        glossary_path,
        "Golgotha,ゴルゴタ,,approved term,approved",
        " golgotha ,別訳,,draft duplicate,draft",
    )

    terms = check_glossary_consistency.load_glossary_terms(glossary_path)

    assert [term.english for term in terms] == ["Golgotha"]


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


def test_json_locations_are_one_based_for_first_and_second_entries(tmp_path: Path) -> None:
    """JSON entry locations follow the translation-token checker's 1-based indexing."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    _write_glossary(glossary_path)
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {"key": "First source", "text": "Golgotha remains untranslated."},
            {"key": "Second source", "text": "Kyakukya remains untranslated."},
        ],
    )

    result = check_glossary_consistency.check_paths([localization], glossary_path=glossary_path, baseline_path=None)

    assert [(issue.term, issue.location) for issue in result.issues] == [
        ("Golgotha", "entry[1].text"),
        ("Kyakukya", "entry[2].text"),
    ]


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


def test_baseline_matches_one_based_json_entry_locations(tmp_path: Path) -> None:
    """Baseline rows suppress JSON entries using 1-based entry[N].text locations."""
    glossary_path = tmp_path / "glossary.csv"
    localization = tmp_path / "Localization"
    baseline_path = tmp_path / "baseline.json"
    _write_glossary(glossary_path)
    _write_entries(
        localization / "Dictionaries" / "demo.ja.json",
        [
            {"key": "First source", "text": "Golgotha remains for now."},
            {"key": "Second source", "text": "Kyakukya remains for now."},
        ],
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
            "text": "Kyakukya remains for now.",
        },
    )

    result = check_glossary_consistency.check_paths(
        [localization],
        glossary_path=glossary_path,
        baseline_path=baseline_path,
    )

    assert result.issues == []


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


def test_missing_baseline_path_raises_explicit_error(tmp_path: Path) -> None:
    """Passing a baseline path must fail explicitly if the file is absent."""
    baseline_path = tmp_path / "missing-baseline.json"

    with pytest.raises(FileNotFoundError, match=r"missing-baseline\.json"):
        _check_with_baseline(tmp_path, baseline_path)


def test_baseline_rejects_wrong_version(tmp_path: Path) -> None:
    """Baseline schema errors use ValueError for caller-visible validation failures."""
    baseline_path = tmp_path / "baseline.json"
    _ = baseline_path.write_text(
        json.dumps({"version": 2, "allowed_occurrences": []}),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="expected version 1"):
        _check_with_baseline(tmp_path, baseline_path)


def test_baseline_rejects_duplicate_occurrences(tmp_path: Path) -> None:
    """Duplicate baseline rows are rejected instead of silently collapsing."""
    baseline_path = tmp_path / "baseline.json"
    occurrence = {
        "term": "Golgotha",
        "path": "Dictionaries/demo.ja.json",
        "location": "entry[1].text",
        "text": "Golgotha remains for now.",
    }
    _write_baseline(baseline_path, occurrence, occurrence)

    with pytest.raises(ValueError, match="duplicate occurrence"):
        _check_with_baseline(tmp_path, baseline_path)


@pytest.mark.parametrize(
    ("occurrence", "message"),
    [
        (["not", "an", "object"], "Invalid glossary baseline occurrence"),
        ({"term": "Golgotha", "path": "Dictionaries/demo.ja.json", "location": "entry[1].text"}, "field 'text'"),
        (
            {"term": "Golgotha", "path": "Dictionaries/demo.ja.json", "location": "entry[1].text", "text": 123},
            "field 'text'",
        ),
    ],
)
def test_baseline_rejects_malformed_or_missing_required_fields(
    tmp_path: Path,
    occurrence: object,
    message: str,
) -> None:
    """Each occurrence must be an object with the required string fields."""
    baseline_path = tmp_path / "baseline.json"
    _ = baseline_path.write_text(
        json.dumps({"version": 1, "allowed_occurrences": [occurrence]}),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match=message):
        _check_with_baseline(tmp_path, baseline_path)


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
