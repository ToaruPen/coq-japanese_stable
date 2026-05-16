"""Static contracts for parallel-safe dotnet/Roslyn test execution."""
# pyright: reportPrivateUsage=false

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


def _write_probe_project(tmp_path: Path) -> Path:
    project_path = tmp_path / "Probe.csproj"
    _ = project_path.write_text(
        "<Project><PropertyGroup><AssemblyName>Probe</AssemblyName></PropertyGroup></Project>\n",
        encoding="utf-8",
    )
    return project_path


def _write_cached_probe_dll(artifacts_root: Path, project_path: Path) -> Path:
    dll = artifacts_root / "bin" / "Probe" / "release" / "Probe.dll"
    dll.parent.mkdir(parents=True)
    _ = dll.write_text("", encoding="utf-8")
    stamp = dotnet_tool_runner._tool_sources_stamp_path(dll)  # noqa: SLF001
    _ = stamp.write_text(dotnet_tool_runner._tool_sources_fingerprint(project_path), encoding="utf-8")  # noqa: SLF001
    return dll


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
        assert "{{dotnet_test_build_properties}}" in block
        assert "QudJP.Tests.dll" in block
        assert "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj" not in block
        assert f"TestCategory={category}" in block


def test_roslyn_tool_wrappers_use_run_scoped_dotnet_runner() -> None:
    """Python Roslyn wrappers should use the shared cached dotnet helper."""
    for path in (
        "scripts/extract_annals_patterns.py",
        "scripts/roslyn_semantic_probe.py",
        "scripts/scan_static_producer_inventory.py",
    ):
        text = _read_repo_file(path)
        assert "run_cached_tool_project" in text

    helper = _read_repo_file("scripts/dotnet_tool_runner.py")
    assert "--artifacts-path" in helper
    assert "TemporaryDirectory" in helper
    assert "tool-runs" in helper
    assert "run_cached_tool_project" in helper
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
    assert block.index("stat -c %Y") < block.index("stat -f %m")
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
        _ = dotnet_tool_runner.run_tool_project(project_path, [], timeout=3)

    message = str(exc_info.value)
    assert "dotnet build timed out after 3s" in message
    assert project_path.as_posix() in message
    assert "partial stdout" in message
    assert "partial stderr" in message


