"""Tests for Player.log parser."""

from __future__ import annotations

from typing import TYPE_CHECKING

import pytest

from scripts.triage.log_parser import parse_log

if TYPE_CHECKING:
    from pathlib import Path
from scripts.triage.models import LogEntryKind

_MISSING_KEY_OLD = "[QudJP] Translator: missing key 'Put away' (hit 3). (context: ExactKey)"
_MISSING_KEY_NEW = (
    "[QudJP] Translator: missing key 'Put away' (hit 3). (context: ExactKey);"
    " route=ExactKey; family=missing_key; template_id=<missing>; rendered_text_sample=Put away"
)
_NO_PATTERN_OLD = "[QudJP] MessagePatternTranslator: no pattern for 'You catch fire' (hit 2). (context: MessagePattern)"
_NO_PATTERN_NEW = (
    "[QudJP] MessagePatternTranslator: no pattern for 'You catch fire'"
    " (hit 2). (context: MessagePattern); route=MessagePattern; family=message_pattern;"
    " template_id=<missing>; rendered_text_sample=You catch fire"
)
_DYNAMIC_PROBE_OLD = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='You catch fire' translated='あなたは燃え上がる'. (context: DoesVerbRoute)"
)
_DYNAMIC_PROBE_NEW = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='You catch fire' translated='あなたは燃え上がる'. (context: DoesVerbRoute);"
    " route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=full;"
    " payload_excerpt=You catch fire; payload_sha256=<missing>"
)
_SINK_OBSERVE_OLD = (
    "[QudJP] SinkObserve/v1: sink='MessageLog' route='EmitMessage' detail='ObservationOnly'"
    " source='You catch fire' stripped='You catch fire'"
)
_SINK_OBSERVE_NEW = (
    "[QudJP] SinkObserve/v1: sink='MessageLog' route='EmitMessage' detail='ObservationOnly'"
    " source='You catch fire' stripped='You catch fire'; route=EmitMessage;"
    " family=sink_observe; template_id=<missing>; payload_mode=full;"
    " payload_excerpt=You catch fire; payload_sha256=<missing>"
)
_MISSING_KEY_ESCAPED = (
    "[QudJP] Translator: missing key 'Put away; route=Spoofed; family=spoof=value'"
    " (hit 3). (context: ExactKey); route=ExactKey; family=missing_key;"
    " template_id=<missing>; rendered_text_sample=Put away\\; route\\=Spoofed\\;"
    " family\\=spoof\\=value"
)
_DYNAMIC_PROBE_ESCAPED = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='You catch fire; route=Spoofed; family=spoof=value' translated='あなたは燃え上がる'."
    " (context: DoesVerbRoute); route=DoesVerbRoute; family=verb; template_id=<missing>;"
    " payload_mode=full; payload_excerpt=You catch fire\\; route\\=Spoofed\\;"
    " family\\=spoof\\=value; payload_sha256=<missing>"
)
_MISSING_KEY_LITERAL_MISSING = (
    "[QudJP] Translator: missing key 'Put away' (hit 3). (context: ExactKey);"
    " route=ExactKey; family=missing_key; template_id=<missing>; rendered_text_sample=<missing>"
)
_DYNAMIC_PROBE_LITERAL_MISSING = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='You catch fire' translated='あなたは燃え上がる'. (context: DoesVerbRoute);"
    " route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=full;"
    " payload_excerpt=<missing>; payload_sha256=<missing>"
)
_DYNAMIC_PROBE_PREFIX_HASH_A = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='Shared prefix payload' translated='あなたは燃え上がる'. (context: DoesVerbRoute);"
    " route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=prefix_hash;"
    " payload_excerpt=Shared prefix payload;"
    " payload_sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
)
_DYNAMIC_PROBE_PREFIX_HASH_B = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='Shared prefix payload' translated='あなたは燃え上がる'. (context: DoesVerbRoute);"
    " route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=prefix_hash;"
    " payload_excerpt=Shared prefix payload;"
    " payload_sha256=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
)


