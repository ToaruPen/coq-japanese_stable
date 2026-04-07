"""Phase 1a scanner: ast-grep sink scan plus override producer grep."""

from __future__ import annotations

import argparse
import json
import logging
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from scripts.scanner.inventory import (
    ExclusionReason,
    FileRecord,
    HitKind,
    RawHit,
    SourceFileInventory,
    write_raw_hits_jsonl,
)

logger = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class SinkFamilySpec:
    """Configuration for one sink family scanned with ast-grep."""

    family: str
    patterns: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class OverrideProducerSpec:
    """Configuration for one override producer family scanned via regex."""

    family: str
    root_segment: str
    regex: re.Pattern[str]


@dataclass(frozen=True, slots=True)
class Phase1aScanResult:
    """Result bundle for a completed Phase 1a scan."""

    source_inventory: SourceFileInventory
    raw_hits: list[RawHit]
    override_hits: list[RawHit]
    raw_hits_path: Path | None
    override_hits_path: Path | None


SINK_FAMILY_SPECS: tuple[SinkFamilySpec, ...] = (
    SinkFamilySpec("SetText", ("$_.SetText($$$)",)),
    SinkFamilySpec("AddPlayerMessage", ("$_.AddPlayerMessage($$$)", "MessageQueue.AddPlayerMessage($$$)")),
    SinkFamilySpec(
        "Popup",
        (
            "Popup.Show($$$)",
            "Popup.ShowFail($$$)",
            "Popup.ShowBlock($$$)",
            "Popup.ShowYesNo($$$)",
            "Popup.ShowYesNoCancel($$$)",
            "Popup.PickOption($$$)",
            "Popup.AskString($$$)",
            "Popup.ShowAsync($$$)",
            "Popup.WarnYesNo($$$)",
        ),
    ),
    SinkFamilySpec(
        "DidX",
        (
            "$_.DidX($$$)",
            "DidX($$$)",
            "$_.DidXToY($$$)",
            "DidXToY($$$)",
            "$_.DidXToYWithZ($$$)",
            "DidXToYWithZ($$$)",
            "Messaging.XDidY($$$)",
            "Messaging.XDidYToZ($$$)",
            "Messaging.WDidXToYWithZ($$$)",
        ),
    ),
    SinkFamilySpec("GetDisplayName", ("$_.GetDisplayName($$$)",)),
    SinkFamilySpec("Does", ("$_.Does($$$)",)),
    SinkFamilySpec("EmitMessage", ("$_.EmitMessage($$$)", "EmitMessage($$$)", "Messaging.EmitMessage($$$)")),
    SinkFamilySpec(
        "GetShort/LongDescription",
        ("$_.GetShortDescription($$$)", "$_.GetLongDescription($$$)"),
    ),
    SinkFamilySpec(
        "JournalAPI",
        (
            "JournalAPI.AddAccomplishment($$$)",
            "JournalAPI.AddMapNote($$$)",
            "JournalAPI.AddObservation($$$)",
        ),
    ),
    SinkFamilySpec("HistoricStringExpander", ("HistoricStringExpander.ExpandString($$$)",)),
    SinkFamilySpec("ReplaceBuilder", ("$_.StartReplace($$$)",)),
)

OVERRIDE_PRODUCER_SPECS: tuple[OverrideProducerSpec, ...] = (
    OverrideProducerSpec(
        "Effects.GetDescription",
        "XRL.World.Effects",
        re.compile(r"override\b.*\bGetDescription\s*\("),
    ),
    OverrideProducerSpec(
        "Effects.GetDetails",
        "XRL.World.Effects",
        re.compile(r"override\b.*\bGetDetails\s*\("),
    ),
    OverrideProducerSpec(
        "Mutations.GetDescription",
        "XRL.World.Parts.Mutation",
        re.compile(r"override\b.*\bGetDescription\s*\("),
    ),
    OverrideProducerSpec(
        "Mutations.GetLevelText",
        "XRL.World.Parts.Mutation",
        re.compile(r"override\b.*\bGetLevelText\s*\("),
    ),
    OverrideProducerSpec(
        "Parts.GetShortDescription",
        "XRL.World.Parts",
        re.compile(r"override\b.*GetShortDescription"),
    ),
)

_AST_GREP_BASE_COMMAND = (
    "ast-grep",
    "run",
    "--lang",
    "csharp",
    "--json=stream",
    "--globs",
    "*.cs",
    "--globs",
    "!*.retry.cs",
    "--globs",
    "!*.msgprobe.cs",
)


