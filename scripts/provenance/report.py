"""Generate structured provenance analysis reports."""

from __future__ import annotations

import json
from collections import Counter
from dataclasses import asdict

from scripts.provenance.models import AuditFinding, GeneratorSignature, ProvenanceReport


def generate_report(
    *,
    audit_findings: list[AuditFinding],
    generator_signatures: list[GeneratorSignature],
    xref_findings: list[AuditFinding],
) -> str:
    """Serialize provenance analysis output to JSON.

    Args:
        audit_findings: Findings from dictionary-auditor checks.
        generator_signatures: Generator signatures catalogued from C# source.
        xref_findings: Findings from the cross-reference pass.

    Returns:
        Pretty-printed JSON report text.
    """
    combined_findings = audit_findings + xref_findings
    report = ProvenanceReport(
        audit_findings=audit_findings,
        xref_findings=xref_findings,
        generator_signatures=generator_signatures,
        findings_by_kind=dict(Counter(finding.kind.value for finding in combined_findings)),
    )
    payload = {
        "summary": {
            "total_audit_findings": len(report.audit_findings),
            "total_xref_findings": len(report.xref_findings),
            "total_generators": len(report.generator_signatures),
            "findings_by_kind": report.findings_by_kind,
        },
        "audit_findings": [_serialize_finding(finding) for finding in report.audit_findings],
        "xref_findings": [_serialize_finding(finding) for finding in report.xref_findings],
        "generator_catalog": [_serialize_signature(signature) for signature in report.generator_signatures],
    }
    return json.dumps(payload, ensure_ascii=False, indent=2)


def _serialize_finding(finding: AuditFinding) -> dict[str, object]:
    """Convert an audit finding to a JSON-serializable dictionary."""
    data = asdict(finding)
    data["kind"] = finding.kind.value
    return data


def _serialize_signature(signature: GeneratorSignature) -> dict[str, object]:
    """Convert a generator signature to a JSON-serializable dictionary."""
    data = asdict(signature)
    data["classification"] = signature.classification.value
    return data
