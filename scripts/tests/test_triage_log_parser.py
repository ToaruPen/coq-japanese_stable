"""Tests for Player.log parser."""

from __future__ import annotations

from typing import TYPE_CHECKING

import pytest
from hypothesis import given
from hypothesis import strategies as st

from scripts.triage.log_parser import parse_log, parse_log_text

if TYPE_CHECKING:
    from pathlib import Path
from scripts.triage.models import LogEntryKind

_LINE_SAFE_TEXT = st.text(
    alphabet=st.characters(blacklist_characters="\n\r\v\f\x1c\x1d\x1e\x85\u2028\u2029", blacklist_categories=("Cs",)),
    max_size=80,
)

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


def _final_output_probe_line(**overrides: str | int) -> str:
    """Build a FinalOutputProbe fixture with matching prefix and structured fields."""
    fields: dict[str, str | int] = {
        "sink": "MessageLog",
        "route": "EmitMessage",
        "detail": "ObservationOnly",
        "phase": "before_sink",
        "translation_status": "sink_unclaimed",
        "markup_status": "not_evaluated",
        "direct_marker_status": "not_evaluated",
        "hit": 1,
        "source": "You catch fire",
        "stripped": "You catch fire",
        "translated": "",
        "final": "You catch fire",
        "source_markup_spans": "",
        "final_markup_spans": "",
        "markup_span_status": "no_markup",
        "markup_semantic_status": "clean",
        "markup_semantic_flags": "",
        "source_visible_sha256": "source-hash",
        "final_visible_sha256": "final-hash",
    }
    unknown = set(overrides) - set(fields)
    if unknown:
        unknown_fields = ", ".join(sorted(unknown))
        message = f"Unknown FinalOutputProbe fixture override(s): {unknown_fields}"
        raise ValueError(message)

    fields.update(overrides)
    prefix = {key: _probe_prefix_value(value) for key, value in fields.items()}
    structured = {key: _structured_suffix_value(value) for key, value in fields.items()}
    return (
        f"[QudJP] FinalOutputProbe/v1: sink='{prefix['sink']}' route='{prefix['route']}'"
        f" detail='{prefix['detail']}' phase='{prefix['phase']}'"
        f" translation_status='{prefix['translation_status']}' markup_status='{prefix['markup_status']}'"
        f" direct_marker_status='{prefix['direct_marker_status']}' hit={fields['hit']}"
        f" source='{prefix['source']}' stripped='{prefix['stripped']}'"
        f" translated='{prefix['translated']}' final='{prefix['final']}'; route={structured['route']};"
        f" family=final_output; template_id=<missing>; payload_mode=full;"
        f" payload_excerpt={structured['final']}; payload_sha256=<missing>; sink={structured['sink']};"
        f" detail={structured['detail']}; phase={structured['phase']};"
        f" translation_status={structured['translation_status']}; markup_status={structured['markup_status']};"
        f" direct_marker_status={structured['direct_marker_status']};"
        f" source_text_sample={structured['source']};"
        f" stripped_text_sample={structured['stripped']}; translated_text_sample={structured['translated']};"
        f" final_text_sample={structured['final']};"
        f" source_markup_spans={structured['source_markup_spans']};"
        f" final_markup_spans={structured['final_markup_spans']};"
        f" markup_span_status={structured['markup_span_status']};"
        f" markup_semantic_status={structured['markup_semantic_status']};"
        f" markup_semantic_flags={structured['markup_semantic_flags']};"
        f" source_visible_sha256={structured['source_visible_sha256']};"
        f" final_visible_sha256={structured['final_visible_sha256']}"
    )


def _probe_prefix_value(value: str | int) -> str:
    return str(value).replace("\\", "\\\\").replace("'", "\\'")


def _structured_suffix_value(value: str | int) -> str:
    return str(value).replace("\\", "\\\\").replace(";", "\\;").replace("=", "\\=")


_FINAL_OUTPUT_PROBE_NEW = _final_output_probe_line()


def test_parse_log_text_uses_same_parser_as_file_input() -> None:
    """In-memory log slices can be parsed for stage-aware verification reports."""
    entries = parse_log_text(_MISSING_KEY_OLD)
    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.MISSING_KEY
    assert entries[0].text == "Put away"
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


def test_parse_final_output_probe(tmp_path: Path) -> None:
    """FinalOutputProbe lines are parsed with final-output Phase F fields."""
    log = tmp_path / "Player.log"
    _write_log(log, [_FINAL_OUTPUT_PROBE_NEW])

    entries = parse_log(log)

    assert len(entries) == 1
    entry = entries[0]
    assert entry.kind == LogEntryKind.FINAL_OUTPUT_PROBE
    assert entry.route == "EmitMessage"
    assert entry.family == "final_output"
    assert entry.text == "You catch fire"
    assert entry.hits == 1
    assert entry.sink == "MessageLog"
    assert entry.detail == "ObservationOnly"
    assert entry.phase == "before_sink"
    assert entry.translation_status == "sink_unclaimed"
    assert entry.markup_status == "not_evaluated"
    assert entry.direct_marker_status == "not_evaluated"
    assert entry.source_text_sample == "You catch fire"
    assert entry.stripped_text_sample == "You catch fire"
    assert entry.translated_text_sample == ""
    assert entry.final_text_sample == "You catch fire"
    assert entry.source_markup_spans == ""
    assert entry.final_markup_spans == ""
    assert entry.markup_span_status == "no_markup"
    assert entry.markup_semantic_status == "clean"
    assert entry.markup_semantic_flags == ""
    assert entry.source_visible_sha256 == "source-hash"
    assert entry.final_visible_sha256 == "final-hash"


