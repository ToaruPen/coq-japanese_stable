"""Tests for Steam Workshop upload staging and VDF generation."""

import json
import os
import re
import zipfile
from pathlib import Path

import pytest

from scripts.build_workshop_upload import (
    WORKSHOP_APP_ID,
    WORKSHOP_PUBLISHED_FILE_ID,
    WorkshopMetadata,
    create_workshop_staging,
    find_latest_release_zip,
    load_metadata,
    main,
    render_vdf,
    validate_workshop_changenote,
    vdf_escape,
    verify_workshop_upload_staging,
)
from scripts.tests._common import (
    VALID_RELEASE_DLL_MARKER_PAYLOAD,
    write_workshop_release_zip,
)


def _write_release_zip(
    path: Path,
    *,
    version: str = "0.2.0",
    dll_payload: bytes = VALID_RELEASE_DLL_MARKER_PAYLOAD,
) -> None:
    """Create a minimal QudJP release ZIP fixture."""
    write_workshop_release_zip(path, version=version, dll_payload=dll_payload)


def test_default_workshop_ids_are_caves_of_qud_item() -> None:
    """Checked-in defaults target the published QudJP Workshop item."""
    assert WORKSHOP_APP_ID == "333640"
    assert WORKSHOP_PUBLISHED_FILE_ID == "3718988020"


def test_load_metadata_reads_checked_in_workshop_item(tmp_path: Path) -> None:
    """Workshop metadata is loaded from a repo-managed JSON file."""
    description = tmp_path / "description.ja.txt"
    description.write_text("Caves of Qud 日本語化", encoding="utf-8")
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(
        json.dumps(
            {
                "appid": "333640",
                "publishedfileid": "3718988020",
                "title": "Caves of Qud Japanese Mod",
                "visibility": "0",
                "description_file": str(description),
            },
        ),
        encoding="utf-8",
    )

    metadata = load_metadata(metadata_path)

    assert metadata == WorkshopMetadata(
        appid="333640",
        publishedfileid="3718988020",
        title="Caves of Qud Japanese Mod",
        visibility="0",
        description_file=description,
    )


def test_load_metadata_rejects_missing_published_file_id(tmp_path: Path) -> None:
    """The existing Workshop item ID is required for updates."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(
        json.dumps({"appid": "333640", "title": "Caves of Qud Japanese Mod"}),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="publishedfileid"):
        load_metadata(metadata_path)


def test_load_metadata_rejects_non_object_json(tmp_path: Path) -> None:
    """Metadata JSON must be an object so the CLI can report validation errors."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text("[]", encoding="utf-8")

    with pytest.raises(ValueError, match="JSON object"):
        load_metadata(metadata_path)


def test_vdf_escape_handles_quotes_backslashes_and_preserves_newlines() -> None:
    """VDF string values are escaped before writing quoted fields."""
    assert vdf_escape('Path "A"\\B\nNext') == 'Path \\"A\\"\\\\B\nNext'


def test_render_vdf_contains_absolute_content_preview_and_changenote(tmp_path: Path) -> None:
    """Rendered VDF points steamcmd at absolute staging and preview paths."""
    content_folder = tmp_path / "QudJP"
    content_folder.mkdir()
    preview_file = content_folder / "preview.png"
    preview_file.write_bytes(b"png")
    metadata = WorkshopMetadata(
        appid="333640",
        publishedfileid="3718988020",
        title="Caves of Qud Japanese Mod",
        visibility="0",
        description_file=None,
    )

    vdf = render_vdf(
        metadata,
        content_folder=content_folder,
        preview_file=preview_file,
        changenote="v0.2.0: release",
        description="Caves of Qud 日本語化",
    )

    assert '"appid" "333640"' in vdf
    assert '"publishedfileid" "3718988020"' in vdf
    assert f'"contentfolder" "{content_folder.resolve()}"' in vdf
    assert f'"previewfile" "{preview_file.resolve()}"' in vdf
    assert '"changenote" "v0.2.0: release"' in vdf
    assert '"description" "Caves of Qud 日本語化"' in vdf


