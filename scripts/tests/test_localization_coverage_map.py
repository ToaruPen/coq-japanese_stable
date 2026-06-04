from __future__ import annotations

import json
from pathlib import Path

from scripts.localization_coverage_map import (
    REQUIRED_SURFACE_IDS,
    load_map,
    validate_map,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
MAP_PATH = REPO_ROOT / "docs" / "localization-coverage-map.json"


def test_localization_coverage_map_is_valid_and_complete() -> None:
    """Coverage map must stay machine-valid and include every required surface lane."""
    errors = validate_map(REPO_ROOT)

    assert errors == []


def test_localization_coverage_map_defines_true_untranslated_zero_closeout() -> None:
    """The map must define what evidence is required before claiming zero untranslated text."""
    document = load_map(MAP_PATH)
    definition = document.get("true_untranslated_zero_definition")
    assert definition is not None

    assert "player-visible" in definition["statement"]
    assert "without fresh runtime evidence" in definition["statement"]
    assert len(definition["required_proofs"]) >= 3
    assert any("runtime" in proof for proof in definition["required_proofs"])
    assert any("sink" in proof for proof in definition["disallowed_proofs"])


def test_localization_coverage_map_reports_invalid_true_zero_definition_fields(tmp_path: Path) -> None:
    """Coverage-map validation must report malformed true-zero metadata without crashing."""
    document: dict[str, object] = {
        "schema_version": "1.0",
        "game_version": "1.0.4",
        "true_untranslated_zero_definition": {
            "required_proofs": "runtime evidence",
            "disallowed_proofs": [1],
        },
        "surfaces": [],
    }
    _ = (tmp_path / "map.json").write_text(json.dumps(document), encoding="utf-8")

    errors = validate_map(tmp_path, Path("map.json"))

    assert "true_untranslated_zero_definition.statement is missing" in errors
    assert "true_untranslated_zero_definition.required_proofs must be a non-empty list" in errors
    assert "true_untranslated_zero_definition.disallowed_proofs must be a non-empty list" in errors


def test_localization_coverage_map_surfaces_have_closeout_contracts() -> None:
    """Every surface lane must name its owner and the evidence gate that closes it."""
    document = load_map(MAP_PATH)

    for surface in document["surfaces"]:
        assert surface["closure_owner"]
        assert surface["closure_gate_type"]
        assert surface["closure_evidence"]


def test_localization_coverage_map_keeps_runtime_and_sink_boundary_lanes_explicit() -> None:
    """The map must keep runtime and sink-boundary lanes separate from static coverage."""
    document = load_map(MAP_PATH)
    surfaces = {surface["id"]: surface for surface in document["surfaces"]}

    assert set(surfaces) >= REQUIRED_SURFACE_IDS
    assert "blueprint_xml_data_sources" not in surfaces
    assert surfaces["runtime_observability_triage"]["status"] == "runtime_evidence"
    assert surfaces["renderer_and_sink_boundaries"]["status"] == "boundary_observed"


def test_localization_coverage_map_tracks_issue809_without_quest_title_or_static_queue_overclaim() -> None:
    """Issue #809 needs a cross-surface audit lane that does not overclaim static closure."""
    document = load_map(MAP_PATH)
    surfaces = {surface["id"]: surface for surface in document["surfaces"]}

    issue809 = surfaces["issue_809_authored_runtime_text_audit"]
    combined_limits = " ".join(issue809["known_limits"])
    issue809_tests = issue809.get("tests", [])

    assert "docs/reports/2026-06-02-issue-809-text-surface-audit.md" in issue809["closure_evidence"]
    assert "docs/issue-809-authored-text-inventory.json" in issue809["closure_evidence"]
    assert "scripts/tests/test_issue809_authored_text_inventory.py" in issue809_tests
    assert "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs" in issue809_tests
    assert "QuestTitle" not in issue809["target_surfaces"]
    assert "quest titles are intentionally excluded" in combined_limits
    assert "DynamicQuestRewardElement_ChoiceFromPopulation reward options" in combined_limits
    assert "&WxN quantity suffixes" in combined_limits
    assert "Static producer queue zero does not close" in combined_limits
    assert "do not have a complete tracked inventory" not in combined_limits
    assert "still need emitted-shape tests" not in combined_limits


def test_localization_coverage_map_does_not_treat_legacy_inventory_as_source_of_truth() -> None:
    """Legacy bridge artifacts must remain explicitly view-only."""
    document = load_map(MAP_PATH)
    legacy = next(surface for surface in document["surfaces"] if surface["id"] == "legacy_candidate_inventory")

    assert legacy["status"] == "legacy_view_only"
    assert legacy["category"] == "legacy_view"
    assert "not a source of truth" in " ".join(legacy["known_limits"])
