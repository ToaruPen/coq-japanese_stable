"""Tests for opt-in native Apple Silicon Harmony helper scripts."""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

_INSTALL_SCRIPT = Path("Mods/QudJP/Install Native Apple Silicon Harmony.command")
_RESTORE_SCRIPT = Path("Mods/QudJP/Restore Game Harmony.command")


def _script_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


@pytest.mark.parametrize("script_path", [_INSTALL_SCRIPT, _RESTORE_SCRIPT])
def test_native_harmony_scripts_are_valid_bash(script_path: Path) -> None:
    """The distributed opt-in helpers remain valid for macOS /bin/bash."""
    subprocess.run(["/bin/bash", "-n", str(script_path)], check=True)  # noqa: S603 -- fixed test command


def _assert_common_discovery_contract(script_text: str) -> None:
    assert "infer_from_workshop_location" in script_text
    assert "infer_from_installed_mod_location" in script_text
    assert "steamapps/workshop/content/333640/3718988020" in script_text
    assert "CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP" in script_text
    assert "Library/Application Support/Steam/steamapps/common/Caves of Qud" in script_text
    assert "CavesOfQud-stable-ref" not in script_text
    assert "choose file" in script_text
    assert "Managed/0Harmony.dll" in script_text
    assert '[[ "${target}" == *"${HARMONY_APP_SUFFIX}" ]]' in script_text


def _assert_in_order(text: str, *needles: str) -> None:
    position = -1
    for needle in needles:
        next_position = text.find(needle, position + 1)
        assert next_position != -1, needle
        position = next_position


def test_native_harmony_install_is_explicit_opt_in_and_restorable() -> None:
    """The installer must never silently replace the game Harmony DLL."""
    installer = _script_text(_INSTALL_SCRIPT)

    assert "display dialog" in installer
    assert "Harmony 2.4.2" in installer
    assert "0Harmony.dll.qudjp-backup-before-2.4.2" in installer
    assert "Harmony-Fat.2.4.2.0.zip" in installer
    assert "77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d" in installer
    assert "curl --proto '=https' --tlsv1.2" in installer
    assert "--retry 3 --retry-delay 2 --retry-all-errors" in installer
    assert "--connect-timeout 10 --max-time 180" in installer
    assert "unzip" in installer
    assert "CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll" in installer
    assert "Restore Game Harmony.command" in installer


def test_native_harmony_install_infers_target_or_prompts_for_manual_selection() -> None:
    """The installer mirrors the Rosetta launcher's self-location first workflow."""
    installer = _script_text(_INSTALL_SCRIPT)

    _assert_common_discovery_contract(installer)


def test_native_harmony_install_confirms_before_mutating_game_dll() -> None:
    """The installer confirms, backs up, verifies, and only then replaces the DLL."""
    installer = _script_text(_INSTALL_SCRIPT)

    _assert_in_order(
        installer,
        'confirm_install "${target}"',
        'cp "${target}" "${backup}"',
        'new_dll="$(download_harmony "${temp_dir}")"',
        'cp "${new_dll}" "${target}"',
    )


def test_native_harmony_restore_uses_only_the_qudjp_backup() -> None:
    """The restore helper must restore from QudJP's named backup file only."""
    restorer = _script_text(_RESTORE_SCRIPT)

    assert "display dialog" in restorer
    assert "0Harmony.dll.qudjp-backup-before-2.4.2" in restorer
    assert "CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll" in restorer
    assert "cp " in restorer
    assert "curl" not in restorer
    assert "github.com/pardeike/Harmony" not in restorer


def test_native_harmony_restore_infers_target_or_prompts_for_manual_selection() -> None:
    """The restore helper uses the same discovery and manual fallback path."""
    restorer = _script_text(_RESTORE_SCRIPT)

    _assert_common_discovery_contract(restorer)


def test_native_harmony_restore_confirms_and_uses_only_the_named_backup() -> None:
    """The restore helper confirms after checking the QudJP backup path."""
    restorer = _script_text(_RESTORE_SCRIPT)

    _assert_in_order(
        restorer,
        'local backup="${target%0Harmony.dll}${BACKUP_NAME}"',
        '[[ -f "${backup}" ]] || fail',
        'confirm_restore "${target}" "${backup}"',
        'cp "${backup}" "${target}"',
    )
