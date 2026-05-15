from __future__ import annotations

from collections import Counter
from pathlib import Path

from scripts.static_producer_closure import (
    COVERED_BY_OWNER_PATCH,
    COVERED_OWNER_CALLSITES,
    COVERED_OWNER_FAMILIES,
    DEFERRED_RUNTIME_CALLSITES,
    covered_callsite_keys,
    covered_family_ids,
    deferred_runtime_callsite_keys,
    family_closure_status,
    format_message_candidate_policy_queue,
    format_owner_action_queue,
    load_inventory,
    message_candidate_policy_entries,
    owner_action_queue,
    owner_action_queue_by_file,
    validate_covered_owner_families,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
TRACKED_INVENTORY = REPO_ROOT / "docs" / "static-producer-inventory.json"


def test_covered_owner_registry_has_unique_family_ids() -> None:
    """Covered-family registry entries must be unique."""
    family_ids = [family.family_id for family in COVERED_OWNER_FAMILIES]

    assert len(family_ids) == len(set(family_ids))


def test_covered_owner_callsite_registry_has_unique_line_keys() -> None:
    """Covered mixed-family callsite registry entries must be unique."""
    expected_keys = [
        (covered.family_id, line)
        for covered in COVERED_OWNER_CALLSITES
        for line in covered.lines
    ]

    assert len(expected_keys) == len(set(expected_keys))
    assert covered_callsite_keys() == frozenset(expected_keys)


def test_deferred_runtime_callsite_registry_has_unique_line_keys() -> None:
    """Runtime-required deferrals must be unique and visible."""
    expected_keys = [
        (deferred.family_id, line)
        for deferred in DEFERRED_RUNTIME_CALLSITES
        for line in deferred.lines
    ]

    assert len(expected_keys) == len(set(expected_keys))
    assert deferred_runtime_callsite_keys() == frozenset(expected_keys)


def test_owner_covered_runtime_callsite_can_close_local_dataflow_shape() -> None:
    """A scanner runtime row can be owner-covered when static local dataflow and tests prove the shape."""
    family_id = "XRL.World.Parts/Physics.cs::XRL.World.Parts.Physics.HandleEvent"

    assert (family_id, 2582) in covered_callsite_keys()
    assert (family_id, 2582) not in deferred_runtime_callsite_keys()


def test_covered_owner_families_have_current_source_and_test_evidence() -> None:
    """Covered families must point at current source and test evidence."""
    inventory = load_inventory(TRACKED_INVENTORY)

    errors = validate_covered_owner_families(inventory, REPO_ROOT)

    assert errors == []


def _declaring_type_from_family_id(family_id: str) -> str:
    return family_id.split("::", maxsplit=1)[1].rsplit(".", maxsplit=1)[0]


def _member_name_from_family_id(family_id: str) -> str:
    return family_id.rsplit(".", maxsplit=1)[1]


def _is_owner_target_resolution_token(
    token: str, *, declaring_type: str, member_name: str
) -> bool:
    parts = token.split("|")
    return len(parts) >= 3 and parts[0] == declaring_type and parts[1] == member_name


def _unquoted_token(token: str) -> str:
    if len(token) >= 2 and token[0] == token[-1] == '"':
        return token[1:-1]
    return token


def test_owner_patch_l2g_evidence_is_family_specific() -> None:
    """Owner-patch L2G evidence must identify the upstream owner method."""
    problems: list[str] = []

    for family in COVERED_OWNER_FAMILIES:
        if family.inventory_statuses != ("owner_patch_required",):
            continue

        declaring_type = _declaring_type_from_family_id(family.family_id)
        member_name = _member_name_from_family_id(family.family_id)
        l2g_evidence_files = [
            evidence
            for evidence in family.evidence_files
            if evidence.path.endswith("L2G/TargetMethodResolutionTests.cs")
        ]

        if not l2g_evidence_files:
            problems.append(f"{family.family_id}: missing L2G target resolution evidence")
            continue

        for evidence in l2g_evidence_files:
            normalized_tokens = [
                _unquoted_token(token) for token in evidence.required_substrings
            ]

            has_full_target_token = any(
                _is_owner_target_resolution_token(
                    token, declaring_type=declaring_type, member_name=member_name
                )
                for token in evidence.required_substrings
            )
            has_declaring_type = any(
                token == declaring_type for token in normalized_tokens
            )
            has_member_name = any(token == member_name for token in normalized_tokens)

            if not has_full_target_token and not (has_declaring_type and has_member_name):
                problem = (
                    f"{family.family_id}: L2G evidence must include a full target signature "
                    f"or both the upstream declaring type and member name"
                )
                problems.append(problem)

    assert problems == []


def test_covered_owner_families_are_removed_from_owner_action_queue() -> None:
    """Covered owner families must not remain in the owner implementation queue."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}

    for family_id in covered_family_ids():
        assert raw_families[family_id]["family_closure_status"] in {
            "owner_patch_required",
            "needs_family_review",
        }
        assert family_closure_status(raw_families[family_id]) == COVERED_BY_OWNER_PATCH

    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}

    assert queued_family_ids.isdisjoint(covered_family_ids())


def test_game_object_heal_owner_family_is_closed_by_current_owner_tests() -> None:
    """GameObject.Heal has current owner-route evidence and must stay out of the queue."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    family_id = "XRL.World/GameObject.cs::XRL.World.GameObject.Heal"

    assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
    assert family_closure_status(raw_families[family_id]) == COVERED_BY_OWNER_PATCH
    assert family_id not in {
        family["producer_family_id"]
        for family in owner_action_queue(inventory)
    }


def test_trade_ui_vendor_owner_callsites_are_split_from_fixed_fallbacks() -> None:
    """Mixed TradeUI vendor families must close owner callsites without full-family over-closure."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    examine_id = "XRL.UI/TradeUI.cs::XRL.UI.TradeUI.DoVendorExamine"
    recharge_id = "XRL.UI/TradeUI.cs::XRL.UI.TradeUI.DoVendorRecharge"
    show_trade_id = "XRL.UI/TradeUI.cs::XRL.UI.TradeUI.ShowTradeScreen"

    assert raw_families[examine_id]["family_closure_status"] == "needs_family_review"
    assert raw_families[recharge_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[examine_id]) == "needs_family_review"
    assert family_closure_status(raw_families[recharge_id]) == "needs_family_review"
    assert examine_id not in covered_family_ids()
    assert recharge_id not in covered_family_ids()
    assert examine_id not in queued_family_ids
    assert recharge_id not in queued_family_ids
    assert show_trade_id not in queued_family_ids
    assert not any(entry["source_file"] == "XRL.UI/TradeUI.cs" for entry in owner_action_queue_by_file(inventory))


def test_journal_screen_popup_owner_callsites_are_split_from_fixed_fallbacks() -> None:
    """JournalScreen popup owner callsites must close without claiming fixed fallback popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    journal_family_ids = {
        "XRL.UI/JournalScreen.cs::XRL.UI.JournalScreen.HandleDelete",
        "XRL.UI/JournalScreen.cs::XRL.UI.JournalScreen.Show",
    }

    for family_id in journal_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert family_id not in queued_family_ids

    assert not any(entry["source_file"] == "XRL.UI/JournalScreen.cs" for entry in owner_action_queue_by_file(inventory))


def test_conversation_script_popup_owner_callsites_are_split_from_fixed_and_runtime_fallbacks() -> None:
    """ConversationScript popup owner callsites close while runtime popups are deferred."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    conversation_family_ids = {
        "XRL.World.Parts/ConversationScript.cs::XRL.World.Parts.ConversationScript.IsPhysicalConversationPossible",
        "XRL.World.Parts/ConversationScript.cs::XRL.World.Parts.ConversationScript.IsMentalConversationPossible",
    }

    for family_id in conversation_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert family_id not in queued_family_ids

    assert "XRL.World.Parts/ConversationScript.cs" not in {
        entry["source_file"] for entry in source_entries
    }


def test_terrain_travel_owner_callsites_are_split_from_runtime_and_fixed_popups() -> None:
    """TerrainTravel owner callsites must close without claiming runtime encounter or fixed lost popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    terrain_family_ids = {
        "XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleEvent",
        "XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleLeavingCell",
    }

    for family_id in terrain_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()

    assert ("XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleEvent", 120) in (
        deferred_runtime_callsite_keys()
    )
    assert ("XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleEvent", 124) in (
        deferred_runtime_callsite_keys()
    )
    assert "XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleEvent" not in queued_family_ids
    assert "XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleLeavingCell" not in queued_family_ids
    assert "XRL.World.Parts/TerrainTravel.cs" not in {
        entry["source_file"] for entry in source_entries
    }


def test_precognition_owner_queue_callsites_are_split_from_fixed_popups() -> None:
    """Precognition queue messages must close without claiming fixed popup candidates."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    precognition_family_ids = {
        "XRL.World.Parts.Mutation/Precognition.cs::XRL.World.Parts.Mutation.Precognition.FireEvent",
        "XRL.World.Parts.Mutation/Precognition.cs::XRL.World.Parts.Mutation.Precognition.OnBeforeDie",
    }

    for family_id in precognition_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert family_id not in queued_family_ids

    assert all(
        entry["source_file"] != "XRL.World.Parts.Mutation/Precognition.cs"
        for entry in source_entries
    )


def test_wish_command_queue_families_are_closed_by_owner_patch() -> None:
    """Wish command queue literals are closed as a shared owner-surface contract."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    wish_family_ids = {
        "XRL.World.Quests/LandingPadsSystem.cs::XRL.World.Quests.LandingPadsSystem.SlynthQuestWish",
        "XRL.World.Quests/ReclamationSystem.cs::XRL.World.Quests.ReclamationSystem.WishTimer",
        "XRL.World/StatWishHandler.cs::XRL.World.StatWishHandler.ClearStatShifts",
    }

    for family_id in wish_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
        assert family_id in covered_family_ids()
        assert family_id not in queued_family_ids


