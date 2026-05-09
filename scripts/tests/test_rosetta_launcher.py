"""Tests for the player-facing macOS Rosetta launcher."""

from __future__ import annotations

import subprocess
from pathlib import Path

_LAUNCHER_PATH = Path("Mods/QudJP/Launch CavesOfQud (Rosetta).command")


def _launcher_text() -> str:
    return _LAUNCHER_PATH.read_text(encoding="utf-8")


def test_rosetta_launcher_is_valid_bash() -> None:
    """The distributed launcher remains valid for macOS /bin/bash."""
    subprocess.run(["/bin/bash", "-n", str(_LAUNCHER_PATH)], check=True)  # noqa: S603 -- fixed test command


def test_rosetta_launcher_uses_gui_dialogs_for_player_facing_errors() -> None:
    """The launcher guides non-technical macOS users through Finder dialogs."""
    launcher = _launcher_text()

    assert "display dialog" in launcher
    assert "choose file" in launcher
    assert "この起動ファイルの場所から推定したSteamライブラリ" in launcher
    assert "CoQ.app/Contents/MacOS/CoQ" in launcher
    assert "edit this launcher" not in launcher
    assert "ゲームファイルの整合性を確認" in launcher


def test_rosetta_launcher_restricts_manual_selection_to_coq_binary() -> None:
    """Manual file picker results must still resolve to the CoQ app binary."""
    launcher = _launcher_text()

    assert "canonicalize_binary_path" in launcher
    assert "while [[ -L" in launcher
    assert "readlink" in launcher
    assert '[[ "${canonical_chosen}" != */CoQ.app/Contents/MacOS/CoQ ]]' in launcher
    assert '[[ ! -x "${canonical_chosen}" ]]' in launcher
    assert 'printf \'%s\\n\' "${canonical_chosen}"' in launcher


def test_rosetta_launcher_resolves_final_binary_symlink(tmp_path: Path) -> None:
    """The manual selection guard resolves a symlink at the final CoQ component."""
    launcher = _launcher_text()
    start = launcher.index("canonicalize_binary_path() {")
    end = launcher.index("\n}\n\nresolve_game_binary", start) + len("\n}")
    function_source = launcher[start:end]

    macos_dir = tmp_path / "Caves of Qud" / "CoQ.app" / "Contents" / "MacOS"
    macos_dir.mkdir(parents=True)
    target_dir = tmp_path / "elsewhere"
    target_dir.mkdir()
    target_binary = target_dir / "NotCoQ"
    target_binary.write_text("#!/bin/sh\n", encoding="utf-8")
    target_binary.chmod(0o755)
    coq_link = macos_dir / "CoQ"
    coq_link.symlink_to(target_binary)

    result = subprocess.run(  # noqa: S603 -- fixed test command with a temporary path argument
        ["/bin/bash", "-c", f'{function_source}\ncanonicalize_binary_path "$1"\n', "bash", str(coq_link)],
        check=True,
        capture_output=True,
        text=True,
    )

    assert result.stdout.strip() == str(target_binary)


def test_rosetta_launcher_infers_game_from_workshop_or_installed_mod_location() -> None:
    """The launcher tries the Steam library implied by its own installed location."""
    launcher = _launcher_text()

    assert "infer_game_binary_from_launcher_location" in launcher
    assert "steamapps/workshop/content/333640/3718988020" in launcher
    assert "common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ" in launcher
    assert "CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP" in launcher


def test_rosetta_launcher_can_offer_rosetta_install_through_dialog() -> None:
    """Missing Rosetta is handled through the launcher, not only a printed command."""
    launcher = _launcher_text()

    assert 'LAUNCHER_TITLE="QudJP Rosetta 起動"' in launcher
    assert "softwareupdate --install-rosetta --agree-to-license" in launcher
    assert "Rosetta 2をインストール" in launcher
    assert "キャンセル" in launcher
    assert "exec arch -x86_64" in launcher
