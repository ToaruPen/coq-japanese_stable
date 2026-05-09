"""Tests for the final Steam Workshop upload preflight."""

from __future__ import annotations

from typing import TYPE_CHECKING

from scripts.build_workshop_upload import WorkshopMetadata, create_workshop_staging, render_vdf
from scripts.tests._common import VALID_RELEASE_DLL_MARKER_PAYLOAD, write_workshop_release_zip
from scripts.verify_workshop_upload import main, verify_workshop_vdf

if TYPE_CHECKING:
    from pathlib import Path

    import pytest


def test_verify_workshop_vdf_reports_identity_and_escaped_text_findings(tmp_path: Path) -> None:
    """The VDF gate catches stale targets and text that steamcmd can misparse."""
    vdf = tmp_path / "workshop_item.vdf"
    vdf.write_text(
        "\n".join(
            [
                '"workshopitem"',
                "{",
                '  "appid" "333640"',
                '  "publishedfileid" "123"',
                f'  "contentfolder" "{tmp_path / "old"}"',
                f'  "previewfile" "{tmp_path / "old-preview.png"}"',
                '  "description" "Run \\"quoted\\" text"',
                "}",
            ],
        ),
        encoding="utf-8",
    )

    findings = verify_workshop_vdf(vdf, content_folder=tmp_path / "QudJP")

    assert findings == [
        "VDF publishedfileid mismatch: expected 3718988020, got 123",
        f"VDF contentfolder mismatch: expected {(tmp_path / 'QudJP').resolve()}, got {tmp_path / 'old'}",
        f"VDF previewfile mismatch: expected {(tmp_path / 'QudJP' / 'preview.png').resolve()}, "
        f"got {tmp_path / 'old-preview.png'}",
        'VDF contains escaped double quote sequences (\\"); remove double quotes from text fields',
    ]


def test_main_accepts_matching_staging_and_vdf(tmp_path: Path) -> None:
    """The CLI accepts upload files generated from the same release ZIP."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    write_workshop_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, preview_file = create_workshop_staging(release_zip, tmp_path / "dist" / "workshop")
    vdf = tmp_path / "dist" / "workshop" / "workshop_item.vdf"
    vdf.write_text(
        render_vdf(
            WorkshopMetadata(
                appid="333640",
                publishedfileid="3718988020",
                title="Caves of Qud Japanese Mod",
                visibility="0",
                description_file=None,
            ),
            content_folder=content_folder,
            preview_file=preview_file,
            changenote="v0.2.50 release",
            description="Caves of Qud 日本語化",
        ),
        encoding="utf-8",
    )

    exit_code = main(
        [
            "--release-zip",
            str(release_zip),
            "--content-folder",
            str(content_folder),
            "--vdf",
            str(vdf),
            "--expected-version",
            "0.2.50",
        ],
    )

    assert exit_code == 0


def test_main_rejects_stale_staging(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """The CLI rejects a content folder left over from an older release."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    write_workshop_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    stale_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    write_workshop_release_zip(stale_zip, version="0.2.0", dll_payload=b"old dll" + VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, preview_file = create_workshop_staging(stale_zip, tmp_path / "dist" / "workshop")
    vdf = tmp_path / "dist" / "workshop" / "workshop_item.vdf"
    vdf.write_text(
        render_vdf(
            WorkshopMetadata(
                appid="333640",
                publishedfileid="3718988020",
                title="Caves of Qud Japanese Mod",
                visibility="0",
                description_file=None,
            ),
            content_folder=content_folder,
            preview_file=preview_file,
            changenote="v0.2.50 release",
            description="Caves of Qud 日本語化",
        ),
        encoding="utf-8",
    )

    exit_code = main(
        [
            "--release-zip",
            str(release_zip),
            "--content-folder",
            str(content_folder),
            "--vdf",
            str(vdf),
            "--expected-version",
            "0.2.50",
        ],
    )

    assert exit_code == 1
    stderr = capsys.readouterr().err
    assert "Error: staged manifest version mismatch: expected 0.2.50, got 0.2.0" in stderr
    assert "Error: staged QudJP.dll SHA256 does not match release ZIP" in stderr


def test_main_reports_missing_release_zip_without_traceback(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """The operator-facing CLI reports missing release artifacts as a clean error."""
    exit_code = main(
        [
            "--release-zip",
            str(tmp_path / "missing.zip"),
            "--content-folder",
            str(tmp_path / "QudJP"),
            "--vdf",
            str(tmp_path / "workshop_item.vdf"),
            "--expected-version",
            "0.2.50",
        ],
    )

    assert exit_code == 1
    stderr = capsys.readouterr().err
    assert stderr.startswith("Error: ")
    assert "Traceback" not in stderr
