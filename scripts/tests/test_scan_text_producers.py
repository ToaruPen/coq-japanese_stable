"""L2 integration tests for the scan_text_producers orchestrator."""

from __future__ import annotations

from collections import Counter
from pathlib import Path
from typing import Protocol

import pytest  # pyright: ignore[reportMissingImports]

from scripts.legacies.scan_text_producers import main
from scripts.legacies.scanner.inventory import (
    DestinationDictionary,
    FixedLeafRejectionReason,
    SiteStatus,
    SiteType,
    read_candidate_inventory_json,
    read_inventory_draft_json,
    read_raw_hits_jsonl,
)

FIXTURE_ROOT = Path(__file__).parent / "fixtures" / "scanner"


class _CapturedOutput(Protocol):
    out: str
    err: str


class _Capsys(Protocol):
    def readouterr(self) -> _CapturedOutput: ...


def _write_good_fixture(source_root: Path) -> None:
    """Create a minimal source fixture whose fixed-leaf validation should pass."""
    source_path = source_root / "Demo" / "ValidPatternCoverage.cs"
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_text(
        """namespace Demo
{
    public sealed class ValidPatternCoverage
    {
        private readonly UIText screen = new();

        public void Run()
        {
            screen.SetText("valid-set-text");
        }
    }

    public sealed class UIText
    {
        public void SetText(string value)
        {
        }
    }
}
""",
        encoding="utf-8",
    )


def test_main_runs_full_pipeline_on_fixture(tmp_path: Path, capsys: _Capsys) -> None:
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
    assert draft.stats.proven_fixed_leaf == 16
    assert draft.stats.rejected_fixed_leaf == 23
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
        SiteStatus.NEEDS_TRANSLATION: 4,
        SiteStatus.NEEDS_REVIEW: 5,
        SiteStatus.EXCLUDED: 1,
    }

    destination_counts = Counter(site.destination_dictionary for site in candidate.sites)
    assert destination_counts == {
        DestinationDictionary.SCOPED: 12,
        DestinationDictionary.GLOBAL_FLAT: 4,
        None: 23,
    }

    rejection_counts = Counter(site.rejection_reason for site in candidate.sites)
    assert rejection_counts == {
        None: 16,
        FixedLeafRejectionReason.MESSAGE_FRAME: 9,
        FixedLeafRejectionReason.UNRESOLVED: 7,
        FixedLeafRejectionReason.NARRATIVE_TEMPLATE: 3,
        FixedLeafRejectionReason.VERB_COMPOSITION: 1,
        FixedLeafRejectionReason.BUILDER_DISPLAY_NAME: 1,
        FixedLeafRejectionReason.PROCEDURAL: 1,
        FixedLeafRejectionReason.VARIABLE_TEMPLATE: 1,
    }

    assert "total sites: 39" in captured.out
    assert "translated: 13" in captured.out


def test_main_supports_individual_phases_and_skips_1c(tmp_path: Path, capsys: _Capsys) -> None:
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


def test_main_validate_fixed_leaf_passes_on_consistent_fixture(tmp_path: Path, capsys: _Capsys) -> None:
    """The CLI validation mode succeeds when the fixture inventory already matches scanner rules."""
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
            "--validate-fixed-leaf",
        ]
    )
    captured = capsys.readouterr()
    candidate = read_candidate_inventory_json(output_path)

    assert result == 0
    assert output_path.exists()
    assert sum(site.destination_dictionary is DestinationDictionary.SCOPED for site in candidate.sites) == 12
    assert "Fixed-leaf validation passed" in captured.out
    assert "0 issue(s)" in captured.out
    assert "Traceback" not in captured.out
    assert captured.err == ""


def test_main_validate_fixed_leaf_succeeds_on_good_fixture(tmp_path: Path, capsys: _Capsys) -> None:
    """The CLI validation mode exits cleanly when a fixture inventory follows fixed-leaf rules."""
    source_root = tmp_path / "good-source"
    cache_dir = tmp_path / ".scanner-cache"
    output_path = tmp_path / "candidate-inventory.json"
    _write_good_fixture(source_root)

    result = main(
        [
            "--source-root",
            str(source_root),
            "--cache-dir",
            str(cache_dir),
            "--output",
            str(output_path),
            "--validate-fixed-leaf",
        ]
    )
    captured = capsys.readouterr()
    candidate = read_candidate_inventory_json(output_path)

    assert result == 0
    assert output_path.exists()
    assert len(candidate.sites) == 1
    assert candidate.sites[0].key == "valid-set-text"
    assert candidate.sites[0].destination_dictionary is DestinationDictionary.GLOBAL_FLAT
    assert "Fixed-leaf validation passed" in captured.out
    assert "0 issue(s)" in captured.out
    assert "Traceback" not in captured.out
    assert captured.err == ""


def test_main_reraises_unexpected_execute_phase_value_errors(monkeypatch: pytest.MonkeyPatch) -> None:
    """Unexpected phase-execution ValueErrors should propagate instead of being flattened into CLI usage errors."""
    msg = "unexpected classifier failure"

    def _raise_unexpected_error(*_args: object, **_kwargs: object) -> None:
        raise ValueError(msg)

    monkeypatch.setattr("scripts.legacies.scan_text_producers._execute_phase", _raise_unexpected_error)

    with pytest.raises(ValueError, match=msg):
        main([])


def test_main_help_marks_legacy_candidate_inventory_as_bridge_view_only(capsys: _Capsys) -> None:
    """CLI help should describe legacy scanner output as bridge/view-only rather than source of truth."""
    with pytest.raises(SystemExit, match="0"):
        main(["--help"])

    captured = capsys.readouterr()

    assert "bridge/view-only" in captured.out
    assert "source of truth" in captured.out
