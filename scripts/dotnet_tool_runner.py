"""Helpers for running repo-local dotnet tools with cached or run-scoped artifacts."""
# ruff: noqa: S314, S603 -- parses repo-local csproj files and invokes repo-local dotnet tools

from __future__ import annotations

import os
import shlex
import shutil
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET
from contextlib import contextmanager
from hashlib import sha256
from pathlib import Path
from typing import TYPE_CHECKING, Final

if TYPE_CHECKING:
    from collections.abc import Generator

REPO_ROOT: Final = Path(__file__).resolve().parents[1]
ARTIFACTS_ENV: Final = "QUDJP_DOTNET_ARTIFACTS_ROOT"
DEFAULT_ARTIFACTS_ROOT: Final = REPO_ROOT / ".artifacts" / "dotnet"
BUILD_LOCK_TIMEOUT_SECONDS: Final = 600.0
BUILD_LOCK_POLL_SECONDS: Final = 0.1
BUILD_LOCK_OWNER_FILE: Final = "owner.pid"
BUILD_LOCK_OWNER_GRACE_SECONDS: Final = 2.0


class DotnetToolError(RuntimeError):
    """Raised when a repo-local dotnet tool cannot be built or run."""


def run_tool_project(
    project_path: Path,
    args: list[str],
    *,
    timeout: int,
    configuration: str = "Release",
) -> subprocess.CompletedProcess[str]:
    """Build a repo-local tool into a run-scoped artifacts root and execute its DLL."""
    resolved_project = project_path.resolve()
    if not resolved_project.is_file():
        msg = f"dotnet tool project is missing: {resolved_project}"
        raise DotnetToolError(msg)
    assembly_name = _assembly_name(resolved_project)
    with _temporary_artifacts_root(assembly_name) as artifacts_root:
        tool_dll = _build_tool_project(
            resolved_project,
            configuration=configuration,
            artifacts_root=artifacts_root,
            timeout=timeout,
        )
        return _run_tool_dll(tool_dll, args, timeout=timeout)


def run_cached_tool_project(
    project_path: Path,
    args: list[str],
    *,
    timeout: int,
    configuration: str = "Release",
) -> subprocess.CompletedProcess[str]:
    """Build a repo-local tool in the shared artifacts root and execute its DLL."""
    resolved_project = project_path.resolve()
    if not resolved_project.is_file():
        msg = f"dotnet tool project is missing: {resolved_project}"
        raise DotnetToolError(msg)

    assembly_name = _assembly_name(resolved_project)
    artifacts_root = _artifacts_root()
    with _build_lock(artifacts_root, assembly_name):
        tool_dll = _tool_dll_path(artifacts_root, assembly_name, configuration)
        if _cached_tool_is_stale(resolved_project, tool_dll):
            tool_dll = _build_tool_project(
                resolved_project,
                configuration=configuration,
                artifacts_root=artifacts_root,
                timeout=timeout,
            )
            stamp_path = _tool_sources_stamp_path(tool_dll)
            try:
                _ = stamp_path.write_text(_tool_sources_fingerprint(resolved_project), encoding="utf-8")
            except OSError as exc:
                msg = f"failed to write dotnet tool source fingerprint stamp: {stamp_path}: {exc}"
                raise DotnetToolError(msg) from exc
    return _run_tool_dll(tool_dll, args, timeout=timeout)


def build_tool_project(project_path: Path, *, configuration: str = "Release") -> Path:
    """Build a repo-local dotnet tool and return the produced DLL path."""
    resolved_project = project_path.resolve()
    return _build_tool_project(
        resolved_project,
        configuration=configuration,
        artifacts_root=_artifacts_root(),
        timeout=BUILD_LOCK_TIMEOUT_SECONDS,
        use_lock=True,
    )


