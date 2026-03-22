"""Tests for the scanner.ast_grep_runner module."""

from collections import Counter
from pathlib import Path

from scripts.scanner.ast_grep_runner import (
    OVERRIDE_PRODUCER_SPECS,
    SINK_FAMILY_SPECS,
    collect_source_inventory,
    scan_source_tree,
)
from scripts.scanner.inventory import ExclusionReason, HitKind, read_raw_hits_jsonl

FIXTURE_ROOT = Path(__file__).parent / "fixtures" / "scanner"


class TestSourceInventory:
    """Tests for Phase 1a file deduplication."""

    def test_collect_source_inventory_applies_phase_1a_deduplication(self) -> None:
        """Empty files, retry/msgprobe files, and flat namespace duplicates are excluded."""
        inventory = collect_source_inventory(FIXTURE_ROOT)

        assert inventory.included_file_count == 5
        assert {
            record.path
            for record in inventory.included_files
        } == {
            "Demo/PatternCoverage.cs",
            "XRL.World.Effects/TestEffect.cs",
            "XRL.World.Parts.Mutation/TestMutation.cs",
            "XRL.World.Parts/TestPart.cs",
            "XRL.World/GameObject.cs",
        }

        excluded = {record.path: record for record in inventory.excluded_files}
        assert excluded["Empty.cs"].exclusion_reason is ExclusionReason.EMPTY_FILE
        assert excluded["XRL_UI_Popup.retry.cs"].exclusion_reason is ExclusionReason.RETRY_ARTIFACT
        assert excluded["XRL_Messages_Message.msgprobe.cs"].exclusion_reason is ExclusionReason.MSGPROBE_ARTIFACT
        assert excluded["XRL.World.GameObject.cs"].exclusion_reason is ExclusionReason.FLAT_NAMESPACE_DUPLICATE
        assert excluded["XRL_World_GameObject.cs"].exclusion_reason is ExclusionReason.FLAT_NAMESPACE_DUPLICATE
        assert excluded["XRL.World.GameObject.cs"].duplicate_of == "XRL.World/GameObject.cs"
        assert excluded["XRL_World_GameObject.cs"].duplicate_of == "XRL.World/GameObject.cs"


class TestScanSourceTree:
    """Tests for Phase 1a ast-grep and override extraction."""

    def test_configuration_matches_design_doc(self) -> None:
        """The configured scanner surface matches the design spec for Phase 1a."""
        assert len(SINK_FAMILY_SPECS) == 11
        assert sum(len(spec.patterns) for spec in SINK_FAMILY_SPECS) == 33
        assert len(OVERRIDE_PRODUCER_SPECS) == 5

    def test_scan_source_tree_extracts_sink_hits_and_override_hits(self, tmp_path: Path) -> None:
        """The scanner finds sink families, deduplicates source files, and writes JSONL cache files."""
        result = scan_source_tree(FIXTURE_ROOT, cache_dir=tmp_path)

        assert result.source_inventory.included_file_count == 5
        assert len(result.raw_hits) == 34
        assert len(result.override_hits) == 5

        sink_counts = Counter(hit.family for hit in result.raw_hits)
        assert sink_counts == {
            "AddPlayerMessage": 2,
            "DidX": 9,
            "Does": 1,
            "EmitMessage": 3,
            "GetDisplayName": 1,
            "GetShort/LongDescription": 2,
            "HistoricStringExpander": 1,
            "JournalAPI": 3,
            "Popup": 10,
            "ReplaceBuilder": 1,
            "SetText": 1,
        }

        override_counts = Counter(hit.family for hit in result.override_hits)
        assert override_counts == {
            "Effects.GetDescription": 1,
            "Effects.GetDetails": 1,
            "Mutations.GetDescription": 1,
            "Mutations.GetLevelText": 1,
            "Parts.GetShortDescription": 1,
        }

        assert not any("dot duplicate" in hit.matched_code for hit in result.raw_hits)
        assert not any("underscore duplicate" in hit.matched_code for hit in result.raw_hits)
        assert any("directory winner" in hit.matched_code for hit in result.raw_hits)
        assert {hit.hit_kind for hit in result.raw_hits} == {HitKind.SINK}
        assert {hit.hit_kind for hit in result.override_hits} == {HitKind.OVERRIDE}

        raw_hits_path = tmp_path / "raw_hits.jsonl"
        override_hits_path = tmp_path / "override_hits.jsonl"
        assert raw_hits_path.exists()
        assert override_hits_path.exists()
        assert read_raw_hits_jsonl(raw_hits_path) == result.raw_hits
        assert read_raw_hits_jsonl(override_hits_path) == result.override_hits
