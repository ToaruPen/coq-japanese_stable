"""Tests for provenance data models."""

from __future__ import annotations

from scripts.provenance.models import (
    AuditFinding,
    AuditFindingKind,
    DictEntry,
    GeneratorSignature,
    ProvenanceReport,
    StringClassification,
)


def test_string_classification_values() -> None:
    """Enum values match the public JSON contract."""
    assert StringClassification.EXACT_LEAF.value == "exact_leaf"
    assert StringClassification.STRUCTURED_DYNAMIC.value == "structured_dynamic"
    assert StringClassification.GENERATOR_FAMILY.value == "generator_family"
    assert StringClassification.OPAQUE.value == "opaque"


def test_dict_entry_creation() -> None:
    """DictEntry stores required fields."""
    entry = DictEntry(
        dictionary_id="ui-default",
        key="Inventory",
        text="インベントリ",
        context=None,
    )
    assert entry.dictionary_id == "ui-default"
    assert entry.context is None


def test_dict_entry_with_context() -> None:
    """DictEntry preserves optional context values."""
    entry = DictEntry(
        dictionary_id="ui-messagelog-world",
        key="You stagger {target} with your shield block!",
        text="{target}を盾受けでよろめかせた！",  # noqa: RUF001
        context="XRL.Messages.MessageQueue",
    )
    assert entry.context == "XRL.Messages.MessageQueue"


def test_audit_finding_creation() -> None:
    """AuditFinding stores its kind and related metadata."""
    finding = AuditFinding(
        kind=AuditFindingKind.FRAGMENT_KEY,
        dictionary_id="ui-messagelog",
        key="You stagger ",
        message="Key has trailing whitespace, likely a string concatenation fragment",
    )
    assert finding.kind == AuditFindingKind.FRAGMENT_KEY


def test_generator_signature_creation() -> None:
    """Generator signatures record scanner evidence."""
    signature = GeneratorSignature(
        source_file="XRL_World_DescriptionBuilder.cs",
        class_name="XRL.World.DescriptionBuilder",
        method_name="ToString",
        classification=StringClassification.GENERATOR_FAMILY,
        pattern_kind="DescriptionBuilder.Add* composition",
        evidence_line=331,
    )
    assert signature.classification == StringClassification.GENERATOR_FAMILY


def test_provenance_report_defaults() -> None:
    """ProvenanceReport starts with empty collections by default."""
    report = ProvenanceReport()
    assert report.audit_findings == []
    assert report.findings_by_kind == {}
