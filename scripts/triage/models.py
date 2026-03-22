"""Data models for the triage pipeline."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class LogEntryKind(Enum):
    """Kind of untranslated string observation."""

    MISSING_KEY = "missing_key"
    NO_PATTERN = "no_pattern"
    DYNAMIC_TEXT_PROBE = "dynamic_text_probe"


class TriageClassification(Enum):
    """Family attribution classification.

    Maps to the project's canonical two-category distinction:
    - static_leaf and route_patch → asset-solvable
    - logic_required → logic-required
    - unresolved → needs investigation before any action
    """

    STATIC_LEAF = "static_leaf"
    ROUTE_PATCH = "route_patch"
    LOGIC_REQUIRED = "logic_required"
    UNRESOLVED = "unresolved"


@dataclass(frozen=True)
class LogEntry:
    """A single untranslated string observation from Player.log."""

    kind: LogEntryKind
    route: str
    text: str
    hits: int
    line_number: int
    family: str | None = None
    translated_text: str | None = None
    changed: bool | None = None


@dataclass(frozen=True)
class TriageResult:
    """Classification result for a single untranslated string."""

    entry: LogEntry
    classification: TriageClassification
    reason: str
    slot_evidence: list[str] = field(default_factory=list)
