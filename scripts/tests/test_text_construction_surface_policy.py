from __future__ import annotations

from pathlib import Path
from typing import Any

from scripts.text_construction_surface_policy import (
    TextConstructionFamily,
    TextConstructionInventory,
    build_surface_queue,
    classify_family,
    format_surface_queue,
    lane_summary_payload,
    load_inventory,
    queue_payload,
    valuable_surface_queue,
)


def test_policy_treats_known_visible_apis_as_player_visible() -> None:
    """Known display/log/journal/description APIs are valuable localization surfaces."""
    inventory = _inventory(
        [
            _family("Demo.cs::TextRoutes.Popup()", "Demo.cs", "Popup", {"Popup": 1}),
            _family("Demo.cs::TextRoutes.Message()", "Demo.cs", "Message", {"MessageFrame": 1}),
            _family(
                "XRL.World.Effects/Asleep.cs::Asleep.GetDescription()",
                "XRL.World.Effects/Asleep.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = valuable_surface_queue(inventory)

    assert [entry["classification"] for entry in entries] == [
        "player_visible_api",
        "player_visible_api",
        "player_visible_api",
    ]
    assert {tuple(entry["player_visible_surfaces"]) for entry in entries} == {
        ("Popup",),
        ("MessageFrame",),
        ("EffectDescriptionReturn",),
    }


def test_policy_promotes_ui_and_semantic_assignments_without_promoting_internal_text() -> None:
    """Assignments are valuable only when their owner context is likely player-visible."""
    inventory = _inventory(
        [
            _family(
                "Qud.UI/InventoryLine.cs::InventoryLine.setData(object)",
                "Qud.UI/InventoryLine.cs",
                "setData",
                {"SetText": 1},
            ),
            _family(
                "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.Build()",
                "XRL.World.ZoneBuilders/VillageBase.cs",
                "Build",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                "Overlay.MapEditor/MapEditorView.cs::MapEditorView.Render()",
                "Overlay.MapEditor/MapEditorView.cs",
                "Render",
                {"DirectTextAssignment": 1},
            ),
            _family("Internal.cs::Internal.Fields", "Internal.cs", "Fields", {"Attribute": 1, "Initializer": 1}),
        ]
    )

    entries = build_surface_queue(inventory)
    classifications = {entry["family_id"]: entry["classification"] for entry in entries}

    assert classifications["Qud.UI/InventoryLine.cs::InventoryLine.setData(object)"] == "player_visible_owner_candidate"
    assert (
        classifications["XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.Build()"]
        == "player_visible_owner_candidate"
    )
    assert classifications["Overlay.MapEditor/MapEditorView.cs::MapEditorView.Render()"] == "candidate_only"
    assert classifications["Internal.cs::Internal.Fields"] == "non_target"


def test_policy_keeps_debug_or_wish_routes_out_of_valuable_queue() -> None:
    """Debug-like visible APIs are not normal gameplay localization coverage."""
    inventory = _inventory(
        [
            _family(
                "XRL.Wish/WishManager.cs::WishManager.HandleWish(string)",
                "XRL.Wish/WishManager.cs",
                "HandleWish",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/WishMenu.cs::WishMenu.Show()",
                "XRL.World.Parts/WishMenu.cs",
                "Show",
                {"Popup": 1},
            ),
            _family(
                "XRL.World/StatWishHandler.cs::StatWishHandler.HandleWish(string)",
                "XRL.World/StatWishHandler.cs",
                "HandleWish",
                {"AddPlayerMessage": 1},
            ),
            _family(
                "XRL.World.Capabilities/Wishing.cs::Wishing.HandleWish(GameObject,string)",
                "XRL.World.Capabilities/Wishing.cs",
                "HandleWish",
                {"Popup": 1, "AddPlayerMessage": 1},
            ),
            _family(
                "XRL.World.Parts/Combat.cs::Combat.Attack()",
                "XRL.World.Parts/Combat.cs",
                "Attack",
                {"MessageFrame": 1},
            ),
        ]
    )

    entries = build_surface_queue(inventory)
    classifications = {entry["family_id"]: entry["classification"] for entry in entries}

    assert (
        classifications["XRL.World.Capabilities/Wishing.cs::Wishing.HandleWish(GameObject,string)"]
        == "candidate_only"
    )
    assert classifications["XRL.Wish/WishManager.cs::WishManager.HandleWish(string)"] == "candidate_only"
    assert classifications["XRL.World.Parts/WishMenu.cs::WishMenu.Show()"] == "candidate_only"
    assert classifications["XRL.World/StatWishHandler.cs::StatWishHandler.HandleWish(string)"] == "candidate_only"
    assert classifications["XRL.World.Parts/Combat.cs::Combat.Attack()"] == "player_visible_api"
    assert [entry["source_file"] for entry in valuable_surface_queue(inventory)] == ["XRL.World.Parts/Combat.cs"]


def test_policy_assigns_actionable_closure_lanes() -> None:
    """Valuable queue entries are split into closure lanes for issue-sized ownership work."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Parts/Combat.cs::Combat.Attack()",
                "XRL.World.Parts/Combat.cs",
                "Attack",
                {"MessageFrame": 2, "Does": 1},
            ),
            _family(
                "XRL.World.ZoneBuilders/Village.cs::Village.BuildZone(Zone)",
                "XRL.World.ZoneBuilders/Village.cs",
                "BuildZone",
                {"HistoricStringExpander": 3},
            ),
            _family(
                "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.setData(object)",
                "Qud.UI/TinkeringDetailsLine.cs",
                "setData",
                {"SetText": 2},
            ),
            _family(
                "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.BestowElement(string)",
                "XRL.World.Parts/CherubimSpawner.cs",
                "BestowElement",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                "XRL.World.Conversations.Parts/Trade.cs::Trade.HandleEvent(GetChoiceTagEvent)",
                "XRL.World.Conversations.Parts/Trade.cs",
                "HandleEvent",
                {"ConversationChoiceTag": 1},
            ),
            _family(
                "XRL.World.Conversations.Parts/WaterRitual.cs::WaterRitual.HandleEvent(DisplayTextEvent)",
                "XRL.World.Conversations.Parts/WaterRitual.cs",
                "HandleEvent",
                {"ConversationTextAppend": 1},
            ),
        ]
    )

    lanes = {entry["family_id"]: entry["closure_lane"] for entry in valuable_surface_queue(inventory)}

    assert lanes["XRL.World.Parts/Combat.cs::Combat.Attack()"] == "combat_message_frame_does"
    assert lanes["XRL.World.ZoneBuilders/Village.cs::Village.BuildZone(Zone)"] == "history_generated_text"
    assert (
        lanes["Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.setData(object)"]
        == "screen_ui_direct_text"
    )
    assert (
        lanes["XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.BestowElement(string)"]
        == "display_name_composition"
    )
    assert (
        lanes["XRL.World.Conversations.Parts/Trade.cs::Trade.HandleEvent(GetChoiceTagEvent)"]
        == "conversation_routes"
    )
    assert (
        lanes["XRL.World.Conversations.Parts/WaterRitual.cs::WaterRitual.HandleEvent(DisplayTextEvent)"]
        == "conversation_routes"
    )


def test_policy_closes_reviewed_conversation_choice_tags_and_classifies_body_routes() -> None:
    """Issue-719 conversation routes carry owner-route closure without static producer conflation."""
    trade_family_id = "XRL.World.Conversations.Parts/Trade.cs::Trade.HandleEvent(GetChoiceTagEvent)"
    mound_family_id = "XRL.World.Conversations.Parts/MoundContext.cs::MoundContext.HandleEvent(PrepareTextEvent)"
    signpost_family_id = "XRL.World.Conversations.Parts/QuestSignpost.cs::QuestSignpost.HandleEvent(PrepareTextEvent)"
    glotrot_family_id = "XRL.World.Conversations.Parts/GlotrotFilter.cs::GlotrotFilter.HandleEvent(PrepareTextEvent)"

    inventory = _inventory(
        [
            _family(
                trade_family_id,
                "XRL.World.Conversations.Parts/Trade.cs",
                "HandleEvent",
                {"ConversationChoiceTag": 1},
            ),
            _family(
                mound_family_id,
                "XRL.World.Conversations.Parts/MoundContext.cs",
                "HandleEvent",
                {"ConversationTextReplace": 1},
            ),
            _family(
                signpost_family_id,
                "XRL.World.Conversations.Parts/QuestSignpost.cs",
                "HandleEvent",
                {"ConversationTextReplace": 1},
            ),
            _family(
                glotrot_family_id,
                "XRL.World.Conversations.Parts/GlotrotFilter.cs",
                "HandleEvent",
                {"ConversationTextAppend": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[trade_family_id]["closure_status"] == "covered_by_owner_route"
    assert "ConversationDisplayTextPatchTests.cs" in " ".join(entries[trade_family_id]["closure_evidence"])
    assert entries[mound_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[signpost_family_id]["closure_status"] == "covered_by_owner_route"
    assert "display-name/data-source routes" in " ".join(entries[signpost_family_id]["closure_evidence"])
    assert entries[glotrot_family_id]["closure_status"] == "runtime_required"


def test_policy_applies_reviewed_closure_overlay_for_high_risk_combat_lane() -> None:
    """High-risk text-construction lanes can carry reviewed owner-route closure evidence."""
    shield_slam_family_id = (
        "XRL.World.Parts.Skill/Shield_Slam.cs::"
        "Shield_Slam.Slam(GameObject,GameObject,Cell,bool)"
    )
    cudgel_slam_cast_family_id = (
        "XRL.World.Parts.Skill/Cudgel_Slam.cs::"
        "Cudgel_Slam.Cast(GameObject,Cudgel_Slam,string,GameObject,bool,int,string)"
    )
    inventory = _inventory(
        [
            _family(
                "XRL.World.Parts/Combat.cs::Combat.MeleeAttackWithWeaponInternal(GameObject,GameObject,GameObject,BodyPart,string,int,int,int,int,int,bool,bool)",
                "XRL.World.Parts/Combat.cs",
                "MeleeAttackWithWeaponInternal",
                {"AddPlayerMessage": 1, "Does": 1},
            ),
            _family(
                "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging()",
                "XRL.World.Parts/BandageMedication.cs",
                "PerformBandaging",
                {"MessageFrame": 1},
            ),
            _family(
                shield_slam_family_id,
                "XRL.World.Parts.Skill/Shield_Slam.cs",
                "Slam",
                {"MessageFrame": 1},
            ),
            _family(
                cudgel_slam_cast_family_id,
                "XRL.World.Parts.Skill/Cudgel_Slam.cs",
                "Cast",
                {"Popup": 7, "MessageFrame": 3},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    covered = entries[
        "XRL.World.Parts/Combat.cs::Combat.MeleeAttackWithWeaponInternal(GameObject,GameObject,GameObject,BodyPart,string,int,int,int,int,int,bool,bool)"
    ]
    unreviewed = entries["XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging()"]
    shield_slam = entries[shield_slam_family_id]
    cudgel_slam_cast = entries[cudgel_slam_cast_family_id]

    assert covered["closure_lane"] == "combat_message_frame_does"
    assert covered["closure_status"] == "covered_by_owner_route"
    assert "CombatAndLogMessageQueuePatchTests.cs" in " ".join(covered["closure_evidence"])
    assert unreviewed["closure_status"] == "unreviewed"
    assert shield_slam["closure_status"] == "covered_by_owner_route"
    assert "shield slam possessive capture" in " ".join(shield_slam["closure_evidence"])
    assert cudgel_slam_cast["closure_status"] == "covered_by_owner_route"
    assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in " ".join(cudgel_slam_cast["closure_evidence"])
    assert "Issue #747 skill-originated" in " ".join(cudgel_slam_cast["closure_evidence"])


def test_policy_closes_issue747_journal_and_skill_rows_with_owner_route_evidence() -> None:
    """Issue-747 scoped journal/quest and skill rows are no longer left as action items."""
    journal_family_id = "XRL.World.Parts/LocationFinder.cs::LocationFinder.TriggerFind()"
    unreviewed_journal_family_id = "XRL.World.Parts/UnreviewedJournal.cs::UnreviewedJournal.TriggerFind()"
    tactics_charge_family_id = "XRL.World.Parts.Skill/Tactics_Charge.cs::Tactics_Charge.PerformCharge()"
    tactics_death_from_above_family_id = (
        "XRL.World.Parts.Skill/Tactics_DeathFromAbove.cs::"
        "Tactics_DeathFromAbove.PerformDeathFromAbove(GameObject,GameObject,string)"
    )
    physic_amputate_family_id = "XRL.World.Parts.Skill/Physic_AmputateLimb.cs::Physic_AmputateLimb.FireEvent(Event)"
    tinkering_mine_family_id = "XRL.World.Parts/Tinkering_Mine.cs::Tinkering_Mine.AttemptDisarm(GameObject,IEvent,bool)"
    unreviewed_skill_family_id = "XRL.World.Parts.Skill/Unreviewed_Skill.cs::Unreviewed_Skill.FireEvent(Event)"
    inventory = _inventory(
        [
            _family(
                journal_family_id,
                "XRL.World.Parts/LocationFinder.cs",
                "TriggerFind",
                {"JournalAPI": 1, "Popup": 1},
            ),
            _family(
                unreviewed_journal_family_id,
                "XRL.World.Parts/UnreviewedJournal.cs",
                "TriggerFind",
                {"JournalAPI": 1, "Popup": 1},
            ),
            _family(
                tactics_charge_family_id,
                "XRL.World.Parts.Skill/Tactics_Charge.cs",
                "PerformCharge",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                tactics_death_from_above_family_id,
                "XRL.World.Parts.Skill/Tactics_DeathFromAbove.cs",
                "PerformDeathFromAbove",
                {"MessageFrame": 1},
            ),
            _family(
                physic_amputate_family_id,
                "XRL.World.Parts.Skill/Physic_AmputateLimb.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                tinkering_mine_family_id,
                "XRL.World.Parts/Tinkering_Mine.cs",
                "AttemptDisarm",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                unreviewed_skill_family_id,
                "XRL.World.Parts.Skill/Unreviewed_Skill.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[journal_family_id]["closure_lane"] == "journal_quest_routes"
    assert entries[journal_family_id]["closure_status"] == "covered_by_owner_route"
    journal_evidence = " ".join(entries[journal_family_id]["closure_evidence"])
    assert journal_family_id in journal_evidence
    assert "JournalApiAddTranslationPatchTests.cs" in journal_evidence
    assert "PopupShowTranslationPatchTests.cs" in journal_evidence
    assert entries[unreviewed_journal_family_id]["closure_lane"] == "journal_quest_routes"
    assert entries[unreviewed_journal_family_id]["closure_status"] == "unreviewed"
    assert entries[tactics_charge_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[tactics_death_from_above_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[physic_amputate_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[tinkering_mine_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[unreviewed_skill_family_id]["closure_status"] == "unreviewed"
    skill_evidence = " ".join(entries[tactics_charge_family_id]["closure_evidence"])
    assert tactics_charge_family_id in skill_evidence
    assert "Issue #747 reviewed skill-originated static family" in skill_evidence
    message_only_evidence = " ".join(entries[tactics_death_from_above_family_id]["closure_evidence"])
    assert "MessageFrames/verbs.ja.json" in message_only_evidence
    assert "SingleCallsiteOwnerPopupTranslationPatch.cs" in message_only_evidence
    physic_message_only_evidence = " ".join(entries[physic_amputate_family_id]["closure_evidence"])
    assert "PhysicAmputateLimbTranslationPatch.cs" in physic_message_only_evidence
    assert "MessageFrames/verbs.ja.json" in physic_message_only_evidence


def test_policy_separates_reviewed_issue711_work_without_overclaiming_closure() -> None:
    """Reviewed issue-711 families distinguish closed, partial, runtime, and likely-gap work."""
    missile_hit_family_id = (
        "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.MissileHit("
        "GameObject,GameObject,GameObject,GameObject,Projectile,GameObject,GameObject,"
        "MissilePath,Cell,FireType,int,int,int,bool,GameObject,bool,ref bool,ref bool,ref bool,bool,bool)"
    )
    inventory_family_id = "XRL.World.Parts/Inventory.cs::Inventory.FireEvent(Event)"
    tombstone_family_id = "XRL.World.Parts/Tombstone.cs::Tombstone.GenerateTombstone()"
    mod_gigantic_fixed_family_id = "XRL.World.Parts/ModGigantic.cs::ModGigantic.GetDescription(int)"
    mod_gigantic_dynamic_family_id = "XRL.World.Parts/ModGigantic.cs::ModGigantic.GetDescription(int,GameObject)"
    tinkering_details_family_id = "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.setData(FrameworkDataElement)"

    inventory = _inventory(
        [
            _family(
                missile_hit_family_id,
                "XRL.World.Parts/MissileWeapon.cs",
                "MissileHit",
                {"Does": 1, "EmitMessage": 1},
            ),
            _family(
                inventory_family_id,
                "XRL.World.Parts/Inventory.cs",
                "FireEvent",
                {"Popup": 1, "MessageFrame": 1},
            ),
            _family(
                tombstone_family_id,
                "XRL.World.Parts/Tombstone.cs",
                "GenerateTombstone",
                {"HistoricStringExpander": 1},
            ),
            _family(
                mod_gigantic_fixed_family_id,
                "XRL.World.Parts/ModGigantic.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                mod_gigantic_dynamic_family_id,
                "XRL.World.Parts/ModGigantic.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                tinkering_details_family_id,
                "Qud.UI/TinkeringDetailsLine.cs",
                "setData",
                {"SetText": 6},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[missile_hit_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[inventory_family_id]["closure_status"] == "covered_by_owner_route"
    inventory_evidence = " ".join(entries[inventory_family_id]["closure_evidence"])
    assert "InventoryFireEventTranslationPatch.cs" in inventory_evidence
    assert "BeginBeingUnequippedFailureMessageTranslationPatch.cs" in inventory_evidence
    assert "cannot-budge" in inventory_evidence
    assert "You can't remove {item} FailureMessage helper shape" in inventory_evidence
    assert entries[tombstone_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[mod_gigantic_fixed_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[mod_gigantic_dynamic_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[tinkering_details_family_id]["closure_status"] == "covered_by_owner_route"

    mod_gigantic_fixed_evidence = " ".join(entries[mod_gigantic_fixed_family_id]["closure_evidence"])
    assert "world-mods.ja.json" in mod_gigantic_fixed_evidence
    assert "DescriptionShortDescriptionPatchTests.cs" in mod_gigantic_fixed_evidence

    mod_gigantic_dynamic_evidence = " ".join(entries[mod_gigantic_dynamic_family_id]["closure_evidence"])
    assert "WorldModsTextTranslator.cs" in mod_gigantic_dynamic_evidence
    assert "TinkeringDetailsLineTranslationPatch.cs" in mod_gigantic_dynamic_evidence
    assert "TinkeringTranslationPatchTests.cs" in mod_gigantic_dynamic_evidence
    tinkering_details_evidence = " ".join(entries[tinkering_details_family_id]["closure_evidence"])
    assert "TinkeringDetailsLineTranslationPatch.cs" in tinkering_details_evidence
    assert "TinkeringTranslationPatchTests.cs" in tinkering_details_evidence


def test_policy_records_issue762_first_slice_without_overclaiming_family_closure() -> None:
    """Issue-762 first slices are partial coverage until sibling route shapes are split."""
    schemasoft_init_family_id = "XRL.World.Parts/CyberneticsSchemasoft.cs::CyberneticsSchemasoft.InitChip(bool)"
    schemasoft_description_family_id = (
        "XRL.World.Parts/CyberneticsSchemasoft.cs::"
        "CyberneticsSchemasoft.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    )
    longblades_initialize_family_id = "XRL.World.Parts/LongBladesCore.cs::LongBladesCore.Initialize()"
    sparking_baetyl_rewards_family_id = (
        "XRL.World/RandomAltarBaetylRewardManager.cs::"
        "RandomAltarBaetylRewardManager.HandleRewardNode(XmlDataHelper)"
    )
    turret_family_id = "XRL.World.Parts/TurretTinker.cs::TurretTinker.FireEvent(Event)"
    bandage_family_id = (
        "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging(GameObject,GameObject)"
    )
    tonic_family_id = "XRL.World.Parts/Tonic.cs::Tonic.HandleEvent(InventoryActionEvent)"
    multihorns_family_id = (
        "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.PerformCharge(List<Cell>,bool)"
    )
    multihorns_mutate_family_id = (
        "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.Mutate(GameObject,int)"
    )
    xrlcore_start_family_id = "XRL.Core/XRLCore.cs::XRLCore._Start()"
    missile_showpicker_family_id = (
        "XRL.World.Parts/MissileWeapon.cs::"
        "MissileWeapon.ShowPicker(int,int,bool,AllowVis,int,bool,GameObject,ref FireType,int)"
    )
    inventory = _inventory(
        [
            _family(
                schemasoft_init_family_id,
                "XRL.World.Parts/CyberneticsSchemasoft.cs",
                "InitChip",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                schemasoft_description_family_id,
                "XRL.World.Parts/CyberneticsSchemasoft.cs",
                "HandleEvent",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                longblades_initialize_family_id,
                "XRL.World.Parts/LongBladesCore.cs",
                "Initialize",
                {"ActivatedAbility": 3},
            ),
            _family(
                sparking_baetyl_rewards_family_id,
                "XRL.World/RandomAltarBaetylRewardManager.cs",
                "HandleRewardNode",
                {"DescriptionAssignment": 1},
            ),
            _family(
                turret_family_id,
                "XRL.World.Parts/TurretTinker.cs",
                "FireEvent",
                {"ActivatedAbility": 1, "Popup": 1},
            ),
            _family(
                bandage_family_id,
                "XRL.World.Parts/BandageMedication.cs",
                "PerformBandaging",
                {"MessageFrame": 2, "Does": 2},
            ),
            _family(
                tonic_family_id,
                "XRL.World.Parts/Tonic.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1, "Does": 2, "MessageFrame": 3},
            ),
            _family(
                multihorns_family_id,
                "XRL.World.Parts.Mutation/MultiHorns.cs",
                "PerformCharge",
                {"MessageFrame": 3},
            ),
            _family(
                multihorns_mutate_family_id,
                "XRL.World.Parts.Mutation/MultiHorns.cs",
                "Mutate",
                {"ActivatedAbility": 1, "DisplayNameAssignment": 4},
            ),
            _family(
                xrlcore_start_family_id,
                "XRL.Core/XRLCore.cs",
                "_Start",
                {"Initializer": 4, "Other": 10, "OtherInvocation": 75, "Popup": 3},
            ),
            _family(
                missile_showpicker_family_id,
                "XRL.World.Parts/MissileWeapon.cs",
                "ShowPicker",
                {"Popup": 176},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[schemasoft_init_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[schemasoft_description_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[longblades_initialize_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[sparking_baetyl_rewards_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[multihorns_mutate_family_id]["closure_status"] == "covered_by_owner_route"

    assert entries[turret_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[bandage_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[tonic_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[multihorns_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[xrlcore_start_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[missile_showpicker_family_id]["closure_status"] == "covered_by_owner_route"

    _assert_issue762_evidence(
        entries,
        {
            "schemasoft_init": schemasoft_init_family_id,
            "schemasoft_description": schemasoft_description_family_id,
            "longblades_initialize": longblades_initialize_family_id,
            "sparking_baetyl_rewards": sparking_baetyl_rewards_family_id,
            "turret": turret_family_id,
            "bandage": bandage_family_id,
            "tonic": tonic_family_id,
            "multihorns": multihorns_family_id,
            "multihorns_mutate": multihorns_mutate_family_id,
            "xrlcore_start": xrlcore_start_family_id,
            "missile_showpicker": missile_showpicker_family_id,
        },
    )


def _assert_issue762_evidence(
    entries: dict[str, dict[str, Any]],
    family_ids: dict[str, str],
) -> None:
    _assert_evidence_contains(
        entries,
        family_ids["schemasoft_init"],
        "GetDisplayNameRouteTranslator.cs",
        "GetDisplayNameRouteTranslatorTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["schemasoft_description"],
        "CyberneticsBehaviorDescriptionTranslationPatch.cs",
        "CyberneticsBehaviorDescriptionTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["longblades_initialize"],
        "ActivatedAbilityNameTranslator.cs",
        "AbilityBarButtonTextTranslationPatchTests.cs",
        "AbilityManagerLineTranslationPatchTests.cs",
        "AbilityManagerScreenTranslationPatchTests.cs",
        "ui-skillsandpowers.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["sparking_baetyl_rewards"],
        "SparkingBaetyls.jp.xml",
        "test_sparking_baetyl_rewards.py",
        "popup and wish routes remain separate",
    )
    _assert_evidence_contains(
        entries,
        family_ids["turret"],
        "ActivatedAbilityNameTranslator.cs",
        "AbilityBarButtonTextTranslationPatchTests.cs",
        "ui-pick-target.ja.json",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["bandage"],
        "ui-pick-target.ja.json",
        "messages.ja.json",
        "MessageFrameTranslatorTests.cs",
        "MessagePatternTranslatorTests.cs",
        "LocalizationCoverageTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["tonic"],
        "SingleCallsiteOwnerQueueTranslationPatch.cs",
        "PickTargetWindowTextTranslator.cs",
        "SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "PickTargetWindowUpdateTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["multihorns"],
        "MessageFrameTranslatorTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "PhysicsProcessTakeDamageTranslationPatch.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["multihorns_mutate"],
        "ui-displayname-atomic.ja.json",
        "StatusScreenBindingOwnerPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["xrlcore_start"],
        "XrlCoreStartMainMenuTranslationPatch.cs",
        "LegacyGamepadPromptTranslationHelpers.cs",
        "LegacyGamepadPromptTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["missile_showpicker"],
        "MissileWeaponShowPickerTranslationPatch.cs",
        "PickTargetWindowTextTranslator.cs",
        "ui-pick-target.ja.json",
        "LegacyGamepadPromptTranslationPatchTests.cs",
        "UITextSkinTranslationPatchTests.cs",
    )


def _assert_evidence_contains(
    entries: dict[str, dict[str, Any]],
    family_id: str,
    *fragments: str,
) -> None:
    evidence = " ".join(entries[family_id]["closure_evidence"])
    for fragment in fragments:
        assert fragment in evidence


def test_policy_defers_unused_base_game_sifrah_to_runtime_evidence() -> None:
    """PsychicCombatSifrah has owner coverage, but the base game does not route through it."""
    family_id = (
        "XRL.World/PsychicCombatSifrah.cs::"
        "PsychicCombatSifrah.PsychicCombatSifrah(GameObject,string,int,int,string)"
    )
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World/PsychicCombatSifrah.cs",
                "PsychicCombatSifrah",
                {"Popup": 1, "Initializer": 6},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_status"] == "runtime_required"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "not used in the base game" in evidence
    assert "SifrahPureOwnerPopupTranslationPatchTests.cs" in evidence


def test_policy_records_issue762_generated_display_and_pit_routes() -> None:
    """Issue-762 finite display/description owner routes are closed without sibling overclaiming."""
    cyclopean_prism_action_family_id = (
        "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.HandleEvent(BeginTakeActionEvent)"
    )
    cyclopean_prism_reset_family_id = "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.ResetPrism()"
    pit_material_paint_family_id = "XRL.World.Parts/PitMaterial.cs::PitMaterial.PaintPit()"
    pit_material_fire_family_id = "XRL.World.Parts/PitMaterial.cs::PitMaterial.FireEvent(Event)"
    templar_phylactery_family_id = (
        "XRL.World.Parts/TemplarPhylactery.cs::"
        "TemplarPhylactery.HandleEvent(AfterObjectCreatedEvent)"
    )
    inventory = _inventory(
        [
            _family(
                cyclopean_prism_action_family_id,
                "XRL.World.Parts/CyclopeanPrism.cs",
                "HandleEvent",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                cyclopean_prism_reset_family_id,
                "XRL.World.Parts/CyclopeanPrism.cs",
                "ResetPrism",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                pit_material_paint_family_id,
                "XRL.World.Parts/PitMaterial.cs",
                "PaintPit",
                {"DisplayNameAssignment": 2, "DescriptionAssignment": 1},
            ),
            _family(
                pit_material_fire_family_id,
                "XRL.World.Parts/PitMaterial.cs",
                "FireEvent",
                {"DisplayNameAssignment": 2, "DescriptionAssignment": 1},
            ),
            _family(
                templar_phylactery_family_id,
                "XRL.World.Parts/TemplarPhylactery.cs",
                "HandleEvent",
                {"DisplayNameAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[cyclopean_prism_action_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[cyclopean_prism_reset_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[pit_material_paint_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[pit_material_fire_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[templar_phylactery_family_id]["closure_status"] == "covered_by_owner_route"

    cyclopean_prism_evidence = " ".join(entries[cyclopean_prism_action_family_id]["closure_evidence"])
    assert "GetDisplayNameRouteTranslator.cs" in cyclopean_prism_evidence
    assert "PtohAnnoyed popup and Die text remain separate" in cyclopean_prism_evidence

    pit_material_evidence = " ".join(entries[pit_material_paint_family_id]["closure_evidence"])
    assert "ui-displayname-atomic.ja.json" in pit_material_evidence
    assert "descriptions.ja.json" in pit_material_evidence
    assert "DescriptionShortDescriptionPatchTests.cs" in pit_material_evidence

    templar_phylactery_evidence = " ".join(entries[templar_phylactery_family_id]["closure_evidence"])
    assert "generated English-prefix display-name route" in templar_phylactery_evidence
    assert "hacking popup and spawn message families remain separate" in templar_phylactery_evidence


def test_policy_records_issue762_evil_twin_route_split() -> None:
    """EvilTwin display/description coverage is split from arbitrary caller popup routes."""
    create_family_id = (
        "XRL.World.Parts.Mutation/EvilTwin.cs::"
        "EvilTwin.CreateEvilTwin(GameObject,string,Cell,string,string,GameObject,string,bool,string,string)"
    )
    description_family_id = "XRL.World.Parts.Mutation/EvilTwin.cs::EvilTwin.GetDescription()"
    inventory = _inventory(
        [
            _family(
                create_family_id,
                "XRL.World.Parts.Mutation/EvilTwin.cs",
                "CreateEvilTwin",
                {"DisplayNameAssignment": 3, "Assignment": 5},
            ),
            _family(
                description_family_id,
                "XRL.World.Parts.Mutation/EvilTwin.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[create_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[description_family_id]["closure_status"] == "covered_by_owner_route"

    create_evidence = " ".join(entries[create_family_id]["closure_evidence"])
    assert "GetDisplayNameRouteTranslator.cs" in create_evidence
    assert "descriptions.ja.json" in create_evidence
    assert "ui-popup.ja.json" in create_evidence
    assert "deferred until a concrete producer/callsite proves a visible localization gap" in create_evidence

    description_evidence = " ".join(entries[description_family_id]["closure_evidence"])
    assert "mutation-descriptions.ja.json" in description_evidence
    assert "runtime clone creation display/popup text is tracked separately" in description_evidence


def test_policy_records_issue762_cherubim_generated_text_routes() -> None:
    """Cherubim element and hexacherubim generation routes have owner-route coverage."""
    cherubim_handle_family_id = (
        "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.HandleEvent(BeforeObjectCreatedEvent)"
    )
    cherubim_bestow_family_id = (
        "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.BestowElement(GameObject,string,bool)"
    )
    hexacherubim_family_id = (
        "XRL.World.Parts/HexacherubimSpawner.cs::HexacherubimSpawner.HandleEvent(BeforeObjectCreatedEvent)"
    )
    inventory = _inventory(
        [
            _family(
                cherubim_handle_family_id,
                "XRL.World.Parts/CherubimSpawner.cs",
                "HandleEvent",
                {"DisplayNameAssignment": 1, "DescriptionAssignment": 1},
            ),
            _family(
                cherubim_bestow_family_id,
                "XRL.World.Parts/CherubimSpawner.cs",
                "BestowElement",
                {"DisplayNameAssignment": 1, "PartTextAssignment": 1},
            ),
            _family(
                hexacherubim_family_id,
                "XRL.World.Parts/HexacherubimSpawner.cs",
                "HandleEvent",
                {"DisplayNameAssignment": 1, "DescriptionAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[cherubim_handle_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[cherubim_bestow_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[hexacherubim_family_id]["closure_status"] == "covered_by_owner_route"

    handle_evidence = " ".join(entries[cherubim_handle_family_id]["closure_evidence"])
    assert "CherubimSpawnerHandleEventTranslationPatch.cs" in handle_evidence
    assert "CherubimSpawnerBestowElementTranslationPatch.cs" in handle_evidence
    assert "base organic/mechanical cherub descriptions" in handle_evidence
    assert "faction-derived object-name composition remains dynamic data" in handle_evidence

    bestow_evidence = " ".join(entries[cherubim_bestow_family_id]["closure_evidence"])
    assert "CherubimSpawnerBestowElementTranslationPatch.cs" in bestow_evidence
    assert "CherubimSpawnerGeneratedTextTranslationPatchTests.cs" in bestow_evidence
    assert "PrependName=false intentionally leaves display names unchanged" in bestow_evidence

    hexacherubim_evidence = " ".join(entries[hexacherubim_family_id]["closure_evidence"])
    assert "HexacherubimSpawnerHandleEventTranslationPatch.cs" in hexacherubim_evidence
    assert "delegated BestowElement RulesDescription text" in hexacherubim_evidence


def test_policy_records_sultan_shrine_wrapper_routes_without_exact_cognomen_leaves() -> None:
    """Sultan shrine wrapper routes are covered while generated names remain dynamic."""
    family_id = "XRL.World.Parts/SultanShrine.cs::SultanShrine.ShrineInitialize()"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World.Parts/SultanShrine.cs",
                "ShrineInitialize",
                {"DescriptionAssignment": 1, "DisplayNameAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "GetDisplayNameRouteTranslator.cs" in evidence
    assert "SultanShrineWrapperTranslator.cs" in evidence
    assert "generated sultan names/cognomina remain dynamic fragments" in evidence


def test_policy_records_issue737_hse_runtime_gap_progress_and_journal_route_closure() -> None:
    """Issue-737 journal runtime gaps are covered once storage/display annals routes are proven."""
    zone_manager_family_id = "XRL.World/ZoneManager.cs::ZoneManager.SetActiveZone(Zone)"
    campfire_family_id = "XRL.World.Parts/Campfire.cs::Campfire.CookFromIngredients(bool)"
    journal_family_id = (
        "Qud.API/JournalAPI.cs::JournalAPI.AddAccomplishment("
        "string,string,string,string,string,MuralCategory,MuralWeight,string,long,bool)"
    )
    inventory = _inventory(
        [
            _family(
                zone_manager_family_id,
                "XRL.World/ZoneManager.cs",
                "SetActiveZone",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
            _family(
                campfire_family_id,
                "XRL.World.Parts/Campfire.cs",
                "CookFromIngredients",
                {"HistoricStringExpander": 1},
            ),
            _family(
                journal_family_id,
                "Qud.API/JournalAPI.cs",
                "AddAccomplishment",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[zone_manager_family_id]["closure_lane"] == "history_generated_text"
    assert entries[zone_manager_family_id]["closure_status"] == "covered_by_owner_route"
    zone_manager_evidence = " ".join(entries[zone_manager_family_id]["closure_evidence"])
    assert "SetActiveZone journey AddAccomplishment branches" in zone_manager_evidence
    assert entries[campfire_family_id]["closure_lane"] == "history_generated_text"
    assert entries[campfire_family_id]["closure_status"] == "covered_by_owner_route"
    campfire_evidence = " ".join(entries[campfire_family_id]["closure_evidence"])
    assert "CampfirePreserveTranslationPatchTests.cs" in campfire_evidence
    assert "PopupShowTranslationPatchTests.cs" in campfire_evidence
    assert "^You eat the meal\\.$ popup pattern" in campfire_evidence
    assert "CampfireRollIngredientsTranslationPatchTests.cs" in campfire_evidence
    assert "CampfireDescribeMealTranslationPatchTests.cs" in campfire_evidence
    assert "CampfireCookFromIngredientsTranslationPatchTests.cs" in campfire_evidence
    assert "CookingRecipeDisplayNameTranslationPatchTests.cs" in campfire_evidence
    assert "spice.cooking.terrain.* direct coverage is 290/290" in campfire_evidence
    assert "issue-737-hse-route-audit.md" in campfire_evidence
    assert entries[journal_family_id]["closure_lane"] == "history_generated_text"
    assert entries[journal_family_id]["closure_status"] == "covered_by_owner_route"
    journal_evidence = " ".join(entries[journal_family_id]["closure_evidence"])
    assert "JournalAccomplishmentAddTranslationPatch.cs" in journal_evidence
    assert "JournalApiAddTranslationPatchTests.cs" in journal_evidence
    assert "ReshephHistoryTranslationTests.cs" in journal_evidence
    assert "accepted annals candidates are merged" in journal_evidence
    assert "issue-737-hse-route-audit.md" in journal_evidence


def test_policy_records_hse_dynamic_quest_owner_route_closure() -> None:
    """Dynamic quest HSE families are covered by producer-scoped owner patches and target-resolution tests."""
    constructor_family_id = (
        "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver("
        "GameObject,Quest,GameObject)"
    )
    generated_quest_family_id = (
        "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.fabricateInteractWithAnObjectQuest("
        "GameObject,string)"
    )
    signpost_family_id = (
        "XRL.World.Parts/DynamicQuestSignpostConversation.cs::"
        "DynamicQuestSignpostConversation.HandleEvent(BeforeConversationEvent)"
    )
    inventory = _inventory(
        [
            _family(
                constructor_family_id,
                "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs",
                "addQuestConversationToGiver",
                {"HistoricStringExpander": 1},
            ),
            _family(
                generated_quest_family_id,
                "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs",
                "fabricateInteractWithAnObjectQuest",
                {"HistoricStringExpander": 1},
            ),
            _family(
                signpost_family_id,
                "XRL.World.Parts/DynamicQuestSignpostConversation.cs",
                "HandleEvent",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [constructor_family_id, generated_quest_family_id, signpost_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        assert "TargetMethodResolutionTests.cs" in " ".join(entries[family_id]["closure_evidence"])
        assert "historic-string-expander-owner-plan.md" in " ".join(entries[family_id]["closure_evidence"])


def test_policy_records_hse_journal_accomplishment_owner_route_closure() -> None:
    """Journal accomplishment HSE families with storage-time route coverage should not stay queued."""
    reputation_family_id = (
        "XRL.World/Reputation.cs::Reputation.Modify("
        "Faction,int,string,StringBuilder,string,bool,bool,bool,bool)"
    )
    gives_rep_family_id = (
        "XRL.World.Parts/GivesRep.cs::GivesRep.HandleEvent(BeforeDeathRemovalEvent)"
    )
    inventory = _inventory(
        [
            _family(
                reputation_family_id,
                "XRL.World/Reputation.cs",
                "Modify",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
            _family(
                gives_rep_family_id,
                "XRL.World.Parts/GivesRep.cs",
                "HandleEvent",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [reputation_family_id, gives_rep_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        assert "JournalApiAddTranslationPatchTests.cs" in " ".join(entries[family_id]["closure_evidence"])
        assert "JournalPatternTranslatorTests.cs" in " ".join(entries[family_id]["closure_evidence"])


def test_policy_records_hse_dynamic_quest_completion_route_progress() -> None:
    """Dynamic quest completion accomplishments are owned by JournalAPI storage-time translation."""
    find_site_family_id = (
        "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::"
        "FindASiteDynamicQuestManagerSystem.CheckCompleted(Zone,JournalMapNote)"
    )
    find_item_family_id = (
        "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestManager.cs::"
        "FindASpecificItemDynamicQuestManagerSystem.CheckCompleted(GameObject)"
    )
    locate_relic_family_id = (
        "XRL.World.Parts/LocateRelicQuestManager.cs::"
        "LocateRelicQuestManagerSystem.CheckCompleted(GameObject)"
    )
    interact_family_id = (
        "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestManager.cs::"
        "System.FinishEntry(QuestEntry,GameObject)"
    )
    inventory = _inventory(
        [
            _family(
                find_site_family_id,
                "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs",
                "CheckCompleted",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
            _family(
                find_item_family_id,
                "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestManager.cs",
                "CheckCompleted",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
            _family(
                locate_relic_family_id,
                "XRL.World.Parts/LocateRelicQuestManager.cs",
                "CheckCompleted",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
            _family(
                interact_family_id,
                "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestManager.cs",
                "FinishEntry",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [find_site_family_id, find_item_family_id, locate_relic_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        assert "JournalApiAddTranslationPatchTests.cs" in " ".join(entries[family_id]["closure_evidence"])
        assert "JournalPatternTranslatorTests.cs" in " ".join(entries[family_id]["closure_evidence"])

    assert entries[interact_family_id]["closure_lane"] == "history_generated_text"
    assert entries[interact_family_id]["closure_status"] == "covered_by_owner_route"
    interact_evidence = " ".join(entries[interact_family_id]["closure_evidence"])
    assert "JournalApiAddTranslationPatchTests.cs" in interact_evidence
    assert "finite QuestableVerb tags" in interact_evidence


def test_policy_records_hse_journal_story_completion_routes() -> None:
    """HSE journal story accomplishments are covered by storage patterns and owner popups."""
    opening_family_id = "XRL.World.Parts/OpeningStory.cs::OpeningStory.AddAccomplishment(string)"
    animator_family_id = "XRL.World.Parts/AnimatorSpray.cs::AnimatorSpray.HandleEvent(InventoryActionEvent)"
    body_family_id = (
        "XRL.World.Parts/Body.cs::Body.Dismember(BodyPart,GameObject,IInventory,bool,bool,IEvent)"
    )
    status_family_id = "XRL.UI/StatusScreen.cs::StatusScreen.BuyRandomMutation(GameObject)"
    village_surface_family_id = "XRL.World.Parts/VillageSurface.cs::VillageSurface.CheckReveal()"
    inventory = _inventory(
        [
            _family(
                opening_family_id,
                "XRL.World.Parts/OpeningStory.cs",
                "AddAccomplishment",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
            _family(
                animator_family_id,
                "XRL.World.Parts/AnimatorSpray.cs",
                "HandleEvent",
                {"HistoricStringExpander": 1, "JournalAPI": 1, "Popup": 1},
            ),
            _family(
                body_family_id,
                "XRL.World.Parts/Body.cs",
                "Dismember",
                {"HistoricStringExpander": 1, "JournalAPI": 1, "Popup": 1},
            ),
            _family(
                status_family_id,
                "XRL.UI/StatusScreen.cs",
                "BuyRandomMutation",
                {"HistoricStringExpander": 1, "JournalAPI": 1, "Popup": 1},
            ),
            _family(
                village_surface_family_id,
                "XRL.World.Parts/VillageSurface.cs",
                "CheckReveal",
                {"HistoricStringExpander": 1, "JournalAPI": 1, "Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [
        opening_family_id,
        animator_family_id,
        body_family_id,
        status_family_id,
    ]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        assert "JournalApiAddTranslationPatchTests.cs" in " ".join(entries[family_id]["closure_evidence"])
        assert "JournalPatternTranslatorTests.cs" in " ".join(entries[family_id]["closure_evidence"])

    assert entries[village_surface_family_id]["closure_lane"] == "history_generated_text"
    assert entries[village_surface_family_id]["closure_status"] == "covered_by_owner_route"
    assert "JournalApiAddTranslationPatchTests.cs" in " ".join(
        entries[village_surface_family_id]["closure_evidence"]
    )
    assert "RevealString data" in " ".join(entries[village_surface_family_id]["closure_evidence"])

    assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in " ".join(
        entries[animator_family_id]["closure_evidence"]
    )
    assert "BodyTranslationPatch.cs" in " ".join(entries[body_family_id]["closure_evidence"])
    assert "StatusScreenPopupTranslationPatchTests.cs" in " ".join(
        entries[status_family_id]["closure_evidence"]
    )


def test_policy_records_hse_owner_plan_closure_for_existing_covered_families() -> None:
    """Existing HSE owner-plan families should not remain unreviewed after evidence-backed review."""
    cooking_family_id = (
        "XRL.World.Skills.Cooking/CookingRecipe.cs::"
        "CookingRecipe.GenerateRecipeName(List<string>,List<string>,string)"
    )
    memorial_family_id = "XRL.World.Parts/EaterCryptPlaque.cs::EaterCryptPlaque.GeneratePlaque()"
    relic_family_id = (
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelic("
        "string,int,HistoricEntitySnapshot,List<string>,Dictionary<string,List<string>>,string,string,string)"
    )
    village_family_id = "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.getAVillageWall()"
    dimension_family_id = "XRL.World.Encounters/DimensionManager.cs::DimensionManager.InitializeFaction()"
    name_style_family_id = (
        "XRL.Names/NameStyle.cs::NameStyle.Generate("
        "GameObject,string,string,string,string,string,string,string,List<string>,string,string,string,"
        "Dictionary<string,string>,bool,bool,NameStyle,List<NameStyle>,int?,int?,bool?,bool?)"
    )
    inventory = _inventory(
        [
            _family(
                cooking_family_id,
                "XRL.World.Skills.Cooking/CookingRecipe.cs",
                "GenerateRecipeName",
                {"HistoricStringExpander": 1},
            ),
            _family(
                memorial_family_id,
                "XRL.World.Parts/EaterCryptPlaque.cs",
                "GeneratePlaque",
                {"HistoricStringExpander": 1},
            ),
            _family(
                relic_family_id,
                "XRL.World/RelicGenerator.cs",
                "GenerateRelic",
                {"HistoricStringExpander": 1},
            ),
            _family(
                village_family_id,
                "XRL.World.ZoneBuilders/VillageBase.cs",
                "getAVillageWall",
                {"HistoricStringExpander": 1},
            ),
            _family(
                dimension_family_id,
                "XRL.World.Encounters/DimensionManager.cs",
                "InitializeFaction",
                {"HistoricStringExpander": 1},
            ),
            _family(
                name_style_family_id,
                "XRL.Names/NameStyle.cs",
                "Generate",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [
        cooking_family_id,
        memorial_family_id,
        relic_family_id,
        village_family_id,
        dimension_family_id,
        name_style_family_id,
    ]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        assert "historic-string-expander-owner-plan.md" in " ".join(entries[family_id]["closure_evidence"])


def test_policy_records_hse_friend_or_foe_reason_owner_route_closure() -> None:
    """Friend-or-foe HSE reason frames are covered by a source-owner placeholder patch."""
    normal_family_id = "XRL.World.Parts/GenerateFriendOrFoe.cs::GenerateFriendOrFoe.replacePlaceholders(string)"
    heb_family_id = (
        "XRL.World.Parts/GenerateFriendOrFoe_HEB.cs::"
        "GenerateFriendOrFoe_HEB.replacePlaceholders(string)"
    )
    inventory = _inventory(
        [
            _family(
                normal_family_id,
                "XRL.World.Parts/GenerateFriendOrFoe.cs",
                "replacePlaceholders",
                {"HistoricStringExpander": 1},
            ),
            _family(
                heb_family_id,
                "XRL.World.Parts/GenerateFriendOrFoe_HEB.cs",
                "replacePlaceholders",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [normal_family_id, heb_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "FriendOrFoeReasonTranslationPatch.cs" in evidence
        assert "FriendOrFoeReasonTranslatorTests.cs" in evidence
        assert "FriendOrFoeReasonTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence


def test_policy_records_hse_gossip_observation_owner_route_closure() -> None:
    """Historic gossip HSE prose is covered by JournalAPI observation storage-time translation."""
    one_faction_family_id = "XRL.World.Parts/Gossip.cs::Gossip.GenerateGossip_OneFaction(string)"
    two_faction_family_id = "XRL.World.Parts/Gossip.cs::Gossip.GenerateGossip_TwoFactions(string,string)"
    inventory = _inventory(
        [
            _family(
                one_faction_family_id,
                "XRL.World.Parts/Gossip.cs",
                "GenerateGossip_OneFaction",
                {"HistoricStringExpander": 1},
            ),
            _family(
                two_faction_family_id,
                "XRL.World.Parts/Gossip.cs",
                "GenerateGossip_TwoFactions",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [one_faction_family_id, two_faction_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "JournalObservationAddTranslationPatch.cs" in evidence
        assert "JournalPatternTranslatorTests.cs" in evidence
        assert "JournalApiAddTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence


def test_policy_defers_text_filters_to_runtime_follow_up() -> None:
    """TextFilters HSE calls require owner-specific runtime evidence outside the fixed-prose route pass."""
    angry_family_id = "XRL.Language/TextFilters.cs::TextFilters.Angry(string)"
    lallated_family_id = "XRL.Language/TextFilters.cs::TextFilters.Lallated(string,string)"
    inventory = _inventory(
        [
            _family(
                angry_family_id,
                "XRL.Language/TextFilters.cs",
                "Angry",
                {"HistoricStringExpander": 1},
            ),
            _family(
                lallated_family_id,
                "XRL.Language/TextFilters.cs",
                "Lallated",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [angry_family_id, lallated_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "issues/726" in evidence
        assert "owner-specific runtime evidence" in evidence
        assert "semantic-probe TextFilters" in evidence
        assert "StyledStatus.Format angry style" in evidence
        assert "Preacher.PreacherHomily filters lineText" in evidence
        assert "ConversationScript installs XRL.World.Conversations.Parts.TextFilter" in evidence
        assert "filtered outputs mutate already-composed speech/status text" in evidence


def test_policy_records_hse_sultan_region_reveal_description_owner_route_closure() -> None:
    """SultanRegion reveal descriptions are covered by a successful SultanReveal owner patch."""
    family_id = "XRL.World.Parts/SultanRegion.cs::SultanRegion.FireEvent(Event)"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World.Parts/SultanRegion.cs",
                "FireEvent",
                {"HistoricStringExpander": 1, "Description": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_lane"] == "history_generated_text"
    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "SultanRegionRevealDescriptionTranslationPatch.cs" in evidence
    assert "SultanRegionRevealDescriptionTranslatorTests.cs" in evidence
    assert "SultanRegionRevealDescriptionTranslationPatchTests.cs" in evidence
    assert "TargetMethodResolutionTests.cs" in evidence


def test_policy_records_hse_relic_component_wrapper_closure() -> None:
    """Relic element wrappers are covered by downstream relic name and description owner routes."""
    spindle_family_id = (
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateSpindleNegotiationRelic("
        "string,string,string,string,int)"
    )
    select_element_family_id = (
        "XRL.World/RelicGenerator.cs::RelicGenerator.SelectElement("
        "GameObject,GameObject,GameObject,GameObject)"
    )
    inventory = _inventory(
        [
            _family(
                spindle_family_id,
                "XRL.World/RelicGenerator.cs",
                "GenerateSpindleNegotiationRelic",
                {"HistoricStringExpander": 1},
            ),
            _family(
                select_element_family_id,
                "XRL.World/RelicGenerator.cs",
                "SelectElement",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [spindle_family_id, select_element_family_id]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "RelicDescriptionAddendumTranslationPatchTests.cs" in evidence
        assert "RelicGeneratorGeneratedNameTranslationPatchTests.cs" in evidence
        assert "PseudoRelicGeneratedNameTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence


def test_policy_records_hse_sultanate_year_name_owner_route_closure() -> None:
    """Sultanate year names are covered by a source-owner helper patch."""
    family_id = "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.GenerateSultanateYearName()"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.Annals/QudHistoryHelpers.cs",
                "GenerateSultanateYearName",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_lane"] == "history_generated_text"
    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "SultanateYearNameTranslationPatch.cs" in evidence
    assert "HistoricSpiceGeneratedNameTranslatorTests.cs" in evidence
    assert "SultanateYearNameTranslationPatchTests.cs" in evidence
    assert "TargetMethodResolutionTests.cs" in evidence


def test_policy_records_hse_imported_food_drink_faction_name_owner_route_closure() -> None:
    """Imported food/drink faction names are covered by a source-owner faction-name patch."""
    family_id = "XRL.Annals/ImportedFoodorDrink.cs::ImportedFoodorDrink.generateFactionName(string)"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.Annals/ImportedFoodorDrink.cs",
                "generateFactionName",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_lane"] == "history_generated_text"
    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "ImportedFoodOrDrinkFactionNameTranslationPatch.cs" in evidence
    assert "ImportedFoodOrDrinkFactionNameTranslatorTests.cs" in evidence
    assert "ImportedFoodOrDrinkFactionNameTranslationPatchTests.cs" in evidence
    assert "historyspice-common.ja.json" in evidence


def test_policy_records_hse_history_item_name_owner_route_closure() -> None:
    """QudHistoryHelpers generated blessing item names are covered at the source helper route."""
    family_ids = [
        "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItem(string,History,HistoricEntity)",
        "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItemNounRoot(string,History,HistoricEntity)",
        "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItemAdjRoot(string,History,HistoricEntity)",
    ]
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.Annals/QudHistoryHelpers.cs",
                family_id.split("::QudHistoryHelpers.")[1].split("(", maxsplit=1)[0],
                {"HistoricStringExpander": 1},
            )
            for family_id in family_ids
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "QudHistoryHelpersItemNameTranslationPatch.cs" in evidence
        assert "HistoricSpiceGeneratedNameTranslatorTests.cs" in evidence
        assert "QudHistoryHelpersItemNameTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence
        assert "world-gospels.ja.json" in evidence


def test_policy_records_hse_village_proverb_storage_route_closure() -> None:
    """VillageProverb text is covered by the village gospel storage route and proverb patterns."""
    family_id = "XRL.Annals/VillageProverb.cs::VillageProverb.Generate()"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.Annals/VillageProverb.cs",
                "Generate",
                {"HistoricStringExpander": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_lane"] == "history_generated_text"
    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "AddVillageGospelsTranslationPatch.cs" in evidence
    assert "HistoricNarrativeDictionaryWalker.cs" in evidence
    assert "JournalPatternTranslatorTests.cs" in evidence
    assert "annals-patterns.ja.json" in evidence


def test_policy_records_hse_village_coda_end_event_display_route_closure() -> None:
    """VillageCoda end-event prose is covered by JournalSultanNote display-route annals patterns."""
    family_id = "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateEndEvent()"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World.ZoneBuilders/VillageCoda.cs",
                "GenerateEndEvent",
                {"HistoricStringExpander": 1, "JournalAPI": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_lane"] == "history_generated_text"
    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "JournalEntryDisplayTextPatch.cs" in evidence
    assert "JournalTextTranslator.cs" in evidence
    assert "JournalEntryDisplayTextPatchTests.cs" in evidence
    assert "annals-patterns.ja.json" in evidence
    assert "candidates_pending.json" in evidence


def test_policy_records_hse_village_buildzone_pet_origin_owner_route_closure() -> None:
    """Village BuildZone HSE pet origin stories are covered by the pet conversation owner route."""
    family_ids = [
        "XRL.World.ZoneBuilders/Village.cs::Village.BuildZone(Zone)",
        "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.BuildZone(Zone)",
    ]
    inventory = _inventory(
        [
            _family(
                family_id,
                family_id.split("::", maxsplit=1)[0],
                "BuildZone",
                {"HistoricStringExpander": 1, "OtherInvocation": 1},
            )
            for family_id in family_ids
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "VillagePetConversationTranslationPatch.cs" in evidence
        assert "VillagePetConversationTranslatorTests.cs" in evidence
        assert "VillagePetConversationTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence
        assert "AddVillagerConversation" in evidence


def test_policy_records_hse_qud_history_factory_generated_name_route_closure() -> None:
    """QudHistoryFactory generated site and cult names are covered by narrow owner patches."""
    family_ids = [
        "XRL.Annals/QudHistoryFactory.cs::QudHistoryFactory.NameRuinsSite(History,out bool,out string)",
        "XRL.Annals/QudHistoryFactory.cs::QudHistoryFactory.GenerateCultName(HistoricEntity,History)",
    ]
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.Annals/QudHistoryFactory.cs",
                family_id.split("::QudHistoryFactory.")[1].split("(", maxsplit=1)[0],
                {"HistoricStringExpander": 1},
            )
            for family_id in family_ids
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "QudHistoryFactoryNameRuinsSiteTranslationPatch.cs" in evidence
        assert "QudHistoryFactoryGenerateCultNameTranslationPatch.cs" in evidence
        assert "HistoricSpiceGeneratedNameTranslatorTests.cs" in evidence
        assert "QudHistoryFactoryGeneratedNameTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence


def test_lane_summary_payload_reports_counts_and_top_families() -> None:
    """Lane output must summarize counts and representative high-risk families."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging()",
                "XRL.World.Parts/BandageMedication.cs",
                "PerformBandaging",
                {"MessageFrame": 1},
            ),
            _family(
                "XRL.World.Parts/Combat.cs::Combat.Attack()",
                "XRL.World.Parts/Combat.cs",
                "Attack",
                {"MessageFrame": 5},
            ),
            _family("Internal.cs::Internal.Fields", "Internal.cs", "Fields", {"Attribute": 5}),
        ]
    )

    payload = lane_summary_payload(inventory, inventory_path=Path("inventory.json"), top_per_lane=1)

    lane = payload["lanes"]["combat_message_frame_does"]
    assert lane["entry_count"] == 2
    assert lane["text_construction_count"] == 6
    assert lane["closure_status_counts"] == {"unreviewed": 2}
    assert [entry["source_file"] for entry in lane["top_entries"]] == ["XRL.World.Parts/Combat.cs"]
    assert "non_target" not in payload["lane_counts"]