def test_single_callsite_owner_queue_families_are_closed_by_owner_patch() -> None:
    """Single-callsite owner queue messages are closed only by owner-keyed route evidence."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_ids = {
        "XRL.World.Parts/ModMorphogenetic.cs::XRL.World.Parts.ModMorphogenetic.ApplyMorphicShock",
        "XRL.World.Quests/WeirdwireConduitSystem.cs::XRL.World.Quests.WeirdwireConduitSystem.HandleEvent",
    }

    for family_id in family_ids:
        assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
        assert family_id in covered_family_ids()
        assert family_id not in queued_family_ids


def test_single_fixed_queue_owner_callsites_are_split_from_mixed_siblings() -> None:
    """Fixed queue callsites close without claiming mixed popup or runtime siblings."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    callsite_keys = covered_callsite_keys()
    split_families = {
        "XRL.World.Effects/Monochrome.cs::XRL.World.Effects.Monochrome.FireEvent": {
            "closed_lines": {133},
            "queued": False,
        },
        (
            "XRL.World.Parts.Skill/Persuasion_RebukeRobot.cs::"
            "XRL.World.Parts.Skill.Persuasion_RebukeRobot.AttemptRebuke"
        ): {
            "closed_lines": {89},
            "queued": False,
        },
        "XRL.World.Effects/SphynxSalt_Tonic.cs::XRL.World.Effects.SphynxSalt_Tonic.Apply": {
            "closed_lines": {124},
            "queued": False,
        },
        "XRL.World.Parts/ThiefBot.cs::XRL.World.Parts.ThiefBot.FireEvent": {
            "closed_lines": {45, 76},
            "queued": False,
        },
    }

    for family_id, expected in split_families.items():
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert {line for covered_family, line in callsite_keys if covered_family == family_id} == expected[
            "closed_lines"
        ]
        assert (family_id in queued_family_ids) is expected["queued"]


