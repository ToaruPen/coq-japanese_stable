"""Build the standalone, verified Harmony 2.4.2 Windows patch archive."""

from __future__ import annotations

import argparse
import hashlib
import http.client
import sys
import tempfile
import urllib.error
import urllib.request
import zipfile
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence

PROJECT_ROOT = Path(__file__).resolve().parents[1]
HARMONY_ARCHIVE_URL = "https://github.com/pardeike/Harmony/releases/download/v2.4.2.0/Harmony-Fat.2.4.2.0.zip"
HARMONY_ARCHIVE_SHA256 = "a5fc5f9d9640b927d786a0527faa18bf7aa776788235140c59e9b73de87a7774"
HARMONY_NET48_DLL_SHA256 = "77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d"

ARCHIVE_ROOT = "QudJP-Harmony-2.4.2-Windows"
OUTPUT_NAME = f"{ARCHIVE_ROOT}.zip"
HARMONY_NET48_MEMBER = "net48/0Harmony.dll"
_FIXED_ZIP_TIMESTAMP = (1980, 1, 1, 0, 0, 0)
_TRACKED_ASSET_NAMES = (
    "Install Harmony 2.4.2.cmd",
    "Restore Game Harmony.cmd",
    "QudJP-Harmony-2.4.2.ps1",
    "README-ja.txt",
    "LICENSE-Harmony.txt",
    "THIRD-PARTY-NOTICES.txt",
)


class HarmonyPatchBuildError(RuntimeError):
    """Raised when a Harmony patch input cannot be verified or packaged safely."""


def sha256_file(path: Path) -> str:
    """Return the lowercase SHA-256 digest of a file."""
    digest = hashlib.sha256()
    try:
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        msg = f"cannot read file for SHA-256: {path}: {exc}"
        raise HarmonyPatchBuildError(msg) from exc
    return digest.hexdigest()


def verify_sha256(path: Path, expected_sha256: str, description: str) -> str:
    """Verify a file against a pinned SHA-256 digest and return its digest."""
    actual_sha256 = sha256_file(path)
    if actual_sha256 != expected_sha256.lower():
        msg = f"{description} SHA-256 mismatch: expected {expected_sha256.lower()}, got {actual_sha256} ({path})"
        raise HarmonyPatchBuildError(msg)
    return actual_sha256


def _exact_net48_member(archive: zipfile.ZipFile) -> zipfile.ZipInfo:
    """Return the one exact regular net48 DLL entry or fail closed."""
    matches = [info for info in archive.infolist() if info.filename == HARMONY_NET48_MEMBER]
    if len(matches) != 1 or matches[0].is_dir():
        msg = f"source archive must contain exactly one regular {HARMONY_NET48_MEMBER}; found {len(matches)}"
        raise HarmonyPatchBuildError(msg)
    return matches[0]


def extract_net48_dll(source_zip: Path, destination_dir: Path) -> Path:
    """Safely extract only the exact net48 Harmony DLL and verify its bytes."""
    destination = destination_dir / "net48" / "0Harmony.dll"
    destination.unlink(missing_ok=True)
    try:
        with zipfile.ZipFile(source_zip) as archive:
            member = _exact_net48_member(archive)
            destination.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(member, "r") as source, destination.open("xb") as target:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    target.write(chunk)
    except HarmonyPatchBuildError:
        destination.unlink(missing_ok=True)
        raise
    except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
        destination.unlink(missing_ok=True)
        msg = f"cannot safely extract {HARMONY_NET48_MEMBER} from {source_zip}: {exc}"
        raise HarmonyPatchBuildError(msg) from exc

    try:
        verify_sha256(destination, HARMONY_NET48_DLL_SHA256, HARMONY_NET48_MEMBER)
    except HarmonyPatchBuildError:
        destination.unlink(missing_ok=True)
        raise
    return destination


def _read_required_file(path: Path, description: str) -> bytes:
    """Read a required package input with an actionable build error."""
    if not path.is_file():
        msg = f"required {description} is missing: {path}"
        raise HarmonyPatchBuildError(msg)
    try:
        return path.read_bytes()
    except OSError as exc:
        msg = f"cannot read required {description}: {path}: {exc}"
        raise HarmonyPatchBuildError(msg) from exc