def _build_tool_project(
    resolved_project: Path,
    *,
    configuration: str,
    artifacts_root: Path,
    timeout: float,
    use_lock: bool = False,
) -> Path:
    if not resolved_project.is_file():
        msg = f"dotnet tool project is missing: {resolved_project}"
        raise DotnetToolError(msg)

    assembly_name = _assembly_name(resolved_project)
    command = [
        _dotnet_path(),
        "build",
        str(resolved_project),
        "--configuration",
        configuration,
        "--artifacts-path",
        str(artifacts_root),
    ]
    result = _run_build_command(
        command,
        timeout=timeout,
        artifacts_root=artifacts_root,
        assembly_name=assembly_name,
        use_lock=use_lock,
    )
    if result.returncode != 0:
        details = "\n".join(part for part in (result.stdout.strip(), result.stderr.strip()) if part)
        msg = f"dotnet build failed with exit {result.returncode}: {shlex.join(command)}"
        if details:
            msg = f"{msg}\n{details}"
        raise DotnetToolError(msg)

    tool_dll = _tool_dll_path(artifacts_root, assembly_name, configuration)
    if not tool_dll.is_file():
        msg = f"dotnet build succeeded but did not produce expected tool DLL: {tool_dll}"
        raise DotnetToolError(msg)
    return tool_dll


def _tool_dll_path(artifacts_root: Path, assembly_name: str, configuration: str) -> Path:
    return artifacts_root / "bin" / assembly_name / configuration.lower() / f"{assembly_name}.dll"


def _cached_tool_is_stale(project_path: Path, tool_dll: Path) -> bool:
    if not tool_dll.is_file():
        return True

    stamp_path = _tool_sources_stamp_path(tool_dll)
    try:
        cached_fingerprint = stamp_path.read_text(encoding="utf-8").strip()
    except FileNotFoundError:
        return True
    except OSError as exc:
        msg = f"failed to read dotnet tool source fingerprint stamp: {stamp_path}: {exc}"
        raise DotnetToolError(msg) from exc
    return cached_fingerprint != _tool_sources_fingerprint(project_path)


def _tool_sources_stamp_path(tool_dll: Path) -> Path:
    return tool_dll.with_name(f"{tool_dll.name}.sources.sha256")


def _tool_sources_fingerprint(project_path: Path) -> str:
    digest = sha256()
    source_root = project_path.parent
    for path in _tool_source_paths(project_path):
        digest.update(path.relative_to(source_root).as_posix().encode())
        digest.update(b"\0")
        try:
            digest.update(path.read_bytes())
        except OSError as exc:
            msg = f"failed to read dotnet tool source for fingerprint: {path}: {exc}"
            raise DotnetToolError(msg) from exc
        digest.update(b"\0")
    return digest.hexdigest()


def _tool_source_paths(project_path: Path) -> list[Path]:
    source_root = project_path.parent
    source_paths = [
        path
        for path in source_root.rglob("*.cs")
        if not {"bin", "obj"}.intersection(path.relative_to(source_root).parts)
    ]
    return [project_path, *sorted(source_paths)]


def _run_tool_dll(tool_dll: Path, args: list[str], *, timeout: int) -> subprocess.CompletedProcess[str]:
    dotnet = _dotnet_path()
    command = [dotnet, str(tool_dll), *args]
    try:
        return subprocess.run(
            command,
            capture_output=True,
            text=True,
            check=False,
            timeout=timeout,
        )
    except subprocess.TimeoutExpired as exc:
        details = "\n".join(part for part in (_output_text(exc.stdout), _output_text(exc.stderr)) if part)
        msg = f"dotnet tool timed out after {timeout}s: {shlex.join(command)}"
        if details:
            msg = f"{msg}\n{details}"
        raise DotnetToolError(msg) from exc


def _run_build_command(
    command: list[str],
    *,
    timeout: float,
    artifacts_root: Path,
    assembly_name: str,
    use_lock: bool,
) -> subprocess.CompletedProcess[str]:
    try:
        if use_lock:
            with _build_lock(artifacts_root, assembly_name):
                return subprocess.run(command, capture_output=True, text=True, check=False, timeout=timeout)
        return subprocess.run(command, capture_output=True, text=True, check=False, timeout=timeout)
    except subprocess.TimeoutExpired as exc:
        details = "\n".join(part for part in (_output_text(exc.stdout), _output_text(exc.stderr)) if part)
        msg = f"dotnet build timed out after {timeout:g}s: {shlex.join(command)}"
        if details:
            msg = f"{msg}\n{details}"
        raise DotnetToolError(msg) from exc


