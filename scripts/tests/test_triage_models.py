"""Tests for triage data models."""

from __future__ import annotations

import pytest

from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification, TriageResult

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


def _structured_suffix_items(line: str) -> list[tuple[str, str | None]]:
    r"""Return ordered structured suffix fields for unescaped fixture data only.

    This helper intentionally does not handle escaped delimiters like ``\\;`` or ``\\=``;
    those transport cases are covered by parser tests instead of this fixture splitter.
    """
    _prefix, separator, suffix = line.partition("; ")
    if not separator:
        return []

    items: list[tuple[str, str | None]] = []
    for token in suffix.split("; "):
        key, value = token.split("=", maxsplit=1)
        items.append((key, None if value == "<missing>" else value))
    return items


def test_classification_values() -> None:
    """All four classification values exist."""
    assert TriageClassification.STATIC_LEAF.value == "static_leaf"
    assert TriageClassification.ROUTE_PATCH.value == "route_patch"
    assert TriageClassification.LOGIC_REQUIRED.value == "logic_required"
    assert TriageClassification.PRESERVED_ENGLISH.value == "preserved_english"
    assert (
        TriageClassification.UNEXPECTED_TRANSLATION_OF_PRESERVED_TOKEN.value
        == "unexpected_translation_of_preserved_token"
    )
    assert TriageClassification.RUNTIME_NOISE.value == "runtime_noise"
    assert TriageClassification.UNRESOLVED.value == "unresolved"


def test_log_entry_missing_key() -> None:
    """LogEntry captures missing key data from Player.log."""
    entry = LogEntry(
        kind=LogEntryKind.MISSING_KEY,
        route="UITextSkinTranslationPatch",
        text="Points Remaining: 12",
        hits=4,
        line_number=331,
    )
    assert entry.route == "UITextSkinTranslationPatch"
    assert entry.kind == LogEntryKind.MISSING_KEY


def test_log_entry_no_pattern() -> None:
    """LogEntry captures no-pattern data from Player.log."""
    entry = LogEntry(
        kind=LogEntryKind.NO_PATTERN,
        route="MessageLogPatch",
        text="Game saved!",
        hits=1,
        line_number=875,
    )
    assert entry.kind == LogEntryKind.NO_PATTERN


def test_triage_result() -> None:
    """TriageResult links a LogEntry to a classification with reasoning."""
    entry = LogEntry(
        kind=LogEntryKind.MISSING_KEY,
        route="UITextSkinTranslationPatch",
        text="Points Remaining: 12",
        hits=4,
        line_number=331,
    )
    result = TriageResult(
        entry=entry,
        classification=TriageClassification.LOGIC_REQUIRED,
        reason="Contains embedded number '12' — likely a dynamic counter",
        slot_evidence=["12"],
    )
    assert result.classification == TriageClassification.LOGIC_REQUIRED
    assert result.slot_evidence == ["12"]


def test_dynamic_text_probe_fields_are_optional() -> None:
    """DynamicTextProbe-specific fields are supported without affecting other entries."""
    entry = LogEntry(
        kind=LogEntryKind.DYNAMIC_TEXT_PROBE,
        route="CharacterStatusScreenTranslationPatch",
        text="Points Remaining: 12",
        hits=2,
        line_number=14,
        family="CharacterStatusFamily",
        translated_text="残りポイント: 12",
        changed=True,
    )
    assert entry.family == "CharacterStatusFamily"
    assert entry.changed is True


def test_phase_f_structured_fields_are_optional() -> None:
    """Structured Phase F fields normalize missing template IDs and remain optional."""
    entry = LogEntry(
        kind=LogEntryKind.SINK_OBSERVE,
        route="EmitMessage",
        text="You catch fire",
        hits=None,
        line_number=41,
        template_id=None,
        payload_mode="full",
        payload_excerpt="You catch fire",
        payload_sha256=None,
    )
    assert entry.kind == LogEntryKind.SINK_OBSERVE
    assert entry.template_id is None
    assert entry.payload_mode == "full"
    assert entry.payload_sha256 is None


@pytest.mark.parametrize(
    ("line", "expected_items"),
    [
        (
            _MISSING_KEY_NEW,
            [
                ("route", "ExactKey"),
                ("family", "missing_key"),
                ("template_id", None),
                ("rendered_text_sample", "Put away"),
            ],
        ),
        (
            _NO_PATTERN_NEW,
            [
                ("route", "MessagePattern"),
                ("family", "message_pattern"),
                ("template_id", None),
                ("rendered_text_sample", "You catch fire"),
            ],
        ),
        (
            _DYNAMIC_PROBE_NEW,
            [
                ("route", "DoesVerbRoute"),
                ("family", "verb"),
                ("template_id", None),
                ("payload_mode", "full"),
                ("payload_excerpt", "You catch fire"),
                ("payload_sha256", None),
            ],
        ),
        (
            _SINK_OBSERVE_NEW,
            [
                ("route", "EmitMessage"),
                ("family", "sink_observe"),
                ("template_id", None),
                ("payload_mode", "full"),
                ("payload_excerpt", "You catch fire"),
                ("payload_sha256", None),
            ],
        ),
    ],
)
def test_phase_f_structured_suffix_contract_is_frozen(
    line: str,
    expected_items: list[tuple[str, str | None]],
) -> None:
    """The fixed plan examples keep their exact field order and `<missing>` null mapping."""
    assert _structured_suffix_items(line) == expected_items


@pytest.mark.parametrize(
    "line",
    [_MISSING_KEY_OLD, _NO_PATTERN_OLD, _DYNAMIC_PROBE_OLD, _SINK_OBSERVE_OLD],
)
def test_phase_f_old_examples_remain_suffix_free(line: str) -> None:
    """Old examples stay parseable without any structured Phase F suffix."""
    assert _structured_suffix_items(line) == []
