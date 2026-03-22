"""Inventory models and JSONL helpers for source-first scanner outputs."""

from __future__ import annotations

import json
from dataclasses import dataclass
from enum import StrEnum
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


class HitKind(StrEnum):
    """Classify whether a raw hit came from ast-grep or override grep."""

    SINK = "sink"
    OVERRIDE = "override"


class ExclusionReason(StrEnum):
    """Reasons a decompiled source file is excluded from scanning."""

    EMPTY_FILE = "empty_file"
    RETRY_ARTIFACT = "retry_artifact"
    MSGPROBE_ARTIFACT = "msgprobe_artifact"
    FLAT_NAMESPACE_DUPLICATE = "flat_namespace_duplicate"


class Confidence(StrEnum):
    """Confidence levels assigned during Phase 1b classification."""

    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


class SiteStatus(StrEnum):
    """Workflow status assigned after Phase 1d cross-reference."""

    TRANSLATED = "translated"
    NEEDS_TRANSLATION = "needs_translation"
    NEEDS_PATCH = "needs_patch"
    NEEDS_REVIEW = "needs_review"
    UNRESOLVED = "unresolved"
    EXCLUDED = "excluded"


class SiteType(StrEnum):
    """Candidate site types produced by the source-first classifier."""

    LEAF = "Leaf"
    TEMPLATE = "Template"
    BUILDER = "Builder"
    MESSAGE_FRAME = "MessageFrame"
    VERB_COMPOSITION = "VerbComposition"
    VARIABLE_TEMPLATE = "VariableTemplate"
    PROCEDURAL_TEXT = "ProceduralText"
    NARRATIVE_TEMPLATE = "NarrativeTemplate"
    UNRESOLVED = "Unresolved"


@dataclass(frozen=True, slots=True)
class RawHit:
    """A Phase 1a source hit emitted by ast-grep or override grep."""

    hit_kind: HitKind
    family: str
    pattern: str
    file: str
    line: int
    column: int
    matched_code: str

    def to_dict(self) -> dict[str, object]:
        """Serialize the hit for JSONL output."""
        return {
            "hit_kind": self.hit_kind.value,
            "family": self.family,
            "pattern": self.pattern,
            "file": self.file,
            "line": self.line,
            "column": self.column,
            "matched_code": self.matched_code,
        }

    @classmethod
    def from_dict(cls, payload: dict[str, object]) -> RawHit:
        """Deserialize a RawHit from a JSON-compatible dictionary."""
        return cls(
            hit_kind=HitKind(str(payload["hit_kind"])),
            family=str(payload["family"]),
            pattern=str(payload["pattern"]),
            file=str(payload["file"]),
            line=int(payload["line"]),
            column=int(payload["column"]),
            matched_code=str(payload["matched_code"]),
        )


@dataclass(frozen=True, slots=True)
class FileRecord:
    """A deduplicated or excluded `.cs` file entry for Phase 1a scanning."""

    path: str
    included: bool
    exclusion_reason: ExclusionReason | None = None
    duplicate_of: str | None = None

    def __post_init__(self) -> None:
        """Keep included/excluded metadata consistent."""
        if self.included and (self.exclusion_reason is not None or self.duplicate_of is not None):
            msg = "Included files cannot have exclusion metadata."
            raise ValueError(msg)
        if not self.included and self.exclusion_reason is None:
            msg = "Excluded files must include an exclusion reason."
            raise ValueError(msg)


@dataclass(frozen=True, slots=True)
class SourceFileInventory:
    """A stable, queryable view of files considered by the scanner."""

    files: tuple[FileRecord, ...]

    @property
    def included_files(self) -> tuple[FileRecord, ...]:
        """Return included file records in stable order."""
        return tuple(record for record in self.files if record.included)

    @property
    def excluded_files(self) -> tuple[FileRecord, ...]:
        """Return excluded file records in stable order."""
        return tuple(record for record in self.files if not record.included)

    @property
    def included_file_count(self) -> int:
        """Return the number of unique source files after deduplication."""
        return len(self.included_files)

    @property
    def included_path_set(self) -> frozenset[str]:
        """Return included file paths for fast membership checks."""
        return frozenset(record.path for record in self.included_files)


@dataclass(frozen=True, slots=True)
class InventorySite:
    """One Phase 1b classified candidate site."""

    id: str
    file: str
    line: int
    column: int
    sink: str
    type: SiteType
    confidence: Confidence
    pattern: str
    key: str | None = None
    verb: str | None = None
    extra: str | None = None
    frame: str | None = None
    lookup_tier: int | None = None
    source_context: str | None = None
    source: str | None = None
    source_id: str | None = None
    needs_review: bool = False
    needs_runtime: bool = False
    status: SiteStatus | None = None
    existing_patch: str | None = None
    existing_dictionary: str | None = None
    existing_xml: str | None = None

    def to_dict(self) -> dict[str, object]:
        """Serialize an inventory site as JSON-compatible data."""
        payload: dict[str, object] = {
            "id": self.id,
            "file": self.file,
            "line": self.line,
            "column": self.column,
            "sink": self.sink,
            "type": self.type.value,
            "confidence": self.confidence.value,
            "pattern": self.pattern,
            "needs_review": self.needs_review,
            "needs_runtime": self.needs_runtime,
        }
        optional_fields: dict[str, object | None] = {
            "key": self.key,
            "verb": self.verb,
            "extra": self.extra,
            "frame": self.frame,
            "lookup_tier": self.lookup_tier,
            "source_context": self.source_context,
            "source": self.source,
            "source_id": self.source_id,
            "status": self.status.value if self.status is not None else None,
            "existing_patch": self.existing_patch,
            "existing_dictionary": self.existing_dictionary,
            "existing_xml": self.existing_xml,
        }
        payload.update({key: value for key, value in optional_fields.items() if value is not None})
        return payload

    @classmethod
    def from_dict(cls, payload: dict[str, object]) -> InventorySite:
        """Deserialize an inventory site from JSON-compatible data."""
        return cls(
            id=str(payload["id"]),
            file=str(payload["file"]),
            line=int(payload["line"]),
            column=int(payload["column"]),
            sink=str(payload["sink"]),
            type=SiteType(str(payload["type"])),
            confidence=Confidence(str(payload["confidence"])),
            pattern=str(payload["pattern"]),
            key=_optional_string(payload.get("key")),
            verb=_optional_string(payload.get("verb")),
            extra=_optional_string(payload.get("extra")),
            frame=_optional_string(payload.get("frame")),
            lookup_tier=_optional_int(payload.get("lookup_tier")),
            source_context=_optional_string(payload.get("source_context")),
            source=_optional_string(payload.get("source")),
            source_id=_optional_string(payload.get("source_id")),
            needs_review=bool(payload.get("needs_review", False)),
            needs_runtime=bool(payload.get("needs_runtime", False)),
            status=_optional_status(payload.get("status")),
            existing_patch=_optional_string(payload.get("existing_patch")),
            existing_dictionary=_optional_string(payload.get("existing_dictionary")),
            existing_xml=_optional_string(payload.get("existing_xml")),
        )