@contextmanager
def _temporary_artifacts_root(assembly_name: str) -> Generator[Path, None, None]:
    run_parent = _artifacts_root() / "tool-runs"
    run_parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=f"{assembly_name}.", dir=run_parent) as raw_path:
        yield Path(raw_path)


@contextmanager
def _build_lock(artifacts_root: Path, assembly_name: str) -> Generator[None, None, None]:
    lock_dir = artifacts_root / "locks"
    lock_dir.mkdir(parents=True, exist_ok=True)
    lock_path = lock_dir / f"{assembly_name}.lock"
    owner_path = lock_path / BUILD_LOCK_OWNER_FILE
    deadline = time.monotonic() + BUILD_LOCK_TIMEOUT_SECONDS
    while True:
        try:
            lock_path.mkdir()
            _ = owner_path.write_text(f"{os.getpid()}\n", encoding="utf-8")
            break
        except FileExistsError as exc:
            if _remove_stale_lock(lock_path):
                continue
            if time.monotonic() >= deadline:
                msg = f"timed out waiting for dotnet build lock: {lock_path}"
                raise DotnetToolError(msg) from exc
            time.sleep(BUILD_LOCK_POLL_SECONDS)
    try:
        yield
    finally:
        owner_path.unlink(missing_ok=True)
        lock_path.rmdir()


def _remove_stale_lock(lock_path: Path) -> bool:
    if not lock_path.is_dir():
        lock_path.unlink(missing_ok=True)
        return True

    owner_path = lock_path / BUILD_LOCK_OWNER_FILE
    return _remove_stale_owner_lock(lock_path, owner_path)


def _remove_stale_owner_lock(lock_path: Path, owner_path: Path) -> bool:
    try:
        owner_text = owner_path.read_text(encoding="utf-8").strip()
    except FileNotFoundError:
        if not _path_is_older_than(lock_path, BUILD_LOCK_OWNER_GRACE_SECONDS):
            return False
        return _remove_empty_lock_dir(lock_path)

    try:
        owner_pid = int(owner_text)
    except ValueError:
        if not _path_is_older_than(owner_path, BUILD_LOCK_OWNER_GRACE_SECONDS):
            return False
        owner_path.unlink(missing_ok=True)
        return _remove_empty_lock_dir(lock_path)

    if _process_exists(owner_pid):
        return False

    owner_path.unlink(missing_ok=True)
    return _remove_empty_lock_dir(lock_path)


def _path_is_older_than(path: Path, seconds: float) -> bool:
    try:
        age_seconds = time.time() - path.stat().st_mtime
    except FileNotFoundError:
        return False
    return age_seconds >= seconds


def _remove_empty_lock_dir(lock_path: Path) -> bool:
    try:
        lock_path.rmdir()
    except OSError:
        return False
    return True


def _process_exists(pid: int) -> bool:
    if pid <= 0:
        return False
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def _artifacts_root() -> Path:
    raw_path = os.environ.get(ARTIFACTS_ENV)
    if raw_path:
        return Path(raw_path).expanduser().resolve()
    return DEFAULT_ARTIFACTS_ROOT


def _assembly_name(project_path: Path) -> str:
    try:
        root = ET.parse(project_path).getroot()
    except ET.ParseError as exc:
        msg = f"dotnet project XML is unreadable: {project_path}: {exc}"
        raise DotnetToolError(msg) from exc

    for element in root.iter():
        if (
            element.tag.removeprefix("{http://schemas.microsoft.com/developer/msbuild/2003}") == "AssemblyName"
            and element.text
            and element.text.strip()
        ):
            return element.text.strip()
    return project_path.stem


def _dotnet_path() -> str:
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        msg = "dotnet 10.0.x SDK required to run repo-local Roslyn tools"
        raise DotnetToolError(msg)
    return dotnet


def _output_text(value: str | bytes | None) -> str:
    if value is None:
        return ""
    if isinstance(value, bytes):
        return value.decode(errors="replace").strip()
    return value.strip()
