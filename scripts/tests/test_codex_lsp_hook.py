"""Tests for the repo-local Codex LSP hook."""

from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
HOOK = REPO_ROOT / ".codex" / "hooks" / "lsp-check-after-tool.sh"
HOOKS_JSON = REPO_ROOT / ".codex" / "hooks.json"


def _run_hook(payload: dict[str, object], **env_overrides: str) -> subprocess.CompletedProcess[str]:
    env = {
        **os.environ,
        "QUDJP_CODEX_LSP_HOOK_DRY_RUN": "1",
        "QUDJP_CODEX_LSP_HOOK_MIN_INTERVAL_SECONDS": "0",
        **env_overrides,
    }
    return subprocess.run(  # noqa: S603 -- test invokes the repo-local hook via bash
        ["bash", str(HOOK)],  # noqa: S607
        input=json.dumps(payload),
        capture_output=True,
        text=True,
        cwd=REPO_ROOT,
        env=env,
        check=False,
    )


def test_codex_hooks_register_lsp_post_tool_use_hook() -> None:
    """The project hook should be wired through Codex's PostToolUse hook format."""
    config = json.loads(HOOKS_JSON.read_text())
    post_tool_use = config["hooks"]["PostToolUse"][0]

    assert post_tool_use["matcher"] == ".*"
    assert post_tool_use["hooks"][0]["type"] == "command"
    assert post_tool_use["hooks"][0]["command"] == "bash .codex/hooks/lsp-check-after-tool.sh"
    assert post_tool_use["hooks"][0]["timeout"] >= 120


def test_hook_runs_lsp_for_relevant_csharp_write() -> None:
    """C# edits should trigger the LSP route, subject to the hook's debounce guard."""
    completed = _run_hook(
        {
            "tool_name": "apply_patch",
            "tool_input": {
                "patch": "*** Update File: Mods/QudJP/Assemblies/src/Patches/Foo.cs\n"
            },
        }
    )

    assert completed.returncode == 0, completed.stderr
    assert "would run just lsp-check" in completed.stderr


def test_hook_skips_plain_csharp_reads_by_default() -> None:
    """Read-only C# inspection should not pay the LSP cost unless explicitly requested."""
    completed = _run_hook(
        {
            "tool_name": "Read",
            "tool_input": {"file_path": "Mods/QudJP/Assemblies/src/Patches/Foo.cs"},
        }
    )

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout == ""
    assert completed.stderr == ""


def test_hook_can_opt_into_plain_csharp_reads() -> None:
    """Read-only C# inspection can opt into the LSP route when needed."""
    completed = _run_hook(
        {
            "tool_name": "Read",
            "tool_input": {"file_path": "Mods/QudJP/Assemblies/src/Patches/Foo.cs"},
        },
        QUDJP_CODEX_LSP_HOOK_ON_READ="1",
    )

    assert completed.returncode == 0, completed.stderr
    assert "would run just lsp-check" in completed.stderr


def test_hook_skips_non_csharp_changes() -> None:
    """Non-C# tool use should stay silent."""
    completed = _run_hook(
        {
            "tool_name": "apply_patch",
            "tool_input": {"patch": "*** Update File: docs/contributing.md\n"},
        }
    )

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout == ""
    assert completed.stderr == ""


def test_hook_skips_exec_command_csharp_reads_by_default() -> None:
    """Shell-based C# reads should not trigger the LSP route unless explicitly opted in."""
    completed = _run_hook(
        {
            "tool_name": "exec_command",
            "tool_input": {"cmd": "sed -n '1,80p' Mods/QudJP/Assemblies/src/Patches/Foo.cs"},
        }
    )

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout == ""
    assert completed.stderr == ""


def test_hook_can_opt_into_codex_bash_tool_csharp_commands() -> None:
    """Codex shell hook payloads may use Bash as the shell tool name."""
    completed = _run_hook(
        {
            "tool_name": "Bash",
            "tool_input": {"command": "sed -n '1,80p' Mods/QudJP/Assemblies/src/Patches/Foo.cs"},
        },
        QUDJP_CODEX_LSP_HOOK_ON_EXEC="1",
    )

    assert completed.returncode == 0, completed.stderr
    assert "would run just lsp-check" in completed.stderr
