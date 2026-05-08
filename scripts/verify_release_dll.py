"""Verify QudJP release DLL runtime markers and dev-only marker absence."""

from __future__ import annotations

import argparse
import sys
import zipfile
from pathlib import Path

_REQUIRED_DLL_MARKERS = (
    b"Unity.TextMeshPro",
    b"TextMeshProUguiFontPatch",
    b"TmpInputFieldFontPatch",
    b"InventoryLineFontFixer",
    b"DelayedInventoryLineRepairScheduler",
    b"ShouldPreserveActiveReplacementForTests",
)

_FORBIDDEN_RELEASE_DLL_MARKERS = (
    b"BaseLineWithTooltipStartTooltipPatch",
    b"SelectableTextMenuItemProbePatch",
)


def _read_dll(path: Path) -> bytes:
    if path.suffix.lower() == ".zip":
        with zipfile.ZipFile(path) as archive:
            try:
                return archive.read("QudJP/Assemblies/QudJP.dll")
            except KeyError as exc:
                msg = f"{path}: missing QudJP/Assemblies/QudJP.dll"
                raise FileNotFoundError(msg) from exc

    return path.read_bytes()


def verify_release_dll(path: Path) -> list[str]:
    """Return release DLL marker validation findings for a DLL or release ZIP."""
    data = _read_dll(path)
    missing_markers = [
        marker.decode("ascii")
        for marker in _REQUIRED_DLL_MARKERS
        if marker not in data
    ]
    forbidden_markers = [
        "forbidden dev marker: " + marker.decode("ascii")
        for marker in _FORBIDDEN_RELEASE_DLL_MARKERS
        if marker in data
    ]
    return missing_markers + forbidden_markers


def main(argv: list[str] | None = None) -> int:
    """Run the release DLL marker verifier CLI."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path, help="QudJP.dll or QudJP release ZIP")
    args = parser.parse_args(argv)

    findings = verify_release_dll(args.path)
    if findings:
        print(  # noqa: T201
            f"{args.path}: release DLL marker validation failed: "
            + ", ".join(findings),
            file=sys.stderr,
        )
        return 1

    print(f"{args.path}: required release DLL markers present")  # noqa: T201
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