def test_single_mixed_owner_callsites_are_split_from_fixed_and_runtime_siblings() -> None:
    """Generated popup and fixed queue owner callsites close without claiming mixed siblings."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    callsite_keys = covered_callsite_keys()
    split_families = {
        "XRL.World.Parts/StairsDown.cs::XRL.World.Parts.StairsDown.CheckPullDown": {
            "closed_lines": {372, 439},
            "queued": False,
        },
        (
            "XRL.World.ZoneParts/ScriptCallToArms.cs::"
            "XRL.World.ZoneParts.ScriptCallToArms.ShowWarning"
        ): {
            "closed_lines": {547},
            "queued": False,
        },
    }

    for family_id, expected in split_families.items():
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert {line for covered_family, line in callsite_keys if covered_family == family_id} == expected[
            "closed_lines"
        ]
        assert (family_id in queued_family_ids) is expected["queued"]


def test_keybinds_menu_option_popups_are_split_from_fixed_restore_prompt() -> None:
    """Keybind removal owner popups close without claiming the fixed restore-default prompt."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    callsite_keys = covered_callsite_keys()
    family_id = "Qud.UI/KeybindsScreen.cs::Qud.UI.KeybindsScreen.HandleMenuOption"

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert {line for covered_family, line in callsite_keys if covered_family == family_id} == {303, 306}
    assert family_id not in queued_family_ids


