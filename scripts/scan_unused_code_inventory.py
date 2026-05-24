"""Python wrapper for the Roslyn unused-code inventory scanner."""

from __future__ import annotations

import argparse
import json
import shlex
import sys
from pathlib import Path
from typing import Final, TypedDict, cast

REPO_ROOT: Final = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.dotnet_tool_runner import DotnetToolError, run_cached_tool_project  # noqa: E402

SCHEMA_VERSION: Final = "1.0"
GAME_VERSION: Final = "1.0.4"
DEFAULT_SOURCE_ROOT: Final = REPO_ROOT
DEFAULT_CONFIG_PATH: Final = REPO_ROOT / "scripts" / "unused_code_inventory_config.json"
ROSLYN_SCANNER_TIMEOUT_SECONDS: Final = 600
PROJECT_PATH: Final = (
    REPO_ROOT
    / "scripts"
    / "tools"
    / "UnusedCodeInventoryScanner"
    / "UnusedCodeInventoryScanner.csproj"
)


class DeclarationPayload(TypedDict):
    """Serialized declaration inventory record."""

    symbol_id: str
    file: str
    line: int
    kind: str
    accessibility: str
    status: str
    reason: str


class TotalsPayload(TypedDict):
    """Aggregated unused-code inventory totals."""

    files_scanned: int
    reportable_declarations: int
    candidates: int
    used_declarations: int
    rooted_declarations: int
    excluded_declarations: int
    candidate_kinds: dict[str, int]
    candidate_accessibilities: dict[str, int]


class GenerationPayload(TypedDict):
    """Scanner generation metadata."""

    tool: str
    parser: str
    includes_raw_source_text: bool
    parse_error_file_count: int
    parse_error_files: list[str]
    metadata_references: MetadataReferencePayload


class MetadataReferencePayload(TypedDict):
    """Metadata references used for Roslyn semantic resolution."""

    trusted_platform_assembly_count: int
    external_references: list[str]
    missing_external_references: list[str]


class InventoryPayload(TypedDict):
    """Top-level unused-code inventory payload."""

    schema_version: str
    game_version: str
    config_schema_version: str
    generation: GenerationPayload
    totals: TotalsPayload
    candidates: list[DeclarationPayload]
    rooted_declarations: list[DeclarationPayload]


def write_inventory(
    source_root: Path,
    config_path: Path,
    output_path: Path,
    *,
    fail_on_candidates: bool = False,
    references: list[Path] | None = None,
    managed_dir: Path | None = None,
) -> InventoryPayload:
    """Write the unused-code inventory JSON."""
    return _run_roslyn_scanner(
        _resolve_source_root(source_root),
        _resolve_config_path(config_path),
        output_path,
        fail_on_candidates=fail_on_candidates,
        references=references or [],
        managed_dir=managed_dir,
    )


def main(argv: list[str] | None = None) -> int:
    """Run the unused-code inventory scanner CLI."""
    parser = argparse.ArgumentParser(description="Scan QudJP C# for unused private/internal declarations.")
    _ = parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT)
    _ = parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG_PATH)
    _ = parser.add_argument("--output", type=Path, required=True)
    _ = parser.add_argument("--reference", type=Path, action="append", default=[])
    _ = parser.add_argument("--managed-dir", type=Path)
    _ = parser.add_argument("--fail-on-candidates", action="store_true")
    args = parser.parse_args(argv)

    source_root = cast("Path", args.source_root).expanduser()
    config_path = cast("Path", args.config).expanduser()
    output_path = cast("Path", args.output)
    references = [path.expanduser() for path in cast("list[Path]", args.reference)]
    managed_dir = cast("Path | None", args.managed_dir)
    if managed_dir is not None:
        managed_dir = managed_dir.expanduser()
    fail_on_candidates = cast("bool", args.fail_on_candidates)

    try:
        _ = write_inventory(
            source_root,
            config_path,
            output_path,
            fail_on_candidates=fail_on_candidates,
            references=references,
            managed_dir=managed_dir,
        )
    except (FileNotFoundError, RuntimeError) as exc:
        _ = sys.stderr.write(f"{exc}\n")
        return 1
    return 0


def _resolve_source_root(source_root: Path) -> Path:
    expanded_source_root = source_root.expanduser().resolve()
    if not expanded_source_root.is_dir():
        msg = f"source root does not exist or is not a directory: {expanded_source_root}"
        raise FileNotFoundError(msg)
    return expanded_source_root


def _resolve_config_path(config_path: Path) -> Path:
    expanded_config_path = config_path.expanduser().resolve()
    if not expanded_config_path.is_file():
        msg = f"unused-code scanner config does not exist: {expanded_config_path}"
        raise FileNotFoundError(msg)
    return expanded_config_path


def _run_roslyn_scanner(
    source_root: Path,
    config_path: Path,
    output_path: Path,
    *,
    fail_on_candidates: bool,
    references: list[Path],
    managed_dir: Path | None,
) -> InventoryPayload:
    normalized_output_path = output_path.expanduser().resolve()
    if not PROJECT_PATH.is_file():
        msg = f"Roslyn unused-code scanner project is missing: {PROJECT_PATH}"
        raise RuntimeError(msg)

    normalized_output_path.parent.mkdir(parents=True, exist_ok=True)
    tool_args = [
        "--source-root",
        str(source_root),
        "--config",
        str(config_path),
        "--output",
        str(normalized_output_path),
    ]
    for reference in references:
        tool_args.extend(["--reference", str(reference.expanduser())])
    if managed_dir is not None:
        tool_args.extend(["--managed-dir", str(managed_dir.expanduser())])
    if fail_on_candidates:
        tool_args.append("--fail-on-candidates")
    try:
        result = run_cached_tool_project(PROJECT_PATH, tool_args, timeout=ROSLYN_SCANNER_TIMEOUT_SECONDS)
    except DotnetToolError as exc:
        raise RuntimeError(str(exc)) from exc
    if result.returncode != 0:
        details = "\n".join(part for part in (result.stdout.strip(), result.stderr.strip()) if part)
        msg = f"Roslyn unused-code scanner failed with exit {result.returncode}: {shlex.join(tool_args)}"
        if details:
            msg = f"{msg}\n{details}"
        raise RuntimeError(msg)
    return _load_inventory(normalized_output_path)


def _load_inventory(path: Path) -> InventoryPayload:
    try:
        payload = cast("InventoryPayload", json.loads(path.read_text(encoding="utf-8")))
    except (OSError, json.JSONDecodeError) as exc:
        msg = f"Roslyn unused-code scanner produced unreadable JSON: {exc}"
        raise RuntimeError(msg) from exc
    if payload.get("schema_version") != SCHEMA_VERSION:
        msg = f"unexpected unused-code inventory schema_version: {payload.get('schema_version')!r}"
        raise RuntimeError(msg)
    if payload.get("game_version") != GAME_VERSION:
        msg = f"unexpected unused-code inventory game_version: {payload.get('game_version')!r}"
        raise RuntimeError(msg)
    return payload


if __name__ == "__main__":
    raise SystemExit(main())
