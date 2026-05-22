"""Tests for the headless QudTest artifact runner."""

# ruff: noqa: S603,S607 -- tests invoke PATH-resolved repo-local tools.

from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path

import pytest

PROJECT = Path("scripts/tools/QudTestHeadless/QudTestHeadless.csproj")
FIXTURES = Path("Mods/QudJP/QudTest/fixtures")
INSPECTOR = Path("scripts/qudtest_inspect.py")


def test_qudtest_headless_writes_inspectable_runtime_artifact(tmp_path: Path) -> None:
    """The headless runner should execute repository fixtures without opening the game UI."""
    output = tmp_path / "QudTest"

    completed = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROJECT),
            "--",
            "--command",
            "qudtest:runtime",
            "--fixtures",
            str(FIXTURES),
            "--output",
            str(output),
            "--mod-language",
            "ja",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )

    assert completed.returncode == 0, completed.stderr
    results = output / "results.json"
    assert results.exists()

    payload = json.loads(results.read_text(encoding="utf-8"))
    assert payload["suite"] == "runtime"
    assert payload["modLanguage"] == "ja"
    assert payload["passed"] is True
    assert payload["totalCount"] > 0
    inventory_case = next(
        case for case in payload["cases"] if case["id"] == "inventory-display-name.game-object-colored-state"
    )
    assert inventory_case["colorShape"]["producer"] == "QudTest.InventoryDisplayNameFixture"
    assert inventory_case["colorShape"]["sourceVisible"] == "copper nugget [empty]"
    assert inventory_case["colorShape"]["finalVisible"] == "銅塊 [空]"
    assert inventory_case["colorShape"]["sourceColorSpans"]
    assert inventory_case["colorShape"]["finalColorSpans"]

    inspected = subprocess.run(
        [
            "uv",
            "run",
            "python",
            str(INSPECTOR),
            "--fixtures",
            str(FIXTURES),
            "--results",
            str(results),
            "--skip-player-log",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=60,
    )

    assert inspected.returncode == 0, inspected.stderr
    assert "QudTest passed" in inspected.stdout


def test_qudtest_headless_captures_inventory_shape_from_game_object(tmp_path: Path) -> None:
    """Inventory shape capture should record the GameObject-produced display name."""
    _skip_without_game_managed_dir()
    output = tmp_path / "QudTest"

    completed = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROJECT),
            "--",
            "--command",
            "qudtest:inventory-shapes",
            "--fixtures",
            str(FIXTURES),
            "--output",
            str(output),
            "--mod-language",
            "ja",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )

    assert completed.returncode == 0, completed.stderr
    results = output / "results.json"
    payload = json.loads(results.read_text(encoding="utf-8"))
    assert payload["suite"] == "inventory-shapes"
    assert payload["modLanguage"] == "ja"
    assert payload["passed"] is True
    assert all(case["colorShape"]["markupSemanticStatus"] == "clean" for case in payload["cases"])
    inventory_case = next(
        case for case in payload["cases"] if case["id"] == "inventory-display-name-game-object.copper-nugget"
    )
    assert inventory_case["input"] == "Copper Nugget"
    assert inventory_case["actual"] == "{{w|銅のナゲット}}"
    assert inventory_case["colorShape"]["producer"] == "InventoryLine.GameObjectDisplayName"
    assert inventory_case["colorShape"]["source"] == "{{w|銅のナゲット}}"
    assert inventory_case["colorShape"]["sourceVisible"] == "銅のナゲット"
    assert inventory_case["colorShape"]["final"] == "{{w|銅のナゲット}}"
    assert inventory_case["colorShape"]["finalVisible"] == "銅のナゲット"
    assert inventory_case["colorShape"]["sourceColorSpans"]
    assert inventory_case["colorShape"]["finalColorSpans"]
    assert inventory_case["colorShape"]["markupSemanticStatus"] == "clean"

    inline_case = next(
        case for case in payload["cases"] if case["id"] == "inventory-display-name-game-object.grit-gate-recoiler"
    )
    assert inline_case["input"] == "Grit Gate Recoiler"
    assert inline_case["actual"] == "{{c|グリット・ゲート}}リコイラー"
    assert inline_case["colorShape"]["source"] == "{{c|グリット・ゲート}}リコイラー"
    assert inline_case["colorShape"]["sourceVisible"] == "グリット・ゲートリコイラー"
    assert inline_case["colorShape"]["finalVisible"] == "グリット・ゲートリコイラー"
    assert inline_case["colorShape"]["sourceColorSpans"] == "0:{{c||8:}}"
    assert inline_case["colorShape"]["finalColorSpans"] == "0:{{c||8:}}"

    long_chain_case = next(
        case for case in payload["cases"] if case["id"] == "inventory-display-name.producer-derived-long-prefix-chain"
    )
    assert long_chain_case["actual"] == long_chain_case["expected"]
    assert long_chain_case["colorShape"]["markupSemanticStatus"] == "clean"
    assert "}}{{" not in long_chain_case["actual"]
    assert "{{freezing|凍結した}} {{painted|彩色された}}" in long_chain_case["actual"]
    assert "{{lacquered|漆仕上げ}} {{phase-harmonic|位相調和}}" in long_chain_case["actual"]

    bracketed_prefix_case = next(
        case for case in payload["cases"] if case["id"] == "inventory-display-name.producer-derived-bracketed-prefix"
    )
    assert bracketed_prefix_case["actual"] == bracketed_prefix_case["expected"]
    assert bracketed_prefix_case["colorShape"]["markupSemanticStatus"] == "clean"
    assert "illuminated" not in bracketed_prefix_case["colorShape"]["finalVisible"]