def test_checked_in_default_description_renders_bbcode_with_literal_newlines(tmp_path: Path) -> None:
    """The checked-in English BBCode keeps literal line breaks in rendered VDF."""
    metadata_path = Path(__file__).resolve().parents[2] / "steam" / "workshop_metadata.json"
    metadata = load_metadata(metadata_path)
    assert metadata.description_file is not None
    description = metadata.description_file.read_text(encoding="utf-8")

    vdf = render_vdf(
        metadata,
        content_folder=tmp_path / "QudJP",
        preview_file=tmp_path / "QudJP" / "preview.png",
        changenote="説明文の書式を更新しました。",
        description=description,
    )

    expected_description_field = f'  "description" "{vdf_escape(description)}"\n'
    assert expected_description_field in vdf
    assert "[h1]Overview[/h1]\n" in vdf
    assert "[olist]\n[*]Subscribe" in vdf
    assert r"[h1]Overview[/h1]\n" not in vdf


def test_render_vdf_rejects_quotes_in_description_or_changenote(tmp_path: Path) -> None:
    """Steam KeyValues parsing is fragile with escaped quotes in long text fields."""
    content_folder = tmp_path / "QudJP"
    content_folder.mkdir()
    preview_file = content_folder / "preview.png"
    preview_file.write_bytes(b"png")
    metadata = WorkshopMetadata(
        appid="333640",
        publishedfileid="3718988020",
        title="Caves of Qud Japanese Mod",
        visibility="0",
        description_file=None,
    )

    with pytest.raises(ValueError, match=r"description.*double quote"):
        render_vdf(
            metadata,
            content_folder=content_folder,
            preview_file=preview_file,
            changenote="v0.2.0 release",
            description='Launch with "Rosetta"',
        )

    with pytest.raises(ValueError, match=r"changenote.*double quote"):
        render_vdf(
            metadata,
            content_folder=content_folder,
            preview_file=preview_file,
            changenote='v0.2.0 "release"',
            description="Caves of Qud 日本語化",
        )


def test_validate_workshop_changenote_rejects_internal_release_terms() -> None:
    """Workshop changenotes must stay player-facing rather than implementation-facing."""
    findings = validate_workshop_changenote(
        "v0.2.51 / abc123\n\n"
        "主な改善:\n"
        "- InventoryLine の TMP override を調整しました。\n"
        "- CI preflight を強化しました。",
    )

    assert findings == [
        "Workshop changenote contains internal term 'CI'; rewrite it in player-facing terms",
        "Workshop changenote contains internal term 'InventoryLine'; rewrite it in player-facing terms",
        "Workshop changenote contains internal term 'override'; rewrite it in player-facing terms",
        "Workshop changenote contains internal term 'preflight'; rewrite it in player-facing terms",
        "Workshop changenote contains internal term 'TMP'; rewrite it in player-facing terms",
    ]


def test_render_vdf_rejects_internal_terms_in_changenote(tmp_path: Path) -> None:
    """VDF generation refuses changenotes that describe repo internals."""
    content_folder = tmp_path / "QudJP"
    content_folder.mkdir()
    preview_file = content_folder / "preview.png"
    preview_file.write_bytes(b"png")
    metadata = WorkshopMetadata(
        appid="333640",
        publishedfileid="3718988020",
        title="Caves of Qud Japanese Mod",
        visibility="0",
        description_file=None,
    )

    with pytest.raises(ValueError, match="InventoryLine"):
        render_vdf(
            metadata,
            content_folder=content_folder,
            preview_file=preview_file,
            changenote="v0.2.51 / abc123\n\n主な改善:\n- InventoryLine の表示を修正しました。",
            description="Caves of Qud 日本語化",
        )


