from __future__ import annotations

from pathlib import Path

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
    assert entries[signpost_family_id]["closure_status"] == "partial_coverage"
    assert entries[glotrot_family_id]["closure_status"] == "runtime_required"


def test_policy_applies_reviewed_closure_overlay_for_high_risk_combat_lane() -> None:
    """High-risk text-construction lanes can carry reviewed owner-route closure evidence."""
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
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    covered = entries[
        "XRL.World.Parts/Combat.cs::Combat.MeleeAttackWithWeaponInternal(GameObject,GameObject,GameObject,BodyPart,string,int,int,int,int,int,bool,bool)"
    ]
    action_required = entries["XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging()"]

    assert covered["closure_lane"] == "combat_message_frame_does"
    assert covered["closure_status"] == "covered_by_owner_route"
    assert "CombatAndLogMessageQueuePatchTests.cs" in " ".join(covered["closure_evidence"])
    assert action_required["closure_status"] == "action_required"


def test_policy_separates_reviewed_issue711_work_without_overclaiming_closure() -> None:
    """Reviewed issue-711 families distinguish closed, partial, runtime, and likely-gap work."""
    missile_hit_family_id = (
        "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.MissileHit("
        "GameObject,GameObject,GameObject,GameObject,Projectile,GameObject,GameObject,"
        "MissilePath,Cell,FireType,int,int,int,bool,GameObject,bool,ref bool,ref bool,ref bool,bool,bool)"
    )
    inventory_family_id = "XRL.World.Parts/Inventory.cs::Inventory.FireEvent(Event)"
    tombstone_family_id = "XRL.World.Parts/Tombstone.cs::Tombstone.GenerateTombstone()"
    mod_gigantic_family_id = "XRL.World.Parts/ModGigantic.cs::ModGigantic.GetDescription(int,GameObject)"

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
                mod_gigantic_family_id,
                "XRL.World.Parts/ModGigantic.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[missile_hit_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[inventory_family_id]["closure_status"] == "partial_coverage"
    assert entries[tombstone_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[mod_gigantic_family_id]["closure_status"] == "likely_true_gap"


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
        village_surface_family_id,
    ]:
        assert entries[family_id]["closure_lane"] == "history_generated_text"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        assert "JournalApiAddTranslationPatchTests.cs" in " ".join(entries[family_id]["closure_evidence"])
        assert "JournalPatternTranslatorTests.cs" in " ".join(entries[family_id]["closure_evidence"])

    assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in " ".join(
        entries[animator_family_id]["closure_evidence"]
    )
    assert "BodyTranslationPatch.cs" in " ".join(entries[body_family_id]["closure_evidence"])
    assert "StatusScreenPopupTranslationPatchTests.cs" in " ".join(
        entries[status_family_id]["closure_evidence"]
    )


def test_policy_records_hse_owner_plan_closure_for_existing_covered_families() -> None:
    """Existing HSE owner-plan families should not remain action_required after evidence-backed review."""
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
    assert lane["closure_status_counts"] == {"action_required": 2}
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