def test_qudtest_headless_writes_inspectable_binding_artifact(tmp_path: Path) -> None:
    """The headless runner should validate patch target bindings without opening the game UI."""
    _skip_without_game_managed_dir()
    output = tmp_path / "QudTest"

    completed = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROJECT),
            "--",
            "--command",
            "qudtest:bindings",
            "--fixtures",
            str(FIXTURES),
            "--output",
            str(output),
            "--mod-language",
            "ja",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )

    assert completed.returncode == 0, completed.stderr
    results = output / "results.json"
    assert results.exists()

    payload = json.loads(results.read_text(encoding="utf-8"))
    assert payload["suite"] == "bindings"
    assert payload["modLanguage"] == "ja"
    assert payload["passed"] is True
    assert payload["totalCount"] > 0

    inspected = subprocess.run(
        [
            "uv",
            "run",
            "python",
            str(INSPECTOR),
            "--fixtures",
            str(FIXTURES),
            "--results",
            str(results),
            "--skip-player-log",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=60,
    )

    assert inspected.returncode == 0, inspected.stderr
    assert "QudTest passed" in inspected.stdout


def test_qudtest_headless_writes_inspectable_all_patch_binding_artifact(tmp_path: Path) -> None:
    """The dynamic binding suite should resolve every patch TargetMethod(s) entrypoint."""
    _skip_without_game_managed_dir()
    output = tmp_path / "QudTest"

    completed = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROJECT),
            "--",
            "--command",
            "qudtest:bindings-all",
            "--fixtures",
            str(FIXTURES),
            "--output",
            str(output),
            "--mod-language",
            "ja",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )

    assert completed.returncode == 0, completed.stderr
    results = output / "results.json"
    payload = json.loads(results.read_text(encoding="utf-8"))
    assert payload["suite"] == "bindings-all"
    assert payload["modLanguage"] == "ja"
    assert payload["passed"] is True
    assert payload["totalCount"] > 100

    inspected = subprocess.run(
        [
            "uv",
            "run",
            "python",
            str(INSPECTOR),
            "--fixtures",
            str(FIXTURES),
            "--results",
            str(results),
            "--skip-player-log",
        ],
        check=False,
        capture_output=True,
        text=True,
        timeout=60,
    )

    assert inspected.returncode == 0, inspected.stderr
    assert "QudTest passed" in inspected.stdout


def _skip_without_game_managed_dir() -> None:
    if _has_game_managed_dir():
        return
    pytest.skip("QudTest patch-binding headless checks require Caves of Qud managed DLLs.")


def _has_game_managed_dir() -> bool:
    env_dir = os.environ.get("COQ_MANAGED_DIR")
    candidates = []
    if env_dir:
        candidates.append(Path(env_dir))
    candidates.append(Path.home() / "Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/Managed")
    return any((candidate / "Assembly-CSharp.dll").exists() for candidate in candidates)