def test_create_workshop_staging_extracts_qudjp_root(tmp_path: Path) -> None:
    """Workshop staging contains the mod files directly under QudJP/."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(release_zip)
    staging_root = tmp_path / "dist" / "workshop"

    content_folder, preview_file = create_workshop_staging(release_zip, staging_root)

    assert content_folder == staging_root / "QudJP"
    assert (content_folder / "manifest.json").is_file()
    assert (content_folder / "Assemblies" / "QudJP.dll").is_file()
    assert (content_folder / "Localization" / "ui.json").is_file()
    assert os.access(content_folder / "Launch CavesOfQud (Rosetta).command", os.X_OK)
    assert os.access(content_folder / "Install Native Apple Silicon Harmony.command", os.X_OK)
    assert os.access(content_folder / "Restore Game Harmony.command", os.X_OK)
    assert preview_file == content_folder / "preview.png"


def test_create_workshop_staging_accepts_zip_content_without_zip_suffix(tmp_path: Path) -> None:
    """Workshop staging validates ZIP content without requiring a .zip file suffix."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0"
    _write_release_zip(release_zip)
    staging_root = tmp_path / "dist" / "workshop"

    content_folder, _preview_file = create_workshop_staging(release_zip, staging_root)

    assert (content_folder / "Assemblies" / "QudJP.dll").is_file()


def test_create_workshop_staging_removes_previous_generated_contents(tmp_path: Path) -> None:
    """Regenerating staging removes stale files from previous releases."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(release_zip)
    staging_root = tmp_path / "dist" / "workshop"
    stale_file = staging_root / "QudJP" / "stale.txt"
    stale_file.parent.mkdir(parents=True)
    stale_file.write_text("stale", encoding="utf-8")

    create_workshop_staging(release_zip, staging_root)

    assert not stale_file.exists()


def test_create_workshop_staging_rejects_zip_without_qudjp_root(tmp_path: Path) -> None:
    """Release ZIPs must use the QudJP/ archive root."""
    release_zip = tmp_path / "dist" / "bad.zip"
    release_zip.parent.mkdir()
    with zipfile.ZipFile(release_zip, "w") as zf:
        zf.writestr("manifest.json", "{}")

    with pytest.raises(ValueError, match="QudJP/"):
        create_workshop_staging(release_zip, tmp_path / "workshop")


def test_create_workshop_staging_rejects_zip_without_compliance_files(tmp_path: Path) -> None:
    """Workshop ZIPs must include release compliance files."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    release_zip.parent.mkdir()
    with zipfile.ZipFile(release_zip, "w") as zf:
        zf.writestr("QudJP/manifest.json", "{}")
        zf.writestr("QudJP/preview.png", b"png")
        zf.writestr("QudJP/Bootstrap.cs", "public static class Bootstrap {}")
        zf.writestr("QudJP/Assemblies/QudJP.dll", b"dll")
        zf.writestr("QudJP/Localization/ui.json", "{}")
        zf.writestr("QudJP/Fonts/OFL.txt", "SIL Open Font License")

    with pytest.raises(ValueError, match="QudJP/LICENSE"):
        create_workshop_staging(release_zip, tmp_path / "workshop")


def test_create_workshop_staging_rejects_zip_without_localization_or_fonts(tmp_path: Path) -> None:
    """Workshop ZIPs must include Localization and Fonts assets."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    release_zip.parent.mkdir()
    with zipfile.ZipFile(release_zip, "w") as zf:
        zf.writestr("QudJP/manifest.json", "{}")
        zf.writestr("QudJP/preview.png", b"png")
        zf.writestr("QudJP/LICENSE", "MIT License")
        zf.writestr("QudJP/NOTICE.md", "# NOTICE")
        zf.writestr("QudJP/Bootstrap.cs", "public static class Bootstrap {}")
        zf.writestr("QudJP/Assemblies/QudJP.dll", b"dll")

    with pytest.raises(ValueError, match="QudJP/Fonts/"):
        create_workshop_staging(release_zip, tmp_path / "workshop")


def test_create_workshop_staging_rejects_release_zip_with_dev_probe_markers(tmp_path: Path) -> None:
    """Workshop staging rejects release ZIPs whose DLL still contains dev-only probes."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(
        release_zip,
        dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD + b"\0[QudJP] SinkObserve/v1:",
    )

    with pytest.raises(
        ValueError,
        match=rf"{re.escape(str(release_zip))}.*forbidden dev marker: \[QudJP\] SinkObserve/v1:",
    ):
        create_workshop_staging(release_zip, tmp_path / "workshop")