def _package_payloads(
    extracted_dll: Path,
    *,
    assets_dir: Path,
    qudjp_license: Path,
) -> dict[str, bytes]:
    """Collect the exact eight non-checksum package members."""
    payloads = {name: _read_required_file(assets_dir / name, name) for name in _TRACKED_ASSET_NAMES}
    payloads["LICENSE-QudJP.txt"] = _read_required_file(qudjp_license, "QudJP license")
    payloads["payload/net48/0Harmony.dll"] = _read_required_file(
        extracted_dll,
        HARMONY_NET48_MEMBER,
    )
    return payloads


def _inner_checksum_manifest(payloads: dict[str, bytes]) -> bytes:
    """Build the deterministic checksum manifest for every other member."""
    lines = [f"{hashlib.sha256(payloads[name]).hexdigest()}  {name}\n" for name in sorted(payloads)]
    return "".join(lines).encode("utf-8")


def _write_deterministic_member(archive: zipfile.ZipFile, name: str, payload: bytes) -> None:
    """Write one regular ZIP member with stable metadata and compression."""
    info = zipfile.ZipInfo(name, date_time=_FIXED_ZIP_TIMESTAMP)
    info.compress_type = zipfile.ZIP_STORED
    info.create_system = 3
    info.external_attr = 0o100644 << 16
    archive.writestr(info, payload, compress_type=zipfile.ZIP_STORED)


def _write_outer_checksum(output_zip: Path) -> Path:
    """Write the archive's sidecar checksum atomically."""
    sidecar = output_zip.with_name(f"{output_zip.name}.sha256")
    temporary = sidecar.with_name(f".{sidecar.name}.tmp")
    temporary.unlink(missing_ok=True)
    try:
        checksum = sha256_file(output_zip)
        temporary.write_text(f"{checksum}  {output_zip.name}\n", encoding="utf-8", newline="\n")
        temporary.replace(sidecar)
    except (OSError, HarmonyPatchBuildError) as exc:
        temporary.unlink(missing_ok=True)
        if isinstance(exc, HarmonyPatchBuildError):
            raise
        msg = f"cannot write Harmony patch checksum sidecar {sidecar}: {exc}"
        raise HarmonyPatchBuildError(msg) from exc
    return sidecar


def build_patch_zip(
    source_zip: Path,
    output_zip: Path,
    *,
    assets_dir: Path | None = None,
    qudjp_license: Path | None = None,
) -> list[str]:
    """Verify inputs and atomically build the exact standalone patch package."""
    assets_dir = assets_dir or PROJECT_ROOT / "steam" / "harmony-patch"
    qudjp_license = qudjp_license or PROJECT_ROOT / "LICENSE"
    sidecar = output_zip.with_name(f"{output_zip.name}.sha256")
    temporary_zip: Path | None = None

    try:
        verify_sha256(source_zip, HARMONY_ARCHIVE_SHA256, "source archive")
        output_zip.parent.mkdir(parents=True, exist_ok=True)
        output_zip.unlink(missing_ok=True)
        sidecar.unlink(missing_ok=True)

        with tempfile.TemporaryDirectory(
            dir=output_zip.parent,
            prefix=".qudjp-harmony-build-",
        ) as temporary_dir_text:
            temporary_dir = Path(temporary_dir_text)
            extracted_dll = extract_net48_dll(source_zip, temporary_dir / "extracted")
            payloads = _package_payloads(
                extracted_dll,
                assets_dir=assets_dir,
                qudjp_license=qudjp_license,
            )
            payloads["SHA256SUMS.txt"] = _inner_checksum_manifest(payloads)

            archive_payloads = {
                f"{ARCHIVE_ROOT}/{relative_name}": payload for relative_name, payload in payloads.items()
            }
            members = sorted(archive_payloads)
            temporary_zip = temporary_dir / output_zip.name
            with zipfile.ZipFile(
                temporary_zip,
                "w",
                compression=zipfile.ZIP_STORED,
            ) as archive:
                for member in members:
                    _write_deterministic_member(archive, member, archive_payloads[member])

            temporary_zip.replace(output_zip)
            temporary_zip = None

        _write_outer_checksum(output_zip)
    except HarmonyPatchBuildError:
        output_zip.unlink(missing_ok=True)
        sidecar.unlink(missing_ok=True)
        if temporary_zip is not None:
            temporary_zip.unlink(missing_ok=True)
        raise
    except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
        output_zip.unlink(missing_ok=True)
        sidecar.unlink(missing_ok=True)
        if temporary_zip is not None:
            temporary_zip.unlink(missing_ok=True)
        msg = f"failed to build Harmony patch archive: {exc}"
        raise HarmonyPatchBuildError(msg) from exc
    else:
        return members


