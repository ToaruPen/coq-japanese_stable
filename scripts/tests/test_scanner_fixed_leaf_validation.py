"""Tests for scanner fixed-leaf validation helpers."""

from __future__ import annotations

from typing import Any, cast

from scripts.legacies.scanner.fixed_leaf_validation import (
    FixedLeafFailureClass,
    render_fixed_leaf_validation_report,
    validate_fixed_leaf_inventory,
)
from scripts.legacies.scanner.inventory import (
    Confidence,
    DestinationDictionary,
    FixedLeafRejectionReason,
    InventoryDraft,
    InventorySite,
    InventoryStats,
    OwnershipClass,
    SiteStatus,
    SiteType,
)


def _draft(*sites: InventorySite) -> InventoryDraft:
    """Build a minimal inventory draft for validation tests."""
    return InventoryDraft(
        version="1.0",
        game_version="2.0.4",
        scan_date="2026-04-11",
        stats=InventoryStats(
            input_hits=len(sites),
            filtered_hits=0,
            output_sites=len(sites),
            high_confidence=sum(site.confidence is Confidence.HIGH for site in sites),
            medium_confidence=sum(site.confidence is Confidence.MEDIUM for site in sites),
            low_confidence=sum(site.confidence is Confidence.LOW for site in sites),
            needs_review=sum(site.needs_review for site in sites),
            needs_runtime=sum(site.needs_runtime for site in sites),
            proven_fixed_leaf=sum(site.is_proven_fixed_leaf for site in sites),
            rejected_fixed_leaf=sum(site.rejection_reason is not None for site in sites),
        ),
        sites=tuple(sites),
    )


def _site(site_id: str, **overrides: object) -> InventorySite:
    """Construct one inventory site for fixed-leaf validation tests."""
    fields: dict[str, Any] = {
        "id": site_id,
        "file": "Qud.UI/TestScreen.cs",
        "line": 1,
        "column": 1,
        "sink": "SetText",
        "source_route": "SetText",
        "type": SiteType.LEAF,
        "confidence": Confidence.HIGH,
        "pattern": 'label.SetText("sample")',
        "key": site_id,
        "ownership_class": OwnershipClass.MID_PIPELINE_OWNED,
        "destination_dictionary": DestinationDictionary.GLOBAL_FLAT,
        "rejection_reason": None,
        "needs_review": False,
        "needs_runtime": False,
    }
    fields.update(overrides)
    return InventorySite(**cast("dict[str, Any]", fields))


