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
