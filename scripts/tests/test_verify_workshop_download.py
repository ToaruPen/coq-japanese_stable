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

    assert verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=expected_dll,
    ) == []


def test_verify_workshop_download_reports_version_and_dll_mismatch(tmp_path: Path) -> None:
    """Version and DLL mismatches are both reported so releases cannot hide drift."""
    expected_dll = tmp_path / "expected" / "QudJP.dll"
    expected_dll.parent.mkdir()
    expected_dll.write_bytes(b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    _write_workshop_download(workshop_dir, version="0.2.45", dll_bytes=b"old dll")

    findings = verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert findings == [
        "manifest version mismatch: expected 0.2.46, got 0.2.45",
        "DLL SHA256 mismatch: downloaded QudJP.dll does not match expected DLL",
    ]


def test_verify_workshop_download_reports_missing_manifest(tmp_path: Path) -> None:
    """A missing manifest is reported as an actionable release validation failure."""
    expected_dll = tmp_path / "QudJP.dll"
    expected_dll.write_bytes(b"fixed dll")

    findings = verify_workshop_download(
        tmp_path / "missing-workshop",
        expected_version="0.2.46",
        expected_dll=expected_dll,
    )

    assert findings == ["Workshop manifest not found"]


def test_verify_workshop_download_accepts_expected_release_zip(tmp_path: Path) -> None:
    """The expected DLL can be read from the release ZIP used for Workshop staging."""
    release_zip = tmp_path / "QudJP-v0.2.46.zip"
    with zipfile.ZipFile(release_zip, "w") as archive:
        archive.writestr("QudJP/Assemblies/QudJP.dll", b"fixed dll")
    workshop_dir = tmp_path / "workshop" / "3718988020"
    _write_workshop_download(workshop_dir, version="0.2.46", dll_bytes=b"fixed dll")

    assert verify_workshop_download(
        workshop_dir,
        expected_version="0.2.46",
        expected_dll=release_zip,
    ) == []


def test_verify_workshop_download_rejects_invalid_expected_version(tmp_path: Path) -> None:
    """Expected versions must be the same simple semver used by manifest.json."""
    with pytest.raises(ValueError, match=r"X\.Y\.Z"):
        verify_workshop_download(
            tmp_path,
            expected_version="v0.2.46",
            expected_dll=tmp_path / "QudJP.dll",
        )
