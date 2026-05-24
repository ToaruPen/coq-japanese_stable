"""Smoke tests for the Roslyn unused-code inventory scanner."""
# ruff: noqa: S603 -- tests invoke dotnet to drive the repo-local tool

from __future__ import annotations

import json
import os
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import cast

import pytest

from scripts.dotnet_tool_runner import build_tool_project
from scripts.scan_unused_code_inventory import InventoryPayload, write_inventory

_REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "UnusedCodeInventoryScanner" / "UnusedCodeInventoryScanner.csproj"
DOTNET_TIMEOUT_SECONDS = 120


@dataclass(frozen=True)
class ToolRunOptions:
    """Optional arguments for invoking the scanner fixture."""

    references: list[Path] | None = None
    managed_dir: Path | None = None
    env: dict[str, str] | None = None


@pytest.fixture(scope="session")
def inventory_tool_dll() -> Path:
    """Build the inventory tool once and reuse its DLL for smoke tests."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")
    return build_tool_project(PROJECT_PATH)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_classifies_candidates_roots_and_test_references(
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """The scanner reports private/internal declarations not referenced by scanned C#."""
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    tests = source_root / "Mods" / "QudJP" / "Assemblies" / "QudJP.Tests"
    production.mkdir(parents=True)
    tests.mkdir(parents=True)
    _ = (production / "Demo.cs").write_text(
        """
using HarmonyLib;

namespace QudJP;

internal static class UsedType
{
    internal static void UsedByProduction() {}
    internal static void UsedOnlyByTests() {}
    private static void UnusedPrivateMethod() {}
    private static int UnusedField;
    private static int UsedField;

    internal static int UseField() => UsedField;
}

internal static class UnusedType
{
    internal static void NeverCalled() {}
}

[HarmonyPatch]
internal static class RuntimePatch
{
    private static void Prefix() {}
    private static void HelperUnusedByPatch() {}
}

internal sealed class ExcludedForTests
{
    internal static void Helper() {}
}
""",
        encoding="utf-8",
    )
    _ = (production / "UseDemo.cs").write_text(
        """
namespace QudJP;

internal static class UseDemo
{
    internal static void Run()
    {
        UsedType.UsedByProduction();
        _ = UsedType.UseField();
    }
}
""",
        encoding="utf-8",
    )
    _ = (tests / "DemoTests.cs").write_text(
        """
namespace QudJP.Tests;

public static class DemoTests
{
    public static void Exercise()
    {
        QudJP.UsedType.UsedOnlyByTests();
    }
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": [
                    "Mods/QudJP/Assemblies/src/",
                    "Mods/QudJP/Assemblies/QudJP.Tests/",
                ],
                "exclude_path_contains": ["/bin/", "/obj/"],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": ["HarmonyPatch", "HarmonyPatchAttribute"],
                "root_member_names_in_attribute_rooted_types": ["Prefix"],
                "exclude_symbol_patterns": ["QudJP.ExcludedForTests*"],
                "exclude_declaration_name_suffixes": [],
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    _run_tool(inventory_tool_dll, source_root, config, output)
    payload = cast("InventoryPayload", json.loads(output.read_text(encoding="utf-8")))

    assert payload["generation"]["includes_raw_source_text"] is False
    assert payload["generation"]["parse_error_file_count"] == 0
    candidate_ids = _candidate_ids(payload)
    assert "QudJP.UsedType.UnusedPrivateMethod()" in candidate_ids
    assert "QudJP.UsedType.UnusedField" in candidate_ids
    assert "QudJP.UnusedType" in candidate_ids
    assert "QudJP.UnusedType.NeverCalled()" in candidate_ids
    assert "QudJP.RuntimePatch.HelperUnusedByPatch()" in candidate_ids
    assert "QudJP.UsedType.UsedByProduction()" not in candidate_ids
    assert "QudJP.UsedType.UsedOnlyByTests()" not in candidate_ids
    assert "QudJP.UsedType.UsedField" not in candidate_ids
    assert "QudJP.RuntimePatch.Prefix()" not in candidate_ids
    assert "QudJP.ExcludedForTests" not in candidate_ids

    rooted_ids = {row["symbol_id"] for row in payload["rooted_declarations"]}
    assert "QudJP.RuntimePatch" in rooted_ids
    assert "QudJP.RuntimePatch.Prefix()" in rooted_ids


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_resolves_external_attribute_references(
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """External metadata references should make semantic attribute roots available."""
    external_dll = _build_external_root_attribute(tmp_path)
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    production.mkdir(parents=True)
    _ = (production / "ExternalRooted.cs").write_text(
        """
using External;

namespace QudJP;

[Root]
internal static class ExternalRooted
{
    private static void Prefix() {}
    private static void HelperUnusedByRoot() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": [],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": ["External.RootAttribute"],
                "root_member_names_in_attribute_rooted_types": ["Prefix"],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    _run_tool(
        inventory_tool_dll,
        source_root,
        config,
        output,
        options=ToolRunOptions(references=[external_dll]),
    )
    payload = cast("InventoryPayload", json.loads(output.read_text(encoding="utf-8")))

    assert payload["generation"]["parse_error_file_count"] == 0
    assert str(external_dll) in payload["generation"]["metadata_references"]["external_references"]
    candidate_ids = _candidate_ids(payload)
    assert "QudJP.ExternalRooted" not in candidate_ids
    assert "QudJP.ExternalRooted.Prefix()" not in candidate_ids
    assert "QudJP.ExternalRooted.HelperUnusedByRoot()" in candidate_ids


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_matches_non_private_accessibilities_as_lowercase(
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """Configured accessibility tokens should match Roslyn accessibility names."""
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    production.mkdir(parents=True)
    _ = (production / "Demo.cs").write_text(
        """
namespace QudJP;

internal class Demo
{
    protected void NeverCalled() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": [],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["protected"],
                "root_attribute_type_suffixes": [],
                "root_member_names_in_attribute_rooted_types": [],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    _run_tool(inventory_tool_dll, source_root, config, output)
    payload = cast("InventoryPayload", json.loads(output.read_text(encoding="utf-8")))

    assert "QudJP.Demo.NeverCalled()" in _candidate_ids(payload)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_resolves_configured_managed_dir_references(
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """Configured assembly names should resolve against an explicit managed directory."""
    external_dll = _build_external_root_attribute(tmp_path)
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    production.mkdir(parents=True)
    _ = (production / "ExternalRooted.cs").write_text(
        """
using External;

namespace QudJP;

[Root]
internal static class ExternalRooted
{
    private static void Prefix() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": [],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": ["External.RootAttribute"],
                "root_member_names_in_attribute_rooted_types": ["Prefix"],
                "reference_assembly_names": [external_dll.name],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    _run_tool(
        inventory_tool_dll,
        source_root,
        config,
        output,
        options=ToolRunOptions(managed_dir=external_dll.parent),
    )
    payload = cast("InventoryPayload", json.loads(output.read_text(encoding="utf-8")))

    assert str(external_dll) in payload["generation"]["metadata_references"]["external_references"]
    assert not payload["generation"]["metadata_references"]["missing_external_references"]
    assert not _candidate_ids(payload)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_reports_configured_references_when_managed_dir_is_missing(
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """Configured assembly names should not disappear when no managed directory resolves."""
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    missing_managed_dir = tmp_path / "missing-managed"
    production.mkdir(parents=True)
    _ = (production / "Demo.cs").write_text(
        """
namespace QudJP;

internal static class Demo
{
    private static void Unused() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": [],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": [],
                "root_member_names_in_attribute_rooted_types": [],
                "reference_assembly_names": ["Missing.External.dll"],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    _run_tool(
        inventory_tool_dll,
        source_root,
        config,
        output,
        options=ToolRunOptions(managed_dir=missing_managed_dir),
    )
    payload = cast("InventoryPayload", json.loads(output.read_text(encoding="utf-8")))

    metadata = payload["generation"]["metadata_references"]
    assert metadata["external_references"] == []
    assert metadata["missing_external_references"] == [str(missing_managed_dir / "Missing.External.dll")]


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_falls_back_when_env_managed_dir_is_missing(
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """A bad COQ_MANAGED_DIR should not hide a later existing managed directory."""
    external_dll = _build_external_root_attribute(tmp_path)
    fake_home = tmp_path / "home"
    default_managed_dir = (
        fake_home
        / "Games"
        / "CavesOfQud-stable-ref"
        / "CoQ.app"
        / "Contents"
        / "Resources"
        / "Data"
        / "Managed"
    )
    default_managed_dir.mkdir(parents=True)
    default_reference = default_managed_dir / external_dll.name
    _ = shutil.copy2(external_dll, default_reference)
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    production.mkdir(parents=True)
    _ = (production / "ExternalRooted.cs").write_text(
        """
using External;

namespace QudJP;

[Root]
internal static class ExternalRooted
{
    private static void Prefix() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": [],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": ["External.RootAttribute"],
                "root_member_names_in_attribute_rooted_types": ["Prefix"],
                "reference_assembly_names": [external_dll.name],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    _run_tool(
        inventory_tool_dll,
        source_root,
        config,
        output,
        options=ToolRunOptions(
            env={
                "COQ_MANAGED_DIR": str(tmp_path / "missing-env-managed"),
                "HOME": str(fake_home),
            },
        ),
    )
    payload = cast("InventoryPayload", json.loads(output.read_text(encoding="utf-8")))

    metadata = payload["generation"]["metadata_references"]
    assert metadata["external_references"] == [str(default_reference)]
    assert not metadata["missing_external_references"]
    assert not _candidate_ids(payload)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_python_wrapper_runs_scanner(tmp_path: Path) -> None:
    """The Python wrapper should build and execute the Roslyn scanner."""
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    production.mkdir(parents=True)
    _ = (production / "Demo.cs").write_text(
        """
namespace QudJP;

internal static class Demo
{
    private static void Unused() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": ["/bin/", "/obj/"],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": [],
                "root_member_names_in_attribute_rooted_types": [],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    output = tmp_path / "unused.json"

    payload = write_inventory(source_root, config, output)

    assert payload["totals"]["candidates"] == 2
    assert _candidate_ids(payload) == {"QudJP.Demo", "QudJP.Demo.Unused()"}


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_python_wrapper_normalizes_relative_output_path(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Relative output paths should be used consistently for scanner and JSON loading."""
    source_root = tmp_path / "repo"
    production = source_root / "Mods" / "QudJP" / "Assemblies" / "src"
    production.mkdir(parents=True)
    _ = (production / "Demo.cs").write_text(
        """
namespace QudJP;

internal static class Demo
{
    private static void Unused() {}
}
""",
        encoding="utf-8",
    )
    config = source_root / "config.json"
    _ = config.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "include_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "exclude_path_contains": [],
                "report_path_prefixes": ["Mods/QudJP/Assemblies/src/"],
                "candidate_accessibilities": ["private", "internal"],
                "root_attribute_type_suffixes": [],
                "root_member_names_in_attribute_rooted_types": [],
                "exclude_symbol_patterns": [],
                "exclude_declaration_name_suffixes": [],
            },
        ),
        encoding="utf-8",
    )
    monkeypatch.chdir(tmp_path)

    payload = write_inventory(source_root, config, Path("nested/unused.json"))

    assert payload["totals"]["candidates"] == 2
    assert (tmp_path / "nested" / "unused.json").is_file()


def _build_external_root_attribute(tmp_path: Path) -> Path:
    project = tmp_path / "external" / "External.csproj"
    project.parent.mkdir()
    _ = project.write_text(
        """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""",
        encoding="utf-8",
    )
    _ = (project.parent / "RootAttribute.cs").write_text(
        """
namespace External;

public sealed class RootAttribute : System.Attribute;
""",
        encoding="utf-8",
    )
    result = _run_dotnet(
        ["dotnet", "build", str(project), "--configuration", "Release"],
        cwd=None,
    )
    assert result.returncode == 0, (
        f"external fixture build failed (exit {result.returncode}). stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )
    return project.parent / "bin" / "Release" / "net10.0" / "External.dll"


def _run_tool(
    tool_dll: Path,
    source_root: Path,
    config: Path,
    output: Path,
    *,
    options: ToolRunOptions | None = None,
) -> None:
    tool_options = options or ToolRunOptions()
    reference_args = [
        arg
        for reference in tool_options.references or []
        for arg in ("--reference", str(reference))
    ]
    managed_dir_args = (
        []
        if tool_options.managed_dir is None
        else ["--managed-dir", str(tool_options.managed_dir)]
    )
    result = _run_dotnet(
        [
            "dotnet",
            str(tool_dll),
            "--source-root",
            str(source_root),
            "--config",
            str(config),
            "--output",
            str(output),
            *reference_args,
            *managed_dir_args,
        ],
        cwd=None,
        env=tool_options.env,
    )
    assert result.returncode == 0, (
        f"scanner failed (exit {result.returncode}). stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )


def _run_dotnet(
    args: list[str],
    *,
    cwd: Path | None,
    env: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    run_env = None if env is None else {**dict(os.environ), **env}
    try:
        return subprocess.run(
            args,
            cwd=cwd,
            env=run_env,
            capture_output=True,
            text=True,
            check=False,
            timeout=DOTNET_TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired as exc:
        message = "\n".join(
            [
                f"dotnet command timed out after {DOTNET_TIMEOUT_SECONDS}s: {' '.join(args)}",
                f"stdout:\n{exc.stdout or ''}",
                f"stderr:\n{exc.stderr or ''}",
            ]
        )
        pytest.fail(message)


def _candidate_ids(payload: InventoryPayload) -> set[str]:
    return {row["symbol_id"] for row in payload["candidates"]}
