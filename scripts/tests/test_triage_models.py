"""Tests for triage data models."""

from __future__ import annotations

from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification, TriageResult


def test_classification_values() -> None:
    """All four classification values exist."""
    assert TriageClassification.STATIC_LEAF.value == "static_leaf"
    assert TriageClassification.ROUTE_PATCH.value == "route_patch"
    assert TriageClassification.LOGIC_REQUIRED.value == "logic_required"
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