def test_queue_payload_defaults_to_valuable_surfaces_only() -> None:
    """The handoff queue must not mix valuable localization surfaces with generic text noise."""
    inventory = _inventory(
        [
            _family("Qud.UI/TradeLine.cs::TradeLine.setData(object)", "Qud.UI/TradeLine.cs", "setData", {"SetText": 2}),
            _family("Internal.cs::Internal.Config", "Internal.cs", "Config", {"Initializer": 3}),
        ]
    )

    payload = queue_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["counts"] == {"player_visible_owner_candidate": 1}
    assert payload["lane_counts"] == {"screen_ui_direct_text": 1}
    assert [entry["source_file"] for entry in payload["entries"]] == ["Qud.UI/TradeLine.cs"]


def test_queue_payload_needs_work_excludes_unreviewed_families() -> None:
    """Known work must be separable from unreviewed static findings."""
    partial_family_id = "XRL.Core/XRLCore.cs::XRLCore.PlayerTurn()"
    inventory = _inventory(
        [
            _family(
                partial_family_id,
                "XRL.Core/XRLCore.cs",
                "PlayerTurn",
                {"Popup": 1, "MessageFrame": 1},
            ),
            _family(
                "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging()",
                "XRL.World.Parts/BandageMedication.cs",
                "PerformBandaging",
                {"MessageFrame": 1},
            ),
        ]
    )

    payload = queue_payload(inventory, inventory_path=Path("inventory.json"), include="needs-work")

    assert [entry["family_id"] for entry in payload["entries"]] == [partial_family_id]
    assert payload["entries"][0]["closure_status"] == "partial_coverage"