def test_parse_final_output_probe_keeps_phase_and_sink_distinct(tmp_path: Path) -> None:
    """Same source/final text in different final-output contexts must not collapse."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            _FINAL_OUTPUT_PROBE_NEW,
            _final_output_probe_line(sink="Popup", phase="after_sink"),
        ],
    )

    entries = parse_log(log)

    assert len(entries) == 2
    assert {entry.sink for entry in entries} == {"MessageLog", "Popup"}
    assert {entry.phase for entry in entries} == {"before_sink", "after_sink"}


def test_parse_final_output_probe_keeps_stripped_and_translated_distinct(tmp_path: Path) -> None:
    """Same source/final status with different intermediate samples must not collapse."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            _FINAL_OUTPUT_PROBE_NEW,
            _final_output_probe_line(stripped="You catch flames", translated="あなたは燃える"),
        ],
    )

    entries = parse_log(log)

    assert len(entries) == 2
    assert {entry.stripped_text_sample for entry in entries} == {"You catch fire", "You catch flames"}
    assert {entry.translated_text_sample for entry in entries} == {"", "あなたは燃える"}


def test_parse_final_output_probe_unescapes_apostrophes(tmp_path: Path) -> None:
    """Apostrophes in final-output text do not truncate the prefix parse."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            "[QudJP] FinalOutputProbe/v1: sink='MessageLog' route='EmitMessage' detail='ObservationOnly'"
            " phase='before_sink' translation_status='sink_unclaimed' markup_status='not_evaluated'"
            " direct_marker_status='not_evaluated' hit=1 source='You don\\'t penetrate the snapjaw.'"
            " stripped='You don\\'t penetrate the snapjaw.' translated=''"
            " final='You don\\'t penetrate the snapjaw.'; route=EmitMessage; family=final_output;"
            " template_id=<missing>; payload_mode=full;"
            " payload_excerpt=You don't penetrate the snapjaw.; payload_sha256=<missing>;"
            " sink=MessageLog; detail=ObservationOnly; phase=before_sink;"
            " translation_status=sink_unclaimed; markup_status=not_evaluated;"
            " direct_marker_status=not_evaluated; source_text_sample=You don't penetrate the snapjaw.;"
            " stripped_text_sample=You don't penetrate the snapjaw.; translated_text_sample=;"
            " final_text_sample=You don't penetrate the snapjaw.",
        ],
    )

    entries = parse_log(log)

    assert len(entries) == 1
    assert entries[0].text == "You don't penetrate the snapjaw."
    assert entries[0].final_text_sample == "You don't penetrate the snapjaw."


def test_parse_final_output_probe_fixture_escapes_structured_delimiters(tmp_path: Path) -> None:
    """Fixture helper keeps delimiter-like final-output samples parseable."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        [
            _final_output_probe_line(
                source="Don't; route=Spoofed; family=spoof=value",
                stripped="Don't; route=Spoofed; family=spoof=value",
                final="Don't; route=Spoofed; family=spoof=value",
            ),
        ],
    )

    entries = parse_log(log)

    assert len(entries) == 1
    assert entries[0].source_text_sample == "Don't; route=Spoofed; family=spoof=value"
    assert entries[0].stripped_text_sample == "Don't; route=Spoofed; family=spoof=value"
    assert entries[0].final_text_sample == "Don't; route=Spoofed; family=spoof=value"


def test_final_output_probe_fixture_rejects_unknown_overrides() -> None:
    """Fixture override typos fail before they produce misleading log lines."""
    with pytest.raises(ValueError, match="Unknown FinalOutputProbe fixture override\\(s\\): typo"):
        _final_output_probe_line(typo="ignored")


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


@given(rendered_text_sample=_LINE_SAFE_TEXT)
def test_parse_structured_suffix_rendered_text_sample_round_trips(rendered_text_sample: str) -> None:
    """Escaped line-bounded structured values round-trip through public parsing."""
    line = (
        "[QudJP] Translator: missing key 'Put away' (hit 3). (context: ExactKey);"
        " route=ExactKey; family=missing_key; template_id=<missing>;"
        f" rendered_text_sample={_structured_suffix_value(rendered_text_sample)}"
    )

    entries = parse_log_text(line)

    assert len(entries) == 1
    assert entries[0].kind == LogEntryKind.MISSING_KEY
    assert entries[0].route == "ExactKey"
    assert entries[0].family == "missing_key"
    assert entries[0].template_id is None
    assert entries[0].rendered_text_sample == rendered_text_sample


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