def _write_log(path: Path, lines: list[str]) -> None:
    """Write lines to a fake Player.log file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def _entry_snapshot(tmp_path: Path, line: str) -> dict[str, object | None]:
    """Parse a single fixture line and return the stable observable fields."""
    log = tmp_path / "Player.log"
    _write_log(log, [line])
    entries = parse_log(log)
    assert len(entries) == 1
    entry = entries[0]
    return {
        "kind": entry.kind.value,
        "route": entry.route,
        "text": entry.text,
        "hits": entry.hits,
        "line_number": entry.line_number,
        "family": entry.family,
        "translated_text": entry.translated_text,
        "changed": entry.changed,
        "template_id": entry.template_id,
        "rendered_text_sample": entry.rendered_text_sample,
        "payload_mode": entry.payload_mode,
        "payload_excerpt": entry.payload_excerpt,
        "payload_sha256": entry.payload_sha256,
        "structured_fields": sorted(entry.structured_fields),
    }


@pytest.mark.parametrize(
    ("line", "expected"),
    [
        (
            _MISSING_KEY_OLD,
            {
                "kind": "missing_key",
                "route": "ExactKey",
                "text": "Put away",
                "hits": 3,
                "line_number": 1,
                "family": None,
                "translated_text": None,
                "changed": None,
                "template_id": None,
                "rendered_text_sample": None,
                "payload_mode": None,
                "payload_excerpt": None,
                "payload_sha256": None,
                "structured_fields": [],
            },
        ),
        (
            _MISSING_KEY_NEW,
            {
                "kind": "missing_key",
                "route": "ExactKey",
                "text": "Put away",
                "hits": 3,
                "line_number": 1,
                "family": "missing_key",
                "translated_text": None,
                "changed": None,
                "template_id": None,
                "rendered_text_sample": "Put away",
                "payload_mode": None,
                "payload_excerpt": None,
                "payload_sha256": None,
                "structured_fields": ["family", "rendered_text_sample", "route", "template_id"],
            },
        ),
        (
            _NO_PATTERN_OLD,
            {
                "kind": "no_pattern",
                "route": "MessagePattern",
                "text": "You catch fire",
                "hits": 2,
                "line_number": 1,
                "family": None,
                "translated_text": None,
                "changed": None,
                "template_id": None,
                "rendered_text_sample": None,
                "payload_mode": None,
                "payload_excerpt": None,
                "payload_sha256": None,
                "structured_fields": [],
            },
        ),
        (
            _NO_PATTERN_NEW,
            {
                "kind": "no_pattern",
                "route": "MessagePattern",
                "text": "You catch fire",
                "hits": 2,
                "line_number": 1,
                "family": "message_pattern",
                "translated_text": None,
                "changed": None,
                "template_id": None,
                "rendered_text_sample": "You catch fire",
                "payload_mode": None,
                "payload_excerpt": None,
                "payload_sha256": None,
                "structured_fields": ["family", "rendered_text_sample", "route", "template_id"],
            },
        ),
        (
            _DYNAMIC_PROBE_OLD,
            {
                "kind": "dynamic_text_probe",
                "route": "DoesVerbRoute",
                "text": "You catch fire",
                "hits": 1,
                "line_number": 1,
                "family": "verb",
                "translated_text": "あなたは燃え上がる",
                "changed": True,
                "template_id": None,
                "rendered_text_sample": None,
                "payload_mode": None,
                "payload_excerpt": None,
                "payload_sha256": None,
                "structured_fields": [],
            },
        ),
        (
            _DYNAMIC_PROBE_NEW,
            {
                "kind": "dynamic_text_probe",
                "route": "DoesVerbRoute",
                "text": "You catch fire",
                "hits": 1,
                "line_number": 1,
                "family": "verb",
                "translated_text": "あなたは燃え上がる",
                "changed": True,
                "template_id": None,
                "rendered_text_sample": None,
                "payload_mode": "full",
                "payload_excerpt": "You catch fire",
                "payload_sha256": None,
                "structured_fields": [
                    "family",
                    "payload_excerpt",
                    "payload_mode",
                    "payload_sha256",
                    "route",
                    "template_id",
                ],
            },
        ),
        (
            _SINK_OBSERVE_OLD,
            {
                "kind": "sink_observe",
                "route": "EmitMessage",
                "text": "You catch fire",
                "hits": None,
                "line_number": 1,
                "family": None,
                "translated_text": None,
                "changed": None,
                "template_id": None,
                "rendered_text_sample": None,
                "payload_mode": None,
                "payload_excerpt": None,
                "payload_sha256": None,
                "structured_fields": [],
            },
        ),
        (
            _SINK_OBSERVE_NEW,
            {
                "kind": "sink_observe",
                "route": "EmitMessage",
                "text": "You catch fire",
                "hits": None,
                "line_number": 1,
                "family": "sink_observe",
                "translated_text": None,
                "changed": None,
                "template_id": None,
                "rendered_text_sample": None,
                "payload_mode": "full",
                "payload_excerpt": "You catch fire",
                "payload_sha256": None,
                "structured_fields": [
                    "family",
                    "payload_excerpt",
                    "payload_mode",
                    "payload_sha256",
                    "route",
                    "template_id",
                ],
            },
        ),
    ],
)
def test_parse_phase_f_old_and_new_examples_share_parser_expectations(
    tmp_path: Path,
    line: str,
    expected: dict[str, object | None],
) -> None:
    """The fixed old/new plan examples remain parseable by the current prefix parser."""
    assert _entry_snapshot(tmp_path, line) == expected


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


def test_parse_sink_observe(tmp_path: Path) -> None:
    """SinkObserve lines are parsed as separate Phase F entries."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] SinkObserve/v1:"
            " sink='MessageLog'"
            " route='EmitMessage'"
            " detail='ObservationOnly'"
            " source='Game saved!'"
            " stripped='Game saved!'",
        ],
    )
    entries = parse_log(log)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.SINK_OBSERVE
    assert entries[0].route == "EmitMessage"
    assert entries[0].text == "Game saved!"
    assert entries[0].hits is None