def test_zone_manager_owner_callsites_are_split_from_runtime_and_fixed_popup_shapes() -> None:
    """ZoneManager owner callsites close without claiming runtime or fixed popup shapes."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    callsite_keys = covered_callsite_keys()
    set_active_zone_family_id = "XRL.World/ZoneManager.cs::XRL.World.ZoneManager.SetActiveZone"
    generate_zone_family_id = "XRL.World/ZoneManager.cs::XRL.World.ZoneManager.GenerateZone"

    for family_id in (set_active_zone_family_id, generate_zone_family_id):
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()

    assert (set_active_zone_family_id, 1889) in deferred_runtime_callsite_keys()
    assert (set_active_zone_family_id, 1912) in deferred_runtime_callsite_keys()
    assert set_active_zone_family_id not in queued_family_ids
    assert generate_zone_family_id not in queued_family_ids
    assert {
        line for covered_family, line in callsite_keys if covered_family == generate_zone_family_id
    } == {3286, 3570}


def test_additional_single_callsite_owner_popup_families_are_closed_by_owner_patch() -> None:
    """Single Popup.Show owner surfaces close only with owner-keyed popup route evidence."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_ids = {
        "XRL.World.Parts/HindrenMysteryCriticalNPC.cs::XRL.World.Parts.HindrenMysteryCriticalNPC.HandleEvent",
        "XRL.World.Parts/MakeFussOnTaken.cs::XRL.World.Parts.MakeFussOnTaken.MakeFuss",
        "XRL.World.Parts/MutationPointsOnEat.cs::XRL.World.Parts.MutationPointsOnEat.FireEvent",
        "XRL.World.Parts/WaterRitualRecord.cs::XRL.World.Parts.WaterRitualRecord.HandleEvent",
        "XRL.World.QuestManagers/SpreadPax.cs::XRL.World.QuestManagers.SpreadPax.Finish",
    }

    for family_id in family_ids:
        assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
        assert family_id in covered_family_ids()
        assert family_id not in queued_family_ids


