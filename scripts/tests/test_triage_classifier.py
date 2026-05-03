"""Tests for triage classifier."""

from __future__ import annotations

from scripts.triage.classifier import classify
from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification


def _mk(
    text: str,
    *,
    route: str = "UITextSkinTranslationPatch",
    kind: LogEntryKind = LogEntryKind.MISSING_KEY,
    family: str | None = None,
) -> LogEntry:
    """Create a ``LogEntry`` for testing."""
    return LogEntry(
        kind=kind,
        route=route,
        text=text,
        hits=1,
        line_number=1,
        family=family,
    )


def test_classify_simple_label() -> None:
    """A known simple label with no numbers or variables is static_leaf."""
    result = classify(_mk("Inventory"))
    assert result.classification == TriageClassification.STATIC_LEAF


def test_classify_ui_keyword() -> None:
    """Known UI labels are static_leaf."""
    result = classify(_mk("SKILLS"))
    assert result.classification == TriageClassification.STATIC_LEAF


def test_classify_no_pattern_is_route_patch() -> None:
    """A no-pattern entry from MessageLogPatch becomes route_patch."""
    result = classify(_mk("Game saved!", kind=LogEntryKind.NO_PATTERN, route="MessageLogPatch"))
    assert result.classification == TriageClassification.ROUTE_PATCH
    assert "pattern" in result.reason.lower()


def test_classify_unknown_no_pattern_route_is_unresolved() -> None:
    """Unknown no-pattern routes stay unresolved to avoid over-broad route patches."""
    result = classify(_mk("Game saved!", kind=LogEntryKind.NO_PATTERN, route="UnknownRoute"))
    assert result.classification == TriageClassification.UNRESOLVED


def test_classify_embedded_number() -> None:
    """Embedded numbers indicate a dynamic counter."""
    result = classify(_mk("Points Remaining: 12"))
    assert result.classification == TriageClassification.LOGIC_REQUIRED
    assert "12" in result.slot_evidence


def test_classify_preserved_stat_abbreviation() -> None:
    """Route-owned stat abbreviations are preserved English, not static leaves."""
    result = classify(_mk("STR", route="CharacterAttributeLineTranslationPatch"))
    assert result.classification == TriageClassification.PRESERVED_ENGLISH
    assert result.slot_evidence == ["STR"]


def test_classify_unexpected_translation_of_preserved_stat_abbreviation() -> None:
    """Dynamic probes flag protected tokens that were translated unexpectedly."""
    entry = LogEntry(
        kind=LogEntryKind.DYNAMIC_TEXT_PROBE,
        route="DescriptionLongDescriptionPatch",
        text="+1 STR",
        hits=1,
        line_number=1,
        family="Description.LongDescription",
        translated_text="+1 筋力",
        changed=True,
    )
    result = classify(entry)
    assert result.classification == TriageClassification.UNEXPECTED_TRANSLATION_OF_PRESERVED_TOKEN
    assert result.slot_evidence == ["STR"]


def test_classify_lbs_is_globally_preserved_compact_weight_unit() -> None:
    """Compact lbs. labels are intentionally preserved across routes."""
    trade_result = classify(_mk("{{W|$50}} | {{K|123/200 lbs.}}", route="TradeScreenUpdateTotalsTranslationPatch"))
    japanese_weight_result = classify(_mk("\u91cd\u91cf\uff1a 1 lbs.", route="DescriptionLongDescriptionPatch"))
    english_weight_result = classify(_mk("Weight: 1 lbs.", route="DescriptionShortDescriptionPatch"))
    exact_result = classify(_mk("lbs.", route="<no-context>"))
    assert trade_result.classification == TriageClassification.PRESERVED_ENGLISH
    assert japanese_weight_result.classification == TriageClassification.PRESERVED_ENGLISH
    assert english_weight_result.classification == TriageClassification.LOGIC_REQUIRED
    assert exact_result.classification == TriageClassification.PRESERVED_ENGLISH


def test_classify_numeric_stat_line() -> None:
    """Weight or HP readouts are dynamic."""
    result = classify(_mk("56/240#"))
    assert result.classification == TriageClassification.LOGIC_REQUIRED


def test_classify_dram_quantity() -> None:
    """Dynamic quantities in item descriptions are logic_required."""
    result = classify(_mk("[31 drams of fresh water]"))
    assert result.classification == TriageClassification.LOGIC_REQUIRED
    assert "31" in result.slot_evidence


def test_classify_level_line() -> None:
    """Level lines contain dynamic slot values."""
    result = classify(_mk("Level: 1"))
    assert result.classification == TriageClassification.LOGIC_REQUIRED


def test_classify_fragment_trailing_space() -> None:
    """Trailing whitespace indicates string-fragment concatenation."""
    result = classify(_mk("You stagger "))
    assert result.classification == TriageClassification.LOGIC_REQUIRED
    assert "fragment" in result.reason.lower()


def test_classify_fragment_leading_space() -> None:
    """Leading whitespace indicates a suffix fragment."""
    result = classify(_mk(" with your shield block!"))
    assert result.classification == TriageClassification.LOGIC_REQUIRED


def test_classify_date_time() -> None:
    """Dynamic day names are logic_required."""
    result = classify(_mk("Last saved: Saturday"))
    assert result.classification == TriageClassification.LOGIC_REQUIRED


def test_classify_proper_noun_only() -> None:
    """A single unknown word is unresolved."""
    result = classify(_mk("Nigashrowar"))
    assert result.classification == TriageClassification.UNRESOLVED


def test_classify_version_string_is_unresolved() -> None:
    """Version numbers are not actionable translations."""
    result = classify(_mk("1.0.4"))
    assert result.classification == TriageClassification.UNRESOLVED


def test_classify_already_japanese() -> None:
    """Strings already containing Japanese stay unresolved."""
    result = classify(_mk("塩まみれのNigashrowar"))
    assert result.classification == TriageClassification.UNRESOLVED
    assert "japanese" in result.reason.lower() or "日本語" in result.reason.lower()


def test_classify_dynamic_probe_is_logic_required() -> None:
    """DynamicTextProbe entries are direct logic-required evidence."""
    result = classify(
        _mk(
            "Points Remaining: 12",
            route="CharacterStatusScreenTranslationPatch",
            kind=LogEntryKind.DYNAMIC_TEXT_PROBE,
            family="CharacterStatusFamily",
        ),
    )
    assert result.classification == TriageClassification.LOGIC_REQUIRED
    assert result.slot_evidence == ["CharacterStatusFamily"]


def test_classify_sink_observe_stays_non_actionable() -> None:
    """SinkObserve observations are Phase F evidence, not actionable route/static verdicts."""
    result = classify(
        _mk(
            "You catch fire",
            route="EmitMessage",
            kind=LogEntryKind.SINK_OBSERVE,
            family="sink_observe",
        ),
    )
    assert result.classification == TriageClassification.UNRESOLVED
    assert "phase f" in result.reason.lower() or "non-actionable" in result.reason.lower()


def test_classify_final_output_probe_stays_non_actionable() -> None:
    """FinalOutputProbe observations are Phase F evidence, not static-label candidates."""
    result = classify(
        _mk(
            "Inventory",
            route="UITextSkinTranslationPatch",
            kind=LogEntryKind.FINAL_OUTPUT_PROBE,
            family="final_output",
        ),
    )
    assert result.classification == TriageClassification.UNRESOLVED
    assert "phase f" in result.reason.lower() or "non-actionable" in result.reason.lower()
