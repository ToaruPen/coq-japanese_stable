"""Tests for the reconcile_inventory_status module."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, cast

import pytest  # pyright: ignore[reportMissingImports]

from scripts.legacies.reconcile_inventory_status import (
    main,
    normalize_csharp_template,
    read_candidate_inventory_with_legacy_statuses,
    reconcile_inventory,
)
from scripts.legacies.scanner.inventory import (
    Confidence,
    InventoryDraft,
    InventorySite,
    InventoryStats,
    SiteStatus,
    SiteType,
    read_candidate_inventory_json,
)

REPO_ROOT = Path(__file__).resolve().parents[2]


def _draft(*sites: InventorySite) -> InventoryDraft:
    """Build a minimal inventory draft for reconciliation tests."""
    return InventoryDraft(
        version="1.0",
        game_version="2.0.4",
        scan_date="2026-03-24",
        stats=InventoryStats(
            input_hits=len(sites),
            filtered_hits=0,
            output_sites=len(sites),
            high_confidence=sum(site.confidence == Confidence.HIGH for site in sites),
            medium_confidence=sum(site.confidence == Confidence.MEDIUM for site in sites),
            low_confidence=sum(site.confidence == Confidence.LOW for site in sites),
            needs_review=sum(site.needs_review for site in sites),
            needs_runtime=sum(site.needs_runtime for site in sites),
        ),
        sites=tuple(sites),
    )


def _site(site_id: str, **overrides: object) -> InventorySite:
    """Construct one InventorySite for test inputs."""
    fields: dict[str, Any] = {
        "id": site_id,
        "file": "XRL.World/Test.cs",
        "line": 1,
        "column": 1,
        "sink": "SetText",
        "type": SiteType.LEAF,
        "confidence": Confidence.HIGH,
        "pattern": 'label.SetText("sample")',
        "status": SiteStatus.NEEDS_TRANSLATION,
        "needs_review": False,
        "needs_runtime": False,
    }
    fields.update(overrides)
    return InventorySite(**cast("dict[str, Any]", fields))


def test_normalize_csharp_template_handles_literals_and_getverb() -> None:
    """Tier-3 C# extra expressions normalize to placeholder templates."""
    assert normalize_csharp_template('base.DisplayNameStripped + " from another wound"') == "{0} from another wound"
    assert (
        normalize_csharp_template(
            '", but " + Subject.it + Subject.GetVerb("dodge", PrependSpace: true, PronounAntecedent: true)'
        )
        == ", but {0} dodge"
    )
    assert normalize_csharp_template('"{{rules|" + result + "}} XP"') == "{{rules|{0}}} XP"


def test_read_candidate_inventory_normalizes_legacy_exclude(tmp_path: Path) -> None:
    """Legacy `exclude` statuses are upgraded to the supported enum value on read."""
    draft = _draft(_site("excluded-site", status=SiteStatus.EXCLUDED))
    payload = draft.to_dict()
    payload["sites"][0]["status"] = "exclude"
    inventory_path = tmp_path / "candidate-inventory.json"
    inventory_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    loaded, normalized = read_candidate_inventory_with_legacy_statuses(inventory_path)

    assert normalized == 1
    assert loaded.sites[0].status is SiteStatus.EXCLUDED


