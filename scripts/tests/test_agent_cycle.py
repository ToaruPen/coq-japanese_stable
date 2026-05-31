"""Tests for scripts/agent_cycle.sh."""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
AGENT_CYCLE = REPO_ROOT / "scripts" / "agent_cycle.sh"
NODE_BIN = REPO_ROOT / "node_modules" / ".bin"


def _tool_available(name: str) -> bool:
    return shutil.which(name) is not None or (NODE_BIN / name).exists()


def _run_agent_cycle(
    *args: str,
    python_bin: str | None = None,
    override_path: str | None = None,
    without_dotfiles_root: bool = False,
) -> subprocess.CompletedProcess[str]:
    env = {**os.environ}
    path = override_path if override_path is not None else env["PATH"]
    env["PATH"] = f"{NODE_BIN}{os.pathsep}{path}"
    if python_bin is not None:
        env["PYTHON_BIN"] = python_bin
    if without_dotfiles_root:
        env.pop("DOTFILES_ROOT", None)
    return subprocess.run(  # noqa: S603 -- tests invoke the repo-local shell script via bash
        ["/bin/bash", str(AGENT_CYCLE), *args],
        capture_output=True,
        text=True,
        cwd=REPO_ROOT,
        env=env,
        check=False,
    )


@pytest.mark.skipif(not _tool_available("ast-grep"), reason="ast-grep CLI not available")
def test_ast_grep_smoke_finds_static_producer_fixture() -> None:
    """The agent-cycle ast-grep smoke must prove structural search actually works."""
    completed = _run_agent_cycle("ast-grep-smoke")

    assert completed.returncode == 0, completed.stderr
    assert "Demo/StaticProducerCases.cs:25:" in completed.stdout
    assert 'Popup.Show("Popup body", Title: "Ignored title")' in completed.stdout


@pytest.mark.skipif(not _tool_available("ast-grep"), reason="ast-grep CLI not available")
def test_ast_grep_check_reports_empty_rule_set_and_runs_smoke() -> None:
    """An empty rule directory should be explicit, not a silent 0-test pass."""
    rule_files = [
        *list((REPO_ROOT / "rules").rglob("*.yml")),
        *list((REPO_ROOT / "rules").rglob("*.yaml")),
        *list((REPO_ROOT / "rule-tests").rglob("*.yml")),
        *list((REPO_ROOT / "rule-tests").rglob("*.yaml")),
    ]
    if rule_files:
        pytest.skip("project ast-grep rules are registered")

    completed = _run_agent_cycle("ast-grep-check")

    assert completed.returncode == 0, completed.stderr
    assert "No project ast-grep rules registered" in completed.stdout
    assert "Demo/StaticProducerCases.cs:25:" in completed.stdout


def test_render_skill_evals_supports_repo_local_skills_without_dotfiles_root() -> None:
    """Repo-local skill eval rendering should not require the dotfiles checkout."""
    completed = _run_agent_cycle(
        "render-skill-evals",
        "roslyn-static-analysis",
        "median-popup-show-owner-route",
        python_bin=sys.executable,
        without_dotfiles_root=True,
    )

    assert completed.returncode == 0, completed.stderr
    assert "Path: `.codex/skills/roslyn-static-analysis/SKILL.md`" in completed.stdout
    assert "Scenario: `median-popup-show-owner-route`" in completed.stdout


@pytest.mark.skipif(
    not _tool_available("ast-grep") or not shutil.which("just") or not shutil.which("dotnet"),
    reason="agent tools not available",
)
def test_tool_check_allows_repo_local_render_without_dotfiles_root() -> None:
    """tool-check should not mark repo-local skill eval rendering unhealthy."""
    completed = _run_agent_cycle("tool-check", python_bin=sys.executable, without_dotfiles_root=True)

    assert completed.returncode == 0, completed.stderr
    assert "DOTFILES_ROOT not set" in completed.stdout


def test_tool_check_uses_uv_python_by_default(tmp_path: Path) -> None:
    """tool-check should not require a host-level python3.12 executable by default."""
    bin_dir = tmp_path / "bin"
    bin_dir.mkdir()
    for tool in ("just", "uv", "ast-grep"):
        _write_executable_stub(bin_dir, tool)

    completed = _run_agent_cycle(
        "tool-check",
        override_path=str(bin_dir),
        without_dotfiles_root=True,
    )

    assert completed.returncode == 1
    assert "missing tool: python3.12" not in completed.stderr
    assert "missing tool: dotnet" in completed.stderr
    assert str(bin_dir / "uv") in completed.stdout


def _write_executable_stub(bin_dir: Path, name: str) -> None:
    stub = bin_dir / name
    stub.write_text("#!/bin/sh\nprintf '%s version\\n' \"$0\"\n", encoding="utf-8")
    stub.chmod(0o755)


def test_tool_check_reports_missing_dotnet_before_restore(tmp_path: Path) -> None:
    """tool-check should distinguish a missing dotnet executable from restore failure."""
    bin_dir = tmp_path / "bin"
    bin_dir.mkdir()
    for tool in ("just", "python3.12", "ast-grep"):
        _write_executable_stub(bin_dir, tool)

    completed = _run_agent_cycle(
        "tool-check",
        python_bin="python3.12",
        override_path=str(bin_dir),
        without_dotfiles_root=True,
    )

    assert completed.returncode == 1
    assert "missing tool: dotnet" in completed.stderr
    assert "dotnet tool restore failed" not in completed.stderr
