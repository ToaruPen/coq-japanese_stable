"""Static contracts for QudJP NUnit test fixtures."""

from __future__ import annotations

import os
import re
import shlex
import shutil
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
JUSTFILE = REPO_ROOT / "justfile"
QUDJP_CSPROJ = REPO_ROOT / "Mods" / "QudJP" / "Assemblies" / "QudJP.csproj"
QUDTEST_HEADLESS_CSPROJ = REPO_ROOT / "scripts" / "tools" / "QudTestHeadless" / "QudTestHeadless.csproj"
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
    target_version = _recipe_block(
        justfile, "target-game-version-check", "quest-step-contract-check"
    )
    quest_step_contract = _recipe_block(
        justfile, "quest-step-contract-check", "game-version-check"
    )
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
    assert (
        'coq_base_quests := env_var_or_default("COQ_BASE_QUESTS", env_var("HOME") + '
        '"/Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/StreamingAssets/'
        'Base/Quests.xml")'
        in justfile
    )
    assert (
        "uv run python scripts/validate_quest_step_contract.py {{quote(coq_base_quests)}} "
        "Mods/QudJP/Localization/Quests.jp.xml"
        in quest_step_contract
    )

    executable_steps = [
        line.strip()
        for line in game_version.splitlines()
        if line.startswith("  ") and not line.lstrip().startswith("#")
    ]
    assert executable_steps[:2] == [
        "just target-game-version-check",
        "just quest-step-contract-check",
    ]

    expected_steps = (
        "just target-game-version-check",
        "just quest-step-contract-check",
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


def test_quest_step_contract_recipe_shell_quotes_base_path_override(tmp_path: Path) -> None:
    """The live Base XML override must preserve spaces and double quotes in one argument."""
    base_quests = str(tmp_path / 'Caves Of "Qud"' / "Base Quests.xml")
    just = shutil.which("just")
    assert just is not None
    result = subprocess.run(  # noqa: S603 -- intentionally probes shell quoting via just dry-run.
        [just, "--dry-run", "quest-step-contract-check"],
        cwd=REPO_ROOT,
        env={**os.environ, "COQ_BASE_QUESTS": base_quests},
        check=True,
        capture_output=True,
        text=True,
    )

    assert shlex.split(result.stderr.strip()) == [
        "uv",
        "run",
        "python",
        "scripts/validate_quest_step_contract.py",
        base_quests,
        "Mods/QudJP/Localization/Quests.jp.xml",
    ]


def test_qudtest_headless_references_assembly_csharp_stub_without_game_dll() -> None:
    """The headless harness must compile game-typed patches on CI runners."""
    csproj = QUDTEST_HEADLESS_CSPROJ.read_text(encoding="utf-8")
    no_game_group = re.search(
        r'<ItemGroup Condition="!Exists\(\'\$\(AssemblyCSharpPath\)\'\)">(?P<body>.*?)</ItemGroup>',
        csproj,
        re.DOTALL,
    )

    assert no_game_group is not None
    assert (
        '<ProjectReference Include="../../../Mods/QudJP/Assemblies/ReferenceStubs/'
        'Assembly-CSharp/Assembly-CSharp.csproj" />'
        in no_game_group.group("body")
    )


def test_route_family_test_guidance_limits_l2_case_growth() -> None:
    """Route-family additions should not default to one Harmony test per string."""
    test_architecture = TEST_ARCHITECTURE_DOC.read_text(encoding="utf-8")
    rules = RULES_DOC.read_text(encoding="utf-8")

    assert "1 回の Harmony patch setup にまとめて batch 実行する" in test_architecture
    assert "静的 inventory や data-contract coverage" in test_architecture
    assert "one L2 smoke case" in rules
    assert "Do not add one new Harmony patch/unpatch test case for every newly claimed string" in rules
