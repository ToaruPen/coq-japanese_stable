"""Smoke test: AnnalsPatternExtractor csproj builds in Release."""
# ruff: noqa: S603,S607 -- tests invoke dotnet (PATH-resolved) to drive the repo-local tool

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path

import pytest

_REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "AnnalsPatternExtractor" / "AnnalsPatternExtractor.csproj"
FIXTURES = Path(__file__).resolve().parent / "fixtures" / "annals"


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_extractor_csproj_builds_in_release() -> None:
    """The Roslyn extractor csproj must build cleanly so the CI step does not rot."""
    result = subprocess.run(
        ["dotnet", "build", str(PROJECT_PATH), "--configuration", "Release"],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, (
        f"dotnet build failed (exit {result.returncode}).\nstdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_threeplus_arm_chain_does_not_collide_with_sibling_if(tmp_path: Path) -> None:
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
            "run",
            "--project",
            str(PROJECT_PATH),
            "--",
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
    actual = json.loads(output.read_text(encoding="utf-8"))
    ids = [c["id"] for c in actual["candidates"]]
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
def test_flatten_concat_partial_rollback(tmp_path: Path) -> None:
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
            "run",
            "--project",
            str(PROJECT_PATH),
            "--",
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
    actual = json.loads(output.read_text(encoding="utf-8"))
    expected = json.loads((FIXTURES / "expected_partial_rollback.json").read_text(encoding="utf-8"))
    assert actual == expected, (
        "FlattenConcat rollback produced unexpected output.\n"
        f"sample_source: {actual['candidates'][0]['sample_source']!r}\n"
        "(expected '{0} world', stale-piece bug would produce 'lit{0} world')"
    )
