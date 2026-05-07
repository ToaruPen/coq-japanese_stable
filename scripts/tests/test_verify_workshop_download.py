from __future__ import annotations

import json
import zipfile
from typing import TYPE_CHECKING

import pytest

from scripts.verify_workshop_download import verify_workshop_download

if TYPE_CHECKING:
    from pathlib import Path


def _write_workshop_download(root: Path, *, version: str, dll_bytes: bytes) -> None:
    """Create a minimal downloaded Workshop item fixture."""
    (root / "Assemblies").mkdir(parents=True)
    (root / "manifest.json").write_text(json.dumps({"Version": version}), encoding="utf-8")
    (root / "Assemblies" / "QudJP.dll").write_bytes(dll_bytes)


def test_verify_workshop_download_accepts_matching_version_and_dll(tmp_path: Path) -> None:
    """A downloaded Workshop item is valid when version and DLL hash match."""
    expected_dll = tmp_path / "expected" / "QudJP.dll"
    expected_dll.parent.mkdir()
    expected_dll.write_bytes(b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    _write_workshop_download(workshop_dir, version="0.2.46", dll_bytes=b"fixed dll")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert findings == []
    assert downloaded_dll_sha256 == "17af6e82115c251ea6aea5e9900ccfcd0f19ab211fd9f92d9e48b6efd02d19c6"


def test_verify_workshop_download_reports_version_and_dll_mismatch(tmp_path: Path) -> None:
    """Version and DLL mismatches are both reported so releases cannot hide drift."""
    expected_dll = tmp_path / "expected" / "QudJP.dll"
    expected_dll.parent.mkdir()
    expected_dll.write_bytes(b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    _write_workshop_download(workshop_dir, version="0.2.45", dll_bytes=b"old dll")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert findings == [
        "manifest version mismatch: expected 0.2.46, got 0.2.45",
        "DLL SHA256 mismatch: downloaded QudJP.dll does not match expected DLL",
    ]
    assert downloaded_dll_sha256 == "6fa63fac4dbe68a82c66caa07a3d821e10d043e5ca2a1c774d4da9671f1305a9"


def test_verify_workshop_download_reports_missing_manifest(tmp_path: Path) -> None:
    """A missing manifest is reported as an actionable release validation failure."""
    expected_dll = tmp_path / "QudJP.dll"
    expected_dll.write_bytes(b"fixed dll")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        tmp_path / "missing-workshop",
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert findings == ["Workshop manifest not found"]
    assert downloaded_dll_sha256 is None


def test_verify_workshop_download_reports_missing_assembly(tmp_path: Path) -> None:
    """A missing Workshop DLL is reported without crashing."""
    expected_dll = tmp_path / "QudJP.dll"
    expected_dll.write_bytes(b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    workshop_dir.mkdir(parents=True)
    (workshop_dir / "manifest.json").write_text(json.dumps({"Version": "0.2.46"}), encoding="utf-8")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert findings == ["Workshop DLL not found: Assemblies/QudJP.dll"]
    assert downloaded_dll_sha256 is None


def test_verify_workshop_download_reports_unreadable_manifest(tmp_path: Path) -> None:
    """A manifest with invalid encoding is reported as a validation finding."""
    expected_dll = tmp_path / "QudJP.dll"
    expected_dll.write_bytes(b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    (workshop_dir / "Assemblies").mkdir(parents=True)
    (workshop_dir / "manifest.json").write_bytes(b"\xff")
    (workshop_dir / "Assemblies" / "QudJP.dll").write_bytes(b"fixed dll")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert len(findings) == 2
    assert findings[0].startswith("Workshop manifest is not valid JSON or could not be read:")
    assert findings[1] == "manifest version mismatch: expected 0.2.46, got <missing>"
    assert downloaded_dll_sha256 == "17af6e82115c251ea6aea5e9900ccfcd0f19ab211fd9f92d9e48b6efd02d19c6"


def test_verify_workshop_download_accepts_expected_release_zip(tmp_path: Path) -> None:
    """The expected DLL can be read from the release ZIP used for Workshop staging."""
    release_zip = tmp_path / "QudJP-v0.2.46.zip"
    with zipfile.ZipFile(release_zip, "w") as archive:
        archive.writestr("QudJP/Assemblies/QudJP.dll", b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    _write_workshop_download(workshop_dir, version="0.2.46", dll_bytes=b"fixed dll")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=release_zip,
    )

    assert findings == []
    assert downloaded_dll_sha256 == "17af6e82115c251ea6aea5e9900ccfcd0f19ab211fd9f92d9e48b6efd02d19c6"


def test_verify_workshop_download_reports_invalid_expected_release_zip(tmp_path: Path) -> None:
    """An unreadable release ZIP is reported as a validation finding."""
    release_zip = tmp_path / "QudJP-v0.2.46.zip"
    release_zip.write_bytes(b"not a zip")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    _write_workshop_download(workshop_dir, version="0.2.46", dll_bytes=b"fixed dll")

    findings, downloaded_dll_sha256 = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=release_zip,
    )

    assert findings == [f"expected DLL could not be read from {release_zip}: File is not a zip file"]
    assert downloaded_dll_sha256 is None


def test_verify_workshop_download_rejects_invalid_expected_version(tmp_path: Path) -> None:
    """Expected versions must be the same simple semver used by manifest.json."""
    with pytest.raises(ValueError, match=r"X\.Y\.Z"):
        verify_workshop_download(
            tmp_path,
            expected_version="v0.2.46",
            expected_dll=tmp_path / "QudJP.dll",
        )
