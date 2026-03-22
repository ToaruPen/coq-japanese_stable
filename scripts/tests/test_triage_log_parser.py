"""Tests for Player.log parser."""

from __future__ import annotations

from typing import TYPE_CHECKING

from scripts.triage.log_parser import parse_log

if TYPE_CHECKING:
    from pathlib import Path
from scripts.triage.models import LogEntryKind


def _write_log(path: Path, lines: list[str]) -> None:
    """Write lines to a fake Player.log file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def test_parse_missing_key(tmp_path: Path) -> None:
    """Parse a missing key line with route context."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'Inventory' (hit 1). (context: UITextSkinTranslationPatch)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.MISSING_KEY
    assert entries[0].text == "Inventory"
    assert entries[0].route == "UITextSkinTranslationPatch"
    assert entries[0].hits == 1
    assert entries[0].line_number == 1


def test_parse_no_pattern(tmp_path: Path) -> None:
    """Parse a no-pattern line."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] MessagePatternTranslator: no pattern for 'Game saved!' (hit 1). (context: MessageLogPatch)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.NO_PATTERN
    assert entries[0].text == "Game saved!"
    assert entries[0].route == "MessageLogPatch"


def test_parse_empty_missing_key(tmp_path: Path) -> None:
    """Empty missing-key strings are preserved instead of being dropped."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key '' (hit 2). (context: UITextSkinTranslationPatch)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.MISSING_KEY
    assert entries[0].text == ""
    assert entries[0].hits == 2


def test_parse_empty_no_pattern(tmp_path: Path) -> None:
    """Empty no-pattern strings are preserved instead of being dropped."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] MessagePatternTranslator: no pattern for '' (hit 8). (context: MessageLogPatch)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.NO_PATTERN
    assert entries[0].text == ""
    assert entries[0].hits == 8


def test_parse_missing_key_with_nested_context(tmp_path: Path) -> None:
    """Route is extracted as primary context before the nested separator."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'covered in liquid'"
            " (hit 1). (context: CharacterStatusScreenTranslationPatch"
            " > field=statusText)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].route == "CharacterStatusScreenTranslationPatch"


def test_parse_skips_non_qud_lines(tmp_path: Path) -> None:
    """Non-QudJP lines are ignored."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "Loading mods...",
            "[QudJP] Translator: missing key 'X' (hit 1). (context: R)",
            "Some other line",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1


def test_parse_deduplicates_by_text(tmp_path: Path) -> None:
    """Same key at different hit counts becomes a single entry with max hits."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'X' (hit 1). (context: R)",
            "[QudJP] Translator: missing key 'X' (hit 2). (context: R)",
            "[QudJP] Translator: missing key 'X' (hit 4). (context: R)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].hits == 4


def test_parse_empty_log(tmp_path: Path) -> None:
    """Empty log file returns an empty list."""
    log = tmp_path / "Player.log"
    _write_log(log, [])
    entries = parse_log(log)
    assert entries == []


def test_parse_dynamic_text_probe(tmp_path: Path) -> None:
    """DynamicTextProbe lines are parsed into structured entries."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] DynamicTextProbe/v1:"
            " route='UITextSkinTranslationPatch'"
            " family='CharacterStatusFamily'"
            " hit=8 changed=true"
            " source='Points Remaining: 12'"
            " translated='残りポイント: 12'."
            " (context: CharacterStatusScreenTranslationPatch"
            " > field=statusText)",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.DYNAMIC_TEXT_PROBE
    assert entries[0].route == "CharacterStatusScreenTranslationPatch"
    assert entries[0].family == "CharacterStatusFamily"
    assert entries[0].text == "Points Remaining: 12"
    assert entries[0].translated_text == "残りポイント: 12"
    assert entries[0].changed is True
