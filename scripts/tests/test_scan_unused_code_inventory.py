"""Smoke tests for the Roslyn unused-code inventory scanner."""
# ruff: noqa: S603,S607 -- tests invoke dotnet to drive the repo-local tool

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import cast

import pytest

from scripts.dotnet_tool_runner import build_tool_project
from scripts.scan_unused_code_inventory import InventoryPayload, write_inventory

_REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "UnusedCodeInventoryScanner" / "UnusedCodeInventoryScanner.csproj"


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


def _run_tool(tool_dll: Path, source_root: Path, config: Path, output: Path) -> None:
    result = subprocess.run(
        [
            "dotnet",
            str(tool_dll),
            "--source-root",
            str(source_root),
            "--config",
            str(config),
            "--output",
            str(output),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, (
        f"scanner failed (exit {result.returncode}). stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )


def _candidate_ids(payload: InventoryPayload) -> set[str]:
    return {row["symbol_id"] for row in payload["candidates"]}
