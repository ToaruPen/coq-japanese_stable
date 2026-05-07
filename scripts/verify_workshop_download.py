"""Verify a downloaded Steam Workshop item against the staged release DLL."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence


def _sha256(path: Path) -> str:
    """Return the SHA256 hex digest for a file."""
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_bytes(payload: bytes) -> str:
    """Return the SHA256 hex digest for bytes."""
    return hashlib.sha256(payload).hexdigest()


def _read_expected_dll(path: Path) -> bytes:
    """Read an expected DLL from a DLL file or QudJP release ZIP."""
    if path.suffix == ".zip":
        with zipfile.ZipFile(path) as archive:
            return archive.read("QudJP/Assemblies/QudJP.dll")
    return path.read_bytes()


def _read_manifest_version(manifest_path: Path) -> str | None:
    """Read the Version field from a Workshop manifest."""
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    version = data.get("Version")
    return version if isinstance(version, str) else None


def verify_workshop_download(
    workshop_dir: Path,
    *,
    expected_version: str,
    expected_dll: Path,
) -> list[str]:
    """Verify a downloaded Workshop item folder.

    Args:
        workshop_dir: Downloaded Workshop item directory.
        expected_version: Expected ``manifest.json`` Version value.
        expected_dll: DLL path, or release ZIP path, that the downloaded
            Workshop DLL must match.

    Returns:
        A list of validation findings. An empty list means the download matches.

    Raises:
        ValueError: If ``expected_version`` is not simple ``X.Y.Z`` semver.
    """
    if re.fullmatch(r"\d+\.\d+\.\d+", expected_version) is None:
        msg = f"expected_version must be simple semver X.Y.Z: {expected_version!r}"
        raise ValueError(msg)

    findings: list[str] = []
    manifest_path = workshop_dir / "manifest.json"
    downloaded_dll = workshop_dir / "Assemblies" / "QudJP.dll"

    if not manifest_path.is_file():
        findings.append("Workshop manifest not found")
        return findings

    try:
        actual_version = _read_manifest_version(manifest_path)
    except json.JSONDecodeError as exc:
        findings.append(f"Workshop manifest is not valid JSON: {exc.msg}")
        actual_version = None

    if actual_version != expected_version:
        findings.append(f"manifest version mismatch: expected {expected_version}, got {actual_version or '<missing>'}")

    if not expected_dll.is_file():
        findings.append(f"expected DLL not found: {expected_dll}")
        return findings

    if not downloaded_dll.is_file():
        findings.append("Workshop DLL not found: Assemblies/QudJP.dll")
        return findings

    expected_payload = _read_expected_dll(expected_dll)
    if _sha256(downloaded_dll) != _sha256_bytes(expected_payload):
        findings.append("DLL SHA256 mismatch: downloaded QudJP.dll does not match expected DLL")

    return findings


def _build_parser() -> argparse.ArgumentParser:
    """Build the command line parser."""
    parser = argparse.ArgumentParser(description="Verify a downloaded QudJP Steam Workshop item.")
    parser.add_argument("--workshop-dir", type=Path, required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-dll", type=Path, required=True, help="Expected QudJP.dll or QudJP release ZIP.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Run the Workshop download verifier."""
    parser = _build_parser()
    args = parser.parse_args(argv)

    try:
        findings = verify_workshop_download(
            args.workshop_dir,
            expected_version=args.expected_version,
            expected_dll=args.expected_dll,
        )
    except ValueError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    if findings:
        for finding in findings:
            print(f"Error: {finding}", file=sys.stderr)  # noqa: T201
        return 1

    downloaded_dll = args.workshop_dir / "Assemblies" / "QudJP.dll"
    print(  # noqa: T201
        "Workshop download verified: "
        f"version={args.expected_version} "
        f"dll_sha256={_sha256(downloaded_dll)}",
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