@dataclass(frozen=True, slots=True)
class InventoryStats:
    """Summary counts for a classified inventory draft."""

    input_hits: int
    filtered_hits: int
    output_sites: int
    high_confidence: int
    medium_confidence: int
    low_confidence: int
    needs_review: int
    needs_runtime: int

    def to_dict(self) -> dict[str, int]:
        """Serialize summary stats."""
        return {
            "input_hits": self.input_hits,
            "filtered_hits": self.filtered_hits,
            "output_sites": self.output_sites,
            "high_confidence": self.high_confidence,
            "medium_confidence": self.medium_confidence,
            "low_confidence": self.low_confidence,
            "needs_review": self.needs_review,
            "needs_runtime": self.needs_runtime,
        }

    @classmethod
    def from_dict(cls, payload: dict[str, object]) -> InventoryStats:
        """Deserialize summary stats from JSON-compatible data."""
        return cls(
            input_hits=int(payload["input_hits"]),
            filtered_hits=int(payload["filtered_hits"]),
            output_sites=int(payload["output_sites"]),
            high_confidence=int(payload["high_confidence"]),
            medium_confidence=int(payload["medium_confidence"]),
            low_confidence=int(payload["low_confidence"]),
            needs_review=int(payload["needs_review"]),
            needs_runtime=int(payload["needs_runtime"]),
        )


@dataclass(frozen=True, slots=True)
class InventoryDraft:
    """Phase 1b inventory draft persisted to JSON."""

    version: str
    game_version: str
    scan_date: str
    stats: InventoryStats
    sites: tuple[InventorySite, ...]

    def to_dict(self) -> dict[str, object]:
        """Serialize the draft as a stable JSON-compatible payload."""
        return {
            "version": self.version,
            "game_version": self.game_version,
            "scan_date": self.scan_date,
            "stats": self.stats.to_dict(),
            "sites": [site.to_dict() for site in self.sites],
        }

    @classmethod
    def from_dict(cls, payload: dict[str, object]) -> InventoryDraft:
        """Deserialize an inventory draft from JSON-compatible data."""
        stats = InventoryStats.from_dict(dict(payload["stats"]))
        sites = tuple(InventorySite.from_dict(site) for site in payload["sites"])
        return cls(
            version=str(payload["version"]),
            game_version=str(payload["game_version"]),
            scan_date=str(payload["scan_date"]),
            stats=stats,
            sites=sites,
        )


def write_raw_hits_jsonl(path: Path, hits: list[RawHit]) -> None:
    """Write raw hits as deterministic JSON Lines."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for hit in hits:
            handle.write(json.dumps(hit.to_dict(), ensure_ascii=False, sort_keys=True))
            handle.write("\n")


def read_raw_hits_jsonl(path: Path) -> list[RawHit]:
    """Read raw hits from a JSONL file."""
    hits: list[RawHit] = []
    with path.open(encoding="utf-8") as handle:
        for line in handle:
            if not line.strip():
                continue
            hits.append(RawHit.from_dict(json.loads(line)))
    return hits


def write_inventory_draft_json(path: Path, draft: InventoryDraft) -> None:
    """Write a classified inventory draft as deterministic JSON."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(draft.to_dict(), handle, ensure_ascii=False, indent=2, sort_keys=True)
        handle.write("\n")


def read_inventory_draft_json(path: Path) -> InventoryDraft:
    """Read a classified inventory draft from disk."""
    with path.open(encoding="utf-8") as handle:
        return InventoryDraft.from_dict(json.load(handle))


def write_candidate_inventory_json(path: Path, draft: InventoryDraft) -> None:
    """Write a Phase 1d candidate inventory JSON payload."""
    write_inventory_draft_json(path, draft)


def read_candidate_inventory_json(path: Path) -> InventoryDraft:
    """Read a Phase 1d candidate inventory JSON payload."""
    return read_inventory_draft_json(path)


def _optional_int(value: object | None) -> int | None:
    """Convert an optional JSON number to int."""
    if value is None:
        return None
    return int(value)


def _optional_string(value: object | None) -> str | None:
    """Convert an optional JSON string value."""
    if value is None:
        return None
    return str(value)


def _optional_status(value: object | None) -> SiteStatus | None:
    """Convert an optional JSON status string."""
    if value is None:
        return None
    return SiteStatus(str(value))