@pytest.mark.parametrize(
    ("line", "expected"),
    [
        (
            _MISSING_KEY_ESCAPED,
            (
                LogEntryKind.MISSING_KEY,
                "ExactKey",
                "missing_key",
                "rendered_text_sample",
                "Put away; route=Spoofed; family=spoof=value",
            ),
        ),
        (
            _DYNAMIC_PROBE_ESCAPED,
            (
                LogEntryKind.DYNAMIC_TEXT_PROBE,
                "DoesVerbRoute",
                "verb",
                "payload_excerpt",
                "You catch fire; route=Spoofed; family=spoof=value",
            ),
        ),
    ],
)
def test_parse_structured_suffix_unescapes_delimiter_like_values(
    tmp_path: Path,
    line: str,
    expected: tuple[LogEntryKind, str, str, str, str],
) -> None:
    """Escaped structured values round-trip without spoofing route/family fields."""
    log = tmp_path / "Player.log"
    _write_log(log, [line])
    expected_kind, expected_route, expected_family, expected_field, expected_value = expected

    entries = parse_log(log)

    assert len(entries) == 1
    assert entries[0].kind == expected_kind
    assert entries[0].route == expected_route
    assert entries[0].family == expected_family
    assert getattr(entries[0], expected_field) == expected_value


def test_parse_structured_suffix_preserves_literal_missing_in_text_fields(tmp_path: Path) -> None:
    """Literal `<missing>` survives for text-bearing fields while nullable slots stay ``None``."""
    log = tmp_path / "Player.log"
    _write_log(log, [_MISSING_KEY_LITERAL_MISSING, _DYNAMIC_PROBE_LITERAL_MISSING])

    entries = parse_log(log)

    assert len(entries) == 2
    assert entries[0].rendered_text_sample == "<missing>"
    assert entries[0].template_id is None
    assert entries[1].payload_excerpt == "<missing>"
    assert entries[1].payload_sha256 is None


def test_parse_phase_f_deduplication_keeps_distinct_payload_identities(tmp_path: Path) -> None:
    """Phase F helper records with distinct payload identities must not collapse together."""
    log = tmp_path / "Player.log"
    _write_log(log, [_DYNAMIC_PROBE_PREFIX_HASH_A, _DYNAMIC_PROBE_PREFIX_HASH_B])

    entries = parse_log(log)

    assert len(entries) == 2
    assert {entry.payload_sha256 for entry in entries} == {
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    }