def test_validate_fixed_leaf_inventory_accepts_expected_global_and_scoped_candidates() -> None:
    """Validation passes when fixed-leaf candidates use the expected destination tier."""
    draft = _draft(
        _site("generic-leaf", key="generic"),
        _site(
            "popup-leaf",
            sink="Popup",
            source_route="Popup",
            pattern='Popup.Show("popup-show")',
            key="popup-show",
            destination_dictionary=DestinationDictionary.SCOPED,
        ),
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is True
    assert report.issues == ()
    assert "Fixed-leaf validation passed" in render_fixed_leaf_validation_report(report)


def test_validate_fixed_leaf_inventory_reports_duplicate_keys() -> None:
    """Validation fails when two fixed-leaf additions would ship the same exact key."""
    draft = _draft(
        _site("leaf-a", key="duplicate-key"),
        _site("leaf-b", key="duplicate-key"),
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is False
    assert len(report.issues) == 1
    issue = report.issues[0]
    assert issue.failure_class is FixedLeafFailureClass.DUPLICATE_KEY
    assert issue.candidate_ids == ("leaf-a", "leaf-b")
    output = render_fixed_leaf_validation_report(report)
    assert "[duplicate_key]" in output
    assert "duplicate-key" in output
    assert "leaf-a, leaf-b" in output
    assert "one exact fixed-leaf key per addition set" in output


def test_validate_fixed_leaf_inventory_ignores_already_translated_duplicate_families() -> None:
    """Existing-coverage duplicates should not fail validation for new fixed-leaf additions."""
    draft = _draft(
        _site(
            "translated-leaf-a",
            key="already-covered",
            status=SiteStatus.TRANSLATED,
            existing_dictionary="Scoped/ui-popup.ja.json",
        ),
        _site(
            "translated-leaf-b",
            key="already-covered",
            status=SiteStatus.TRANSLATED,
            existing_dictionary="Scoped/ui-popup.ja.json",
        ),
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is True
    assert report.candidate_count == 0
    assert report.issues == ()


def test_validate_fixed_leaf_inventory_reports_broad_owner_routed_entries() -> None:
    """Validation fails when a rejected owner-routed site is forced into a dictionary tier."""
    draft = _draft(
        _site(
            "message-frame-site",
            sink="DidX",
            source_route="DidX",
            type=SiteType.MESSAGE_FRAME,
            pattern='ParentObject.DidX("charge")',
            key="charge",
            ownership_class=OwnershipClass.PRODUCER_OWNED,
            destination_dictionary=DestinationDictionary.GLOBAL_FLAT,
            rejection_reason=FixedLeafRejectionReason.MESSAGE_FRAME,
        )
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is False
    assert len(report.issues) == 1
    issue = report.issues[0]
    assert issue.failure_class is FixedLeafFailureClass.BROAD_ENTRY
    assert issue.candidate_ids == ("message-frame-site",)
    output = render_fixed_leaf_validation_report(report)
    assert "[broad_entry]" in output
    assert "message-frame-site" in output
    assert "message_frame" in output
    assert "ownership=producer-owned" in output
    assert "keep this route owner-routed" in output


def test_validate_fixed_leaf_inventory_reports_broad_sink_observed_add_player_message_entries() -> None:
    """AddPlayerMessage should fail validation when treated as a fixed-leaf owner or sink-side fallback."""
    draft = _draft(
        _site(
            "message-log-leaf",
            sink="AddPlayerMessage",
            source_route="AddPlayerMessage",
            pattern='MessageQueue.AddPlayerMessage("You begin flying!")',
            key="You begin flying!",
            ownership_class=OwnershipClass.SINK,
            destination_dictionary=DestinationDictionary.SCOPED,
            rejection_reason=FixedLeafRejectionReason.NEEDS_REVIEW,
        )
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is False
    assert len(report.issues) == 1
    issue = report.issues[0]
    assert issue.failure_class is FixedLeafFailureClass.BROAD_ENTRY
    assert issue.candidate_ids == ("message-log-leaf",)
    output = render_fixed_leaf_validation_report(report)
    assert "[broad_entry]" in output
    assert "message-log-leaf" in output
    assert "ownership=sink" in output
    assert "needs_review" in output


def test_validate_fixed_leaf_inventory_rejects_forged_add_player_message_fixed_leaf_metadata() -> None:
    """Validator must reject AddPlayerMessage even when forged metadata makes it look proven."""
    draft = _draft(
        _site(
            "forged-message-log-leaf",
            sink="AddPlayerMessage",
            source_route="AddPlayerMessage",
            pattern='MessageQueue.AddPlayerMessage("You begin flying!")',
            key="You begin flying!",
            ownership_class=OwnershipClass.MID_PIPELINE_OWNED,
            destination_dictionary=DestinationDictionary.GLOBAL_FLAT,
            rejection_reason=None,
            needs_review=False,
            needs_runtime=False,
        )
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is False
    assert len(report.issues) == 1
    issue = report.issues[0]
    assert issue.failure_class is FixedLeafFailureClass.BROAD_ENTRY
    assert issue.candidate_ids == ("forged-message-log-leaf",)
    output = render_fixed_leaf_validation_report(report)
    assert "[broad_entry]" in output
    assert "forged-message-log-leaf" in output
    assert "route=AddPlayerMessage" in output


def test_validate_fixed_leaf_inventory_reports_wrong_destination_dictionary() -> None:
    """Validation fails when a route-specific leaf is proposed for the wrong dictionary tier."""
    draft = _draft(
        _site(
            "popup-leaf",
            sink="Popup",
            source_route="Popup",
            pattern='Popup.Show("popup-show")',
            key="popup-show",
            destination_dictionary=DestinationDictionary.GLOBAL_FLAT,
        )
    )

    report = validate_fixed_leaf_inventory(draft)

    assert report.is_valid is False
    assert len(report.issues) == 1
    issue = report.issues[0]
    assert issue.failure_class is FixedLeafFailureClass.WRONG_DESTINATION
    assert issue.candidate_ids == ("popup-leaf",)
    assert issue.expected_destination is DestinationDictionary.SCOPED
    output = render_fixed_leaf_validation_report(report)
    assert "[wrong_destination]" in output
    assert "popup-leaf" in output
    assert "popup-show" in output
    assert "actual=global_flat" in output
    assert "expected=scoped" in output
    assert "Popup and message-log leaves should use a scoped dictionary" in output