def _download_official_archive(destination: Path) -> None:
    """Download the pinned archive only from the reviewed official HTTPS URL."""
    if not HARMONY_ARCHIVE_URL.startswith("https://github.com/pardeike/Harmony/releases/"):
        msg = f"refusing non-official Harmony archive URL: {HARMONY_ARCHIVE_URL}"
        raise HarmonyPatchBuildError(msg)

    request = urllib.request.Request(  # noqa: S310 - constant reviewed HTTPS URL.
        HARMONY_ARCHIVE_URL,
        headers={"User-Agent": "QudJP-Harmony-patch-builder/1"},
    )
    try:
        with urllib.request.urlopen(request, timeout=60) as response:  # noqa: S310
            destination.parent.mkdir(parents=True, exist_ok=True)
            with destination.open("xb") as output:
                for chunk in iter(lambda: response.read(1024 * 1024), b""):
                    output.write(chunk)
    except (http.client.HTTPException, OSError, urllib.error.URLError) as exc:
        destination.unlink(missing_ok=True)
        msg = f"cannot download official Harmony archive from {HARMONY_ARCHIVE_URL}: {exc}"
        raise HarmonyPatchBuildError(msg) from exc


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Parse CLI arguments, including explicit local-input test hooks."""
    parser = argparse.ArgumentParser(
        description="Build the verified QudJP Harmony 2.4.2 Windows patch ZIP.",
    )
    parser.add_argument(
        "--source-zip",
        type=Path,
        help="Use this local Harmony-Fat ZIP instead of downloading the official archive.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=PROJECT_ROOT / "dist" / OUTPUT_NAME,
        help="Output ZIP path (default: dist/QudJP-Harmony-2.4.2-Windows.zip).",
    )
    parser.add_argument(
        "--assets-dir",
        type=Path,
        default=PROJECT_ROOT / "steam" / "harmony-patch",
        help=argparse.SUPPRESS,
    )
    parser.add_argument(
        "--qudjp-license",
        type=Path,
        default=PROJECT_ROOT / "LICENSE",
        help=argparse.SUPPRESS,
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Build the patch from a supplied verified ZIP or a fresh official download."""
    args = _parse_args(argv)
    try:
        if args.source_zip is not None:
            members = build_patch_zip(
                args.source_zip,
                args.output,
                assets_dir=args.assets_dir,
                qudjp_license=args.qudjp_license,
            )
        else:
            with tempfile.TemporaryDirectory(prefix="qudjp-harmony-download-") as temporary_dir:
                source_zip = Path(temporary_dir) / "Harmony-Fat.2.4.2.0.zip"
                _download_official_archive(source_zip)
                members = build_patch_zip(
                    source_zip,
                    args.output,
                    assets_dir=args.assets_dir,
                    qudjp_license=args.qudjp_license,
                )

    except HarmonyPatchBuildError as exc:
        print(f"error: {exc}", file=sys.stderr)  # noqa: T201
        return 1
    else:
        sidecar = args.output.with_name(f"{args.output.name}.sha256")
        print(f"Built Harmony patch: {args.output}")  # noqa: T201
        print(f"SHA-256: {sha256_file(args.output)}")  # noqa: T201
        print(f"Checksum: {sidecar}")  # noqa: T201
        print(f"Members: {len(members)}")  # noqa: T201
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
