"""Shared data models for provenance analysis."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum


class StringClassification(StrEnum):
    """Four-category classification for translation string provenance."""

    EXACT_LEAF = "exact_leaf"
    STRUCTURED_DYNAMIC = "structured_dynamic"
    GENERATOR_FAMILY = "generator_family"
    OPAQUE = "opaque"


class AuditFindingKind(StrEnum):
    """Kinds of issues the provenance pipeline can report."""

    FRAGMENT_KEY = "fragment_key"
    DUPLICATE_KEY = "duplicate_key"
    PLACEHOLDER_MISMATCH = "placeholder_mismatch"
    COPY_PASTE_ERROR = "copy_paste_error"
    DEAD_PLACEHOLDER = "dead_placeholder"


@dataclass(frozen=True)
class DictEntry:
    """A single translation dictionary entry.

    Attributes:
        dictionary_id: Stable dictionary identifier.
        key: English lookup key.
        text: Localized output text.
        context: Optional source or route namespace hint.
    """

    dictionary_id: str
    key: str
    text: str
    context: str | None


@dataclass(frozen=True)
class AuditFinding:
    """A structured issue emitted by an audit or cross-reference pass.

    Attributes:
        kind: Category of finding.
        dictionary_id: Dictionary that owns the finding.
        key: Entry key associated with the finding.
        message: Human-readable explanation.
        related_dictionary_id: Optional secondary dictionary involved in the finding.
        related_key: Optional secondary key involved in the finding.
    """

    kind: AuditFindingKind
    dictionary_id: str
    key: str
    message: str
    related_dictionary_id: str | None = None
    related_key: str | None = None


@dataclass(frozen=True)
class GeneratorSignature:
    """A string-producing method detected in decompiled C# source.

    Attributes:
        source_file: Decompiled source file name.
        class_name: Fully qualified class name when available.
        method_name: Method that matched a provenance heuristic.
        classification: Provenance classification assigned to the method.
        pattern_kind: Short label describing the matched heuristic.
        evidence_line: 1-based source line of the matched evidence.
    """

    source_file: str
    class_name: str
    method_name: str
    classification: StringClassification
    pattern_kind: str
    evidence_line: int


@dataclass(frozen=True)
class ProvenanceReport:
    """Combined provenance analysis output.

    Attributes:
        audit_findings: Dictionary-auditor findings.
        xref_findings: Cross-reference findings.
        generator_signatures: Scanner matches catalogued from C# source.
        findings_by_kind: Count of all findings grouped by kind value.
    """

    audit_findings: list[AuditFinding] = field(default_factory=list)
    xref_findings: list[AuditFinding] = field(default_factory=list)
    generator_signatures: list[GeneratorSignature] = field(default_factory=list)
    findings_by_kind: dict[str, int] = field(default_factory=dict)
