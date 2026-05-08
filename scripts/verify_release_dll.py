"""Verify QudJP release DLL runtime markers and dev-only marker absence."""

from __future__ import annotations

import argparse
import re
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
    "BaseLineWithTooltipStartTooltipPatch",
    "SelectableTextMenuItemProbePatch",
    "[QudJP] SinkObserve/v1:",
    "[QudJP] Translator: missing key",
    "no pattern for",
)

_FORBIDDEN_VERBOSE_PROBE_MARKER_PATTERN = re.compile(
    r"\[QudJP\] [A-Za-z0-9]+Probe/v1:",
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
        "forbidden dev marker: " + marker
        for marker in _find_forbidden_release_markers(data)
    ]

    return missing_markers + forbidden_markers


def _find_forbidden_release_markers(data: bytes) -> list[str]:
    """Find forbidden marker text in ASCII test payloads and .NET UTF-16 metadata."""
    findings: list[str] = []
    seen: set[str] = set()
    for text in _iter_search_texts(data):
        for match in _FORBIDDEN_VERBOSE_PROBE_MARKER_PATTERN.finditer(text):
            _append_once(findings, seen, match.group(0))

        for marker in _FORBIDDEN_RELEASE_DLL_MARKERS:
            if marker in text:
                _append_once(findings, seen, marker)

    return findings


def _iter_search_texts(data: bytes) -> tuple[str, str, str]:
    return (
        data.decode("latin1", errors="ignore"),
        data.decode("utf-16le", errors="ignore"),
        data[1:].decode("utf-16le", errors="ignore"),
    )


def _append_once(findings: list[str], seen: set[str], marker: str) -> None:
    if marker in seen:
        return

    seen.add(marker)
    findings.append(marker)


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
