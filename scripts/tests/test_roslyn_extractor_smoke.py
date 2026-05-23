"""Smoke tests for repo-local Roslyn tool projects."""
# ruff: noqa: S603,S607 -- tests invoke dotnet (PATH-resolved) to drive the repo-local tool

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import cast

import pytest

from scripts.dotnet_tool_runner import build_tool_project

_REPO_ROOT = Path(__file__).resolve().parents[2]
ANNALS_PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "AnnalsPatternExtractor" / "AnnalsPatternExtractor.csproj"
STATIC_PRODUCER_PROJECT_PATH = (
    _REPO_ROOT
    / "scripts"
    / "tools"
    / "StaticProducerInventoryScanner"
    / "StaticProducerInventoryScanner.csproj"
)
SEMANTIC_PROBE_PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "RoslynSemanticProbe" / "RoslynSemanticProbe.csproj"
UNUSED_CODE_PROJECT_PATH = (
    _REPO_ROOT
    / "scripts"
    / "tools"
    / "UnusedCodeInventoryScanner"
    / "UnusedCodeInventoryScanner.csproj"
)
FIXTURES = Path(__file__).resolve().parent / "fixtures" / "annals"


@pytest.fixture(scope="session")
def annals_tool_dll() -> Path:
    """Build the Annals extractor once for smoke execution."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")
    return build_tool_project(ANNALS_PROJECT_PATH)


@pytest.fixture(scope="session")
def static_producer_tool_dll() -> Path:
    """Build the static producer scanner once for smoke validation."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")
    return build_tool_project(STATIC_PRODUCER_PROJECT_PATH)


@pytest.fixture(scope="session")
def semantic_probe_tool_dll() -> Path:
    """Build the semantic probe once for smoke validation."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")
    return build_tool_project(SEMANTIC_PROBE_PROJECT_PATH)


@pytest.fixture(scope="session")
def unused_code_tool_dll() -> Path:
    """Build the unused-code scanner once for smoke validation."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")
    return build_tool_project(UNUSED_CODE_PROJECT_PATH)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_extractor_csproj_builds_in_release(annals_tool_dll: Path) -> None:
    """The Roslyn extractor csproj must build cleanly so the CI step does not rot."""
    assert annals_tool_dll.is_file()


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_static_producer_inventory_scanner_csproj_builds_in_release(static_producer_tool_dll: Path) -> None:
    """The Roslyn static producer scanner csproj must build cleanly."""
    assert static_producer_tool_dll.is_file()


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_semantic_probe_csproj_builds_in_release(semantic_probe_tool_dll: Path) -> None:
    """The Roslyn semantic probe csproj must build cleanly."""
    assert semantic_probe_tool_dll.is_file()


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_unused_code_inventory_scanner_csproj_builds_in_release(unused_code_tool_dll: Path) -> None:
    """The Roslyn unused-code scanner csproj must build cleanly."""
    assert unused_code_tool_dll.is_file()


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_threeplus_arm_chain_does_not_collide_with_sibling_if(tmp_path: Path, annals_tool_dll: Path) -> None:
    """3+-arm else-if chains must produce arm-distinct ids that don't collide with sibling ifs.

    Regression guard for issue-430 follow-up (ChallengeSultan): when a 3-arm chain drives a
    branched local for a setter that has NO `ResolveIfBranchSuffix` of its own, while a
    sibling 2-arm `if/else` carries setters that DO get `#if:then` / `#if:else`, the pre-fix
    extractor labelled BOTH paths with `then`/`else` and the dedupe pass bailed out with
    "duplicate candidate id with divergent outcome: ...#gospel#if:then".

    Post-fix, the 3-arm chain emits `case0` / `case1` / `case2` and the 2-arm chain keeps
    `then` / `else`, so the five gospel candidates have distinct ids. This is a true smoke
    test (extractor exits 0) on top of the golden-file equality test that the auto-discovered
    fixture parametrize set already runs.
    """
    output = tmp_path / "elseif_chain_collision_with_sibling_if.json"
    result = subprocess.run(
        [
            "dotnet",
            str(annals_tool_dll),
            "--source-root",
            str(FIXTURES),
            "--include",
            "elseif_chain_collision_with_sibling_if.cs",
            "--output",
            str(output),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, (
        "extractor must extract ChallengeSultan-style chains without bailing on "
        f"duplicate-id collision. exit={result.returncode}\n"
        f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )
    actual = cast("dict[str, object]", json.loads(output.read_text(encoding="utf-8")))
    candidates = cast("list[dict[str, object]]", actual["candidates"])
    ids = [str(candidate["id"]) for candidate in candidates]
    # Three case-labelled arms from the 3-arm chain (branched-local fanout
    # uses `#bl:` to avoid collision with setter-chain `#if:`) plus the
    # legacy then/else from the 2-arm sibling if (setter-chain).
    # Pin the FULL set so a regression that emits extra candidates or
    # duplicates an id cannot pass with only membership checks.
    expected_ids = {
        "elseif_chain_collision_with_sibling_if#gospel#bl:case0",
        "elseif_chain_collision_with_sibling_if#gospel#bl:case1",
        "elseif_chain_collision_with_sibling_if#gospel#bl:case2",
        "elseif_chain_collision_with_sibling_if#gospel#if:then",
        "elseif_chain_collision_with_sibling_if#gospel#if:else",
    }
    assert set(ids) == expected_ids, f"unexpected ids: {sorted(set(ids) ^ expected_ids)}"
    assert len(ids) == len(expected_ids), f"duplicate ids in output: {ids}"


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_flatten_concat_partial_rollback(tmp_path: Path, annals_tool_dll: Path) -> None:
    """FlattenConcat must roll back stale pieces when a sub-expression fails.

    Regression guard for A1 (Devin finding): when a local variable's initializer
    is a binary concat whose right-hand side is an unsupported expression (e.g.
    SomeClass.UnsupportedMethod()), FlattenConcat previously left the left-hand
    literal in `pieces` before degrading to a slot.  After the fix, the entire
    variable degrades to a single slot and the stale literal is removed.

    Fixture: string a = "lit" + SomeClass.UnsupportedMethod() + "rest";
             SetEventProperty("gospel", a + " world");
    Expected sample_source: "{0} world"  (single slot for `a`)
    Bug output would have been: "lit{0} world"  (stale "lit" piece from failed recursion)
    """
    output = tmp_path / "partial_rollback.json"
    result = subprocess.run(
        [
            "dotnet",
            str(annals_tool_dll),
            "--source-root",
            str(FIXTURES),
            "--include",
            "partial_rollback.cs",
            "--output",
            str(output),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, (
        f"extractor failed (exit {result.returncode}). stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )
    actual = cast("dict[str, object]", json.loads(output.read_text(encoding="utf-8")))
    expected = cast(
        "dict[str, object]",
        json.loads((FIXTURES / "expected_partial_rollback.json").read_text(encoding="utf-8")),
    )
    actual_candidates = cast("list[dict[str, object]]", actual["candidates"])
    assert actual == expected, (
        "FlattenConcat rollback produced unexpected output.\n"
        f"sample_source: {actual_candidates[0]['sample_source']!r}\n"
        "(expected '{0} world', stale-piece bug would produce 'lit{0} world')"
    )