def test_reconcile_inventory_promotes_sites_from_current_assets(tmp_path: Path) -> None:
    """Leaf, message-frame, and Does() sites are promoted when current assets cover them."""
    draft = _draft(
        _site(
            "leaf-cancel",
            key="Cancel",
            pattern='Popup.Show("Cancel")',
            sink="Popup",
            status=SiteStatus.NEEDS_TRANSLATION,
        ),
        _site(
            "message-frame-tier2",
            type=SiteType.MESSAGE_FRAME,
            sink="DidX",
            pattern='DidX("feel", "a bit glitchy")',
            verb="feel",
            extra="a bit glitchy",
            frame="DidX",
            lookup_tier=2,
            status=SiteStatus.NEEDS_PATCH,
        ),
        _site(
            "message-frame-tier3",
            type=SiteType.MESSAGE_FRAME,
            sink="DidX",
            pattern='DidX("begin", base.DisplayNameStripped + " from another wound")',
            verb="begin",
            extra='base.DisplayNameStripped + " from another wound"',
            frame="DidX",
            lookup_tier=3,
            status=SiteStatus.NEEDS_PATCH,
        ),
        _site(
            "does-verb",
            type=SiteType.VERB_COMPOSITION,
            sink="Does",
            pattern='speaker.Does("ask")',
            verb="ask",
            source_context='speaker.Does("ask")',
            status=SiteStatus.NEEDS_REVIEW,
        ),
        _site(
            "unmatched-message-frame",
            type=SiteType.MESSAGE_FRAME,
            sink="DidX",
            pattern='DidX("feel", "completely novel")',
            verb="feel",
            extra="completely novel",
            frame="DidX",
            lookup_tier=2,
            status=SiteStatus.NEEDS_PATCH,
        ),
    )

    reconciled, summary = reconcile_inventory(draft, REPO_ROOT, source_root=tmp_path / "missing-source")
    sites = {site.id: site for site in reconciled.sites}

    assert sites["leaf-cancel"].status is SiteStatus.TRANSLATED
    assert sites["leaf-cancel"].existing_dictionary is not None
    assert "ui-popup.ja.json" in sites["leaf-cancel"].existing_dictionary.split(", ")

    assert sites["message-frame-tier2"].status is SiteStatus.TRANSLATED
    assert sites["message-frame-tier2"].existing_dictionary == "MessageFrames/verbs.ja.json"

    assert sites["message-frame-tier3"].status is SiteStatus.TRANSLATED
    assert sites["message-frame-tier3"].existing_dictionary == "MessageFrames/verbs.ja.json"

    assert sites["does-verb"].status is SiteStatus.TRANSLATED
    assert sites["does-verb"].existing_dictionary == "MessageFrames/verbs.ja.json"

    assert sites["unmatched-message-frame"].status is SiteStatus.NEEDS_PATCH

    assert summary.translated_before == 0
    assert summary.translated_after == 4
    assert summary.baseline_promotions == 1
    assert summary.message_frame_promotions == 2
    assert summary.does_verb_promotions == 1


def test_main_rewrites_inventory_and_reports_summary(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI rewrites the inventory file and reports the reconciliation summary."""
    draft = _draft(
        _site(
            "legacy-excluded",
            status=SiteStatus.EXCLUDED,
            type=SiteType.PROCEDURAL_TEXT,
            confidence=Confidence.LOW,
            pattern='HistoricStringExpander.ExpandString("<spice>")',
        ),
        _site(
            "does-verb",
            type=SiteType.VERB_COMPOSITION,
            sink="Does",
            pattern='speaker.Does("ask")',
            verb="ask",
            source_context='speaker.Does("ask")',
            status=SiteStatus.NEEDS_REVIEW,
        ),
    )
    payload = draft.to_dict()
    payload["sites"][0]["status"] = "exclude"
    inventory_path = tmp_path / "candidate-inventory.json"
    output_path = tmp_path / "candidate-inventory.reconciled.json"
    inventory_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    result = main(
        [
            "--inventory",
            str(inventory_path),
            "--output",
            str(output_path),
            "--repo-root",
            str(REPO_ROOT),
            "--source-root",
            str(tmp_path / "missing-source"),
        ]
    )
    captured = capsys.readouterr()
    reconciled = read_candidate_inventory_json(output_path)

    assert result == 0
    assert output_path.exists()
    assert reconciled.sites[0].status is SiteStatus.EXCLUDED
    assert reconciled.sites[1].status is SiteStatus.TRANSLATED
    assert "translated (+" not in captured.err
    assert "0 -> 1 translated (+1)" in captured.out
    assert "legacy exclude -> excluded: 1" in captured.out


def test_main_help_marks_reconciliation_as_legacy_bridge_view(capsys: pytest.CaptureFixture[str]) -> None:
    """CLI help should lock the task-4 first-PR static consumer boundary."""
    with pytest.raises(SystemExit, match="0"):
        main(["--help"])

    captured = capsys.readouterr()

    assert "pilot-aware" in captured.out
    assert "bridge-only" in captured.out
    assert "deferred" in captured.out
    assert "Roslyn pilot schema contract" in captured.out
    assert "Roslyn pilot verification/tests" in captured.out
    assert (
        "scripts/legacies/reconcile_inventory_status.py legacy scanner candidate-inventory bridge/view-only consumer"
        in captured.out
    )
    assert "runtime observability consumers" in captured.out
    assert "scripts/triage/*" in captured.out
    assert "Phase F and unresolved full static consumer migration" in captured.out
    assert "bridge/view-only" in captured.out
    assert "source of truth" in captured.out
