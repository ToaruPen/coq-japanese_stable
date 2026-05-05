"""Python wrapper for the Roslyn static producer inventory scanner."""
# ruff: noqa: S603 -- invokes the repo-local dotnet scanner with explicit arguments

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from functools import cache
from pathlib import Path
from typing import Final, NotRequired, TypedDict, cast

SCHEMA_VERSION: Final = "1.0"
GAME_VERSION: Final = "2.0.4"
DEFAULT_SOURCE_ROOT: Final = Path("~/dev/coq-decompiled_stable").expanduser()
TARGET_SURFACES: Final = ["EmitMessage", "Popup.Show*", "AddPlayerMessage"]

REPO_ROOT: Final = Path(__file__).resolve().parents[1]
PROJECT_PATH: Final = (
    REPO_ROOT
    / "scripts"
    / "tools"
    / "StaticProducerInventoryScanner"
    / "StaticProducerInventoryScanner.csproj"
)


class TextArgumentPayload(TypedDict):
    """Serialized text argument inventory record."""

    role: str
    formal_index: int
    expression: str
    expression_kind: str
    closure_status: str


class CallsitePayload(TypedDict):
    """Serialized callsite inventory record."""

    file: str
    line: int
    target_surface: str
    receiver: str | None
    method: str
    expression: str
    namespace: str | None
    type_name: str
    member_name: str
    member_kind: str
    member_start_line: int
    producer_family_id: str
    text_arguments: list[TextArgumentPayload]
    closure_status: str
    closure_reason: NotRequired[str]
    argument_count: NotRequired[int]
    argument_names: NotRequired[list[str]]
    callee_expression: NotRequired[str]
    roslyn_symbol_status: NotRequired[str]
    method_symbol: NotRequired[str]
    containing_type_symbol: NotRequired[str]
    receiver_type_symbol: NotRequired[str]


class RepresentativeCallPayload(TypedDict):
    """Small callsite sample embedded in family records."""

    file: str
    line: int
    target_surface: str
    method: str
    closure_status: str
    expression: str


class FamilyPayload(TypedDict):
    """Serialized producer family inventory record."""

    producer_family_id: str
    file: str
    namespace: str | None
    type_name: str
    member_name: str
    member_kind: str
    member_start_line: int
    callsite_count: int
    text_argument_count: int
    family_closure_status: str
    closure_status_counts: dict[str, int]
    surface_counts: dict[str, int]
    representative_calls: list[RepresentativeCallPayload]


class TotalsPayload(TypedDict):
    """Aggregated inventory totals."""

    callsites: int
    families: int
    text_arguments: int
    callsite_statuses: dict[str, int]
    callsite_only_statuses: dict[str, int]
    text_argument_statuses: dict[str, int]
    text_argument_classifications: dict[str, int]
    family_statuses: dict[str, int]


class InventoryPayload(TypedDict):
    """Top-level static producer inventory payload."""

    schema_version: str
    game_version: str
    target_surfaces: list[str]
    totals: TotalsPayload
    callsites: list[CallsitePayload]
    families: list[FamilyPayload]


def scan_source_root(source_root: Path) -> InventoryPayload:
    """Scan decompiled C# sources for static producer inventory callsites."""
    expanded_source_root = _resolve_source_root(source_root)
    return _scan_source_root_cached(str(expanded_source_root), _scanner_cache_fingerprint(expanded_source_root))


def write_inventory(source_root: Path, output_path: Path) -> None:
    """Write the static producer inventory JSON."""
    _ = _run_roslyn_scanner(_resolve_source_root(source_root), output_path)


def main(argv: list[str] | None = None) -> int:
    """Run the static producer inventory scanner CLI."""
    parser = argparse.ArgumentParser(description="Scan decompiled C# for static text producer callsites.")
    _ = parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT)
    _ = parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)

    source_root = cast("Path", args.source_root).expanduser()
    output_path = cast("Path", args.output)
    if not source_root.is_dir():
        _ = sys.stderr.write(f"source root does not exist or is not a directory: {source_root}\n")
        return 1

    try:
        write_inventory(source_root, output_path)
    except RuntimeError as exc:
        _ = sys.stderr.write(f"{exc}\n")
        return 1
    return 0


def _resolve_source_root(source_root: Path) -> Path:
    expanded_source_root = source_root.expanduser().resolve()
    if not expanded_source_root.is_dir():
        msg = f"source root does not exist or is not a directory: {expanded_source_root}"
        raise FileNotFoundError(msg)
    return expanded_source_root


@cache
def _scan_source_root_cached(source_root: str, fingerprint: tuple[tuple[str, int, int], ...]) -> InventoryPayload:
    _ = fingerprint
    with tempfile.TemporaryDirectory(prefix="qudjp-static-producer-") as tmp_dir:
        output_path = Path(tmp_dir) / "inventory.json"
        return _run_roslyn_scanner(Path(source_root), output_path)


def _scanner_cache_fingerprint(source_root: Path) -> tuple[tuple[str, int, int], ...]:
    tracked_paths = [
        PROJECT_PATH,
        PROJECT_PATH.with_name("Program.cs"),
        *sorted(path for path in source_root.rglob("*.cs") if path.is_file()),
    ]
    fingerprint: list[tuple[str, int, int]] = []
    for path in tracked_paths:
        stat = path.stat()
        fingerprint.append((path.as_posix(), stat.st_mtime_ns, stat.st_size))
    return tuple(fingerprint)


def _run_roslyn_scanner(source_root: Path, output_path: Path) -> InventoryPayload:
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        msg = "dotnet 10.0.x SDK required to run the Roslyn static producer scanner"
        raise RuntimeError(msg)
    if not PROJECT_PATH.is_file():
        msg = f"Roslyn static producer scanner project is missing: {PROJECT_PATH}"
        raise RuntimeError(msg)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(
        [
            dotnet,
            "run",
            "--project",
            str(PROJECT_PATH),
            "--",
            "--source-root",
            str(source_root),
            "--output",
            str(output_path),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        details = "\n".join(part for part in (result.stdout.strip(), result.stderr.strip()) if part)
        msg = f"Roslyn static producer scanner failed with exit {result.returncode}"
        if details:
            msg = f"{msg}\n{details}"
        raise RuntimeError(msg)
    return _load_inventory(output_path)


def _load_inventory(path: Path) -> InventoryPayload:
    try:
        payload = cast("InventoryPayload", json.loads(path.read_text(encoding="utf-8")))
    except (OSError, json.JSONDecodeError) as exc:
        msg = f"Roslyn static producer scanner produced unreadable JSON: {exc}"
        raise RuntimeError(msg) from exc
    if payload.get("schema_version") != SCHEMA_VERSION:
        msg = f"unexpected static producer inventory schema_version: {payload.get('schema_version')!r}"
        raise RuntimeError(msg)
    if payload.get("game_version") != GAME_VERSION:
        msg = f"unexpected static producer inventory game_version: {payload.get('game_version')!r}"
        raise RuntimeError(msg)
    if payload.get("target_surfaces") != TARGET_SURFACES:
        msg = f"unexpected static producer inventory target_surfaces: {payload.get('target_surfaces')!r}"
        raise RuntimeError(msg)
    return payload


if __name__ == "__main__":
    raise SystemExit(main())
