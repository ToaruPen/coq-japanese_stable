"""Cross-reference dictionary entries against generator signatures."""

from __future__ import annotations

import re

from scripts.provenance.models import (
    AuditFinding,
    AuditFindingKind,
    DictEntry,
    GeneratorSignature,
    StringClassification,
)

_FRAGMENT_TRAILING_WS = re.compile(r"\s+$")
_FRAGMENT_LEADING_WS = re.compile(r"^\s+")
_FRAGMENT_OPEN_BRACKET = re.compile(r"(?:\[|\()$")
_MIN_SHARED_NAMESPACE_DEPTH = 2


def cross_reference(entries: list[DictEntry], signatures: list[GeneratorSignature]) -> list[AuditFinding]:
    """Flag dictionary entries that overlap generator-family namespaces.

    Args:
        entries: Dictionary entries under analysis.
        signatures: C# scanner output.

    Returns:
        Cross-reference findings highlighting likely misclassified dictionary entries.
    """
    generator_namespaces = {
        namespace
        for signature in signatures
        if signature.classification == StringClassification.GENERATOR_FAMILY
        if (namespace := _extract_namespace(signature)) is not None
    }

    findings: list[AuditFinding] = []
    for entry in entries:
        if not _context_overlaps_generator(entry.context, generator_namespaces):
            continue
        if _is_fragment_key(entry.key):
            findings.append(
                AuditFinding(
                    kind=AuditFindingKind.FRAGMENT_KEY,
                    dictionary_id=entry.dictionary_id,
                    key=entry.key,
                    message=(
                        "Fragment-style dictionary key overlaps a generator-family namespace; "
                        "prefer a template or patch route instead of a dictionary entry, "
                        "because it likely belongs to generator-family code"
                    ),
                )
            )
            continue
        findings.append(
            AuditFinding(
                kind=AuditFindingKind.FRAGMENT_KEY,
                dictionary_id=entry.dictionary_id,
                key=entry.key,
                message=(
                    f"Entry context {entry.context!r} shares a namespace with generator-family code; "
                    "verify this is not dynamically composed upstream"
                ),
            )
        )
    return findings


def _extract_namespace(signature: GeneratorSignature) -> str | None:
    """Extract a namespace hint from a generator signature."""
    if "." in signature.class_name:
        parts = signature.class_name.split(".")
        if len(parts) >= _MIN_SHARED_NAMESPACE_DEPTH:
            return ".".join(parts[:-1])
    source_hint = signature.source_file.removesuffix(".cs").replace("_", ".")
    if "." in source_hint:
        return ".".join(source_hint.split(".")[:-1])
    return None


def _context_overlaps_generator(entry_context: str | None, namespaces: set[str]) -> bool:
    """Return whether an entry context shares a namespace prefix depth of two or more."""
    if entry_context is None:
        return False
    entry_parts = _namespace_parts(entry_context)
    return any(
        _shared_prefix_depth(entry_parts, _namespace_parts(namespace)) >= _MIN_SHARED_NAMESPACE_DEPTH
        for namespace in namespaces
    )


def _namespace_parts(value: str) -> list[str]:
    """Split a dotted namespace string into normalized parts."""
    return [part for part in value.split(".") if part]


def _shared_prefix_depth(left: list[str], right: list[str]) -> int:
    """Count how many namespace segments two inputs share from the start."""
    depth = 0
    for left_part, right_part in zip(left, right, strict=False):
        if left_part != right_part:
            break
        depth += 1
    return depth


def _is_fragment_key(key: str) -> bool:
    """Return whether a key looks like a dynamic string fragment."""
    return bool(
        _FRAGMENT_TRAILING_WS.search(key)
        or _FRAGMENT_LEADING_WS.search(key)
        or _FRAGMENT_OPEN_BRACKET.search(key)
    )