def collect_source_inventory(source_root: Path) -> SourceFileInventory:
    """Collect deduplicated `.cs` files for Phase 1a scanning."""
    resolved_root = source_root.expanduser().resolve()
    file_paths = sorted(resolved_root.rglob("*.cs"))
    directory_counterparts = _index_directory_counterparts(resolved_root, file_paths)
    records: list[FileRecord] = []

    for file_path in file_paths:
        relative_path = file_path.relative_to(resolved_root).as_posix()
        exclusion_reason = _artifact_exclusion_reason(relative_path)
        duplicate_of: str | None = None

        if file_path.stat().st_size == 0:
            exclusion_reason = ExclusionReason.EMPTY_FILE
        elif exclusion_reason is None and _is_flat_namespace_candidate(file_path.relative_to(resolved_root)):
            normalized_key = _normalized_identity(file_path.relative_to(resolved_root))
            duplicate_of = directory_counterparts.get(normalized_key)
            if duplicate_of is not None:
                exclusion_reason = ExclusionReason.FLAT_NAMESPACE_DUPLICATE

        if exclusion_reason is None:
            records.append(FileRecord(path=relative_path, included=True))
            continue

        records.append(
            FileRecord(
                path=relative_path,
                included=False,
                exclusion_reason=exclusion_reason,
                duplicate_of=duplicate_of,
            )
        )

    return SourceFileInventory(files=tuple(records))


def scan_source_tree(source_root: Path, *, cache_dir: Path | None = None) -> Phase1aScanResult:
    """Run the full Phase 1a scan for sink families and override producers."""
    resolved_root = source_root.expanduser().resolve()
    source_inventory = collect_source_inventory(resolved_root)
    raw_hits = _scan_sink_families(resolved_root, source_inventory)
    override_hits = _scan_override_producers(resolved_root, source_inventory)

    raw_hits_path: Path | None = None
    override_hits_path: Path | None = None
    if cache_dir is not None:
        resolved_cache_dir = cache_dir.expanduser().resolve()
        raw_hits_path = resolved_cache_dir / "raw_hits.jsonl"
        override_hits_path = resolved_cache_dir / "override_hits.jsonl"
        write_raw_hits_jsonl(raw_hits_path, raw_hits)
        write_raw_hits_jsonl(override_hits_path, override_hits)

    return Phase1aScanResult(
        source_inventory=source_inventory,
        raw_hits=raw_hits,
        override_hits=override_hits,
        raw_hits_path=raw_hits_path,
        override_hits_path=override_hits_path,
    )


def _scan_sink_families(source_root: Path, source_inventory: SourceFileInventory) -> list[RawHit]:
    """Run all configured ast-grep sink family patterns."""
    included_paths = source_inventory.included_path_set
    deduped_hits: dict[tuple[str, str, int, int, str], RawHit] = {}

    for spec in SINK_FAMILY_SPECS:
        for pattern in spec.patterns:
            for hit in _run_ast_grep_pattern(source_root, included_paths, spec.family, pattern):
                deduped_hits[(hit.family, hit.file, hit.line, hit.column, hit.matched_code)] = hit

    return sorted(deduped_hits.values(), key=_sort_key)


def _run_ast_grep_pattern(
    source_root: Path,
    included_paths: frozenset[str],
    family: str,
    pattern: str,
) -> list[RawHit]:
    """Run one ast-grep pattern and convert JSON output into RawHit rows."""
    command = [*_AST_GREP_BASE_COMMAND, "--pattern", pattern, str(source_root)]
    try:
        completed = subprocess.run(  # noqa: S603
            command,
            capture_output=True,
            check=False,
            text=True,
            timeout=120,
        )
    except subprocess.TimeoutExpired as exc:
        logger.exception("ast-grep timed out after 120s: %s", " ".join(command))
        raise subprocess.CalledProcessError(
            1,
            command,
            output="",
            stderr=f"Timed out: {exc}",
        ) from exc
    if completed.returncode not in {0, 1}:
        raise subprocess.CalledProcessError(
            completed.returncode,
            command,
            output=completed.stdout,
            stderr=completed.stderr,
        )
    if not completed.stdout.strip():
        return []

    hits: list[RawHit] = []
    for line in completed.stdout.splitlines():
        payload = json.loads(line)
        relative_path = _relative_match_path(source_root, Path(payload["file"]))
        if relative_path not in included_paths:
            continue
        hits.append(
            RawHit(
                hit_kind=HitKind.SINK,
                family=family,
                pattern=pattern,
                file=relative_path,
                line=int(payload["range"]["start"]["line"]) + 1,
                column=int(payload["range"]["start"]["column"]) + 1,
                matched_code=str(payload["text"]),
            )
        )

    return hits


