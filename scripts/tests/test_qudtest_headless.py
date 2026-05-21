"""Tests for the headless QudTest artifact runner."""

# ruff: noqa: S603,S607 -- tests invoke PATH-resolved repo-local tools.

from __future__ import annotations

import json
import subprocess
from pathlib import Path

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


def test_qudtest_headless_writes_inspectable_binding_artifact(tmp_path: Path) -> None:
    """The headless runner should validate patch target bindings without opening the game UI."""
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
