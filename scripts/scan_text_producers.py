"""Main CLI orchestrator for the source-first scanner pipeline."""

from __future__ import annotations

import argparse
import subprocess
import sys
from collections import Counter
from pathlib import Path

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[1]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts.scanner.ast_grep_runner import Phase1aScanResult, scan_source_tree
from scripts.scanner.cross_reference import cross_reference_inventory_file
from scripts.scanner.inventory import (
    InventoryDraft,
    RawHit,
    SiteStatus,
    read_raw_hits_jsonl,
    write_inventory_draft_json,
)
from scripts.scanner.rule_classifier import classify_raw_hits

DEFAULT_SOURCE_ROOT = Path("~/dev/coq-decompiled_stable")
DEFAULT_CACHE_DIR = Path(".scanner-cache")
DEFAULT_OUTPUT_PATH = Path("docs/candidate-inventory.json")
DEFAULT_INVENTORY_DRAFT_PATH = Path("inventory_draft.json")
PHASE_CHOICES = ("1a", "1b", "1c", "1d", "all")
REPO_ROOT = Path(__file__).resolve().parents[1]


def run_phase_1a(source_root: Path, cache_dir: Path) -> Phase1aScanResult:
    """Run Phase 1a and persist raw hit caches."""
    return scan_source_tree(source_root, cache_dir=cache_dir)


def run_phase_1b(source_root: Path, cache_dir: Path) -> InventoryDraft:
    """Run Phase 1b from cached Phase 1a outputs and persist the inventory draft."""
    draft = classify_raw_hits(_read_phase_1a_hits(cache_dir), source_root)
    write_inventory_draft_json(_inventory_draft_path(cache_dir), draft)
    return draft


def run_phase_1d(source_root: Path, cache_dir: Path, output_path: Path) -> InventoryDraft:
    """Run Phase 1d from the cached inventory draft and persist the candidate inventory."""
    return cross_reference_inventory_file(
        _inventory_draft_path(cache_dir),
        REPO_ROOT,
        source_root=source_root,
        output_path=output_path,
    )


def _read_phase_1a_hits(cache_dir: Path) -> list[RawHit]:
    """Load sink and override hits produced by Phase 1a."""
    raw_hits_path = cache_dir / "raw_hits.jsonl"
    override_hits_path = cache_dir / "override_hits.jsonl"
    return read_raw_hits_jsonl(_require_path(raw_hits_path, "1a")) + read_raw_hits_jsonl(
        _require_path(override_hits_path, "1a")
    )


def _inventory_draft_path(cache_dir: Path) -> Path:
    """Return the standard cache path for the Phase 1b draft."""
    return cache_dir / DEFAULT_INVENTORY_DRAFT_PATH


def _require_path(path: Path, producing_phase: str) -> Path:
    """Require a cache path and raise an actionable error when it is missing."""
    if path.exists():
        return path
    msg = f"Required file not found: {path} (run --phase {producing_phase} first)"
    raise FileNotFoundError(msg)


def _format_type_counts(draft: InventoryDraft) -> str:
    """Format deterministic per-type counts for summary output."""
    counts = Counter(site.type.value for site in draft.sites)
    return ", ".join(f"{site_type}={counts[site_type]}" for site_type in sorted(counts))


def _translated_count(draft: InventoryDraft) -> int:
    """Count translated sites in a Phase 1d candidate inventory."""
    return sum(site.status is SiteStatus.TRANSLATED for site in draft.sites)


def _write_phase_1a_summary(result: Phase1aScanResult) -> None:
    """Print a concise Phase 1a summary."""
    sys.stdout.write(
        "Phase 1a complete: "
        f"{result.source_inventory.included_file_count} unique files, "
        f"{len(result.raw_hits)} sink hits, "
        f"{len(result.override_hits)} override hits.\n"
    )


def _write_inventory_summary(phase: str, draft: InventoryDraft) -> None:
    """Print a concise summary for a classified or cross-referenced inventory."""
    sys.stdout.write(
        f"Phase {phase} complete: "
        f"total sites: {len(draft.sites)}; "
        f"types: {_format_type_counts(draft)}; "
        f"translated: {_translated_count(draft)}\n"
    )


def _write_phase_1c_placeholder() -> None:
    """Print the placeholder message for the interactive Phase 1c step."""
    sys.stdout.write("Phase 1c is interactive and not implemented in this orchestrator yet.\n")


def _execute_phase(phase: str, source_root: Path, cache_dir: Path, output_path: Path) -> None:
    """Execute one requested phase, or the full non-interactive pipeline."""
    if phase == "1a":
        _write_phase_1a_summary(run_phase_1a(source_root, cache_dir))
    elif phase == "1b":
        _write_inventory_summary("1b", run_phase_1b(source_root, cache_dir))
    elif phase == "1c":
        _write_phase_1c_placeholder()
    elif phase == "1d":
        _write_inventory_summary("1d", run_phase_1d(source_root, cache_dir, output_path))
    else:
        phase_1a = run_phase_1a(source_root, cache_dir)
        _write_phase_1a_summary(phase_1a)
        _write_inventory_summary("1b", run_phase_1b(source_root, cache_dir))
        _write_phase_1c_placeholder()
        _write_inventory_summary("1d", run_phase_1d(source_root, cache_dir, output_path))


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments for the scanner orchestrator."""
    parser = argparse.ArgumentParser(description="Run the source-first scanner pipeline.")
    parser.add_argument(
        "--source-root",
        default=str(DEFAULT_SOURCE_ROOT),
        help="Path to the decompiled C# source root.",
    )
    parser.add_argument(
        "--cache-dir",
        default=str(DEFAULT_CACHE_DIR),
        help="Directory for intermediate Phase 1a/1b outputs.",
    )
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT_PATH),
        help="Path to write the final candidate inventory JSON.",
    )
    parser.add_argument(
        "--phase",
        choices=PHASE_CHOICES,
        default="all",
        help="Pipeline phase to run: 1a, 1b, 1c, 1d, or all.",
    )
    parser.add_argument(
        "--diff",
        action="store_true",
        help="Reserved for future freshness-management diffs; currently a no-op.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Execute the requested scanner phase or the full 1a → 1b → 1d pipeline."""
    args = _parse_args(argv)
    source_root = Path(args.source_root).expanduser().resolve()
    cache_dir = Path(args.cache_dir).expanduser().resolve()
    output_path = Path(args.output).expanduser().resolve()

    if args.diff:
        sys.stdout.write("Note: --diff is reserved for future freshness management and is not implemented yet.\n")

    try:
        _execute_phase(args.phase, source_root, cache_dir, output_path)
    except FileNotFoundError as exc:
        sys.stderr.write(f"Error: {exc}\n")
        return 1
    except subprocess.CalledProcessError as exc:
        sys.stderr.write(f"Error: scanner command failed: {' '.join(str(part) for part in exc.cmd)}\n")
        if exc.stderr:
            sys.stderr.write(f"{exc.stderr.rstrip()}\n")
        return exc.returncode or 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
