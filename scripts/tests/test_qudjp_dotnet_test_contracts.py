"""Static contracts for QudJP NUnit test fixtures."""

from __future__ import annotations

import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
JUSTFILE = REPO_ROOT / "justfile"
QUDJP_CSPROJ = REPO_ROOT / "Mods" / "QudJP" / "Assemblies" / "QudJP.csproj"
TEST_ARCHITECTURE_DOC = REPO_ROOT / "docs" / "test-architecture.md"
RULES_DOC = REPO_ROOT / "docs" / "RULES.md"


def _recipe_block(justfile: str, recipe_name: str, next_recipe_name: str | None) -> str:
    recipe_start = re.compile(rf"\n{re.escape(recipe_name)}(?:\s[^:\n]*)?:")
    start_match = recipe_start.search(justfile)
    assert start_match is not None, f"recipe not found: {recipe_name}"
    start = start_match.start()
    if next_recipe_name is None:
        end = len(justfile)
    else:
        next_recipe_start = re.compile(rf"\n{re.escape(next_recipe_name)}(?:\s[^:\n]*)?:")
        end_match = next_recipe_start.search(justfile, start_match.end())
        assert end_match is not None, f"recipe not found: {next_recipe_name}"
        end = end_match.start()
    return justfile[start:end]


def test_local_csharp_full_suite_builds_test_project_once() -> None:
    """The local all-C# test entrypoint should avoid per-category build and VSTest startup."""
    justfile = "\n" + JUSTFILE.read_text(encoding="utf-8")
    recipe = _recipe_block(justfile, "test-csharp", "python-check")
    check_recipe = _recipe_block(justfile, "check", "pr-check")

    assert 'dotnet_test_build_properties := "-p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false"' in justfile
    assert "dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj" in recipe
    assert recipe.count("dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj") == 1
    assert "{{dotnet_test_build_properties}}" in recipe
    assert recipe.count('dotnet test "$test_dll"') == 1
    assert "TestCategory=" not in recipe

    assert "test-csharp" in check_recipe
    assert "test-l1 test-l2 test-l2g" not in check_recipe


def test_game_version_gate_covers_current_and_game_free_contracts() -> None:
    """Version upgrades must prove docs, both dependency modes, and patch bindings."""
    justfile = "\n" + JUSTFILE.read_text(encoding="utf-8")
    csproj = QUDJP_CSPROJ.read_text(encoding="utf-8")
    pr_check = _recipe_block(justfile, "pr-check", "ci-dotnet")
    no_game = _recipe_block(justfile, "ci-dotnet-no-game", "target-game-version-check")
    target_version = _recipe_block(justfile, "target-game-version-check", "game-version-check")
    game_version = _recipe_block(justfile, "game-version-check", "roslyn-build-annals")

    assert "<EnableDefaultCompileItems>false</EnableDefaultCompileItems>" in csproj
    assert '<OutputPath Condition="\'$(QudJPOutputPath)\' == \'\'">' in csproj
    assert '<OutputPath Condition="\'$(QudJPOutputPath)\' != \'\'">$(QudJPOutputPath)</OutputPath>' in csproj
    assert "ci-dotnet-no-game" in pr_check
    assert "ast-grep-check" in pr_check

    assert 'mktemp -d "{{dotnet_artifacts_root}}/ci-dotnet-no-game/run.XXXXXX"' in no_game
    assert 'run_root="$(cd "$run_root" && pwd)"' in no_game
    assert 'missing_game_dir="$run_root/missing-game"' in no_game
    assert no_game.count("dotnet build Mods/QudJP/Assemblies/QudJP.csproj") == 1
    assert no_game.count("dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj") == 1
    assert no_game.count('--artifacts-path "$') == 2
    assert no_game.count('-p:GameDir="$missing_game_dir"') == 2
    assert no_game.count('-p:QudJPOutputPath="$') == 2
    assert 'production_output="$production_artifacts/bin/QudJP/release"' in no_game
    assert 'test -f "$production_output/QudJP.dll"' in no_game
    assert "Assembly-CSharp.dll" in no_game
    assert "UnityEngine*.dll" in no_game
    assert "Unity.TextMeshPro.dll" in no_game
    assert "stubs leaked into no-game production output" in no_game
    assert "--no-dependencies" not in no_game
    assert "{{dotnet_test_build_properties}}" in no_game
    assert no_game.count('dotnet test "$test_dll"') == 1

    assert "scripts/tests/test_target_game_version_contract.py" in target_version

    expected_steps = (
        "just target-game-version-check",
        (
            "uv run pytest scripts/tests/test_static_producer_closure.py::"
            "test_covered_owner_families_have_current_source_and_test_evidence -q"
        ),
        "just build",
        "just test-csharp",
        "just ci-dotnet-no-game",
        "just qudtest-headless qudtest:bindings .artifacts/qudtest-game-version-bindings",
        "just qudtest-headless qudtest:bindings-all .artifacts/qudtest-game-version-bindings-all",
    )
    positions = [game_version.index(step) for step in expected_steps]
    assert positions == sorted(positions)


def test_route_family_test_guidance_limits_l2_case_growth() -> None:
    """Route-family additions should not default to one Harmony test per string."""
    test_architecture = TEST_ARCHITECTURE_DOC.read_text(encoding="utf-8")
    rules = RULES_DOC.read_text(encoding="utf-8")

    assert "1 回の Harmony patch setup にまとめて batch 実行する" in test_architecture
    assert "静的 inventory や data-contract coverage" in test_architecture
    assert "one L2 smoke case" in rules
    assert "Do not add one new Harmony patch/unpatch test case for every newly claimed string" in rules
