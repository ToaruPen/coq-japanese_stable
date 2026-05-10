"""Static contracts for parallel-safe dotnet/Roslyn test execution."""

from __future__ import annotations

import os
import re
import subprocess
import time
from pathlib import Path

import pytest

from scripts import dotnet_tool_runner

REPO_ROOT = Path(__file__).resolve().parents[2]


def _read_repo_file(path: str) -> str:
    return (REPO_ROOT / path).read_text(encoding="utf-8")


def _recipe_block(justfile: str, recipe_name: str) -> str:
    escaped_name = re.escape(recipe_name)
    start_match = re.search(rf"(?m)^{escaped_name}(?:[ \t][^:\n]*)?:", justfile)
    assert start_match is not None, f"recipe not found: {recipe_name}"
    next_match = re.search(r"(?m)^[A-Za-z0-9_-]+(?:[ \t][^:\n]*)?:", justfile[start_match.end() :])
    end = len(justfile) if next_match is None else start_match.end() + next_match.start()
    return justfile[start_match.start() : end]


def _probe_lock_path(tmp_path: Path) -> Path:
    return tmp_path / "locks" / "Probe.lock"


def _age_path(path: Path) -> None:
    stale_time = time.time() - dotnet_tool_runner.BUILD_LOCK_OWNER_GRACE_SECONDS - 1.0
    os.utime(path, (stale_time, stale_time))


def test_category_test_recipes_use_isolated_artifacts_and_test_built_dll() -> None:
    """L1/L2/L2G recipes must be safe to launch from separate shells in parallel."""
    justfile = "\n" + _read_repo_file("justfile")
    artifacts_root_definition = (
        'dotnet_artifacts_root := env_var_or_default("QUDJP_DOTNET_ARTIFACTS_ROOT", ".artifacts/dotnet")'
    )

    for recipe_name, category in (("test-l1", "L1"), ("test-l2", "L2"), ("test-l2g", "L2G")):
        block = _recipe_block(justfile, recipe_name)
        assert artifacts_root_definition in justfile
        assert f"{{{{dotnet_artifacts_root}}}}/{recipe_name}" in block
        assert "--artifacts-path" in block
        assert "--no-dependencies" in block
        assert "QudJP.Tests.dll" in block
        assert "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj" not in block
        assert f"TestCategory={category}" in block


def test_roslyn_tool_wrappers_use_run_scoped_dotnet_runner() -> None:
    """Python Roslyn wrappers should use the shared run-scoped dotnet helper."""
    for path in (
        "scripts/extract_annals_patterns.py",
        "scripts/roslyn_semantic_probe.py",
        "scripts/scan_static_producer_inventory.py",
    ):
        text = _read_repo_file(path)
        assert "run_tool_project" in text

    helper = _read_repo_file("scripts/dotnet_tool_runner.py")
    assert "--artifacts-path" in helper
    assert "TemporaryDirectory" in helper
    assert "tool-runs" in helper
    assert "owner.pid" in helper
    assert "_remove_stale_lock" in helper


def test_text_construction_recipe_runs_prebuilt_tool_dll() -> None:
    """The text construction just recipe should build then execute the DLL directly."""
    justfile = "\n" + _read_repo_file("justfile")
    block = _recipe_block(justfile, "text-construction-inventory")

    assert "dotnet build scripts/tools/TextConstructionInventory/TextConstructionInventory.csproj" in block
    assert "--artifacts-path" in block
    assert 'dotnet "$tool_dll"' in block


def test_ci_dotnet_uses_run_scoped_artifacts_and_serializes_production_build() -> None:
    """The CI-like local recipe must not delete or share another run's test artifacts."""
    justfile = "\n" + _read_repo_file("justfile")
    block = _recipe_block(justfile, "ci-dotnet")

    assert 'mktemp -d "{{dotnet_artifacts_root}}/ci-dotnet/run.XXXXXX"' in block
    assert 'rm -rf "$ci_root"' not in block
    assert "qudjp-csproj.lock" in block
    assert "owner.pid" in block
    assert "remove_stale_lock" in block
    assert "lock_owner_grace_seconds=2" in block
    assert "is_stale_path" in block
    assert "lock_timeout_seconds=600" in block
    assert "timed out waiting for dotnet build lock" in block
    assert "lock_acquired=1" in block
    assert "QudJP.Tests.dll" in block


def test_roslyn_build_recipes_use_run_scoped_artifacts() -> None:
    """Roslyn build validation recipes should be safe to run concurrently."""
    justfile = "\n" + _read_repo_file("justfile")

    for recipe_name in (
        "roslyn-build-annals",
        "roslyn-build-static-producer",
        "roslyn-build-semantic-probe",
        "roslyn-build-text-construction",
    ):
        block = _recipe_block(justfile, recipe_name)
        assert "mktemp -d" in block
        assert '--artifacts-path "$artifacts_root"' in block
        assert "trap 'rm -rf \"$artifacts_root\"' EXIT" in block


