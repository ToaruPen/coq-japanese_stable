"""Tests for the scanner.inventory module."""

from pathlib import Path

import scripts.legacies.scanner.inventory as inventory_module
from scripts.legacies.scanner.inventory import (
    Confidence,
    DestinationDictionary,
    ExclusionReason,
    FileRecord,
    FixedLeafRejectionReason,
    HitKind,
    InventoryDraft,
    InventorySite,
    InventoryStats,
    OwnershipClass,
    RawHit,
    SiteStatus,
    SiteType,
    SourceFileInventory,
    default_destination_dictionary_for_route,
    read_inventory_draft_json,
    read_raw_hits_jsonl,
    write_inventory_draft_json,
    write_raw_hits_jsonl,
)


class TestRawHitsJsonl:
    """Tests for RawHit JSONL persistence."""

    def test_round_trips_raw_hits_jsonl(self, tmp_path: Path) -> None:
        """Raw hits persist without losing fields or enum values."""
        hits = [
            RawHit(
                hit_kind=HitKind.SINK,
                family="Popup",
                pattern="Popup.Show($$$)",
                file="Demo/PatternCoverage.cs",
                line=19,
                column=9,
                matched_code='Popup.Show("hello")',
            ),
            RawHit(
                hit_kind=HitKind.OVERRIDE,
                family="Effects.GetDescription",
                pattern=r"override\b.*\bGetDescription\s*\(",
                file="XRL.World.Effects/TestEffect.cs",
                line=10,
                column=28,
                matched_code="public override string GetDescription()",
            ),
        ]

        output_path = tmp_path / "raw_hits.jsonl"
        write_raw_hits_jsonl(output_path, hits)

        assert read_raw_hits_jsonl(output_path) == hits


class TestSourceFileInventory:
    """Tests for SourceFileInventory helpers."""

    def test_included_and_excluded_views_are_stable(self) -> None:
        """Inventory exposes deterministic included and excluded file records."""
        inventory = SourceFileInventory(
            files=(
                FileRecord(path="keep.cs", included=True),
                FileRecord(
                    path="drop.retry.cs",
                    included=False,
                    exclusion_reason=ExclusionReason.RETRY_ARTIFACT,
                ),
            ),
        )

        assert inventory.included_file_count == 1
        assert [record.path for record in inventory.included_files] == ["keep.cs"]
        assert [record.path for record in inventory.excluded_files] == ["drop.retry.cs"]

    def test_first_pr_static_consumer_boundary_stays_static_only(self) -> None:
        """Task 4 freezes the first-PR boundary to pilot-aware, bridge-only, and deferred buckets."""
        assert hasattr(inventory_module, "FIRST_PR_STATIC_CONSUMER_BOUNDARY")

        boundary = inventory_module.FIRST_PR_STATIC_CONSUMER_BOUNDARY

        assert tuple(boundary) == ("pilot-aware", "bridge-only", "deferred")
        assert boundary["pilot-aware"] == (
            "Roslyn pilot schema contract",
            "Roslyn pilot verification/tests",
        )
        assert boundary["bridge-only"] == (
            "scripts/legacies/scanner/inventory.py stable/queryable view surface",
            "scripts/legacies/scanner/cross_reference.py legacy scanner candidate-inventory bridge/view-only consumer",
            (
                "scripts/legacies/reconcile_inventory_status.py "
                "legacy scanner candidate-inventory bridge/view-only consumer"
            ),
        )
        assert boundary["deferred"] == (
            "runtime observability consumers",
            "scripts/triage/*",
            "Phase F and unresolved full static consumer migration",
        )
        assert all("runtime" not in consumer for consumer in boundary["pilot-aware"])
        assert all("scripts/triage" not in consumer for consumer in boundary["pilot-aware"])
        assert all("runtime" not in consumer for consumer in boundary["bridge-only"])
        assert all("scripts/triage" not in consumer for consumer in boundary["bridge-only"])


