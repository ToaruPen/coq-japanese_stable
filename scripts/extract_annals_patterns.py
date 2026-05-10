"""Run AnnalsPatternExtractor against decompiled XRL.Annals/*.cs sources."""
# ruff: noqa: D103, DTZ005, PLR0911, T201

from __future__ import annotations

import argparse
import datetime as dt
import json
import shutil
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.dotnet_tool_runner import DotnetToolError, run_tool_project  # noqa: E402

PROJECT_PATH = REPO_ROOT / "scripts" / "tools" / "AnnalsPatternExtractor" / "AnnalsPatternExtractor.csproj"
EXTRACTOR_TIMEOUT_SECONDS = 600


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract Annals candidate patterns via Roslyn AST.",
    )
    parser.add_argument("--source-root", required=True, type=Path, help="Decompiled XRL.Annals directory")
    parser.add_argument("--include", required=True, help="Glob filter, e.g. 'Resheph*.cs'")
    parser.add_argument("--output", required=True, type=Path, help="Path where candidate JSON will be written")
    parser.add_argument(
        "--force", action="store_true", help="Overwrite existing output (creates a .bak-YYYYMMDDHHMMSS first)"
    )
    return parser.parse_args(argv)


def backup_existing(path: Path) -> Path:
    timestamp = dt.datetime.now().strftime("%Y%m%d%H%M%S")
    backup = path.with_suffix(path.suffix + f".bak-{timestamp}")
    shutil.copy2(path, backup)
    return backup


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)

    if not args.source_root.is_dir():
        print(f"error: --source-root does not exist: {args.source_root}", file=sys.stderr)
        return 1

    if args.output.exists():
        if not args.force:
            print(
                f"error: {args.output} already exists. "
                f"Re-run with --force to overwrite (a .bak- copy will be made first).",
                file=sys.stderr,
            )
            return 1
        backup = backup_existing(args.output)
        print(f"[extract] backed up existing output to {backup}")

    args.output.parent.mkdir(parents=True, exist_ok=True)

    tool_args = [
        "--source-root",
        str(args.source_root),
        "--include",
        args.include,
        "--output",
        str(args.output),
    ]
    print("[extract] building and running AnnalsPatternExtractor via isolated dotnet artifacts")
    try:
        result = run_tool_project(PROJECT_PATH, tool_args, timeout=EXTRACTOR_TIMEOUT_SECONDS)
    except DotnetToolError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    if result.stdout:
        print(result.stdout, end="")
    if result.stderr:
        print(result.stderr, end="", file=sys.stderr)
    if result.returncode != 0:
        print(f"error: extractor exited with {result.returncode}", file=sys.stderr)
        return 1

    # Validate basic JSON shape before declaring success
    try:
        doc = json.loads(args.output.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: produced output is not valid JSON: {exc}", file=sys.stderr)
        return 1
    if doc.get("schema_version") != "1" or "candidates" not in doc:
        print(f"error: produced output has unexpected schema: {doc.keys()}", file=sys.stderr)
        return 1

    n = len(doc["candidates"])
    accepted = sum(1 for c in doc["candidates"] if c["status"] == "pending")
    needs = sum(1 for c in doc["candidates"] if c["status"] == "needs_manual")
    print(f"[extract] OK — {n} candidate(s): {accepted} pending, {needs} needs_manual")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
