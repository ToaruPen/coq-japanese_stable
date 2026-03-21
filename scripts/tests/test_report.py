"""Tests for provenance report generation."""

from __future__ import annotations

import json

from scripts.provenance.models import (
    AuditFinding,
    AuditFindingKind,
    GeneratorSignature,
    StringClassification,
)
from scripts.provenance.report import generate_report


def test_generate_report_json_structure() -> None:
    """Report JSON exposes the expected top-level sections and counts."""
    findings = [
        AuditFinding(
            kind=AuditFindingKind.FRAGMENT_KEY,
            dictionary_id="msg",
            key="You stagger ",
            message="trailing whitespace",
        )
    ]
    signatures = [
        GeneratorSignature(
            source_file="XRL_World_DescriptionBuilder.cs",
            class_name="XRL.World.DescriptionBuilder",
            method_name="ToString",
            classification=StringClassification.GENERATOR_FAMILY,
            pattern_kind="DescriptionBuilder.Add* composition",
            evidence_line=331,
        )
    ]
    report = generate_report(audit_findings=findings, generator_signatures=signatures, xref_findings=[])
    data = json.loads(report)
    assert set(data) == {"summary", "audit_findings", "xref_findings", "generator_catalog"}
    assert data["summary"]["total_audit_findings"] == 1
    assert data["summary"]["total_generators"] == 1


def test_generate_report_summary_by_kind() -> None:
    """Summary counts findings by enum value."""
    findings = [
        AuditFinding(kind=AuditFindingKind.FRAGMENT_KEY, dictionary_id="a", key="k1", message="m1"),
        AuditFinding(kind=AuditFindingKind.FRAGMENT_KEY, dictionary_id="b", key="k2", message="m2"),
        AuditFinding(kind=AuditFindingKind.DUPLICATE_KEY, dictionary_id="c", key="k3", message="m3"),
    ]
    report = generate_report(audit_findings=findings, generator_signatures=[], xref_findings=[])
    data = json.loads(report)
    assert data["summary"]["findings_by_kind"]["fragment_key"] == 2
    assert data["summary"]["findings_by_kind"]["duplicate_key"] == 1