def test_wish_reward_and_rank_single_callsite_popup_families_are_closed_by_owner_patch() -> None:
    """Small single-callsite popup owner surfaces close without claiming debug-only siblings."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    full_family_ids = {
        "XRL.World.Quests/AscensionSystem.cs::XRL.World.Quests.AscensionSystem.BarathrumStartConversation",
        "XRL.World/DynamicQuestRewardElement_GameObject.cs::XRL.World.DynamicQuestRewardElement_GameObject.award",
        "XRL.World.ZoneBuilders/FactionEncounters.cs::XRL.World.ZoneBuilders.FactionEncounters.HandleFactionEncounterWish",
        "XRL.World.Parts/KindrishProperties.cs::XRL.World.Parts.KindrishProperties.ReturnAward",
        "XRL.World/Reputation.cs::XRL.World.Reputation.SetFactionRank",
    }
    biome_family_id = "XRL.World.Biomes/BiomeManager.cs::XRL.World.Biomes.BiomeManager.DisplaySurfaceDistribution"

    for family_id in full_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
        assert family_id in covered_family_ids()
        assert family_id not in queued_family_ids

    assert raw_families[biome_family_id]["family_closure_status"] == "owner_patch_required"
    assert biome_family_id not in covered_family_ids()
    assert (biome_family_id, 129) in covered_callsite_keys()
    assert biome_family_id not in queued_family_ids


def test_remaining_pure_single_callsite_owner_popup_families_are_closed_by_owner_patch() -> None:
    """Remaining pure single-callsite popup owners close by owner-patch evidence."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_ids = {
        (
            "XRL.CharacterBuilds.Qud/QudSpecificCharacterInitModule.cs::"
            "XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule.handleBootEvent"
        ),
        "XRL.UI/Look.cs::XRL.UI.Look.ShowLooker",
        "XRL.World.Parts/MarkovBook.cs::XRL.World.Parts.MarkovBook.HandleEvent",
        "XRL.World.Parts/MumblesInfection.cs::XRL.World.Parts.MumblesInfection.FireEvent",
        (
            "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs::"
            "XRL.World.Parts.Mutation.PhotosyntheticSkin.HandleEvent"
        ),
        "XRL.World.Parts/Toolbox.cs::XRL.World.Parts.Toolbox.HandleBonus",
    }

    for family_id in family_ids:
        assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
        assert family_id in covered_family_ids()
        assert family_id not in queued_family_ids


def test_xrlcore_old_save_popup_callsite_is_split_from_fixed_save_management_popups() -> None:
    """XRLCore.SaveManagement closes the old-save owner popup without claiming fixed save prompts."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    family_id = "XRL.Core/XRLCore.cs::XRL.Core.XRLCore.SaveManagement"

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 3962) in covered_callsite_keys()
    assert family_id not in queued_family_ids

    assert "XRL.Core/XRLCore.cs" not in {entry["source_file"] for entry in source_entries}


def test_sifrah_token_item_owner_callsites_are_split_from_fixed_kind_message() -> None:
    """Sifrah token item checks close generated item names while deferring the fixed kind message."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    gift_family_id = "XRL.World/SocialSifrahTokenGift.cs::XRL.World.SocialSifrahTokenGift.CheckTokenUse"
    item_family_id = "XRL.World/SocialSifrahTokenItem.cs::XRL.World.SocialSifrahTokenItem.CheckTokenUse"

    assert raw_families[gift_family_id]["family_closure_status"] == "needs_family_review"
    assert raw_families[item_family_id]["family_closure_status"] == "needs_family_review"

    for family_id in (gift_family_id, item_family_id):
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert family_id not in queued_family_ids

    assert (gift_family_id, 120) in covered_callsite_keys()
    assert (gift_family_id, 124) in covered_callsite_keys()
    assert (item_family_id, 115) in covered_callsite_keys()
    assert (item_family_id, 119) in covered_callsite_keys()
    assert all(
        entry["source_file"]
        not in {
            "XRL.World/SocialSifrahTokenGift.cs",
            "XRL.World/SocialSifrahTokenItem.cs",
        }
        for entry in source_entries
    )


def test_reverse_engineering_sifrah_finish_callsite_is_split_from_critical_failure_popup() -> None:
    """ReverseEngineeringSifrah.Finish closes the failure owner popup without claiming critical failure."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    family_id = "XRL.World/ReverseEngineeringSifrah.cs::XRL.World.ReverseEngineeringSifrah.Finish"

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 202) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert not any(
        entry["source_file"] == "XRL.World/ReverseEngineeringSifrah.cs"
        for entry in source_entries
    )


def test_pick_target_show_picker_range_failure_is_split_from_fixed_visibility_popups() -> None:
    """PickTarget.ShowPicker closes generated range failure without claiming fixed visibility popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.UI/PickTarget.cs::XRL.UI.PickTarget.ShowPicker"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 850) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (850, "owner_patch_required"),
        (854, "messages_candidate"),
        (858, "messages_candidate"),
    }


