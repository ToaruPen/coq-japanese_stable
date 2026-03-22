"""L2 integration tests for the scan_text_producers orchestrator."""

from __future__ import annotations

from collections import Counter
from pathlib import Path
from typing import TYPE_CHECKING

from scripts.scan_text_producers import main
from scripts.scanner.inventory import (
    SiteStatus,
    SiteType,
    read_candidate_inventory_json,
    read_inventory_draft_json,
    read_raw_hits_jsonl,
)

FIXTURE_ROOT = Path(__file__).parent / "fixtures" / "scanner"

if TYPE_CHECKING:
    import pytest


def test_main_runs_full_pipeline_on_fixture(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """The orchestrator wires phases 1a, 1b, and 1d end-to-end on fixture sources."""
    cache_dir = tmp_path / ".scanner-cache"
    output_path = tmp_path / "candidate-inventory.json"

    result = main(
        [
            "--source-root",
            str(FIXTURE_ROOT),
            "--cache-dir",
            str(cache_dir),
            "--output",
            str(output_path),
        ]
    )
    captured = capsys.readouterr()

    assert result == 0
    assert (cache_dir / "raw_hits.jsonl").exists()
    assert (cache_dir / "override_hits.jsonl").exists()
    assert (cache_dir / "inventory_draft.json").exists()
    assert output_path.exists()

    raw_hits = read_raw_hits_jsonl(cache_dir / "raw_hits.jsonl")
    override_hits = read_raw_hits_jsonl(cache_dir / "override_hits.jsonl")
    draft = read_inventory_draft_json(cache_dir / "inventory_draft.json")
    candidate = read_candidate_inventory_json(output_path)

    assert len(raw_hits) == 34
    assert len(override_hits) == 5
    assert draft.stats.input_hits == 39
    assert draft.stats.output_sites == 39
    assert len(candidate.sites) == 39

    type_counts = Counter(site.type for site in candidate.sites)
    assert type_counts == {
        SiteType.LEAF: 16,
        SiteType.MESSAGE_FRAME: 9,
        SiteType.UNRESOLVED: 7,
        SiteType.NARRATIVE_TEMPLATE: 3,
        SiteType.VERB_COMPOSITION: 1,
        SiteType.BUILDER: 1,
        SiteType.PROCEDURAL_TEXT: 1,
        SiteType.VARIABLE_TEMPLATE: 1,
    }

    status_counts = Counter(site.status for site in candidate.sites)
    assert status_counts == {
        SiteStatus.TRANSLATED: 13,
        SiteStatus.NEEDS_PATCH: 9,
        SiteStatus.UNRESOLVED: 7,
        SiteStatus.NEEDS_TRANSLATION: 5,
        SiteStatus.NEEDS_REVIEW: 4,
        SiteStatus.EXCLUDED: 1,
    }

    assert "total sites: 39" in captured.out
    assert "translated: 13" in captured.out


def test_main_supports_individual_phases_and_skips_1c(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """The CLI can run cached phases individually and reserves 1c as an interactive step."""
    cache_dir = tmp_path / ".scanner-cache"
    output_path = tmp_path / "candidate-inventory.json"
    shared_args = [
        "--source-root",
        str(FIXTURE_ROOT),
        "--cache-dir",
        str(cache_dir),
        "--output",
        str(output_path),
    ]

    assert main([*shared_args, "--phase", "1a"]) == 0
    phase_1a = capsys.readouterr()
    assert "phase 1a" in phase_1a.out.lower()
    assert (cache_dir / "raw_hits.jsonl").exists()
    assert (cache_dir / "override_hits.jsonl").exists()
    assert not (cache_dir / "inventory_draft.json").exists()
    assert not output_path.exists()

    assert main([*shared_args, "--phase", "1b"]) == 0
    phase_1b = capsys.readouterr()
    assert "phase 1b" in phase_1b.out.lower()
    assert (cache_dir / "inventory_draft.json").exists()
    assert not output_path.exists()

    assert main([*shared_args, "--phase", "1c"]) == 0
    phase_1c = capsys.readouterr()
    assert "interactive" in phase_1c.out.lower()
    assert "not implemented" in phase_1c.out.lower()
    assert not output_path.exists()

    assert main([*shared_args, "--phase", "1d"]) == 0
    phase_1d = capsys.readouterr()
    assert "phase 1d" in phase_1d.out.lower()
    assert "translated: 13" in phase_1d.out
    assert output_path.exists()