class TestInventoryDraftJson:
    """Tests for InventoryDraft JSON persistence."""

    def test_round_trips_inventory_draft_with_fixed_leaf_provenance(self, tmp_path: Path) -> None:
        """Inventory drafts persist explicit fixed-leaf provenance fields and counts."""
        draft = InventoryDraft(
            version="1.0",
            game_version="2.0.4",
            scan_date="2026-04-11",
            stats=InventoryStats(
                input_hits=2,
                filtered_hits=0,
                output_sites=2,
                high_confidence=2,
                medium_confidence=0,
                low_confidence=0,
                needs_review=1,
                needs_runtime=0,
                proven_fixed_leaf=1,
                rejected_fixed_leaf=1,
            ),
            sites=(
                InventorySite(
                    id="leaf-site",
                    file="Qud.UI/TestScreen.cs",
                    line=10,
                    column=5,
                    sink="Popup",
                    source_route="Popup",
                    type=SiteType.LEAF,
                    confidence=Confidence.HIGH,
                    pattern='Popup.Show("Leave")',
                    key="Leave",
                    ownership_class=OwnershipClass.MID_PIPELINE_OWNED,
                    destination_dictionary=DestinationDictionary.GLOBAL_FLAT,
                    status=SiteStatus.NEEDS_TRANSLATION,
                ),
                InventorySite(
                    id="builder-site",
                    file="XRL.World/Test.cs",
                    line=20,
                    column=7,
                    sink="SetText",
                    source_route="SetText",
                    type=SiteType.BUILDER,
                    confidence=Confidence.HIGH,
                    pattern="label.SetText(part.GetDisplayName())",
                    ownership_class=OwnershipClass.PRODUCER_OWNED,
                    rejection_reason=FixedLeafRejectionReason.BUILDER_DISPLAY_NAME,
                    needs_review=True,
                    status=SiteStatus.NEEDS_REVIEW,
                ),
            ),
        )

        output_path = tmp_path / "inventory_draft.json"
        write_inventory_draft_json(output_path, draft)

        assert read_inventory_draft_json(output_path) == draft


class TestInventorySite:
    """Tests for InventorySite helpers."""

    def test_default_destination_keeps_popup_route_scoped(self) -> None:
        """Popup routes use a scoped dictionary even when only sink provenance is known."""
        assert (
            default_destination_dictionary_for_route(source_route="Popup")
            is DestinationDictionary.SCOPED
        )
        assert (
            default_destination_dictionary_for_route(source_route=None, sink="Popup")
            is DestinationDictionary.SCOPED
        )
        assert (
            default_destination_dictionary_for_route(source_route="SetText", sink="SetText")
            is DestinationDictionary.GLOBAL_FLAT
        )

    def test_is_proven_fixed_leaf_requires_route_and_ownership_provenance(self) -> None:
        """A proven fixed-leaf candidate must keep both source-route and ownership provenance."""
        base_fields = {
            "id": "leaf-site",
            "file": "Qud.UI/TestScreen.cs",
            "line": 10,
            "column": 5,
            "sink": "Popup",
            "type": SiteType.LEAF,
            "confidence": Confidence.HIGH,
            "pattern": 'Popup.Show("Leave")',
            "key": "Leave",
            "destination_dictionary": DestinationDictionary.SCOPED,
            "rejection_reason": None,
            "needs_review": False,
            "needs_runtime": False,
        }

        missing_route = InventorySite(
            **base_fields,
            source_route=None,
            ownership_class=OwnershipClass.MID_PIPELINE_OWNED,
        )
        missing_ownership = InventorySite(
            **base_fields,
            source_route="Popup",
            ownership_class=None,
        )
        valid_site = InventorySite(
            **base_fields,
            source_route="Popup",
            ownership_class=OwnershipClass.MID_PIPELINE_OWNED,
        )

        assert missing_route.is_proven_fixed_leaf is False
        assert missing_ownership.is_proven_fixed_leaf is False
        assert valid_site.is_proven_fixed_leaf is True

    def test_popup_scoped_destination_can_hold_owner_proof_review(self) -> None:
        """Popup can keep scoped destination metadata while owner proof remains on hold."""
        site = InventorySite(
            id="popup-hold",
            file="Qud.UI/TestScreen.cs",
            line=10,
            column=5,
            sink="Popup",
            source_route="Popup",
            type=SiteType.LEAF,
            confidence=Confidence.HIGH,
            pattern='Popup.Show("Leave")',
            key="Leave",
            ownership_class=None,
            destination_dictionary=DestinationDictionary.SCOPED,
            rejection_reason=FixedLeafRejectionReason.NEEDS_REVIEW,
            needs_review=True,
            status=SiteStatus.NEEDS_REVIEW,
        )

        payload = site.to_dict()

        assert site.is_proven_fixed_leaf is False
        assert payload["source_route"] == "Popup"
        assert payload["destination_dictionary"] == "scoped"
        assert payload["ownership_class"] is None
        assert payload["rejection_reason"] == "needs_review"
        assert payload["needs_review"] is True
        assert InventorySite.from_dict(payload) == site