def test_create_workshop_staging_rejects_release_zip_missing_required_dll_markers(tmp_path: Path) -> None:
    """Workshop staging rejects ZIPs whose DLL is missing required release markers."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(release_zip, dll_payload=b"dll")

    with pytest.raises(ValueError, match=rf"{re.escape(str(release_zip))}.*Unity\.TextMeshPro"):
        create_workshop_staging(release_zip, tmp_path / "workshop")


def test_verify_workshop_upload_staging_accepts_matching_release_zip(tmp_path: Path) -> None:
    """Upload preflight accepts staging generated from the same release ZIP."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    _write_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, _preview = create_workshop_staging(release_zip, tmp_path / "dist" / "workshop")

    findings = verify_workshop_upload_staging(release_zip, content_folder, expected_version="0.2.50")

    assert findings == []


def test_verify_workshop_upload_staging_reports_stale_content_folder(tmp_path: Path) -> None:
    """Upload preflight reports stale staged content before steamcmd can publish it."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    _write_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    stale_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(stale_zip, version="0.2.0", dll_payload=b"old dll" + VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, _preview = create_workshop_staging(stale_zip, tmp_path / "dist" / "workshop")

    findings = verify_workshop_upload_staging(release_zip, content_folder, expected_version="0.2.50")

    assert findings == [
        "staged manifest version mismatch: expected 0.2.50, got 0.2.0",
        "staged QudJP.dll SHA256 does not match release ZIP",
        "staged file hash mismatch: manifest.json",
    ]


def test_verify_workshop_upload_staging_reports_stale_localization_asset(tmp_path: Path) -> None:
    """Upload preflight reports stale non-DLL files, not only manifest and assembly drift."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    _write_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, _preview = create_workshop_staging(release_zip, tmp_path / "dist" / "workshop")
    (content_folder / "Localization" / "ui.json").write_text('{"stale": true}', encoding="utf-8")

    findings = verify_workshop_upload_staging(release_zip, content_folder, expected_version="0.2.50")

    assert findings == ["staged file hash mismatch: Localization/ui.json"]


def test_verify_workshop_upload_staging_reports_missing_and_extra_files(tmp_path: Path) -> None:
    """Upload preflight reports content set drift before steamcmd upload."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    _write_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, _preview = create_workshop_staging(release_zip, tmp_path / "dist" / "workshop")
    (content_folder / "preview.png").unlink()
    (content_folder / "stale.txt").write_text("stale", encoding="utf-8")

    findings = verify_workshop_upload_staging(release_zip, content_folder, expected_version="0.2.50")

    assert findings == [
        "staged content missing file: preview.png",
        "staged content has extra file: stale.txt",
    ]


def test_verify_workshop_upload_staging_reports_same_version_manifest_drift(tmp_path: Path) -> None:
    """Upload preflight reports manifest drift even when Version still matches."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.50.zip"
    _write_release_zip(release_zip, version="0.2.50", dll_payload=VALID_RELEASE_DLL_MARKER_PAYLOAD)
    content_folder, _preview = create_workshop_staging(release_zip, tmp_path / "dist" / "workshop")
    (content_folder / "manifest.json").write_text(
        json.dumps({"Version": "0.2.50", "PreviewImage": "old-preview.png"}),
        encoding="utf-8",
    )

    findings = verify_workshop_upload_staging(release_zip, content_folder, expected_version="0.2.50")

    assert findings == ["staged file hash mismatch: manifest.json"]


