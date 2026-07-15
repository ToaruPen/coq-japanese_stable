"""Tests for the standalone Windows Harmony patch package."""

from __future__ import annotations

import hashlib
import zipfile
from typing import TYPE_CHECKING

import pytest

from scripts import build_harmony_patch

if TYPE_CHECKING:
    from pathlib import Path


EXPECTED_MEMBERS = {
    "QudJP-Harmony-2.4.2-Windows/Install Harmony 2.4.2.cmd",
    "QudJP-Harmony-2.4.2-Windows/Restore Game Harmony.cmd",
    "QudJP-Harmony-2.4.2-Windows/QudJP-Harmony-2.4.2.ps1",
    "QudJP-Harmony-2.4.2-Windows/README-ja.txt",
    "QudJP-Harmony-2.4.2-Windows/SHA256SUMS.txt",
    "QudJP-Harmony-2.4.2-Windows/LICENSE-QudJP.txt",
    "QudJP-Harmony-2.4.2-Windows/LICENSE-Harmony.txt",
    "QudJP-Harmony-2.4.2-Windows/THIRD-PARTY-NOTICES.txt",
    "QudJP-Harmony-2.4.2-Windows/payload/net48/0Harmony.dll",
}
ARCHIVE_ROOT = "QudJP-Harmony-2.4.2-Windows"
CHECKSUM_MEMBER = f"{ARCHIVE_ROOT}/SHA256SUMS.txt"


def _sha256_bytes(payload: bytes) -> str:
    """Return the SHA-256 digest for fixture bytes."""
    return hashlib.sha256(payload).hexdigest()


def _write_patch_assets(assets_dir: Path) -> Path:
    """Create minimal tracked inputs for the package builder."""
    assets_dir.mkdir()
    for name in (
        "Install Harmony 2.4.2.cmd",
        "Restore Game Harmony.cmd",
        "QudJP-Harmony-2.4.2.ps1",
        "README-ja.txt",
        "LICENSE-Harmony.txt",
        "THIRD-PARTY-NOTICES.txt",
    ):
        (assets_dir / name).write_text(f"fixture: {name}\n", encoding="utf-8")

    qudjp_license = assets_dir.parent / "LICENSE"
    qudjp_license.write_text("fixture: QudJP license\n", encoding="utf-8")
    return qudjp_license


