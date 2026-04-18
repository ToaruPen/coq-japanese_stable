"""Data models for the triage pipeline."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class LogEntryKind(Enum):
    """Kind of untranslated string observation."""

    MISSING_KEY = "missing_key"
    NO_PATTERN = "no_pattern"
    DYNAMIC_TEXT_PROBE = "dynamic_text_probe"
    SINK_OBSERVE = "sink_observe"


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
    PRESERVED_ENGLISH = "preserved_english"
    UNEXPECTED_TRANSLATION_OF_PRESERVED_TOKEN = "unexpected_translation_of_preserved_token"  # noqa: S105
    UNRESOLVED = "unresolved"


@dataclass(frozen=True)
class LogEntry:
    """A single untranslated string observation from Player.log."""

    kind: LogEntryKind
    route: str
    text: str
    hits: int | None
    line_number: int
    family: str | None = None
    translated_text: str | None = None
    changed: bool | None = None
    template_id: str | None = None
    rendered_text_sample: str | None = None
    payload_mode: str | None = None
    payload_excerpt: str | None = None
    payload_sha256: str | None = None
    structured_fields: frozenset[str] = field(default_factory=frozenset)

    def has_structured_field(self, field_name: str) -> bool:
        """Return whether the structured Phase F suffix explicitly carried ``field_name``."""
        return field_name in self.structured_fields

    def has_structured_phase_f_data(self) -> bool:
        """Return whether the entry carried any structured Phase F suffix fields."""
        return bool(self.structured_fields)


@dataclass(frozen=True)
class TriageResult:
    """Classification result for a single untranslated string."""

    entry: LogEntry
    classification: TriageClassification
    reason: str
    slot_evidence: list[str] = field(default_factory=list)