def _relative_match_path(source_root: Path, matched_file: Path) -> str:
    """Return a source-root-relative path for ast-grep match output."""
    if matched_file.is_absolute():
        return matched_file.resolve().relative_to(source_root).as_posix()
    return matched_file.as_posix()


def _scan_override_producers(source_root: Path, source_inventory: SourceFileInventory) -> list[RawHit]:
    """Run regex-based override producer extraction on deduplicated source files."""
    hits: list[RawHit] = []

    for file_record in source_inventory.included_files:
        relative_path = Path(file_record.path)
        if not relative_path.parts:
            continue
        file_path = source_root / relative_path
        try:
            source_text = file_path.read_text(encoding="utf-8")
        except UnicodeDecodeError as exc:
            logger.warning("Encoding error in %s: %s — falling back to replace", file_path, exc)
            source_text = file_path.read_bytes().decode("utf-8", errors="replace")
        lines = source_text.splitlines()
        top_level_segment = relative_path.parts[0]

        for spec in OVERRIDE_PRODUCER_SPECS:
            if top_level_segment != spec.root_segment:
                continue
            for line_number, line in enumerate(lines, start=1):
                match = spec.regex.search(line)
                if match is None:
                    continue
                hits.append(
                    RawHit(
                        hit_kind=HitKind.OVERRIDE,
                        family=spec.family,
                        pattern=spec.regex.pattern,
                        file=relative_path.as_posix(),
                        line=line_number,
                        column=match.start() + 1,
                        matched_code=line.strip(),
                    )
                )

    return sorted(hits, key=_sort_key)


def _artifact_exclusion_reason(relative_path: str) -> ExclusionReason | None:
    """Map retry/msgprobe artifacts to their exclusion reason."""
    if relative_path.endswith(".retry.cs"):
        return ExclusionReason.RETRY_ARTIFACT
    if relative_path.endswith(".msgprobe.cs"):
        return ExclusionReason.MSGPROBE_ARTIFACT
    return None


def _index_directory_counterparts(source_root: Path, file_paths: list[Path]) -> dict[str, str]:
    """Index non-flat files so flat namespace duplicates can be removed."""
    counterparts: dict[str, str] = {}

    for file_path in file_paths:
        relative_path = file_path.relative_to(source_root)
        if relative_path.parent == Path():
            continue
        if file_path.stat().st_size == 0 or _artifact_exclusion_reason(relative_path.as_posix()) is not None:
            continue
        normalized_key = _normalized_identity(relative_path)
        counterparts.setdefault(normalized_key, relative_path.as_posix())

    return counterparts


def _is_flat_namespace_candidate(relative_path: Path) -> bool:
    """Return whether the file is a top-level flat namespace candidate."""
    return relative_path.parent == Path() and any(separator in relative_path.stem for separator in (".", "_"))


def _normalized_identity(relative_path: Path) -> str:
    """Normalize a path for flat-file duplicate comparison."""
    parts = [*relative_path.parts[:-1], relative_path.stem]
    return "".join(character for part in parts for character in part if character not in "._").lower()


def _sort_key(hit: RawHit) -> tuple[str, str, int, int, str, str]:
    """Return a stable ordering key for raw hits."""
    return (hit.family, hit.file, hit.line, hit.column, hit.pattern, hit.matched_code)


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments for Phase 1a scanning."""
    parser = argparse.ArgumentParser(description="Run Phase 1a source-first scanning.")
    parser.add_argument(
        "source_root",
        nargs="?",
        default="~/dev/coq-decompiled_stable",
        help="Path to decompiled C# source root.",
    )
    parser.add_argument(
        "--cache-dir",
        default=".scanner-cache",
        help="Directory for raw Phase 1a JSONL outputs.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Execute the Phase 1a scan from the command line."""
    args = _parse_args(argv)
    result = scan_source_tree(Path(args.source_root), cache_dir=Path(args.cache_dir))
    sys.stdout.write(
        "Phase 1a scan complete: "
        f"{result.source_inventory.included_file_count} unique files, "
        f"{len(result.raw_hits)} sink hits, "
        f"{len(result.override_hits)} override hits.\n"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
