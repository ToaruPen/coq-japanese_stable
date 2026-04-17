"""Package CLI for directory-based triage classification."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import TYPE_CHECKING, Any

from scripts.triage.classifier import classify
from scripts.triage.log_parser import parse_log
from scripts.triage_untranslated import _build_report, _iter_actionable_entries, _iter_phase_f_entries

if TYPE_CHECKING:
    from scripts.triage.models import LogEntry


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse package CLI arguments."""
    parser = argparse.ArgumentParser(description="Run triage helpers from the package entry point.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    classify_parser = subparsers.add_parser(
        "classify",
        help="Classify Player.log files found under an evidence directory.",
    )
    classify_parser.add_argument(
        "--input-dir",
        type=Path,
        required=True,
        help="Directory to scan recursively for *.log files. Missing directories yield an empty report.",
    )
    classify_parser.add_argument(
        "--output",
        type=Path,
        required=True,
        help="Path to write the triage JSON report.",
    )
    return parser.parse_args(argv)


def _collect_entries(input_dir: Path) -> list[LogEntry]:
    """Collect parsed log entries from ``input_dir`` recursively."""
    if not input_dir.exists():
        return []
    if not input_dir.is_dir():
        msg = f"Input dir is not a directory: {input_dir}"
        raise ValueError(msg)

    entries: list[LogEntry] = []
    for log_path in sorted(input_dir.rglob("*.log")):
        entries.extend(parse_log(log_path))
    return entries


def _build_directory_report(input_dir: Path) -> dict[str, Any]:
    """Build a triage report from every log found under ``input_dir``."""
    entries = _collect_entries(input_dir)
    actionable_results = [classify(entry) for entry in _iter_actionable_entries(entries)]
    phase_f_results = [classify(entry) for entry in _iter_phase_f_entries(entries)]
    return _build_report(actionable_results, phase_f_results)


def _dispatch_command(args: argparse.Namespace) -> dict[str, Any]:
    """Execute the requested subcommand and return its report payload."""
    if args.command != "classify":
        msg = f"Unsupported command: {args.command}"
        raise ValueError(msg)

    return _build_directory_report(args.input_dir)


def main(argv: list[str] | None = None) -> int:
    """Execute the triage package CLI."""
    args = _parse_args(argv)

    try:
        report = _dispatch_command(args)
        report_json = json.dumps(report, ensure_ascii=False, indent=2)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(report_json, encoding="utf-8")
    except (ValueError, OSError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    print(f"Triage report written to {args.output}", file=sys.stderr)  # noqa: T201
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