def test_give_resheph_secret_reward_popups_are_split_from_fixed_no_secret_popup() -> None:
    """GiveReshephSecret.HandleEvent closes reward popups without claiming fixed no-secret text."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = (
        "XRL.World.Conversations.Parts/GiveReshephSecret.cs::"
        "XRL.World.Conversations.Parts.GiveReshephSecret.HandleEvent"
    )
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 54) in covered_callsite_keys()
    assert (family_id, 55) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (38, "messages_candidate"),
        (54, "owner_patch_required"),
        (55, "owner_patch_required"),
    }


def test_water_ritual_random_mutation_incompatible_popup_is_split_from_fixed_and_runtime_popups() -> None:
    """WaterRitualRandomMutation closes incompatible-category popup without claiming fixed/runtime text."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = (
        "XRL.World.Conversations.Parts/WaterRitualRandomMutation.cs::"
        "XRL.World.Conversations.Parts.WaterRitualRandomMutation.HandleEvent"
    )
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 92) in covered_callsite_keys()
    assert (family_id, 98) in deferred_runtime_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (88, "messages_candidate"),
        (92, "owner_patch_required"),
        (98, "runtime_required"),
    }


def test_psychometry_owner_popups_are_split_from_fixed_continue_prompt() -> None:
    """Psychometry closes generated item popups without claiming fixed continuation prompts."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts.Mutation/Psychometry.cs::XRL.World.Parts.Mutation.Psychometry.HandleEvent"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert {
        (family_id, line)
        for line in (168, 172, 181, 185, 191, 206)
    }.issubset(covered_callsite_keys())
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (60, "Popup.Show*", "messages_candidate"),
        (79, "Popup.Show*", "messages_candidate"),
        (168, "Popup.Show*", "owner_patch_required"),
        (172, "Popup.Show*", "owner_patch_required"),
        (181, "Popup.Show*", "owner_patch_required"),
        (185, "Popup.Show*", "owner_patch_required"),
        (191, "Popup.Show*", "owner_patch_required"),
        (206, "Popup.Show*", "owner_patch_required"),
    }


def test_sunder_mind_tick_head_explosion_queue_is_split_from_fixed_popups() -> None:
    """SunderMind.Tick closes generated head-explosion queue text without claiming fixed popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts.Mutation/SunderMind.cs::XRL.World.Parts.Mutation.SunderMind.Tick"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 279) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (273, "Popup.Show*", "messages_candidate"),
        (274, "Popup.Show*", "messages_candidate"),
        (279, "AddPlayerMessage", "owner_patch_required"),
    }


def test_axe_dismember_cast_self_confirmation_is_split_from_fixed_popups() -> None:
    """Axe_Dismember.Cast closes the self-confirmation owner popup without claiming fixed popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = (
        "XRL.World.Parts.Skill/Axe_Dismember.cs::"
        "XRL.World.Parts.Skill.Axe_Dismember.Cast"
    )
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 250) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (237, "Popup.Show*", "messages_candidate"),
        (241, "Popup.Show*", "messages_candidate"),
        (250, "Popup.Show*", "owner_patch_required"),
    }


def test_cudgel_smash_up_prepare_queue_is_split_from_fixed_popups() -> None:
    """Cudgel_SmashUp.FireEvent closes the generated prepare queue without claiming fixed popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = (
        "XRL.World.Parts.Skill/Cudgel_SmashUp.cs::"
        "XRL.World.Parts.Skill.Cudgel_SmashUp.FireEvent"
    )
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 95) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (80, "Popup.Show*", "messages_candidate"),
        (89, "Popup.Show*", "messages_candidate"),
        (95, "AddPlayerMessage", "owner_patch_required"),
    }


