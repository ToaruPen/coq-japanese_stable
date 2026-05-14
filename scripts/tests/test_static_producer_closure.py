from __future__ import annotations

from pathlib import Path

from scripts.static_producer_closure import (
    COVERED_BY_OWNER_PATCH,
    COVERED_OWNER_CALLSITES,
    COVERED_OWNER_FAMILIES,
    covered_callsite_keys,
    covered_family_ids,
    family_closure_status,
    format_owner_action_queue,
    load_inventory,
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
    source_entries = owner_action_queue_by_file(inventory)
    trade_ui_entry = next(entry for entry in source_entries if entry["source_file"] == "XRL.UI/TradeUI.cs")
    examine_id = "XRL.UI/TradeUI.cs::XRL.UI.TradeUI.DoVendorExamine"
    recharge_id = "XRL.UI/TradeUI.cs::XRL.UI.TradeUI.DoVendorRecharge"

    assert raw_families[examine_id]["family_closure_status"] == "needs_family_review"
    assert raw_families[recharge_id]["family_closure_status"] == "needs_family_review"
    assert family_closure_status(raw_families[examine_id]) == "needs_family_review"
    assert family_closure_status(raw_families[recharge_id]) == "needs_family_review"
    assert examine_id not in covered_family_ids()
    assert recharge_id not in covered_family_ids()
    assert examine_id not in queued_family_ids
    assert recharge_id not in queued_family_ids
    assert [family["member_name"] for family in trade_ui_entry["families"]] == ["ShowTradeScreen"]


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
    """ConversationScript popup owner callsites must close without claiming fixed or runtime popups."""
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
        assert family_id in queued_family_ids

    conversation_entry = next(
        entry for entry in source_entries if entry["source_file"] == "XRL.World.Parts/ConversationScript.cs"
    )
    assert conversation_entry["callsite_count"] == 6
    assert [family["member_name"] for family in conversation_entry["families"]] == [
        "IsPhysicalConversationPossible",
        "IsMentalConversationPossible",
    ]


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

    assert "XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleEvent" in queued_family_ids
    assert "XRL.World.Parts/TerrainTravel.cs::XRL.World.Parts.TerrainTravel.HandleLeavingCell" not in queued_family_ids
    terrain_entry = next(
        entry for entry in source_entries if entry["source_file"] == "XRL.World.Parts/TerrainTravel.cs"
    )
    assert terrain_entry["callsite_count"] == 2
    assert [family["member_name"] for family in terrain_entry["families"]] == ["HandleEvent"]


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


def test_zone_manager_owner_queue_callsites_are_split_from_runtime_and_popup_shapes() -> None:
    """ZoneManager queue owner callsites must close without claiming runtime or popup shapes."""
    inventory = load_inventory(TRACKED_INVENTORY)
    raw_families = {family["producer_family_id"]: family for family in inventory["families"]}
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}
    source_entries = owner_action_queue_by_file(inventory)
    zone_manager_family_ids = {
        "XRL.World/ZoneManager.cs::XRL.World.ZoneManager.SetActiveZone",
        "XRL.World/ZoneManager.cs::XRL.World.ZoneManager.GenerateZone",
    }

    for family_id in zone_manager_family_ids:
        assert raw_families[family_id]["family_closure_status"] == "needs_family_review"
        assert family_closure_status(raw_families[family_id]) == "needs_family_review"
        assert family_id not in covered_family_ids()
        assert family_id in queued_family_ids

    zone_manager_entry = next(
        entry for entry in source_entries if entry["source_file"] == "XRL.World/ZoneManager.cs"
    )
    assert zone_manager_entry["callsite_count"] == 4
    assert [family["member_name"] for family in zone_manager_entry["families"]] == [
        "SetActiveZone",
        "GenerateZone",
    ]
    assert [
        family["representative_lines"] for family in zone_manager_entry["families"]
    ] == [[1889, 1912], [3213, 3570]]
    assert [
        family["closure_status_counts"] for family in zone_manager_entry["families"]
    ] == [
        {"runtime_required": 2},
        {"messages_candidate": 1, "owner_patch_required": 1},
    ]
    assert [family["surface_counts"] for family in zone_manager_entry["families"]] == [
        {"AddPlayerMessage": 2},
        {"Popup.Show*": 2},
    ]


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
    """Remaining pure single-callsite popup owners close while PhotosyntheticSkin stays deferred."""
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
        "XRL.World.Parts/Toolbox.cs::XRL.World.Parts.Toolbox.HandleBonus",
    }
    deferred_family_id = (
        "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs::"
        "XRL.World.Parts.Mutation.PhotosyntheticSkin.HandleEvent"
    )

    for family_id in family_ids:
        assert raw_families[family_id]["family_closure_status"] == "owner_patch_required"
        assert family_id in covered_family_ids()
        assert family_id not in queued_family_ids

    assert raw_families[deferred_family_id]["family_closure_status"] == "owner_patch_required"
    assert deferred_family_id not in covered_family_ids()
    assert deferred_family_id in queued_family_ids


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

    xrlcore_entry = next(entry for entry in source_entries if entry["source_file"] == "XRL.Core/XRLCore.cs")
    assert "SaveManagement" not in [family["member_name"] for family in xrlcore_entry["families"]]
    assert "PlayerTurn" in [family["member_name"] for family in xrlcore_entry["families"]]


def test_uncovered_high_volume_owner_family_remains_in_owner_action_queue() -> None:
    """Uncovered high-volume owner families must stay actionable."""
    inventory = load_inventory(TRACKED_INVENTORY)
    queued_family_ids = {family["producer_family_id"] for family in owner_action_queue(inventory)}

    assert "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookFromIngredients" in queued_family_ids


def test_owner_action_queue_groups_actionable_work_by_source_file() -> None:
    """Static producer work queue must expose class-file starting points."""
    inventory = load_inventory(TRACKED_INVENTORY)
    source_entries = owner_action_queue_by_file(inventory)
    campfire_entry = next(entry for entry in source_entries if entry["source_file"] == "XRL.World.Parts/Campfire.cs")

    assert source_entries == sorted(
        source_entries,
        key=lambda entry: (
            -entry["family_count"],
            -entry["text_argument_count"],
            -entry["callsite_count"],
            entry["source_file"],
        ),
    )
    assert campfire_entry["family_count"] > 0
    assert campfire_entry["text_argument_count"] > 0
    assert any(
        family["producer_family_id"]
        == "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookFromIngredients"
        for family in campfire_entry["families"]
    )


def test_owner_action_queue_text_summary_names_source_files_and_methods() -> None:
    """Text output must be useful as an agent handoff queue."""
    inventory = load_inventory(TRACKED_INVENTORY)

    summary = format_owner_action_queue(inventory, limit=5)

    assert "owner action queue:" in summary
    assert ".cs:" in summary
    assert "surfaces=" in summary
    assert "line " in summary