def test_text_summary_names_reason_and_action_for_agent_handoff() -> None:
    """Text output must explain why a surface is worth translating."""
    inventory = _inventory(
        [
            _family(
                "Qud.UI/InventoryLine.cs::InventoryLine.setData(object)",
                "Qud.UI/InventoryLine.cs",
                "setData",
                {"SetText": 1},
            ),
        ]
    )

    summary = format_surface_queue(inventory, inventory_path=Path("inventory.json"))

    assert "text construction surface queue:" in summary
    assert "[player_visible_owner_candidate/screen_ui_direct_text]" in summary
    assert "closure lanes:" in summary
    assert "Qud.UI/InventoryLine.cs" in summary
    assert "reason:" in summary
    assert "action:" in summary


def test_classify_family_keeps_generic_string_construction_as_candidate_only() -> None:
    """StringBuilder/StringFormat alone is not a localization surface."""
    result = classify_family(
        _family("Demo.cs::Builder.Build()", "Demo.cs", "Build", {"StringBuilderAppend": 1, "StringFormat": 1})
    )

    assert result["classification"] == "candidate_only"
    assert result["construction_only_surfaces"] == ["StringBuilderAppend", "StringFormat"]


def test_load_inventory_normalizes_static_producer_inventory_schema(tmp_path: Path) -> None:
    """The CLI accepts the scanner's producer_family_id/callsite_count payload."""
    inventory_path = tmp_path / "static-producer-inventory.json"
    inventory_path.write_text(
        """
{
  "schema_version": "1.0",
  "game_version": "1.0.4",
  "totals": {},
  "families": [
    {
      "producer_family_id": "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookPresetMeal",
      "file": "XRL.World.Parts/Campfire.cs",
      "namespace": "XRL.World.Parts",
      "type_name": "XRL.World.Parts.Campfire",
      "member_name": "CookPresetMeal",
      "member_kind": "method",
      "member_start_line": 734,
      "callsite_count": 3,
      "surface_counts": {"HistoricStringExpander": 1, "Popup.Show*": 2},
      "representative_calls": [
        {"line": 738, "target_surface": "HistoricStringExpander"}
      ]
    }
  ]
}
""",
        encoding="utf-8",
    )

    inventory = load_inventory(inventory_path)
    entry = build_surface_queue(inventory)[0]

    assert entry["family_id"] == "XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookPresetMeal"
    assert entry["member_signature"] == "CookPresetMeal"
    assert entry["text_construction_count"] == 3
    assert entry["first_lines"] == [738]