def test_find_latest_release_zip_uses_newest_mtime(tmp_path: Path) -> None:
    """Latest release ZIP is chosen by modification time."""
    older = tmp_path / "QudJP-v0.1.0.zip"
    newer = tmp_path / "QudJP-v0.2.0.zip"
    _write_release_zip(older, version="0.1.0")
    _write_release_zip(newer, version="0.2.0")
    os.utime(older, (1_700_000_000, 1_700_000_000))
    os.utime(newer, (1_700_000_001, 1_700_000_001))

    assert find_latest_release_zip(tmp_path) == newer


def test_find_latest_release_zip_uses_name_tiebreaker_for_equal_mtime(tmp_path: Path) -> None:
    """Equal mtimes are resolved deterministically by file name."""
    first = tmp_path / "QudJP-v0.2.0.zip"
    second = tmp_path / "QudJP-v0.3.0.zip"
    _write_release_zip(first, version="0.2.0")
    _write_release_zip(second, version="0.3.0")
    os.utime(first, (1_700_000_000, 1_700_000_000))
    os.utime(second, (1_700_000_000, 1_700_000_000))

    assert find_latest_release_zip(tmp_path) == second


def test_main_writes_vdf_and_staging(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI creates staging and workshop_item.vdf for steamcmd."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(release_zip)
    description = tmp_path / "steam" / "description.ja.txt"
    description.parent.mkdir()
    description.write_text("Caves of Qud 日本語化", encoding="utf-8")
    metadata = tmp_path / "steam" / "workshop_metadata.json"
    metadata.write_text(
        json.dumps(
            {
                "appid": "333640",
                "publishedfileid": "3718988020",
                "title": "Caves of Qud Japanese Mod",
                "visibility": "0",
                "description_file": str(description),
            },
        ),
        encoding="utf-8",
    )
    staging_root = tmp_path / "dist" / "workshop"
    vdf_output = staging_root / "workshop_item.vdf"

    exit_code = main(
        [
            "--release-zip",
            str(release_zip),
            "--metadata",
            str(metadata),
            "--staging-dir",
            str(staging_root),
            "--vdf-output",
            str(vdf_output),
            "--changenote",
            "v0.2.0: initial steamcmd setup",
        ],
    )

    assert exit_code == 0
    assert (staging_root / "QudJP" / "manifest.json").is_file()
    vdf = vdf_output.read_text(encoding="utf-8")
    assert '"publishedfileid" "3718988020"' in vdf
    assert '"changenote" "v0.2.0: initial steamcmd setup"' in vdf
    assert (
        f"Upload command: steamcmd +login <user> +workshop_build_item {vdf_output.resolve()} +quit"
        in capsys.readouterr().out
    )


def test_main_reads_changenote_file(tmp_path: Path) -> None:
    """CLI accepts release notes from a checked or temporary changenote file."""
    release_zip = tmp_path / "dist" / "QudJP-v0.2.0.zip"
    _write_release_zip(release_zip)
    metadata = tmp_path / "steam" / "workshop_metadata.json"
    metadata.parent.mkdir()
    metadata.write_text(
        json.dumps(
            {
                "appid": "333640",
                "publishedfileid": "3718988020",
                "title": "Caves of Qud Japanese Mod",
                "visibility": "0",
            },
        ),
        encoding="utf-8",
    )
    changenote = tmp_path / "steam" / "changenote.md"
    changenote.write_text("v0.2.0 / abc1234\n\n更新内容:\n- UI 翻訳を更新", encoding="utf-8")
    vdf_output = tmp_path / "dist" / "workshop" / "workshop_item.vdf"

    exit_code = main(
        [
            "--release-zip",
            str(release_zip),
            "--metadata",
            str(metadata),
            "--vdf-output",
            str(vdf_output),
            "--changenote-file",
            str(changenote),
        ],
    )

    assert exit_code == 0
    vdf = vdf_output.read_text(encoding="utf-8")
    assert '"changenote" "v0.2.0 / abc1234\n\n更新内容:\n- UI 翻訳を更新"' in vdf