def test_run_cached_tool_project_reuses_shared_artifacts_root(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Cached wrapper runs should rebuild incrementally in the shared artifacts root."""
    project_path = _write_probe_project(tmp_path)
    artifacts_root = tmp_path / "artifacts"
    monkeypatch.setattr(dotnet_tool_runner, "_dotnet_path", lambda: "dotnet")
    monkeypatch.setattr(dotnet_tool_runner, "_artifacts_root", lambda: artifacts_root)

    def fake_run_build_command(
        command: list[str],
        *,
        timeout: float,
        artifacts_root: Path,
        assembly_name: str,
        use_lock: bool,
    ) -> subprocess.CompletedProcess[str]:
        assert timeout == 7
        assert artifacts_root == tmp_path / "artifacts"
        assert assembly_name == "Probe"
        assert not use_lock
        assert (artifacts_root / "locks" / "Probe.lock" / "owner.pid").is_file()
        assert "--artifacts-path" in command
        dll = artifacts_root / "bin" / assembly_name / "release" / "Probe.dll"
        dll.parent.mkdir(parents=True)
        _ = dll.write_text("", encoding="utf-8")
        return subprocess.CompletedProcess(command, 0, "", "")

    def fake_run_tool_dll(
        tool_dll: Path,
        args: list[str],
        *,
        timeout: int,
    ) -> subprocess.CompletedProcess[str]:
        assert tool_dll == artifacts_root / "bin" / "Probe" / "release" / "Probe.dll"
        assert args == ["--flag"]
        assert timeout == 7
        assert not (artifacts_root / "locks" / "Probe.lock" / "owner.pid").exists()
        return subprocess.CompletedProcess(["dotnet", str(tool_dll), *args], 0, "ok", "")

    monkeypatch.setattr(dotnet_tool_runner, "_run_build_command", fake_run_build_command)
    monkeypatch.setattr(dotnet_tool_runner, "_run_tool_dll", fake_run_tool_dll)

    result = dotnet_tool_runner.run_cached_tool_project(project_path, ["--flag"], timeout=7)

    assert result.stdout == "ok"


def test_run_cached_tool_project_skips_build_when_cached_dll_is_fresh(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Fresh cached tool DLLs should avoid repeated dotnet build startup."""
    project_path = _write_probe_project(tmp_path)
    artifacts_root = tmp_path / "artifacts"
    dll = _write_cached_probe_dll(artifacts_root, project_path)
    fresh_time = project_path.stat().st_mtime + 10.0
    os.utime(dll, (fresh_time, fresh_time))
    monkeypatch.setattr(dotnet_tool_runner, "_dotnet_path", lambda: "dotnet")
    monkeypatch.setattr(dotnet_tool_runner, "_artifacts_root", lambda: artifacts_root)

    def fail_build(
        command: list[str],
        *,
        timeout: float,
        artifacts_root: Path,
        assembly_name: str,
        use_lock: bool,
    ) -> subprocess.CompletedProcess[str]:
        _ = command, timeout, artifacts_root, assembly_name, use_lock
        pytest.fail("fresh cached tool should not invoke dotnet build")

    def fake_run_tool_dll(
        tool_dll: Path,
        args: list[str],
        *,
        timeout: int,
    ) -> subprocess.CompletedProcess[str]:
        assert tool_dll == dll
        assert args == ["--cached"]
        assert timeout == 5
        return subprocess.CompletedProcess(["dotnet", str(tool_dll), *args], 0, "cached", "")

    monkeypatch.setattr(dotnet_tool_runner, "_run_build_command", fail_build)
    monkeypatch.setattr(dotnet_tool_runner, "_run_tool_dll", fake_run_tool_dll)

    result = dotnet_tool_runner.run_cached_tool_project(project_path, ["--cached"], timeout=5)

    assert result.stdout == "cached"


def test_run_cached_tool_project_rebuilds_when_source_file_is_deleted(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Cached tool fingerprints should catch SDK-style glob source deletions."""
    project_path = _write_probe_project(tmp_path)
    source_path = tmp_path / "Program.cs"
    artifacts_root = tmp_path / "artifacts"
    _ = source_path.write_text("public static class Program {}\n", encoding="utf-8")
    dll = _write_cached_probe_dll(artifacts_root, project_path)
    source_path.unlink()

    monkeypatch.setattr(dotnet_tool_runner, "_dotnet_path", lambda: "dotnet")
    monkeypatch.setattr(dotnet_tool_runner, "_artifacts_root", lambda: artifacts_root)

    build_calls = 0

    def fake_run_build_command(
        command: list[str],
        *,
        timeout: float,
        artifacts_root: Path,
        assembly_name: str,
        use_lock: bool,
    ) -> subprocess.CompletedProcess[str]:
        nonlocal build_calls
        _ = timeout, use_lock
        build_calls += 1
        assert command[0] == "dotnet"
        _ = (artifacts_root / "bin" / assembly_name / "release" / "Probe.dll").write_text("", encoding="utf-8")
        return subprocess.CompletedProcess(command, 0, "", "")

    def fake_run_tool_dll(
        tool_dll: Path,
        args: list[str],
        *,
        timeout: int,
    ) -> subprocess.CompletedProcess[str]:
        assert tool_dll == dll
        assert args == []
        assert timeout == 5
        return subprocess.CompletedProcess(["dotnet", str(tool_dll)], 0, "rebuilt", "")

    monkeypatch.setattr(dotnet_tool_runner, "_run_build_command", fake_run_build_command)
    monkeypatch.setattr(dotnet_tool_runner, "_run_tool_dll", fake_run_tool_dll)

    result = dotnet_tool_runner.run_cached_tool_project(project_path, [], timeout=5)

    assert build_calls == 1
    assert result.stdout == "rebuilt"


def test_run_cached_tool_project_normalizes_stamp_write_errors(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Stamp write failures should retain the failing path in a DotnetToolError."""
    project_path = _write_probe_project(tmp_path)
    artifacts_root = tmp_path / "artifacts"
    broken_stamp = tmp_path / "missing-parent" / "Probe.dll.sources.sha256"
    monkeypatch.setattr(dotnet_tool_runner, "_dotnet_path", lambda: "dotnet")
    monkeypatch.setattr(dotnet_tool_runner, "_artifacts_root", lambda: artifacts_root)

    def broken_stamp_path(_tool_dll: Path) -> Path:
        return broken_stamp

    monkeypatch.setattr(dotnet_tool_runner, "_tool_sources_stamp_path", broken_stamp_path)

    def fake_run_build_command(
        command: list[str],
        *,
        timeout: float,
        artifacts_root: Path,
        assembly_name: str,
        use_lock: bool,
    ) -> subprocess.CompletedProcess[str]:
        _ = timeout, use_lock
        assert command[0] == "dotnet"
        dll = artifacts_root / "bin" / assembly_name / "release" / "Probe.dll"
        dll.parent.mkdir(parents=True)
        _ = dll.write_text("", encoding="utf-8")
        return subprocess.CompletedProcess(command, 0, "", "")

    monkeypatch.setattr(dotnet_tool_runner, "_run_build_command", fake_run_build_command)

    with pytest.raises(dotnet_tool_runner.DotnetToolError) as exc_info:
        _ = dotnet_tool_runner.run_cached_tool_project(project_path, [], timeout=5)

    assert broken_stamp.as_posix() in str(exc_info.value)
    assert isinstance(exc_info.value.__cause__, OSError)


def test_cached_tool_stale_check_normalizes_stamp_read_errors(tmp_path: Path) -> None:
    """Unreadable stamp paths should report the failing stamp path consistently."""
    project_path = _write_probe_project(tmp_path)
    artifacts_root = tmp_path / "artifacts"
    dll = _write_cached_probe_dll(artifacts_root, project_path)
    stamp = dotnet_tool_runner._tool_sources_stamp_path(dll)  # noqa: SLF001
    stamp.unlink()
    stamp.mkdir()

    with pytest.raises(dotnet_tool_runner.DotnetToolError) as exc_info:
        _ = dotnet_tool_runner._cached_tool_is_stale(project_path, dll)  # noqa: SLF001

    assert stamp.as_posix() in str(exc_info.value)
    assert isinstance(exc_info.value.__cause__, OSError)


def test_tool_sources_fingerprint_normalizes_source_read_errors(tmp_path: Path) -> None:
    """Unreadable source inputs should report the failing source path consistently."""
    project_path = _write_probe_project(tmp_path)
    broken_source = tmp_path / "Broken.cs"
    broken_source.mkdir()

    with pytest.raises(dotnet_tool_runner.DotnetToolError) as exc_info:
        _ = dotnet_tool_runner._tool_sources_fingerprint(project_path)  # noqa: SLF001

    assert broken_source.as_posix() in str(exc_info.value)
    assert isinstance(exc_info.value.__cause__, OSError)


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
