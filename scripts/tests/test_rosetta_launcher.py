"""Tests for the player-facing macOS Rosetta launcher."""

from __future__ import annotations

from pathlib import Path


def _launcher_text() -> str:
    return Path("Mods/QudJP/Launch CavesOfQud (Rosetta).command").read_text(encoding="utf-8")


def test_rosetta_launcher_uses_gui_dialogs_for_player_facing_errors() -> None:
    """The launcher guides non-technical macOS users through Finder dialogs."""
    launcher = _launcher_text()

    assert "display dialog" in launcher
    assert "choose file" in launcher
    assert "CoQ.app/Contents/MacOS/CoQ" in launcher
    assert "edit this launcher" not in launcher


def test_rosetta_launcher_can_offer_rosetta_install_without_manual_terminal_command() -> None:
    """Missing Rosetta is handled through the launcher, not only a printed command."""
    launcher = _launcher_text()

    assert "softwareupdate --install-rosetta --agree-to-license" in launcher
    assert "Install Rosetta 2" in launcher
    assert "exec arch -x86_64" in launcher
