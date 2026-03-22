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