def test_submersion_too_shallow_popup_is_split_from_fixed_popups() -> None:
    """Submersion.HandleEvent closes the generated too-shallow popup without claiming fixed popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts.Skill/Submersion.cs::XRL.World.Parts.Skill.Submersion.HandleEvent"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 62) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (58, "Popup.Show*", "messages_candidate"),
        (62, "Popup.Show*", "owner_patch_required"),
        (71, "Popup.Show*", "messages_candidate"),
    }


def test_tinkering_tinker1_recharge_popups_are_split_from_fixed_popup() -> None:
    """Tinkering_Tinker1.Recharge closes generated recharge popups without claiming fixed popup."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = (
        "XRL.World.Parts.Skill/Tinkering_Tinker1.cs::"
        "XRL.World.Parts.Skill.Tinkering_Tinker1.Recharge"
    )
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 80) in covered_callsite_keys()
    assert (family_id, 92) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (80, "Popup.Show*", "owner_patch_required"),
        (88, "Popup.Show*", "messages_candidate"),
        (92, "Popup.Show*", "owner_patch_required"),
    }


def test_container_attempt_open_popups_are_split_from_fixed_popup() -> None:
    """Container.AttemptOpen closes generated owner popups without claiming fixed warning."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts/Container.cs::XRL.World.Parts.Container.AttemptOpen"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 113) in covered_callsite_keys()
    assert (family_id, 132) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (113, "Popup.Show*", "owner_patch_required"),
        (119, "Popup.Show*", "messages_candidate"),
        (132, "Popup.Show*", "owner_patch_required"),
    }


def test_elevator_switch_queue_message_is_split_from_fixed_popups() -> None:
    """ElevatorSwitch.FireEvent closes queue message without claiming fixed popups."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts/ElevatorSwitch.cs::XRL.World.Parts.ElevatorSwitch.FireEvent"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 58) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (49, "Popup.Show*", "messages_candidate"),
        (53, "Popup.Show*", "messages_candidate"),
        (58, "AddPlayerMessage", "owner_patch_required"),
    }


def test_imodification_wish_modify_popups_are_split_from_fixed_popup() -> None:
    """IModification.WishModify closes generated owner popups without claiming fixed warning."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts/IModification.cs::XRL.World.Parts.IModification.WishModify"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 254) in covered_callsite_keys()
    assert (family_id, 260) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (248, "Popup.Show*", "messages_candidate"),
        (254, "Popup.Show*", "owner_patch_required"),
        (260, "Popup.Show*", "owner_patch_required"),
    }


def test_neutron_flux_containment_popups_are_split_from_runtime_warning() -> None:
    """NeutronFluxContainment.HandleEvent closes owner popups but leaves runtime warning queued."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = (
        "XRL.World.Parts/NeutronFluxContainment.cs::"
        "XRL.World.Parts.NeutronFluxContainment.HandleEvent"
    )
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 63) in covered_callsite_keys()
    assert (family_id, 91) in covered_callsite_keys()
    assert (family_id, 99) not in covered_callsite_keys()
    assert (family_id, 99) in deferred_runtime_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (63, "Popup.Show*", "owner_patch_required"),
        (91, "Popup.Show*", "owner_patch_required"),
        (99, "Popup.Show*", "runtime_required"),
    }