def _write_source_zip(source_zip: Path, dll_payload: bytes) -> None:
    """Create a minimal Harmony Fat fixture containing the net48 DLL."""
    with zipfile.ZipFile(source_zip, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("net48/0Harmony.dll", dll_payload)


def _build_fixture_patch(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    """Build a patch ZIP after pinning both hashes to deterministic fixture bytes."""
    dll_payload = b"fixture Harmony net48 DLL"
    source_zip = tmp_path / "Harmony-Fat.2.4.2.0.zip"
    _write_source_zip(source_zip, dll_payload)
    assets_dir = tmp_path / "harmony-patch"
    qudjp_license = _write_patch_assets(assets_dir)
    output_zip = tmp_path / "QudJP-Harmony-2.4.2-Windows.zip"
    monkeypatch.setattr(
        build_harmony_patch,
        "HARMONY_ARCHIVE_SHA256",
        build_harmony_patch.sha256_file(source_zip),
    )
    monkeypatch.setattr(build_harmony_patch, "HARMONY_NET48_DLL_SHA256", _sha256_bytes(dll_payload))
    build_harmony_patch.build_patch_zip(
        source_zip,
        output_zip,
        assets_dir=assets_dir,
        qudjp_license=qudjp_license,
    )
    return output_zip


def test_pins_official_harmony_fat_and_net48_dll_hashes() -> None:
    """The builder accepts only the reviewed official Harmony 2.4.2 inputs."""
    assert (
        build_harmony_patch.HARMONY_ARCHIVE_SHA256 == "a5fc5f9d9640b927d786a0527faa18bf7aa776788235140c59e9b73de87a7774"
    )
    assert (
        build_harmony_patch.HARMONY_NET48_DLL_SHA256
        == "77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d"
    )


def test_build_patch_zip_has_exact_member_contract(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """A verified fixture archive builds the complete standalone package."""
    dll_payload = b"fixture Harmony net48 DLL"
    source_zip = tmp_path / "Harmony-Fat.2.4.2.0.zip"
    with zipfile.ZipFile(source_zip, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("net48/0Harmony.dll", dll_payload)

    assets_dir = tmp_path / "harmony-patch"
    qudjp_license = _write_patch_assets(assets_dir)
    output_zip = tmp_path / "QudJP-Harmony-2.4.2-Windows.zip"
    monkeypatch.setattr(
        build_harmony_patch,
        "HARMONY_ARCHIVE_SHA256",
        build_harmony_patch.sha256_file(source_zip),
    )
    monkeypatch.setattr(build_harmony_patch, "HARMONY_NET48_DLL_SHA256", _sha256_bytes(dll_payload))

    members = build_harmony_patch.build_patch_zip(
        source_zip,
        output_zip,
        assets_dir=assets_dir,
        qudjp_license=qudjp_license,
    )

    with zipfile.ZipFile(output_zip) as archive:
        names = set(archive.namelist())
        packaged_dll = archive.read("QudJP-Harmony-2.4.2-Windows/payload/net48/0Harmony.dll")

    assert names == EXPECTED_MEMBERS
    assert members == sorted(EXPECTED_MEMBERS)
    assert packaged_dll == dll_payload


def test_build_patch_zip_rejects_source_archive_hash_mismatch(tmp_path: Path) -> None:
    """An unreviewed source archive is rejected before package creation."""
    source_zip = tmp_path / "Harmony-Fat.2.4.2.0.zip"
    _write_source_zip(source_zip, b"fixture Harmony net48 DLL")
    assets_dir = tmp_path / "harmony-patch"
    qudjp_license = _write_patch_assets(assets_dir)
    output_zip = tmp_path / "QudJP-Harmony-2.4.2-Windows.zip"

    with pytest.raises(build_harmony_patch.HarmonyPatchBuildError, match="source archive SHA-256 mismatch"):
        build_harmony_patch.build_patch_zip(
            source_zip,
            output_zip,
            assets_dir=assets_dir,
            qudjp_license=qudjp_license,
        )

    assert not output_zip.exists()


def test_build_patch_zip_rejects_net48_dll_hash_mismatch(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """A reviewed container cannot substitute different net48 DLL bytes."""
    source_zip = tmp_path / "Harmony-Fat.2.4.2.0.zip"
    _write_source_zip(source_zip, b"fixture Harmony net48 DLL")
    assets_dir = tmp_path / "harmony-patch"
    qudjp_license = _write_patch_assets(assets_dir)
    output_zip = tmp_path / "QudJP-Harmony-2.4.2-Windows.zip"
    monkeypatch.setattr(
        build_harmony_patch,
        "HARMONY_ARCHIVE_SHA256",
        build_harmony_patch.sha256_file(source_zip),
    )

    with pytest.raises(build_harmony_patch.HarmonyPatchBuildError, match=r"net48/0Harmony\.dll SHA-256 mismatch"):
        build_harmony_patch.build_patch_zip(
            source_zip,
            output_zip,
            assets_dir=assets_dir,
            qudjp_license=qudjp_license,
        )

    assert not output_zip.exists()


def test_inner_sha256sums_match_every_packaged_member(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """The inner checksum manifest names and hashes every other packaged file."""
    output_zip = _build_fixture_patch(tmp_path, monkeypatch)

    with zipfile.ZipFile(output_zip) as archive:
        checksum_text = archive.read(CHECKSUM_MEMBER).decode("utf-8")
        checksums: dict[str, str] = {}
        checksum_lines = checksum_text.splitlines()
        for line in checksum_lines:
            digest, separator, relative_name = line.partition("  ")
            assert separator == "  "
            checksums[relative_name] = digest

        expected_targets = {
            member.removeprefix(f"{ARCHIVE_ROOT}/") for member in EXPECTED_MEMBERS if member != CHECKSUM_MEMBER
        }
        assert len(checksums) == len(checksum_lines)
        assert set(checksums) == expected_targets
        for relative_name, digest in checksums.items():
            member_bytes = archive.read(f"{ARCHIVE_ROOT}/{relative_name}")
            assert digest == _sha256_bytes(member_bytes)


def test_notice_distinguishes_mod_zip_from_opt_in_patch_zip() -> None:
    """Repository notices state which release asset redistributes Harmony."""
    notice = (build_harmony_patch.PROJECT_ROOT / "NOTICE.md").read_text(encoding="utf-8")

    assert "normal QudJP mod ZIP does not bundle `0Harmony.dll`" in notice
    assert "standalone opt-in Windows Harmony" in notice
    assert "patch ZIP bundles `0Harmony.dll`" in notice


def test_patch_legal_assets_cover_usage_scope_and_embedded_dependencies() -> None:
    """Tracked player guidance and third-party notices cover the package scope."""
    assets_dir = build_harmony_patch.PROJECT_ROOT / "steam" / "harmony-patch"
    readme = (assets_dir / "README-ja.txt").read_text(encoding="utf-8")
    third_party = (assets_dir / "THIRD-PARTY-NOTICES.txt").read_text(encoding="utf-8")
    harmony_license = (assets_dir / "LICENSE-Harmony.txt").read_text(encoding="utf-8")

    for term in (
        "Caves of Qud 1.0.5",
        "任意",
        "終了",
        "バックアップ",
        "復元",
        "SHA-256",
        "Steam",
        "ゲーム更新",
        "FPS",
        "CPU",
        "https://github.com/pardeike/Harmony",
    ):
        assert term in readme

    for term in ("MonoMod", "Mono.Cecil", "iced", "MIT License", "Copyright", "https://github.com/"):
        assert term in third_party

    assert harmony_license.startswith("MIT License\n\nCopyright (c) 2017 Andreas Pardeike")
    assert 'THE SOFTWARE IS PROVIDED "AS IS"' in harmony_license