def test_load_inventory_normalizes_family_payload_even_when_family_id_exists(tmp_path: Path) -> None:
    """Mixed-schema payloads with family_id still receive computed compatibility fields."""
    inventory_path = tmp_path / "static-producer-inventory.json"
    inventory_path.write_text(
        """
{
  "schema_version": "1.0",
  "game_version": "1.0.4",
  "totals": {},
      "families": [
    {
      "family_id": "XRL.World.Parts/Campfire.cs::Campfire.CookPresetMeal(int)",
      "file": "XRL.World.Parts/Campfire.cs",
      "type_name": "XRL.World.Parts.Campfire",
      "member_name": "CookPresetMeal",
      "member_start_line": 734,
      "surface_counts": {"HistoricStringExpander": 1, "Popup.Show*": 2},
      "representative_calls": [
        {"line": 738, "target_surface": "HistoricStringExpander"}
      ]
    }
  ]
}
""",
        encoding="utf-8",
    )

    inventory = load_inventory(inventory_path)
    entry = build_surface_queue(inventory)[0]

    assert entry["family_id"] == "XRL.World.Parts/Campfire.cs::Campfire.CookPresetMeal(int)"
    assert entry["member_signature"] == "CookPresetMeal"
    assert entry["text_construction_count"] == 3
    assert entry["first_lines"] == [738]


