"""Tests for the scanner.cross_reference module."""

from __future__ import annotations

from pathlib import Path

from scripts.scanner.cross_reference import (
    build_translation_index,
    cross_reference_inventory,
    cross_reference_inventory_file,
)
from scripts.scanner.inventory import (
    Confidence,
    InventoryDraft,
    InventorySite,
    InventoryStats,
    SiteStatus,
    SiteType,
    read_candidate_inventory_json,
    write_inventory_draft_json,
)

REPO_ROOT = Path(__file__).resolve().parents[2]


def _draft(*sites: InventorySite) -> InventoryDraft:
    """Build a minimal inventory draft for cross-reference tests."""
    return InventoryDraft(
        version="1.0",
        game_version="2.0.4",
        scan_date="2026-03-22",
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


def _site(
    site_id: str,
    **overrides: object,
) -> InventorySite:
    """Construct one InventorySite for test inputs."""
    fields: dict[str, object] = {
        "id": site_id,
        "file": "XRL.World/Test.cs",
        "line": 1,
        "column": 1,
        "sink": "SetText",
        "type": SiteType.LEAF,
        "confidence": Confidence.HIGH,
        "pattern": 'label.SetText("sample")',
        "key": None,
        "source": None,
        "source_id": None,
        "needs_review": False,
        "needs_runtime": False,
    }
    fields.update(overrides)
    return InventorySite(**fields)


def _write_source_file(tmp_path: Path, relative_path: str, lines: list[str]) -> None:
    """Create a decompiled-source fixture file under the given root."""
    source_path = tmp_path / relative_path
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def test_build_translation_index_reads_repo_samples() -> None:
    """Dictionary keys, XML identifiers, and patch targets are indexed from repo files."""
    index = build_translation_index(REPO_ROOT)

    assert "Cancel" in index.dictionary_keys
    assert "CommandAmputateLimb" in index.xml_ids
    assert "ActivatedAbilities.jp.xml" in index.xml_ids["CommandAmputateLimb"]

    assert "Qud.UI.OptionsScreen:Show" in index.patch_targets
    assert index.patch_targets["Qud.UI.OptionsScreen:Show"] == {"OptionsLocalizationPatch"}

    assert "Qud.UI.CharacterStatusScreen:UpdateViewFromData" in index.patch_targets
    assert index.patch_targets["Qud.UI.CharacterStatusScreen:UpdateViewFromData"] == {
        "CharacterStatusScreenBindingPatch",
        "CharacterStatusScreenTranslationPatch",
    }

    assert "XRL.UI.Popup:ShowBlock" in index.patch_targets
    assert index.patch_targets["XRL.UI.Popup:ShowBlock"] == {"PopupTranslationPatch"}


def test_cross_reference_marks_dictionary_xml_and_patch_matches(tmp_path: Path) -> None:
    """Cross-reference marks translated sites from dictionary, XML, and patch coverage."""
    _write_source_file(
        tmp_path,
        "Qud.UI/CharacterStatusScreen.cs",
        [
            "namespace Qud.UI;",
            "",
            "public sealed class CharacterStatusScreen",
            "{",
            "    public void UpdateViewFromData()",
            "    {",
            '        levelText.SetText(string.Format("Attribute Points: {0}", points));',
            "    }",
            "}",
        ],
    )
    _write_source_file(
        tmp_path,
        "XRL.World.Parts/Combat.cs",
        [
            "namespace XRL.World.Parts;",
            "",
            "public sealed class Combat",
            "{",
            "    public void HandleEvent()",
            "    {",
            '        Popup.Show("Do you really want to attack the snapjaw?");',
            '        MessageQueue.AddPlayerMessage($"You block with {shieldName}! (+{av} AV)");',
            "    }",
            "}",
        ],
    )

    draft = _draft(
        _site(
            "dict-leaf",
            file="XRL.World/Test.cs",
            line=10,
            sink="Popup",
            type=SiteType.LEAF,
            pattern='Popup.Show("Cancel")',
            key="Cancel",
        ),
        _site(
            "xml-blueprint",
            file="XRL.World/TestAbility.cs",
            line=12,
            sink="SetText",
            type=SiteType.LEAF,
            pattern="label.SetText(ability.Description)",
            confidence=Confidence.MEDIUM,
            source="xml-blueprint",
            source_id="CommandAmputateLimb",
        ),
        _site(
            "screen-template",
            file="Qud.UI/CharacterStatusScreen.cs",
            line=7,
            sink="SetText",
            type=SiteType.TEMPLATE,
            pattern='levelText.SetText(string.Format("Attribute Points: {0}", points))',
        ),
        _site(
            "popup-template",
            file="XRL.World.Parts/Combat.cs",
            line=7,
            sink="Popup",
            type=SiteType.TEMPLATE,
            pattern='Popup.Show("Do you really want to attack the snapjaw?")',
        ),
        _site(
            "message-template",
            file="XRL.World.Parts/Combat.cs",
            line=8,
            sink="AddPlayerMessage",
            type=SiteType.TEMPLATE,
            pattern='MessageQueue.AddPlayerMessage($"You block with {shieldName}! (+{av} AV)")',
        ),
        _site(
            "needs-translation",
            file="XRL.World/Test.cs",
            line=20,
            sink="SetText",
            type=SiteType.LEAF,
            pattern='label.SetText("Needs translation")',
            key="Needs translation",
        ),
        _site(
            "needs-patch",
            file="XRL.World/Test.cs",
            line=30,
            sink="DidX",
            type=SiteType.MESSAGE_FRAME,
            pattern='ParentObject.DidX("charge")',
            confidence=Confidence.HIGH,
        ),
        _site(
            "needs-review",
            file="XRL.World/Test.cs",
            line=40,
            sink="Does",
            type=SiteType.VERB_COMPOSITION,
            pattern='Object.Does("begin")',
            confidence=Confidence.HIGH,
        ),
        _site(
            "excluded",
            file="History/Test.cs",
            line=50,
            sink="HistoricStringExpander",
            type=SiteType.PROCEDURAL_TEXT,
            pattern='HistoricStringExpander.ExpandString("<spice.villages.warden.introDialog.!random>")',
            confidence=Confidence.LOW,
            needs_runtime=True,
        ),
        _site(
            "unresolved",
            file="XRL.World/Test.cs",
            line=60,
            sink="SetText",
            type=SiteType.UNRESOLVED,
            pattern="SetText(ComputeSomething())",
            confidence=Confidence.LOW,
            needs_runtime=True,
        ),
    )

    candidate = cross_reference_inventory(draft, REPO_ROOT, source_root=tmp_path)
    sites = {site.id: site for site in candidate.sites}

    assert sites["dict-leaf"].status is SiteStatus.TRANSLATED
    assert sites["dict-leaf"].existing_dictionary is not None
    assert "ui-popup.ja.json" in sites["dict-leaf"].existing_dictionary.split(", ")

    assert sites["xml-blueprint"].status is SiteStatus.TRANSLATED
    assert sites["xml-blueprint"].existing_xml == "ActivatedAbilities.jp.xml#CommandAmputateLimb"

    assert sites["screen-template"].status is SiteStatus.TRANSLATED
    expected_patch = "CharacterStatusScreenBindingPatch, CharacterStatusScreenTranslationPatch"
    assert sites["screen-template"].existing_patch == expected_patch

    assert sites["popup-template"].status is SiteStatus.TRANSLATED
    assert sites["popup-template"].existing_patch is not None
    assert "PopupTranslationPatch" in sites["popup-template"].existing_patch

    assert sites["message-template"].status is SiteStatus.TRANSLATED
    assert (
        "MessageLogPatch" in sites["message-template"].existing_patch
        or "MessageQueuePatch" in sites["message-template"].existing_patch
    )

    assert sites["needs-translation"].status is SiteStatus.NEEDS_TRANSLATION
    assert sites["needs-patch"].status is SiteStatus.NEEDS_PATCH
    assert sites["needs-review"].status is SiteStatus.NEEDS_REVIEW
    assert sites["excluded"].status is SiteStatus.EXCLUDED
    assert sites["unresolved"].status is SiteStatus.UNRESOLVED


def test_cross_reference_inventory_file_writes_candidate_inventory(tmp_path: Path) -> None:
    """Phase 1d can read an inventory draft file and persist candidate inventory JSON."""
    _write_source_file(
        tmp_path / "source",
        "Qud.UI/CharacterStatusScreen.cs",
        [
            "namespace Qud.UI;",
            "",
            "public sealed class CharacterStatusScreen",
            "{",
            "    public void UpdateViewFromData()",
            "    {",
            '        levelText.SetText(string.Format("Attribute Points: {0}", points));',
            "    }",
            "}",
        ],
    )
    draft = _draft(
        _site(
            "screen-template",
            file="Qud.UI/CharacterStatusScreen.cs",
            line=7,
            sink="SetText",
            type=SiteType.TEMPLATE,
            pattern='levelText.SetText(string.Format("Attribute Points: {0}", points))',
        )
    )
    input_path = tmp_path / "inventory_draft.json"
    output_path = tmp_path / "candidate-inventory.json"
    write_inventory_draft_json(input_path, draft)

    candidate = cross_reference_inventory_file(
        input_path,
        REPO_ROOT,
        source_root=tmp_path / "source",
        output_path=output_path,
    )

    assert output_path.exists()
    persisted = read_candidate_inventory_json(output_path)
    assert persisted == candidate
    assert persisted.sites[0].status is SiteStatus.TRANSLATED
    expected_patch = "CharacterStatusScreenBindingPatch, CharacterStatusScreenTranslationPatch"
    assert persisted.sites[0].existing_patch == expected_patch