def test_roslyn_python_check_covers_shared_dotnet_runner() -> None:
    """The shared Python runner must stay inside the Roslyn lint and type gates."""
    justfile = "\n" + _read_repo_file("justfile")
    block = _recipe_block(justfile, "roslyn-python-check")

    assert "ruff check scripts/dotnet_tool_runner.py" in block
    assert "uvx basedpyright scripts/dotnet_tool_runner.py" in block


def test_dotnet_build_lock_recovers_non_directory_lock_artifact(tmp_path: Path) -> None:
    """Non-directory lock artifacts should not block the next build."""
    lock_path = _probe_lock_path(tmp_path)
    lock_path.parent.mkdir()
    _ = lock_path.write_text("", encoding="utf-8")

    with dotnet_tool_runner._build_lock(tmp_path, "Probe"):  # noqa: SLF001
        assert (lock_path / "owner.pid").is_file()

    assert not lock_path.exists()


def test_dotnet_build_lock_preserves_fresh_pre_owner_lock(tmp_path: Path) -> None:
    """A fresh lock directory without owner metadata may belong to an active writer."""
    lock_path = _probe_lock_path(tmp_path)
    lock_path.mkdir(parents=True)

    assert not dotnet_tool_runner._remove_stale_lock(lock_path)  # noqa: SLF001
    assert lock_path.is_dir()


def test_dotnet_build_lock_recovers_aged_pre_owner_lock(tmp_path: Path) -> None:
    """A lock directory without owner metadata is stale only after the grace period."""
    lock_path = _probe_lock_path(tmp_path)
    lock_path.mkdir(parents=True)
    _age_path(lock_path)

    assert dotnet_tool_runner._remove_stale_lock(lock_path)  # noqa: SLF001
    assert not lock_path.exists()


def test_dotnet_build_lock_preserves_fresh_malformed_owner(tmp_path: Path) -> None:
    """A fresh malformed owner file may be a writer truncation window."""
    lock_path = _probe_lock_path(tmp_path)
    lock_path.mkdir(parents=True)
    owner_path = lock_path / "owner.pid"
    _ = owner_path.write_text("", encoding="utf-8")

    assert not dotnet_tool_runner._remove_stale_lock(lock_path)  # noqa: SLF001
    assert lock_path.is_dir()


def test_dotnet_build_lock_recovers_aged_malformed_owner(tmp_path: Path) -> None:
    """Malformed owner metadata is stale only after the grace period."""
    lock_path = _probe_lock_path(tmp_path)
    lock_path.mkdir(parents=True)
    owner_path = lock_path / "owner.pid"
    _ = owner_path.write_text("", encoding="utf-8")
    _age_path(owner_path)

    assert dotnet_tool_runner._remove_stale_lock(lock_path)  # noqa: SLF001
    assert not lock_path.exists()


def test_dotnet_build_lock_recovers_dead_owner(tmp_path: Path) -> None:
    """Directory locks with a dead owner PID should be reclaimed."""
    lock_path = _probe_lock_path(tmp_path)
    lock_path.mkdir(parents=True)
    _ = (lock_path / "owner.pid").write_text("0\n", encoding="utf-8")

    with dotnet_tool_runner._build_lock(tmp_path, "Probe"):  # noqa: SLF001
        assert (lock_path / "owner.pid").is_file()

    assert not lock_path.exists()


def test_run_tool_project_applies_timeout_to_build(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Wrapper timeout should include the build phase, not only DLL execution."""
    project_path = tmp_path / "Probe.csproj"
    _ = project_path.write_text(
        "<Project><PropertyGroup><AssemblyName>Probe</AssemblyName></PropertyGroup></Project>\n",
        encoding="utf-8",
    )
    monkeypatch.setattr(dotnet_tool_runner, "_dotnet_path", lambda: "dotnet")
    monkeypatch.setattr(dotnet_tool_runner, "_artifacts_root", lambda: tmp_path / "artifacts")

    def fake_run(
        command: list[str],
        *,
        capture_output: bool,
        text: bool,
        check: bool,
        timeout: float,
    ) -> subprocess.CompletedProcess[str]:
        assert capture_output
        assert text
        assert not check
        assert timeout == 3
        raise subprocess.TimeoutExpired(command, timeout, output="partial stdout", stderr="partial stderr")

    monkeypatch.setattr(subprocess, "run", fake_run)

    with pytest.raises(dotnet_tool_runner.DotnetToolError) as exc_info:
        dotnet_tool_runner.run_tool_project(project_path, [], timeout=3)

    message = str(exc_info.value)
    assert "dotnet build timed out after 3s" in message
    assert project_path.as_posix() in message
    assert "partial stdout" in message
    assert "partial stderr" in message


def test_parallel_dotnet_policy_is_documented() -> None:
    """Docs should name the artifact-root knob used by local parallel runs."""
    combined_docs = "\n".join(
        [
            _read_repo_file("docs/test-architecture.md"),
            _read_repo_file("scripts/README.md"),
            _read_repo_file("scripts/AGENTS.md"),
        ]
    )

    assert "QUDJP_DOTNET_ARTIFACTS_ROOT" in combined_docs
    assert "parallel" in combined_docs
