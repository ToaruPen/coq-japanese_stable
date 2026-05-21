"""Contract tests for QudTest's game-compiled wish bridge."""

from __future__ import annotations

from pathlib import Path

BOOTSTRAP = Path("Mods/QudJP/Bootstrap.cs")


def test_bootstrap_exposes_qudtest_wish_commands_from_game_compiled_source() -> None:
    """WishManager discovers Bootstrap.cs commands before the loaded QudJP.dll."""
    source = BOOTSTRAP.read_text(encoding="utf-8")

    assert "[HasWishCommand]" in source
    assert '[WishCommand("qudtest", null)]' in source
    assert '[WishCommand("qudtest:all", null)]' in source
    assert '[WishCommand("qudtest:runtime", null)]' in source
    assert '[WishCommand("qudtest:wish", null)]' in source
    assert '[WishCommand("qudtest:bindings", null)]' in source
    assert '[WishCommand("qudtest:bindings-all", null)]' in source


def test_bootstrap_bridge_delegates_qudtest_to_loaded_dll_entrypoint() -> None:
    """The bridge must not duplicate runtime logic inside Bootstrap.cs."""
    source = BOOTSTRAP.read_text(encoding="utf-8")

    assert 'GetType("QudJP.QudTest.QudTestRuntimeEntrypoint")' in source
    assert 'GetMethod("Run", BindingFlags.Public | BindingFlags.Static)' in source
    assert "qudTestRunMethod.Invoke(null, new object[] { command });" in source
