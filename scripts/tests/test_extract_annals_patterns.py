"""Golden tests for the AnnalsPatternExtractor C# tool."""
# ruff: noqa: S603,S607 -- tests invoke dotnet (PATH-resolved) to drive the repo-local tool

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import Protocol, cast

import pytest

from scripts.dotnet_tool_runner import build_tool_project

_REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "AnnalsPatternExtractor" / "AnnalsPatternExtractor.csproj"
FIXTURES = Path(__file__).resolve().parent / "fixtures" / "annals"


class AnnalsTranslator(Protocol):
    """Subset of translate_annals_patterns used by these tests."""

    def compute_en_template_hash(self, candidate: dict[str, object]) -> str:
        """Compute the stable template hash for an annals candidate."""
        ...


def _has_dotnet_10_sdk() -> bool:
    """Return whether the .NET 10 SDK needed to build the extractor is available."""
    if not shutil.which("dotnet"):
        return False

    result = subprocess.run(["dotnet", "--list-sdks"], capture_output=True, text=True, check=False)
    if result.returncode != 0:
        return False
    return any(line.startswith("10.") for line in result.stdout.splitlines())


def _ensure_extractor_dll() -> Path:
    """Build or reuse the extractor through the shared repo-local tool cache."""
    if not _has_dotnet_10_sdk():
        pytest.skip("dotnet 10.0 SDK not available")
    return build_tool_project(PROJECT_PATH)


def _run_extractor_batch(output: Path) -> subprocess.CompletedProcess[str]:
    """Invoke the built AnnalsPatternExtractor against all fixture files in one process."""
    tool_dll = _ensure_extractor_dll()
    return subprocess.run(
        [
            "dotnet",
            str(tool_dll),
            "--source-root",
            str(FIXTURES),
            "--include",
            "*.cs",
            "--output",
            str(output),
        ],
        capture_output=True,
        text=True,
        check=False,
    )


def _discover_fixtures() -> list[str]:
    """Discover fixture names from `expected_*.json` files paired with a sibling `.cs` file.

    Auto-discovery prevents the parametrize list from rotting when new fixtures are added.
    Sorted to keep test-run order deterministic. Fails loud at import time if the fixture
    directory is empty — pytest 8.x silently `skip`s an empty `parametrize` list, which
    would let CI go green even with no extractor coverage.

    Also fails loud on orphans in either direction: every `expected_*.json` must have a
    sibling `<name>.cs`, and every `<name>.cs` (excluding the canonical `expected_*.json`
    pairing) must have a sibling `expected_<name>.json`. CR R8: silently dropping
    orphans lets a fixture go untested or an expected golden go unbacked.
    """
    expected_names = {p.name.removeprefix("expected_").removesuffix(".json") for p in FIXTURES.glob("expected_*.json")}
    source_names = {p.stem for p in FIXTURES.glob("*.cs")}

    missing_source = expected_names - source_names
    missing_expected = source_names - expected_names
    if missing_source or missing_expected:
        details: list[str] = []
        if missing_source:
            details.append("expected_*.json without sibling .cs: " + ", ".join(sorted(missing_source)))
        if missing_expected:
            details.append(".cs without sibling expected_*.json: " + ", ".join(sorted(missing_expected)))
        msg = f"orphaned annals extractor fixtures under {FIXTURES}: " + "; ".join(details)
        raise RuntimeError(msg)

    fixtures = sorted(expected_names & source_names)
    if not fixtures:
        msg = f"no annals extractor fixtures discovered under {FIXTURES}"
        raise RuntimeError(msg)
    return fixtures


_FIXTURES = _discover_fixtures()


def _load_expected_document() -> dict[str, object]:
    """Combine per-fixture golden files into the same document shape as a batch extraction."""
    candidates: list[dict[str, object]] = []
    for fixture in _FIXTURES:
        doc = json.loads((FIXTURES / f"expected_{fixture}.json").read_text(encoding="utf-8"))
        assert doc["schema_version"] == "1", f"unexpected schema for expected_{fixture}.json"
        candidates.extend(doc["candidates"])

    return {
        "schema_version": "1",
        "candidates": sorted(candidates, key=lambda candidate: candidate["id"]),
    }


def _load_expected_fixture(fixture: str) -> dict[str, object]:
    return json.loads((FIXTURES / f"expected_{fixture}.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="session")
def annals_extraction(tmp_path_factory: pytest.TempPathFactory) -> dict[str, object]:
    """Run the C# extractor once for the entire golden suite."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")

    output = tmp_path_factory.mktemp("annals-extraction") / "all.json"
    result = _run_extractor_batch(output)
    assert result.returncode == 0, (
        f"extractor failed (exit {result.returncode}). stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )
    return json.loads(output.read_text(encoding="utf-8"))


@pytest.fixture(scope="session")
def annals_translator() -> AnnalsTranslator:
    """Load the Python translator module once for hash parity checks."""
    import importlib.util  # noqa: PLC0415

    script = _REPO_ROOT / "scripts" / "translate_annals_patterns.py"
    spec = importlib.util.spec_from_file_location("translate_annals_patterns", script)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return cast("AnnalsTranslator", module)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_extractor_matches_golden(annals_extraction: dict[str, object]) -> None:
    """Batch extractor output must match the committed golden JSON exactly."""
    expected = _load_expected_document()

    # Schema sanity (will catch if golden was regenerated against a broken extractor)
    assert annals_extraction["schema_version"] == "1"
    assert "candidates" in annals_extraction

    # Direct equality. If the extractor changes output shape, the golden must be regenerated.
    assert annals_extraction == expected, "batch extractor output diverged from golden fixtures"


@pytest.mark.parametrize("fixture", _FIXTURES)
def test_csharp_and_python_hashes_match(fixture: str, annals_translator: AnnalsTranslator) -> None:
    """The C# extractor and Python translator must compute the same en_template_hash."""
    doc = _load_expected_fixture(fixture)
    candidates = cast("list[dict[str, object]]", doc["candidates"])
    for candidate in candidates:
        csharp_hash = candidate["en_template_hash"]
        python_hash = annals_translator.compute_en_template_hash(candidate)
        assert csharp_hash == python_hash, (
            f"Hash divergence for {fixture} {candidate['id']}: C#={csharp_hash}, py={python_hash}"
        )