def test_load_inventory_preserves_existing_text_construction_count(tmp_path: Path) -> None:
    """Mixed-schema payloads keep authoritative precomputed counts when present."""
    inventory_path = tmp_path / "static-producer-inventory.json"
    inventory_path.write_text(
        """
{
  "schema_version": "1.0",
  "game_version": "1.0.4",
  "totals": {},
  "families": [
    {
      "family_id": "XRL.World.Parts/Campfire.cs::Campfire.CookPresetMeal(int)",
      "file": "XRL.World.Parts/Campfire.cs",
      "type_name": "XRL.World.Parts.Campfire",
      "member_name": "CookPresetMeal",
      "member_start_line": 734,
      "text_construction_count": 7,
      "surface_counts": {"HistoricStringExpander": 1},
      "representative_calls": [
        {"line": 738, "target_surface": "HistoricStringExpander"}
      ]
    }
  ]
}
""",
        encoding="utf-8",
    )

    inventory = load_inventory(inventory_path)
    entry = build_surface_queue(inventory)[0]

    assert entry["text_construction_count"] == 7


def test_load_inventory_backfills_empty_first_lines_from_representative_calls(tmp_path: Path) -> None:
    """Mixed-schema payloads with first_lines: [] still keep representative callsite evidence."""
    inventory_path = tmp_path / "static-producer-inventory.json"
    inventory_path.write_text(
        """
{
  "schema_version": "1.0",
  "game_version": "1.0.4",
  "totals": {},
  "families": [
    {
      "family_id": "XRL.World.Parts/Campfire.cs::Campfire.CookPresetMeal(int)",
      "file": "XRL.World.Parts/Campfire.cs",
      "type_name": "XRL.World.Parts.Campfire",
      "member_name": "CookPresetMeal",
      "member_start_line": 734,
      "surface_counts": {"HistoricStringExpander": 1, "Popup.Show*": 2},
      "first_lines": [],
      "representative_calls": [
        {"line": 738, "target_surface": "HistoricStringExpander"}
      ]
    }
  ]
}
""",
        encoding="utf-8",
    )

    inventory = load_inventory(inventory_path)
    entry = build_surface_queue(inventory)[0]

    assert entry["first_lines"] == [738]


def _inventory(families: list[TextConstructionFamily]) -> TextConstructionInventory:
    return {
        "schema_version": "1.0",
        "game_version": "1.0.4",
        "totals": {},
        "families": families,
    }


def _family(
    family_id: str,
    file_path: str,
    member_name: str,
    surface_counts: dict[str, int],
) -> TextConstructionFamily:
    return {
        "family_id": family_id,
        "file": file_path,
        "namespace": None,
        "type_name": family_id.split("::", maxsplit=1)[1].split(".", maxsplit=1)[0],
        "member_name": member_name,
        "member_signature": f"{member_name}()",
        "member_kind": "method",
        "member_start_line": 10,
        "text_construction_count": sum(surface_counts.values()),
        "shape_counts": {"static_literal": sum(surface_counts.values())},
        "context_counts": {"invocation_argument": sum(surface_counts.values())},
        "surface_counts": surface_counts,
        "first_lines": [10],
    }