def test_polygel_handle_event_popups_are_split_from_fixed_popup() -> None:
    """Polygel.HandleEvent closes generated owner popups without claiming fixed warning."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    family_id = "XRL.World.Parts/Polygel.cs::XRL.World.Parts.Polygel.HandleEvent"
    family_callsites = [
        callsite
        for callsite in inventory["callsites"]
        if callsite["producer_family_id"] == family_id
    ]

    assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[family_id]) == "needs_family_review"
    assert family_id not in covered_family_ids()
    assert (family_id, 83) in covered_callsite_keys()
    assert (family_id, 100) in covered_callsite_keys()
    assert family_id not in queued_family_ids
    assert {
        (callsite["line"], callsite["target_surface"], callsite["closure_status"])
        for callsite in family_callsites
    } == {
        (51, "Popup.Show*", "messages_candidate"),
        (83, "Popup.Show*", "owner_patch_required"),
        (100, "Popup.Show*", "owner_patch_required"),
    }


def test_message_candidate_policy_entries_group_remaining_static_and_pattern_rows() -> None:
    """Message-candidate policy export must split existing leaves, rejects, and pattern deferrals."""
    inventory = load_inventory(TRACKED_INVENTORY)
    entries = message_candidate_policy_entries(inventory, REPO_ROOT)
    decision_counts = Counter(entry["decision"] for entry in entries)
    choose_color_entry = next(entry for entry in entries if entry["literal_text"] == "Choose color")
    empty_entry = next(
        entry
        for entry in entries
        if entry["source_file"] == "XRL.UI/Popup.cs" and entry["literal_text"] == ""
    )
    chat_template_entry = next(
        entry
        for entry in entries
        if entry["producer_family_id"] == "XRL.World.Parts/Chat.cs::XRL.World.Parts.Chat.PerformChat"
    )

    assert len(entries) == 698
    assert decision_counts == {
        "existing_dictionary_coverage": 542,
        "existing_message_pattern_coverage": 144,
        "existing_does_verb_route_coverage": 5,
        "existing_owner_route_coverage": 2,
        "reject_pseudo_leaf": 5,
    }
    assert choose_color_entry["decision"] == "existing_dictionary_coverage"
    assert choose_color_entry["coverage_locations"] == [
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json"
    ]
    assert empty_entry["decision"] == "reject_pseudo_leaf"
    assert chat_template_entry["decision"] == "existing_does_verb_route_coverage"


def test_message_candidate_policy_text_summary_reports_group_counts() -> None:
    """Message-candidate text output must be useful as a policy handoff summary."""
    inventory = load_inventory(TRACKED_INVENTORY)

    summary = format_message_candidate_policy_queue(inventory, repo_root=REPO_ROOT)

    assert "message candidate policy queue: 698 text arguments" in summary
    assert "existing_dictionary_coverage:542" in summary
    assert "existing_message_pattern_coverage:144" in summary
    assert "existing_does_verb_route_coverage:5" in summary
    assert "existing_owner_route_coverage:2" in summary
    assert "reject_pseudo_leaf:5" in summary


def test_large_mixed_families_drop_out_of_owner_action_queue_after_runtime_deferral() -> None:
    """Large mixed families should leave the owner queue after owner and runtime rows are split."""
    inventory = load_inventory(TRACKED_INVENTORY)
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}

    assert {
        "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookPresetMeal",
        "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookFromIngredients",
        "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookFromRecipe",
        "XRL.Core/XRLCore.cs::XRL.Core.XRLCore.PlayerTurn",
        "XRL.World.Parts/Inventory.cs::XRL.World.Parts.Inventory.FireEvent",
        "XRL.World.Parts/Physics.cs::XRL.World.Parts.Physics.HandleEvent",
        "XRL.World.Parts/Physics.cs::XRL.World.Parts.Physics.ProcessTargetedMove",
        (
            "XRL.World.Parts/ConversationScript.cs::"
            "XRL.World.Parts.ConversationScript.IsPhysicalConversationPossible"
        ),
        (
            "XRL.World.Parts/ConversationScript.cs::"
            "XRL.World.Parts.ConversationScript.IsMentalConversationPossible"
        ),
        "XRL.World.Parts/Crayons.cs::XRL.World.Parts.Crayons.HandleEvent",
        "XRL.World.Parts/Chat.cs::XRL.World.Parts.Chat.PerformChat",
        "XRL.World.Parts/ITeleporter.cs::XRL.World.Parts.ITeleporter.AttemptTeleport",
        "XRL.World.Parts/MissileWeapon.cs::XRL.World.Parts.MissileWeapon.FireEvent",
        "XRL.World.Parts/Garbage.cs::XRL.World.Parts.Garbage.AttemptRifle",
    }.isdisjoint(queued_family_ids)


def test_owner_action_queue_groups_actionable_work_by_source_file() -> None:
    """All static producer owner-route work must be closed or explicitly deferred."""
    inventory = load_inventory(TRACKED_INVENTORY)
    source_entries = owner_action_queue_by_file(inventory)

    assert source_entries == []


def test_owner_action_queue_text_summary_names_source_files_and_methods() -> None:
    """Text output must report a zero-sized owner queue when no owner action remains."""
    inventory = load_inventory(TRACKED_INVENTORY)

    summary = format_owner_action_queue(inventory, limit=5)

    assert summary == "owner action queue: 0 families, 0 callsites, 0 text arguments across 0 source files"
