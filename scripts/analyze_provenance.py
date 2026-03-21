"""Analyze string provenance across dictionaries and decompiled source."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main(argv: list[str] | None = None) -> int:
    """Run the provenance-analysis CLI.

    Args:
        argv: Optional command-line arguments.

    Returns:
        Exit code where ``0`` means success and ``1`` means failure.
    """
    _configure_import_path()

    from scripts.provenance.cross_reference import cross_reference  # noqa: PLC0415
    from scripts.provenance.csharp_pattern_scanner import scan_directory  # noqa: PLC0415
    from scripts.provenance.dictionary_auditor import audit_dictionaries, load_dictionary_entries  # noqa: PLC0415
    from scripts.provenance.report import generate_report  # noqa: PLC0415

    parser = argparse.ArgumentParser(
        description="Analyze translation dictionary entries for string provenance risks.",
    )
    project_root = _find_project_root()
    parser.add_argument(
        "--dictionaries",
        type=Path,
        default=project_root / "Mods" / "QudJP" / "Localization" / "Dictionaries",
        help="Directory containing translation dictionaries.",
    )
    parser.add_argument(
        "--ilspy-raw",
        type=Path,
        default=project_root / "docs" / "ilspy-raw",
        help="Directory containing decompiled C# source files.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Optional file path for the generated JSON report.",
    )
    args = parser.parse_args(argv)

    try:
        audit_findings = audit_dictionaries(args.dictionaries)
        generator_signatures = scan_directory(args.ilspy_raw) if args.ilspy_raw.exists() else []
        xref_findings = cross_reference(load_dictionary_entries(args.dictionaries), generator_signatures)
        report = generate_report(
            audit_findings=audit_findings,
            generator_signatures=generator_signatures,
            xref_findings=xref_findings,
        )
    except (FileNotFoundError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    if args.output is None:
        print(report)  # noqa: T201
        return 0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(report, encoding="utf-8")
    print(f"Report written to {args.output}", file=sys.stderr)  # noqa: T201
    return 0


def _find_project_root() -> Path:
    """Find the repository root that contains ``pyproject.toml``."""
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "pyproject.toml").exists():
            return current
        current = current.parent
    msg = "Could not locate project root containing pyproject.toml"
    raise FileNotFoundError(msg)


def _configure_import_path() -> None:
    """Ensure direct script execution can import the repo-root ``scripts`` package."""
    if __package__ not in {None, ""}:
        return
    project_root = Path(__file__).resolve().parents[1]
    project_root_str = str(project_root)
    if project_root_str not in sys.path:
        sys.path.insert(0, project_root_str)


if __name__ == "__main__":
    raise SystemExit(main())
