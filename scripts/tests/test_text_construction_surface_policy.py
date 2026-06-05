from __future__ import annotations

from pathlib import Path
from typing import Any

from scripts.text_construction_surface_policy import (
    ISSUE719_FOLLOWUP_BY_BUCKET,
    TextConstructionFamily,
    TextConstructionInventory,
    build_surface_queue,
    classify_family,
    followup_issue_payload,
    format_surface_queue,
    lane_summary_payload,
    load_inventory,
    queue_payload,
    residual_bucket_payload,
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
        classifications["XRL.World.Capabilities/Wishing.cs::Wishing.HandleWish(GameObject,string)"] == "candidate_only"
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
    assert lanes["Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.setData(object)"] == "screen_ui_direct_text"
    assert (
        lanes["XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.BestowElement(string)"] == "display_name_composition"
    )
    assert (
        lanes["XRL.World.Conversations.Parts/Trade.cs::Trade.HandleEvent(GetChoiceTagEvent)"] == "conversation_routes"
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
    assert entries[glotrot_family_id]["closure_status"] == "covered_by_owner_route"
    assert "N/G + n* + period gibberish" in " ".join(entries[glotrot_family_id]["closure_evidence"])


def test_policy_promotes_single_callsite_popup_exact_owner_tranche_without_pull_down_overclaim() -> None:
    """Exact SingleCallsiteOwnerPopup owners can close without claiming lookalike popup callers."""
    xrl_game_load = "XRL/XRLGame.cs::XRLGame.LoadGame(string,bool,bool,Dictionary<string,object>)"
    food_handle_event = "XRL.World.Parts/Food.cs::Food.HandleEvent(InventoryActionEvent)"
    container_attempt_open = "XRL.World.Parts/Container.cs::Container.AttemptOpen(GameObject,IEvent)"
    population_wish_generate = "XRL/PopulationManager.cs::PopulationManager.WishGenerate(string)"
    game_object_pull_down = "XRL.World/GameObject.cs::GameObject.PullDown(bool)"
    inventory = _inventory(
        [
            _family(xrl_game_load, "XRL/XRLGame.cs", "LoadGame", {"Popup": 50}),
            _family(food_handle_event, "XRL.World.Parts/Food.cs", "HandleEvent", {"Popup": 32}),
            _family(container_attempt_open, "XRL.World.Parts/Container.cs", "AttemptOpen", {"Popup": 16}),
            _family(population_wish_generate, "XRL/PopulationManager.cs", "WishGenerate", {"Popup": 13}),
            _family(game_object_pull_down, "XRL.World/GameObject.cs", "PullDown", {"Popup": 15}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [
        xrl_game_load,
        food_handle_event,
        container_attempt_open,
        population_wish_generate,
    ]:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "SingleCallsiteOwnerPopupTranslationPatch.cs",
            "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "ui-popup.ja.json",
        )

    evidence = " ".join(entries[xrl_game_load]["closure_evidence"])
    assert "XRL.XRLGame|LoadGame" in evidence
    assert "XRL.World.Parts.Food|HandleEvent" in " ".join(entries[food_handle_event]["closure_evidence"])
    assert "XRL.World.Parts.Container|AttemptOpen" in " ".join(entries[container_attempt_open]["closure_evidence"])
    assert "XRL.PopulationManager|WishGenerate" in " ".join(entries[population_wish_generate]["closure_evidence"])
    assert entries[game_object_pull_down]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        game_object_pull_down,
        "GameObjectPopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "Select a destination",
    )


def test_policy_promotes_existing_message_frame_dictionary_tranche() -> None:
    """Reviewed fixed MessageFrame producers can close through the repository frame dictionary."""
    family_ids = [
        "XRL.World.Parts/GeomagneticDisc.cs::"
        "GeomagneticDisc.DoThrow(GameObject,List<FindPath>,bool,bool,List<GameObject>,GameObject,int,int?,"
        "IThrownWeaponFlexPhaseProvider,IEvent)",
        "XRL.World.Parts/Leveler.cs::Leveler.LevelUp(GameObject,GameObject,string,IEvent)",
        "XRL.World.Parts/CryptFerretBehavior.cs::CryptFerretBehavior.FireEvent(Event)",
        "XRL.World.Parts/CyberneticsHighFidelityMatterRecompositer.cs::"
        "CyberneticsHighFidelityMatterRecompositer.HandleEvent(CommandEvent)",
        "XRL.World.AI.GoalHandlers/PlaceTurretGoal.cs::PlaceTurretGoal.TakeAction()",
        "XRL.World.Parts/CyberneticsMatterRecompositer.cs::"
        "CyberneticsMatterRecompositer.HandleEvent(CommandEvent)",
        "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.FireEvent(Event)",
    ]
    inventory = _inventory(
        [
            _family(family_ids[0], "XRL.World.Parts/GeomagneticDisc.cs", "DoThrow", {"MessageFrame": 32}),
            _family(family_ids[1], "XRL.World.Parts/Leveler.cs", "LevelUp", {"MessageFrame": 13}),
            _family(family_ids[2], "XRL.World.Parts/CryptFerretBehavior.cs", "FireEvent", {"MessageFrame": 13}),
            _family(
                family_ids[3],
                "XRL.World.Parts/CyberneticsHighFidelityMatterRecompositer.cs",
                "HandleEvent",
                {"MessageFrame": 10},
            ),
            _family(family_ids[4], "XRL.World.AI.GoalHandlers/PlaceTurretGoal.cs", "TakeAction", {"MessageFrame": 9}),
            _family(
                family_ids[5],
                "XRL.World.Parts/CyberneticsMatterRecompositer.cs",
                "HandleEvent",
                {"MessageFrame": 8},
            ),
            _family(family_ids[6], "XRL.World.Parts.Mutation/GasGeneration.cs", "FireEvent", {"MessageFrame": 8}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
        )


def test_policy_promotes_reviewed_combat_message_frame_dictionary_tranche() -> None:
    """Reviewed combat MessageFrame producers can close through new repository dictionary leaves."""
    family_ids = [
        "XRL.World.Parts/PointDefense.cs::PointDefense.HandleEvent(ProjectileMovingEvent)",
        "XRL.World.Parts/GreaterVoider.cs::GreaterVoider.FireEvent(Event)",
        "XRL.World.Parts/RunOver.cs::RunOver.PerformCharge(List<Cell>,bool)",
        "XRL.World.Parts/AjiConch.cs::AjiConch.ActivateAjiConch()",
        "XRL.World.Capabilities/Disarming.cs::"
        "Disarming.Disarm(GameObject,GameObject,int,string,string,GameObject,GameObject)",
        "XRL.World.Parts/EngulfingClones.cs::EngulfingClones.FireEvent(Event)",
        "XRL.World.Parts/Fan.cs::Fan.TurnTick(long,int)",
        "XRL.World.Parts/HookOnMissileHit.cs::HookOnMissileHit.FireEvent(Event)",
    ]
    inventory = _inventory(
        [
            _family(family_ids[0], "XRL.World.Parts/PointDefense.cs", "HandleEvent", {"MessageFrame": 16}),
            _family(family_ids[1], "XRL.World.Parts/GreaterVoider.cs", "FireEvent", {"MessageFrame": 16}),
            _family(family_ids[2], "XRL.World.Parts/RunOver.cs", "PerformCharge", {"MessageFrame": 13}),
            _family(family_ids[3], "XRL.World.Parts/AjiConch.cs", "ActivateAjiConch", {"MessageFrame": 12}),
            _family(family_ids[4], "XRL.World.Capabilities/Disarming.cs", "Disarm", {"MessageFrame": 12}),
            _family(family_ids[5], "XRL.World.Parts/EngulfingClones.cs", "FireEvent", {"MessageFrame": 12}),
            _family(family_ids[6], "XRL.World.Parts/Fan.cs", "TurnTick", {"MessageFrame": 12}),
            _family(family_ids[7], "XRL.World.Parts/HookOnMissileHit.cs", "FireEvent", {"MessageFrame": 12}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
        )


def test_policy_promotes_reviewed_physical_message_frame_dictionary_tranche() -> None:
    """Reviewed physical MessageFrame producers can close through repository dictionary leaves."""
    family_ids = [
        "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Blast(MentalAttackEvent)",
        (
            "XRL.World.Parts/Physics.cs::"
            "Physics.AccelerateInternal(int,string,Cell,Cell,string,GameObject,bool,GameObject,string,double,"
            "bool,bool,bool,bool,bool,bool,bool)"
        ),
        (
            "XRL.World.Parts/Butcherable.cs::"
            "Butcherable.AttemptButcher(GameObject,bool,bool,bool,string,Cell,List<GameObject>)"
        ),
        "XRL.World.Parts/PluckablePolyp.cs::PluckablePolyp.Pluck(GameObject)",
        "XRL.World.Parts/Interior.cs::Interior.ShowMessage(GameObject,int)",
        "XRL.World.Parts/CyberneticsStasisProjector.cs::CyberneticsStasisProjector.HandleEvent(CommandEvent)",
        "XRL.World.Parts.Mutation/TimeDilation.cs::TimeDilation.HandleEvent(CommandEvent)",
        "XRL.World.Parts/SwapOnHit.cs::SwapOnHit.SwapPositions(GameObject,Cell,GameObject,Cell,Event,bool)",
    ]
    inventory = _inventory(
        [
            _family(family_ids[0], "XRL.World.Parts.Mutation/SunderMind.cs", "Blast", {"MessageFrame": 24}),
            _family(family_ids[1], "XRL.World.Parts/Physics.cs", "AccelerateInternal", {"MessageFrame": 22}),
            _family(family_ids[2], "XRL.World.Parts/Butcherable.cs", "AttemptButcher", {"MessageFrame": 22}),
            _family(family_ids[3], "XRL.World.Parts/PluckablePolyp.cs", "Pluck", {"MessageFrame": 12}),
            _family(family_ids[4], "XRL.World.Parts/Interior.cs", "ShowMessage", {"MessageFrame": 10}),
            _family(
                family_ids[5],
                "XRL.World.Parts/CyberneticsStasisProjector.cs",
                "HandleEvent",
                {"MessageFrame": 10},
            ),
            _family(family_ids[6], "XRL.World.Parts.Mutation/TimeDilation.cs", "HandleEvent", {"MessageFrame": 9}),
            _family(family_ids[7], "XRL.World.Parts/SwapOnHit.cs", "SwapPositions", {"MessageFrame": 8}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
        )


def test_policy_promotes_fixed_popup_dictionary_producer_tranche() -> None:
    """Fixed popup producers with shipped dictionary coverage should not remain queued."""
    endgame_pick_state = "XRL.World.Conversations.Parts/EndGame.cs::EndGame.PickState()"
    pronoun_pick = (
        "XRL/PronounAndGenderSets.cs::"
        "PronounAndGenderSets.ShowPickGenderAndPronounSet(GameObject,string)"
    )
    checkpoint_death = "XRL/CheckpointingSystem.cs::CheckpointingSystem.ShowDeathMessage(string,string)"
    pronoun_change = "XRL/PronounAndGenderSets.cs::PronounAndGenderSets.ShowChangePronounSet(GameObject)"
    game_object_auto_equip = "XRL.World/GameObject.cs::GameObject.AutoEquip(GameObject,bool,bool,bool)"
    inventory = _inventory(
        [
            _family(endgame_pick_state, "XRL.World.Conversations.Parts/EndGame.cs", "PickState", {"Popup": 43}),
            _family(
                pronoun_pick,
                "XRL/PronounAndGenderSets.cs",
                "ShowPickGenderAndPronounSet",
                {"Popup": 39},
            ),
            _family(checkpoint_death, "XRL/CheckpointingSystem.cs", "ShowDeathMessage", {"Popup": 31}),
            _family(pronoun_change, "XRL/PronounAndGenderSets.cs", "ShowChangePronounSet", {"Popup": 23}),
            _family(game_object_auto_equip, "XRL.World/GameObject.cs", "AutoEquip", {"Popup": 72}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in [endgame_pick_state, pronoun_pick, checkpoint_death, pronoun_change]:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "PopupTranslationPatch.cs",
            "PopupPickOptionTranslationPatch.cs",
            "PopupTranslationPatchTests.cs",
            "PopupPickOptionTranslationPatchTests.cs",
            "ui-popup.ja.json",
        )

    assert "PopupShowSpaceTranslationPatchTests.cs" in " ".join(entries[checkpoint_death]["closure_evidence"])
    assert entries[game_object_auto_equip]["closure_status"] == "covered_by_owner_route"
    auto_equip_evidence = " ".join(entries[game_object_auto_equip]["closure_evidence"])
    assert "AutoEquip fixed ammunition ShowFail branch" in auto_equip_evidence
    assert "PopupShowTranslationPatchTests.cs" in auto_equip_evidence
    assert "ui-popup.ja.json" in auto_equip_evidence


def test_policy_applies_reviewed_closure_overlay_for_high_risk_combat_lane() -> None:
    """High-risk text-construction lanes can carry reviewed owner-route closure evidence."""
    shield_slam_family_id = "XRL.World.Parts.Skill/Shield_Slam.cs::Shield_Slam.Slam(GameObject,GameObject,Cell,bool)"
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
    assert unreviewed["closure_status"] == "action_required"
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
    assert entries[unreviewed_journal_family_id]["closure_status"] == "runtime_required"
    assert entries[tactics_charge_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[tactics_death_from_above_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[physic_amputate_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[tinkering_mine_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[unreviewed_skill_family_id]["closure_status"] == "action_required"
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
        "XRL.World/RandomAltarBaetylRewardManager.cs::RandomAltarBaetylRewardManager.HandleRewardNode(XmlDataHelper)"
    )
    turret_family_id = "XRL.World.Parts/TurretTinker.cs::TurretTinker.FireEvent(Event)"
    bandage_family_id = (
        "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging(GameObject,GameObject)"
    )
    tonic_family_id = "XRL.World.Parts/Tonic.cs::Tonic.HandleEvent(InventoryActionEvent)"
    multihorns_family_id = "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.PerformCharge(List<Cell>,bool)"
    multihorns_mutate_family_id = "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.Mutate(GameObject,int)"
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


def test_policy_promotes_unused_base_game_sifrah_by_static_evidence() -> None:
    """Unused base-game Sifrah classes are statically closed without runtime evidence."""
    psychic_family_id = (
        "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.PsychicCombatSifrah(GameObject,string,int,int,string)"
    )
    beguiling_family_id = (
        "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.BeguilingSifrah(GameObject,int,bool,int,int)"
    )
    inventory = _inventory(
        [
            _family(
                psychic_family_id,
                "XRL.World/PsychicCombatSifrah.cs",
                "PsychicCombatSifrah",
                {"Popup": 1, "Initializer": 6},
            ),
            _family(
                beguiling_family_id,
                "XRL.World/BeguilingSifrah.cs",
                "BeguilingSifrah",
                {"Popup": 1, "DescriptionAssignment": 12},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(inventory, inventory_path=Path("unused-sifrah-test.json"))

    assert residual["entries"] == []
    assert entries[psychic_family_id]["closure_status"] == "covered_by_owner_route"
    psychic_evidence = " ".join(entries[psychic_family_id]["closure_evidence"])
    assert "not used in the base game" in psychic_evidence
    assert "static unused-base-game classification" in psychic_evidence
    assert "SifrahPureOwnerPopupTranslationPatchTests.cs" in psychic_evidence
    assert entries[beguiling_family_id]["closure_status"] == "covered_by_owner_route"
    beguiling_evidence = " ".join(entries[beguiling_family_id]["closure_evidence"])
    assert "not used in the base game" in beguiling_evidence
    assert "static unused-base-game classification" in beguiling_evidence


def test_policy_promotes_sifrah_constructor_popup_owners_without_beguiling_overclaim() -> None:
    """Sifrah constructor popup families close only when the pure-owner patch targets the constructor."""
    constructor_families = {
        "baetyl": (
            "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.BaetylOfferingSifrah(GameObject,int,int)",
            "XRL.World/BaetylOfferingSifrah.cs",
            "BaetylOfferingSifrah",
        ),
        "formal_water_ritual": (
            "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.FormalWaterRitualSifrah(GameObject)",
            "XRL.World/FormalWaterRitualSifrah.cs",
            "FormalWaterRitualSifrah",
        ),
        "haggling": (
            "XRL.World/HagglingSifrah.cs::HagglingSifrah.HagglingSifrah(GameObject)",
            "XRL.World/HagglingSifrah.cs",
            "HagglingSifrah",
        ),
        "disarming": (
            "XRL.World/DisarmingSifrah.cs::DisarmingSifrah.DisarmingSifrah(GameObject,int,int,bool)",
            "XRL.World/DisarmingSifrah.cs",
            "DisarmingSifrah",
        ),
        "examine": (
            "XRL.World/ExamineSifrah.cs::ExamineSifrah.ExamineSifrah(GameObject,int,int,int,int)",
            "XRL.World/ExamineSifrah.cs",
            "ExamineSifrah",
        ),
        "hacking": (
            "XRL.World/HackingSifrah.cs::HackingSifrah.HackingSifrah(GameObject,int,int,int)",
            "XRL.World/HackingSifrah.cs",
            "HackingSifrah",
        ),
        "proselytization": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ProselytizationSifrah(GameObject,int,int)",
            "XRL.World/ProselytizationSifrah.cs",
            "ProselytizationSifrah",
        ),
        "rebuking": (
            "XRL.World/RebukingSifrah.cs::RebukingSifrah.RebukingSifrah(GameObject,int,int)",
            "XRL.World/RebukingSifrah.cs",
            "RebukingSifrah",
        ),
        "item_modding": (
            "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ItemModdingSifrah(GameObject,int,int,int)",
            "XRL.World/ItemModdingSifrah.cs",
            "ItemModdingSifrah",
        ),
        "item_naming": (
            "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ItemNamingSifrah(GameObject,int,int)",
            "XRL.World/ItemNamingSifrah.cs",
            "ItemNamingSifrah",
        ),
        "repair": (
            "XRL.World/RepairSifrah.cs::RepairSifrah.RepairSifrah(GameObject,int,int,int)",
            "XRL.World/RepairSifrah.cs",
            "RepairSifrah",
        ),
        "reverse_engineering": (
            "XRL.World/ReverseEngineeringSifrah.cs::ReverseEngineeringSifrah.ReverseEngineeringSifrah(GameObject,int,int,int,TinkerData)",
            "XRL.World/ReverseEngineeringSifrah.cs",
            "ReverseEngineeringSifrah",
        ),
    }
    beguiling_constructor = (
        "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.BeguilingSifrah(GameObject,int,bool,int,int)"
    )
    psychic_constructor = (
        "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.PsychicCombatSifrah(GameObject,string,int,int,string)"
    )
    inventory = _inventory(
        [
            *[
                _family(family_id, source_file, method_name, {"Popup": 1})
                for family_id, source_file, method_name in constructor_families.values()
            ],
            _family(beguiling_constructor, "XRL.World/BeguilingSifrah.cs", "BeguilingSifrah", {"Popup": 1}),
            _family(
                psychic_constructor,
                "XRL.World/PsychicCombatSifrah.cs",
                "PsychicCombatSifrah",
                {"Popup": 1, "Initializer": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _ in constructor_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "SifrahPureOwnerPopupTranslationPatch.cs",
            "SifrahPureOwnerPopupTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "ui-popup.ja.json",
        )
    beguiling_evidence = " ".join(entries[beguiling_constructor]["closure_evidence"])
    assert "SifrahPureOwnerPopupTranslationPatch" not in beguiling_evidence
    assert "static unused-base-game classification" in beguiling_evidence
    assert entries[beguiling_constructor]["closure_status"] == "covered_by_owner_route"
    assert entries[psychic_constructor]["closure_status"] == "covered_by_owner_route"


def test_policy_splits_residual_sifrah_descriptions_by_static_owner_shape() -> None:
    """Remaining Sifrah descriptions are not one homogeneous runtime bucket."""
    unused_constructor = (
        "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.BeguilingSifrah(GameObject,int,bool,int,int)"
    )
    dynamic_constructor = (
        "XRL.World/TinkeringSifrahTokenBit.cs::TinkeringSifrahTokenBit.TinkeringSifrahTokenBit(BitType)"
    )
    getdescription_return = (
        "XRL.World/TinkeringSifrahTokenBit.cs::TinkeringSifrahTokenBit.GetDescription(SifrahGame,SifrahSlot,GameObject)"
    )
    inventory = _inventory(
        [
            _family(
                unused_constructor,
                "XRL.World/BeguilingSifrah.cs",
                "BeguilingSifrah",
                {"Popup": 1, "DescriptionAssignment": 1},
            ),
            _family(
                dynamic_constructor,
                "XRL.World/TinkeringSifrahTokenBit.cs",
                "TinkeringSifrahTokenBit",
                {"DescriptionAssignment": 2},
            ),
            _family(
                getdescription_return,
                "XRL.World/TinkeringSifrahTokenBit.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual_entries = {
        entry["family_id"]: entry
        for entry in residual_bucket_payload(inventory, inventory_path=Path("sifrah.json"))["entries"]
    }

    assert unused_constructor not in residual_entries
    assert entries[unused_constructor]["closure_status"] == "covered_by_owner_route"

    assert dynamic_constructor not in residual_entries
    assert getdescription_return not in residual_entries
    assert entries[dynamic_constructor]["closure_status"] == "covered_by_owner_route"
    assert entries[getdescription_return]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        dynamic_constructor,
        "SifrahTokenDescriptionTranslationPatch.cs",
        "SifrahTokenDescriptionTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        getdescription_return,
        "SifrahTokenDescriptionTranslationPatch.cs",
        "SifrahTokenDescriptionTranslationPatchTests.cs",
    )


def test_policy_splits_ui_description_assignments_by_static_menu_option_shape() -> None:
    """Qud.UI MenuOption descriptions are static owner gaps, not runtime-only rows."""
    line_option = "Qud.UI/TradeLine.cs::TradeLine.itemOptions"
    options_control = "Qud.UI/OptionsSliderControl.cs::OptionsSliderControl.SAVE_VALUE"
    xrl_ui_sink = "XRL.UI/Popup.cs::Popup.description"
    inventory = _inventory(
        [
            _family(line_option, "Qud.UI/TradeLine.cs", "itemOptions", {"DescriptionAssignment": 3}),
            _family(
                options_control,
                "Qud.UI/OptionsSliderControl.cs",
                "SAVE_VALUE",
                {"DescriptionAssignment": 2},
            ),
            _family(xrl_ui_sink, "XRL.UI/Popup.cs", "description", {"DescriptionAssignment": 1}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual_entries = {
        entry["family_id"]: entry
        for entry in residual_bucket_payload(inventory, inventory_path=Path("ui-description.json"))["entries"]
    }

    assert line_option not in residual_entries
    assert entries[line_option]["closure_status"] == "covered_by_owner_route"

    assert options_control not in residual_entries
    assert entries[options_control]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        options_control,
        "UiMenuOptionDescriptionTranslationPatch.cs",
        "UiMenuOptionDescriptionTranslationPatchTests.cs",
        "ui-options.ja.json",
    )

    assert residual_entries[xrl_ui_sink]["residual_bucket"] == "ui_description_assignment_runtime"
    assert residual_entries[xrl_ui_sink]["residual_disposition"] == "runtime_evidence_required"
    assert entries[xrl_ui_sink]["closure_status"] == "runtime_required"


def test_policy_records_issue762_generated_display_and_pit_routes() -> None:
    """Issue-762 finite display/description owner routes are closed without sibling overclaiming."""
    cyclopean_prism_action_family_id = (
        "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.HandleEvent(BeginTakeActionEvent)"
    )
    cyclopean_prism_reset_family_id = "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.ResetPrism()"
    pit_material_paint_family_id = "XRL.World.Parts/PitMaterial.cs::PitMaterial.PaintPit()"
    pit_material_fire_family_id = "XRL.World.Parts/PitMaterial.cs::PitMaterial.FireEvent(Event)"
    templar_phylactery_family_id = (
        "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.HandleEvent(AfterObjectCreatedEvent)"
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


def test_policy_records_issue719_existing_owner_route_overlay_candidates() -> None:
    """Issue-719 residual rows with existing owner evidence are closed narrowly."""
    family_ids = {
        "action_manager": "XRL.Core/ActionManager.cs::ActionManager.RunSegment()",
        "spindle": "XRL.World.Parts/SpindleNegotiation.cs::SpindleNegotiation.FireEvent(Event)",
        "look": "XRL.UI/Look.cs::Look.ShowLooker(int,int,int)",
        "light": "XRL.World.Parts.Mutation/LightManipulation.cs::LightManipulation.HandleEvent(CommandEvent)",
        "tinkering": (
            "XRL.UI/TinkeringScreen.cs::"
            "TinkeringScreen.PerformUITinkerMod("
            "GameObject,GameObject,TinkerData,BitCost,IEvent,ref bool,List<GameObject>)"
        ),
        "ability_bar": "Qud.UI/AbilityBar.cs::AbilityBar.Update()",
        "status": "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.UpdateViewFromData()",
        "journal": "Qud.UI/JournalLine.cs::JournalLine.setData(FrameworkDataElement)",
        "keybind": "Qud.UI/KeybindRow.cs::KeybindRow.setData(FrameworkDataElement)",
        "pick_object": "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.setData(FrameworkDataElement)",
        "active_effect": "XRL.World.Effects/Submerged.cs::Submerged.GetDescription()",
    }
    inventory = _inventory(
        [
            _family(family_ids["action_manager"], "XRL.Core/ActionManager.cs", "RunSegment", {"Popup": 1}),
            _family(family_ids["spindle"], "XRL.World.Parts/SpindleNegotiation.cs", "FireEvent", {"Popup": 1}),
            _family(family_ids["look"], "XRL.UI/Look.cs", "ShowLooker", {"Popup": 1}),
            _family(
                family_ids["light"],
                "XRL.World.Parts.Mutation/LightManipulation.cs",
                "HandleEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["tinkering"],
                "XRL.UI/TinkeringScreen.cs",
                "PerformUITinkerMod",
                {"Does": 1, "Popup": 1},
            ),
            _family(family_ids["ability_bar"], "Qud.UI/AbilityBar.cs", "Update", {"SetText": 1}),
            _family(
                family_ids["status"],
                "Qud.UI/CharacterStatusScreen.cs",
                "UpdateViewFromData",
                {"SetText": 1},
            ),
            _family(family_ids["journal"], "Qud.UI/JournalLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["keybind"], "Qud.UI/KeybindRow.cs", "setData", {"SetText": 1}),
            _family(family_ids["pick_object"], "Qud.UI/PickGameObjectLine.cs", "setData", {"SetText": 1}),
            _family(
                family_ids["active_effect"],
                "XRL.World.Effects/Submerged.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["action_manager"],
        "ActionManagerRunSegmentTranslationPatch.cs",
        "ActionManagerRunSegmentTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["spindle"],
        "SpindleNegotiationTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["look"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["light"],
        "CombatAndLogMessageQueuePatchTests.cs",
        "LightManipulation",
    )
    _assert_evidence_contains(
        entries,
        family_ids["tinkering"],
        "TinkeringModPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["ability_bar"],
        "AbilityBarButtonTextTranslationPatch.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["active_effect"],
        "EffectDescriptionPatch.cs",
        "ActiveEffectsOwnerPatchTests.cs",
    )


def test_policy_records_issue719_ui_screen_owner_route_overlays() -> None:
    """Issue-719 UI screen residual rows close only exact owner-patched screen routes."""
    family_ids = {
        "attribute_line": "Qud.UI/CharacterAttributeLine.cs::CharacterAttributeLine.setData(FrameworkDataElement)",
        "effect_line": "Qud.UI/CharacterEffectLine.cs::CharacterEffectLine.setData(FrameworkDataElement)",
        "mod_menu": "Qud.UI/ModMenuLine.cs::ModMenuLine.Update()",
        "skills_line": "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.setData(FrameworkDataElement)",
        "equipment_line": "Qud.UI/EquipmentLine.cs::EquipmentLine.setData(FrameworkDataElement)",
        "help_row": "Qud.UI/HelpRow.cs::HelpRow.setData(FrameworkDataElement)",
        "ability_manager_line": "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.setData(FrameworkDataElement)",
        "inventory_status": (
            "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.UpdateViewFromData()"
        ),
        "inventory_line": "Qud.UI/InventoryLine.cs::InventoryLine.setData(FrameworkDataElement)",
        "trade_line": "Qud.UI/TradeLine.cs::TradeLine.setData(FrameworkDataElement)",
        "tinkering_status": "Qud.UI/TinkeringStatusScreen.cs::TinkeringStatusScreen.UpdateViewFromData()",
        "popup_message": (
            "Qud.UI/PopupMessage.cs::PopupMessage.ShowPopup("
            "string,List<QudMenuItem>,Action<QudMenuItem>,List<QudMenuItem>,Action<QudMenuItem>,"
            "string,bool,string,int,Action,IRenderable,string,IRenderable,bool,bool,CancellationToken,"
            "bool,string,string,Location2D,string)"
        ),
        "tinkering_line": "Qud.UI/TinkeringLine.cs::TinkeringLine.setData(FrameworkDataElement)",
        "factions_line": "Qud.UI/FactionsLine.cs::FactionsLine.setData(FrameworkDataElement)",
        "selectable_menu_item": "Qud.UI/SelectableTextMenuItem.cs::SelectableTextMenuItem.SelectChanged(bool)",
        "tinkering_bits": "Qud.UI/TinkeringBitsLine.cs::TinkeringBitsLine.setData(FrameworkDataElement)",
        "keybinds_screen": "Qud.UI/KeybindsScreen.cs::KeybindsScreen.QueryKeybinds()",
        "mod_manager": "Qud.UI/ModManagerUI.cs::ModManagerUI.OnSelect(ModInfo)",
        "achievement_data": "Qud.UI/AchievementViewRow.cs::AchievementViewRow.SetAchievementData(AchievementInfoData)",
        "hidden_achievement": "Qud.UI/AchievementViewRow.cs::AchievementViewRow.SetHiddenData(HiddenAchievementData)",
        "quests_expand": "Qud.UI/QuestsLine.cs::QuestsLine.categoryExpandOptions",
        "quests_collapse": "Qud.UI/QuestsLine.cs::QuestsLine.categoryCollapseOptions",
        "equipment_hotkey": "Qud.UI/EquipmentLine.cs::EquipmentLine.UpdateHotkey()",
        "filter_expand": "Qud.UI/FilterBarCategoryButton.cs::FilterBarCategoryButton.categoryExpandOptions",
        "filter_collapse": "Qud.UI/FilterBarCategoryButton.cs::FilterBarCategoryButton.categoryCollapseOptions",
        "filter_item": "Qud.UI/FilterBarCategoryButton.cs::FilterBarCategoryButton.itemOptions",
        "button_bar_item": "Qud.UI/ButtonBarButton.cs::ButtonBarButton.itemOptions",
        "factions_expand": "Qud.UI/FactionsLine.cs::FactionsLine.categoryExpandOptions",
        "factions_collapse": "Qud.UI/FactionsLine.cs::FactionsLine.categoryCollapseOptions",
        "factions_status_expand_all": "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.EXPAND_ALL",
        "factions_status_collapse_all": "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.COLLAPSE_ALL",
        "inventory_expand": "Qud.UI/InventoryLine.cs::InventoryLine.categoryExpandOptions",
        "inventory_collapse": "Qud.UI/InventoryLine.cs::InventoryLine.categoryCollapseOptions",
        "sultan_expand": "Qud.UI/JournalSultanStatueLine.cs::JournalSultanStatueLine.categoryExpandOptions",
        "sultan_collapse": "Qud.UI/JournalSultanStatueLine.cs::JournalSultanStatueLine.categoryCollapseOptions",
        "skills_expand": "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.categoryExpandOptions",
        "skills_collapse": "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.categoryCollapseOptions",
        "bits_expand": "Qud.UI/TinkeringBitsLine.cs::TinkeringBitsLine.categoryExpandOptions",
        "bits_collapse": "Qud.UI/TinkeringBitsLine.cs::TinkeringBitsLine.categoryCollapseOptions",
        "details_expand": "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.categoryExpandOptions",
        "details_collapse": "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.categoryCollapseOptions",
        "tinkering_expand": "Qud.UI/TinkeringLine.cs::TinkeringLine.categoryExpandOptions",
        "tinkering_collapse": "Qud.UI/TinkeringLine.cs::TinkeringLine.categoryCollapseOptions",
        "trade_expand": "Qud.UI/TradeLine.cs::TradeLine.categoryExpandOptions",
        "trade_collapse": "Qud.UI/TradeLine.cs::TradeLine.categoryCollapseOptions",
        "trade_item": "Qud.UI/TradeLine.cs::TradeLine.itemOptions",
        "popup_sink": (
            "XRL.UI/Popup.cs::Popup.GetPopupOption("
            "int,IReadOnlyList<string>,IReadOnlyList<char>,IReadOnlyList<IRenderable>)"
        ),
    }
    inventory = _inventory(
        [
            _family(family_ids["attribute_line"], "Qud.UI/CharacterAttributeLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["effect_line"], "Qud.UI/CharacterEffectLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["mod_menu"], "Qud.UI/ModMenuLine.cs", "Update", {"SetText": 1}),
            _family(family_ids["skills_line"], "Qud.UI/SkillsAndPowersLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["equipment_line"], "Qud.UI/EquipmentLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["help_row"], "Qud.UI/HelpRow.cs", "setData", {"SetText": 1}),
            _family(family_ids["ability_manager_line"], "Qud.UI/AbilityManagerLine.cs", "setData", {"SetText": 1}),
            _family(
                family_ids["inventory_status"],
                "Qud.UI/InventoryAndEquipmentStatusScreen.cs",
                "UpdateViewFromData",
                {"SetText": 1},
            ),
            _family(family_ids["inventory_line"], "Qud.UI/InventoryLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["trade_line"], "Qud.UI/TradeLine.cs", "setData", {"SetText": 1}),
            _family(
                family_ids["tinkering_status"],
                "Qud.UI/TinkeringStatusScreen.cs",
                "UpdateViewFromData",
                {"SetText": 1},
            ),
            _family(family_ids["popup_message"], "Qud.UI/PopupMessage.cs", "ShowPopup", {"SetText": 1}),
            _family(family_ids["tinkering_line"], "Qud.UI/TinkeringLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["factions_line"], "Qud.UI/FactionsLine.cs", "setData", {"SetText": 1}),
            _family(
                family_ids["selectable_menu_item"],
                "Qud.UI/SelectableTextMenuItem.cs",
                "SelectChanged",
                {"SetText": 1},
            ),
            _family(family_ids["tinkering_bits"], "Qud.UI/TinkeringBitsLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["keybinds_screen"], "Qud.UI/KeybindsScreen.cs", "QueryKeybinds", {"SetText": 1}),
            _family(family_ids["mod_manager"], "Qud.UI/ModManagerUI.cs", "OnSelect", {"SetText": 1}),
            _family(
                family_ids["achievement_data"],
                "Qud.UI/AchievementViewRow.cs",
                "SetAchievementData",
                {"SetText": 4},
            ),
            _family(
                family_ids["hidden_achievement"],
                "Qud.UI/AchievementViewRow.cs",
                "SetHiddenData",
                {"SetText": 4},
            ),
            _family(
                family_ids["quests_expand"],
                "Qud.UI/QuestsLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 3},
            ),
            _family(
                family_ids["quests_collapse"],
                "Qud.UI/QuestsLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 3},
            ),
            _family(family_ids["equipment_hotkey"], "Qud.UI/EquipmentLine.cs", "UpdateHotkey", {"SetText": 1}),
            _family(
                family_ids["filter_expand"],
                "Qud.UI/FilterBarCategoryButton.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 3},
            ),
            _family(
                family_ids["filter_collapse"],
                "Qud.UI/FilterBarCategoryButton.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 3},
            ),
            _family(
                family_ids["filter_item"],
                "Qud.UI/FilterBarCategoryButton.cs",
                "itemOptions",
                {"DescriptionAssignment": 3},
            ),
            *[
                _family(family_ids[key], source_file, member_name, {"DescriptionAssignment": 3})
                for key, source_file, member_name in (
                    ("button_bar_item", "Qud.UI/ButtonBarButton.cs", "itemOptions"),
                    ("factions_expand", "Qud.UI/FactionsLine.cs", "categoryExpandOptions"),
                    ("factions_collapse", "Qud.UI/FactionsLine.cs", "categoryCollapseOptions"),
                    ("factions_status_expand_all", "Qud.UI/FactionsStatusScreen.cs", "EXPAND_ALL"),
                    ("factions_status_collapse_all", "Qud.UI/FactionsStatusScreen.cs", "COLLAPSE_ALL"),
                    ("inventory_expand", "Qud.UI/InventoryLine.cs", "categoryExpandOptions"),
                    ("inventory_collapse", "Qud.UI/InventoryLine.cs", "categoryCollapseOptions"),
                    ("sultan_expand", "Qud.UI/JournalSultanStatueLine.cs", "categoryExpandOptions"),
                    ("sultan_collapse", "Qud.UI/JournalSultanStatueLine.cs", "categoryCollapseOptions"),
                    ("skills_expand", "Qud.UI/SkillsAndPowersLine.cs", "categoryExpandOptions"),
                    ("skills_collapse", "Qud.UI/SkillsAndPowersLine.cs", "categoryCollapseOptions"),
                    ("bits_expand", "Qud.UI/TinkeringBitsLine.cs", "categoryExpandOptions"),
                    ("bits_collapse", "Qud.UI/TinkeringBitsLine.cs", "categoryCollapseOptions"),
                    ("details_expand", "Qud.UI/TinkeringDetailsLine.cs", "categoryExpandOptions"),
                    ("details_collapse", "Qud.UI/TinkeringDetailsLine.cs", "categoryCollapseOptions"),
                    ("tinkering_expand", "Qud.UI/TinkeringLine.cs", "categoryExpandOptions"),
                    ("tinkering_collapse", "Qud.UI/TinkeringLine.cs", "categoryCollapseOptions"),
                    ("trade_expand", "Qud.UI/TradeLine.cs", "categoryExpandOptions"),
                    ("trade_collapse", "Qud.UI/TradeLine.cs", "categoryCollapseOptions"),
                    ("trade_item", "Qud.UI/TradeLine.cs", "itemOptions"),
                )
            ],
            _family(
                family_ids["popup_sink"],
                "XRL.UI/Popup.cs",
                "GetPopupOption",
                {"DirectTextAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "attribute_line",
        "effect_line",
        "mod_menu",
        "skills_line",
        "equipment_line",
        "help_row",
        "ability_manager_line",
        "inventory_status",
        "inventory_line",
        "trade_line",
        "tinkering_status",
        "popup_message",
        "tinkering_line",
        "factions_line",
        "selectable_menu_item",
        "tinkering_bits",
        "keybinds_screen",
        "mod_manager",
        "achievement_data",
        "hidden_achievement",
        "quests_expand",
        "quests_collapse",
        "filter_expand",
        "filter_collapse",
        "filter_item",
        "button_bar_item",
        "factions_expand",
        "factions_collapse",
        "factions_status_expand_all",
        "factions_status_collapse_all",
        "inventory_expand",
        "inventory_collapse",
        "sultan_expand",
        "sultan_collapse",
        "skills_expand",
        "skills_collapse",
        "bits_expand",
        "bits_collapse",
        "details_expand",
        "details_collapse",
        "tinkering_expand",
        "tinkering_collapse",
        "trade_expand",
        "trade_collapse",
        "trade_item",
        "popup_sink",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["attribute_line"],
        "CharacterAttributeLineTranslationPatch.cs",
        "StatusScreenBindingOwnerPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mod_menu"],
        "ModMenuLineTranslationPatch.cs",
        "ModMenuLineTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["inventory_line"],
        "InventoryLineTranslationPatch.cs",
        "InventoryLineTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["popup_message"],
        "PopupMessageTranslationPatch.cs",
        "PopupMessageTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["popup_sink"],
        "PopupGetPopupOptionTranslationPatch.cs",
        "PopupGetPopupOptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["skills_line"],
        "SkillsAndPowersLineTranslationPatch.cs",
        "SkillsAndPowersLineTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["achievement_data"],
        "AchievementViewRowTranslationPatch.cs",
        "AchievementViewRowTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["quests_expand"],
        "QuestsLineTranslationPatch.cs",
        "QuestUiTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["filter_item"],
        "FilterBarCategoryButtonTranslationPatch.cs",
        "FilterBarCategoryButtonTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["trade_item"],
        "UiMenuOptionDescriptionTranslationPatch.cs",
        "UiMenuOptionDescriptionTranslationPatchTests.cs",
    )
    assert entries[family_ids["equipment_hotkey"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["equipment_hotkey"],
        "UpdateHotkey emit hotkey glyphs only",
    )


def test_policy_records_issue719_look_tooltip_owner_overlays() -> None:
    """Look tooltip UI rows close only when GenerateTooltipContent owns the text."""
    family_ids = {
        "setup_tooltip": ("XRL.UI/Look.cs::Look.SetupItemTooltipAsync(XRL.World.GameObject,TooltipTrigger)"),
        "show_tooltip": (
            "XRL.UI/Look.cs::Look.ShowItemTooltipAsync(Vector3,XRL.World.GameObject,bool,UnityEngine.GameObject)"
        ),
        "world_generation": ("Qud.UI/WorldGenerationScreen.cs::WorldGenerationScreen._ShowWorldGenerationScreen(int)"),
        "trade_highlight": ("Qud.UI/TradeScreen.cs::TradeScreen.HandleHighlightObject(FrameworkDataElement)"),
        "popup_update": "Qud.UI/PopupMessage.cs::PopupMessage.Update()",
        "popup_wait": (
            "XRL.UI/Popup.cs::"
            "Popup.WaitNewPopupMessage("
            "string,List<QudMenuItem>,Action<QudMenuItem>,List<QudMenuItem>,"
            "string,string,int,string,IRenderable,IRenderable,bool,bool,"
            "Location2D,string,bool)"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["setup_tooltip"],
                "XRL.UI/Look.cs",
                "SetupItemTooltipAsync",
                {"SetText": 1},
            ),
            _family(
                family_ids["show_tooltip"],
                "XRL.UI/Look.cs",
                "ShowItemTooltipAsync",
                {"SetText": 1},
            ),
            _family(
                family_ids["world_generation"],
                "Qud.UI/WorldGenerationScreen.cs",
                "_ShowWorldGenerationScreen",
                {"SetText": 1},
            ),
            _family(
                family_ids["trade_highlight"],
                "Qud.UI/TradeScreen.cs",
                "HandleHighlightObject",
                {"SetText": 1},
            ),
            _family(family_ids["popup_update"], "Qud.UI/PopupMessage.cs", "Update", {"SetText": 1}),
            _family(family_ids["popup_wait"], "XRL.UI/Popup.cs", "WaitNewPopupMessage", {"SetText": 1}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("setup_tooltip", "show_tooltip"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "LookTooltipContentPatch.cs",
            "LookTooltipContentPatchTests.cs",
        )

    assert entries[family_ids["world_generation"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["world_generation"],
        'BookUI.Books["Quotes"]',
        "Books.jp.xml",
    )
    assert entries[family_ids["trade_highlight"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["trade_highlight"],
        "TradeScreen.HandleHighlightObject binds DisplayNameSingle plus weight/price glyph data",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["popup_update"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["popup_update"],
        "PopupMessage.cs Update only manages hide/cancel/input runtime state",
    )
    assert entries[family_ids["popup_wait"]]["closure_status"] == "not_owner_surface"
    _assert_evidence_contains(
        entries,
        family_ids["popup_wait"],
        "generic PopupMessage.ShowPopup wrappers",
        "no route-local fixed English leaf",
    )


def test_policy_splits_issue719_ui_screen_residuals_by_static_owner_shape() -> None:
    """Qud.UI direct text residuals are split before runtime deferral."""
    families = {
        "fixed_skills_header": (
            "Qud.UI/SkillsAndPowersStatusScreen.cs::"
            "SkillsAndPowersStatusScreen.ShowScreen(XRL.World.GameObject,StatusScreensScreen)",
            "Qud.UI/SkillsAndPowersStatusScreen.cs",
            "ShowScreen",
            {"SetText": 10},
            "ui_screen_fixed_label_gap",
            "covered_by_owner_route",
        ),
        "fixed_key_prompt": (
            "Qud.UI/KeybindBox.cs::KeybindBox.Update()",
            "Qud.UI/KeybindBox.cs",
            "Update",
            {"DirectTextAssignment": 1},
            "ui_screen_fixed_label_gap",
            "covered_by_owner_route",
        ),
        "world_generation": (
            "Qud.UI/WorldGenerationScreen.cs::WorldGenerationScreen._ShowWorldGenerationScreen(int)",
            "Qud.UI/WorldGenerationScreen.cs",
            "_ShowWorldGenerationScreen",
            {"SetText": 9},
            "ui_screen_world_generation_data_owner",
            "covered_by_owner_route",
        ),
        "popup_message": (
            "Qud.UI/PopupMessage.cs::PopupMessage.Update()",
            "Qud.UI/PopupMessage.cs",
            "Update",
            {"DirectTextAssignment": 5},
            "ui_screen_popup_message_runtime",
            "covered_by_owner_route",
        ),
        "options_control": (
            "Qud.UI/OptionsCheckboxControl.cs::OptionsCheckboxControl.Render()",
            "Qud.UI/OptionsCheckboxControl.cs",
            "Render",
            {"SetText": 3},
            "ui_screen_options_control_runtime",
            "covered_by_owner_route",
        ),
        "trade_inventory": (
            "Qud.UI/TradeLine.cs::TradeLine.OnDrag(PointerEventData)",
            "Qud.UI/TradeLine.cs",
            "OnDrag",
            {"SetText": 1},
            "ui_screen_trade_drag_numeric_runtime",
            "covered_by_owner_route",
        ),
        "data_bound": (
            "Qud.UI/Notification.cs::Notification.Routine()",
            "Qud.UI/Notification.cs",
            "Routine",
            {"SetText": 2},
            "ui_screen_notification_runtime",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, _, disposition in families.values():
        expected_status = (
            "covered_by_owner_route"
            if disposition == "covered_by_owner_route"
            else "action_required"
            if disposition == "likely_implementation_gap"
            else "runtime_required"
        )
        assert entries[family_id]["closure_status"] == expected_status

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-ui-screen-static-shapes-test.json"))
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_policy_promotes_options_control_settext_routes_through_existing_owner_patch() -> None:
    """Options control SetText rows are covered by OptionsScreen.Show row translation before binding."""
    families = {
        "category": (
            "Qud.UI/OptionsCategoryControl.cs::OptionsCategoryControl.Render()",
            "Qud.UI/OptionsCategoryControl.cs",
            "Render",
            {"SetText": 3},
        ),
        "checkbox": (
            "Qud.UI/OptionsCheckboxControl.cs::OptionsCheckboxControl.Render()",
            "Qud.UI/OptionsCheckboxControl.cs",
            "Render",
            {"SetText": 3},
        ),
        "row": (
            "Qud.UI/OptionsRow.cs::OptionsRow.setData(FrameworkDataElement)",
            "Qud.UI/OptionsRow.cs",
            "setData",
            {"SetText": 3},
        ),
        "button": (
            "Qud.UI/OptionsButtonControl.cs::OptionsButtonControl.Render()",
            "Qud.UI/OptionsButtonControl.cs",
            "Render",
            {"SetText": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-options-control-test.json"))

    assert residual["entries"] == []
    for family_id, _, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "OptionsLocalizationPatch.cs",
            "OptionsLocalizationPatchTests.cs",
            "UITextSkinTranslationPatch.cs",
            "UITextSkinTranslationPatchTests.cs",
        )


def test_policy_promotes_trade_line_drag_settext_routes_as_numeric_pass_through() -> None:
    """TradeLine drag indicator SetText rows contain only colored numeric counts."""
    families = {
        "update": (
            "Qud.UI/TradeLine.cs::TradeLine.Update()",
            "Qud.UI/TradeLine.cs",
            "Update",
        ),
        "begin_drag": (
            "Qud.UI/TradeLine.cs::TradeLine.OnBeginDrag(PointerEventData)",
            "Qud.UI/TradeLine.cs",
            "OnBeginDrag",
        ),
        "drag": (
            "Qud.UI/TradeLine.cs::TradeLine.OnDrag(PointerEventData)",
            "Qud.UI/TradeLine.cs",
            "OnDrag",
        ),
        "scroll": (
            "Qud.UI/TradeLine.cs::TradeLine.OnScroll(PointerEventData)",
            "Qud.UI/TradeLine.cs",
            "OnScroll",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"SetText": 1})
            for family_id, source_file, member_name in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-trade-line-numeric-test.json"))

    assert residual["entries"] == []
    for family_id, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "numeric-only pass-through",
            "TradeLine.cs lines 388, 536, 561, and 619",
        )


def test_policy_splits_issue719_ui_screen_data_bound_runtime_by_source_shape() -> None:
    """Data-bound UI residuals are split by source widget before runtime deferral."""
    families = {
        "left_side_category": (
            "Qud.UI/LeftSideCategory.cs::LeftSideCategory.setData(FrameworkDataElement)",
            "Qud.UI/LeftSideCategory.cs",
            "setData",
            {"SetText": 4},
            "ui_screen_left_side_category_gap",
            "covered_by_owner_route",
        ),
        "mod_manager_back": (
            "Qud.UI/ModManagerUI.cs::ModManagerUI.SetBackButtonText(string)",
            "Qud.UI/ModManagerUI.cs",
            "SetBackButtonText",
            {"DirectTextAssignment": 3},
            "ui_screen_mod_manager_back_button_runtime",
            "covered_by_owner_route",
        ),
        "notification": (
            "Qud.UI/Notification.cs::Notification.Routine()",
            "Qud.UI/Notification.cs",
            "Routine",
            {"SetText": 2},
            "ui_screen_notification_runtime",
            "covered_by_owner_route",
        ),
        "console_input": (
            "Qud.UI/ConsoleWindow.cs::ConsoleWindow.Update()",
            "Qud.UI/ConsoleWindow.cs",
            "Update",
            {"DirectTextAssignment": 1},
            "ui_screen_console_input_runtime",
            "covered_by_owner_route",
        ),
        "cybernetics_terminal_set": (
            "Qud.UI/CyberneticsTerminalRow.cs::CyberneticsTerminalRow.setData(FrameworkDataElement)",
            "Qud.UI/CyberneticsTerminalRow.cs",
            "setData",
            {"SetText": 1},
            "ui_screen_cybernetics_terminal_runtime",
            "covered_by_owner_route",
        ),
        "cybernetics_terminal_update": (
            "Qud.UI/CyberneticsTerminalRow.cs::CyberneticsTerminalRow.Update()",
            "Qud.UI/CyberneticsTerminalRow.cs",
            "Update",
            {"SetText": 1},
            "ui_screen_cybernetics_terminal_runtime",
            "covered_by_owner_route",
        ),
        "missile_weapon_status": (
            "Qud.UI/MissileWeaponAreaInfo.cs::"
            "MissileWeaponAreaInfo.UpdateFrom(MissileWeaponArea.MissileWeaponAreaWeaponStatus)",
            "Qud.UI/MissileWeaponAreaInfo.cs",
            "UpdateFrom",
            {"SetText": 1},
            "ui_screen_missile_weapon_status_runtime",
            "covered_by_owner_route",
        ),
        "popup_update": (
            "Qud.UI/PopupMessage.cs::PopupMessage.Update()",
            "Qud.UI/PopupMessage.cs",
            "Update",
            {"DirectTextAssignment": 5},
            "ui_screen_popup_message_runtime",
            "covered_by_owner_route",
        ),
        "status_update": (
            "Qud.UI/StatusBarStatBlock.cs::StatusBarStatBlock.Update()",
            "Qud.UI/StatusBarStatBlock.cs",
            "Update",
            {"SetText": 1},
            "ui_screen_status_stat_runtime",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-ui-data-bound-test.json"))

    assert {
        family_id: entries[family_id]["closure_status"]
        for family_id, _, _, _, _, _ in families.values()
    } == {
        family_id: (
            "action_required" if disposition == "likely_implementation_gap" else disposition
        )
        for family_id, _, _, _, _, disposition in families.values()
    }
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_trade_inventory_ui_runtime_routes_by_widget_shape() -> None:
    """Trade/inventory UI residuals keep control, numeric, and item-detail widgets separate."""
    families = {
        "trade_highlight": (
            "Qud.UI/TradeScreen.cs::TradeScreen.HandleHighlightObject(FrameworkDataElement)",
            "Qud.UI/TradeScreen.cs",
            "HandleHighlightObject",
            "ui_screen_trade_highlight_runtime",
        ),
        "equipment_hotkey": (
            "Qud.UI/EquipmentLine.cs::EquipmentLine.UpdateHotkey()",
            "Qud.UI/EquipmentLine.cs",
            "UpdateHotkey",
            "ui_screen_hotkey_control_runtime",
        ),
        "inventory_hotkey": (
            "Qud.UI/InventoryLine.cs::InventoryLine.UpdateHotkey()",
            "Qud.UI/InventoryLine.cs",
            "UpdateHotkey",
            "ui_screen_hotkey_control_runtime",
        ),
        "inventory_drag": (
            "Qud.UI/InventoryLine.cs::InventoryLine.OnBeginDragObject()",
            "Qud.UI/InventoryLine.cs",
            "OnBeginDragObject",
            "ui_screen_inventory_drag_numeric_runtime",
        ),
        "progress": (
            "Qud.UI/ProgressBar.cs::ProgressBar.Set(int,int)",
            "Qud.UI/ProgressBar.cs",
            "Set",
            "ui_screen_progress_numeric_runtime",
        ),
        "status": (
            "Qud.UI/StatusBarStatBlock.cs::StatusBarStatBlock.UpdateStats(Dictionary<string,string>)",
            "Qud.UI/StatusBarStatBlock.cs",
            "UpdateStats",
            "ui_screen_status_stat_runtime",
        ),
        "trade_drag": (
            "Qud.UI/TradeLine.cs::TradeLine.OnDrag(PointerEventData)",
            "Qud.UI/TradeLine.cs",
            "OnDrag",
            "ui_screen_trade_drag_numeric_runtime",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"SetText": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-ui-trade-inventory-runtime-test.json"),
    )

    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "runtime_evidence_required")
        for family_id, _, _, bucket in families.values()
        if bucket
        not in {
            "ui_screen_hotkey_control_runtime",
            "ui_screen_inventory_drag_numeric_runtime",
            "ui_screen_progress_numeric_runtime",
            "ui_screen_status_stat_runtime",
            "ui_screen_trade_drag_numeric_runtime",
            "ui_screen_trade_highlight_runtime",
        }
    }


def test_policy_reclassifies_issue719_final_sink_sentinel_and_static_owner_runtime_rows() -> None:
    """Final sinks/sentinels close as pass-through while exact owners become implementation gaps."""
    families = {
        "show_success": (
            "Extensions.cs::Extensions.ShowSuccess(this XRL.World.GameObject,string,bool)",
            "Extensions.cs",
            "ShowSuccess",
            {"Popup": 1},
            None,
            "covered_by_owner_route",
        ),
        "message_queue_char": (
            "XRL.Messages/MessageQueue.cs::MessageQueue.AddPlayerMessage(string,char,bool)",
            "XRL.Messages/MessageQueue.cs",
            "AddPlayerMessage",
            {"AddPlayerMessage": 1},
            None,
            "covered_by_owner_route",
        ),
        "fade_text": (
            "XRL.UI/FadeText.cs::FadeText.Update()",
            "XRL.UI/FadeText.cs",
            "Update",
            {"TutorialManagerPopup": 2},
            None,
            "covered_by_owner_route",
        ),
        "fungal_choose_limb": (
            "XRL.World.Effects/FungalSporeInfection.cs::"
            "FungalSporeInfection.ChooseLimbForInfection(List<BodyPart>,string,out BodyPart,out string,bool)",
            "XRL.World.Effects/FungalSporeInfection.cs",
            "ChooseLimbForInfection",
            {"Popup": 4},
            None,
            "covered_by_owner_route",
        ),
        "desalination": (
            "XRL.World.Parts/DesalinationPellet.cs::DesalinationPellet.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/DesalinationPellet.cs",
            "HandleEvent",
            {"EmitMessage": 3},
            None,
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    assert {
        family_id: entries[family_id]["closure_status"]
        for family_id, _, _, _, _, _ in families.values()
    } == {
        family_id: (
            "action_required" if disposition == "likely_implementation_gap" else disposition
        )
        for family_id, _, _, _, _, disposition in families.values()
    }
    _assert_evidence_contains(
        entries,
        families["show_success"][0],
        "ShowSuccess forwards caller-owned Message to Popup.Show",
    )
    _assert_evidence_contains(
        entries,
        families["fade_text"][0],
        "sends only the <nohighlight> tutorial control sentinel",
    )
    _assert_evidence_contains(
        entries,
        families["fungal_choose_limb"][0],
        "FungalSporeInfectionTranslationPatch",
        "PopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests",
    )
    _assert_evidence_contains(
        entries,
        families["desalination"][0],
        "DesalinationPelletTranslationPatch",
        "DesalinationPelletFragmentTranslator",
        "WorldPartsProducerTranslationPatchTests",
    )

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-final-runtime-static-test.json"))
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_policy_records_issue719_mutation_and_world_mod_description_route_overlays() -> None:
    """Issue-719 residual mutation and world-mod GetDescription rows use existing owner routes."""
    family_ids = {
        "mutation": "XRL.World.Parts.Mutation/SlimeGlands.cs::SlimeGlands.GetDescription()",
        "world_mod": "XRL.World.Parts/ModNanon.cs::ModNanon.GetDescription(int)",
        "existing_mutation_exact": "XRL.World.Parts.Mutation/EvilTwin.cs::EvilTwin.GetDescription()",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["mutation"],
                "XRL.World.Parts.Mutation/SlimeGlands.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                family_ids["world_mod"],
                "XRL.World.Parts/ModNanon.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                family_ids["existing_mutation_exact"],
                "XRL.World.Parts.Mutation/EvilTwin.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_ids["mutation"]]["closure_status"] == "covered_by_owner_route"
    assert entries[family_ids["world_mod"]]["closure_status"] == "covered_by_owner_route"
    assert entries[family_ids["existing_mutation_exact"]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["mutation"],
        "mutation-descriptions.ja.json",
        "CharacterStatusScreenMutationDetailsPatchTests.cs",
        "ChargenStructuredTextTranslatorTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["world_mod"],
        "WorldModsTextTranslator.cs",
        "DescriptionShortDescriptionPatchTests.cs",
        "DescriptionLongDescriptionPatchTests.cs",
    )

    exact_evidence = " ".join(entries[family_ids["existing_mutation_exact"]]["closure_evidence"])
    assert "runtime clone creation display/popup text is tracked separately" in exact_evidence


def test_policy_records_issue719_chargen_customize_owner_overlays() -> None:
    """Issue-719 chargen customize rows close only exact existing owner-patched routes."""
    family_ids = {
        "get_selections": (
            "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
            "QudCustomizeCharacterModuleWindow.GetSelections()"
        ),
        "select_menu_option": (
            "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
            "QudCustomizeCharacterModuleWindow.SelectMenuOption(FrameworkDataElement)"
        ),
        "choose_gender": (
            "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
            "QudCustomizeCharacterModuleWindow.OnChooseGenderAsync()"
        ),
        "choose_pronouns": (
            "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
            "QudCustomizeCharacterModuleWindow.OnChoosePronounSetAsync()"
        ),
        "choose_pet": (
            "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
            "QudCustomizeCharacterModuleWindow.OnChoosePet()"
        ),
        "mutations_select_variant": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::QudMutationsModuleWindow.SelectVariant()"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["get_selections"],
                "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs",
                "GetSelections",
                {"DescriptionAssignment": 11},
            ),
            _family(
                family_ids["select_menu_option"],
                "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs",
                "SelectMenuOption",
                {"Popup": 6},
            ),
            _family(
                family_ids["choose_gender"],
                "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs",
                "OnChooseGenderAsync",
                {"Popup": 5},
            ),
            _family(
                family_ids["choose_pronouns"],
                "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs",
                "OnChoosePronounSetAsync",
                {"Popup": 6},
            ),
            _family(
                family_ids["choose_pet"],
                "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs",
                "OnChoosePet",
                {"Popup": 3},
            ),
            _family(
                family_ids["mutations_select_variant"],
                "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
                "SelectVariant",
                {"Popup": 2},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "get_selections",
        "select_menu_option",
        "choose_gender",
        "choose_pronouns",
        "choose_pet",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "CharGenCustomizeTranslationPatch.cs",
            "CharGenProducerTranslationPatchTests.cs",
            "CharGenProducerTranslationPatchResolutionTests.cs",
            "ui-chargen.ja.json",
        )

    assert entries[family_ids["mutations_select_variant"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["mutations_select_variant"],
        "QudMutationsModuleWindowVariantPopupTranslationPatch.cs",
        "QudMutationsModuleWindowTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-chargen-supplement.ja.json",
    )


def test_policy_records_issue719_exact_description_short_description_overlay() -> None:
    """Issue-719 description residuals close only exact owner-route evidence."""
    family_ids = {
        "short_description": ("XRL.World.Parts/Description.cs::Description.GetShortDescription(bool,bool,string)"),
        "effect_details": "XRL.World/Effect.cs::Effect.GetDetails()",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["short_description"],
                "XRL.World.Parts/Description.cs",
                "GetShortDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                family_ids["effect_details"],
                "XRL.World/Effect.cs",
                "GetDetails",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_ids["short_description"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["short_description"],
        "DescriptionShortDescriptionPatch.cs",
        "DescriptionShortDescriptionPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["effect_details"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["effect_details"],
        "EffectDetailsPatch.cs",
        "ActiveEffectsOwnerPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_exact_producer_message_owner_overlays() -> None:  # noqa: PLR0915
    """Issue-719 producer residuals close only exact owner-patched message routes."""
    family_ids = {
        "xrl_game_load": "XRL/XRLGame.cs::XRLGame.LoadGame(string,bool,bool,Dictionary<string,object>)",
        "disassembly": "XRL.World.Tinkering/Disassembly.cs::Disassembly.Continue()",
        "zone_generate": "XRL.World/ZoneManager.cs::ZoneManager.GenerateZone(string)",
        "scores": "XRL.Core/Scores.cs::Scores.Show()",
        "tinkering_build": (
            "XRL.UI/TinkeringScreen.cs::TinkeringScreen.PerformUITinkerBuild(GameObject,TinkerData,IEvent)"
        ),
        "mod_info_dependencies": "XRL/ModInfo.cs::ModInfo.ConfirmDependencies()",
        "mod_info_update": "XRL/ModInfo.cs::ModInfo.ConfirmUpdate()",
        "mod_scroller_one": "Qud.UI/ModScrollerOne.cs::ModScrollerOne.OnActivate(ModInfo)",
        "key_mapping": "XRL.UI/KeyMappingUI.cs::KeyMappingUI.Show()",
        "keybinds_handle_menu_option": (
            "Qud.UI/KeybindsScreen.cs::KeybindsScreen.HandleMenuOption(FrameworkDataElement)"
        ),
        "spiral_borer_curio": "XRL.World.Parts/SpiralBorerCurio.cs::SpiralBorerCurio.HandleEvent(InventoryActionEvent)",
        "telekinesis": "XRL.World.Parts.Mutation/Telekinesis.cs::Telekinesis.HandleEvent(InventoryActionEvent)",
        "telekinesis_activate": "XRL.World.Parts.Mutation/Telekinesis.cs::Telekinesis.Activate(bool)",
        "telekinesis_attempt": "XRL.World.Parts.Mutation/Telekinesis.cs::Telekinesis.AttemptTelekinesis()",
        "destroy_on_unequip": (
            "XRL.World.Parts/DestroyOnUnequip.cs::DestroyOnUnequip.HandleEvent(BeginBeingUnequippedEvent)"
        ),
        "trade_screen_ask_number": "Qud.UI/TradeScreen.cs::TradeScreen.HandleTradeSome(TradeLine)",
        "activated_ability_entry": (
            "XRL.World.Parts/ActivatedAbilityEntry.cs::ActivatedAbilityEntry.TrySendCommandEventOnPlayer()"
        ),
        "fetches": "XRL.World.Parts/Fetches.cs::Fetches.HandleEvent(AIBoredEvent)",
        "checkpoint_death": "XRL/CheckpointingSystem.cs::CheckpointingSystem.ShowDeathMessage(string,string)",
        "skills_select_node": "XRL.UI/SkillsAndPowersScreen.cs::SkillsAndPowersScreen.SelectNode(SPNode,GameObject)",
        "status_mutation_popup": "XRL.UI/StatusScreen.cs::StatusScreen.ShowMutationPopup(GameObject,BaseMutation)",
        "campfire_disease": "XRL.World.Parts/Campfire.cs::Campfire.NostrumsTreatDiseaseOnset()",
        "campfire_poison": "XRL.World.Parts/Campfire.cs::Campfire.NostrumsTreatPoison()",
        "campfire_illness": "XRL.World.Parts/Campfire.cs::Campfire.NostrumsTreatIllness()",
        "campfire_bleeding": "XRL.World.Parts/Campfire.cs::Campfire.NostrumsStopBleeding()",
        "door_attempt_open": (
            "XRL.World.Parts/Door.cs::Door.AttemptOpen(GameObject,bool,bool,bool,bool,bool,bool,IEvent)"
        ),
        "door_hack_success": "XRL.World.Parts/Door.cs::Door.HackingResultSuccess(GameObject,GameObject,HackingSifrah)",
        "door_hack_exceptional": (
            "XRL.World.Parts/Door.cs::Door.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "door_hack_partial": (
            "XRL.World.Parts/Door.cs::Door.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "door_hack_failure": "XRL.World.Parts/Door.cs::Door.HackingResultFailure(GameObject,GameObject,HackingSifrah)",
        "door_hack_critical": (
            "XRL.World.Parts/Door.cs::Door.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "power_switch_hack_success": (
            "XRL.World.Parts/PowerSwitch.cs::PowerSwitch.HackingResultSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "power_switch_hack_exceptional": (
            "XRL.World.Parts/PowerSwitch.cs::"
            "PowerSwitch.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "power_switch_hack_partial": (
            "XRL.World.Parts/PowerSwitch.cs::"
            "PowerSwitch.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "power_switch_hack_failure": (
            "XRL.World.Parts/PowerSwitch.cs::PowerSwitch.HackingResultFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "power_switch_hack_critical": (
            "XRL.World.Parts/PowerSwitch.cs::"
            "PowerSwitch.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "phylactery_hack_success": (
            "XRL.World.Parts/TemplarPhylactery.cs::"
            "TemplarPhylactery.HackingResultSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "phylactery_hack_exceptional": (
            "XRL.World.Parts/TemplarPhylactery.cs::"
            "TemplarPhylactery.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "phylactery_hack_partial": (
            "XRL.World.Parts/TemplarPhylactery.cs::"
            "TemplarPhylactery.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "phylactery_hack_failure": (
            "XRL.World.Parts/TemplarPhylactery.cs::"
            "TemplarPhylactery.HackingResultFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "phylactery_hack_critical": (
            "XRL.World.Parts/TemplarPhylactery.cs::"
            "TemplarPhylactery.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "cybernetics_hack_exceptional": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::"
            "CyberneticsTerminal2.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "cybernetics_hack_partial": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::"
            "CyberneticsTerminal2.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)"
        ),
        "cybernetics_hack_failure": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::"
            "CyberneticsTerminal2.HackingResultFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "cybernetics_hack_critical": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::"
            "CyberneticsTerminal2.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)"
        ),
        "leveler_rapid": "XRL.World.Parts/Leveler.cs::Leveler.RapidAdvancement(int,GameObject)",
        "vehicle_seat": "XRL.World.Parts/VehicleSeat.cs::VehicleSeat.AttemptPilot(GameObject)",
        "decoy_hologram": (
            "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.ActivateHologramBracelet(GameObject,IEvent)"
        ),
        "teleporter_pair": "XRL.World.Parts/TeleporterPair.cs::TeleporterPair.AttemptTeleport(GameObject,IEvent)",
        "campfire_preserve": "XRL.World.Parts/Campfire.cs::Campfire.Preserve()",
        "campfire_preserve_exotic": "XRL.World.Parts/Campfire.cs::Campfire.PreserveExotic()",
        "joppa_zealot": "XRL.World.Parts/JoppaZealot.cs::JoppaZealot.ZealotDeclaim(GameObject,bool)",
        "six_day_zealot": "XRL.World.Parts/SixDayZealot.cs::SixDayZealot.ZealotDeclaim(GameObject,bool)",
        "companion_ability": (
            "XRL.World/GameObject.cs::GameObject.ChangeCompanionAbilityUse(GameObject,ActivatedAbilities)"
        ),
        "confirm_important_async": (
            "XRL.World/GameObject.cs::GameObject.ConfirmUseImportantAsync(GameObject,string,string,int)"
        ),
        "confirm_important": ("XRL.World/GameObject.cs::GameObject.ConfirmUseImportant(GameObject,string,string,int)"),
        "toggle_activated_ability": ("XRL.World/GameObject.cs::GameObject.ToggleActivatedAbility(Guid,bool,bool?)"),
        "gain_sp": "XRL.World/GameObject.cs::GameObject.GainSP(int,bool)",
        "gain_ego": "XRL.World/GameObject.cs::GameObject.GainEgo(int,bool)",
        "lose_ego": "XRL.World/GameObject.cs::GameObject.LoseEgo(int,bool)",
        "gain_intelligence": "XRL.World/GameObject.cs::GameObject.GainIntelligence(int,bool)",
        "gain_willpower": "XRL.World/GameObject.cs::GameObject.GainWillpower(int,bool)",
        "proselytization_critical": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultCriticalFailure(GameObject)"
        ),
        "proselytization_failure": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultFailure(GameObject)"
        ),
        "proselytization_partial": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultPartialSuccess(GameObject)"
        ),
        "proselytization_success": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultSuccess(GameObject)"
        ),
        "proselytization_exceptional": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultExceptionalSuccess(GameObject)"
        ),
        "proselytization_constructor": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ProselytizationSifrah(GameObject,int,int)"
        ),
        "proselytization_check_early_exit": (
            "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.CheckEarlyExit(GameObject)"
        ),
        "conversation_check_lost": "XRL.UI/ConversationUI.cs::ConversationUI.CheckLost()",
        "belcher": "XRL.World.Parts.Mutation/Belcher.cs::Belcher.Cast(Belcher,string,bool,bool)",
        "terrain_travel": "XRL.World.Parts/TerrainTravel.cs::TerrainTravel.HandleEvent(ObjectEnteredCellEvent)",
        "terrain_leaving": ("XRL.World.Parts/TerrainTravel.cs::TerrainTravel.HandleLeavingCell(GameObject,ref int)"),
        "journal_delete": ("XRL.UI/JournalScreen.cs::JournalScreen.HandleDelete(string,IBaseJournalEntry,GameObject)"),
        "polygel": "XRL.World.Parts/Polygel.cs::Polygel.HandleEvent(InventoryActionEvent)",
        "script_call_to_arms": "XRL.World.ZoneParts/ScriptCallToArms.cs::ScriptCallToArms.ShowWarning()",
        "game_object_factory": "XRL.World/GameObjectFactory.cs::GameObjectFactory.HandleBlueprintXML(string)",
        "player_mural_controller": (
            "XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.HandleEvent(EndTurnEvent)"
        ),
        "code_redemption_no_progress": "CodeRedemptionManager.cs::CodeRedemptionManager.redeemNoProgress(string)",
        "code_redemption_progress": "CodeRedemptionManager.cs::CodeRedemptionManager.redeem(string)",
        "xrl_core_save_management": "XRL.Core/XRLCore.cs::XRLCore.SaveManagement()",
        "examiner_critical": "XRL.World.Parts/Examiner.cs::Examiner.ResultCriticalFailure(GameObject)",
        "quest_finish_step": "XRL.World/Quest.cs::Quest.ShowFinishStepPopup(QuestStep)",
        "dynamic_quest_reward": (
            "XRL.World/DynamicQuestRewardElement_GameObject.cs::DynamicQuestRewardElement_GameObject.award()"
        ),
        "imodification": "XRL.World.Parts/IModification.cs::IModification.WishModify(string)",
        "cursed_cell_changed": ("XRL.World.Parts/CursedCellSocket.cs::CursedCellSocket.HandleEvent(CellChangedEvent)"),
        "cursed_cell_depleted": (
            "XRL.World.Parts/CursedCellSocket.cs::CursedCellSocket.HandleEvent(CellDepletedEvent)"
        ),
        "nephal_before_death": (
            "XRL.World.Parts/NephalProperties.cs::NephalProperties.HandleEvent(BeforeDeathRemovalEvent)"
        ),
        "mutation_wish": "XRL.World.Parts/Mutations.cs::Mutations.WishMutation(string)",
        "reality_distortion_sifrah": (
            "XRL.World/RealityDistortionSifrah.cs::"
            "RealityDistortionSifrah.RealityDistortionSifrah(GameObject,string,string,int,int)"
        ),
        "baetyl_check_early_exit": (
            "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.CheckEarlyExit(GameObject)"
        ),
        "beguiling_check_early_exit": ("XRL.World/BeguilingSifrah.cs::BeguilingSifrah.CheckEarlyExit(GameObject)"),
        "formal_water_ritual_check_early_exit": (
            "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.CheckEarlyExit(GameObject)"
        ),
        "haggling_check_early_exit": ("XRL.World/HagglingSifrah.cs::HagglingSifrah.CheckEarlyExit(GameObject)"),
        "item_naming_check_early_exit": ("XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.CheckEarlyExit(GameObject)"),
        "psychic_combat_check_early_exit": (
            "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.CheckEarlyExit(GameObject)"
        ),
        "rebuking_check_early_exit": ("XRL.World/RebukingSifrah.cs::RebukingSifrah.CheckEarlyExit(GameObject)"),
        "reverse_engineering_check": (
            "XRL.World/ReverseEngineeringSifrah.cs::ReverseEngineeringSifrah.CheckEarlyExit(GameObject)"
        ),
        "reverse_engineering_finish": (
            "XRL.World/ReverseEngineeringSifrah.cs::ReverseEngineeringSifrah.Finish(GameObject)"
        ),
        "ritual_attribute_sacrifice": (
            "XRL.World/RitualSifrahTokenAttributeSacrifice.cs::"
            "RitualSifrahTokenAttributeSacrifice.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "ritual_invoke_higher_being": (
            "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs::"
            "RitualSifrahTokenInvokeHigherBeing.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "social_secret_check": (
            "XRL.World/SocialSifrahTokenSecret.cs::"
            "SocialSifrahTokenSecret.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "tinkering_bit_check": (
            "XRL.World/TinkeringSifrahTokenBit.cs::"
            "TinkeringSifrahTokenBit.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "tinkering_charge_check": (
            "XRL.World/TinkeringSifrahTokenCharge.cs::"
            "TinkeringSifrahTokenCharge.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "tinkering_compute_check": (
            "XRL.World/TinkeringSifrahTokenComputePower.cs::"
            "TinkeringSifrahTokenComputePower.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "tinkering_liquid_check": (
            "XRL.World/TinkeringSifrahTokenLiquid.cs::"
            "TinkeringSifrahTokenLiquid.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "sifrah_make_move": "XRL/SifrahGame.cs::SifrahGame.MakeMoveForSlot(int,GameObject)",
        "sifrah_use_insight": "XRL/SifrahGame.cs::SifrahGame.UseInsight(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["xrl_game_load"], "XRL/XRLGame.cs", "LoadGame", {"Popup": 1}),
            _family(family_ids["disassembly"], "XRL.World.Tinkering/Disassembly.cs", "Continue", {"Popup": 1}),
            _family(family_ids["zone_generate"], "XRL.World/ZoneManager.cs", "GenerateZone", {"Popup": 1}),
            _family(family_ids["scores"], "XRL.Core/Scores.cs", "Show", {"Popup": 1}),
            _family(
                family_ids["tinkering_build"],
                "XRL.UI/TinkeringScreen.cs",
                "PerformUITinkerBuild",
                {"Popup": 1},
            ),
            _family(family_ids["mod_info_dependencies"], "XRL/ModInfo.cs", "ConfirmDependencies", {"Popup": 1}),
            _family(family_ids["mod_info_update"], "XRL/ModInfo.cs", "ConfirmUpdate", {"Popup": 1}),
            _family(family_ids["mod_scroller_one"], "Qud.UI/ModScrollerOne.cs", "OnActivate", {"Popup": 1}),
            _family(family_ids["key_mapping"], "XRL.UI/KeyMappingUI.cs", "Show", {"Popup": 1}),
            _family(
                family_ids["keybinds_handle_menu_option"],
                "Qud.UI/KeybindsScreen.cs",
                "HandleMenuOption",
                {"Popup": 1},
            ),
            _family(
                family_ids["spiral_borer_curio"],
                "XRL.World.Parts/SpiralBorerCurio.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["telekinesis"],
                "XRL.World.Parts.Mutation/Telekinesis.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["telekinesis_activate"],
                "XRL.World.Parts.Mutation/Telekinesis.cs",
                "Activate",
                {"Popup": 1},
            ),
            _family(
                family_ids["telekinesis_attempt"],
                "XRL.World.Parts.Mutation/Telekinesis.cs",
                "AttemptTelekinesis",
                {"Popup": 1},
            ),
            _family(
                family_ids["destroy_on_unequip"],
                "XRL.World.Parts/DestroyOnUnequip.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["trade_screen_ask_number"],
                "Qud.UI/TradeScreen.cs",
                "HandleTradeSome",
                {"Popup": 1},
            ),
            _family(
                family_ids["activated_ability_entry"],
                "XRL.World.Parts/ActivatedAbilityEntry.cs",
                "TrySendCommandEventOnPlayer",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["fetches"],
                "XRL.World.Parts/Fetches.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["checkpoint_death"],
                "XRL/CheckpointingSystem.cs",
                "ShowDeathMessage",
                {"Popup": 1},
            ),
            _family(family_ids["skills_select_node"], "XRL.UI/SkillsAndPowersScreen.cs", "SelectNode", {"Popup": 1}),
            _family(family_ids["status_mutation_popup"], "XRL.UI/StatusScreen.cs", "ShowMutationPopup", {"Popup": 1}),
            _family(
                family_ids["campfire_disease"],
                "XRL.World.Parts/Campfire.cs",
                "NostrumsTreatDiseaseOnset",
                {"MessageFrame": 1},
            ),
            _family(family_ids["campfire_poison"], "XRL.World.Parts/Campfire.cs", "NostrumsTreatPoison", {"Popup": 1}),
            _family(
                family_ids["campfire_illness"],
                "XRL.World.Parts/Campfire.cs",
                "NostrumsTreatIllness",
                {"Popup": 1},
            ),
            _family(
                family_ids["campfire_bleeding"],
                "XRL.World.Parts/Campfire.cs",
                "NostrumsStopBleeding",
                {"Popup": 1},
            ),
            _family(family_ids["door_attempt_open"], "XRL.World.Parts/Door.cs", "AttemptOpen", {"MessageFrame": 1}),
            _family(family_ids["door_hack_success"], "XRL.World.Parts/Door.cs", "HackingResultSuccess", {"Popup": 1}),
            _family(
                family_ids["door_hack_exceptional"],
                "XRL.World.Parts/Door.cs",
                "HackingResultExceptionalSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["door_hack_partial"],
                "XRL.World.Parts/Door.cs",
                "HackingResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(family_ids["door_hack_failure"], "XRL.World.Parts/Door.cs", "HackingResultFailure", {"Popup": 1}),
            _family(
                family_ids["door_hack_critical"],
                "XRL.World.Parts/Door.cs",
                "HackingResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["power_switch_hack_success"],
                "XRL.World.Parts/PowerSwitch.cs",
                "HackingResultSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["power_switch_hack_exceptional"],
                "XRL.World.Parts/PowerSwitch.cs",
                "HackingResultExceptionalSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["power_switch_hack_partial"],
                "XRL.World.Parts/PowerSwitch.cs",
                "HackingResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["power_switch_hack_failure"],
                "XRL.World.Parts/PowerSwitch.cs",
                "HackingResultFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["power_switch_hack_critical"],
                "XRL.World.Parts/PowerSwitch.cs",
                "HackingResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["phylactery_hack_success"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "HackingResultSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["phylactery_hack_exceptional"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "HackingResultExceptionalSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["phylactery_hack_partial"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "HackingResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["phylactery_hack_failure"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "HackingResultFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["phylactery_hack_critical"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "HackingResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["cybernetics_hack_exceptional"],
                "XRL.World.Parts/CyberneticsTerminal2.cs",
                "HackingResultExceptionalSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["cybernetics_hack_partial"],
                "XRL.World.Parts/CyberneticsTerminal2.cs",
                "HackingResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["cybernetics_hack_failure"],
                "XRL.World.Parts/CyberneticsTerminal2.cs",
                "HackingResultFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["cybernetics_hack_critical"],
                "XRL.World.Parts/CyberneticsTerminal2.cs",
                "HackingResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(family_ids["leveler_rapid"], "XRL.World.Parts/Leveler.cs", "RapidAdvancement", {"Popup": 1}),
            _family(family_ids["vehicle_seat"], "XRL.World.Parts/VehicleSeat.cs", "AttemptPilot", {"Popup": 1}),
            _family(
                family_ids["decoy_hologram"],
                "XRL.World.Parts/DecoyHologramEmitter.cs",
                "ActivateHologramBracelet",
                {"Popup": 1},
            ),
            _family(
                family_ids["teleporter_pair"],
                "XRL.World.Parts/TeleporterPair.cs",
                "AttemptTeleport",
                {"Popup": 1},
            ),
            _family(family_ids["campfire_preserve"], "XRL.World.Parts/Campfire.cs", "Preserve", {"Popup": 1}),
            _family(
                family_ids["campfire_preserve_exotic"],
                "XRL.World.Parts/Campfire.cs",
                "PreserveExotic",
                {"Popup": 1},
            ),
            _family(family_ids["joppa_zealot"], "XRL.World.Parts/JoppaZealot.cs", "ZealotDeclaim", {"EmitMessage": 1}),
            _family(
                family_ids["six_day_zealot"],
                "XRL.World.Parts/SixDayZealot.cs",
                "ZealotDeclaim",
                {"EmitMessage": 1},
            ),
            _family(
                family_ids["companion_ability"],
                "XRL.World/GameObject.cs",
                "ChangeCompanionAbilityUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["confirm_important_async"],
                "XRL.World/GameObject.cs",
                "ConfirmUseImportantAsync",
                {"Popup": 1},
            ),
            _family(
                family_ids["confirm_important"],
                "XRL.World/GameObject.cs",
                "ConfirmUseImportant",
                {"Popup": 1},
            ),
            _family(
                family_ids["toggle_activated_ability"],
                "XRL.World/GameObject.cs",
                "ToggleActivatedAbility",
                {"AddPlayerMessage": 1},
            ),
            _family(family_ids["gain_sp"], "XRL.World/GameObject.cs", "GainSP", {"Popup": 1}),
            _family(family_ids["gain_ego"], "XRL.World/GameObject.cs", "GainEgo", {"Popup": 1}),
            _family(family_ids["lose_ego"], "XRL.World/GameObject.cs", "LoseEgo", {"Popup": 1}),
            _family(
                family_ids["gain_intelligence"],
                "XRL.World/GameObject.cs",
                "GainIntelligence",
                {"Popup": 1},
            ),
            _family(
                family_ids["gain_willpower"],
                "XRL.World/GameObject.cs",
                "GainWillpower",
                {"Popup": 1},
            ),
            _family(
                family_ids["proselytization_critical"],
                "XRL.World/ProselytizationSifrah.cs",
                "ResultCriticalFailure",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["proselytization_failure"],
                "XRL.World/ProselytizationSifrah.cs",
                "ResultFailure",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["proselytization_partial"],
                "XRL.World/ProselytizationSifrah.cs",
                "ResultPartialSuccess",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["proselytization_success"],
                "XRL.World/ProselytizationSifrah.cs",
                "ResultSuccess",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["proselytization_exceptional"],
                "XRL.World/ProselytizationSifrah.cs",
                "ResultExceptionalSuccess",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["proselytization_constructor"],
                "XRL.World/ProselytizationSifrah.cs",
                ".ctor",
                {"Popup": 1},
            ),
            _family(
                family_ids["proselytization_check_early_exit"],
                "XRL.World/ProselytizationSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 1},
            ),
            _family(
                family_ids["conversation_check_lost"],
                "XRL.UI/ConversationUI.cs",
                "CheckLost",
                {"Does": 1, "Popup": 1},
            ),
            _family(family_ids["belcher"], "XRL.World.Parts.Mutation/Belcher.cs", "Cast", {"Popup": 1}),
            _family(
                family_ids["terrain_travel"],
                "XRL.World.Parts/TerrainTravel.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["terrain_leaving"],
                "XRL.World.Parts/TerrainTravel.cs",
                "HandleLeavingCell",
                {"AddPlayerMessage": 1, "Popup": 1},
            ),
            _family(family_ids["journal_delete"], "XRL.UI/JournalScreen.cs", "HandleDelete", {"Popup": 1}),
            _family(family_ids["polygel"], "XRL.World.Parts/Polygel.cs", "HandleEvent", {"Popup": 1}),
            _family(
                family_ids["script_call_to_arms"],
                "XRL.World.ZoneParts/ScriptCallToArms.cs",
                "ShowWarning",
                {"Popup": 1},
            ),
            _family(
                family_ids["game_object_factory"],
                "XRL.World/GameObjectFactory.cs",
                "HandleBlueprintXML",
                {"Popup": 1},
            ),
            _family(
                family_ids["player_mural_controller"],
                "XRL.World.Parts/PlayerMuralController.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["code_redemption_no_progress"],
                "CodeRedemptionManager.cs",
                "redeemNoProgress",
                {"Popup": 1},
            ),
            _family(
                family_ids["code_redemption_progress"],
                "CodeRedemptionManager.cs",
                "redeem",
                {"Popup": 1},
            ),
            _family(
                family_ids["xrl_core_save_management"],
                "XRL.Core/XRLCore.cs",
                "SaveManagement",
                {"Popup": 1},
            ),
            _family(
                family_ids["examiner_critical"],
                "XRL.World.Parts/Examiner.cs",
                "ResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["quest_finish_step"],
                "XRL.World/Quest.cs",
                "ShowFinishStepPopup",
                {"Popup": 1},
            ),
            _family(
                family_ids["dynamic_quest_reward"],
                "XRL.World/DynamicQuestRewardElement_GameObject.cs",
                "award",
                {"Popup": 1},
            ),
            _family(
                family_ids["imodification"],
                "XRL.World.Parts/IModification.cs",
                "WishModify",
                {"Popup": 1},
            ),
            _family(
                family_ids["cursed_cell_changed"],
                "XRL.World.Parts/CursedCellSocket.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["cursed_cell_depleted"],
                "XRL.World.Parts/CursedCellSocket.cs",
                "HandleEvent",
                {"EmitMessage": 1},
            ),
            _family(
                family_ids["nephal_before_death"],
                "XRL.World.Parts/NephalProperties.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["mutation_wish"],
                "XRL.World.Parts/Mutations.cs",
                "WishMutation",
                {"Popup": 1},
            ),
            _family(
                family_ids["reality_distortion_sifrah"],
                "XRL.World/RealityDistortionSifrah.cs",
                "RealityDistortionSifrah",
                {"Popup": 1},
            ),
            _family(
                family_ids["baetyl_check_early_exit"],
                "XRL.World/BaetylOfferingSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["beguiling_check_early_exit"],
                "XRL.World/BeguilingSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["formal_water_ritual_check_early_exit"],
                "XRL.World/FormalWaterRitualSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["haggling_check_early_exit"],
                "XRL.World/HagglingSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["item_naming_check_early_exit"],
                "XRL.World/ItemNamingSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["psychic_combat_check_early_exit"],
                "XRL.World/PsychicCombatSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["rebuking_check_early_exit"],
                "XRL.World/RebukingSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 2},
            ),
            _family(
                family_ids["reverse_engineering_check"],
                "XRL.World/ReverseEngineeringSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 1},
            ),
            _family(
                family_ids["reverse_engineering_finish"],
                "XRL.World/ReverseEngineeringSifrah.cs",
                "Finish",
                {"Popup": 1},
            ),
            _family(
                family_ids["ritual_attribute_sacrifice"],
                "XRL.World/RitualSifrahTokenAttributeSacrifice.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["ritual_invoke_higher_being"],
                "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["social_secret_check"],
                "XRL.World/SocialSifrahTokenSecret.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["tinkering_bit_check"],
                "XRL.World/TinkeringSifrahTokenBit.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["tinkering_charge_check"],
                "XRL.World/TinkeringSifrahTokenCharge.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["tinkering_compute_check"],
                "XRL.World/TinkeringSifrahTokenComputePower.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["tinkering_liquid_check"],
                "XRL.World/TinkeringSifrahTokenLiquid.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(family_ids["sifrah_make_move"], "XRL/SifrahGame.cs", "MakeMoveForSlot", {"Popup": 1}),
            _family(family_ids["sifrah_use_insight"], "XRL/SifrahGame.cs", "UseInsight", {"Popup": 4}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "disassembly",
        "tinkering_build",
        "zone_generate",
        "mod_info_dependencies",
        "mod_info_update",
        "mod_scroller_one",
        "key_mapping",
        "keybinds_handle_menu_option",
        "spiral_borer_curio",
        "telekinesis",
        "telekinesis_activate",
        "telekinesis_attempt",
        "destroy_on_unequip",
        "trade_screen_ask_number",
        "activated_ability_entry",
        "fetches",
        "skills_select_node",
        "status_mutation_popup",
        "campfire_disease",
        "campfire_poison",
        "campfire_illness",
        "campfire_bleeding",
        "door_attempt_open",
        "door_hack_success",
        "door_hack_exceptional",
        "door_hack_partial",
        "door_hack_failure",
        "door_hack_critical",
        "power_switch_hack_success",
        "power_switch_hack_exceptional",
        "power_switch_hack_partial",
        "power_switch_hack_failure",
        "power_switch_hack_critical",
        "phylactery_hack_success",
        "phylactery_hack_exceptional",
        "phylactery_hack_partial",
        "phylactery_hack_failure",
        "phylactery_hack_critical",
        "cybernetics_hack_exceptional",
        "cybernetics_hack_partial",
        "cybernetics_hack_failure",
        "cybernetics_hack_critical",
        "leveler_rapid",
        "vehicle_seat",
        "decoy_hologram",
        "teleporter_pair",
        "campfire_preserve",
        "campfire_preserve_exotic",
        "joppa_zealot",
        "six_day_zealot",
        "companion_ability",
        "confirm_important_async",
        "confirm_important",
        "toggle_activated_ability",
        "gain_sp",
        "gain_ego",
        "lose_ego",
        "gain_intelligence",
        "gain_willpower",
        "proselytization_critical",
        "proselytization_failure",
        "proselytization_partial",
        "proselytization_success",
        "proselytization_exceptional",
        "conversation_check_lost",
        "belcher",
        "terrain_travel",
        "terrain_leaving",
        "journal_delete",
        "polygel",
        "script_call_to_arms",
        "game_object_factory",
        "player_mural_controller",
        "code_redemption_no_progress",
        "code_redemption_progress",
        "xrl_core_save_management",
        "examiner_critical",
        "quest_finish_step",
        "dynamic_quest_reward",
        "imodification",
        "cursed_cell_changed",
        "nephal_before_death",
        "mutation_wish",
        "reality_distortion_sifrah",
        "baetyl_check_early_exit",
        "beguiling_check_early_exit",
        "formal_water_ritual_check_early_exit",
        "haggling_check_early_exit",
        "item_naming_check_early_exit",
        "proselytization_check_early_exit",
        "psychic_combat_check_early_exit",
        "rebuking_check_early_exit",
        "reverse_engineering_check",
        "reverse_engineering_finish",
        "ritual_attribute_sacrifice",
        "ritual_invoke_higher_being",
        "social_secret_check",
        "tinkering_bit_check",
        "tinkering_charge_check",
        "tinkering_compute_check",
        "tinkering_liquid_check",
        "sifrah_use_insight",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["disassembly"],
        "DisassemblyStartTranslationPatch.cs",
        "DisassemblyStartTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["tinkering_build"],
        "TinkeringBuildPopupTranslationPatch.cs",
        "TinkeringBuildPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["zone_generate"],
        "ZoneManagerGenerateZoneTranslationPatch.cs",
        "ZoneManagerGenerateZoneTranslationPatchTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mod_info_dependencies"],
        "ModInfoTranslationPatch.cs",
        "ModInfoTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mod_scroller_one"],
        "ModScrollerOneTranslationPatch.cs",
        "ModScrollerOneTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["key_mapping"],
        "KeyMappingUiTranslationPatch.cs",
        "KeyMappingUiTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["keybinds_handle_menu_option"],
        "KeyMappingUiTranslationPatch.cs",
        "KeyMappingUiTranslationPatchTests.cs",
        "PopupShowTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-options.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["spiral_borer_curio"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "world-parts.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["telekinesis"],
        "TelekinesisTranslationPatch.cs",
        "TelekinesisTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["telekinesis_activate"],
        "TelekinesisTranslationPatch.cs",
        "TelekinesisTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["telekinesis_attempt"],
        "TelekinesisTranslationPatch.cs",
        "TelekinesisTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["destroy_on_unequip"],
        "static_producer_closure.py",
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["trade_screen_ask_number"],
        "TradeScreenUiTranslationPatch.cs",
        "PopupAskNumberTranslationPatch.cs",
        "TradeScreenUiTranslationPatchTests.cs",
        "PopupAskNumberTranslationPatchTests.cs",
        "templates-format.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["activated_ability_entry"],
        "static_producer_closure.py",
        "SingleCallsiteOwnerQueueTranslationPatch.cs",
        "SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["fetches"],
        "static_producer_closure.py",
        "SingleCallsiteOwnerQueueTranslationPatch.cs",
        "SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["skills_select_node"],
        "SkillsAndPowersSelectNodePopupTranslationPatch.cs",
        "SkillsAndPowersSelectNodePopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["status_mutation_popup"],
        "StatusScreenMutationPopupTranslationPatch.cs",
        "StatusScreenPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["campfire_poison"],
        "CampfireNostrumsTranslationPatch.cs",
        "CampfireNostrumsTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["door_attempt_open"],
        "DoorAttemptOpenTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["door_hack_success"],
        "HackingSifrahResultTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["power_switch_hack_success"],
        "HackingSifrahResultTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["phylactery_hack_success"],
        "HackingSifrahResultTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cybernetics_hack_exceptional"],
        "HackingSifrahResultTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["leveler_rapid"],
        "LevelerTranslationPatch.cs",
        "LevelerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["vehicle_seat"],
        "VehicleSeatTranslationPatch.cs",
        "VehicleSeatTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["decoy_hologram"],
        "DecoyHologramEmitterActivateTranslationPatch.cs",
        "DecoyHologramEmitterActivateTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["teleporter_pair"],
        "TeleporterPairTranslationPatch.cs",
        "TeleporterPairTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["campfire_preserve"],
        "CampfirePreserveTranslationPatch.cs",
        "CampfirePreserveTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["joppa_zealot"],
        "JoppaZealotTranslationPatch.cs",
        "FloatingYellTextTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["six_day_zealot"],
        "SixDayZealotTranslationPatch.cs",
        "FloatingYellTextTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["companion_ability"],
        "GameObjectPopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["confirm_important"],
        "GameObjectPopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["toggle_activated_ability"],
        "GameObjectToggleActivatedAbilityTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["gain_sp"],
        "GameObjectStatPopupTranslationPatch.cs",
        "GameObjectStatPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["proselytization_critical"],
        "ProselytizationSifrahTranslationPatch.cs",
        "ProselytizationSifrahTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["conversation_check_lost"],
        "ConversationCheckLostPopupTranslationPatch.cs",
        "ConversationCheckLostPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["belcher"],
        "MutationGeneratedTextTranslationPatch.cs",
        "MutationGeneratedTextTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["terrain_travel"],
        "TerrainTravelTranslationPatch.cs",
        "TerrainTravelTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["terrain_leaving"],
        "TerrainTravelTranslationPatch.cs",
        "TerrainTravelTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["journal_delete"],
        "JournalScreenPopupTranslationPatch.cs",
        "JournalScreenPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["polygel"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["script_call_to_arms"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["game_object_factory"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["player_mural_controller"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["code_redemption_no_progress"],
        "CodeRedemptionPopupTranslationPatch.cs",
        "PopupShowTranslationPatch.cs",
        "CodeRedemptionPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["xrl_core_save_management"],
        "OldSaveContinueMenuPopupTranslationPatch.cs",
        "OldSaveContinueMenuPopupTranslationPatchTests.cs",
        "PopupShowTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["examiner_critical"],
        "ExaminerTranslationPatch.cs",
        "ExaminerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["quest_finish_step"],
        "QuestLifecyclePopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["dynamic_quest_reward"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["imodification"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cursed_cell_changed"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["nephal_before_death"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mutation_wish"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["reality_distortion_sifrah"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["proselytization_constructor"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    for family_key in (
        "baetyl_check_early_exit",
        "beguiling_check_early_exit",
        "formal_water_ritual_check_early_exit",
        "haggling_check_early_exit",
        "item_naming_check_early_exit",
        "proselytization_check_early_exit",
        "psychic_combat_check_early_exit",
        "rebuking_check_early_exit",
    ):
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "SifrahPureOwnerPopupTranslationPatch.cs",
            "SifrahPureOwnerPopupTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "ui-popup.ja.json",
        )
    _assert_evidence_contains(
        entries,
        family_ids["reverse_engineering_finish"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["tinkering_liquid_check"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["sifrah_make_move"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["sifrah_use_insight"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cursed_cell_depleted"],
        "GameObjectEmitMessageTranslationPatch.cs",
        "MessagePatternTranslatorTests.cs",
        "DoesVerbFamilyTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cybernetics_hack_partial"],
        "HackingSifrahResultTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["scores"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["scores"],
        "LegacyScoresScreenTranslationPatchTests.cs",
        "HighScoresDeletePopupTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["xrl_game_load"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["checkpoint_death"],
        "PopupTranslationPatch.cs",
        "PopupPickOptionTranslationPatch.cs",
        "PopupShowSpaceTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )


def test_policy_records_issue719_existing_sifrah_and_deploy_exact_owner_overlays() -> None:
    """Issue-719 closes exact Sifrah/deploy rows while keeping partial neighbors residual."""
    family_ids = {
        "check_companion": "XRL.World/GameObject.cs::GameObject.CheckCompanionDirection(GameObject)",
        "deploy_one": (
            "XRL.World.Parts/DeployableInfrastructure.cs::DeployableInfrastructure.DeployOne(GameObject,Cell,bool,bool)"
        ),
        "beguiling_critical": ("XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultCriticalFailure(GameObject)"),
        "beguiling_failure": "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultFailure(GameObject)",
        "beguiling_partial": ("XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultPartialSuccess(GameObject)"),
        "beguiling_success": "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultSuccess(GameObject)",
        "beguiling_exceptional": ("XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultExceptionalSuccess(GameObject)"),
        "item_modding_failure": "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultFailure(GameObject)",
        "item_modding_partial": ("XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultPartialSuccess(GameObject)"),
        "item_modding_success": "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultSuccess(GameObject)",
        "item_modding_critical_success": (
            "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultCriticalSuccess(GameObject)"
        ),
        "rebuking_critical": "XRL.World/RebukingSifrah.cs::RebukingSifrah.ResultCriticalFailure(GameObject)",
        "rebuking_partial": "XRL.World/RebukingSifrah.cs::RebukingSifrah.ResultPartialSuccess(GameObject)",
        "rebuking_failure": "XRL.World/RebukingSifrah.cs::RebukingSifrah.ResultFailure(GameObject)",
        "giant_clam_from": (
            "XRL.World.Parts/GiantClamProperties.cs::GiantClamProperties.TeleportFromClamWorld(GameObject)"
        ),
        "electrical_discharge": (
            "XRL.World.Parts.Mutation/ElectricalGeneration.cs::ElectricalGeneration.PerformDischarge(bool)"
        ),
        "water_random_mutation": (
            "XRL.World.Conversations.Parts/WaterRitualRandomMutation.cs::"
            "WaterRitualRandomMutation.HandleEvent(EnteredElementEvent)"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["check_companion"],
                "XRL.World/GameObject.cs",
                "CheckCompanionDirection",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["deploy_one"],
                "XRL.World.Parts/DeployableInfrastructure.cs",
                "DeployOne",
                {"Assignment": 1, "EmitMessage": 1, "OtherInvocation": 1},
            ),
            _family(
                family_ids["beguiling_critical"],
                "XRL.World/BeguilingSifrah.cs",
                "ResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(family_ids["beguiling_failure"], "XRL.World/BeguilingSifrah.cs", "ResultFailure", {"Popup": 1}),
            _family(
                family_ids["beguiling_partial"],
                "XRL.World/BeguilingSifrah.cs",
                "ResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(family_ids["beguiling_success"], "XRL.World/BeguilingSifrah.cs", "ResultSuccess", {"Popup": 1}),
            _family(
                family_ids["beguiling_exceptional"],
                "XRL.World/BeguilingSifrah.cs",
                "ResultExceptionalSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["item_modding_failure"],
                "XRL.World/ItemModdingSifrah.cs",
                "ResultFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["item_modding_partial"],
                "XRL.World/ItemModdingSifrah.cs",
                "ResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["item_modding_success"],
                "XRL.World/ItemModdingSifrah.cs",
                "ResultSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["item_modding_critical_success"],
                "XRL.World/ItemModdingSifrah.cs",
                "ResultCriticalSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["rebuking_critical"],
                "XRL.World/RebukingSifrah.cs",
                "ResultCriticalFailure",
                {"Popup": 1},
            ),
            _family(
                family_ids["rebuking_partial"],
                "XRL.World/RebukingSifrah.cs",
                "ResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(family_ids["rebuking_failure"], "XRL.World/RebukingSifrah.cs", "ResultFailure", {"Popup": 1}),
            _family(
                family_ids["giant_clam_from"],
                "XRL.World.Parts/GiantClamProperties.cs",
                "TeleportFromClamWorld",
                {"AddPlayerMessage": 1, "Initializer": 1, "OtherInvocation": 1, "Popup": 1},
            ),
            _family(
                family_ids["electrical_discharge"],
                "XRL.World.Parts.Mutation/ElectricalGeneration.cs",
                "PerformDischarge",
                {"Initializer": 1, "OtherInvocation": 1, "Popup": 1},
            ),
            _family(
                family_ids["water_random_mutation"],
                "XRL.World.Conversations.Parts/WaterRitualRandomMutation.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "check_companion",
        "deploy_one",
        "beguiling_critical",
        "beguiling_failure",
        "beguiling_partial",
        "beguiling_success",
        "beguiling_exceptional",
        "item_modding_failure",
        "item_modding_partial",
        "item_modding_success",
        "item_modding_critical_success",
        "rebuking_critical",
        "rebuking_failure",
        "rebuking_partial",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["check_companion"],
        "GameObjectPopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["deploy_one"],
        "DeployableInfrastructureTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "static_producer_closure.py",
    )
    _assert_evidence_contains(
        entries,
        family_ids["beguiling_critical"],
        "BeguilingSifrahTranslationPatch.cs",
        "BeguilingSifrahTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["item_modding_failure"],
        "ItemModdingSifrahTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["rebuking_critical"],
        "RebukingSifrahTranslationPatch.cs",
        "RebukingSifrahTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["giant_clam_from"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["giant_clam_from"],
        "GiantClamTeleportTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "GiantClamTeleportTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["electrical_discharge"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["electrical_discharge"],
        "MutationActionFailureTranslationPatch.cs",
        "MutationActionFailureTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["water_random_mutation"],
        "WaterRitualPopupTranslationPatch.cs",
        "WaterRitualPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_existing_patch_exact_audit_batch() -> None:
    """Issue-719 exact audit closes only whole-family existing-patch matches."""
    family_ids = {
        "neutron_pour": (
            "XRL.World.Parts/NeutronFluxContainment.cs::"
            "NeutronFluxContainment.HandleEvent(NeutronFluxPourExplodesEvent)"
        ),
        "fabricate_activate": "XRL.World.Parts/FabricateFromSelf.cs::FabricateFromSelf.Activate(bool)",
        "psychometry_inventory": (
            "XRL.World.Parts.Mutation/Psychometry.cs::Psychometry.HandleEvent(InventoryActionEvent)"
        ),
        "repair_critical": "XRL.World.Parts/Repair.cs::Repair.RepairResultCriticalFailure(GameObject,GameObject)",
        "neutron_begin_take": (
            "XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.HandleEvent(BeginTakeActionEvent)"
        ),
        "psychometry_bonus": (
            "XRL.World.Parts.Mutation/Psychometry.cs::Psychometry.HandleEvent(GetTinkeringBonusEvent)"
        ),
        "sunder_tick": "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Tick()",
        "social_gift": (
            "XRL.World/SocialSifrahTokenGift.cs::SocialSifrahTokenGift.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "social_item": (
            "XRL.World/SocialSifrahTokenItem.cs::SocialSifrahTokenItem.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "summoning_curio": ("XRL.World.Parts/SummoningCurio.cs::SummoningCurio.HandleEvent(InventoryActionEvent)"),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["neutron_pour"],
                "XRL.World.Parts/NeutronFluxContainment.cs",
                "HandleEvent",
                {"Does": 2, "OtherInvocation": 2, "Popup": 1},
            ),
            _family(
                family_ids["fabricate_activate"],
                "XRL.World.Parts/FabricateFromSelf.cs",
                "Activate",
                {"AddPlayerMessage": 3, "Initializer": 1, "OtherInvocation": 10, "Popup": 3},
            ),
            _family(
                family_ids["psychometry_inventory"],
                "XRL.World.Parts.Mutation/Psychometry.cs",
                "HandleEvent",
                {"OtherInvocation": 5, "Popup": 6},
            ),
            _family(
                family_ids["repair_critical"],
                "XRL.World.Parts/Repair.cs",
                "RepairResultCriticalFailure",
                {"Initializer": 1, "MessageFrame": 3, "OtherInvocation": 3},
            ),
            _family(
                family_ids["neutron_begin_take"],
                "XRL.World.Parts/NeutronFluxContainment.cs",
                "HandleEvent",
                {"OtherInvocation": 1, "Popup": 1},
            ),
            _family(
                family_ids["psychometry_bonus"],
                "XRL.World.Parts.Mutation/Psychometry.cs",
                "HandleEvent",
                {"Other": 6, "Popup": 2},
            ),
            _family(
                family_ids["sunder_tick"],
                "XRL.World.Parts.Mutation/SunderMind.cs",
                "Tick",
                {"AddPlayerMessage": 1, "Initializer": 8, "OtherInvocation": 18, "Popup": 2},
            ),
            _family(family_ids["social_gift"], "XRL.World/SocialSifrahTokenGift.cs", "CheckTokenUse", {"Popup": 3}),
            _family(family_ids["social_item"], "XRL.World/SocialSifrahTokenItem.cs", "CheckTokenUse", {"Popup": 3}),
            _family(
                family_ids["summoning_curio"],
                "XRL.World.Parts/SummoningCurio.cs",
                "HandleEvent",
                {"Assignment": 1, "Other": 1, "OtherInvocation": 5, "Popup": 2},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "neutron_pour",
        "fabricate_activate",
        "psychometry_inventory",
        "repair_critical",
        "neutron_begin_take",
        "psychometry_bonus",
        "social_gift",
        "social_item",
        "summoning_curio",
        "sunder_tick",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["neutron_pour"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "MessageFrames/verbs.ja.json",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["fabricate_activate"],
        "FabricateFromSelfTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["psychometry_inventory"],
        "PsychometryTranslationPatch.cs",
        "WorldPartsProducerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["repair_critical"],
        "RepairTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "MessageFrames/verbs.ja.json",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["neutron_begin_take"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["psychometry_bonus"],
        "PopupShowTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["summoning_curio"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "static_producer_closure.py",
    )
    for family_key in ("social_gift", "social_item"):
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "SifrahTokenItemPopupTranslationPatch.cs",
            "SifrahTokenItemPopupTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    _assert_evidence_contains(
        entries,
        family_ids["sunder_tick"],
        "SunderMindTranslationPatch.cs",
        "MessageQueueSemanticPipeline.cs",
        "PopupShowSemanticPipeline.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )


def test_policy_records_issue719_existing_patch_mutation_popup_mixed_batch() -> None:
    """Issue-719 closes mutation popup/queue rows only when every visible branch is covered."""
    family_ids = {
        "mass_mind": "XRL.World.Parts.Mutation/MassMind.cs::MassMind.FireEvent(Event)",
        "pack_rat": "XRL.World.Parts.Mutation/PackRat.cs::PackRat.FireEvent(Event)",
        "precognition": "XRL.World.Parts.Mutation/Precognition.cs::Precognition.FireEvent(Event)",
        "eros": (
            "XRL.World.Parts.Mutation/ErosTeleportation.cs::ErosTeleportation.Cast(ErosTeleportation,string,Event,Cell)"
        ),
        "food": "XRL.World.Parts/Food.cs::Food.HandleEvent(InventoryActionEvent)",
        "stomach": "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
        "life_drain": "XRL.World.Parts.Mutation/LifeDrain.cs::LifeDrain.FireEvent(Event)",
        "item_naming": (
            "XRL.World.Capabilities/ItemNaming.cs::"
            "ItemNaming.NameItem(GameObject,GameObject,GameObject,GameObject,string,string,bool)"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["mass_mind"],
                "XRL.World.Parts.Mutation/MassMind.cs",
                "FireEvent",
                {
                    "AddPlayerMessage": 3,
                    "Assignment": 1,
                    "Initializer": 1,
                    "Other": 2,
                    "OtherInvocation": 8,
                    "Popup": 1,
                },
            ),
            _family(
                family_ids["pack_rat"],
                "XRL.World.Parts.Mutation/PackRat.cs",
                "FireEvent",
                {"AddPlayerMessage": 1, "Initializer": 1, "Other": 4, "OtherInvocation": 7, "Popup": 2},
            ),
            _family(
                family_ids["precognition"],
                "XRL.World.Parts.Mutation/Precognition.cs",
                "FireEvent",
                {
                    "AddPlayerMessage": 3,
                    "Assignment": 1,
                    "Initializer": 2,
                    "Other": 4,
                    "OtherInvocation": 11,
                    "Popup": 3,
                },
            ),
            _family(
                family_ids["eros"],
                "XRL.World.Parts.Mutation/ErosTeleportation.cs",
                "Cast",
                {"EmitMessage": 1, "Initializer": 5, "OtherInvocation": 9, "Popup": 2},
            ),
            _family(
                family_ids["food"],
                "XRL.World.Parts/Food.cs",
                "HandleEvent",
                {"Initializer": 1, "Other": 1, "OtherInvocation": 28, "Popup": 2},
            ),
            _family(
                family_ids["stomach"],
                "XRL.World.Parts/Stomach.cs",
                "FireEvent",
                {"AddPlayerMessage": 2, "EmitMessage": 1, "OtherInvocation": 18, "Popup": 4},
            ),
            _family(
                family_ids["life_drain"],
                "XRL.World.Parts.Mutation/LifeDrain.cs",
                "FireEvent",
                {"Popup": 3, "OtherInvocation": 7},
            ),
            _family(
                family_ids["item_naming"],
                "XRL.World.Capabilities/ItemNaming.cs",
                "NameItem",
                {"Other": 16, "OtherInvocation": 3, "Popup": 8},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("mass_mind", "pack_rat", "precognition", "eros", "stomach", "life_drain"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["mass_mind"],
        "MassMindTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "PopupShowTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["pack_rat"],
        "MutationGeneratedTextTranslationPatch.cs",
        "MutationGeneratedTextTranslationPatchTests.cs",
        "PopupShowTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["life_drain"],
        "MutationGeneratedTextTranslationPatch.cs",
        "MutationGeneratedTextTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["precognition"],
        "PrecognitionTranslationPatch.cs",
        "PrecognitionTranslationPatchTests.cs",
        "PopupShowTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["eros"],
        "ErosTeleportationTranslationPatch.cs",
        "RealityStabilizedInterdictTranslationPatch.cs",
        "FloatingYellTextTranslationPatchTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )

    _assert_evidence_contains(
        entries,
        family_ids["food"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["stomach"],
        "StomachTranslationPatch.cs",
        "StomachTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "world-parts.ja.json",
    )
    assert entries[family_ids["item_naming"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["item_naming"],
        "ItemNamingTranslationPatch.cs",
        "PopupShowColorPickerTranslationPatch.cs",
        "ItemNamingTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_residual_pure_does_verb_tranche() -> None:
    """Issue-719 pure Does residuals close only for reviewed DoesVerb route families."""
    covered_family_ids = {
        "quicken_mind": "XRL.World.Parts/QuickenMind.cs::QuickenMind.Activate(GameObject)",
        "stasis_entangler": (
            "XRL.World.Parts/CyberneticsStasisEntangler.cs::"
            "CyberneticsStasisEntangler.ActivateStasisEntangler(GameObject,GameObject,IEvent)"
        ),
        "glass_armor": "XRL.World.Parts/ModGlassArmor.cs::ModGlassArmor.HandleEvent(BeforeApplyDamageEvent)",
        "stasis_arena": (
            "XRL.World.Parts/CyberneticsStasisArena.cs::"
            "CyberneticsStasisArena.ActivateStasisArena(GameObject,GameObject,IEvent)"
        ),
        "stomach_vomit": "XRL.World.Parts/Stomach.cs::Stomach.HandleEvent(InduceVomitingEvent)",
        "cooldown_loader": "XRL.World.Parts/CooldownAmmoLoader.cs::CooldownAmmoLoader.GetCoolingDownMessage()",
        "phylactery_inventory": (
            "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.HandleEvent(InventoryActionEvent)"
        ),
        "liquid_loader": "XRL.World.Parts/LiquidAmmoLoader.cs::LiquidAmmoLoader.GetStatusMessage(ActivePartStatus)",
        "powered_floating": "XRL.World.Parts/PoweredFloating.cs::PoweredFloating.FireEvent(Event)",
        "conversation_script": (
            "XRL.World.Parts/ConversationScript.cs::"
            "ConversationScript.AttemptConversation(GameObject,GameObject,GameObject,GameObject,"
            "ConversationXMLBlueprint,int,bool,bool,bool?,IEvent)"
        ),
        "electrical_loader": (
            "XRL.World.Parts/ElectricalDischargeLoader.cs::"
            "ElectricalDischargeLoader.GetStatusMessage(ActivePartStatus)"
        ),
        "magazine_loader": "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.HandleEvent(LoadAmmoEvent)",
        "energy_loader": "XRL.World.Parts/EnergyAmmoLoader.cs::EnergyAmmoLoader.GetStatusMessage(ActivePartStatus)",
        "liquid_cooled": "XRL.World.Parts/ModLiquidCooled.cs::ModLiquidCooled.GetStatusMessage(ActivePartStatus)",
        "ai_wiring": "XRL.World.Parts/AIWiring.cs::AIWiring.HandleEvent(IsConversationallyResponsiveEvent)",
        "phylactery_description": (
            "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.HandleEvent(GetShortDescriptionEvent)"
        ),
    }
    residual_family_ids = {
        "cybernetics_menu": "XRL.UI/CyberneticsScreenMainMenu.cs::CyberneticsScreenMainMenu()",
    }
    exact_owner_family_ids = {
        "game_text": "XRL/GameText.cs::GameText.RoughConvertSecondPersonToThirdPerson(string,GameObject)",
        "cybernetics_terminal": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AttemptInterface(GameObject,IEvent)"
        ),
        "domination_process": "XRL.World.Parts.Mutation/Domination.cs::Domination.ProcessTarget(GameObject,ref string)",
    }
    inventory = _inventory(
        [
            *[
                _family(
                    family_id,
                    family_id.split("::", maxsplit=1)[0],
                    family_id.rsplit(".", maxsplit=1)[-1].split("(", maxsplit=1)[0],
                    {"Does": 1},
                )
                for family_id in covered_family_ids.values()
            ],
            *[
                _family(
                    family_id,
                    family_id.split("::", maxsplit=1)[0],
                    family_id.rsplit(".", maxsplit=1)[-1].split("(", maxsplit=1)[0],
                    {"Does": 1},
                )
                for family_id in residual_family_ids.values()
            ],
            *[
                _family(
                    family_id,
                    family_id.split("::", maxsplit=1)[0],
                    family_id.rsplit(".", maxsplit=1)[-1].split("(", maxsplit=1)[0],
                    {"Does": 1},
                )
                for family_id in exact_owner_family_ids.values()
            ],
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_family_ids.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "DoesVerbRouteTranslator.cs",
            "DoesVerbFamilyTests.cs",
            "verbs.ja.json",
        )

    for family_id in residual_family_ids.values():
        assert entries[family_id]["closure_status"] != "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        exact_owner_family_ids["game_text"],
        "GameTextDeathReasonTranslationPatch.cs",
        "GameTextDeathReasonTranslationPatchTests.cs",
        "DeathReasonTranslationPatch.cs",
    )
    _assert_evidence_contains(
        entries,
        exact_owner_family_ids["cybernetics_terminal"],
        "CyberneticsTerminalInterfacePopupTranslationPatch.cs",
        "CyberneticsTerminalInterfacePopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        exact_owner_family_ids["domination_process"],
        "DominationProcessTargetTranslationPatch.cs",
        "DominationProcessTargetTranslationPatchTests.cs",
        "MessageQueueSemanticPipeline.cs",
    )


def test_policy_records_issue719_fixed_literal_popup_overlays() -> None:
    """Issue-719 fixed popup literals close through the generic popup dictionary route."""
    family_ids = {
        "burrowing": "XRL.World.Parts.Mutation/BurrowingClaws.cs::BurrowingClaws.CheckDig()",
        "main_menu": "Qud.UI/MainMenu.cs::MainMenu.Quit()",
        "keybinds": "Qud.UI/KeybindsScreen.cs::KeybindsScreen.Exit()",
        "teleprojector": "XRL.World.Parts/Teleprojector.cs::Teleprojector.EndDomination(GameObject)",
        "tutorial": "TutorialStep.cs::TutorialStep.ConstrainToCurrentZone(Cell)",
        "golem": "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.CheckCompletion()",
        "pax": ("XRL.World.Quests/PaxKlanqIPresumeSystem.cs::PaxKlanqIPresumeSystem.UnderConstructionMessage()"),
        "ambient": "XRL.World.ZoneParts/AmbientStabilization.cs::AmbientStabilization.Stabilize()",
        "reality_distortion": (
            "XRL.World/RealityDistortionSifrah.cs::RealityDistortionSifrah.CheckEarlyExit(GameObject)"
        ),
        "sifrah_incomplete": "XRL/SifrahGame.cs::SifrahGame.CheckIncompleteTurn(GameObject)",
        "sifrah_exit": "XRL/SifrahGame.cs::SifrahGame.CheckEarlyExit(GameObject)",
        "toolkit": (
            "XRL.World/TinkeringSifrahTokenToolkit.cs::"
            "TinkeringSifrahTokenToolkit.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "advanced_toolkit": (
            "XRL.World/TinkeringSifrahTokenAdvancedToolkit.cs::"
            "TinkeringSifrahTokenAdvancedToolkit.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "copper_wire": (
            "XRL.World/TinkeringSifrahTokenCopperWire.cs::"
            "TinkeringSifrahTokenCopperWire.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "hookah": (
            "XRL.World/SocialSifrahTokenHookah.cs::"
            "SocialSifrahTokenHookah.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
        ),
        "call_to_arms_parties": "XRL.World.ZoneParts/ScriptCallToArms.cs::ScriptCallToArms.spawnParties(int)",
        "check_frozen": "XRL.World/GameObject.cs::GameObject.CheckFrozen(bool,bool,bool,GameObject)",
        "mouse_blocker": "Qud.UI/MouseBlocker.cs::MouseBlocker.OnPointerClick(PointerEventData)",
        "boot_handlers": (
            "XRL.CharacterBuilds.Qud/QudSpecificBootHandlersModule.cs::"
            "QudSpecificBootHandlersModule.handleBootEvent(string,XRLGame,EmbarkInfo,object)"
        ),
        "sifrah_make_move": "XRL/SifrahGame.cs::SifrahGame.MakeMoveForSlot(int,GameObject)",
        "examiner": "XRL.World.Parts/Examiner.cs::Examiner.MakeUnderstood(bool)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["burrowing"],
                "XRL.World.Parts.Mutation/BurrowingClaws.cs",
                "CheckDig",
                {"Popup": 1},
            ),
            _family(
                family_ids["main_menu"],
                "Qud.UI/MainMenu.cs",
                "Quit",
                {"Popup": 1},
            ),
            _family(
                family_ids["keybinds"],
                "Qud.UI/KeybindsScreen.cs",
                "Exit",
                {"Popup": 1},
            ),
            _family(
                family_ids["teleprojector"],
                "XRL.World.Parts/Teleprojector.cs",
                "EndDomination",
                {"Popup": 1},
            ),
            _family(
                family_ids["tutorial"],
                "TutorialStep.cs",
                "ConstrainToCurrentZone",
                {"Popup": 1},
            ),
            _family(
                family_ids["golem"],
                "XRL.World.Parts/GolemQuestMound.cs",
                "CheckCompletion",
                {"Popup": 1},
            ),
            _family(
                family_ids["pax"],
                "XRL.World.Quests/PaxKlanqIPresumeSystem.cs",
                "UnderConstructionMessage",
                {"Popup": 1},
            ),
            _family(
                family_ids["ambient"],
                "XRL.World.ZoneParts/AmbientStabilization.cs",
                "Stabilize",
                {"Popup": 1},
            ),
            _family(
                family_ids["reality_distortion"],
                "XRL.World/RealityDistortionSifrah.cs",
                "CheckEarlyExit",
                {"Popup": 1},
            ),
            _family(family_ids["sifrah_incomplete"], "XRL/SifrahGame.cs", "CheckIncompleteTurn", {"Popup": 1}),
            _family(family_ids["sifrah_exit"], "XRL/SifrahGame.cs", "CheckEarlyExit", {"Popup": 1}),
            _family(
                family_ids["toolkit"],
                "XRL.World/TinkeringSifrahTokenToolkit.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["advanced_toolkit"],
                "XRL.World/TinkeringSifrahTokenAdvancedToolkit.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["copper_wire"],
                "XRL.World/TinkeringSifrahTokenCopperWire.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["hookah"],
                "XRL.World/SocialSifrahTokenHookah.cs",
                "CheckTokenUse",
                {"Popup": 1},
            ),
            _family(
                family_ids["call_to_arms_parties"],
                "XRL.World.ZoneParts/ScriptCallToArms.cs",
                "spawnParties",
                {"Popup": 1},
            ),
            _family(
                family_ids["check_frozen"],
                "XRL.World/GameObject.cs",
                "CheckFrozen",
                {"Popup": 1},
            ),
            _family(
                family_ids["mouse_blocker"],
                "Qud.UI/MouseBlocker.cs",
                "OnPointerClick",
                {"Popup": 1},
            ),
            _family(
                family_ids["boot_handlers"],
                "XRL.CharacterBuilds.Qud/QudSpecificBootHandlersModule.cs",
                "handleBootEvent",
                {"Popup": 1},
            ),
            _family(family_ids["sifrah_make_move"], "XRL/SifrahGame.cs", "MakeMoveForSlot", {"Popup": 1}),
            _family(
                family_ids["examiner"],
                "XRL.World.Parts/Examiner.cs",
                "MakeUnderstood",
                {"Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "burrowing",
        "main_menu",
        "keybinds",
        "teleprojector",
        "tutorial",
        "golem",
        "ambient",
        "reality_distortion",
        "sifrah_incomplete",
        "sifrah_exit",
        "toolkit",
        "advanced_toolkit",
        "copper_wire",
        "hookah",
        "call_to_arms_parties",
        "check_frozen",
        "mouse_blocker",
        "boot_handlers",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupShowTranslationPatch.cs",
            "PopupShowTranslationPatchTests.cs",
            "generic popup dictionary route",
        )
    assert entries[family_ids["pax"]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(entries, family_ids["burrowing"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["main_menu"], "ui-default.ja.json")
    _assert_evidence_contains(entries, family_ids["keybinds"], "ui-options.ja.json")
    _assert_evidence_contains(entries, family_ids["teleprojector"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["tutorial"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["golem"], "ui-popup.ja.json")
    _assert_evidence_contains(
        entries,
        family_ids["pax"],
        "PopupShowSpaceTranslationPatch.cs",
        "PopupShowSpaceTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(entries, family_ids["ambient"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["reality_distortion"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["sifrah_exit"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["advanced_toolkit"], "ui-popup.ja.json")
    _assert_evidence_contains(entries, family_ids["call_to_arms_parties"], "ui-popup.ja.json")
    _assert_evidence_contains(
        entries,
        family_ids["check_frozen"],
        "PopupShowTranslationPatch.cs",
        "PopupTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mouse_blocker"],
        "PopupShowTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(entries, family_ids["boot_handlers"], "ui-popup.ja.json")
    _assert_evidence_contains(
        entries,
        family_ids["sifrah_make_move"],
        "SifrahPureOwnerPopupTranslationPatch.cs",
        "SifrahPureOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["examiner"],
        "ExaminerTranslationPatch.cs",
        "ExaminerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_exact_combat_message_owner_overlays() -> None:
    """Issue-719 combat/message residuals close only exact owner-patched producer routes."""
    family_ids = {
        "latches_on": "XRL.World.Parts/LatchesOn.cs::LatchesOn.FireEvent(Event)",
        "tattoo": "XRL.World.Parts/TattooGun.cs::TattooGun.AttemptTattoo(GameObject)",
        "beguiling": ("XRL.World.Parts.Mutation/Beguiling.cs::Beguiling.Cast(GameObject,Beguiling,Event,int)"),
        "engraver": "XRL.World.Parts/Engraver.cs::Engraver.AttemptEngrave(GameObject)",
        "physics_inventory": "XRL.World.Parts/Physics.cs::Physics.HandleEvent(InventoryActionEvent)",
        "iteleporter": "XRL.World.Parts/ITeleporter.cs::ITeleporter.AttemptTeleport(GameObject,IEvent)",
        "energy_ammo": "XRL.World.Parts/EnergyAmmoLoader.cs::EnergyAmmoLoader.FireEvent(Event)",
        "electrical_loader": (
            "XRL.World.Parts/ElectricalDischargeLoader.cs::ElectricalDischargeLoader.FireEvent(Event)"
        ),
        "data_disk": "XRL.World.Parts/DataDisk.cs::DataDisk.HandleEvent(InventoryActionEvent)",
        "pet_either_or": "XRL.World.Parts/PetEitherOr.cs::PetEitherOr.explode()",
        "bed": "XRL.World.Parts/Bed.cs::Bed.AttemptSleep(GameObject,out bool,out bool,out bool)",
        "liquid_pour": (
            "XRL.World.Parts/LiquidVolume.cs::LiquidVolume.Pour(ref bool,GameObject,Cell,bool,bool,int,bool)"
        ),
        "chair": "XRL.World.Parts/Chair.cs::Chair.SitDown(GameObject,IEvent)",
        "stairs_down": "XRL.World.Parts/StairsDown.cs::StairsDown.CheckPullDown(GameObject)",
        "garbage": "XRL.World.Parts/Garbage.cs::Garbage.AttemptRifle(GameObject,bool,Cell,List<GameObject>)",
        "energy_cell": (
            "XRL.World.Parts/EnergyCellSocket.cs::"
            "EnergyCellSocket.AttemptReplaceCell(GameObject,InventoryActionEvent,int,GameObject)"
        ),
        "enclosing": "XRL.World.Parts/Enclosing.cs::Enclosing.EnterEnclosure(GameObject,IEvent)",
        "enclosing_exit": ("XRL.World.Parts/Enclosing.cs::Enclosing.ExitEnclosure(GameObject,IEvent,Enclosed)"),
        "vehicle_recall": "XRL.World.Parts/VehicleRecall.cs::VehicleRecall.HandleEvent(InventoryActionEvent)",
        "game_object_rename": "XRL.World/GameObject.cs::GameObject.HandleRename(InventoryActionEvent)",
        "faction_deed": "XRL.World.Parts/FactionDeed.cs::FactionDeed.HandleEvent(InventoryActionEvent)",
        "animate_object": "XRL.World.Parts/AnimateObject.cs::AnimateObject.HandleEvent(InventoryActionEvent)",
        "eel_spawn": "XRL.World.Parts/EelSpawn.cs::EelSpawn.HandleEvent(ObjectEnteredCellEvent)",
        "water_ritual_buy_secret": (
            "XRL.World.Conversations.Parts/WaterRitualBuySecret.cs::WaterRitualBuySecret.RevealEntry(IBaseJournalEntry)"
        ),
        "equipment_api_twiddle": (
            "Qud.API/EquipmentAPI.cs::"
            "EquipmentAPI.TwiddleObject(GameObject,GameObject,ref bool,out InventoryAction,bool,bool,bool)"
        ),
        "campfire_cook": "XRL.World.Parts/Campfire.cs::Campfire.Cook()",
        "examiner_partial": "XRL.World.Parts/Examiner.cs::Examiner.ResultPartialSuccess(GameObject,int)",
        "submerged_apply": "XRL.World.Effects/Submerged.cs::Submerged.Apply(GameObject)",
        "submerged_fire": "XRL.World.Effects/Submerged.cs::Submerged.FireEvent(Event)",
        "submerged_remove": "XRL.World.Effects/Submerged.cs::Submerged.Remove(GameObject)",
        "burrowed_apply": "XRL.World.Effects/Burrowed.cs::Burrowed.Apply(GameObject)",
        "burrowed_fire": "XRL.World.Effects/Burrowed.cs::Burrowed.FireEvent(Event)",
        "burrowed_emerge": "XRL.World.Effects/Burrowed.cs::Burrowed.Emerge()",
        "conversation_physical": (
            "XRL.World.Parts/ConversationScript.cs::"
            "ConversationScript.IsPhysicalConversationPossible(GameObject,GameObject,bool,bool,bool,int)"
        ),
        "conversation_mental": (
            "XRL.World.Parts/ConversationScript.cs::"
            "ConversationScript.IsMentalConversationPossible(GameObject,GameObject,bool,bool,int)"
        ),
        "trade_vendor_examine": "XRL.UI/TradeUI.cs::TradeUI.DoVendorExamine(GameObject,GameObject)",
        "trade_vendor_recharge": "XRL.UI/TradeUI.cs::TradeUI.DoVendorRecharge(GameObject,GameObject)",
        "precognition_before_die": (
            "XRL.World.Parts.Mutation/Precognition.cs::"
            "Precognition.OnBeforeDie(GameObject,Guid,Guid,ref int,ref int,ref int,ref long,bool,bool,IPart)"
        ),
        "stomach_handle": "XRL.World.Parts/Stomach.cs::Stomach.HandleEvent(BeginTakeActionEvent)",
        "door": "XRL.World.Parts/Door.cs::Door.AttemptOpen(GameObject,bool,bool,bool,bool,bool,bool,IEvent)",
    }
    inventory = _inventory(
        [
            _family(family_ids["latches_on"], "XRL.World.Parts/LatchesOn.cs", "FireEvent", {"MessageFrame": 1}),
            _family(family_ids["tattoo"], "XRL.World.Parts/TattooGun.cs", "AttemptTattoo", {"Popup": 1}),
            _family(
                family_ids["beguiling"],
                "XRL.World.Parts.Mutation/Beguiling.cs",
                "Cast",
                {"AddPlayerMessage": 1, "Does": 1, "Popup": 1},
            ),
            _family(family_ids["engraver"], "XRL.World.Parts/Engraver.cs", "AttemptEngrave", {"Popup": 1}),
            _family(
                family_ids["physics_inventory"],
                "XRL.World.Parts/Physics.cs",
                "HandleEvent",
                {"Does": 1, "Popup": 1},
            ),
            _family(family_ids["iteleporter"], "XRL.World.Parts/ITeleporter.cs", "AttemptTeleport", {"Popup": 1}),
            _family(family_ids["energy_ammo"], "XRL.World.Parts/EnergyAmmoLoader.cs", "FireEvent", {"MessageFrame": 1}),
            _family(
                family_ids["electrical_loader"],
                "XRL.World.Parts/ElectricalDischargeLoader.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(family_ids["data_disk"], "XRL.World.Parts/DataDisk.cs", "HandleEvent", {"Popup": 1}),
            _family(family_ids["pet_either_or"], "XRL.World.Parts/PetEitherOr.cs", "explode", {"Does": 1}),
            _family(family_ids["bed"], "XRL.World.Parts/Bed.cs", "AttemptSleep", {"Popup": 1}),
            _family(family_ids["liquid_pour"], "XRL.World.Parts/LiquidVolume.cs", "Pour", {"Popup": 1}),
            _family(family_ids["chair"], "XRL.World.Parts/Chair.cs", "SitDown", {"MessageFrame": 1}),
            _family(family_ids["stairs_down"], "XRL.World.Parts/StairsDown.cs", "CheckPullDown", {"Popup": 1}),
            _family(family_ids["garbage"], "XRL.World.Parts/Garbage.cs", "AttemptRifle", {"Does": 1}),
            _family(
                family_ids["energy_cell"],
                "XRL.World.Parts/EnergyCellSocket.cs",
                "AttemptReplaceCell",
                {"Popup": 1},
            ),
            _family(family_ids["enclosing"], "XRL.World.Parts/Enclosing.cs", "EnterEnclosure", {"Popup": 1}),
            _family(
                family_ids["enclosing_exit"],
                "XRL.World.Parts/Enclosing.cs",
                "ExitEnclosure",
                {"AddPlayerMessage": 1, "Popup": 1},
            ),
            _family(family_ids["vehicle_recall"], "XRL.World.Parts/VehicleRecall.cs", "HandleEvent", {"Popup": 1}),
            _family(family_ids["game_object_rename"], "XRL.World/GameObject.cs", "HandleRename", {"Popup": 1}),
            _family(family_ids["faction_deed"], "XRL.World.Parts/FactionDeed.cs", "HandleEvent", {"Popup": 1}),
            _family(family_ids["animate_object"], "XRL.World.Parts/AnimateObject.cs", "HandleEvent", {"Popup": 1}),
            _family(family_ids["eel_spawn"], "XRL.World.Parts/EelSpawn.cs", "HandleEvent", {"Popup": 1}),
            _family(
                family_ids["water_ritual_buy_secret"],
                "XRL.World.Conversations.Parts/WaterRitualBuySecret.cs",
                "RevealEntry",
                {"Popup": 1},
            ),
            _family(
                family_ids["equipment_api_twiddle"],
                "Qud.API/EquipmentAPI.cs",
                "TwiddleObject",
                {"Popup": 1},
            ),
            _family(family_ids["campfire_cook"], "XRL.World.Parts/Campfire.cs", "Cook", {"Popup": 1}),
            _family(
                family_ids["examiner_partial"],
                "XRL.World.Parts/Examiner.cs",
                "ResultPartialSuccess",
                {"Popup": 1},
            ),
            _family(
                family_ids["submerged_apply"],
                "XRL.World.Effects/Submerged.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["submerged_fire"],
                "XRL.World.Effects/Submerged.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["submerged_remove"],
                "XRL.World.Effects/Submerged.cs",
                "Remove",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["burrowed_apply"],
                "XRL.World.Effects/Burrowed.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["burrowed_fire"],
                "XRL.World.Effects/Burrowed.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["burrowed_emerge"],
                "XRL.World.Effects/Burrowed.cs",
                "Emerge",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["conversation_physical"],
                "XRL.World.Parts/ConversationScript.cs",
                "IsPhysicalConversationPossible",
                {"Popup": 1},
            ),
            _family(
                family_ids["conversation_mental"],
                "XRL.World.Parts/ConversationScript.cs",
                "IsMentalConversationPossible",
                {"Popup": 1},
            ),
            _family(
                family_ids["trade_vendor_examine"],
                "XRL.UI/TradeUI.cs",
                "DoVendorExamine",
                {"Popup": 1},
            ),
            _family(
                family_ids["trade_vendor_recharge"],
                "XRL.UI/TradeUI.cs",
                "DoVendorRecharge",
                {"Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["precognition_before_die"],
                "XRL.World.Parts.Mutation/Precognition.cs",
                "OnBeforeDie",
                {"AddPlayerMessage": 1, "MessageFrame": 1, "Popup": 1},
            ),
            _family(family_ids["stomach_handle"], "XRL.World.Parts/Stomach.cs", "HandleEvent", {"Popup": 1}),
            _family(family_ids["door"], "XRL.World.Parts/Door.cs", "AttemptOpen", {"Popup": 1}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "latches_on",
        "tattoo",
        "beguiling",
        "engraver",
        "physics_inventory",
        "iteleporter",
        "energy_ammo",
        "electrical_loader",
        "data_disk",
        "pet_either_or",
        "bed",
        "liquid_pour",
        "chair",
        "stairs_down",
        "garbage",
        "energy_cell",
        "enclosing",
        "enclosing_exit",
        "vehicle_recall",
        "game_object_rename",
        "faction_deed",
        "animate_object",
        "eel_spawn",
        "water_ritual_buy_secret",
        "equipment_api_twiddle",
        "campfire_cook",
        "examiner_partial",
        "submerged_apply",
        "submerged_fire",
        "submerged_remove",
        "burrowed_apply",
        "burrowed_fire",
        "burrowed_emerge",
        "conversation_physical",
        "conversation_mental",
        "trade_vendor_examine",
        "trade_vendor_recharge",
        "precognition_before_die",
        "door",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["latches_on"],
        "LatchesOnTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["energy_cell"],
        "EnergyCellSocketAccessPopupTranslationPatch.cs",
        "EnergyCellSocketAccessPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["enclosing_exit"],
        "EnclosingTranslationPatch.cs",
        "WorldPartsProducerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["door"],
        "DoorAttemptOpenTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["vehicle_recall"],
        "ClonelingVehicleTranslationPatch.cs",
        "WorldPartsProducerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["game_object_rename"],
        "GameObjectPopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["faction_deed"],
        "MapRevealPopupTranslationPatch.cs",
        "MapRevealPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["animate_object"],
        "AnimateObjectTranslationPatch.cs",
        "AnimateObjectTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["eel_spawn"],
        "EelSpawnTranslationPatch.cs",
        "EelSpawnTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["water_ritual_buy_secret"],
        "WaterRitualPopupTranslationPatch.cs",
        "WaterRitualPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["equipment_api_twiddle"],
        "EquipmentApiTwiddleObjectTranslationPatch.cs",
        "EquipmentApiTwiddleObjectTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["campfire_cook"],
        "CampfireCookAvailabilityTranslationPatch.cs",
        "CampfireCookAvailabilityTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["examiner_partial"],
        "ExaminerTranslationPatch.cs",
        "ExaminerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["submerged_apply"],
        "SubmergedBurrowedOwnerTranslationPatch.cs",
        "MessageQueueSemanticPipeline.cs",
        "PopupShowSemanticPipeline.cs",
        "SubmergedBurrowedOwnerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["burrowed_apply"],
        "SubmergedBurrowedOwnerTranslationPatch.cs",
        "MessageQueueSemanticPipeline.cs",
        "PopupShowSemanticPipeline.cs",
        "SubmergedBurrowedOwnerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["conversation_physical"],
        "ConversationScriptPopupTranslationPatch.cs",
        "ConversationScriptPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["conversation_mental"],
        "ConversationScriptPopupTranslationPatch.cs",
        "ConversationScriptPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["trade_vendor_examine"],
        "TradeUiVendorPopupTranslationPatch.cs",
        "TradeUiVendorPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["trade_vendor_recharge"],
        "TradeUiVendorPopupTranslationPatch.cs",
        "TradeUiVendorPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["precognition_before_die"],
        "PrecognitionTranslationPatch.cs",
        "PrecognitionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["stomach_handle"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["stomach_handle"],
        "PopupShowTranslationPatchTests.cs",
        "MessageFrameTranslatorTests.cs",
        "world-parts.ja.json",
    )


def test_policy_reuses_static_producer_owner_registry_for_issue719_family_audit() -> None:
    """Issue-719 producer audit rows reuse method-exact static producer owner evidence."""
    family_ids = {
        "monochrome_poison": ("XRL.World.Parts/MonochromePoisonOnDamage.cs::MonochromePoisonOnDamage.FireEvent(Event)"),
        "kill_missile": "XRL.World.AI.GoalHandlers/Kill.cs::Kill.TryMissileWeapon()",
        "stuck_effect": "XRL.World.Effects/Stuck.cs::Stuck.FireEvent(Event)",
        "sifrah_popup": "XRL.World/ExaminerSifrah.cs::ExaminerSifrah.Finish(SifrahSlot)",
        "trade_offer": (
            "XRL.UI/TradeUI.cs::TradeUI.PerformOffer(int,bool,GameObject,TradeScreenMode,List<TradeEntry>[],int[][])"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["monochrome_poison"],
                "XRL.World.Parts/MonochromePoisonOnDamage.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["kill_missile"],
                "XRL.World.AI.GoalHandlers/Kill.cs",
                "TryMissileWeapon",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["stuck_effect"],
                "XRL.World.Effects/Stuck.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["sifrah_popup"],
                "XRL.World/ExaminerSifrah.cs",
                "Finish",
                {"Popup": 1},
            ),
            _family(
                family_ids["trade_offer"],
                "XRL.UI/TradeUI.cs",
                "PerformOffer",
                {"Does": 1, "Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("monochrome_poison", "kill_missile"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["monochrome_poison"],
        "static_producer_closure.py",
        "MonochromeOnsetTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["stuck_effect"]]["closure_status"] == "covered_by_owner_route"
    assert entries[family_ids["sifrah_popup"]]["closure_status"] != "covered_by_owner_route"
    assert entries[family_ids["trade_offer"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["trade_offer"],
        "TradeUiPopupTranslationPatch.cs",
        "TradeUiPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_exact_ui_and_description_owner_overlays() -> None:
    """Issue-719 UI/description residuals close only exact owner-patched methods."""
    family_ids = {
        "player_status": "Qud.UI/PlayerStatusBar.cs::PlayerStatusBar.Update()",
        "ability_highlight": (
            "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.HandleHighlightLeft(FrameworkDataElement)"
        ),
        "main_menu": "Qud.UI/MainMenu.cs::MainMenu.Show()",
        "missile_area": "Qud.UI/MissileWeaponArea.cs::MissileWeaponArea.AfterRender(XRLCore,ScreenBuffer)",
        "trade_totals": "Qud.UI/TradeScreen.cs::TradeScreen.UpdateTotals()",
        "trade_menubars": "Qud.UI/TradeScreen.cs::TradeScreen.UpdateMenuBars()",
        "character_mutation": ("Qud.UI/CharacterMutationLine.cs::CharacterMutationLine.setData(FrameworkDataElement)"),
        "quests_line": "Qud.UI/QuestsLine.cs::QuestsLine.setData(FrameworkDataElement)",
        "high_scores": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.Show()",
        "buy_mutation": "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.BUY_MUTATION",
        "show_effects": "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.SHOW_EFFECTS",
        "cherubim": (
            "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.ReplaceDescription(GameObject,string,string)"
        ),
        "saves_api": "Qud.API/SavesAPI.cs::SavesAPI.ReadSaveJson(string,string)",
        "cybernetics_terminal": ("Qud.UI/CyberneticsTerminalScreen.cs::CyberneticsTerminalScreen.UpdateMenuBars()"),
        "status_filter": "Qud.UI/StatusScreensScreen.cs::StatusScreensScreen.SET_FILTER",
        "inventory_quick_drop": (
            "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.QUICK_DROP"
        ),
        "journal_insert": "Qud.UI/JournalStatusScreen.cs::JournalStatusScreen.CMD_INSERT",
        "book_prev": "Qud.UI/BookScreen.cs::BookScreen.PREV_PAGE",
        "credits": "Qud.UI/Credits.cs::Credits.UpdateMenuBars()",
        "ability_default": "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.defaultMenuOptions",
        "ability_toggle_sort": "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.TOGGLE_SORT",
        "ability_filter_items": "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.FILTER_ITEMS",
        "ability_filter_method": "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.FilterItems()",
        "main_menu_bars": "Qud.UI/MainMenu.cs::MainMenu.UpdateMenuBars()",
        "game_summary_bars": "Qud.UI/GameSummaryScreen.cs::GameSummaryScreen.UpdateMenuBars()",
        "pick_default": "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.defaultMenuOptions",
        "pick_get_item": "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.getItemMenuOptions",
        "pick_toggle_sort": "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.TOGGLE_SORT",
        "pick_take_all": "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.TAKE_ALL",
        "pick_store_item": "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.STORE_ITEM",
        "trade_default": "Qud.UI/TradeScreen.cs::TradeScreen.defaultMenuOptions",
        "trade_get_item": "Qud.UI/TradeScreen.cs::TradeScreen.getItemMenuOptions",
        "trade_set_filter": "Qud.UI/TradeScreen.cs::TradeScreen.SET_FILTER",
        "trade_toggle_sort": "Qud.UI/TradeScreen.cs::TradeScreen.TOGGLE_SORT",
        "trade_offer": "Qud.UI/TradeScreen.cs::TradeScreen.OFFER_TRADE",
        "trade_add_one": "Qud.UI/TradeScreen.cs::TradeScreen.ADD_ONE",
        "trade_remove_one": "Qud.UI/TradeScreen.cs::TradeScreen.REMOVE_ONE",
        "trade_toggle_all": "Qud.UI/TradeScreen.cs::TradeScreen.TOGGLE_ALL",
        "trade_vendor_actions": "Qud.UI/TradeScreen.cs::TradeScreen.VENDOR_ACTIONS",
        "help_bars": "Qud.UI/HelpScreen.cs::HelpScreen.UpdateMenuBars()",
        "keybind_remove": "Qud.UI/KeybindsScreen.cs::KeybindsScreen.REMOVE_BIND",
        "keybind_restore": "Qud.UI/KeybindsScreen.cs::KeybindsScreen.RESTORE_DEFAULTS",
        "ability_line_bind": "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.BIND_KEY",
        "ability_line_down": "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.MOVE_DOWN",
        "ability_line_up": "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.MOVE_UP",
        "ability_line_unbind": "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.UNBIND_KEY",
        "keybind_row": "Qud.UI/KeybindRow.cs::KeybindRow.dataRow",
        "message_log_expand": "Qud.UI/MessageLogLine.cs::MessageLogLine.categoryExpandOptions",
        "message_log_collapse": "Qud.UI/MessageLogLine.cs::MessageLogLine.categoryCollapseOptions",
        "pick_line_expand": "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.categoryExpandOptions",
        "pick_line_collapse": "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.categoryCollapseOptions",
        "pick_line_item": "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.itemOptions",
        "qud_mutations": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::QudMutationsModuleWindow.UpdateControls()"
        ),
        "skills_line": "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.setData(FrameworkDataElement)",
        "factions_screen": (
            "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.ShowScreen(GameObject,StatusScreensScreen)"
        ),
        "high_scores_bars": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.UpdateMenuBars()",
        "keybind_bars": "Qud.UI/KeybindsScreen.cs::KeybindsScreen.UpdateMenuBars()",
        "attribute_expand": "Qud.UI/CharacterAttributeLine.cs::CharacterAttributeLine.categoryExpandOptions",
        "attribute_collapse": "Qud.UI/CharacterAttributeLine.cs::CharacterAttributeLine.categoryCollapseOptions",
        "ask_number_items": "Qud.UI/AskNumberScreen.cs::AskNumberScreen.getItemMenuOptions",
        "save_management_bars": "Qud.UI/SaveManagement.cs::SaveManagement.UpdateMenuBars()",
        "effect_expand": "Qud.UI/CharacterEffectLine.cs::CharacterEffectLine.categoryExpandOptions",
        "effect_collapse": "Qud.UI/CharacterEffectLine.cs::CharacterEffectLine.categoryCollapseOptions",
        "mutation_expand": "Qud.UI/CharacterMutationLine.cs::CharacterMutationLine.categoryExpandOptions",
        "mutation_collapse": "Qud.UI/CharacterMutationLine.cs::CharacterMutationLine.categoryCollapseOptions",
        "equipment_expand": "Qud.UI/EquipmentLine.cs::EquipmentLine.categoryExpandOptions",
        "equipment_collapse": "Qud.UI/EquipmentLine.cs::EquipmentLine.categoryCollapseOptions",
        "achievement_bars": "Qud.UI/AchievementView.cs::AchievementView.UpdateMenuBars()",
        "options_default": "Qud.UI/OptionsScreen.cs::OptionsScreen.defaultMenuOptions",
        "options_collapse": "Qud.UI/OptionsScreen.cs::OptionsScreen.COLLAPSE_ALL",
        "options_expand": "Qud.UI/OptionsScreen.cs::OptionsScreen.EXPAND_ALL",
        "options_help": "Qud.UI/OptionsScreen.cs::OptionsScreen.HELP_TEXT",
        "high_scores_achievements": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.ACHIEVEMENTS",
        "high_scores_local": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.LOCAL_SCORES",
        "high_scores_global_daily": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.GLOBAL_DAILY",
        "high_scores_friends_daily": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.FRIENDS_DAILY",
        "high_scores_previous_day": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.PREVIOUS_DAY",
        "high_scores_next_day": "Qud.UI/HighScoresScreen.cs::HighScoresScreen.NEXT_DAY",
        "popup_pick": (
            "XRL.UI/Popup.cs::"
            "Popup.PickSeveral(string,string,string,string,IReadOnlyList<string>,"
            "IReadOnlyList<char>,IReadOnlyList<int>,IReadOnlyList<IRenderable>,"
            "XRL.World.GameObject,IRenderable,Action<int>,int,int,int,int,int,"
            "bool,bool,bool,bool,bool)"
        ),
    }
    inventory = _inventory(
        [
            _family(family_ids["player_status"], "Qud.UI/PlayerStatusBar.cs", "Update", {"SetText": 1}),
            _family(
                family_ids["ability_highlight"],
                "Qud.UI/AbilityManagerScreen.cs",
                "HandleHighlightLeft",
                {"SetText": 1},
            ),
            _family(family_ids["main_menu"], "Qud.UI/MainMenu.cs", "Show", {"SetText": 1}),
            _family(family_ids["missile_area"], "Qud.UI/MissileWeaponArea.cs", "AfterRender", {"SetText": 1}),
            _family(family_ids["trade_totals"], "Qud.UI/TradeScreen.cs", "UpdateTotals", {"SetText": 1}),
            _family(family_ids["trade_menubars"], "Qud.UI/TradeScreen.cs", "UpdateMenuBars", {"SetText": 1}),
            _family(
                family_ids["character_mutation"],
                "Qud.UI/CharacterMutationLine.cs",
                "setData",
                {"SetText": 1},
            ),
            _family(family_ids["quests_line"], "Qud.UI/QuestsLine.cs", "setData", {"SetText": 1}),
            _family(family_ids["high_scores"], "Qud.UI/HighScoresScreen.cs", "Show", {"SetText": 1}),
            _family(
                family_ids["buy_mutation"],
                "Qud.UI/CharacterStatusScreen.cs",
                "BUY_MUTATION",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["show_effects"],
                "Qud.UI/CharacterStatusScreen.cs",
                "SHOW_EFFECTS",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["cherubim"],
                "XRL.World.Parts/CherubimSpawner.cs",
                "ReplaceDescription",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["saves_api"], "Qud.API/SavesAPI.cs", "ReadSaveJson", {"DescriptionAssignment": 1}),
            _family(
                family_ids["cybernetics_terminal"],
                "Qud.UI/CyberneticsTerminalScreen.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["status_filter"],
                "Qud.UI/StatusScreensScreen.cs",
                "SET_FILTER",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["inventory_quick_drop"],
                "Qud.UI/InventoryAndEquipmentStatusScreen.cs",
                "QUICK_DROP",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["journal_insert"],
                "Qud.UI/JournalStatusScreen.cs",
                "CMD_INSERT",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["book_prev"], "Qud.UI/BookScreen.cs", "PREV_PAGE", {"DescriptionAssignment": 1}),
            _family(family_ids["credits"], "Qud.UI/Credits.cs", "UpdateMenuBars", {"DescriptionAssignment": 1}),
            _family(
                family_ids["ability_default"],
                "Qud.UI/AbilityManagerScreen.cs",
                "defaultMenuOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_toggle_sort"],
                "Qud.UI/AbilityManagerScreen.cs",
                "TOGGLE_SORT",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_filter_items"],
                "Qud.UI/AbilityManagerScreen.cs",
                "FILTER_ITEMS",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_filter_method"],
                "Qud.UI/AbilityManagerScreen.cs",
                "FilterItems",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["main_menu_bars"],
                "Qud.UI/MainMenu.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["game_summary_bars"],
                "Qud.UI/GameSummaryScreen.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_default"],
                "Qud.UI/PickGameObjectScreen.cs",
                "defaultMenuOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_get_item"],
                "Qud.UI/PickGameObjectScreen.cs",
                "getItemMenuOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_toggle_sort"],
                "Qud.UI/PickGameObjectScreen.cs",
                "TOGGLE_SORT",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_take_all"],
                "Qud.UI/PickGameObjectScreen.cs",
                "TAKE_ALL",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_store_item"],
                "Qud.UI/PickGameObjectScreen.cs",
                "STORE_ITEM",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["trade_default"],
                "Qud.UI/TradeScreen.cs",
                "defaultMenuOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["trade_get_item"],
                "Qud.UI/TradeScreen.cs",
                "getItemMenuOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["trade_set_filter"],
                "Qud.UI/TradeScreen.cs",
                "SET_FILTER",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["trade_toggle_sort"],
                "Qud.UI/TradeScreen.cs",
                "TOGGLE_SORT",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["trade_offer"], "Qud.UI/TradeScreen.cs", "OFFER_TRADE", {"DescriptionAssignment": 1}),
            _family(family_ids["trade_add_one"], "Qud.UI/TradeScreen.cs", "ADD_ONE", {"DescriptionAssignment": 1}),
            _family(
                family_ids["trade_remove_one"],
                "Qud.UI/TradeScreen.cs",
                "REMOVE_ONE",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["trade_toggle_all"],
                "Qud.UI/TradeScreen.cs",
                "TOGGLE_ALL",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["trade_vendor_actions"],
                "Qud.UI/TradeScreen.cs",
                "VENDOR_ACTIONS",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["help_bars"], "Qud.UI/HelpScreen.cs", "UpdateMenuBars", {"DescriptionAssignment": 1}),
            _family(
                family_ids["keybind_remove"],
                "Qud.UI/KeybindsScreen.cs",
                "REMOVE_BIND",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["keybind_restore"],
                "Qud.UI/KeybindsScreen.cs",
                "RESTORE_DEFAULTS",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_line_bind"],
                "Qud.UI/AbilityManagerLine.cs",
                "BIND_KEY",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_line_down"],
                "Qud.UI/AbilityManagerLine.cs",
                "MOVE_DOWN",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_line_up"],
                "Qud.UI/AbilityManagerLine.cs",
                "MOVE_UP",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ability_line_unbind"],
                "Qud.UI/AbilityManagerLine.cs",
                "UNBIND_KEY",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["keybind_row"], "Qud.UI/KeybindRow.cs", "dataRow", {"DescriptionAssignment": 1}),
            _family(
                family_ids["message_log_expand"],
                "Qud.UI/MessageLogLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["message_log_collapse"],
                "Qud.UI/MessageLogLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_line_expand"],
                "Qud.UI/PickGameObjectLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_line_collapse"],
                "Qud.UI/PickGameObjectLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["pick_line_item"],
                "Qud.UI/PickGameObjectLine.cs",
                "itemOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["qud_mutations"],
                "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
                "UpdateControls",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["achievement_bars"],
                "Qud.UI/AchievementView.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["options_default"],
                "Qud.UI/OptionsScreen.cs",
                "defaultMenuOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["options_collapse"],
                "Qud.UI/OptionsScreen.cs",
                "COLLAPSE_ALL",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["options_expand"],
                "Qud.UI/OptionsScreen.cs",
                "EXPAND_ALL",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["options_help"],
                "Qud.UI/OptionsScreen.cs",
                "HELP_TEXT",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_achievements"],
                "Qud.UI/HighScoresScreen.cs",
                "ACHIEVEMENTS",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_local"],
                "Qud.UI/HighScoresScreen.cs",
                "LOCAL_SCORES",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_global_daily"],
                "Qud.UI/HighScoresScreen.cs",
                "GLOBAL_DAILY",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_friends_daily"],
                "Qud.UI/HighScoresScreen.cs",
                "FRIENDS_DAILY",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_previous_day"],
                "Qud.UI/HighScoresScreen.cs",
                "PREVIOUS_DAY",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_next_day"],
                "Qud.UI/HighScoresScreen.cs",
                "NEXT_DAY",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["skills_line"], "Qud.UI/SkillsAndPowersLine.cs", "setData", {"SetText": 1}),
            _family(
                family_ids["factions_screen"],
                "Qud.UI/FactionsStatusScreen.cs",
                "ShowScreen",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["high_scores_bars"],
                "Qud.UI/HighScoresScreen.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["keybind_bars"],
                "Qud.UI/KeybindsScreen.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["attribute_expand"],
                "Qud.UI/CharacterAttributeLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["attribute_collapse"],
                "Qud.UI/CharacterAttributeLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["ask_number_items"],
                "Qud.UI/AskNumberScreen.cs",
                "getItemMenuOptions",
                {"DescriptionAssignment": 2},
            ),
            _family(
                family_ids["save_management_bars"],
                "Qud.UI/SaveManagement.cs",
                "UpdateMenuBars",
                {"DescriptionAssignment": 2},
            ),
            _family(
                family_ids["effect_expand"],
                "Qud.UI/CharacterEffectLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["effect_collapse"],
                "Qud.UI/CharacterEffectLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["mutation_expand"],
                "Qud.UI/CharacterMutationLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["mutation_collapse"],
                "Qud.UI/CharacterMutationLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["equipment_expand"],
                "Qud.UI/EquipmentLine.cs",
                "categoryExpandOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["equipment_collapse"],
                "Qud.UI/EquipmentLine.cs",
                "categoryCollapseOptions",
                {"DescriptionAssignment": 1},
            ),
            _family(family_ids["popup_pick"], "XRL.UI/Popup.cs", "PickSeveral", {"DirectTextAssignment": 1}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "player_status",
        "ability_highlight",
        "main_menu",
        "missile_area",
        "trade_totals",
        "trade_menubars",
        "character_mutation",
        "quests_line",
        "high_scores",
        "buy_mutation",
        "show_effects",
        "cherubim",
        "saves_api",
        "cybernetics_terminal",
        "status_filter",
        "inventory_quick_drop",
        "journal_insert",
        "book_prev",
        "credits",
        "ability_default",
        "ability_toggle_sort",
        "ability_filter_items",
        "ability_filter_method",
        "main_menu_bars",
        "game_summary_bars",
        "pick_default",
        "pick_get_item",
        "pick_toggle_sort",
        "pick_take_all",
        "pick_store_item",
        "trade_default",
        "trade_get_item",
        "trade_set_filter",
        "trade_toggle_sort",
        "trade_offer",
        "trade_add_one",
        "trade_remove_one",
        "trade_toggle_all",
        "trade_vendor_actions",
        "help_bars",
        "keybind_remove",
        "keybind_restore",
        "ability_line_bind",
        "ability_line_down",
        "ability_line_up",
        "ability_line_unbind",
        "keybind_row",
        "message_log_expand",
        "message_log_collapse",
        "pick_line_expand",
        "pick_line_collapse",
        "pick_line_item",
        "qud_mutations",
        "achievement_bars",
        "options_default",
        "options_collapse",
        "options_expand",
        "options_help",
        "high_scores_achievements",
        "high_scores_local",
        "high_scores_global_daily",
        "high_scores_friends_daily",
        "high_scores_previous_day",
        "high_scores_next_day",
        "skills_line",
        "factions_screen",
        "high_scores_bars",
        "keybind_bars",
        "attribute_expand",
        "attribute_collapse",
        "popup_pick",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["player_status"],
        "PlayerStatusBarProducerTranslationPatch.cs",
        "PlayerStatusBarProducerTranslationPatchResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cherubim"],
        "CherubimSpawnerReplaceDescriptionPatch.cs",
        "CherubimSpawnerReplaceDescriptionPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["buy_mutation"],
        "CharacterStatusScreenMutationDetailsPatch.cs",
        "CharacterStatusScreenMutationDetailsPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["show_effects"],
        "CharacterStatusScreenMutationDetailsPatch.cs",
        "CharacterStatusScreenMutationDetailsPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["saves_api"],
        "SavesApiReadSaveJsonTranslationPatch.cs",
        "SavesApiReadSaveJsonTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["popup_pick"],
        "PopupPickSeveralTranslationPatch.cs",
        "PopupPickSeveralTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["achievement_bars"],
        "AchievementViewTranslationPatch.cs",
        "AchievementViewTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["options_default"],
        "OptionsLocalizationPatch.cs",
        "OptionsLocalizationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["high_scores_achievements"],
        "HighScoresScreenTranslationPatch.cs",
        "HighScoresScreenTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["skills_line"],
        "SkillsAndPowersLineTranslationPatch.cs",
        "SkillsAndPowersLineTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["factions_screen"],
        "UiMenuOptionDescriptionTranslationPatch.cs",
        "UiMenuOptionDescriptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    for family_key in (
        "ask_number_items",
        "save_management_bars",
        "effect_expand",
        "effect_collapse",
        "mutation_expand",
        "mutation_collapse",
        "equipment_expand",
        "equipment_collapse",
    ):
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "UiMenuOptionDescriptionTranslationPatch.cs",
            "UiMenuOptionDescriptionTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_policy_records_issue719_exact_journal_display_text_owner_overlay() -> None:
    """Issue-719 display text residuals close exact journal entry owner routes only."""
    family_ids = {
        "base_entry": "Qud.API/IBaseJournalEntry.cs::IBaseJournalEntry.GetDisplayText()",
        "village_note": "Qud.API/JournalVillageNote.cs::JournalVillageNote.GetDisplayText()",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["base_entry"],
                "Qud.API/IBaseJournalEntry.cs",
                "GetDisplayText",
                {"DisplayTextReturn": 1},
            ),
            _family(
                family_ids["village_note"],
                "Qud.API/JournalVillageNote.cs",
                "GetDisplayText",
                {"DisplayTextReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_ids["base_entry"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["base_entry"],
        "JournalEntryDisplayTextPatch.cs",
        "JournalEntryDisplayTextPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["village_note"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["village_note"],
        "JournalEntryDisplayTextPatch.cs",
        "JournalEntryDisplayTextPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue781_elemental_pseudopod_display_name_owner_overlay() -> None:
    """Issue-719 generated display-name gaps close exact pseudopod owner routes only."""
    family_ids = {
        "elemental_jelly_setup": "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.SetupPod(GameObject)",
        "panhumor_setup": "XRL.World.Parts/Panhumor.cs::Panhumor.SetupPod(GameObject)",
        "elemental_jelly_fire_event": "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.FireEvent(Event)",
        "panhumor_fire_event": "XRL.World.Parts/Panhumor.cs::Panhumor.FireEvent(Event)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["elemental_jelly_setup"],
                "XRL.World.Parts/ElementalJelly.cs",
                "SetupPod",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                family_ids["panhumor_setup"],
                "XRL.World.Parts/Panhumor.cs",
                "SetupPod",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                family_ids["elemental_jelly_fire_event"],
                "XRL.World.Parts/ElementalJelly.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["panhumor_fire_event"],
                "XRL.World.Parts/Panhumor.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in (family_ids["elemental_jelly_setup"], family_ids["panhumor_setup"]):
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
            "ElementalPseudopodDisplayNameTranslationPatch.cs",
            "ElementalPseudopodDisplayNameTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )
    for family_id in (family_ids["elemental_jelly_fire_event"], family_ids["panhumor_fire_event"]):
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            'DidX("explode"',
            "MessageFrames/verbs.ja.json",
        )


def test_policy_records_issue781_gas_generation_description_owner_overlay() -> None:
    """Issue-719 description gap closes exact GasGeneration owner route only."""
    family_ids = {
        "gas_generation": "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.SyncFromBlueprint()",
        "gas_generation_fire_event": "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.FireEvent(Event)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["gas_generation"],
                "XRL.World.Parts.Mutation/GasGeneration.cs",
                "SyncFromBlueprint",
                {"DescriptionAssignment": 1},
            ),
            _family(
                family_ids["gas_generation_fire_event"],
                "XRL.World.Parts.Mutation/GasGeneration.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_ids["gas_generation"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["gas_generation"],
        "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
        "GasGenerationDescriptionTranslationPatch.cs",
        "GasGenerationDescriptionTranslatorTests.cs",
        "GasGenerationDescriptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["gas_generation_fire_event"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["gas_generation_fire_event"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "MessageFrames/verbs.ja.json",
    )


def test_policy_records_issue781_activated_ability_misc_provider_owner_overlays() -> None:
    """Issue-719 implementation gap closes exact activated ability provider routes only."""
    covered_families = {
        "cloneling": "XRL.World.Parts/Cloneling.cs::Cloneling.Initialize()",
        "digging": "XRL.World.Parts/Digging.cs::Digging.Initialize()",
        "engulfing": "XRL.World.Parts/Engulfing.cs::Engulfing.Initialize()",
        "fabricate": "XRL.World.Parts/FabricateFromSelf.cs::FabricateFromSelf.Initialize()",
        "recoil": "XRL.World.Parts/RecoilAbility.cs::RecoilAbility.Initialize()",
        "run": "XRL.World.Parts/Run.cs::Run.SyncAbility(bool)",
        "run_over": "XRL.World.Parts/RunOver.cs::RunOver.Initialize()",
        "trash": "XRL.World.Parts/TrashRifling.cs::TrashRifling.Initialize()",
    }
    residual_family_id = "XRL.World.Parts/Miner.cs::Miner.Initialize()"
    inventory = _inventory(
        [
            *[
                _family(
                    family_id,
                    family_id.split("::", maxsplit=1)[0],
                    family_id.split("::", maxsplit=1)[1].split(".", maxsplit=1)[1].split("(", maxsplit=1)[0],
                    {"ActivatedAbility": 1},
                )
                for family_id in covered_families.values()
            ],
            _family(
                residual_family_id,
                "XRL.World.Parts/Miner.cs",
                "Initialize",
                {"ActivatedAbility": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
            "ActivatedAbilityMiscProviderTranslationPatch.cs",
            "ActivatedAbilityNameTranslatorTests.cs",
            "ActivatedAbilityMiscProviderTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_mutation_activated_ability_name_owner_overlays() -> None:
    """Issue-719 implementation queue closes selected mutation ability registration routes."""
    covered_families = {
        "will_force": "XRL.World.Parts.Mutation/WillForce.cs::WillForce.Mutate(GameObject,int)",
        "burrowing_claws": ("XRL.World.Parts.Mutation/BurrowingClaws.cs::BurrowingClaws.Mutate(GameObject,int)"),
        "electrical_generation": (
            "XRL.World.Parts.Mutation/ElectricalGeneration.cs::ElectricalGeneration.Mutate(GameObject,int)"
        ),
        "light_manipulation": (
            "XRL.World.Parts.Mutation/LightManipulation.cs::LightManipulation.Mutate(GameObject,int)"
        ),
        "precognition": "XRL.World.Parts.Mutation/Precognition.cs::Precognition.Mutate(GameObject,int)",
        "slog_glands": "XRL.World.Parts.Mutation/SlogGlands.cs::SlogGlands.Mutate(GameObject,int)",
        "beguiling": "XRL.World.Parts.Mutation/Beguiling.cs::Beguiling.Mutate(GameObject,int)",
        "acid_slime_glands": ("XRL.World.Parts.Mutation/AcidSlimeGlands.cs::AcidSlimeGlands.Mutate(GameObject,int)"),
        "adrenal_control": ("XRL.World.Parts.Mutation/AdrenalControl2.cs::AdrenalControl2.Mutate(GameObject,int)"),
        "burgeoning": "XRL.World.Parts.Mutation/Burgeoning.cs::Burgeoning.Mutate(GameObject,int)",
        "burrowing": "XRL.World.Parts.Mutation/Burrowing.cs::Burrowing.Mutate(GameObject,int)",
        "carapace": "XRL.World.Parts.Mutation/Carapace.cs::Carapace.Mutate(GameObject,int)",
        "clairvoyance": "XRL.World.Parts.Mutation/Clairvoyance.cs::Clairvoyance.Mutate(GameObject,int)",
        "confusion": "XRL.World.Parts.Mutation/Confusion.cs::Confusion.Mutate(GameObject,int)",
        "decarbonizer": "XRL.World.Parts.Mutation/Decarbonizer.cs::Decarbonizer.Mutate(GameObject,int)",
        "defensive_chromatophores": (
            "XRL.World.Parts.Mutation/DefensiveChromatophores.cs::DefensiveChromatophores.Mutate(GameObject,int)"
        ),
        "domination": "XRL.World.Parts.Mutation/Domination.cs::Domination.Mutate(GameObject,int)",
        "electromagnetic_pulse": (
            "XRL.World.Parts.Mutation/ElectromagneticPulse.cs::ElectromagneticPulse.Mutate(GameObject,int)"
        ),
        "eros_teleportation": (
            "XRL.World.Parts.Mutation/ErosTeleportation.cs::ErosTeleportation.Mutate(GameObject,int)"
        ),
        "force_wall": "XRL.World.Parts.Mutation/ForceWall.cs::ForceWall.Mutate(GameObject,int)",
        "freeze_breath": "XRL.World.Parts.Mutation/FreezeBreath.cs::FreezeBreath.AddAbility()",
        "frost_webs": "XRL.World.Parts.Mutation/FrostWebs.cs::FrostWebs.Mutate(GameObject,int)",
        "infiltrate": "XRL.World.Parts.Mutation/Infiltrate.cs::Infiltrate.Mutate(GameObject,int)",
        "irisdual_beam": "XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.Mutate(GameObject,int)",
        "kindle": "XRL.World.Parts.Mutation/Kindle.cs::Kindle.Mutate(GameObject,int)",
        "ley_shifting": "XRL.World.Parts.Mutation/LeyShifting.cs::LeyShifting.Mutate(GameObject,int)",
        "life_drain": "XRL.World.Parts.Mutation/LifeDrain.cs::LifeDrain.Mutate(GameObject,int)",
        "liquid_spitter": "XRL.World.Parts.Mutation/LiquidSpitter.cs::LiquidSpitter.Mutate(GameObject,int)",
        "mass_mind": "XRL.World.Parts.Mutation/MassMind.cs::MassMind.Mutate(GameObject,int)",
        "mental_mirror": "XRL.World.Parts.Mutation/MentalMirror.cs::MentalMirror.Mutate(GameObject,int)",
        "metamorphed": "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.Apply(GameObject)",
        "metamorphosis": "XRL.World.Parts.Mutation/Metamorphosis.cs::Metamorphosis.Mutate(GameObject,int)",
        "phasing": "XRL.World.Parts.Mutation/Phasing.cs::Phasing.Mutate(GameObject,int)",
        "serenity": "XRL.World.Parts.Mutation/Serenity.cs::Serenity.Mutate(GameObject,int)",
        "spacetime_vortex": ("XRL.World.Parts.Mutation/SpacetimeVortex.cs::SpacetimeVortex.Mutate(GameObject,int)"),
        "spider_webs": "XRL.World.Parts.Mutation/SpiderWebs.cs::SpiderWebs.Mutate(GameObject,int)",
        "spinnerets": "XRL.World.Parts.Mutation/Spinnerets.cs::Spinnerets.Mutate(GameObject,int)",
        "sticky_tongue": "XRL.World.Parts.Mutation/StickyTongue.cs::StickyTongue.Mutate(GameObject,int)",
        "stinger": "XRL.World.Parts.Mutation/Stinger.cs::Stinger.Mutate(GameObject,int)",
        "stunning_force": ("XRL.World.Parts.Mutation/StunningForce.cs::StunningForce.Mutate(GameObject,int)"),
        "sunder_mind": "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Mutate(GameObject,int)",
        "teleport_other": "XRL.World.Parts.Mutation/TeleportOther.cs::TeleportOther.Mutate(GameObject,int)",
        "time_dilation": "XRL.World.Parts.Mutation/TimeDilation.cs::TimeDilation.Mutate(GameObject,int)",
        "waveform_worm": ("XRL.World.Parts.Mutation/WaveformWorm.cs::WaveformWorm.Mutate(GameObject,int)"),
        "cryokinesis": "XRL.World.Parts.Mutation/Cryokinesis.cs::Cryokinesis.Mutate(GameObject,int)",
        "disintegration": ("XRL.World.Parts.Mutation/Disintegration.cs::Disintegration.Mutate(GameObject,int)"),
        "fear_aura": "XRL.World.Parts.Mutation/FearAura.cs::FearAura.Mutate(GameObject,int)",
        "flaming_ray": "XRL.World.Parts.Mutation/FlamingRay.cs::FlamingRay.AddAbility()",
        "force_bubble": "XRL.World.Parts.Mutation/ForceBubble.cs::ForceBubble.Mutate(GameObject,int)",
        "freezing_ray": "XRL.World.Parts.Mutation/FreezingRay.cs::FreezingRay.AddAbility()",
        "magnetic_pulse": ("XRL.World.Parts.Mutation/MagneticPulse.cs::MagneticPulse.AddAbility(GameObject)"),
        "pyrokinesis": "XRL.World.Parts.Mutation/Pyrokinesis.cs::Pyrokinesis.Mutate(GameObject,int)",
        "repelling_force": "XRL.World.Parts.Mutation/RepellingForce.cs::RepellingForce.Mutate(GameObject,int)",
        "slime_glands": "XRL.World.Parts.Mutation/SlimeGlands.cs::SlimeGlands.Mutate(GameObject,int)",
        "telepathy": "XRL.World.Parts.Mutation/Telepathy.cs::Telepathy.Mutate(GameObject,int)",
        "teleportation": "XRL.World.Parts.Mutation/Teleportation.cs::Teleportation.Mutate(GameObject,int)",
        "belcher": "XRL.World.Parts.Mutation/Belcher.cs::Belcher.Mutate(GameObject,int)",
        "breather_base": "XRL.World.Parts.Mutation/BreatherBase.cs::BreatherBase.Mutate(GameObject,int)",
        "gas_generation": "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.Mutate(GameObject,int)",
        "delayed_line": (
            "XRL.World.Parts.Mutation/IDelayedLineMutation.cs::IDelayedLineMutation.Mutate(GameObject,int)"
        ),
        "quills": "XRL.World.Parts.Mutation/Quills.cs::Quills.Mutate(GameObject,int)",
        "temporal_fugue": "XRL.World.Parts.Mutation/TemporalFugue.cs::TemporalFugue.Mutate(GameObject,int)",
    }
    residual_family_id = "XRL.World.Parts/Miner.cs::Miner.Initialize()"
    inventory = _inventory(
        [
            *[
                _family(
                    family_id,
                    family_id.split("::", maxsplit=1)[0],
                    family_id.split("::", maxsplit=1)[1].split(".", maxsplit=1)[1].split("(", maxsplit=1)[0],
                    {"ActivatedAbility": 1},
                )
                for family_id in covered_families.values()
            ],
            _family(
                residual_family_id,
                "XRL.World.Parts/Miner.cs",
                "Initialize",
                {"ActivatedAbility": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "https://github.com/ToaruPen/coq-japanese_stable/issues/719",
            "MutationActivatedAbilityNameTranslationPatch.cs",
            "ActivatedAbilityNameTranslatorTests.cs",
            "MutationActivatedAbilityNameTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_skill_activated_ability_name_owner_overlays() -> None:
    """Issue-719 implementation queue closes selected skill ability registration routes."""
    covered_families = {
        "lay_mine": "XRL.World.Parts.Skill/Tinkering_LayMine.cs::Tinkering_LayMine.AddSkill(GameObject)",
        "empty_the_clips": ("XRL.World.Parts.Skill/Pistol_EmptyTheClips.cs::Pistol_EmptyTheClips.AddSkill(GameObject)"),
        "tinker1": "XRL.World.Parts.Skill/Tinkering_Tinker1.cs::Tinkering_Tinker1.AddSkill(GameObject)",
        "decapitate": "XRL.World.Parts.Skill/Axe_Decapitate.cs::Axe_Decapitate.AddAbility()",
        "dismember": "XRL.World.Parts.Skill/Axe_Dismember.cs::Axe_Dismember.AddSkill(GameObject)",
        "hook_and_drag": ("XRL.World.Parts.Skill/Axe_HookAndDrag.cs::Axe_HookAndDrag.AddSkill(GameObject)"),
        "harvestry": (
            "XRL.World.Parts.Skill/CookingAndGathering_Harvestry.cs::CookingAndGathering_Harvestry.AddSkill(GameObject)"
        ),
        "dueling_stance": (
            "XRL.World.Parts.Skill/LongBladesDuelingStance.cs::LongBladesDuelingStance.AddSkill(GameObject)"
        ),
        "rebuke_robot": (
            "XRL.World.Parts.Skill/Persuasion_RebukeRobot.cs::Persuasion_RebukeRobot.AddSkill(GameObject)"
        ),
        "shank": "XRL.World.Parts.Skill/ShortBlades_Shank.cs::ShortBlades_Shank.AddSkill(GameObject)",
        "berserk": "XRL.World.Parts.Skill/Axe_Berserk.cs::Axe_Berserk.AddSkill(GameObject)",
        "butchery": (
            "XRL.World.Parts.Skill/CookingAndGathering_Butchery.cs::CookingAndGathering_Butchery.AddSkill(GameObject)"
        ),
        "slam": "XRL.World.Parts.Skill/Cudgel_Slam.cs::Cudgel_Slam.AddSkill(GameObject)",
        "demolish": "XRL.World.Parts.Skill/Cudgel_SmashUp.cs::Cudgel_SmashUp.AddSkill(GameObject)",
        "meditate": "XRL.World.Parts.Skill/Discipline_Meditate.cs::Discipline_Meditate.AddSkill(GameObject)",
        "deathblow": "XRL.World.Parts.Skill/LongBladesDeathblow.cs::LongBladesDeathblow.AddSkill(GameObject)",
        "lunge": "XRL.World.Parts.Skill/LongBladesLunge.cs::LongBladesLunge.AddSkill(GameObject)",
        "swipe": "XRL.World.Parts.Skill/LongBladesSwipe.cs::LongBladesSwipe.AddSkill(GameObject)",
        "flurry": "XRL.World.Parts.Skill/Multiweapon_Flurry.cs::Multiweapon_Flurry.AddSkill(GameObject)",
        "proselytize": ("XRL.World.Parts.Skill/Persuasion_Proselytize.cs::Persuasion_Proselytize.AddSkill(GameObject)"),
        "amputate_limb": ("XRL.World.Parts.Skill/Physic_AmputateLimb.cs::Physic_AmputateLimb.AddSkill(GameObject)"),
        "akimbo": "XRL.World.Parts.Skill/Pistol_Akimbo.cs::Pistol_Akimbo.AddAbility()",
        "hobble": "XRL.World.Parts.Skill/ShortBlades_Hobble.cs::ShortBlades_Hobble.AddSkill(GameObject)",
        "rejoinder": ("XRL.World.Parts.Skill/ShortBlades_Rejoinder.cs::ShortBlades_Rejoinder.AddAbility()"),
        "make_camp": "XRL.World.Parts.Skill/Survival_Camp.cs::Survival_Camp.AddSkill(GameObject)",
        "deploy_turret": (
            "XRL.World.Parts.Skill/Tinkering_DeployTurret.cs::Tinkering_DeployTurret.AddSkill(GameObject)"
        ),
        "catapult": "XRL.World.Parts.Skill/Smash_Floor.cs::Smash_Floor.AddSkill(GameObject)",
        "howl": "XRL.World.Parts.Skill/Snapjaw_Howl.cs::Snapjaw_Howl.AddSkill(GameObject)",
        "submerge": "XRL.World.Parts.Skill/Submersion.cs::Submersion.AddSkill(GameObject)",
        "conk": "XRL.World.Parts.Skill/Cudgel_Conk.cs::Cudgel_Conk.AddSkill(GameObject)",
        "sweep": "XRL.World.Parts.Skill/HeavyWeapons_Sweep.cs::HeavyWeapons_Sweep.AddSkill(GameObject)",
        "berate": "XRL.World.Parts.Skill/Persuasion_Berate.cs::Persuasion_Berate.AddSkill(GameObject)",
        "intimidate": ("XRL.World.Parts.Skill/Persuasion_Intimidate.cs::Persuasion_Intimidate.AddSkill(GameObject)"),
        "mark_target": "XRL.World.Parts.Skill/Rifle_DrawABead.cs::Rifle_DrawABead.AddSkill(GameObject)",
        "shield_wall": "XRL.World.Parts.Skill/Shield_ShieldWall.cs::Shield_ShieldWall.AddSkill(GameObject)",
        "shield_slam": "XRL.World.Parts.Skill/Shield_Slam.cs::Shield_Slam.AddSkill(GameObject)",
        "charge": "XRL.World.Parts.Skill/Tactics_Charge.cs::Tactics_Charge.AddSkill(GameObject)",
        "death_from_above": (
            "XRL.World.Parts.Skill/Tactics_DeathFromAbove.cs::Tactics_DeathFromAbove.AddSkill(GameObject)"
        ),
        "juke": "XRL.World.Parts.Skill/Tactics_Juke.cs::Tactics_Juke.AddSkill(GameObject)",
        "jump": "XRL.World.Parts.Skill/Acrobatics_Jump.cs::Acrobatics_Jump.SyncAbility(bool)",
    }
    residual_family_id = "XRL.World.Parts/Miner.cs::Miner.Initialize()"
    inventory = _inventory(
        [
            *[
                _family(
                    family_id,
                    family_id.split("::", maxsplit=1)[0],
                    family_id.split("::", maxsplit=1)[1].split(".", maxsplit=1)[1].split("(", maxsplit=1)[0],
                    {"ActivatedAbility": 1},
                )
                for family_id in covered_families.values()
            ],
            _family(
                residual_family_id,
                "XRL.World.Parts/Miner.cs",
                "Initialize",
                {"ActivatedAbility": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "https://github.com/ToaruPen/coq-japanese_stable/issues/719",
            "SkillActivatedAbilityNameTranslationPatch.cs",
            "ActivatedAbilityNameTranslatorTests.cs",
            "SkillActivatedAbilityNameTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue781_chargen_direct_ui_owner_overlays() -> None:
    """Issue-719 implementation gap closes exact chargen direct UI routes only."""
    covered_families = {
        "attribute_selection": (
            "XRL.CharacterBuilds.Qud.UI/AttributeSelectionControl.cs::AttributeSelectionControl.Updated()"
        ),
        "subtype_window": (
            "XRL.CharacterBuilds.Qud.UI/QudSubtypeModuleWindow.cs::"
            "QudSubtypeModuleWindow.BeforeShow(EmbarkBuilderModuleWindowDescriptor)"
        ),
    }
    residual_family_id = (
        "XRL.CharacterBuilds.Qud.UI/QudCyberneticsModuleWindow.cs::"
        "QudCyberneticsModuleWindow.BeforeShow(EmbarkBuilderModuleWindowDescriptor)"
    )
    inventory = _inventory(
        [
            _family(
                covered_families["attribute_selection"],
                "XRL.CharacterBuilds.Qud.UI/AttributeSelectionControl.cs",
                "Updated",
                {"SetText": 1, "DirectTextAssignment": 2},
            ),
            _family(
                covered_families["subtype_window"],
                "XRL.CharacterBuilds.Qud.UI/QudSubtypeModuleWindow.cs",
                "BeforeShow",
                {"SetText": 1},
            ),
            _family(
                residual_family_id,
                "XRL.CharacterBuilds.Qud.UI/QudCyberneticsModuleWindow.cs",
                "BeforeShow",
                {"SetText": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
            "CharGenDirectUiTranslationPatch.cs",
            "CharGenDirectUiTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_chargen_menu_option_owner_overlays() -> None:
    """Issue-719 chargen menu/build-library owner rows close only exact implemented owners."""
    covered_families = {
        "summary_menu": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs::QudBuildSummaryModuleWindow.GetKeyMenuBar()"
        ),
        "mutations_menu": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::QudMutationsModuleWindow.GetKeyMenuBar()"
        ),
        "build_library_selections": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::QudBuildLibraryModuleWindow.GetSelections()"
        ),
        "build_library_menu": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::QudBuildLibraryModuleWindow.GetKeyMenuBar()"
        ),
        "customize_get_pets": (
            "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
            "QudCustomizeCharacterModuleWindow.GetPets()"
        ),
        "gamemode_selections": (
            "XRL.CharacterBuilds.Qud.UI/QudGamemodeModuleWindow.cs::QudGamemodeModuleWindow.GetSelections()"
        ),
        "gamemode_quickstart": (
            "XRL.CharacterBuilds.Qud.UI/QudGamemodeModuleWindow.cs::QudGamemodeModuleWindow.QUICKSTART"
        ),
        "attributes_menu": (
            "XRL.CharacterBuilds.Qud.UI/QudAttributesModuleWindow.cs::QudAttributesModuleWindow.GetKeyMenuBar()"
        ),
    }
    residual_family_id = "XRL.World.Parts.Mutation/Belcher.cs::Belcher.GetLevelText(int)"
    inventory = _inventory(
        [
            _family(
                covered_families["summary_menu"],
                "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs",
                "GetKeyMenuBar",
                {"DescriptionAssignment": 3},
            ),
            _family(
                covered_families["mutations_menu"],
                "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
                "GetKeyMenuBar",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["build_library_selections"],
                "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
                "GetSelections",
                {"DescriptionAssignment": 1},
            ),
            _family(
                covered_families["build_library_menu"],
                "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
                "GetKeyMenuBar",
                {"DescriptionAssignment": 1},
            ),
            _family(
                covered_families["customize_get_pets"],
                "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs",
                "GetPets",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["gamemode_selections"],
                "XRL.CharacterBuilds.Qud.UI/QudGamemodeModuleWindow.cs",
                "GetSelections",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["gamemode_quickstart"],
                "XRL.CharacterBuilds.Qud.UI/QudGamemodeModuleWindow.cs",
                "QUICKSTART",
                {"DescriptionAssignment": 1},
            ),
            _family(
                covered_families["attributes_menu"],
                "XRL.CharacterBuilds.Qud.UI/QudAttributesModuleWindow.cs",
                "GetKeyMenuBar",
                {"DescriptionAssignment": 1},
            ),
            _family(
                residual_family_id,
                "XRL.World.Parts.Mutation/Belcher.cs",
                "GetLevelText",
                {"EffectDescriptionReturn": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "CharGenMenuOptionOwnerTranslationPatch.cs",
            "CharGenMenuOptionOwnerTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_fabricate_ability_description_overlay() -> None:
    """Issue-719 closes FabricateFromSelf ability description only with exact getter evidence."""
    covered_family_id = "XRL.World.Parts/FabricateFromSelf.cs::FabricateFromSelf.AbilityDescription"
    residual_family_id = "XRL.World.Parts.Mutation/Belcher.cs::Belcher.GetLevelText(int)"
    inventory = _inventory(
        [
            _family(
                covered_family_id,
                "XRL.World.Parts/FabricateFromSelf.cs",
                "AbilityDescription",
                {"DescriptionAssignment": 1},
            ),
            _family(
                residual_family_id,
                "XRL.World.Parts.Mutation/Belcher.cs",
                "GetLevelText",
                {"EffectDescriptionReturn": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[covered_family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        covered_family_id,
        "FabricateFromSelfAbilityDescriptionTranslationPatch.cs",
        "FabricateFromSelfAbilityDescriptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_cybernetics_description_assignment_overlays() -> None:
    """Issue-719 closes selected cybernetics description assignments by exact owners."""
    covered_families = {
        "motorized_treads": (
            "XRL.World.Parts/CyberneticsMotorizedTreads.cs::CyberneticsMotorizedTreads.HandleEvent(ImplantedEvent)"
        ),
        "stasis_arena": (
            "XRL.World.Parts/CyberneticsStasisArena.cs::"
            "CyberneticsStasisArena.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
        ),
        "optical_multiscanner": (
            "XRL.World.Parts/CyberneticsOpticalMultiscanner.cs::"
            "CyberneticsOpticalMultiscanner.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
        ),
        "single_skillsoft": (
            "XRL.World.Parts/CyberneticsSingleSkillsoft.cs::"
            "CyberneticsSingleSkillsoft.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
        ),
        "tree_skillsoft": (
            "XRL.World.Parts/CyberneticsTreeSkillsoft.cs::"
            "CyberneticsTreeSkillsoft.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
        ),
        "social_coprocessor": (
            "XRL.World.Parts/CyberneticsSocialCoprocessor.cs::"
            "CyberneticsSocialCoprocessor.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
        ),
        "tech_indexer": (
            "XRL.World.Parts/CyberneticsTechIndexer.cs::"
            "CyberneticsTechIndexer.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
        ),
    }
    residual_family_id = (
        "XRL.World.Parts/CyberneticsStasisEntangler.cs::"
        "CyberneticsStasisEntangler.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    )
    inventory = _inventory(
        [
            _family(
                covered_families["motorized_treads"],
                "XRL.World.Parts/CyberneticsMotorizedTreads.cs",
                "HandleEvent",
                {"DescriptionAssignment": 6},
            ),
            _family(
                covered_families["stasis_arena"],
                "XRL.World.Parts/CyberneticsStasisArena.cs",
                "HandleEvent",
                {"DescriptionAssignment": 4},
            ),
            _family(
                covered_families["optical_multiscanner"],
                "XRL.World.Parts/CyberneticsOpticalMultiscanner.cs",
                "HandleEvent",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["single_skillsoft"],
                "XRL.World.Parts/CyberneticsSingleSkillsoft.cs",
                "HandleEvent",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["tree_skillsoft"],
                "XRL.World.Parts/CyberneticsTreeSkillsoft.cs",
                "HandleEvent",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["social_coprocessor"],
                "XRL.World.Parts/CyberneticsSocialCoprocessor.cs",
                "HandleEvent",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["tech_indexer"],
                "XRL.World.Parts/CyberneticsTechIndexer.cs",
                "HandleEvent",
                {"DescriptionAssignment": 2},
            ),
            _family(
                residual_family_id,
                "XRL.World.Parts/CyberneticsStasisEntangler.cs",
                "HandleEvent",
                {"DescriptionAssignment": 4},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "CyberneticsDescriptionAssignmentTranslationPatch.cs",
            "CyberneticsDescriptionAssignmentTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_misc_description_assignment_overlays() -> None:
    """Issue-719 closes only exact miscellaneous description-assignment owners."""
    covered_families = {
        "movement_capabilities": (
            "XRL.World/GetMovementCapabilitiesEvent.cs::"
            "GetMovementCapabilitiesEvent.Add(string,string,int,ActivatedAbilityEntry,bool)"
        ),
        "biocapacitor": "XRL.World.Parts/Biocapacitor.cs::Biocapacitor.Biocapacitor()",
        "foliage_camouflage": "XRL.World.Parts/FoliageCamouflage.cs::FoliageCamouflage.FoliageCamouflage()",
        "urban_camouflage": "XRL.World.Parts/UrbanCamouflage.cs::UrbanCamouflage.UrbanCamouflage()",
        "mechanimist_librarian": "XRL.World.Parts/MechanimistLibrarian.cs::MechanimistLibrarian.Initialize()",
        "wings_default_equipment": ("XRL.World.Parts.Mutation/Wings.cs::Wings.OnRegenerateDefaultEquipment(Body)"),
        "banner_short_description": "XRL.World.Parts/Banner.cs::Banner.HandleEvent(GetShortDescriptionEvent)",
    }
    residual_family_id = "XRL.World.Parts.Mutation/Belcher.cs::Belcher.GetLevelText(int)"
    inventory = _inventory(
        [
            _family(
                covered_families["movement_capabilities"],
                "XRL.World/GetMovementCapabilitiesEvent.cs",
                "Add",
                {"DescriptionAssignment": 7},
            ),
            _family(
                covered_families["biocapacitor"],
                "XRL.World.Parts/Biocapacitor.cs",
                "Biocapacitor",
                {"DescriptionAssignment": 3},
            ),
            _family(
                covered_families["foliage_camouflage"],
                "XRL.World.Parts/FoliageCamouflage.cs",
                "FoliageCamouflage",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["urban_camouflage"],
                "XRL.World.Parts/UrbanCamouflage.cs",
                "UrbanCamouflage",
                {"DescriptionAssignment": 2},
            ),
            _family(
                covered_families["mechanimist_librarian"],
                "XRL.World.Parts/MechanimistLibrarian.cs",
                "Initialize",
                {"DescriptionAssignment": 19},
            ),
            _family(
                covered_families["wings_default_equipment"],
                "XRL.World.Parts.Mutation/Wings.cs",
                "OnRegenerateDefaultEquipment",
                {"DescriptionAssignment": 7},
            ),
            _family(
                covered_families["banner_short_description"],
                "XRL.World.Parts/Banner.cs",
                "HandleEvent",
                {"DescriptionAssignment": 1},
            ),
            _family(
                residual_family_id,
                "XRL.World.Parts.Mutation/Belcher.cs",
                "GetLevelText",
                {"EffectDescriptionReturn": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "DescriptionAssignmentOwnerTranslationPatch.cs",
            "DescriptionAssignmentOwnerTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )

    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_hologram_and_ability_description_overlays() -> None:
    """Issue-719 closes decoy hologram and activated-ability descriptions only by exact owners."""
    covered_families = {
        "decoy_hologram": (
            "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.CreateHologramOf(GameObject)"
        ),
        "activated_ability_description": (
            "XRL.World/GameObject.cs::GameObject.DescribeActivatedAbility(Guid,Action<Templates.StatCollector>)"
        ),
    }
    residual_family_id = "XRL.World.Parts.Mutation/Belcher.cs::Belcher.GetLevelText(int)"
    inventory = _inventory(
        [
            _family(
                covered_families["decoy_hologram"],
                "XRL.World.Parts/DecoyHologramEmitter.cs",
                "CreateHologramOf",
                {"DescriptionAssignment": 4},
            ),
            _family(
                covered_families["activated_ability_description"],
                "XRL.World/GameObject.cs",
                "DescribeActivatedAbility",
                {"DescriptionAssignment": 2},
            ),
            _family(
                residual_family_id,
                "XRL.World.Parts.Mutation/Belcher.cs",
                "GetLevelText",
                {"EffectDescriptionReturn": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[covered_families["decoy_hologram"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        covered_families["decoy_hologram"],
        "DecoyHologramDescriptionTranslationPatch.cs",
        "DecoyHologramDescriptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[covered_families["activated_ability_description"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        covered_families["activated_ability_description"],
        "GameObjectActivatedAbilityDescriptionTranslationPatch.cs",
        "GameObjectActivatedAbilityDescriptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_urchin_belcher_owner_overlay() -> None:
    """Issue-719 closes UrchinBelcher ctor text only with exact ctor owner evidence."""
    covered_family_id = "XRL.World.Parts.Mutation/UrchinBelcher.cs::UrchinBelcher.UrchinBelcher()"
    residual_family_id = "XRL.World.Parts.Mutation/Belcher.cs::Belcher.GetLevelText(int)"
    inventory = _inventory(
        [
            _family(
                covered_family_id,
                "XRL.World.Parts.Mutation/UrchinBelcher.cs",
                "UrchinBelcher",
                {"DescriptionAssignment": 5},
            ),
            _family(
                residual_family_id,
                "XRL.World.Parts.Mutation/Belcher.cs",
                "GetLevelText",
                {"EffectDescriptionReturn": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[covered_family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        covered_family_id,
        "UrchinBelcherTranslationPatch.cs",
        "UrchinBelcherTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[residual_family_id]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_reviewed_active_effect_message_overlays() -> None:
    """Issue-719 residual active-effect messages close only exact reviewed owner routes."""
    family_ids = {
        "prone_apply": "XRL.World.Effects/Prone.cs::Prone.Apply(GameObject)",
        "prone_stand_up": "XRL.World.Effects/Prone.cs::Prone.StandUp(bool)",
        "holographic_start": ("XRL.World.Effects/HolographicBleeding.cs::HolographicBleeding.StartMessage(GameObject)"),
        "holographic_stop": ("XRL.World.Effects/HolographicBleeding.cs::HolographicBleeding.StopMessage(GameObject)"),
        "asleep_apply": "XRL.World.Effects/Asleep.cs::Asleep.Apply(GameObject)",
        "asleep_inventory": "XRL.World.Effects/Asleep.cs::Asleep.HandleEvent(InventoryActionEvent)",
        "asleep_begin_action": "XRL.World.Effects/Asleep.cs::Asleep.HandleEvent(BeginTakeActionEvent)",
        "shattered_armor": "XRL.World.Effects/ShatteredArmor.cs::ShatteredArmor.Apply(GameObject)",
        "life_drain_end_turn": "XRL.World.Effects/LifeDrain.cs::LifeDrain.HandleEvent(EndTurnEvent)",
        "rusted_apply": "XRL.World.Effects/Rusted.cs::Rusted.Apply(GameObject)",
        "life_drain_apply": "XRL.World.Effects/LifeDrain.cs::LifeDrain.Apply(GameObject)",
        "asleep_fire": "XRL.World.Effects/Asleep.cs::Asleep.FireEvent(Event)",
        "ill_fire": "XRL.World.Effects/Ill.cs::Ill.FireEvent(Event)",
        "latched_fire": "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.FireEvent(Event)",
        "stun_gas_fire": "XRL.World.Effects/StunGasStun.cs::StunGasStun.FireEvent(Event)",
        "proselytized_inventory": ("XRL.World.Effects/Proselytized.cs::Proselytized.HandleEvent(InventoryActionEvent)"),
        "rebuked_inventory": "XRL.World.Effects/Rebuked.cs::Rebuked.HandleEvent(InventoryActionEvent)",
        "shield_wall": "XRL.World.Effects/ShieldWall.cs::ShieldWall.Apply(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["prone_apply"], "XRL.World.Effects/Prone.cs", "Apply", {"MessageFrame": 1}),
            _family(family_ids["prone_stand_up"], "XRL.World.Effects/Prone.cs", "StandUp", {"MessageFrame": 1}),
            _family(
                family_ids["holographic_start"],
                "XRL.World.Effects/HolographicBleeding.cs",
                "StartMessage",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["holographic_stop"],
                "XRL.World.Effects/HolographicBleeding.cs",
                "StopMessage",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["asleep_apply"],
                "XRL.World.Effects/Asleep.cs",
                "Apply",
                {"AddPlayerMessage": 1, "Does": 1},
            ),
            _family(
                family_ids["asleep_inventory"],
                "XRL.World.Effects/Asleep.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1, "Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["asleep_begin_action"],
                "XRL.World.Effects/Asleep.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["shattered_armor"],
                "XRL.World.Effects/ShatteredArmor.cs",
                "Apply",
                {"AddPlayerMessage": 1, "Does": 1},
            ),
            _family(
                family_ids["life_drain_end_turn"],
                "XRL.World.Effects/LifeDrain.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["rusted_apply"],
                "XRL.World.Effects/Rusted.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["latched_fire"],
                "XRL.World.Effects/LatchedOnto.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["asleep_fire"],
                "XRL.World.Effects/Asleep.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["ill_fire"],
                "XRL.World.Effects/Ill.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["life_drain_apply"],
                "XRL.World.Effects/LifeDrain.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["stun_gas_fire"],
                "XRL.World.Effects/StunGasStun.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["proselytized_inventory"],
                "XRL.World.Effects/Proselytized.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["rebuked_inventory"],
                "XRL.World.Effects/Rebuked.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["shield_wall"],
                "XRL.World.Effects/ShieldWall.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "prone_apply",
        "prone_stand_up",
        "holographic_start",
        "holographic_stop",
        "asleep_apply",
        "asleep_inventory",
        "asleep_begin_action",
        "shattered_armor",
        "life_drain_end_turn",
        "rusted_apply",
        "asleep_fire",
        "ill_fire",
        "latched_fire",
        "stun_gas_fire",
        "proselytized_inventory",
        "rebuked_inventory",
        "shield_wall",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["prone_apply"],
        "docs/reports/2026-04-12-issue-354-stale-bucket-reclassification-batch-01.md",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["holographic_start"],
        "docs/reports/2026-04-11-didx-holographicbleeding-review.md",
        "MessageFrameTranslatorTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["asleep_inventory"],
        "AsleepOwnerTranslationPatch.cs",
        "AsleepMessageTranslationPatch.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["shattered_armor"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["life_drain_end_turn"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["rusted_apply"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["asleep_fire"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["ill_fire"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["latched_fire"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["stun_gas_fire"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["proselytized_inventory"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["rebuked_inventory"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["shield_wall"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )

    assert entries[family_ids["life_drain_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["life_drain_apply"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "MessageFrames/verbs.ja.json",
    )


def test_policy_records_issue719_tranche37_active_effect_fixed_message_frame_overlays() -> None:
    """Tranche 37 closes only reviewed active-effect families on fixed MessageFrame keys."""
    family_ids = {
        "sitting_stand_up": "XRL.World.Effects/Sitting.cs::Sitting.StandUp(Event)",
        "frenzied_trigger_berserk": "XRL.World.Effects/Frenzied.cs::Frenzied.TriggerBerserk()",
        "spore_cloud_poison_fire": "XRL.World.Effects/SporeCloudPoison.cs::SporeCloudPoison.FireEvent(Event)",
        "cardiac_arrest_apply": "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Apply(GameObject)",
        "lovesick_apply": "XRL.World.Effects/Lovesick.cs::Lovesick.Apply(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["sitting_stand_up"], "XRL.World.Effects/Sitting.cs", "StandUp", {"MessageFrame": 1}),
            _family(
                family_ids["frenzied_trigger_berserk"],
                "XRL.World.Effects/Frenzied.cs",
                "TriggerBerserk",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["spore_cloud_poison_fire"],
                "XRL.World.Effects/SporeCloudPoison.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["cardiac_arrest_apply"],
                "XRL.World.Effects/CardiacArrest.cs",
                "Apply",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["lovesick_apply"],
                "XRL.World.Effects/Lovesick.cs",
                "Apply",
                {"MessageFrame": 11},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "sitting_stand_up",
        "frenzied_trigger_berserk",
        "spore_cloud_poison_fire",
        "cardiac_arrest_apply",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
        )

    assert entries[family_ids["lovesick_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["lovesick_apply"],
        "ActiveEffectMessageFrameOwnerTranslationPatch.cs",
        "JournalApiAddTranslationPatchTests.cs",
        "journal-patterns.ja.json",
        'Lovesick.Apply source frame: DidXToY("fall", "in love with", Beauty)',
    )


def test_policy_records_issue719_targeted_effect_popup_and_queue_overlays() -> None:
    """Issue-719 residual effect popup/queue rows close only exact owner-patched producers."""
    family_ids = {
        "brain_brine_gain": "XRL.World.Effects/BrainBrineCurse.cs::BrainBrineCurse.GainChoice(string)",
        "brain_brine_fire": "XRL.World.Effects/BrainBrineCurse.cs::BrainBrineCurse.FireEvent(Event)",
        "cooking_reflect_unit": (
            "XRL.World.Effects/CookingDomainReflect_UnitReflectDamage.cs::"
            "CookingDomainReflect_UnitReflectDamage.FireEvent(Event)"
        ),
        "cooking_reflect_100": (
            "XRL.World.Effects/CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.cs::"
            "CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.FireEvent(Event)"
        ),
        "cooking_teleport": (
            "XRL.World.Effects/CookingDomainTeleport_UnitBlink.cs::CookingDomainTeleport_UnitBlink.FireEvent(Event)"
        ),
        "cooking_rubber_extra_2_jumps": (
            "XRL.World.Effects/CookingDomainRubber_Extra2Jumps.cs::CookingDomainRubber_Extra2Jumps.FireEvent(Event)"
        ),
        "cooking_rubber_extra_jump": (
            "XRL.World.Effects/CookingDomainRubber_ExtraJump.cs::CookingDomainRubber_ExtraJump.FireEvent(Event)"
        ),
        "cooking_no_phase": (
            "XRL.World.Effects/NoPhase_ProceduralCookingTriggeredAction_Effect.cs::"
            "NoPhase_ProceduralCookingTriggeredAction_Effect.FireEvent(Event)"
        ),
        "ironshank_onset": "XRL.World.Effects/IronshankOnset.cs::IronshankOnset.FireEvent(Event)",
        "engulfed": "XRL.World.Effects/Engulfed.cs::Engulfed.FireEvent(Event)",
        "immobilized": "XRL.World.Effects/Immobilized.cs::Immobilized.FireEvent(Event)",
        "stuck": "XRL.World.Effects/Stuck.cs::Stuck.FireEvent(Event)",
    }
    inventory = _inventory(
        [
            _family(family_ids["brain_brine_gain"], "XRL.World.Effects/BrainBrineCurse.cs", "GainChoice", {"Popup": 1}),
            _family(family_ids["brain_brine_fire"], "XRL.World.Effects/BrainBrineCurse.cs", "FireEvent", {"Popup": 1}),
            _family(
                family_ids["cooking_reflect_unit"],
                "XRL.World.Effects/CookingDomainReflect_UnitReflectDamage.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["cooking_reflect_100"],
                "XRL.World.Effects/CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["cooking_teleport"],
                "XRL.World.Effects/CookingDomainTeleport_UnitBlink.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["cooking_rubber_extra_2_jumps"],
                "XRL.World.Effects/CookingDomainRubber_Extra2Jumps.cs",
                "FireEvent",
                {"AddPlayerMessage": 5},
            ),
            _family(
                family_ids["cooking_rubber_extra_jump"],
                "XRL.World.Effects/CookingDomainRubber_ExtraJump.cs",
                "FireEvent",
                {"AddPlayerMessage": 5},
            ),
            _family(
                family_ids["cooking_no_phase"],
                "XRL.World.Effects/NoPhase_ProceduralCookingTriggeredAction_Effect.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["ironshank_onset"],
                "XRL.World.Effects/IronshankOnset.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(family_ids["engulfed"], "XRL.World.Effects/Engulfed.cs", "FireEvent", {"Popup": 1}),
            _family(
                family_ids["immobilized"],
                "XRL.World.Effects/Immobilized.cs",
                "FireEvent",
                {"AddPlayerMessage": 1, "Popup": 1},
            ),
            _family(
                family_ids["stuck"],
                "XRL.World.Effects/Stuck.cs",
                "FireEvent",
                {"AddPlayerMessage": 1, "MessageFrame": 1, "Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "brain_brine_gain",
        "cooking_reflect_unit",
        "cooking_reflect_100",
        "cooking_teleport",
        "cooking_rubber_extra_2_jumps",
        "cooking_rubber_extra_jump",
        "cooking_no_phase",
        "ironshank_onset",
        "engulfed",
        "immobilized",
        "stuck",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["brain_brine_gain"],
        "BrainBrineCurseTranslationPatch.cs",
        "BrainBrineCurseTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cooking_reflect_unit"],
        "CookingRuntimeTranslationPatch.cs",
        "CookingRuntimeTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["ironshank_onset"],
        "IronshankOnsetTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["engulfed"],
        "EffectMobilityBlockTranslationPatch.cs",
        "EffectMobilityBlockTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["brain_brine_fire"],
        "ActiveEffectPopupQueueTranslationPatch.cs",
        "ActiveEffectPopupQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_more_targeted_effect_owner_overlays() -> None:
    """Additional effect owner patches close only their exact residual families."""
    family_ids = {
        "reality_failed": "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.FailedToContest(GameObject)",
        "reality_short": (
            "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShortCircuitDevice(GameObject,GameObject,Event)"
        ),
        "reality_try": "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.TryContest(GameObject,int,int)",
        "reality_generic": (
            "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShowGenericInterdictMessage(GameObject,Event)"
        ),
        "reality_distant": (
            "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShowDistantInterdictMessage(GameObject,Event)"
        ),
        "reality_dual": (
            "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShowDualInterdictMessage(GameObject,Event)"
        ),
        "glotrot_onset": "XRL.World.Effects/GlotrotOnset.cs::GlotrotOnset.FireEvent(Event)",
        "monochrome_onset": "XRL.World.Effects/MonochromeOnset.cs::MonochromeOnset.FireEvent(Event)",
        "phased_effect_applied": "XRL.World.Effects/Phased.cs::Phased.HandleEvent(EffectAppliedEvent)",
        "phased_begin": "XRL.World.Effects/Phased.cs::Phased.HandleEvent(BeginTakeActionEvent)",
        "phased_remove": "XRL.World.Effects/Phased.cs::Phased.Remove(GameObject)",
        "latched_expired": "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.Expired()",
        "ambient": (
            "XRL.World.Effects/AmbientRealityStabilized.cs::AmbientRealityStabilized.HandleEvent(EndTurnEvent)"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["reality_failed"],
                "XRL.World.Effects/RealityStabilized.cs",
                "FailedToContest",
                {"AddPlayerMessage": 1, "Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["reality_short"],
                "XRL.World.Effects/RealityStabilized.cs",
                "ShortCircuitDevice",
                {"AddPlayerMessage": 1, "Does": 1, "Popup": 1},
            ),
            _family(
                family_ids["reality_try"],
                "XRL.World.Effects/RealityStabilized.cs",
                "TryContest",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["reality_generic"],
                "XRL.World.Effects/RealityStabilized.cs",
                "ShowGenericInterdictMessage",
                {"Popup": 1},
            ),
            _family(
                family_ids["reality_distant"],
                "XRL.World.Effects/RealityStabilized.cs",
                "ShowDistantInterdictMessage",
                {"Popup": 1},
            ),
            _family(
                family_ids["reality_dual"],
                "XRL.World.Effects/RealityStabilized.cs",
                "ShowDualInterdictMessage",
                {"Popup": 1},
            ),
            _family(
                family_ids["glotrot_onset"],
                "XRL.World.Effects/GlotrotOnset.cs",
                "FireEvent",
                {"AddPlayerMessage": 1, "Popup": 1},
            ),
            _family(
                family_ids["monochrome_onset"],
                "XRL.World.Effects/MonochromeOnset.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["phased_effect_applied"],
                "XRL.World.Effects/Phased.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["phased_begin"],
                "XRL.World.Effects/Phased.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(family_ids["phased_remove"], "XRL.World.Effects/Phased.cs", "Remove", {"AddPlayerMessage": 1}),
            _family(
                family_ids["latched_expired"],
                "XRL.World.Effects/LatchedOnto.cs",
                "Expired",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["ambient"],
                "XRL.World.Effects/AmbientRealityStabilized.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "reality_failed",
        "reality_short",
        "reality_try",
        "reality_generic",
        "reality_distant",
        "reality_dual",
        "glotrot_onset",
        "monochrome_onset",
        "phased_effect_applied",
        "phased_begin",
        "phased_remove",
        "latched_expired",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["reality_failed"],
        "RealityStabilizedEventTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["reality_generic"],
        "RealityStabilizedInterdictTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["glotrot_onset"],
        "GlotrotOnsetTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["phased_remove"],
        "PhasedTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["latched_expired"],
        "LatchedOntoExpiredTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )

    assert entries[family_ids["ambient"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["ambient"],
        "PopupShowTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )


def test_policy_records_issue719_tranche35_owner_route_overlays() -> None:
    """Issue-719 tranche 35 closes exact VehicleUnpowered, Hooked, and MechanicalWings families."""
    family_ids = {
        "vehicle_unpowered": (
            "XRL.World.Effects/VehicleUnpowered.cs::VehicleUnpowered.PreventActionMessage(GameObject)"
        ),
        "hooked": "XRL.World.Effects/Hooked.cs::Hooked.HandleEvent(CommandTakeActionEvent)",
        "mechanical_wings": "XRL.World.Parts/MechanicalWings.cs::MechanicalWings.FireEvent(Event)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["vehicle_unpowered"],
                "XRL.World.Effects/VehicleUnpowered.cs",
                "PreventActionMessage",
                {"Does": 7},
            ),
            _family(
                family_ids["hooked"],
                "XRL.World.Effects/Hooked.cs",
                "HandleEvent",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["mechanical_wings"],
                "XRL.World.Parts/MechanicalWings.cs",
                "FireEvent",
                {"Popup": 3},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in family_ids:
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["vehicle_unpowered"],
        "VehicleUnpoweredTranslationPatch.cs",
        "VehicleUnpoweredTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["hooked"],
        "HookedOwnerTranslationPatch.cs",
        "HookedOwnerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mechanical_wings"],
        "MechanicalWingsPopupTranslationPatch.cs",
        "MechanicalWingsPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_queue_only_effect_owner_overlays() -> None:
    """Queue-only active-effect owner patches close exact AddPlayerMessage residuals."""
    family_ids = {
        "cripple": "XRL.World.Effects/Cripple.cs::Cripple.Apply(GameObject)",
        "budding_apply": "XRL.World.Effects/Budding.cs::Budding.Apply(GameObject)",
        "budding_remove": "XRL.World.Effects/Budding.cs::Budding.Remove(GameObject)",
        "axons_inflated": "XRL.World.Effects/AxonsInflated.cs::AxonsInflated.Apply(GameObject)",
        "axons_deflated": "XRL.World.Effects/AxonsDeflated.cs::AxonsDeflated.Apply(GameObject)",
        "cudgel": "XRL.World.Effects/Cudgel_SmashingUp.cs::Cudgel_SmashingUp.FireEvent(Event)",
        "berserk": "XRL.World.Effects/Berserk.cs::Berserk.HandleEvent(BeginTakeActionEvent)",
        "exhausted": "XRL.World.Effects/Exhausted.cs::Exhausted.Apply(GameObject)",
        "flagging": "XRL.World.Effects/Flagging.cs::Flagging.HandleEvent(BeginTakeActionEvent)",
        "nocturnal": "XRL.World.Effects/NocturnalApexed.cs::NocturnalApexed.Apply(GameObject)",
        "paralyzed": "XRL.World.Effects/Paralyzed.cs::Paralyzed.HandleEvent(BeginTakeActionEvent)",
        "cyber_apply": (
            "XRL.World.Effects/CyberneticRejectionSyndrome.cs::CyberneticRejectionSyndrome.Apply(GameObject)"
        ),
        "cyber_remove": (
            "XRL.World.Effects/CyberneticRejectionSyndrome.cs::CyberneticRejectionSyndrome.Remove(GameObject)"
        ),
        "cyber_reduce": ("XRL.World.Effects/CyberneticRejectionSyndrome.cs::CyberneticRejectionSyndrome.Reduce(int)"),
        "emboldened_apply": "XRL.World.Effects/Emboldened.cs::Emboldened.Apply(GameObject)",
        "emboldened_remove": "XRL.World.Effects/Emboldened.cs::Emboldened.Remove(GameObject)",
        "healing_fire": "XRL.World.Effects/Healing.cs::Healing.FireEvent(Event)",
        "healing_energy": "XRL.World.Effects/Healing.cs::Healing.HandleEvent(UseEnergyEvent)",
        "stasis": "XRL.World.Effects/Stasis.cs::Stasis.HandleEvent(BeforeApplyDamageEvent)",
        "stressed_apply": "XRL.World.Effects/Stressed.cs::Stressed.Apply(GameObject)",
        "stressed_remove": "XRL.World.Effects/Stressed.cs::Stressed.Remove(GameObject)",
        "blaze_remove": "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.Remove(GameObject)",
        "healing_apply": "XRL.World.Effects/Healing.cs::Healing.Apply(GameObject)",
        "empty_the_clips": "XRL.World.Effects/EmptyTheClips.cs::EmptyTheClips.Apply(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["cripple"], "XRL.World.Effects/Cripple.cs", "Apply", {"AddPlayerMessage": 1}),
            _family(family_ids["budding_apply"], "XRL.World.Effects/Budding.cs", "Apply", {"AddPlayerMessage": 1}),
            _family(family_ids["budding_remove"], "XRL.World.Effects/Budding.cs", "Remove", {"AddPlayerMessage": 1}),
            _family(
                family_ids["axons_inflated"],
                "XRL.World.Effects/AxonsInflated.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["axons_deflated"],
                "XRL.World.Effects/AxonsDeflated.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["cudgel"],
                "XRL.World.Effects/Cudgel_SmashingUp.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(family_ids["berserk"], "XRL.World.Effects/Berserk.cs", "HandleEvent", {"AddPlayerMessage": 1}),
            _family(family_ids["exhausted"], "XRL.World.Effects/Exhausted.cs", "Apply", {"AddPlayerMessage": 1}),
            _family(family_ids["flagging"], "XRL.World.Effects/Flagging.cs", "HandleEvent", {"AddPlayerMessage": 1}),
            _family(family_ids["nocturnal"], "XRL.World.Effects/NocturnalApexed.cs", "Apply", {"AddPlayerMessage": 1}),
            _family(family_ids["paralyzed"], "XRL.World.Effects/Paralyzed.cs", "HandleEvent", {"AddPlayerMessage": 1}),
            _family(
                family_ids["cyber_apply"],
                "XRL.World.Effects/CyberneticRejectionSyndrome.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["cyber_remove"],
                "XRL.World.Effects/CyberneticRejectionSyndrome.cs",
                "Remove",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["cyber_reduce"],
                "XRL.World.Effects/CyberneticRejectionSyndrome.cs",
                "Reduce",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["emboldened_apply"],
                "XRL.World.Effects/Emboldened.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["emboldened_remove"],
                "XRL.World.Effects/Emboldened.cs",
                "Remove",
                {"AddPlayerMessage": 1},
            ),
            _family(family_ids["healing_fire"], "XRL.World.Effects/Healing.cs", "FireEvent", {"AddPlayerMessage": 1}),
            _family(
                family_ids["healing_energy"],
                "XRL.World.Effects/Healing.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(family_ids["stasis"], "XRL.World.Effects/Stasis.cs", "HandleEvent", {"AddPlayerMessage": 1}),
            _family(family_ids["stressed_apply"], "XRL.World.Effects/Stressed.cs", "Apply", {"AddPlayerMessage": 1}),
            _family(family_ids["stressed_remove"], "XRL.World.Effects/Stressed.cs", "Remove", {"AddPlayerMessage": 1}),
            _family(family_ids["blaze_remove"], "XRL.World.Effects/Blaze_Tonic.cs", "Remove", {"AddPlayerMessage": 1}),
            _family(family_ids["healing_apply"], "XRL.World.Effects/Healing.cs", "Apply", {"MessageFrame": 1}),
            _family(
                family_ids["empty_the_clips"],
                "XRL.World.Effects/EmptyTheClips.cs",
                "Apply",
                {"AddPlayerMessage": 1, "MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "cripple",
        "budding_apply",
        "budding_remove",
        "axons_inflated",
        "axons_deflated",
        "cudgel",
        "berserk",
        "exhausted",
        "flagging",
        "nocturnal",
        "paralyzed",
        "cyber_apply",
        "cyber_remove",
        "cyber_reduce",
        "emboldened_apply",
        "emboldened_remove",
        "healing_fire",
        "healing_energy",
        "stasis",
        "stressed_apply",
        "stressed_remove",
        "blaze_remove",
        "empty_the_clips",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["axons_inflated"],
        "EffectStaticMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cripple"],
        "CrippleApplyTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["cyber_apply"],
        "CyberneticRejectionSyndromeTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["blaze_remove"],
        "BlazeTonicRemoveTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["empty_the_clips"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )


def test_policy_records_issue719_remaining_exact_effect_queue_overlays() -> None:
    """The final queue bucket pass closes only exact effect owner patches."""
    family_ids = {
        "boost_apply": "XRL.World.Effects/BoostStatistic.cs::BoostStatistic.Apply(GameObject)",
        "boost_remove": "XRL.World.Effects/BoostStatistic.cs::BoostStatistic.Remove(GameObject)",
        "fungal_fire": "XRL.World.Effects/FungalSporeInfection.cs::FungalSporeInfection.FireEvent(Event)",
        "mutating_apply": "XRL.World.Effects/Mutating.cs::Mutating.Apply(GameObject)",
        "mutating_end_turn": "XRL.World.Effects/Mutating.cs::Mutating.HandleEvent(EndTurnEvent)",
        "blinking_tic": ("XRL.World.Effects/BlinkingTicSickness.cs::BlinkingTicSickness.FireEvent(Event)"),
        "meditating_remove": "XRL.World.Effects/Meditating.cs::Meditating.Remove(GameObject)",
        "irisdual_apply": "XRL.World.Effects/IrisdualCallow.cs::IrisdualCallow.Apply(GameObject)",
        "irisdual_remove": "XRL.World.Effects/IrisdualCallow.cs::IrisdualCallow.Remove(GameObject)",
        "fungal_cure_queasy_apply": ("XRL.World.Effects/FungalCureQueasy.cs::FungalCureQueasy.Apply(GameObject)"),
        "luminous_remove": "XRL.World.Effects/Luminous.cs::Luminous.Remove(GameObject)",
        "nosebleed_start": "XRL.World.Effects/Nosebleed.cs::Nosebleed.StartMessage(GameObject)",
        "nosebleed_stop": "XRL.World.Effects/Nosebleed.cs::Nosebleed.StopMessage(GameObject)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["boost_apply"],
                "XRL.World.Effects/BoostStatistic.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["boost_remove"],
                "XRL.World.Effects/BoostStatistic.cs",
                "Remove",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["fungal_fire"],
                "XRL.World.Effects/FungalSporeInfection.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["mutating_apply"],
                "XRL.World.Effects/Mutating.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["mutating_end_turn"],
                "XRL.World.Effects/Mutating.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1, "Popup": 1},
            ),
            _family(
                family_ids["blinking_tic"],
                "XRL.World.Effects/BlinkingTicSickness.cs",
                "FireEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["meditating_remove"],
                "XRL.World.Effects/Meditating.cs",
                "Remove",
                {"AddPlayerMessage": 1},
            ),
            _family(
                family_ids["irisdual_apply"],
                "XRL.World.Effects/IrisdualCallow.cs",
                "Apply",
                {"EmitMessage": 1},
            ),
            _family(
                family_ids["irisdual_remove"],
                "XRL.World.Effects/IrisdualCallow.cs",
                "Remove",
                {"EmitMessage": 2},
            ),
            _family(
                family_ids["fungal_cure_queasy_apply"],
                "XRL.World.Effects/FungalCureQueasy.cs",
                "Apply",
                {"EmitMessage": 2},
            ),
            _family(
                family_ids["luminous_remove"],
                "XRL.World.Effects/Luminous.cs",
                "Remove",
                {"EmitMessage": 2},
            ),
            _family(
                family_ids["nosebleed_start"],
                "XRL.World.Effects/Nosebleed.cs",
                "StartMessage",
                {"EmitMessage": 1},
            ),
            _family(
                family_ids["nosebleed_stop"],
                "XRL.World.Effects/Nosebleed.cs",
                "StopMessage",
                {"EmitMessage": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "boost_apply",
        "boost_remove",
        "fungal_fire",
        "mutating_apply",
        "mutating_end_turn",
        "blinking_tic",
        "meditating_remove",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["boost_apply"],
        "BoostStatisticTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["fungal_fire"],
        "FungalSporeInfectionTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["mutating_end_turn"],
        "MutatingTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["blinking_tic"],
        "BlinkingTicTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["meditating_remove"],
        "MeditatingTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )

    for family_key in ("nosebleed_start", "nosebleed_stop"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "DoesVerbFamilyTests.cs",
            "MessagePatternTranslatorTests.cs",
        )

    for family_key in ("irisdual_remove", "fungal_cure_queasy_apply", "luminous_remove"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "GameObjectEmitMessageTranslationPatch.cs",
            "MessagePatternTranslator.cs",
            "DoesVerbFamilyTests.cs",
            "ui-messagelog-leaf.ja.json",
        )

    assert entries[family_ids["irisdual_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["irisdual_apply"],
        "ActiveEffectPopupQueueTranslationPatch.cs",
        "ActiveEffectPopupQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_mutation_message_frame_fixed_route_overlays() -> None:
    """Mutation command families close only when existing fixed routes cover every visible shape."""
    family_ids = {
        "slime": "XRL.World.Parts.Mutation/SlimeGlands.cs::SlimeGlands.HandleEvent(CommandEvent)",
        "acid_slime": "XRL.World.Parts.Mutation/AcidSlimeGlands.cs::AcidSlimeGlands.FireEvent(Event)",
        "multi_horns": "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.FireEvent(Event)",
        "clairvoyance": "XRL.World.Parts.Mutation/Clairvoyance.cs::Clairvoyance.FireEvent(Event)",
        "force_wall": "XRL.World.Parts.Mutation/ForceWall.cs::ForceWall.HandleEvent(CommandEvent)",
        "slog": "XRL.World.Parts.Mutation/SlogGlands.cs::SlogGlands.FireEvent(Event)",
        "waveform": "XRL.World.Parts.Mutation/WaveformWorm.cs::WaveformWorm.FireEvent(Event)",
        "burrowing": "XRL.World.Parts.Mutation/BurrowingClaws.cs::BurrowingClaws.FireEvent(Event)",
        "stinger": "XRL.World.Parts.Mutation/Stinger.cs::Stinger.HandleEvent(CommandEvent)",
        "life_drain_apply": "XRL.World.Effects/LifeDrain.cs::LifeDrain.Apply(GameObject)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["slime"],
                "XRL.World.Parts.Mutation/SlimeGlands.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["acid_slime"],
                "XRL.World.Parts.Mutation/AcidSlimeGlands.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["multi_horns"],
                "XRL.World.Parts.Mutation/MultiHorns.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["clairvoyance"],
                "XRL.World.Parts.Mutation/Clairvoyance.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["force_wall"],
                "XRL.World.Parts.Mutation/ForceWall.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["slog"],
                "XRL.World.Parts.Mutation/SlogGlands.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["waveform"],
                "XRL.World.Parts.Mutation/WaveformWorm.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["burrowing"],
                "XRL.World.Parts.Mutation/BurrowingClaws.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["stinger"],
                "XRL.World.Parts.Mutation/Stinger.cs",
                "HandleEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                family_ids["life_drain_apply"],
                "XRL.World.Effects/LifeDrain.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "slime",
        "acid_slime",
        "multi_horns",
        "clairvoyance",
        "force_wall",
        "waveform",
        "burrowing",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "PopupTranslationPatch.cs",
            "MessageFrameTranslator.cs",
            "MessageFrameTranslatorTests.cs",
            "PopupTranslationPatchTests.cs",
            "verbs.ja.json",
            "ui-messagelog-world.ja.json",
            "ui-skillsandpowers.ja.json",
        )

    for family_key in ("slog", "stinger"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupShowTranslationPatchTests.cs",
            "MessageFrameTranslatorTests.cs",
            "verbs.ja.json",
            "ui-popup.ja.json",
        )
    assert entries[family_ids["life_drain_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["life_drain_apply"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "TryTranslateXDidY_RepositoryDictionary_TranslatesTranche39ActiveEffectFrames",
    )


def test_policy_records_issue719_stun_fixed_message_route_overlays() -> None:
    """Stun closes only exact fixed message routes with existing route tests."""
    family_ids = {
        "apply": "XRL.World.Effects/Stun.cs::Stun.Apply(GameObject)",
        "conversational": ("XRL.World.Effects/Stun.cs::Stun.HandleEvent(IsConversationallyResponsiveEvent)"),
        "begin_take_action": ("XRL.World.Effects/Stun.cs::Stun.HandleEvent(BeginTakeActionEvent)"),
        "running_apply": "XRL.World.Effects/Running.cs::Running.Apply(GameObject)",
        "scintillating_apply": ("XRL.World.Effects/Scintillating.cs::Scintillating.Apply(GameObject)"),
    }
    inventory = _inventory(
        [
            _family(family_ids["apply"], "XRL.World.Effects/Stun.cs", "Apply", {"MessageFrame": 1}),
            _family(
                family_ids["conversational"],
                "XRL.World.Effects/Stun.cs",
                "HandleEvent",
                {"Does": 1},
            ),
            _family(
                family_ids["begin_take_action"],
                "XRL.World.Effects/Stun.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["running_apply"],
                "XRL.World.Effects/Running.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["scintillating_apply"],
                "XRL.World.Effects/Scintillating.cs",
                "Apply",
                {"MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("apply", "conversational"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "DoesFragmentMarkingPatch.cs",
            "DoesVerbRouteTranslator.cs",
            "MessageFrameTranslatorTests.cs",
            "DoesVerbFamilyTests.cs",
            "DoesVerbRouteTranslatorTests.cs",
            "verbs.ja.json",
        )

    assert entries[family_ids["begin_take_action"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["begin_take_action"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    assert entries[family_ids["running_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["running_apply"],
        "EffectGeneratedMessageTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "MessageFrames/verbs.ja.json",
    )
    assert entries[family_ids["scintillating_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["scintillating_apply"],
        "ActiveEffectPopupQueueTranslationPatch.cs",
        "ActiveEffectPopupQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_generic_fixed_message_frame_route_overlays() -> None:
    """Fixed DidX message-frame producers close through the existing generic route."""
    family_ids = {
        "breakable": ("XRL.World.Parts/BreakableInMelee.cs::BreakableInMelee.HandleEvent(DefendMeleeHitEvent)"),
        "existence": "XRL.World.Parts/ExistenceSupport.cs::ExistenceSupport.Unsupported(bool)",
        "hologram_enable": "XRL.World.Parts/HologramProjector.cs::HologramProjector.Enable(bool)",
        "hologram_disable": "XRL.World.Parts/HologramProjector.cs::HologramProjector.Disable(bool)",
        "slumberling": "XRL.World.Parts/Slumberling.cs::Slumberling.CheckHibernate(int)",
        "temporary": "XRL.World.Parts/Temporary.cs::Temporary.Expire(bool)",
        "spiral_iron": "XRL.World.Parts/SpiralIron.cs::SpiralIron.PressSpiralIron(GameObject,bool)",
        "capacitor": "XRL.World.Parts/Capacitor.cs::Capacitor.HandleEvent(BeforeDeathRemovalEvent)",
        "light_dimmer": "XRL.World.Parts/LightDimmer.cs::LightDimmer.Tick(int)",
        "quantum_reverb": ("XRL.World.Parts/ModQuantumReverb.cs::ModQuantumReverb.PlaceHologram(GameObject,Cell)"),
        "fear_aura": "XRL.World.Parts.Mutation/FearAura.cs::FearAura.HandleEvent(CommandEvent)",
        "dystechnia": (
            "XRL.World.Parts.Mutation/Dystechnia.cs::Dystechnia.CauseExplosion(GameObject,GameObject,IEvent)"
        ),
        "irisdual_beam": ("XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.Refract(int,List<GameObject>,bool)"),
        "spontaneous_combustion": (
            "XRL.World.Parts.Mutation/SpontaneousCombustion.cs::SpontaneousCombustion.TurnTick(long,int)"
        ),
        "kindle": "XRL.World.Parts.Mutation/Kindle.cs::Kindle.FireEvent(Event)",
        "decarbonizer": ("XRL.World.Parts.Mutation/Decarbonizer.cs::Decarbonizer.HandleEvent(CommandEvent)"),
        "frost_webs": "XRL.World.Parts.Mutation/FrostWebs.cs::FrostWebs.FrostWeb(List<Cell>)",
        "electromagnetic_pulse": (
            "XRL.World.Parts.Mutation/ElectromagneticPulse.cs::ElectromagneticPulse.FireEvent(Event)"
        ),
        "irisdual_handle": ("XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.HandleEvent(CommandEvent)"),
        "narcolepsy": "XRL.World.Parts.Mutation/Narcolepsy.cs::Narcolepsy.HandleEvent(EndTurnEvent)",
        "mod_psionic": "XRL.World.Parts/ModPsionic.cs::ModPsionic.FireEvent(Event)",
        "repelling_force": "XRL.World.Parts.Mutation/RepellingForce.cs::RepellingForce.FireEvent(Event)",
        "electrical_generation": (
            "XRL.World.Parts.Mutation/ElectricalGeneration.cs::ElectricalGeneration.DischargeMessage(int)"
        ),
        "blast_on_hit": "XRL.World.Parts/BlastOnHit.cs::BlastOnHit.Detonate(GameObject)",
        "emp_grenade": ("XRL.World.Parts/EMPGrenade.cs::EMPGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"),
        "he_grenade": ("XRL.World.Parts/HEGrenade.cs::HEGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"),
        "thermal_grenade": (
            "XRL.World.Parts/ThermalGrenade.cs::ThermalGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"
        ),
        "phase_grenade": ("XRL.World.Parts/PhaseGrenade.cs::PhaseGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"),
        "gas_grenade": ("XRL.World.Parts/GasGrenade.cs::GasGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"),
        "gravity_grenade": (
            "XRL.World.Parts/GravityGrenade.cs::GravityGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"
        ),
        "time_dilation_grenade": (
            "XRL.World.Parts/TimeDilationGrenade.cs::TimeDilationGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"
        ),
        "flashbang_grenade": (
            "XRL.World.Parts/FlashbangGrenade.cs::FlashbangGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"
        ),
        "explode_on_hit": "XRL.World.Parts/ExplodeOnHit.cs::ExplodeOnHit.Detonate(GameObject)",
        "fusion_reactor": "XRL.World.Parts/FusionReactor.cs::FusionReactor.Explode(GameObject,bool)",
        "shatters_on_hit": "XRL.World.Parts/ShattersOnHit.cs::ShattersOnHit.Shatter(GameObject)",
        "sunder_grenade": (
            "XRL.World.Parts/SunderGrenade.cs::SunderGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"
        ),
        "deployment_grenade": (
            "XRL.World.Parts/DeploymentGrenade.cs::DeploymentGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"
        ),
        "charge_used": (
            "XRL.World/ChargeUsedEvent.cs::"
            "ChargeUsedEvent.Send(GameObject,GameObject,int,int,int,long,bool,bool,bool,bool,int)"
        ),
        "dust_urn": "XRL.World.AI.GoalHandlers/DustAnUrnGoal.cs::DustAnUrnGoal.MoveToAndDustUrn()",
        "give_treat": ("XRL.World.AI.GoalHandlers/GiveATreatToPartyLeader.cs::GiveATreatToPartyLeader.TakeAction()"),
        "delayed_line": (
            "XRL.World.Parts.Mutation/IDelayedLineMutation.cs::IDelayedLineMutation.HandleEvent(CommandEvent)"
        ),
        "crypt_alert": "XRL.World.Parts/CryptSitterBehavior.cs::CryptSitterBehavior.Alert()",
        "crypt_unalert": "XRL.World.Parts/CryptSitterBehavior.cs::CryptSitterBehavior.Unalert()",
        "crumbles_on_hit": "XRL.World.Parts/CrumblesOnHit.cs::CrumblesOnHit.FireEvent(Event)",
        "temperature_venting": "XRL.World.Parts/TemperatureVenting.cs::TemperatureVenting.Trigger()",
        "faction_rank": ("XRL.World.Parts/FactionRank.cs::FactionRank.PromoteIfBelow(string,string,bool,bool,bool)"),
        "inventory_restocker": (
            "XRL.World.Parts/GenericInventoryRestocker.cs::GenericInventoryRestocker.PerformStock(bool,bool)"
        ),
        "forcefield": "XRL.World.Parts/Forcefield.cs::Forcefield.HandleEvent(RealityStabilizeEvent)",
        "forcefield_material": (
            "XRL.World.Parts/ForcefieldMaterial.cs::ForcefieldMaterial.HandleEvent(RealityStabilizeEvent)"
        ),
        "hidden": "XRL.World.Parts/Hidden.cs::Hidden.RevealInternal(bool)",
        "lava_sludge_temperature": "XRL.World.Parts/LavaSludge.cs::LavaSludge.CheckTemperature()",
        "shrine_pray": "XRL.World.Parts/Shrine.cs::Shrine.PrayAtShrine(GameObject,bool,bool,bool)",
        "bubble_level": "XRL.World.Parts/BubbleLevel.cs::BubbleLevel.FlipBubbleLevel(GameObject,bool)",
        "ejection_slot": "XRL.World.Parts/EjectionSlot.cs::EjectionSlot.LockSeats(Cell,bool)",
        "holographic_ivory": (
            "XRL.World.Parts/HolographicIvory.cs::HolographicIvory.HandleEvent(ObjectEnteredCellEvent)"
        ),
        "pet_phylactery_despawn": "XRL.World.Parts/PetPhylactery.cs::PetPhylactery.Despawn()",
        "templar_despawn": "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.Despawn()",
        "soup_sludge": "XRL.World.Parts/SoupSludge.cs::SoupSludge.ReactWith(string,LiquidVolume)",
        "space_time_vortex": ("XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.HandleEvent(RealityStabilizeEvent)"),
        "disperse_emp": ("XRL.World.Parts/DisperseEMP.cs::DisperseEMP.HandleEvent(BeginTakeActionEvent)"),
        "clone_on_hit": "XRL.World.Parts/CloneOnHit.cs::CloneOnHit.FireEvent(Event)",
        "rocket_skates": (
            "XRL.World.Parts/RocketSkates.cs::RocketSkates.EmitFlamePlume(Cell,Cell,GameObject,bool,bool)"
        ),
        "hidden_hide": "XRL.World.Parts/Hidden.cs::Hidden.HideInternal(bool)",
        "explode_after_turns": "XRL.World.Parts/ExplodeAfterTurns.cs::ExplodeAfterTurns.Detonate(GameObject)",
        "neutron_flux_explosion": (
            "XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.CheckExplosion()"
        ),
        "rummager": "XRL.World.Parts/Rummager.cs::Rummager.CheckPickUp()",
        "stride_mason": ("XRL.World.Parts/StrideMason.cs::StrideMason.ExamineFailure(IExamineEvent,int)"),
        "troll_king": "XRL.World.Parts/TrollKing.cs::TrollKing.Spawn()",
        "warm_static": (
            "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.ApplyRandomEffectTo(GameObject,int,bool,bool)"
        ),
        "interdiction": ("XRL.World.Parts.Mutation/Interdiction.cs::Interdiction.BeginInterdiction(GameObject)"),
        "quantum_fugue": "XRL.World.Parts.Mutation/QuantumFugue.cs::QuantumFugue.Cohere(Zone)",
        "cooldown_on_step": ("XRL.World.Parts/CooldownOnStep.cs::CooldownOnStep.HandleEvent(ObjectEnteredCellEvent)"),
        "heat_self_on_freeze": "XRL.World.Parts/HeatSelfOnFreeze.cs::HeatSelfOnFreeze.FireEvent(Event)",
        "fan": "XRL.World.Parts/Fan.cs::Fan.TurnTick(long,int)",
        "neutron_flux_warning": (
            "XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.GetWarningMessage()"
        ),
        "psychic_meridian": ("XRL.World.Parts/PsychicMeridian.cs::PsychicMeridian.AfflictNosebleed(GameObject)"),
        "pluckable_polyp": "XRL.World.Parts/PluckablePolyp.cs::PluckablePolyp.Pluck(GameObject)",
        "place_turret": "XRL.World.AI.GoalHandlers/PlaceTurretGoal.cs::PlaceTurretGoal.TakeAction()",
        "hook_on_missile": "XRL.World.Parts/HookOnMissileHit.cs::HookOnMissileHit.FireEvent(Event)",
        "energy_cell_socket_remove": (
            "XRL.World.Parts/EnergyCellSocket.cs::"
            "EnergyCellSocket.AttemptRemoveCell(GameObject,InventoryActionEvent,bool)"
        ),
        "domination_dominate": "XRL.World.Parts.Mutation/Domination.cs::Domination.Dominate(MentalAttackEvent)",
        "slip_ring": "XRL.World.Parts/SlipRing.cs::SlipRing.FireEvent(Event)",
        "lava_sludge_before_die": "XRL.World.Parts/LavaSludge.cs::LavaSludge.HandleEvent(BeforeDieEvent)",
        "no_stand_up": "XRL.World.Parts/NoStandUp.cs::NoStandUp.HandleEvent(InventoryActionEvent)",
        "stairs_down_fire_event": "XRL.World.Parts/StairsDown.cs::StairsDown.FireEvent(Event)",
        "thurible": "XRL.World.Parts/Thurible.cs::Thurible.SmokeThurible(GameObject,bool)",
        "disintegration_command": (
            "XRL.World.Parts.Mutation/Disintegration.cs::Disintegration.HandleEvent(CommandEvent)"
        ),
        "metamorphed_fire_event": "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.FireEvent(Event)",
        "blink_on_damage": "XRL.World.Parts/BlinkOnDamage.cs::BlinkOnDamage.FireEvent(Event)",
        "sap_on_penetration": "XRL.World.Parts/SapOnPenetration.cs::SapOnPenetration.FireEvent(Event)",
        "feeling_on_target": "XRL.World.Parts/FeelingOnTarget.cs::FeelingOnTarget.FireEvent(Event)",
        "time_dilation_apply": (
            "XRL.World.Parts.Mutation/TimeDilation.cs::"
            "TimeDilation.ApplyField(GameObject,int,bool,int,int,IPart)"
        ),
        "chair_stand_up": "XRL.World.Parts/Chair.cs::Chair.StandUp(GameObject,IEvent,Sitting)",
        "irisdual_inflict_damage": (
            "XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.InflictDamage(GameObject,Projectile)"
        ),
        "engulfing_handoff": (
            "XRL.World.Parts/EngulfingHandOff.cs::"
            "EngulfingHandOff.AttemptHandOff(Engulfing,Engulfing,GameObject)"
        ),
        "stinger_failure": (
            "XRL.World.Parts/IStingerProperties.cs::"
            "IStingerProperties.FailureMessage(GameObject,GameObject,Effect)"
        ),
        "reflect_projectiles_check": "XRL.World.Parts/ReflectProjectiles.cs::ReflectProjectiles.Check()",
        "reflect_projectiles_fire": "XRL.World.Parts/ReflectProjectiles.cs::ReflectProjectiles.FireEvent(Event)",
        "run_over_handle": "XRL.World.Parts/RunOver.cs::RunOver.HandleEvent(CommandEvent)",
        "skybear_shroud": "XRL.World.Parts/SkybearShroud.cs::SkybearShroud.ActivateSkyshroud()",
        "banner_handle": "XRL.World.Parts/Banner.cs::Banner.HandleEvent(InventoryActionEvent)",
        "lay_mine": "XRL.World.AI.GoalHandlers/LayMineGoal.cs::LayMineGoal.TakeAction()",
        "burgeon_on_hit": "XRL.World.Parts/BurgeonOnHit.cs::BurgeonOnHit.FireEvent(Event)",
        "burn_off_gas": "XRL.World.Parts/BurnOffGas.cs::BurnOffGas.FireEvent(Event)",
        "extradimensional_hunter": (
            "XRL.World.Parts/ExtradimensionalHunterSummoner.cs::ExtradimensionalHunterSummoner.Summon(int)"
        ),
        "grabber_arm": "XRL.World.Parts/GrabberArm.cs::GrabberArm.FireEvent(Event)",
        "ironshroom": "XRL.World.Parts/Ironshroom.cs::Ironshroom.FireEvent(Event)",
        "drop_on_damage": "XRL.World.Parts/DropOnDamage.cs::DropOnDamage.FireEvent(Event)",
        "sweeper": "XRL.World.Parts/Sweeper.cs::Sweeper.FireEvent(Event)",
        "pet_phylactery": "XRL.World.Parts/PetPhylactery.cs::PetPhylactery.Spawn()",
        "templar_spawn": "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.Spawn()",
        "energy_ammo_status": (
            "XRL.World.Parts/EnergyAmmoLoader.cs::EnergyAmmoLoader.GetStatusMessage(ActivePartStatus)"
        ),
        "loot_on_step": "XRL.World.Parts/LootOnStep.cs::LootOnStep.SteppedOn(GameObject,bool)",
        "mod_liquid_cooled": ("XRL.World.Parts/ModLiquidCooled.cs::ModLiquidCooled.GetStatusMessage(ActivePartStatus)"),
        "reflect_shame": "XRL.World.Parts.Mutation/ReflectShame.cs::ReflectShame.Shame(MentalAttackEvent)",
        "eel_spawn": "XRL.World.Parts/EelSpawn.cs::EelSpawn.Reveal(GameObject)",
        "ejection_seat": "XRL.World.Parts/EjectionSeat.cs::EjectionSeat.Message(GameObject,List<GameObject>)",
        "di_thermo_beam": "XRL.World.Parts/DiThermoBeam.cs::DiThermoBeam.FlipBeam(GameObject)",
        "sticky_on_hit": "XRL.World.Parts/StickyOnHit.cs::StickyOnHit.Entangle(GameObject)",
        "tonic": "XRL.World.Parts/Tonic.cs::Tonic.HandleEvent(ExamineCriticalFailureEvent)",
        "conversation_award_xp": (
            "XRL.World.Conversations/ConversationDelegates.cs::ConversationDelegates.AwardXP(DelegateContext)"
        ),
        "spider_webs": "XRL.World.Parts/SpiderWebs.cs::SpiderWebs.HandleEvent(LeftCellEvent)",
        "if_then_else": "XRL.World.Parts/IfThenElseQuestWidget.cs::IfThenElseQuestWidget.TurnTick(long,int)",
        "cathedra_black": (
            "XRL.World.Parts/CyberneticsCathedraBlackOpal.cs::CyberneticsCathedraBlackOpal.Activate(GameObject)"
        ),
        "cathedra_white": (
            "XRL.World.Parts/CyberneticsCathedraWhiteOpal.cs::CyberneticsCathedraWhiteOpal.Activate(GameObject)"
        ),
        "examiner": "XRL.World.Parts/Examiner.cs::Examiner.MakeUnderstood(bool)",
        "mechanical_wings": "XRL.World.Parts/MechanicalWings.cs::MechanicalWings.FireEvent(Event)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["breakable"],
                "XRL.World.Parts/BreakableInMelee.cs",
                "HandleEvent",
                {"MessageFrame": 1},
            ),
            _family(family_ids["existence"], "XRL.World.Parts/ExistenceSupport.cs", "Unsupported", {"MessageFrame": 1}),
            _family(
                family_ids["hologram_enable"],
                "XRL.World.Parts/HologramProjector.cs",
                "Enable",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["hologram_disable"],
                "XRL.World.Parts/HologramProjector.cs",
                "Disable",
                {"MessageFrame": 1},
            ),
            _family(family_ids["slumberling"], "XRL.World.Parts/Slumberling.cs", "CheckHibernate", {"MessageFrame": 2}),
            _family(family_ids["temporary"], "XRL.World.Parts/Temporary.cs", "Expire", {"MessageFrame": 2}),
            _family(family_ids["spiral_iron"], "XRL.World.Parts/SpiralIron.cs", "PressSpiralIron", {"MessageFrame": 2}),
            _family(family_ids["capacitor"], "XRL.World.Parts/Capacitor.cs", "HandleEvent", {"MessageFrame": 2}),
            _family(family_ids["light_dimmer"], "XRL.World.Parts/LightDimmer.cs", "Tick", {"MessageFrame": 2}),
            _family(
                family_ids["quantum_reverb"],
                "XRL.World.Parts/ModQuantumReverb.cs",
                "PlaceHologram",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["fear_aura"],
                "XRL.World.Parts.Mutation/FearAura.cs",
                "HandleEvent",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["dystechnia"],
                "XRL.World.Parts.Mutation/Dystechnia.cs",
                "CauseExplosion",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["irisdual_beam"],
                "XRL.World.Parts.Mutation/IrisdualBeam.cs",
                "Refract",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["spontaneous_combustion"],
                "XRL.World.Parts.Mutation/SpontaneousCombustion.cs",
                "TurnTick",
                {"MessageFrame": 3},
            ),
            _family(family_ids["kindle"], "XRL.World.Parts.Mutation/Kindle.cs", "FireEvent", {"MessageFrame": 7}),
            _family(
                family_ids["decarbonizer"],
                "XRL.World.Parts.Mutation/Decarbonizer.cs",
                "HandleEvent",
                {"MessageFrame": 5},
            ),
            _family(
                family_ids["frost_webs"],
                "XRL.World.Parts.Mutation/FrostWebs.cs",
                "FrostWeb",
                {"MessageFrame": 5},
            ),
            _family(
                family_ids["electromagnetic_pulse"],
                "XRL.World.Parts.Mutation/ElectromagneticPulse.cs",
                "FireEvent",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["irisdual_handle"],
                "XRL.World.Parts.Mutation/IrisdualBeam.cs",
                "HandleEvent",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["narcolepsy"],
                "XRL.World.Parts.Mutation/Narcolepsy.cs",
                "HandleEvent",
                {"MessageFrame": 4},
            ),
            _family(family_ids["mod_psionic"], "XRL.World.Parts/ModPsionic.cs", "FireEvent", {"MessageFrame": 3}),
            _family(
                family_ids["repelling_force"],
                "XRL.World.Parts.Mutation/RepellingForce.cs",
                "FireEvent",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["electrical_generation"],
                "XRL.World.Parts.Mutation/ElectricalGeneration.cs",
                "DischargeMessage",
                {"MessageFrame": 6},
            ),
            _family(family_ids["blast_on_hit"], "XRL.World.Parts/BlastOnHit.cs", "Detonate", {"MessageFrame": 2}),
            _family(family_ids["emp_grenade"], "XRL.World.Parts/EMPGrenade.cs", "DoDetonate", {"MessageFrame": 3}),
            _family(family_ids["he_grenade"], "XRL.World.Parts/HEGrenade.cs", "DoDetonate", {"MessageFrame": 3}),
            _family(
                family_ids["thermal_grenade"],
                "XRL.World.Parts/ThermalGrenade.cs",
                "DoDetonate",
                {"MessageFrame": 18},
            ),
            _family(family_ids["phase_grenade"], "XRL.World.Parts/PhaseGrenade.cs", "DoDetonate", {"MessageFrame": 14}),
            _family(family_ids["gas_grenade"], "XRL.World.Parts/GasGrenade.cs", "DoDetonate", {"MessageFrame": 6}),
            _family(
                family_ids["gravity_grenade"],
                "XRL.World.Parts/GravityGrenade.cs",
                "DoDetonate",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["time_dilation_grenade"],
                "XRL.World.Parts/TimeDilationGrenade.cs",
                "DoDetonate",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["flashbang_grenade"],
                "XRL.World.Parts/FlashbangGrenade.cs",
                "DoDetonate",
                {"MessageFrame": 5},
            ),
            _family(family_ids["explode_on_hit"], "XRL.World.Parts/ExplodeOnHit.cs", "Detonate", {"MessageFrame": 2}),
            _family(family_ids["fusion_reactor"], "XRL.World.Parts/FusionReactor.cs", "Explode", {"MessageFrame": 2}),
            _family(family_ids["shatters_on_hit"], "XRL.World.Parts/ShattersOnHit.cs", "Shatter", {"MessageFrame": 2}),
            _family(
                family_ids["sunder_grenade"],
                "XRL.World.Parts/SunderGrenade.cs",
                "DoDetonate",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["deployment_grenade"],
                "XRL.World.Parts/DeploymentGrenade.cs",
                "DoDetonate",
                {"MessageFrame": 2},
            ),
            _family(family_ids["charge_used"], "XRL.World/ChargeUsedEvent.cs", "Send", {"MessageFrame": 2}),
            _family(
                family_ids["dust_urn"],
                "XRL.World.AI.GoalHandlers/DustAnUrnGoal.cs",
                "MoveToAndDustUrn",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["give_treat"],
                "XRL.World.AI.GoalHandlers/GiveATreatToPartyLeader.cs",
                "TakeAction",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["delayed_line"],
                "XRL.World.Parts.Mutation/IDelayedLineMutation.cs",
                "HandleEvent",
                {"MessageFrame": 2},
            ),
            _family(family_ids["crypt_alert"], "XRL.World.Parts/CryptSitterBehavior.cs", "Alert", {"MessageFrame": 2}),
            _family(
                family_ids["crypt_unalert"],
                "XRL.World.Parts/CryptSitterBehavior.cs",
                "Unalert",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["crumbles_on_hit"],
                "XRL.World.Parts/CrumblesOnHit.cs",
                "FireEvent",
                {"MessageFrame": 9},
            ),
            _family(
                family_ids["temperature_venting"],
                "XRL.World.Parts/TemperatureVenting.cs",
                "Trigger",
                {"MessageFrame": 9},
            ),
            _family(
                family_ids["faction_rank"],
                "XRL.World.Parts/FactionRank.cs",
                "PromoteIfBelow",
                {"MessageFrame": 9},
            ),
            _family(
                family_ids["inventory_restocker"],
                "XRL.World.Parts/GenericInventoryRestocker.cs",
                "PerformStock",
                {"MessageFrame": 12},
            ),
            _family(family_ids["forcefield"], "XRL.World.Parts/Forcefield.cs", "HandleEvent", {"MessageFrame": 6}),
            _family(
                family_ids["forcefield_material"],
                "XRL.World.Parts/ForcefieldMaterial.cs",
                "HandleEvent",
                {"MessageFrame": 6},
            ),
            _family(family_ids["hidden"], "XRL.World.Parts/Hidden.cs", "RevealInternal", {"MessageFrame": 6}),
            _family(
                family_ids["lava_sludge_temperature"],
                "XRL.World.Parts/LavaSludge.cs",
                "CheckTemperature",
                {"MessageFrame": 6},
            ),
            _family(family_ids["shrine_pray"], "XRL.World.Parts/Shrine.cs", "PrayAtShrine", {"MessageFrame": 7}),
            _family(
                family_ids["bubble_level"],
                "XRL.World.Parts/BubbleLevel.cs",
                "FlipBubbleLevel",
                {"MessageFrame": 4},
            ),
            _family(family_ids["ejection_slot"], "XRL.World.Parts/EjectionSlot.cs", "LockSeats", {"MessageFrame": 4}),
            _family(
                family_ids["holographic_ivory"],
                "XRL.World.Parts/HolographicIvory.cs",
                "HandleEvent",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["pet_phylactery_despawn"],
                "XRL.World.Parts/PetPhylactery.cs",
                "Despawn",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["templar_despawn"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "Despawn",
                {"MessageFrame": 4},
            ),
            _family(family_ids["soup_sludge"], "XRL.World.Parts/SoupSludge.cs", "ReactWith", {"MessageFrame": 4}),
            _family(
                family_ids["space_time_vortex"],
                "XRL.World.Parts/SpaceTimeVortex.cs",
                "HandleEvent",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["disperse_emp"],
                "XRL.World.Parts/DisperseEMP.cs",
                "HandleEvent",
                {"MessageFrame": 3},
            ),
            _family(family_ids["clone_on_hit"], "XRL.World.Parts/CloneOnHit.cs", "FireEvent", {"MessageFrame": 5}),
            _family(
                family_ids["rocket_skates"],
                "XRL.World.Parts/RocketSkates.cs",
                "EmitFlamePlume",
                {"MessageFrame": 5},
            ),
            _family(family_ids["hidden_hide"], "XRL.World.Parts/Hidden.cs", "HideInternal", {"MessageFrame": 4}),
            _family(
                family_ids["explode_after_turns"],
                "XRL.World.Parts/ExplodeAfterTurns.cs",
                "Detonate",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["neutron_flux_explosion"],
                "XRL.World.Parts/NeutronFluxContainment.cs",
                "CheckExplosion",
                {"MessageFrame": 3},
            ),
            _family(family_ids["rummager"], "XRL.World.Parts/Rummager.cs", "CheckPickUp", {"MessageFrame": 3}),
            _family(
                family_ids["stride_mason"],
                "XRL.World.Parts/StrideMason.cs",
                "ExamineFailure",
                {"MessageFrame": 3},
            ),
            _family(family_ids["troll_king"], "XRL.World.Parts/TrollKing.cs", "Spawn", {"MessageFrame": 4}),
            _family(
                family_ids["warm_static"],
                "XRL.Liquids/LiquidWarmStatic.cs",
                "ApplyRandomEffectTo",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["interdiction"],
                "XRL.World.Parts.Mutation/Interdiction.cs",
                "BeginInterdiction",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["quantum_fugue"],
                "XRL.World.Parts.Mutation/QuantumFugue.cs",
                "Cohere",
                {"MessageFrame": 1},
            ),
            _family(
                family_ids["cooldown_on_step"],
                "XRL.World.Parts/CooldownOnStep.cs",
                "HandleEvent",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["heat_self_on_freeze"],
                "XRL.World.Parts/HeatSelfOnFreeze.cs",
                "FireEvent",
                {"MessageFrame": 3},
            ),
            _family(family_ids["fan"], "XRL.World.Parts/Fan.cs", "TurnTick", {"MessageFrame": 12}),
            _family(
                family_ids["neutron_flux_warning"],
                "XRL.World.Parts/NeutronFluxContainment.cs",
                "GetWarningMessage",
                {"Does": 3},
            ),
            _family(
                family_ids["psychic_meridian"],
                "XRL.World.Parts/PsychicMeridian.cs",
                "AfflictNosebleed",
                {"MessageFrame": 3},
            ),
            _family(family_ids["pluckable_polyp"], "XRL.World.Parts/PluckablePolyp.cs", "Pluck", {"MessageFrame": 12}),
            _family(
                family_ids["place_turret"],
                "XRL.World.AI.GoalHandlers/PlaceTurretGoal.cs",
                "TakeAction",
                {"MessageFrame": 9},
            ),
            _family(
                family_ids["hook_on_missile"],
                "XRL.World.Parts/HookOnMissileHit.cs",
                "FireEvent",
                {"MessageFrame": 12},
            ),
            _family(
                family_ids["energy_cell_socket_remove"],
                "XRL.World.Parts/EnergyCellSocket.cs",
                "AttemptRemoveCell",
                {"MessageFrame": 12},
            ),
            _family(
                family_ids["domination_dominate"],
                "XRL.World.Parts.Mutation/Domination.cs",
                "Dominate",
                {"MessageFrame": 8},
            ),
            _family(family_ids["slip_ring"], "XRL.World.Parts/SlipRing.cs", "FireEvent", {"MessageFrame": 8}),
            _family(
                family_ids["lava_sludge_before_die"],
                "XRL.World.Parts/LavaSludge.cs",
                "HandleEvent",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["no_stand_up"],
                "XRL.World.Parts/NoStandUp.cs",
                "HandleEvent",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["stairs_down_fire_event"],
                "XRL.World.Parts/StairsDown.cs",
                "FireEvent",
                {"MessageFrame": 6},
            ),
            _family(family_ids["thurible"], "XRL.World.Parts/Thurible.cs", "SmokeThurible", {"MessageFrame": 6}),
            _family(
                family_ids["disintegration_command"],
                "XRL.World.Parts.Mutation/Disintegration.cs",
                "HandleEvent",
                {"MessageFrame": 5},
            ),
            _family(
                family_ids["metamorphed_fire_event"],
                "XRL.World.Parts.Mutation/Metamorphed.cs",
                "FireEvent",
                {"MessageFrame": 5},
            ),
            _family(
                family_ids["blink_on_damage"],
                "XRL.World.Parts/BlinkOnDamage.cs",
                "FireEvent",
                {"MessageFrame": 5},
            ),
            _family(
                family_ids["sap_on_penetration"],
                "XRL.World.Parts/SapOnPenetration.cs",
                "FireEvent",
                {"MessageFrame": 29},
            ),
            _family(
                family_ids["feeling_on_target"],
                "XRL.World.Parts/FeelingOnTarget.cs",
                "FireEvent",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["time_dilation_apply"],
                "XRL.World.Parts.Mutation/TimeDilation.cs",
                "ApplyField",
                {"MessageFrame": 5},
            ),
            _family(family_ids["chair_stand_up"], "XRL.World.Parts/Chair.cs", "StandUp", {"MessageFrame": 5}),
            _family(
                family_ids["irisdual_inflict_damage"],
                "XRL.World.Parts.Mutation/IrisdualBeam.cs",
                "InflictDamage",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["engulfing_handoff"],
                "XRL.World.Parts/EngulfingHandOff.cs",
                "AttemptHandOff",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["stinger_failure"],
                "XRL.World.Parts/IStingerProperties.cs",
                "FailureMessage",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["reflect_projectiles_check"],
                "XRL.World.Parts/ReflectProjectiles.cs",
                "Check",
                {"MessageFrame": 4},
            ),
            _family(
                family_ids["reflect_projectiles_fire"],
                "XRL.World.Parts/ReflectProjectiles.cs",
                "FireEvent",
                {"MessageFrame": 4},
            ),
            _family(family_ids["run_over_handle"], "XRL.World.Parts/RunOver.cs", "HandleEvent", {"MessageFrame": 4}),
            _family(
                family_ids["skybear_shroud"],
                "XRL.World.Parts/SkybearShroud.cs",
                "ActivateSkyshroud",
                {"MessageFrame": 4},
            ),
            _family(family_ids["banner_handle"], "XRL.World.Parts/Banner.cs", "HandleEvent", {"MessageFrame": 3}),
            _family(
                family_ids["lay_mine"],
                "XRL.World.AI.GoalHandlers/LayMineGoal.cs",
                "TakeAction",
                {"MessageFrame": 7},
            ),
            _family(family_ids["burgeon_on_hit"], "XRL.World.Parts/BurgeonOnHit.cs", "FireEvent", {"MessageFrame": 6}),
            _family(family_ids["burn_off_gas"], "XRL.World.Parts/BurnOffGas.cs", "FireEvent", {"MessageFrame": 6}),
            _family(
                family_ids["extradimensional_hunter"],
                "XRL.World.Parts/ExtradimensionalHunterSummoner.cs",
                "Summon",
                {"MessageFrame": 6},
            ),
            _family(family_ids["grabber_arm"], "XRL.World.Parts/GrabberArm.cs", "FireEvent", {"MessageFrame": 6}),
            _family(family_ids["ironshroom"], "XRL.World.Parts/Ironshroom.cs", "FireEvent", {"MessageFrame": 6}),
            _family(family_ids["drop_on_damage"], "XRL.World.Parts/DropOnDamage.cs", "FireEvent", {"MessageFrame": 5}),
            _family(family_ids["sweeper"], "XRL.World.Parts/Sweeper.cs", "FireEvent", {"MessageFrame": 5}),
            _family(family_ids["pet_phylactery"], "XRL.World.Parts/PetPhylactery.cs", "Spawn", {"MessageFrame": 5}),
            _family(
                family_ids["templar_spawn"],
                "XRL.World.Parts/TemplarPhylactery.cs",
                "Spawn",
                {"MessageFrame": 6},
            ),
            _family(
                family_ids["energy_ammo_status"],
                "XRL.World.Parts/EnergyAmmoLoader.cs",
                "GetStatusMessage",
                {"Does": 4},
            ),
            _family(family_ids["loot_on_step"], "XRL.World.Parts/LootOnStep.cs", "SteppedOn", {"Does": 4}),
            _family(
                family_ids["mod_liquid_cooled"],
                "XRL.World.Parts/ModLiquidCooled.cs",
                "GetStatusMessage",
                {"Does": 4},
            ),
            _family(
                family_ids["reflect_shame"],
                "XRL.World.Parts.Mutation/ReflectShame.cs",
                "Shame",
                {"MessageFrame": 4},
            ),
            _family(family_ids["eel_spawn"], "XRL.World.Parts/EelSpawn.cs", "Reveal", {"MessageFrame": 4}),
            _family(family_ids["ejection_seat"], "XRL.World.Parts/EjectionSeat.cs", "Message", {"MessageFrame": 4}),
            _family(family_ids["di_thermo_beam"], "XRL.World.Parts/DiThermoBeam.cs", "FlipBeam", {"MessageFrame": 3}),
            _family(family_ids["sticky_on_hit"], "XRL.World.Parts/StickyOnHit.cs", "Entangle", {"MessageFrame": 3}),
            _family(family_ids["tonic"], "XRL.World.Parts/Tonic.cs", "HandleEvent", {"MessageFrame": 4}),
            _family(
                family_ids["conversation_award_xp"],
                "XRL.World.Conversations/ConversationDelegates.cs",
                "AwardXP",
                {"MessageFrame": 3},
            ),
            _family(family_ids["spider_webs"], "XRL.World.Parts/SpiderWebs.cs", "HandleEvent", {"MessageFrame": 3}),
            _family(
                family_ids["if_then_else"],
                "XRL.World.Parts/IfThenElseQuestWidget.cs",
                "TurnTick",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["cathedra_black"],
                "XRL.World.Parts/CyberneticsCathedraBlackOpal.cs",
                "Activate",
                {"MessageFrame": 3},
            ),
            _family(
                family_ids["cathedra_white"],
                "XRL.World.Parts/CyberneticsCathedraWhiteOpal.cs",
                "Activate",
                {"MessageFrame": 3},
            ),
            _family(family_ids["examiner"], "XRL.World.Parts/Examiner.cs", "MakeUnderstood", {"Popup": 1}),
            _family(family_ids["mechanical_wings"], "XRL.World.Parts/MechanicalWings.cs", "FireEvent", {"Popup": 3}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "breakable",
        "existence",
        "hologram_enable",
        "hologram_disable",
        "slumberling",
        "temporary",
        "spiral_iron",
        "capacitor",
        "light_dimmer",
        "quantum_reverb",
        "fear_aura",
        "dystechnia",
        "irisdual_beam",
        "spontaneous_combustion",
        "kindle",
        "decarbonizer",
        "frost_webs",
        "electromagnetic_pulse",
        "irisdual_handle",
        "narcolepsy",
        "mod_psionic",
        "repelling_force",
        "electrical_generation",
        "blast_on_hit",
        "emp_grenade",
        "he_grenade",
        "thermal_grenade",
        "phase_grenade",
        "gas_grenade",
        "gravity_grenade",
        "time_dilation_grenade",
        "flashbang_grenade",
        "explode_on_hit",
        "fusion_reactor",
        "shatters_on_hit",
        "sunder_grenade",
        "deployment_grenade",
        "charge_used",
        "dust_urn",
        "give_treat",
        "delayed_line",
        "crypt_alert",
        "crypt_unalert",
        "crumbles_on_hit",
        "temperature_venting",
        "faction_rank",
        "inventory_restocker",
        "forcefield",
        "forcefield_material",
        "lava_sludge_temperature",
        "shrine_pray",
        "bubble_level",
        "ejection_slot",
        "holographic_ivory",
        "pet_phylactery_despawn",
        "templar_despawn",
        "soup_sludge",
        "space_time_vortex",
        "disperse_emp",
        "clone_on_hit",
        "rocket_skates",
        "hidden_hide",
        "explode_after_turns",
        "neutron_flux_explosion",
        "rummager",
        "stride_mason",
        "troll_king",
        "warm_static",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslator.cs",
            "MessageFrameTranslatorTests.cs",
            "XDidYTranslationPatchTests.cs",
            "verbs.ja.json",
        )

    assert entries[family_ids["hidden"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["hidden"],
        "HiddenRenderTranslationPatch.cs",
        "HiddenRenderTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )

    for family_key in (
        "energy_cell_socket_remove",
        "domination_dominate",
        "slip_ring",
        "lava_sludge_before_die",
        "no_stand_up",
        "stairs_down_fire_event",
        "thurible",
        "disintegration_command",
        "metamorphed_fire_event",
        "blink_on_damage",
        "sap_on_penetration",
        "interdiction",
        "quantum_fugue",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "verbs.ja.json",
        )

    for family_key in (
        "feeling_on_target",
        "time_dilation_apply",
        "chair_stand_up",
        "irisdual_inflict_damage",
        "engulfing_handoff",
        "stinger_failure",
        "reflect_projectiles_check",
        "reflect_projectiles_fire",
        "run_over_handle",
        "skybear_shroud",
        "banner_handle",
        "cooldown_on_step",
        "cathedra_black",
        "cathedra_white",
        "if_then_else",
        "psychic_meridian",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "verbs.ja.json",
        )

    assert entries[family_ids["heat_self_on_freeze"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["heat_self_on_freeze"],
        "XDidYTranslationPatchTests.cs",
        "verbs.ja.json",
    )
    assert entries[family_ids["fan"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["fan"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "verbs.ja.json",
    )
    assert entries[family_ids["neutron_flux_warning"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["neutron_flux_warning"],
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "verbs.ja.json",
    )
    assert entries[family_ids["pluckable_polyp"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["pluckable_polyp"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "verbs.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["place_turret"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "verbs.ja.json",
    )
    assert entries[family_ids["hook_on_missile"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["hook_on_missile"],
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "verbs.ja.json",
    )
    for family_key in (
        "lay_mine",
        "burgeon_on_hit",
        "burn_off_gas",
        "grabber_arm",
        "ironshroom",
        "drop_on_damage",
        "sweeper",
        "pet_phylactery",
        "templar_spawn",
        "reflect_shame",
        "eel_spawn",
        "ejection_seat",
        "di_thermo_beam",
        "sticky_on_hit",
        "tonic",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "verbs.ja.json",
        )

    assert entries[family_ids["extradimensional_hunter"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["extradimensional_hunter"],
        "MessageFrameTranslatorTests.cs",
        "verbs.ja.json",
    )
    for family_key in ("energy_ammo_status", "mod_liquid_cooled"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "DoesVerbRouteTranslator.cs",
            "DoesVerbFamilyTests.cs",
            "verbs.ja.json",
        )
    assert entries[family_ids["loot_on_step"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["loot_on_step"],
        "DeathReasonTranslationPatchTests.cs",
        "verbs.ja.json",
    )
    assert entries[family_ids["conversation_award_xp"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["conversation_award_xp"],
        "MessageFrameTranslatorTests.cs",
        "verbs.ja.json",
    )
    assert entries[family_ids["spider_webs"]]["closure_status"] != "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["examiner"],
        "ExaminerTranslationPatch.cs",
        "ExaminerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["mechanical_wings"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["mechanical_wings"],
        "MechanicalWingsPopupTranslationPatch.cs",
        "MechanicalWingsPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )

    _assert_evidence_contains(
        entries,
        family_ids["deployment_grenade"],
        "ObjectBlueprints/Items.jp.xml",
    )


def test_policy_records_issue719_fixed_emit_message_and_does_popup_overlays() -> None:
    """Non-DidX fixed producers close only through their exact existing route evidence."""
    family_ids = {
        "force_projector": (
            "XRL.World.Parts/ForceProjector.cs::ForceProjector.ForceProjectorDeactivate(GameObject,IEvent)"
        ),
        "force_projector_activate": (
            "XRL.World.Parts/ForceProjector.cs::ForceProjector.ForceProjectorActivate(GameObject,IEvent)"
        ),
        "liquid_sludge": ("XRL.Liquids/LiquidSludge.cs::LiquidSludge.ObjectGoingProne(LiquidVolume,GameObject,bool)"),
        "liquid_goo": "XRL.Liquids/LiquidGoo.cs::LiquidGoo.ObjectGoingProne(LiquidVolume,GameObject,bool)",
        "liquid_ooze": "XRL.Liquids/LiquidOoze.cs::LiquidOoze.ObjectGoingProne(LiquidVolume,GameObject,bool)",
        "refresh_cooldowns": ("XRL.World.Parts/RefreshCooldownsOnEat.cs::RefreshCooldownsOnEat.FireEvent(Event)"),
        "cherubim_lock": "XRL.World.Parts/CherubimLock.cs::CherubimLock.FireEvent(Event)",
        "magazine_load": ("XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.Load(GameObject,GameObject,bool)"),
        "magazine_reload": (
            "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.HandleEvent(CommandReloadEvent)"
        ),
        "combat_perform_melee": (
            "XRL.World.Parts/Combat.cs::Combat.PerformMeleeAttack(GameObject,GameObject,int,int,int,int,string,bool)"
        ),
        "chevron_wall": "XRL.World.Parts/ChevronWall.cs::ChevronWall.HandleEvent(AfterDieEvent)",
        "hex_crystal": "XRL.World.Parts/HexCrystal.cs::HexCrystal.HandleEvent(AfterDieEvent)",
        "equip_stat_boost": ("XRL.World.Parts/EquipStatBoost.cs::EquipStatBoost.ExamineFailure(IExamineEvent,int)"),
        "door_attempt_close": ("XRL.World.Parts/Door.cs::Door.AttemptClose(GameObject,bool,bool,bool,bool,bool,bool)"),
        "helping_hands": ("XRL.World.Parts/HelpingHands.cs::HelpingHands.ExamineFailure(IExamineEvent,int)"),
        "decoy_hologram": (
            "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.DestroyHolograms(GameObject,GameObject,bool)"
        ),
        "chimeric_body_part": (
            "XRL.World.Parts/Mutations.cs::Mutations.AddChimericBodyPart(bool,string,BodyPart)"
        ),
        "protean_gunk": (
            "XRL.Liquids/LiquidProteanGunk.cs::LiquidProteanGunk.ProcessTurns(LiquidVolume,GameObject,int)"
        ),
        "geomagnetic_disc": "XRL.World.Parts/GeomagneticDisc.cs::GeomagneticDisc.FireEvent(Event)",
        "force_bubble": "XRL.World.Parts.Mutation/ForceBubble.cs::ForceBubble.CreateBubble()",
        "decoy_hologram_place": (
            "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.PlaceHologram(Cell,GameObject,int,int)"
        ),
        "rocket_skates": "XRL.World.Parts/RocketSkates.cs::RocketSkates.HandleEvent(JumpedEvent)",
        "cursed_cell_depleted": (
            "XRL.World.Parts/CursedCellSocket.cs::CursedCellSocket.HandleEvent(CellDepletedEvent)"
        ),
        "magazine_check_load": (
            "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.HandleEvent(CheckLoadAmmoEvent)"
        ),
        "cybernetics_terminal": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AttemptInterface(GameObject,IEvent)"
        ),
        "psychic_hunters": "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CheckPsychicHunters(Zone)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["force_projector"],
                "XRL.World.Parts/ForceProjector.cs",
                "ForceProjectorDeactivate",
                {"Popup": 2},
            ),
            _family(
                family_ids["force_projector_activate"],
                "XRL.World.Parts/ForceProjector.cs",
                "ForceProjectorActivate",
                {"Does": 7},
            ),
            _family(
                family_ids["liquid_sludge"],
                "XRL.Liquids/LiquidSludge.cs",
                "ObjectGoingProne",
                {"EmitMessage": 2},
            ),
            _family(
                family_ids["liquid_goo"],
                "XRL.Liquids/LiquidGoo.cs",
                "ObjectGoingProne",
                {"EmitMessage": 7},
            ),
            _family(
                family_ids["liquid_ooze"],
                "XRL.Liquids/LiquidOoze.cs",
                "ObjectGoingProne",
                {"EmitMessage": 4},
            ),
            _family(
                family_ids["refresh_cooldowns"],
                "XRL.World.Parts/RefreshCooldownsOnEat.cs",
                "FireEvent",
                {"EmitMessage": 6},
            ),
            _family(
                family_ids["cherubim_lock"],
                "XRL.World.Parts/CherubimLock.cs",
                "FireEvent",
                {"EmitMessage": 5},
            ),
            _family(
                family_ids["magazine_load"],
                "XRL.World.Parts/MagazineAmmoLoader.cs",
                "Load",
                {"EmitMessage": 6},
            ),
            _family(
                family_ids["magazine_reload"],
                "XRL.World.Parts/MagazineAmmoLoader.cs",
                "HandleEvent",
                {"EmitMessage": 2},
            ),
            _family(
                family_ids["combat_perform_melee"],
                "XRL.World.Parts/Combat.cs",
                "PerformMeleeAttack",
                {"EmitMessage": 13},
            ),
            _family(
                family_ids["chevron_wall"],
                "XRL.World.Parts/ChevronWall.cs",
                "HandleEvent",
                {"EmitMessage": 6},
            ),
            _family(
                family_ids["hex_crystal"],
                "XRL.World.Parts/HexCrystal.cs",
                "HandleEvent",
                {"EmitMessage": 6},
            ),
            _family(
                family_ids["equip_stat_boost"],
                "XRL.World.Parts/EquipStatBoost.cs",
                "ExamineFailure",
                {"EmitMessage": 7},
            ),
            _family(
                family_ids["door_attempt_close"],
                "XRL.World.Parts/Door.cs",
                "AttemptClose",
                {"EmitMessage": 6},
            ),
            _family(
                family_ids["helping_hands"],
                "XRL.World.Parts/HelpingHands.cs",
                "ExamineFailure",
                {"EmitMessage": 5},
            ),
            _family(
                family_ids["decoy_hologram"],
                "XRL.World.Parts/DecoyHologramEmitter.cs",
                "DestroyHolograms",
                {"EmitMessage": 4},
            ),
            _family(
                family_ids["chimeric_body_part"],
                "XRL.World.Parts/Mutations.cs",
                "AddChimericBodyPart",
                {"EmitMessage": 15},
            ),
            _family(
                family_ids["protean_gunk"],
                "XRL.Liquids/LiquidProteanGunk.cs",
                "ProcessTurns",
                {"EmitMessage": 14},
            ),
            _family(
                family_ids["geomagnetic_disc"],
                "XRL.World.Parts/GeomagneticDisc.cs",
                "FireEvent",
                {"EmitMessage": 10},
            ),
            _family(
                family_ids["force_bubble"],
                "XRL.World.Parts.Mutation/ForceBubble.cs",
                "CreateBubble",
                {"EmitMessage": 3},
            ),
            _family(
                family_ids["decoy_hologram_place"],
                "XRL.World.Parts/DecoyHologramEmitter.cs",
                "PlaceHologram",
                {"EmitMessage": 3},
            ),
            _family(
                family_ids["rocket_skates"],
                "XRL.World.Parts/RocketSkates.cs",
                "HandleEvent",
                {"EmitMessage": 3},
            ),
            _family(
                family_ids["cursed_cell_depleted"],
                "XRL.World.Parts/CursedCellSocket.cs",
                "HandleEvent",
                {"EmitMessage": 1},
            ),
            _family(
                family_ids["magazine_check_load"],
                "XRL.World.Parts/MagazineAmmoLoader.cs",
                "HandleEvent",
                {"Does": 4},
            ),
            _family(
                family_ids["cybernetics_terminal"],
                "XRL.World.Parts/CyberneticsTerminal2.cs",
                "AttemptInterface",
                {"Popup": 2},
            ),
            _family(
                family_ids["psychic_hunters"],
                "XRL/PsychicHunterSystem.cs",
                "CheckPsychicHunters",
                {"EmitMessage": 2},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_ids["force_projector"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["force_projector"],
        "DoesFragmentMarkingPatch.cs",
        "PopupTranslationPatch.cs",
        "DoesVerbRouteTranslatorTests.cs",
        "PopupTranslationPatchTests.cs",
        "verbs.ja.json",
    )

    assert entries[family_ids["force_projector_activate"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["force_projector_activate"],
        "DoesFragmentMarkingPatch.cs",
        "PopupTranslationPatch.cs",
        "DoesVerbRouteTranslatorTests.cs",
        "PopupTranslationPatchTests.cs",
        "verbs.ja.json",
    )

    assert entries[family_ids["liquid_sludge"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["liquid_sludge"],
        "GameObjectEmitMessageTranslationPatch.cs",
        "MessagePatternTranslatorTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "ui-messagelog-leaf.ja.json",
    )

    for family_key in (
        "liquid_goo",
        "liquid_ooze",
        "refresh_cooldowns",
        "cherubim_lock",
        "magazine_load",
        "magazine_reload",
        "combat_perform_melee",
        "chevron_wall",
        "hex_crystal",
        "equip_stat_boost",
        "door_attempt_close",
        "helping_hands",
        "decoy_hologram",
        "chimeric_body_part",
        "protean_gunk",
        "geomagnetic_disc",
        "force_bubble",
        "decoy_hologram_place",
        "rocket_skates",
        "cursed_cell_depleted",
        "psychic_hunters",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "GameObjectEmitMessageTranslationPatch.cs",
            "MessagePatternTranslatorTests.cs",
            "DoesVerbFamilyTests.cs",
            "CombatAndLogMessageQueuePatchTests.cs",
        )

    assert entries[family_ids["magazine_check_load"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["magazine_check_load"],
        "DoesFragmentMarkingPatch.cs",
        "MessageLogPatch.cs",
        "DoesVerbFamilyTests.cs",
        "messages.ja.json",
        "verbs.ja.json",
    )

    assert entries[family_ids["cybernetics_terminal"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cybernetics_terminal"],
        "CyberneticsTerminalInterfacePopupTranslationPatch.cs",
        "PopupShowSemanticPipeline.cs",
        "CyberneticsTerminalInterfacePopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "verbs.ja.json",
    )


def test_policy_records_issue719_fixed_active_effect_message_overlays() -> None:
    """Fixed active-effect messages close through existing message frame/pattern routes."""
    family_ids = {
        "empty_the_clips": "XRL.World.Effects/EmptyTheClips.cs::EmptyTheClips.Remove(GameObject)",
        "immobilized": "XRL.World.Effects/Immobilized.cs::Immobilized.EndImmobilization()",
        "rebuked": "XRL.World.Effects/Rebuked.cs::Rebuked.Remove(GameObject)",
        "scintillating": "XRL.World.Effects/Scintillating.cs::Scintillating.Remove(GameObject)",
        "shade_oil": "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.Remove(GameObject)",
        "terrified": ("XRL.World.Effects/Terrified.cs::Terrified.Attack(MentalAttackEvent,GameObject,Cell,bool,bool)"),
        "bleeding": "XRL.World.Effects/Bleeding.cs::Bleeding.StopMessage(GameObject)",
        "beguiled": "XRL.World.Effects/Beguiled.cs::Beguiled.Remove(GameObject)",
        "ill": "XRL.World.Effects/Ill.cs::Ill.Remove(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["empty_the_clips"], "XRL.World.Effects/EmptyTheClips.cs", "Remove", {"MessageFrame": 2}),
            _family(
                family_ids["immobilized"],
                "XRL.World.Effects/Immobilized.cs",
                "EndImmobilization",
                {"MessageFrame": 2},
            ),
            _family(family_ids["rebuked"], "XRL.World.Effects/Rebuked.cs", "Remove", {"MessageFrame": 2}),
            _family(family_ids["scintillating"], "XRL.World.Effects/Scintillating.cs", "Remove", {"MessageFrame": 2}),
            _family(family_ids["shade_oil"], "XRL.World.Effects/ShadeOil_Tonic.cs", "Remove", {"MessageFrame": 2}),
            _family(family_ids["terrified"], "XRL.World.Effects/Terrified.cs", "Attack", {"MessageFrame": 2}),
            _family(
                family_ids["bleeding"],
                "XRL.World.Effects/Bleeding.cs",
                "StopMessage",
                {"EmitMessage": 1, "MessageFrame": 2},
            ),
            _family(family_ids["beguiled"], "XRL.World.Effects/Beguiled.cs", "Remove", {"MessageFrame": 2}),
            _family(family_ids["ill"], "XRL.World.Effects/Ill.cs", "Remove", {"EmitMessage": 1}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "empty_the_clips",
        "immobilized",
        "rebuked",
        "scintillating",
        "shade_oil",
        "terrified",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "verbs.ja.json",
        )

    assert entries[family_ids["bleeding"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["bleeding"],
        "GameObjectEmitMessageTranslationPatch.cs",
        "MessagePatternTranslatorTests.cs",
        "MessageFrameTranslatorTests.cs",
        "messages.ja.json",
        "verbs.ja.json",
    )

    assert entries[family_ids["beguiled"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["beguiled"],
        "XDidYTranslationPatch.cs",
        "TryTranslateXDidYToZ_RepositoryDictionary_TranslatesTranche40BeguiledLoseInterestFrame",
    )
    assert entries[family_ids["ill"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["ill"],
        "IllRemoveTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_tranche38_active_effect_fixed_message_frame_overlays() -> None:
    """Tranche 38 closes reviewed active-effect families on fixed MessageFrame keys."""
    family_ids = {
        "running_remove": "XRL.World.Effects/Running.cs::Running.Remove(GameObject)",
        "resummon_gloaming": ("XRL.World.Effects/ResummonGloaming.cs::ResummonGloaming.HandleEvent(EnteredCellEvent)"),
        "artifact_identify_all": (
            "XRL.World.Effects/CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.cs::"
            "CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.Apply(GameObject)"
        ),
        "beguiled_remove": "XRL.World.Effects/Beguiled.cs::Beguiled.Remove(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["running_remove"], "XRL.World.Effects/Running.cs", "Remove", {"MessageFrame": 1}),
            _family(
                family_ids["resummon_gloaming"],
                "XRL.World.Effects/ResummonGloaming.cs",
                "HandleEvent",
                {"MessageFrame": 2},
            ),
            _family(
                family_ids["artifact_identify_all"],
                "XRL.World.Effects/CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.cs",
                "Apply",
                {"MessageFrame": 4},
            ),
            _family(family_ids["beguiled_remove"], "XRL.World.Effects/Beguiled.cs", "Remove", {"MessageFrame": 2}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("running_remove", "resummon_gloaming", "artifact_identify_all"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
            "TryTranslateXDidY_RepositoryDictionary_TranslatesTranche38ActiveEffectFrames",
        )

    assert entries[family_ids["beguiled_remove"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["beguiled_remove"],
        'Beguiled.Remove source frame: DidXToY("lose", "interest in", Beguiler)',
        "MessageFrame key: verb=lose extra=interest in {0}",
    )
    assert (
        sum(
            entries[family_ids[family_key]]["text_construction_count"]
            for family_key in (
                "running_remove",
                "resummon_gloaming",
                "artifact_identify_all",
            )
        )
        == 7
    )
    _assert_evidence_contains(
        entries,
        family_ids["running_remove"],
        'Running.Remove source frame: DidX("stop", MessageName)',
        "MessageFrame key: verb=stop extra=power skating",
        "MessageFrame key: verb=stop extra=sprinting",
    )
    _assert_evidence_contains(
        entries,
        family_ids["resummon_gloaming"],
        'ResummonGloaming.HandleEvent source frame: XDidY(gameObject, "reappear")',
        "MessageFrame key: verb=reappear extra=<none>",
    )
    _assert_evidence_contains(
        entries,
        family_ids["artifact_identify_all"],
        (
            "CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.Apply "
            'source frame: XDidYToZ(go, "flush", "with understanding of", target)'
        ),
        "MessageFrame key: verb=flush extra=with understanding of {0}",
    )


def test_policy_records_issue719_tranche39_active_effect_fixed_message_frame_overlays() -> None:
    """Tranche 39 closes reviewed active-effect families on fixed MessageFrame keys."""
    family_ids = {
        "life_drain_apply": "XRL.World.Effects/LifeDrain.cs::LifeDrain.Apply(GameObject)",
        "life_drain_inventory": "XRL.World.Effects/LifeDrain.cs::LifeDrain.HandleEvent(InventoryActionEvent)",
        "bleeding_start": "XRL.World.Effects/Bleeding.cs::Bleeding.StartMessage(GameObject)",
        "beguiled_remove": "XRL.World.Effects/Beguiled.cs::Beguiled.Remove(GameObject)",
        "cardiac_arrest_remove": "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)",
        "immobilized_apply": "XRL.World.Effects/Immobilized.cs::Immobilized.Apply(GameObject)",
        "stuck_apply": "XRL.World.Effects/Stuck.cs::Stuck.Apply(GameObject)",
        "latched_begin_take_action": "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.HandleEvent(BeginTakeActionEvent)",
        "unreviewed_frame_01": "XRL.World.Effects/UnreviewedFrame01.cs::UnreviewedFrame01.Apply(GameObject)",
        "unreviewed_frame_02": "XRL.World.Effects/UnreviewedFrame02.cs::UnreviewedFrame02.Apply(GameObject)",
        "unreviewed_frame_03": "XRL.World.Effects/UnreviewedFrame03.cs::UnreviewedFrame03.Apply(GameObject)",
        "unreviewed_frame_04": "XRL.World.Effects/UnreviewedFrame04.cs::UnreviewedFrame04.Apply(GameObject)",
        "unreviewed_frame_05": "XRL.World.Effects/UnreviewedFrame05.cs::UnreviewedFrame05.Apply(GameObject)",
        "unreviewed_frame_06": "XRL.World.Effects/UnreviewedFrame06.cs::UnreviewedFrame06.Apply(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["life_drain_apply"], "XRL.World.Effects/LifeDrain.cs", "Apply", {"MessageFrame": 12}),
            _family(
                family_ids["life_drain_inventory"],
                "XRL.World.Effects/LifeDrain.cs",
                "HandleEvent",
                {"MessageFrame": 3},
            ),
            _family(family_ids["bleeding_start"], "XRL.World.Effects/Bleeding.cs", "StartMessage", {"MessageFrame": 6}),
            _family(family_ids["beguiled_remove"], "XRL.World.Effects/Beguiled.cs", "Remove", {"MessageFrame": 6}),
            _family(
                family_ids["cardiac_arrest_remove"],
                "XRL.World.Effects/CardiacArrest.cs",
                "Remove",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["immobilized_apply"],
                "XRL.World.Effects/Immobilized.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
            _family(family_ids["stuck_apply"], "XRL.World.Effects/Stuck.cs", "Apply", {"MessageFrame": 7}),
            _family(
                family_ids["latched_begin_take_action"],
                "XRL.World.Effects/LatchedOnto.cs",
                "HandleEvent",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["unreviewed_frame_01"],
                "XRL.World.Effects/UnreviewedFrame01.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["unreviewed_frame_02"],
                "XRL.World.Effects/UnreviewedFrame02.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["unreviewed_frame_03"],
                "XRL.World.Effects/UnreviewedFrame03.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["unreviewed_frame_04"],
                "XRL.World.Effects/UnreviewedFrame04.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["unreviewed_frame_05"],
                "XRL.World.Effects/UnreviewedFrame05.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
            _family(
                family_ids["unreviewed_frame_06"],
                "XRL.World.Effects/UnreviewedFrame06.cs",
                "Apply",
                {"MessageFrame": 7},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("life_drain_apply", "life_drain_inventory", "bleeding_start"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "XDidYTranslationPatch.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
            "TryTranslateXDidY_RepositoryDictionary_TranslatesTranche39ActiveEffectFrames",
        )

    _assert_evidence_contains(
        entries,
        family_ids["life_drain_apply"],
        'LifeDrain.Apply source frame: XDidYToZ(Drainer, "bond", "with", Object)',
        'LifeDrain.Apply source frame: XDidYToZ(Drainer, "begin", "to drain life essence from", Object)',
        "MessageFrame key: verb=bond extra=with {0}",
        "MessageFrame key: verb=begin extra=to drain life essence from {0}",
    )
    _assert_evidence_contains(
        entries,
        family_ids["life_drain_inventory"],
        (
            'LifeDrain.HandleEvent(InventoryActionEvent) source frame: XDidYToZ(E.Actor, "release", '
            'base.Object, "from " + E.Actor.its + " life drain", UsePopup: true)'
        ),
        "MessageFrame key: verb=release extra={0} from {1} life drain",
    )
    _assert_evidence_contains(
        entries,
        family_ids["bleeding_start"],
        'Bleeding.StartMessage source frame: DidX("begin", DisplayNameStripped)',
        'Bleeding.StartMessage source frame: DidX("begin", DisplayNameStripped + " from another wound")',
        "generic circulatory MessageFrame templates",
    )

    assert entries[family_ids["beguiled_remove"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["beguiled_remove"],
        'Beguiled.Remove source frame: DidXToY("lose", "interest in", Beguiler)',
        "MessageFrame key: verb=lose extra=interest in {0}",
    )

    assert entries[family_ids["cardiac_arrest_remove"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cardiac_arrest_remove"],
        "ActiveEffectMessageFrameOwnerTranslationPatch.cs",
        "PopupShowSemanticPipeline.cs",
        'CardiacArrest.Remove player popup: Popup.Show("{{G|Your heart restarts!}}")',
        'CardiacArrest.Remove nested Ill.Apply popup source: "You feel shaken and infirm."',
    )

    residual_payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert residual_payload["actionable_entries"] == 6
    assert sum(entry["text_construction_count"] for entry in residual_payload["entries"]) == 42
    assert residual_payload["bucket_counts"] == {
        "active_effect_message_frame_route_split": 6,
    }

    deferred_family_ids = {
        "journal_apply": "XRL.World.Effects/JournalTextEntry.cs::JournalTextEntry.Apply(GameObject)",
    }
    deferred_inventory = _inventory(
        [
            _family(
                deferred_family_ids["journal_apply"],
                "XRL.World.Effects/JournalTextEntry.cs",
                "Apply",
                {"JournalAPI": 1},
            ),
        ]
    )
    deferred_entries = {entry["family_id"]: entry for entry in valuable_surface_queue(deferred_inventory)}

    assert deferred_entries[deferred_family_ids["journal_apply"]]["closure_status"] != "covered_by_owner_route"


def test_policy_records_issue719_tranche40_active_effect_fixed_message_frame_overlays() -> None:
    """Tranche 40 closes Beguiled.Remove through the fixed DidXToY frame route."""
    family_id = "XRL.World.Effects/Beguiled.cs::Beguiled.Remove(GameObject)"
    residual_id = "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)"
    inventory = _inventory(
        [
            _family(family_id, "XRL.World.Effects/Beguiled.cs", "Remove", {"MessageFrame": 2}),
            _family(residual_id, "XRL.World.Effects/CardiacArrest.cs", "Remove", {"MessageFrame": 5}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_id,
        "XDidYTranslationPatch.cs",
        "MessageFrameTranslatorTests.cs",
        "MessageFrames/verbs.ja.json",
        'Beguiled.Remove source frame: DidXToY("lose", "interest in", Beguiler)',
        "MessageFrame key: verb=lose extra=interest in {0}",
        "TryTranslateXDidYToZ_RepositoryDictionary_TranslatesTranche40BeguiledLoseInterestFrame",
    )
    assert entries[residual_id]["closure_status"] == "covered_by_owner_route"

    residual_payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert residual_payload["actionable_entries"] == 0
    assert sum(entry["text_construction_count"] for entry in residual_payload["entries"]) == 0


def test_policy_records_issue719_tranche40_active_effect_conversation_responsive_overlays() -> None:
    """Tranche 40 closes Confused/Dominating conversation failures through the owner popup route."""
    family_ids = {
        "confused": "XRL.World.Effects/Confused.cs::Confused.HandleEvent(IsConversationallyResponsiveEvent)",
        "dominating": "XRL.World.Effects/Dominating.cs::Dominating.HandleEvent(IsConversationallyResponsiveEvent)",
        "cardiac_arrest_remove": "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["confused"], "XRL.World.Effects/Confused.cs", "HandleEvent", {"Does": 4}),
            _family(family_ids["dominating"], "XRL.World.Effects/Dominating.cs", "HandleEvent", {"Does": 4}),
            _family(
                family_ids["cardiac_arrest_remove"],
                "XRL.World.Effects/CardiacArrest.cs",
                "Remove",
                {"MessageFrame": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("confused", "dominating"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "ConversationScriptPopupTranslationPatch.cs",
            "ConversationScriptPopupTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "IsConversationallyResponsiveEvent",
        )

    _assert_evidence_contains(
        entries,
        family_ids["confused"],
        'Confused conversation source: Does("don\'t") + " seem to understand you."',
        'Confused mental source: Poss("mind") + " is in disarray."',
        "DoesNotUnderstand",
        "MindInDisarray",
    )
    _assert_evidence_contains(
        entries,
        family_ids["dominating"],
        'Dominating conversation source: Does("are") + " utterly unresponsive."',
        'Dominating mental source: Poss("mind") + " seems to be elsewhere."',
        "UtterlyUnresponsive",
        "MindElsewhere",
    )
    assert entries[family_ids["cardiac_arrest_remove"]]["closure_status"] == "covered_by_owner_route"

    residual_payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert residual_payload["actionable_entries"] == 0
    assert sum(entry["text_construction_count"] for entry in residual_payload["entries"]) == 0


def test_policy_records_issue719_tranche41_active_effect_didx_owner_route_overlays() -> None:
    """Tranche 41 closes exact active-effect DidX frames with owner-scoped L2/L2G evidence."""
    family_ids = {
        "immobilized_apply": "XRL.World.Effects/Immobilized.cs::Immobilized.Apply(GameObject)",
        "stuck_apply": "XRL.World.Effects/Stuck.cs::Stuck.Apply(GameObject)",
        "latched_begin_take_action": "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.HandleEvent(BeginTakeActionEvent)",
        "cardiac_arrest_remove": "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)",
    }
    inventory = _inventory(
        [
            _family(family_ids["immobilized_apply"], "XRL.World.Effects/Immobilized.cs", "Apply", {"MessageFrame": 7}),
            _family(family_ids["stuck_apply"], "XRL.World.Effects/Stuck.cs", "Apply", {"MessageFrame": 7}),
            _family(
                family_ids["latched_begin_take_action"],
                "XRL.World.Effects/LatchedOnto.cs",
                "HandleEvent",
                {"MessageFrame": 6},
            ),
            _family(
                family_ids["cardiac_arrest_remove"],
                "XRL.World.Effects/CardiacArrest.cs",
                "Remove",
                {"MessageFrame": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "immobilized_apply",
        "stuck_apply",
        "latched_begin_take_action",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "ActiveEffectMessageFrameOwnerTranslationPatch.cs",
            "ActiveEffectMessageFrameOwnerTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "MessageFrameTranslatorTests.cs",
            "MessageFrames/verbs.ja.json",
        )

    _assert_evidence_contains(
        entries,
        family_ids["immobilized_apply"],
        'Immobilized.Apply source frame: DidX("are", Text)',
        "MessageFrame key: verb=are extra=immobilized",
    )
    _assert_evidence_contains(
        entries,
        family_ids["stuck_apply"],
        'Stuck.Apply source frame: DidX("are", DisplayName)',
        "MessageFrame key: verb=are extra=stuck in {0}",
        "MessageFrame key: verb=are extra=grabbed by {0}",
    )
    _assert_evidence_contains(
        entries,
        family_ids["latched_begin_take_action"],
        'LatchedOnto.HandleEvent(BeginTakeActionEvent) source frame: DidX("break", "free from " + text)',
        "MessageFrame key: verb=break extra=free from {0}",
    )
    assert entries[family_ids["cardiac_arrest_remove"]]["closure_status"] == "covered_by_owner_route"

    residual_payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert residual_payload["actionable_entries"] == 0
    assert sum(entry["text_construction_count"] for entry in residual_payload["entries"]) == 0


def test_policy_records_issue719_tranche42_social_active_effect_owner_route_overlays() -> None:
    """Tranche 42 closes social active-effect Apply rows across MessageFrame and JournalAPI surfaces."""
    family_ids = {
        "lovesick_apply": "XRL.World.Effects/Lovesick.cs::Lovesick.Apply(GameObject)",
        "beguiled_apply": "XRL.World.Effects/Beguiled.cs::Beguiled.Apply(GameObject)",
        "proselytized_apply": "XRL.World.Effects/Proselytized.cs::Proselytized.Apply(GameObject)",
        "rebuked_apply": "XRL.World.Effects/Rebuked.cs::Rebuked.Apply(GameObject)",
        "cardiac_arrest_remove": "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["lovesick_apply"],
                "XRL.World.Effects/Lovesick.cs",
                "Apply",
                {"MessageFrame": 1, "JournalAPI": 3},
            ),
            _family(
                family_ids["beguiled_apply"],
                "XRL.World.Effects/Beguiled.cs",
                "Apply",
                {"MessageFrame": 1, "JournalAPI": 3},
            ),
            _family(
                family_ids["proselytized_apply"],
                "XRL.World.Effects/Proselytized.cs",
                "Apply",
                {"MessageFrame": 1, "JournalAPI": 3},
            ),
            _family(
                family_ids["rebuked_apply"],
                "XRL.World.Effects/Rebuked.cs",
                "Apply",
                {"MessageFrame": 1, "JournalAPI": 3, "HistoricStringExpander": 1},
            ),
            _family(
                family_ids["cardiac_arrest_remove"],
                "XRL.World.Effects/CardiacArrest.cs",
                "Remove",
                {"MessageFrame": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in ("lovesick_apply", "beguiled_apply", "proselytized_apply", "rebuked_apply"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "ActiveEffectMessageFrameOwnerTranslationPatch.cs",
            "JournalAccomplishmentAddTranslationPatch.cs",
            "JournalPatternTranslatorTests.cs",
            "JournalApiAddTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "MessageFrames/verbs.ja.json",
            "Dictionaries/journal-patterns.ja.json",
        )

    _assert_evidence_contains(
        entries,
        family_ids["rebuked_apply"],
        "Rebuked.Apply HSE mural is covered after expansion by the JournalAPI storage route",
    )
    assert entries[family_ids["cardiac_arrest_remove"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cardiac_arrest_remove"],
        "ActiveEffectMessageFrameOwnerTranslationPatch.cs",
        "ActiveEffectMessageFrameOwnerTranslationPatchTests.cs",
        "ui-popup.ja.json",
        "ui-messagelog-leaf.ja.json",
        "MessageFrames/verbs.ja.json",
        'CardiacArrest.Remove non-player source frame: DidX("look", "less stricken")',
    )

    residual_payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert residual_payload["actionable_entries"] == 0
    assert sum(entry["text_construction_count"] for entry in residual_payload["entries"]) == 0


def test_policy_records_issue719_tranche43_cardiac_arrest_remove_owner_route_overlay() -> None:
    """Tranche 43 closes CardiacArrest.Remove across popup, MessageFrame, and nested Ill popup shapes."""
    family_id = "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World.Effects/CardiacArrest.cs",
                "Remove",
                {"MessageFrame": 5},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_id,
        "ActiveEffectMessageFrameOwnerTranslationPatch.cs",
        "PopupShowSemanticPipeline.cs",
        "PopupTranslationPatchTests.cs",
        'CardiacArrest.Remove player popup: Popup.Show("{{G|Your hearts restart!}}")',
        'CardiacArrest.Remove nested Ill.Apply popup source: "You feel shaken and infirm."',
        "RecordsCardiacArrestRemovePlayerPopupTranslations_WhenOwnerIsPatched",
    )

    residual_payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert residual_payload["actionable_entries"] == 0
    assert sum(entry["text_construction_count"] for entry in residual_payload["entries"]) == 0


def test_policy_records_issue719_fixed_popup_dictionary_route_overlays() -> None:
    """Fixed popup-only families close only when existing dictionary route covers every visible string."""
    family_ids = {
        "death_gate": "XRL.World.Parts/DeathGate.cs::DeathGate.FireEvent(Event)",
        "chavvah_hide": "XRL/ChavvahSystem.cs::ChavvahSystem.Hide()",
        "switch": "XRL.World.Parts/Switch.cs::Switch.FlipSwitch(GameObject)",
        "wish_skill": "XRL.World.Parts/Skills.cs::Skills.WishSkill(string)",
        "interior_portal": ("XRL.World.Parts/InteriorPortal.cs::InteriorPortal.HandleEvent(InventoryActionEvent)"),
        "famished_apply": "XRL.World.Effects/Famished.cs::Famished.Apply(GameObject)",
        "glotrot_fire_event": "XRL.World.Effects/Glotrot.cs::Glotrot.FireEvent(Event)",
        "paralyzed_fire_event": "XRL.World.Effects/Paralyzed.cs::Paralyzed.FireEvent(Event)",
        "waking_dream_award": "XRL.World.Effects/WakingDream.cs::WakingDream.Award(GameObject)",
        "body_part": "XRL.World.Anatomy/BodyPart.cs::BodyPart.SetAsPreferredDefault(bool)",
        "defensive_chromatophores": (
            "XRL.World.Parts.Mutation/DefensiveChromatophores.cs::DefensiveChromatophores.AttemptScintillate(bool)"
        ),
        "unwelcome_germination": (
            "XRL.World.Parts.Mutation/UnwelcomeGermination.cs::UnwelcomeGermination.FireEvent(Event)"
        ),
        "teleport_gate": ("XRL.World.Parts/TeleportGate.cs::TeleportGate.CheckPossibleSubject(GameObject,IEvent,bool)"),
        "cybernetics_imprinting": (
            "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs::"
            "CyberneticsOnboardRecoilerImprinting.HandleEvent(InventoryActionEvent)"
        ),
        "pax_infect": ("XRL.World.Conversations.Parts/PaxInfectLimb.cs::PaxInfectLimb.Infect(GameObject)"),
        "water_ritual_learn_skill": (
            "XRL.World.Conversations.Parts/WaterRitualLearnSkill.cs::"
            "WaterRitualLearnSkill.HandleEvent(EnteredElementEvent)"
        ),
        "tinker_data_data_disk": "XRL.World.Tinkering/TinkerData.cs::TinkerData.DataDisk()",
        "domination": "XRL.World.Parts.Mutation/Domination.cs::Domination.BreakDomination()",
        "recoil": "XRL.World.Parts/RecoilAbility.cs::RecoilAbility.HandleEvent(CommandEvent)",
        "cybernetics_teleporter": (
            "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs::"
            "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport(GameObject,IEvent)"
        ),
        "igrenade": "XRL.World.Parts/IGrenade.cs::IGrenade.HandleEvent(InventoryActionEvent)",
        "mechanical_wings": "XRL.World.Parts/MechanicalWings.cs::MechanicalWings.FireEvent(Event)",
        "keybinds_select_input": "Qud.UI/KeybindsScreen.cs::KeybindsScreen.SelectInputType()",
        "object_finder": "XRL.UI/ObjectFinder.cs::ObjectFinder.ConfigFilters()",
        "ascension_end_turn": ("XRL.World.Quests/AscensionSystem.cs::AscensionSystem.HandleEvent(EndTurnEvent)"),
        "ascension_after_conversation": (
            "XRL.World.Quests/AscensionSystem.cs::AscensionSystem.HandleEvent(AfterConversationEvent)"
        ),
        "ascension_generic_query": (
            "XRL.World.Quests/AscensionSystem.cs::AscensionSystem.HandleEvent(GenericQueryEvent)"
        ),
        "golem_wish_finish": (
            "XRL.World.Quests.GolemQuest/GolemQuestSelection.cs::GolemQuestSelection.WishFinishGolem()"
        ),
        "cloneling_fire_event": "XRL.World.Parts/Cloneling.cs::Cloneling.FireEvent(Event)",
        "main_menu_delete": "Qud.UI/MainMenu.cs::MainMenu.HandleDelete()",
        "save_management_delete": "Qud.UI/SaveManagement.cs::SaveManagement.HandleDelete()",
        "mod_manager_prompt_scripting": "Qud.UI/ModManagerUI.cs::ModManagerUI.PromptScripting()",
        "exhausted_begin_take_action": ("XRL.World.Effects/Exhausted.cs::Exhausted.HandleEvent(BeginTakeActionEvent)"),
        "exhausted_fire_event": "XRL.World.Effects/Exhausted.cs::Exhausted.FireEvent(Event)",
        "lost_remove": "XRL.World.Effects/Lost.cs::Lost.Remove(GameObject)",
        "glotrot_ask_pulldown": "XRL.World.Effects/Glotrot.cs::Glotrot.AskPulldown()",
        "ark_core_start_end": "XRL.World.Parts/ArkCore.cs::ArkCore.StartEnd(bool)",
        "main_menu_selected_info": "Qud.UI/MainMenu.cs::MainMenu.SelectedInfo(FrameworkDataElement)",
        "qud_chargen_select_type": (
            "XRL.CharacterBuilds.Qud/QudChartypeModule.cs::QudChartypeModule.selectType(string)"
        ),
        "sunder_mind_fire_event": ("XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.FireEvent(Event)"),
        "switch_fire_event": "XRL.World.Parts/Switch.cs::Switch.FireEvent(Event)",
        "stairs_up_fire_event": "XRL.World.Parts/StairsUp.cs::StairsUp.FireEvent(Event)",
        "cherubim_lock_chime": "XRL.World.Parts/CherubimLock.cs::CherubimLock.Chime()",
        "teleport_on_eat": "XRL.World.Parts/TeleportOnEat.cs::TeleportOnEat.FireEvent(Event)",
        "dynamic_quest_find_target": (
            "XRL.World/DynamicQuestsGameState.cs::DynamicQuestsGameState.FindQuestTarget(string)"
        ),
        "mod_disguise_fire_event": "XRL.World.Parts/ModDisguise.cs::ModDisguise.FireEvent(Event)",
        "psionic_migraines_fire_event": (
            "XRL.World.Parts.Mutation/PsionicMigraines.cs::PsionicMigraines.FireEvent(Event)"
        ),
        "frost_webs_fire_event": "XRL.World.Parts.Mutation/FrostWebs.cs::FrostWebs.FireEvent(Event)",
        "cell_invalid_physics": "XRL.World/Cell.cs::Cell.LogInvalidPhysics(GameObject)",
        "gas_disease_apply": "XRL.World.Parts/GasDisease.cs::GasDisease.ApplyDisease(GameObject)",
        "skittish_lose_control": "XRL.World.Parts.Mutation/Skittish.cs::Skittish.LoseControl()",
        "time_cube_activate": "XRL.World.Parts/TimeCube.cs::TimeCube.Activate(GameObject,bool,IExamineEvent)",
        "terrain_travel_fungal_fire_event": (
            "XRL.World.Parts/TerrainTravelFungal.cs::TerrainTravelFungal.FireEvent(Event)"
        ),
        "xrl_game_save_error": "XRL/XRLGame.cs::XRLGame.SaveGameError(string,Exception,bool)",
        "cyclopean_prism_ptoh_annoyed": ("XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.PtohAnnoyed(GameObject)"),
        "time_cubed_apply": "XRL.World.Effects/TimeCubed.cs::TimeCubed.Apply(GameObject)",
        "sticky_tongue": ("XRL.World.Parts.Mutation/StickyTongue.cs::StickyTongue.HandleEvent(CommandEvent)"),
        "cybernetics_custom_visage": (
            "XRL.World.Parts/CyberneticsCustomVisage.cs::CyberneticsCustomVisage.ApplyVisage(GameObject)"
        ),
        "crungle_gaze": "XRL.World.Parts.Mutation/CrungleGaze.cs::CrungleGaze.FireLine(List<Cell>)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["death_gate"],
                "XRL.World.Parts/DeathGate.cs",
                "FireEvent",
                {"Popup": 2},
            ),
            _family(
                family_ids["chavvah_hide"],
                "XRL/ChavvahSystem.cs",
                "Hide",
                {"Popup": 1},
            ),
            _family(
                family_ids["switch"],
                "XRL.World.Parts/Switch.cs",
                "FlipSwitch",
                {"Popup": 1},
            ),
            _family(
                family_ids["wish_skill"],
                "XRL.World.Parts/Skills.cs",
                "WishSkill",
                {"Popup": 1},
            ),
            _family(
                family_ids["interior_portal"],
                "XRL.World.Parts/InteriorPortal.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["famished_apply"],
                "XRL.World.Effects/Famished.cs",
                "Apply",
                {"Popup": 7},
            ),
            _family(
                family_ids["glotrot_fire_event"],
                "XRL.World.Effects/Glotrot.cs",
                "FireEvent",
                {"Popup": 14},
            ),
            _family(
                family_ids["paralyzed_fire_event"],
                "XRL.World.Effects/Paralyzed.cs",
                "FireEvent",
                {"Popup": 8},
            ),
            _family(
                family_ids["waking_dream_award"],
                "XRL.World.Effects/WakingDream.cs",
                "Award",
                {"Popup": 7},
            ),
            _family(
                family_ids["body_part"],
                "XRL.World.Anatomy/BodyPart.cs",
                "SetAsPreferredDefault",
                {"Popup": 2},
            ),
            _family(
                family_ids["defensive_chromatophores"],
                "XRL.World.Parts.Mutation/DefensiveChromatophores.cs",
                "AttemptScintillate",
                {"Popup": 3},
            ),
            _family(
                family_ids["unwelcome_germination"],
                "XRL.World.Parts.Mutation/UnwelcomeGermination.cs",
                "FireEvent",
                {"Popup": 3},
            ),
            _family(
                family_ids["teleport_gate"],
                "XRL.World.Parts/TeleportGate.cs",
                "CheckPossibleSubject",
                {"Popup": 3},
            ),
            _family(
                family_ids["cybernetics_imprinting"],
                "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs",
                "HandleEvent",
                {"Popup": 3},
            ),
            _family(
                family_ids["pax_infect"],
                "XRL.World.Conversations.Parts/PaxInfectLimb.cs",
                "Infect",
                {"Popup": 3},
            ),
            _family(
                family_ids["water_ritual_learn_skill"],
                "XRL.World.Conversations.Parts/WaterRitualLearnSkill.cs",
                "HandleEvent",
                {"Popup": 3},
            ),
            _family(
                family_ids["tinker_data_data_disk"],
                "XRL.World.Tinkering/TinkerData.cs",
                "DataDisk",
                {"Popup": 3},
            ),
            _family(
                family_ids["domination"],
                "XRL.World.Parts.Mutation/Domination.cs",
                "BreakDomination",
                {"Popup": 1},
            ),
            _family(
                family_ids["recoil"],
                "XRL.World.Parts/RecoilAbility.cs",
                "HandleEvent",
                {"Popup": 2},
            ),
            _family(
                family_ids["cybernetics_teleporter"],
                "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs",
                "ActuateTeleport",
                {"Popup": 3},
            ),
            _family(
                family_ids["igrenade"],
                "XRL.World.Parts/IGrenade.cs",
                "HandleEvent",
                {"Popup": 3},
            ),
            _family(
                family_ids["mechanical_wings"],
                "XRL.World.Parts/MechanicalWings.cs",
                "FireEvent",
                {"Popup": 3},
            ),
            _family(
                family_ids["keybinds_select_input"],
                "Qud.UI/KeybindsScreen.cs",
                "SelectInputType",
                {"Popup": 2},
            ),
            _family(
                family_ids["object_finder"],
                "XRL.UI/ObjectFinder.cs",
                "ConfigFilters",
                {"Popup": 2},
            ),
            _family(
                family_ids["ascension_end_turn"],
                "XRL.World.Quests/AscensionSystem.cs",
                "HandleEvent",
                {"Popup": 7},
            ),
            _family(
                family_ids["ascension_after_conversation"],
                "XRL.World.Quests/AscensionSystem.cs",
                "HandleEvent",
                {"Popup": 4},
            ),
            _family(
                family_ids["ascension_generic_query"],
                "XRL.World.Quests/AscensionSystem.cs",
                "HandleEvent",
                {"Popup": 4},
            ),
            _family(
                family_ids["golem_wish_finish"],
                "XRL.World.Quests.GolemQuest/GolemQuestSelection.cs",
                "WishFinishGolem",
                {"Popup": 3},
            ),
            _family(
                family_ids["cloneling_fire_event"],
                "XRL.World.Parts/Cloneling.cs",
                "FireEvent",
                {"Popup": 4},
            ),
            _family(
                family_ids["main_menu_delete"],
                "Qud.UI/MainMenu.cs",
                "HandleDelete",
                {"Popup": 4},
            ),
            _family(
                family_ids["save_management_delete"],
                "Qud.UI/SaveManagement.cs",
                "HandleDelete",
                {"Popup": 4},
            ),
            _family(
                family_ids["mod_manager_prompt_scripting"],
                "Qud.UI/ModManagerUI.cs",
                "PromptScripting",
                {"Popup": 7},
            ),
            _family(
                family_ids["exhausted_begin_take_action"],
                "XRL.World.Effects/Exhausted.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["exhausted_fire_event"],
                "XRL.World.Effects/Exhausted.cs",
                "FireEvent",
                {"Popup": 4},
            ),
            _family(
                family_ids["lost_remove"],
                "XRL.World.Effects/Lost.cs",
                "Remove",
                {"Popup": 2},
            ),
            _family(
                family_ids["glotrot_ask_pulldown"],
                "XRL.World.Effects/Glotrot.cs",
                "AskPulldown",
                {"Popup": 1},
            ),
            _family(
                family_ids["ark_core_start_end"],
                "XRL.World.Parts/ArkCore.cs",
                "StartEnd",
                {"Popup": 2, "OtherInvocation": 18, "Assignment": 9},
            ),
            _family(
                family_ids["main_menu_selected_info"],
                "Qud.UI/MainMenu.cs",
                "SelectedInfo",
                {"Popup": 2, "OtherInvocation": 4, "Initializer": 2, "Other": 4},
            ),
            _family(
                family_ids["qud_chargen_select_type"],
                "XRL.CharacterBuilds.Qud/QudChartypeModule.cs",
                "selectType",
                {"Popup": 1, "OtherInvocation": 3, "Initializer": 1, "Other": 3},
            ),
            _family(
                family_ids["sunder_mind_fire_event"],
                "XRL.World.Parts.Mutation/SunderMind.cs",
                "FireEvent",
                {"Popup": 4, "OtherInvocation": 8, "Assignment": 3},
            ),
            _family(
                family_ids["switch_fire_event"],
                "XRL.World.Parts/Switch.cs",
                "FireEvent",
                {"Popup": 3, "OtherInvocation": 4},
            ),
            _family(
                family_ids["stairs_up_fire_event"],
                "XRL.World.Parts/StairsUp.cs",
                "FireEvent",
                {"Popup": 1, "OtherInvocation": 5},
            ),
            _family(
                family_ids["cherubim_lock_chime"],
                "XRL.World.Parts/CherubimLock.cs",
                "Chime",
                {"Popup": 1, "OtherInvocation": 5},
            ),
            _family(
                family_ids["teleport_on_eat"],
                "XRL.World.Parts/TeleportOnEat.cs",
                "FireEvent",
                {"Popup": 1, "OtherInvocation": 5},
            ),
            _family(
                family_ids["dynamic_quest_find_target"],
                "XRL.World/DynamicQuestsGameState.cs",
                "FindQuestTarget",
                {"Popup": 1, "OtherInvocation": 5},
            ),
            _family(
                family_ids["mod_disguise_fire_event"],
                "XRL.World.Parts/ModDisguise.cs",
                "FireEvent",
                {"Popup": 1, "OtherInvocation": 4},
            ),
            _family(
                family_ids["psionic_migraines_fire_event"],
                "XRL.World.Parts.Mutation/PsionicMigraines.cs",
                "FireEvent",
                {"Popup": 1, "OtherInvocation": 4},
            ),
            _family(
                family_ids["frost_webs_fire_event"],
                "XRL.World.Parts.Mutation/FrostWebs.cs",
                "FireEvent",
                {"Popup": 1, "OtherInvocation": 5},
            ),
            _family(
                family_ids["cell_invalid_physics"],
                "XRL.World/Cell.cs",
                "LogInvalidPhysics",
                {"Popup": 1, "OtherInvocation": 4},
            ),
            _family(
                family_ids["gas_disease_apply"],
                "XRL.World.Parts/GasDisease.cs",
                "ApplyDisease",
                {"Popup": 1, "OtherInvocation": 4},
            ),
            _family(
                family_ids["skittish_lose_control"],
                "XRL.World.Parts.Mutation/Skittish.cs",
                "LoseControl",
                {"Popup": 1, "OtherInvocation": 3},
            ),
            _family(
                family_ids["time_cube_activate"],
                "XRL.World.Parts/TimeCube.cs",
                "Activate",
                {"Popup": 1, "OtherInvocation": 3},
            ),
            _family(
                family_ids["terrain_travel_fungal_fire_event"],
                "XRL.World.Parts/TerrainTravelFungal.cs",
                "FireEvent",
                {"Popup": 1, "OtherInvocation": 3},
            ),
            _family(
                family_ids["xrl_game_save_error"],
                "XRL/XRLGame.cs",
                "SaveGameError",
                {"Popup": 1, "OtherInvocation": 3},
            ),
            _family(
                family_ids["cyclopean_prism_ptoh_annoyed"],
                "XRL.World.Parts/CyclopeanPrism.cs",
                "PtohAnnoyed",
                {"Popup": 1, "OtherInvocation": 6},
            ),
            _family(
                family_ids["time_cubed_apply"],
                "XRL.World.Effects/TimeCubed.cs",
                "Apply",
                {"Popup": 1},
            ),
            _family(
                family_ids["sticky_tongue"],
                "XRL.World.Parts.Mutation/StickyTongue.cs",
                "HandleEvent",
                {"Other": 1, "OtherInvocation": 1, "Popup": 1},
            ),
            _family(
                family_ids["cybernetics_custom_visage"],
                "XRL.World.Parts/CyberneticsCustomVisage.cs",
                "ApplyVisage",
                {"Initializer": 3, "Popup": 3},
            ),
            _family(
                family_ids["crungle_gaze"],
                "XRL.World.Parts.Mutation/CrungleGaze.cs",
                "FireLine",
                {"OtherInvocation": 4, "Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "death_gate",
        "chavvah_hide",
        "switch",
        "interior_portal",
        "famished_apply",
        "glotrot_fire_event",
        "paralyzed_fire_event",
        "waking_dream_award",
        "body_part",
        "defensive_chromatophores",
        "unwelcome_germination",
        "teleport_gate",
        "cybernetics_imprinting",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupTranslationPatch.cs",
            "PopupShowTranslationPatch.cs",
            "PopupTranslationPatchTests.cs",
            "ui-popup.ja.json",
        )

    for family_key in (
        "ascension_end_turn",
        "ascension_after_conversation",
        "ascension_generic_query",
        "golem_wish_finish",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupShowTranslationPatch.cs",
            "PopupShowTranslationPatchTests.cs",
            "ui-popup.ja.json",
        )

    assert entries[family_ids["cloneling_fire_event"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cloneling_fire_event"],
        "PopupShowTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "world-parts.ja.json",
    )

    for family_key in ("main_menu_delete", "save_management_delete"):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupMessageTranslationPatch.cs",
            "PopupMessageTranslationPatchTests.cs",
            "ui-popup.ja.json",
            "ui-default.ja.json",
        )

    assert entries[family_ids["mod_manager_prompt_scripting"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["mod_manager_prompt_scripting"],
        "PopupMessageTranslationPatch.cs",
        "PopupMessageTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "ui-modpage.ja.json",
    )

    for family_key in (
        "exhausted_begin_take_action",
        "exhausted_fire_event",
        "glotrot_ask_pulldown",
        "ark_core_start_end",
        "qud_chargen_select_type",
        "sunder_mind_fire_event",
        "switch_fire_event",
        "stairs_up_fire_event",
        "teleport_on_eat",
        "dynamic_quest_find_target",
        "mod_disguise_fire_event",
        "psionic_migraines_fire_event",
        "cell_invalid_physics",
        "gas_disease_apply",
        "skittish_lose_control",
        "time_cube_activate",
        "xrl_game_save_error",
        "cyclopean_prism_ptoh_annoyed",
        "domination",
        "sticky_tongue",
        "crungle_gaze",
        "wish_skill",
        "pax_infect",
        "water_ritual_learn_skill",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupShowTranslationPatch.cs",
            "PopupShowTranslationPatchTests.cs",
            "ui-popup.ja.json",
        )

    assert entries[family_ids["frost_webs_fire_event"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["frost_webs_fire_event"],
        "PopupShowTranslationPatch.cs",
        "PopupTranslationPatch.cs",
        "PopupTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["terrain_travel_fungal_fire_event"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["terrain_travel_fungal_fire_event"],
        "PopupTranslationPatch.cs",
        "PopupTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["time_cubed_apply"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["time_cubed_apply"],
        "PopupTranslationPatch.cs",
        "PopupTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["tinker_data_data_disk"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["tinker_data_data_disk"],
        "PopupPickOptionTranslationPatch.cs",
        "PopupPickOptionTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["cybernetics_custom_visage"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cybernetics_custom_visage"],
        "PopupPickOptionTranslationPatch.cs",
        "PopupPickOptionTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["lost_remove"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["lost_remove"],
        "PopupShowSpaceTranslationPatch.cs",
        "PopupShowSpaceTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["cherubim_lock_chime"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cherubim_lock_chime"],
        "PopupShowTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "world-parts.ja.json",
    )

    assert entries[family_ids["main_menu_selected_info"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["main_menu_selected_info"],
        "PopupAskStringTranslationPatch.cs",
        "PopupAskStringTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )

    assert entries[family_ids["keybinds_select_input"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["keybinds_select_input"],
        "PopupPickOptionTranslationPatch.cs",
        "PopupPickOptionTranslationPatchTests.cs",
        "ui-options.ja.json",
        "ui-keybinds.ja.json",
    )

    assert entries[family_ids["recoil"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["recoil"],
        "PopupPickOptionTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )
    assert entries[family_ids["cybernetics_teleporter"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["cybernetics_teleporter"],
        "CyberneticsOnboardRecoilerPopupTranslationPatch.cs",
        "CyberneticsOnboardRecoilerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["igrenade"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["igrenade"],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["mechanical_wings"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["mechanical_wings"],
        "MechanicalWingsPopupTranslationPatch.cs",
        "MechanicalWingsPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[family_ids["object_finder"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["object_finder"],
        "ObjectFinderConfigFiltersTranslationPatch.cs",
        "ObjectFinderConfigFiltersTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_baetyl_offering_fixed_popup_dictionary_route_overlays() -> None:
    """BaetylOffering fixed result popups are existing dictionary leaves, not owner-patch work."""
    family_ids = {
        "out_of_options": ("XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.CheckOutOfOptions(GameObject)"),
        "critical_failure": (
            "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultCriticalFailure(GameObject)"
        ),
        "failure": "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultFailure(GameObject)",
        "partial_success": ("XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultPartialSuccess(GameObject)"),
        "success": "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultSuccess(GameObject)",
        "exceptional_success": (
            "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultExceptionalSuccess(GameObject)"
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, "XRL.World/BaetylOfferingSifrah.cs", member_name, {"Popup": 1})
            for member_name, family_id in {
                "CheckOutOfOptions": family_ids["out_of_options"],
                "ResultCriticalFailure": family_ids["critical_failure"],
                "ResultFailure": family_ids["failure"],
                "ResultPartialSuccess": family_ids["partial_success"],
                "ResultSuccess": family_ids["success"],
                "ResultExceptionalSuccess": family_ids["exceptional_success"],
            }.items()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "static_producer_closure.py",
            "PopupShowTranslationPatch.cs",
            "PopupShowTranslationPatchTests.cs",
        )

    _assert_evidence_contains(
        entries,
        family_ids["critical_failure"],
        "ui-popup.ja.json",
    )
    _assert_evidence_contains(
        entries,
        family_ids["out_of_options"],
        "ui-messagelog-world.ja.json",
        "world-parts.ja.json",
    )


def test_policy_records_issue719_basic_cooking_effect_apply_overlays() -> None:
    """Basic cooking ApplyEffect popup rows are covered by the cooking runtime owner patch."""
    family_ids = {
        "hitpoints": (
            "XRL.World.Effects/BasicCookingEffect_Hitpoints.cs::BasicCookingEffect_Hitpoints.ApplyEffect(GameObject)"
        ),
        "ma": "XRL.World.Effects/BasicCookingEffect_MA.cs::BasicCookingEffect_MA.ApplyEffect(GameObject)",
        "ms": "XRL.World.Effects/BasicCookingEffect_MS.cs::BasicCookingEffect_MS.ApplyEffect(GameObject)",
        "quickness": (
            "XRL.World.Effects/BasicCookingEffect_Quickness.cs::BasicCookingEffect_Quickness.ApplyEffect(GameObject)"
        ),
        "to_hit": "XRL.World.Effects/BasicCookingEffect_ToHit.cs::BasicCookingEffect_ToHit.ApplyEffect(GameObject)",
        "xp": "XRL.World.Effects/BasicCookingEffect_XP.cs::BasicCookingEffect_XP.ApplyEffect(GameObject)",
        "regeneration": (
            "XRL.World.Effects/BasicCookingEffect_Regeneration.cs::"
            "BasicCookingEffect_Regeneration.ApplyEffect(GameObject)"
        ),
        "random_stat": (
            "XRL.World.Effects/BasicCookingEffect_RandomStat.cs::BasicCookingEffect_RandomStat.ApplyEffect(GameObject)"
        ),
        "fungal_limb": (
            "XRL.World.Effects/FungalSporeInfection.cs::"
            "FungalSporeInfection.ChooseLimbForInfection(List<BodyPart>,string,out BodyPart,out string,bool)"
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_ids["hitpoints"],
                "XRL.World.Effects/BasicCookingEffect_Hitpoints.cs",
                "ApplyEffect",
                {"Popup": 1},
            ),
            _family(family_ids["ma"], "XRL.World.Effects/BasicCookingEffect_MA.cs", "ApplyEffect", {"Popup": 1}),
            _family(family_ids["ms"], "XRL.World.Effects/BasicCookingEffect_MS.cs", "ApplyEffect", {"Popup": 1}),
            _family(
                family_ids["quickness"],
                "XRL.World.Effects/BasicCookingEffect_Quickness.cs",
                "ApplyEffect",
                {"Popup": 1},
            ),
            _family(
                family_ids["to_hit"],
                "XRL.World.Effects/BasicCookingEffect_ToHit.cs",
                "ApplyEffect",
                {"Popup": 1},
            ),
            _family(family_ids["xp"], "XRL.World.Effects/BasicCookingEffect_XP.cs", "ApplyEffect", {"Popup": 1}),
            _family(
                family_ids["regeneration"],
                "XRL.World.Effects/BasicCookingEffect_Regeneration.cs",
                "ApplyEffect",
                {"Popup": 1},
            ),
            _family(
                family_ids["random_stat"],
                "XRL.World.Effects/BasicCookingEffect_RandomStat.cs",
                "ApplyEffect",
                {"Popup": 1},
            ),
            _family(
                family_ids["fungal_limb"],
                "XRL.World.Effects/FungalSporeInfection.cs",
                "ChooseLimbForInfection",
                {"Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "hitpoints",
        "ma",
        "ms",
        "quickness",
        "to_hit",
        "xp",
        "regeneration",
        "random_stat",
        "fungal_limb",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"

    _assert_evidence_contains(
        entries,
        family_ids["random_stat"],
        "CookingRuntimeTranslationPatch.cs",
        "CookingRuntimeTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        family_ids["fungal_limb"],
        "FungalSporeInfectionTranslationPatch.cs",
        "PopupTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_tonic_fixed_popup_overlays() -> None:
    """Fixed tonic popup rows close through the existing exact popup dictionary route."""
    family_ids = {
        "ambient_reality": (
            "XRL.World.Effects/AmbientRealityStabilized.cs::AmbientRealityStabilized.HandleEvent(EndTurnEvent)"
        ),
        "blaze": ("XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.HandleEvent(BeginTakeActionEvent)"),
        "blaze_apply": "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.Apply(GameObject)",
        "blaze_overdose": "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.ApplyOverdose(GameObject)",
        "hoarshroom_apply": "XRL.World.Effects/Hoarshroom_Tonic.cs::Hoarshroom_Tonic.Apply(GameObject)",
        "hoarshroom_overdose": ("XRL.World.Effects/Hoarshroom_Tonic.cs::Hoarshroom_Tonic.ApplyOverdose(GameObject)"),
        "hoarshroom": "XRL.World.Effects/Hoarshroom_Tonic.cs::Hoarshroom_Tonic.Remove(GameObject)",
        "hulk_honey_apply": "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.Apply(GameObject)",
        "hulk_honey_allergy": ("XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.ApplyAllergy(GameObject)"),
        "hulk_honey_fire": "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.FireEvent(Event)",
        "hulk_honey": "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.Remove(GameObject)",
        "love_apply": "XRL.World.Effects/LoveTonic.cs::LoveTonic.Apply(GameObject)",
        "love_fire": "XRL.World.Effects/LoveTonic.cs::LoveTonic.FireEvent(Event)",
        "love": "XRL.World.Effects/LoveTonic.cs::LoveTonic.Remove(GameObject)",
        "rubbergum_apply": "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.Apply(GameObject)",
        "rubbergum_allergy": ("XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.ApplyAllergy(GameObject)"),
        "rubbergum_fire": "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.FireEvent(Event)",
        "rubbergum": "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.Remove(GameObject)",
        "salve_apply": "XRL.World.Effects/Salve_Tonic.cs::Salve_Tonic.Apply(GameObject)",
        "salve_fire": "XRL.World.Effects/Salve_Tonic.cs::Salve_Tonic.FireEvent(Event)",
        "salve": "XRL.World.Effects/Salve_Tonic.cs::Salve_Tonic.Remove(GameObject)",
        "skulk_apply": "XRL.World.Effects/Skulk_Tonic.cs::Skulk_Tonic.Apply(GameObject)",
        "skulk_overdose": "XRL.World.Effects/Skulk_Tonic.cs::Skulk_Tonic.ApplyOverdose(GameObject)",
        "skulk": "XRL.World.Effects/Skulk_Tonic.cs::Skulk_Tonic.Remove(GameObject)",
        "sphynx_overdose": ("XRL.World.Effects/SphynxSalt_Tonic.cs::SphynxSalt_Tonic.ApplyOverdose(GameObject)"),
        "sphynx": "XRL.World.Effects/SphynxSalt_Tonic.cs::SphynxSalt_Tonic.Remove(GameObject)",
        "shade_oil_overdose": ("XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.ApplyOverdose(GameObject)"),
        "ubernostrum_apply": "XRL.World.Effects/Ubernostrum_Tonic.cs::Ubernostrum_Tonic.Apply(GameObject)",
        "ubernostrum_fire": ("XRL.World.Effects/Ubernostrum_Tonic.cs::Ubernostrum_Tonic.FireEvent(Event)"),
        "ubernostrum": "XRL.World.Effects/Ubernostrum_Tonic.cs::Ubernostrum_Tonic.Remove(GameObject)",
    }
    inventory = _inventory(
        [
            _family(
                family_ids["ambient_reality"],
                "XRL.World.Effects/AmbientRealityStabilized.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                family_ids["blaze"],
                "XRL.World.Effects/Blaze_Tonic.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(family_ids["blaze_apply"], "XRL.World.Effects/Blaze_Tonic.cs", "Apply", {"Popup": 1}),
            _family(
                family_ids["blaze_overdose"],
                "XRL.World.Effects/Blaze_Tonic.cs",
                "ApplyOverdose",
                {"Popup": 3},
            ),
            _family(
                family_ids["hoarshroom_apply"],
                "XRL.World.Effects/Hoarshroom_Tonic.cs",
                "Apply",
                {"Popup": 4},
            ),
            _family(
                family_ids["hoarshroom_overdose"],
                "XRL.World.Effects/Hoarshroom_Tonic.cs",
                "ApplyOverdose",
                {"Popup": 3},
            ),
            _family(family_ids["hoarshroom"], "XRL.World.Effects/Hoarshroom_Tonic.cs", "Remove", {"Popup": 1}),
            _family(
                family_ids["hulk_honey_apply"],
                "XRL.World.Effects/HulkHoney_Tonic.cs",
                "Apply",
                {"Popup": 4},
            ),
            _family(
                family_ids["hulk_honey_allergy"],
                "XRL.World.Effects/HulkHoney_Tonic.cs",
                "ApplyAllergy",
                {"Popup": 3},
            ),
            _family(
                family_ids["hulk_honey_fire"],
                "XRL.World.Effects/HulkHoney_Tonic.cs",
                "FireEvent",
                {"Popup": 10},
            ),
            _family(family_ids["hulk_honey"], "XRL.World.Effects/HulkHoney_Tonic.cs", "Remove", {"Popup": 1}),
            _family(family_ids["love_apply"], "XRL.World.Effects/LoveTonic.cs", "Apply", {"Popup": 3}),
            _family(family_ids["love_fire"], "XRL.World.Effects/LoveTonic.cs", "FireEvent", {"Popup": 7}),
            _family(family_ids["love"], "XRL.World.Effects/LoveTonic.cs", "Remove", {"Popup": 1}),
            _family(family_ids["rubbergum_apply"], "XRL.World.Effects/Rubbergum_Tonic.cs", "Apply", {"Popup": 4}),
            _family(
                family_ids["rubbergum_allergy"],
                "XRL.World.Effects/Rubbergum_Tonic.cs",
                "ApplyAllergy",
                {"Popup": 3},
            ),
            _family(
                family_ids["rubbergum_fire"],
                "XRL.World.Effects/Rubbergum_Tonic.cs",
                "FireEvent",
                {"Popup": 10},
            ),
            _family(family_ids["rubbergum"], "XRL.World.Effects/Rubbergum_Tonic.cs", "Remove", {"Popup": 1}),
            _family(family_ids["salve_apply"], "XRL.World.Effects/Salve_Tonic.cs", "Apply", {"Popup": 3}),
            _family(family_ids["salve_fire"], "XRL.World.Effects/Salve_Tonic.cs", "FireEvent", {"Popup": 8}),
            _family(family_ids["salve"], "XRL.World.Effects/Salve_Tonic.cs", "Remove", {"Popup": 1}),
            _family(family_ids["skulk_apply"], "XRL.World.Effects/Skulk_Tonic.cs", "Apply", {"Popup": 4}),
            _family(
                family_ids["skulk_overdose"],
                "XRL.World.Effects/Skulk_Tonic.cs",
                "ApplyOverdose",
                {"Popup": 3},
            ),
            _family(family_ids["skulk"], "XRL.World.Effects/Skulk_Tonic.cs", "Remove", {"Popup": 1}),
            _family(
                family_ids["sphynx_overdose"],
                "XRL.World.Effects/SphynxSalt_Tonic.cs",
                "ApplyOverdose",
                {"Popup": 4},
            ),
            _family(family_ids["sphynx"], "XRL.World.Effects/SphynxSalt_Tonic.cs", "Remove", {"Popup": 2}),
            _family(
                family_ids["shade_oil_overdose"],
                "XRL.World.Effects/ShadeOil_Tonic.cs",
                "ApplyOverdose",
                {"Popup": 8},
            ),
            _family(
                family_ids["ubernostrum_apply"],
                "XRL.World.Effects/Ubernostrum_Tonic.cs",
                "Apply",
                {"Popup": 3},
            ),
            _family(
                family_ids["ubernostrum_fire"],
                "XRL.World.Effects/Ubernostrum_Tonic.cs",
                "FireEvent",
                {"Popup": 4},
            ),
            _family(family_ids["ubernostrum"], "XRL.World.Effects/Ubernostrum_Tonic.cs", "Remove", {"Popup": 1}),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_key in (
        "blaze",
        "blaze_apply",
        "blaze_overdose",
        "hoarshroom_apply",
        "hoarshroom_overdose",
        "hoarshroom",
        "hulk_honey_apply",
        "hulk_honey_allergy",
        "hulk_honey_fire",
        "hulk_honey",
        "love_apply",
        "love_fire",
        "love",
        "rubbergum_apply",
        "rubbergum_allergy",
        "rubbergum_fire",
        "rubbergum",
        "salve_apply",
        "salve_fire",
        "salve",
        "skulk_apply",
        "skulk_overdose",
        "skulk",
        "sphynx_overdose",
        "sphynx",
        "shade_oil_overdose",
        "ubernostrum_apply",
        "ubernostrum_fire",
        "ubernostrum",
    ):
        assert entries[family_ids[family_key]]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_ids[family_key],
            "PopupShowTranslationPatch.cs",
            "PopupTranslationPatch.cs",
            "PopupShowTranslationPatchTests.cs",
            "PopupTranslationPatchTests.cs",
            "world-effects-tonics.ja.json",
        )

    assert entries[family_ids["ambient_reality"]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        family_ids["ambient_reality"],
        "PopupShowTranslationPatch.cs",
        "PopupShowTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )


def test_policy_records_issue719_haggling_sifrah_result_description_overlay() -> None:
    """Haggling Sifrah result descriptions close through exact owner postfix coverage."""
    family_ids = {
        "critical": "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultCriticalFailure()",
        "failure": "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultFailure()",
        "partial": "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultPartialSuccess()",
        "success": "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultSuccess()",
        "exceptional": "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultExceptionalSuccess()",
    }
    inventory = _inventory(
        [
            _family(family_id, "XRL.World/HagglingSifrah.cs", member, {"DescriptionAssignment": 1})
            for family_id, member in (
                (family_ids["critical"], "ResultCriticalFailure"),
                (family_ids["failure"], "ResultFailure"),
                (family_ids["partial"], "ResultPartialSuccess"),
                (family_ids["success"], "ResultSuccess"),
                (family_ids["exceptional"], "ResultExceptionalSuccess"),
            )
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "HagglingSifrahResultDescriptionTranslationPatch.cs",
            "HagglingSifrahResultDescriptionTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_policy_records_issue719_sifrah_token_description_overlay() -> None:
    """Selected Sifrah token description assignments close through exact owner postfix coverage."""
    families = {
        "liquid": (
            "XRL.World/TinkeringSifrahTokenLiquid.cs::TinkeringSifrahTokenLiquid.TinkeringSifrahTokenLiquid(string)",
            "XRL.World/TinkeringSifrahTokenLiquid.cs",
            "TinkeringSifrahTokenLiquid",
        ),
        "attribute_sacrifice": (
            "XRL.World/RitualSifrahTokenAttributeSacrifice.cs::"
            "RitualSifrahTokenAttributeSacrifice.RitualSifrahTokenAttributeSacrifice(string)",
            "XRL.World/RitualSifrahTokenAttributeSacrifice.cs",
            "RitualSifrahTokenAttributeSacrifice",
        ),
        "invoke_higher_being": (
            "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs::"
            "RitualSifrahTokenInvokeHigherBeing.SetBeing(Worshippable,List<Worshippable>)",
            "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs",
            "SetBeing",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member, {"DescriptionAssignment": 1})
            for family_id, source_file, member in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "SifrahTokenDescriptionTranslationPatch.cs",
            "SifrahTokenDescriptionTranslatorTests.cs",
            "SifrahTokenDescriptionTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_policy_records_issue719_sifrah_token_description_overlays() -> None:
    """Sifrah token description constructors close through exact token description coverage."""
    covered_families = {
        "psionic": (
            "XRL.World/PsionicSifrahTokenApplyAncientLore.cs::"
            "PsionicSifrahTokenApplyAncientLore.PsionicSifrahTokenApplyAncientLore()",
            "XRL.World/PsionicSifrahTokenApplyAncientLore.cs",
            "PsionicSifrahTokenApplyAncientLore",
        ),
        "ritual": (
            "XRL.World/RitualSifrahTokenEffectDazed.cs::"
            "RitualSifrahTokenEffectDazed.RitualSifrahTokenEffectDazed()",
            "XRL.World/RitualSifrahTokenEffectDazed.cs",
            "RitualSifrahTokenEffectDazed",
        ),
        "social": (
            "XRL.World/SocialSifrahTokenDisplayABarathrumiteToken.cs::"
            "SocialSifrahTokenDisplayABarathrumiteToken.SocialSifrahTokenDisplayABarathrumiteToken()",
            "XRL.World/SocialSifrahTokenDisplayABarathrumiteToken.cs",
            "SocialSifrahTokenDisplayABarathrumiteToken",
        ),
        "tinkering": (
            "XRL.World/TinkeringSifrahTokenCreationKnowledge.cs::"
            "TinkeringSifrahTokenCreationKnowledge.TinkeringSifrahTokenCreationKnowledge()",
            "XRL.World/TinkeringSifrahTokenCreationKnowledge.cs",
            "TinkeringSifrahTokenCreationKnowledge",
        ),
    }
    dynamic_overload = (
        "XRL.World/RitualSifrahTokenEffectDazed.cs::"
        "RitualSifrahTokenEffectDazed.RitualSifrahTokenEffectDazed(int)"
    )
    inventory = _inventory(
        [
            *[
                _family(family_id, source_file, member, {"DescriptionAssignment": 1})
                for family_id, source_file, member in covered_families.values()
            ],
            _family(
                dynamic_overload,
                "XRL.World/RitualSifrahTokenEffectDazed.cs",
                "RitualSifrahTokenEffectDazed",
                {"DescriptionAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _ in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "SifrahTokenDescriptionTranslationPatch.cs",
            "SifrahTokenDescriptionTranslatorTests.cs",
            "SifrahTokenDescriptionTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )
    assert entries[dynamic_overload]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        dynamic_overload,
        "SifrahTokenDescriptionTranslationPatch.cs",
        "SifrahTokenDescriptionTranslatorTests.cs",
        "SifrahTokenDescriptionTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )


def test_policy_records_issue719_preset_cooking_recipe_description_overlay() -> None:
    """Preset cooking recipe descriptions close through exact recipe owner coverage."""
    recipe_names = [
        "AppleMatz",
        "BoneBabka",
        "CloacaSurprise",
        "CrystalDelight",
        "GoatAndSweetLeaf",
        "HotandSpiny",
        "MahLahSoup",
        "MushroomCider",
        "ThePorridge",
        "TongueAndCheek",
    ]
    family_ids = {
        recipe_name: (f"XRL.World.Skills.Cooking/{recipe_name}.cs::{recipe_name}.GetDescription()")
        for recipe_name in recipe_names
    }
    inventory = _inventory(
        [
            _family(
                family_id,
                f"XRL.World.Skills.Cooking/{recipe_name}.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            )
            for recipe_name, family_id in family_ids.items()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id in family_ids.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "CookingEffectTranslationPatch.cs",
            "CookingEffectTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_policy_records_issue719_action_effect_description_return_overlay() -> None:
    """Small action/effect description returns close through exact owner postfix coverage."""
    families = {
        "kill": (
            "XRL.World.AI.GoalHandlers/Kill.cs::Kill.GetDetails()",
            "XRL.World.AI.GoalHandlers/Kill.cs",
            "GetDetails",
        ),
        "disassembly": (
            "XRL.World.Tinkering/Disassembly.cs::Disassembly.GetDescription()",
            "XRL.World.Tinkering/Disassembly.cs",
            "GetDescription",
        ),
        "ongoing": (
            "XRL/OngoingAction.cs::OngoingAction.GetDescription()",
            "XRL/OngoingAction.cs",
            "GetDescription",
        ),
        "metamorphed": (
            "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.GetDetails()",
            "XRL.World.Parts.Mutation/Metamorphed.cs",
            "GetDetails",
        ),
        "stinger": (
            "XRL.World.Parts/IStingerProperties.cs::IStingerProperties.GetDescription()",
            "XRL.World.Parts/IStingerProperties.cs",
            "GetDescription",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member, {"EffectDescriptionReturn": 1})
            for family_id, source_file, member in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "ActionEffectDescriptionReturnTranslationPatch.cs",
            "ActionEffectDescriptionReturnTranslatorTests.cs",
            "ActionEffectDescriptionReturnTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_policy_records_issue719_description_detail_return_overlay() -> None:
    """Description detail and game-object unit returns close through owner postfix coverage."""
    families = {
        "cybernetics_choice_description": (
            "XRL.CharacterBuilds.Qud/QudCyberneticsModule.cs::CyberneticsChoice.GetDescription()",
            "XRL.CharacterBuilds.Qud/QudCyberneticsModule.cs",
            "GetDescription",
        ),
        "cybernetics_choice_long_description": (
            "XRL.CharacterBuilds.Qud/QudCyberneticsModule.cs::CyberneticsChoice.GetLongDescription()",
            "XRL.CharacterBuilds.Qud/QudCyberneticsModule.cs",
            "GetLongDescription",
        ),
        "tinker_data_unclipped_description": (
            "XRL.World.Tinkering/TinkerData.cs::TinkerData.UnclippedDescription",
            "XRL.World.Tinkering/TinkerData.cs",
            "UnclippedDescription",
        ),
        "tinker_data_description": (
            "XRL.World.Tinkering/TinkerData.cs::TinkerData.Description",
            "XRL.World.Tinkering/TinkerData.cs",
            "Description",
        ),
        "game_object_cybernetics_unit": (
            "XRL.World.Units/GameObjectCyberneticsUnit.cs::GameObjectCyberneticsUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectCyberneticsUnit.cs",
            "GetDescription",
        ),
        "game_object_skill_unit": (
            "XRL.World.Units/GameObjectSkillUnit.cs::GameObjectSkillUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectSkillUnit.cs",
            "GetDescription",
        ),
        "game_object_relic_unit": (
            "XRL.World.Units/GameObjectRelicUnit.cs::GameObjectRelicUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectRelicUnit.cs",
            "GetDescription",
        ),
        "game_object_golem_quest_random_unit": (
            "XRL.World.Units/GameObjectGolemQuestRandomUnit.cs::GameObjectGolemQuestRandomUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectGolemQuestRandomUnit.cs",
            "GetDescription",
        ),
        "game_object_metachrome_unit": (
            "XRL.World.Units/GameObjectMetachromeUnit.cs::GameObjectMetachromeUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectMetachromeUnit.cs",
            "GetDescription",
        ),
        "game_object_body_part_unit": (
            "XRL.World.Units/GameObjectBodyPartUnit.cs::GameObjectBodyPartUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectBodyPartUnit.cs",
            "GetDescription",
        ),
        "game_object_experience_unit": (
            "XRL.World.Units/GameObjectExperienceUnit.cs::GameObjectExperienceUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectExperienceUnit.cs",
            "GetDescription",
        ),
        "game_object_mutation_unit": (
            "XRL.World.Units/GameObjectMutationUnit.cs::GameObjectMutationUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectMutationUnit.cs",
            "GetDescription",
        ),
        "game_object_baetyl_unit": (
            "XRL.World.Units/GameObjectBaetylUnit.cs::GameObjectBaetylUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectBaetylUnit.cs",
            "GetDescription",
        ),
        "game_object_clone_unit": (
            "XRL.World.Units/GameObjectCloneUnit.cs::GameObjectCloneUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectCloneUnit.cs",
            "GetDescription",
        ),
        "game_object_reputation_unit": (
            "XRL.World.Units/GameObjectReputationUnit.cs::GameObjectReputationUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectReputationUnit.cs",
            "GetDescription",
        ),
        "game_object_secret_unit": (
            "XRL.World.Units/GameObjectSecretUnit.cs::GameObjectSecretUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectSecretUnit.cs",
            "GetDescription",
        ),
        "game_object_unit": (
            "XRL.World.Units/GameObjectUnit.cs::GameObjectUnit.GetDescription(bool)",
            "XRL.World.Units/GameObjectUnit.cs",
            "GetDescription",
        ),
        "game_object_unit_aggregate": (
            "XRL.World.Units/GameObjectUnitAggregate.cs::GameObjectUnitAggregate.GetDescription(bool)",
            "XRL.World.Units/GameObjectUnitAggregate.cs",
            "GetDescription",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member, {"EffectDescriptionReturn": 1})
            for family_id, source_file, member in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "DescriptionDetailReturnTranslationPatch.cs",
            "DescriptionDetailReturnTranslatorTests.cs",
            "DescriptionDetailReturnTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_policy_records_issue719_active_effect_popup_queue_overlay() -> None:
    """Active-effect popup/queue split families close through scoped owner routes."""
    families = {
        "irisdual": (
            "XRL.World.Effects/IrisdualCallow.cs::IrisdualCallow.Apply(GameObject)",
            "XRL.World.Effects/IrisdualCallow.cs",
            "Apply",
            {"AddPlayerMessage": 10},
        ),
        "three_tongues": (
            "XRL.World.Effects/CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.cs::"
            "CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.Apply(GameObject)",
            "XRL.World.Effects/CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.cs",
            "Apply",
            {"AddPlayerMessage": 2},
        ),
        "shade_oil": (
            "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.FireEvent(Event)",
            "XRL.World.Effects/ShadeOil_Tonic.cs",
            "FireEvent",
            {"Popup": 14},
        ),
        "brain_brine": (
            "XRL.World.Effects/BrainBrineCurse.cs::BrainBrineCurse.FireEvent(Event)",
            "XRL.World.Effects/BrainBrineCurse.cs",
            "FireEvent",
            {"Popup": 11},
        ),
        "sphynx": (
            "XRL.World.Effects/SphynxSalt_Tonic.cs::SphynxSalt_Tonic.Apply(GameObject)",
            "XRL.World.Effects/SphynxSalt_Tonic.cs",
            "Apply",
            {"AddPlayerMessage": 1, "Popup": 7},
        ),
        "hobbled": (
            "XRL.World.Effects/Hobbled.cs::Hobbled.Apply(GameObject)",
            "XRL.World.Effects/Hobbled.cs",
            "Apply",
            {"MessageFrame": 7},
        ),
        "terrified": (
            "XRL.World.Effects/Terrified.cs::Terrified.Apply(GameObject)",
            "XRL.World.Effects/Terrified.cs",
            "Apply",
            {"MessageFrame": 7},
        ),
        "geometric_heal": (
            "XRL.World.Effects/GeometricHeal.cs::GeometricHeal.Apply(GameObject)",
            "XRL.World.Effects/GeometricHeal.cs",
            "Apply",
            {"MessageFrame": 6},
        ),
        "trance": (
            "XRL.World.Effects/Trance.cs::Trance.Apply(GameObject)",
            "XRL.World.Effects/Trance.cs",
            "Apply",
            {"MessageFrame": 6},
        ),
        "stinger_poisoned": (
            "XRL.World.Effects/StingerPoisoned.cs::StingerPoisoned.Apply(GameObject)",
            "XRL.World.Effects/StingerPoisoned.cs",
            "Apply",
            {"MessageFrame": 6},
        ),
        "furiously_confused": (
            "XRL.World.Effects/FuriouslyConfused.cs::FuriouslyConfused.Apply(GameObject)",
            "XRL.World.Effects/FuriouslyConfused.cs",
            "Apply",
            {"MessageFrame": 8},
        ),
        "confused": (
            "XRL.World.Effects/Confused.cs::Confused.Apply(GameObject)",
            "XRL.World.Effects/Confused.cs",
            "Apply",
            {"MessageFrame": 6},
        ),
        "poisoned": (
            "XRL.World.Effects/Poisoned.cs::Poisoned.Apply(GameObject)",
            "XRL.World.Effects/Poisoned.cs",
            "Apply",
            {"MessageFrame": 6},
        ),
        "phase_poisoned": (
            "XRL.World.Effects/PhasePoisoned.cs::PhasePoisoned.Apply(GameObject)",
            "XRL.World.Effects/PhasePoisoned.cs",
            "Apply",
            {"MessageFrame": 6},
        ),
        "poisoned_fire": (
            "XRL.World.Effects/Poisoned.cs::Poisoned.FireEvent(Event)",
            "XRL.World.Effects/Poisoned.cs",
            "FireEvent",
            {"MessageFrame": 9},
        ),
        "phase_poisoned_fire": (
            "XRL.World.Effects/PhasePoisoned.cs::PhasePoisoned.FireEvent(Event)",
            "XRL.World.Effects/PhasePoisoned.cs",
            "FireEvent",
            {"MessageFrame": 9},
        ),
        "ash_poison": (
            "XRL.World.Effects/AshPoison.cs::AshPoison.FireEvent(Event)",
            "XRL.World.Effects/AshPoison.cs",
            "FireEvent",
            {"MessageFrame": 4},
        ),
        "basilisk_poison": (
            "XRL.World.Effects/BasiliskPoison.cs::BasiliskPoison.FireEvent(Event)",
            "XRL.World.Effects/BasiliskPoison.cs",
            "FireEvent",
            {"AddPlayerMessage": 1, "MessageFrame": 7},
        ),
        "cripple_fire": (
            "XRL.World.Effects/Cripple.cs::Cripple.FireEvent(Event)",
            "XRL.World.Effects/Cripple.cs",
            "FireEvent",
            {"MessageFrame": 6},
        ),
        "poison_gas_poison": (
            "XRL.World.Effects/PoisonGasPoison.cs::PoisonGasPoison.FireEvent(Event)",
            "XRL.World.Effects/PoisonGasPoison.cs",
            "FireEvent",
            {"MessageFrame": 4},
        ),
        "luminous": (
            "XRL.World.Effects/Luminous.cs::Luminous.Apply(GameObject)",
            "XRL.World.Effects/Luminous.cs",
            "Apply",
            {"MessageFrame": 3},
        ),
        "meditating": (
            "XRL.World.Effects/Meditating.cs::Meditating.Apply(GameObject)",
            "XRL.World.Effects/Meditating.cs",
            "Apply",
            {"MessageFrame": 5},
        ),
        "scintillating": (
            "XRL.World.Effects/Scintillating.cs::Scintillating.Apply(GameObject)",
            "XRL.World.Effects/Scintillating.cs",
            "Apply",
            {"MessageFrame": 5},
        ),
        "suppressed": (
            "XRL.World.Effects/Suppressed.cs::Suppressed.Apply(GameObject)",
            "XRL.World.Effects/Suppressed.cs",
            "Apply",
            {"MessageFrame": 5},
        ),
        "shade_oil_apply": (
            "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.Apply(GameObject)",
            "XRL.World.Effects/ShadeOil_Tonic.cs",
            "Apply",
            {"MessageFrame": 4},
        ),
        "asleep_remove": (
            "XRL.World.Effects/Asleep.cs::Asleep.Remove(GameObject)",
            "XRL.World.Effects/Asleep.cs",
            "Remove",
            {"MessageFrame": 4},
        ),
        "healing": (
            "XRL.World.Effects/Healing.cs::Healing.Apply(GameObject)",
            "XRL.World.Effects/Healing.cs",
            "Apply",
            {"MessageFrame": 5},
        ),
        "dazed": (
            "XRL.World.Effects/Dazed.cs::Dazed.Apply(GameObject)",
            "XRL.World.Effects/Dazed.cs",
            "Apply",
            {"MessageFrame": 5},
        ),
        "paralyzed_apply": (
            "XRL.World.Effects/Paralyzed.cs::Paralyzed.Apply(GameObject)",
            "XRL.World.Effects/Paralyzed.cs",
            "Apply",
            {"MessageFrame": 5},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member, surfaces)
            for family_id, source_file, member, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "ActiveEffectPopupQueueTranslationPatch.cs",
            "ActiveEffectPopupQueueTranslatorTests.cs",
            "ActiveEffectPopupQueueTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_residual_bucket_payload_assigns_every_unreviewed_entry_to_a_followup_bucket() -> None:
    """Issue-719 residual output must turn raw unreviewed rows into an execution queue."""
    inventory = _inventory(
        [
            _family(
                "Qud.UI/OptionsLine.cs::OptionsLine.setData(object)",
                "Qud.UI/OptionsLine.cs",
                "setData",
                {"SetText": 1},
            ),
            _family(
                "XRL.UI/Popup.cs::Popup.PickSeveral(string,string[],bool)",
                "XRL.UI/Popup.cs",
                "PickSeveral",
                {"SetText": 1},
            ),
            _family(
                "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ItemNamingSifrah(GameObject)",
                "XRL.World/ItemNamingSifrah.cs",
                "ItemNamingSifrah",
                {"DescriptionAssignment": 1},
            ),
            _family(
                "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.ShowIntro()",
                "JoppaTutorial/FightSnapjaw.cs",
                "ShowIntro",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/GameObject.cs::GameObject.AutoEquip(GameObject)",
                "XRL.World.Parts/GameObject.cs",
                "AutoEquip",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/Miner.cs::Miner.Initialize()",
                "XRL.World.Parts/Miner.cs",
                "Initialize",
                {"ActivatedAbility": 1},
            ),
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["actionable_entries"] == 6
    assert payload["disposition_counts"] == {
        "likely_implementation_gap": 2,
        "runtime_evidence_required": 4,
    }
    assert payload["bucket_counts"] == {
        "activated_ability_misc_provider_gap": 1,
        "producer_broad_route_split": 1,
        "sifrah_description_token_dynamic_constructor_gap": 1,
        "tutorial_popup_runtime": 1,
        "ui_popup_sink_route_split": 1,
        "ui_screen_options_control_runtime": 1,
    }
    assert {entry["residual_bucket"] for entry in payload["entries"]} == set(payload["bucket_counts"])


def test_policy_promotes_game_object_heal_existing_owner_patch_from_broad_residuals() -> None:
    """GameObject.Heal is broad by file, but exact existing owner evidence closes it."""
    family_id = "XRL.World/GameObject.cs::GameObject.Heal(int,bool,bool,bool)"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World/GameObject.cs",
                "Heal",
                {"AddPlayerMessage": 2, "Does": 2, "Initializer": 2, "OtherInvocation": 1},
            ),
        ]
    )

    entry = valuable_surface_queue(inventory)[0]

    assert entry["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        {family_id: entry},
        family_id,
        "GameObjectHealTranslationPatch.cs",
        "MessageQueueSemanticPipeline.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "TargetMethodResolutionTests.cs",
        "static_producer_closure.py",
    )


def test_followup_issue_payload_groups_residual_buckets_into_consolidated_issue() -> None:
    """Issue-719 residual buckets are connected to the single consolidated tracker."""
    inventory = _inventory(
        [
            _family(
                "Qud.UI/OptionsLine.cs::OptionsLine.setData(object)",
                "Qud.UI/OptionsLine.cs",
                "setData",
                {"SetText": 1},
            ),
            _family(
                "XRL.UI/Popup.cs::Popup.PickSeveral(string,string[],bool)",
                "XRL.UI/Popup.cs",
                "PickSeveral",
                {"SetText": 1},
            ),
            _family(
                "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ItemNamingSifrah(GameObject)",
                "XRL.World/ItemNamingSifrah.cs",
                "ItemNamingSifrah",
                {"DescriptionAssignment": 1},
            ),
            _family(
                "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.ShowIntro()",
                "JoppaTutorial/FightSnapjaw.cs",
                "ShowIntro",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/GameObject.cs::GameObject.AutoEquip(GameObject)",
                "XRL.World.Parts/GameObject.cs",
                "AutoEquip",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/Miner.cs::Miner.Initialize()",
                "XRL.World.Parts/Miner.cs",
                "Initialize",
                {"ActivatedAbility": 1},
            ),
        ]
    )

    payload = followup_issue_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["actionable_entries"] == 6
    assert payload["track_counts"] == {"consolidated": 6}
    assert payload["issue_counts"] == {"issue719-consolidated-residuals": 6}
    assert list(payload["issues"]) == ["issue719-consolidated-residuals"]

    issue = payload["issues"]["issue719-consolidated-residuals"]
    assert issue["github_issue_number"] == 719
    assert issue["entry_count"] == 6
    assert issue["disposition_counts"] == {
        "likely_implementation_gap": 2,
        "runtime_evidence_required": 4,
    }
    assert {
        "activated_ability_misc_provider_gap",
        "producer_broad_route_split",
        "sifrah_description_token_dynamic_constructor_gap",
        "tutorial_popup_runtime",
        "ui_popup_sink_route_split",
        "ui_screen_options_control_runtime",
    } <= set(issue["buckets"])


def test_residual_bucket_payload_splits_tutorial_popup_routes_by_step_shape() -> None:
    """JoppaTutorial popup guard shapes are covered by popup/tutorial owner routes."""
    families = {
        "lateupdate": (
            "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.LateUpdate()",
            "JoppaTutorial/FightSnapjaw.cs",
            "LateUpdate",
            {"TutorialManagerPopup": 76},
            "tutorial_lateupdate_popup_gap",
        ),
        "command_guard": (
            "JoppaTutorial/FightBear.cs::FightBear.AllowCommand(string)",
            "JoppaTutorial/FightBear.cs",
            "AllowCommand",
            {"Popup": 31},
            "tutorial_command_guard_popup_gap",
        ),
        "snapjaw_command_guard": (
            "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.AllowCommand(string)",
            "JoppaTutorial/FightSnapjaw.cs",
            "AllowCommand",
            {"Popup": 21},
            "tutorial_command_guard_popup_gap",
        ),
        "bear_target_pick": (
            "JoppaTutorial/FightBear.cs::FightBear.AllowTargetPick(GameObject,Type,List<Cell>)",
            "JoppaTutorial/FightBear.cs",
            "AllowTargetPick",
            {"Popup": 3},
            "tutorial_command_guard_popup_gap",
        ),
        "battle_remains_inventory": (
            "JoppaTutorial/BattleRemains.cs::BattleRemains.AllowInventoryInteract(GameObject)",
            "JoppaTutorial/BattleRemains.cs",
            "AllowInventoryInteract",
            {"Popup": 1},
            "tutorial_command_guard_popup_gap",
        ),
        "cell_guard": (
            "JoppaTutorial/ExploreWorldMap.cs::ExploreWorldMap.BeforePlayerEnterCell(Cell)",
            "JoppaTutorial/ExploreWorldMap.cs",
            "BeforePlayerEnterCell",
            {"Popup": 3},
            "tutorial_cell_guard_popup_gap",
        ),
        "bear_cell_guard": (
            "JoppaTutorial/FightBear.cs::FightBear.BeforePlayerEnterCell(Cell)",
            "JoppaTutorial/FightBear.cs",
            "BeforePlayerEnterCell",
            {"Popup": 2},
            "tutorial_cell_guard_popup_gap",
        ),
        "snapjaw_cell_guard": (
            "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.BeforePlayerEnterCell(Cell)",
            "JoppaTutorial/FightSnapjaw.cs",
            "BeforePlayerEnterCell",
            {"Popup": 1},
            "tutorial_cell_guard_popup_gap",
        ),
        "chest_cell_guard": (
            "JoppaTutorial/MoveToChest.cs::MoveToChest.BeforePlayerEnterCell(Cell)",
            "JoppaTutorial/MoveToChest.cs",
            "BeforePlayerEnterCell",
            {"Popup": 1},
            "tutorial_cell_guard_popup_gap",
        ),
        "seen": (
            "JoppaTutorial/FightBear.cs::FightBear.BearSeen(Location2D)",
            "JoppaTutorial/FightBear.cs",
            "BearSeen",
            {"TutorialManagerPopup": 2},
            "tutorial_seen_popup_gap",
        ),
        "snapjaw_seen": (
            "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.SnapjawSeen(Location2D)",
            "JoppaTutorial/FightSnapjaw.cs",
            "SnapjawSeen",
            {"TutorialManagerPopup": 2},
            "tutorial_seen_popup_gap",
        ),
        "trigger": (
            "JoppaTutorial/MakeCamp.cs::MakeCamp.OnTrigger(string)",
            "JoppaTutorial/MakeCamp.cs",
            "OnTrigger",
            {"TutorialManagerPopup": 2},
            "tutorial_trigger_popup_gap",
        ),
        "chest_trigger": (
            "JoppaTutorial/MoveToChest.cs::MoveToChest.OnTrigger(string)",
            "JoppaTutorial/MoveToChest.cs",
            "OnTrigger",
            {"TutorialManagerPopup": 3},
            "tutorial_trigger_popup_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    assert entries[families["lateupdate"][0]]["closure_status"] == "covered_by_owner_route"
    lateupdate_evidence = " ".join(entries[families["lateupdate"][0]]["closure_evidence"])
    assert "TutorialManagerTranslationPatch" in lateupdate_evidence
    for family_id, _, _, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    assert "PopupShowTranslationPatch" in " ".join(entries[families["command_guard"][0]]["closure_evidence"])
    assert "TutorialManagerCellPopupTranslationPatch" in " ".join(entries[families["seen"][0]]["closure_evidence"])

    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-tutorial-popup-static-shapes-test.json"),
    )
    assert residual["entries"] == []


def test_followup_issue_payload_routes_edge_residual_buckets_without_keyerror() -> None:
    """Fallback residual buckets must still map to the consolidated issue."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Effects/Flux.cs::Flux.Apply(GameObject)",
                "XRL.World.Effects/Flux.cs",
                "Apply",
                {"Description": 1},
            ),
            _family(
                "XRL.World.Parts.Mutation/ResidualGas.cs::ResidualGas.FireEvent(Event)",
                "XRL.World.Parts.Mutation/ResidualGas.cs",
                "FireEvent",
                {"Description": 1},
            ),
        ]
    )

    payload = followup_issue_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["actionable_entries"] == 2
    assert payload["issue_counts"] == {"issue719-consolidated-residuals": 2}
    assert (
        "active_effect_non_description_route_split" in payload["issues"]["issue719-consolidated-residuals"]["buckets"]
    )
    assert "world_part_description_gap" in payload["issues"]["issue719-consolidated-residuals"]["buckets"]


def test_followup_bucket_mapping_covers_all_residual_bucket_emitters() -> None:
    """Every bucket emitted by residual classification must have an issue route."""
    emitted_buckets = {
        "action_description_autoact_gap",
        "action_description_runtime",
        "activated_ability_asset_bridge",
        "activated_ability_misc_provider_gap",
        "active_effect_message_frame_route_split",
        "active_effect_misc_route_split",
        "active_effect_non_description_route_split",
        "active_effect_popup_route_split",
        "active_effect_queue_route_split",
        "chargen_cybernetics_description_runtime",
        "cooking_description_route_split",
        "description_assignment_route_split",
        "description_detail_route_split",
        "effect_description_route_split",
        "game_object_unit_description_runtime",
        "generated_display_name_child_issue",
        "generated_display_name_core_faction_runtime",
        "generated_display_name_core_invalid_object_gap",
        "generated_display_name_core_metadata_runtime",
        "generated_display_name_core_possessive_gap",
        "generated_display_name_core_possessive_runtime",
        "generated_display_name_core_runtime",
        "generated_display_name_cooking_preset_recipe_gap",
        "generated_display_name_cooking_recipe_runtime",
        "generated_display_name_core_running_behavior_runtime",
        "generated_display_name_gap",
        "generated_display_name_mural_blank_slate_gap",
        "generated_display_name_mural_historic_event_gap",
        "generated_display_name_mural_runtime",
        "generated_display_name_mural_player_event_gap",
        "generated_display_name_mural_ruined_historic_gap",
        "generated_display_name_mutation_base_display_gap",
        "generated_display_name_mutation_effect_display_gap",
        "generated_display_name_mutation_light_manipulation_ability_gap",
        "generated_display_name_mutation_route_split",
        "generated_display_name_mutation_stat_shift_gap",
        "generated_display_name_mutation_temporal_fugue_copy_gap",
        "generated_display_name_stat_shift_gap",
        "generated_display_name_runtime",
        "generated_display_name_sultan_entity_gap",
        "generated_display_name_sultan_entity_runtime",
        "generated_display_name_village_dynamic_quest_reward_runtime",
        "generated_display_name_village_dynamic_quest_reward_gap",
        "generated_display_name_village_faction_gap",
        "generated_display_name_village_signature_dish_runtime",
        "generated_display_name_village_signature_item_runtime",
        "generated_display_name_world_part_cybernetics_recoiler_gap",
        "generated_display_name_world_part_cybernetics_skillsoft_gap",
        "generated_display_name_world_part_cybernetics_runtime",
        "generated_display_name_world_part_fixed_leaf_gap",
        "generated_display_name_world_part_figurine_gap",
        "generated_display_name_world_part_generated_object_runtime",
        "generated_display_name_world_part_hologram_gap",
        "generated_display_name_world_part_item_mod_runtime",
        "generated_display_name_world_part_pet_phylactery_gap",
        "generated_display_name_ui_cybernetics_install_gap",
        "generated_display_name_ui_object_finder_context_gap",
        "generated_display_name_ui_object_finder_sorter_gap",
        "generated_display_name_ui_runtime",
        "generated_display_name_world_part_route_split",
        "generated_display_name_world_part_statue_gap",
        "generated_display_name_world_part_tomb_cultist_gap",
        "generated_display_name_world_part_wish_debug_gap",
        "generated_display_name_world_part_wish_debug_runtime",
        "history_text_filter_speech_status_gap",
        "history_text_filter_speech_status_runtime",
        "misc_route_split",
        "conversation_book_line_data_runtime",
        "producer_broad_route_split",
        "producer_broad_gameobject_autoequip_runtime",
        "producer_broad_gameobject_death_runtime",
        "producer_broad_gameobject_destroy_gap",
        "producer_broad_gameobject_explode_death_gap",
        "producer_broad_gameobject_hostile_spot_gap",
        "producer_broad_gameobject_hostile_spot_runtime",
        "producer_broad_gameobject_inventory_companion_gap",
        "producer_broad_gameobject_pulldown_gap",
        "producer_broad_gameobject_regenera_runtime",
        "producer_broad_missile_trajectory_message_runtime",
        "producer_runtime_api_equipment_action_menu_gap",
        "producer_runtime_api_journal_wish_gospel_runtime",
        "producer_runtime_api_route_split",
        "producer_runtime_api_save_error_gap",
        "producer_runtime_capability_firefighting_gap",
        "producer_runtime_capability_item_naming_gap",
        "producer_runtime_capability_item_naming_wish_debug_gap",
        "producer_runtime_capability_route_split",
        "producer_runtime_core_coda_endgame_popup_gap",
        "producer_runtime_core_game_text_third_person_death_gap",
        "producer_runtime_core_generic_sink_runtime",
        "producer_runtime_core_mod_config_popup_gap",
        "producer_runtime_core_mod_failure_popup_gap",
        "producer_runtime_core_population_wish_popup_runtime",
        "producer_runtime_core_scores_popup_runtime",
        "producer_runtime_core_sound_debug_queue_runtime",
        "producer_runtime_core_system_route_split",
        "producer_runtime_conversation_api_reward_pick_gap",
        "producer_runtime_conversation_endgame_confirm_gap",
        "producer_runtime_conversation_give_artifact_gap",
        "producer_runtime_conversation_resheph_secret_gap",
        "producer_runtime_conversation_route_split",
        "producer_runtime_conversation_water_ritual_secret_gap",
        "producer_runtime_cybernetics_butcher_message_gap",
        "producer_runtime_cybernetics_cathedra_flight_popup_gap",
        "producer_runtime_cybernetics_force_lathe_activation_gap",
        "producer_runtime_cybernetics_force_lathe_replace_gap",
        "producer_runtime_cybernetics_holographic_visage_gap",
        "producer_runtime_cybernetics_low_level_hack_popup_gap",
        "producer_runtime_cybernetics_recoiler_popup_gap",
        "producer_runtime_cybernetics_route_split",
        "producer_message_family_audit",
        "producer_runtime_evidence_required",
        "producer_runtime_gameplay_route_split",
        "producer_runtime_inventory_action_does_popup_route_split",
        "producer_runtime_inventory_action_emit_route_split",
        "producer_runtime_inventory_action_crayons_popup_gap",
        "producer_runtime_inventory_action_description_look_popup_gap",
        "producer_runtime_inventory_action_grenade_detonate_popup_gap",
        "producer_runtime_inventory_action_inventory_drop_popup_gap",
        "producer_runtime_inventory_action_examiner_popup_gap",
        "producer_runtime_inventory_action_fixit_spray_popup_gap",
        "producer_runtime_inventory_action_magnetized_applicator_popup_gap",
        "producer_runtime_inventory_action_message_frame_popup_route_split",
        "producer_runtime_inventory_action_popup_route_split",
        "producer_runtime_inventory_action_route_split",
        "producer_runtime_inventory_action_tinker_item_popup_gap",
        "producer_runtime_inventory_action_vehicle_follower_popup_gap",
        "producer_runtime_liquid_glitch_components_gap",
        "producer_runtime_liquid_route_split",
        "producer_runtime_liquid_wish_warm_effect_gap",
        "producer_runtime_mutation_base_variant_popup_gap",
        "producer_runtime_mutation_carapace_loosen_gap",
        "producer_runtime_mutation_domination_failure_gap",
        "producer_runtime_mutation_route_split",
        "producer_runtime_mutation_sunder_mind_gap",
        "producer_runtime_mutation_temporal_fugue_gap",
        "producer_runtime_mutation_wings_flight_gap",
        "producer_runtime_quest_find_site_wish_debug_gap",
        "producer_runtime_quest_reward_choice_gap",
        "producer_runtime_quest_route_split",
        "producer_runtime_ui_chargen_build_library_add_gap",
        "producer_runtime_ui_chargen_build_library_import_gap",
        "producer_runtime_ui_chargen_build_library_manage_gap",
        "producer_runtime_ui_chargen_build_summary_gap",
        "producer_runtime_ui_chargen_gender_customize_gap",
        "producer_runtime_ui_chargen_mutation_menu_gap",
        "producer_runtime_ui_chargen_mutation_variant_gap",
        "producer_runtime_ui_chargen_popup_route_split",
        "producer_runtime_ui_chargen_validation_popup_gap",
        "producer_runtime_ui_inventory_trade_popup_route_split",
        "producer_runtime_ui_misc_popup_route_split",
        "producer_runtime_ui_options_command_binding_gap",
        "producer_runtime_ui_options_help_popup_gap",
        "producer_runtime_ui_options_legacy_popup_gap",
        "producer_runtime_ui_ability_manager_empty_gap",
        "producer_runtime_ui_factions_status_sort_gap",
        "producer_runtime_ui_inventory_status_options_gap",
        "producer_runtime_ui_options_popup_route_split",
        "producer_runtime_ui_route_split",
        "producer_runtime_ui_status_popup_route_split",
        "producer_runtime_ui_tutorial_popup_route_split",
        "producer_runtime_world_part_message_frame_route_split",
        "producer_runtime_world_part_does_emit_message_frame_route_split",
        "producer_runtime_world_part_does_emit_route_split",
        "producer_runtime_world_part_does_message_frame_route_split",
        "producer_runtime_world_part_does_popup_route_split",
        "producer_runtime_world_part_does_route_split",
        "producer_runtime_world_part_defibrillator_gap",
        "producer_runtime_world_part_emit_message_frame_popup_route_split",
        "producer_runtime_world_part_emit_popup_route_split",
        "producer_runtime_world_part_disguise_popup_gap",
        "producer_runtime_world_part_magazine_supply_gap",
        "producer_runtime_world_part_golem_popup_runtime",
        "producer_runtime_world_part_golem_mound_popup_gap",
            "producer_runtime_world_part_grip_recoil_popup_gap",
            "producer_runtime_world_part_heat_self_frame_gap",
            "producer_runtime_world_part_biome_distribution_queue_popup_gap",
            "producer_runtime_world_part_elevator_switch_queue_popup_gap",
            "producer_runtime_world_part_liquid_cleaning_frame_gap",
            "producer_runtime_world_part_liquid_contact_frame_gap",
            "producer_runtime_world_part_mixed_route_split",
            "producer_runtime_world_part_movement_popup_runtime",
            "producer_runtime_world_part_dance_opponent_debug_queue_gap",
            "producer_runtime_world_part_dance_opponent_register_queue_gap",
            "producer_runtime_world_part_campfire_extinguish_gap",
            "producer_runtime_world_part_interior_damage_queue_gap",
        "producer_runtime_world_part_harvestable_attempt_gap",
        "producer_runtime_world_part_chat_emit_gap",
        "producer_runtime_world_part_fungal_cure_emit_gap",
        "producer_runtime_world_part_player_dance_ritual_queue_gap",
        "producer_runtime_world_part_nephal_absorb_frame_gap",
            "producer_runtime_world_part_pet_recipe_frame_gap",
            "producer_runtime_world_part_pet_taunt_frame_gap",
            "producer_runtime_world_part_popup_message_frame_route_split",
            "producer_runtime_world_part_popup_route_split",
            "producer_runtime_world_part_pseudopod_death_frame_gap",
            "producer_runtime_world_part_queue_does_route_split",
            "producer_runtime_world_part_queue_popup_route_split",
            "producer_runtime_world_part_queue_route_split",
            "producer_runtime_world_part_route_split",
            "producer_runtime_world_part_ship_ark_popup_gap",
            "producer_runtime_world_part_shrine_popup_gap",
            "producer_runtime_world_part_shuttle_frame_gap",
        "producer_runtime_world_part_stomach_water_queue_popup_gap",
        "producer_runtime_world_part_tinkering_popup_gap",
        "producer_runtime_world_part_vehicle_infiltration_emit_gap",
        "producer_runtime_world_part_vortex_apply_gap",
        "producer_runtime_world_part_vortex_periodic_frame_gap",
            "producer_runtime_world_part_wish_debug_popup_gap",
            "producer_runtime_world_part_wish_debug_popup_runtime",
        "sifrah_description_route_split",
        "sifrah_popup_check_out_of_options_gap",
        "sifrah_popup_hacking_partial_success_gap",
        "sifrah_popup_route_split",
        "sifrah_popup_result_owner_gap",
        "sifrah_popup_secret_use_token_gap",
        "sifrah_popup_token_check_use_gap",
        "sifrah_popup_unused_base_game_runtime",
        "tutorial_cell_guard_popup_gap",
        "tutorial_command_guard_popup_gap",
        "tutorial_lateupdate_popup_gap",
        "tutorial_popup_runtime",
        "tutorial_seen_popup_gap",
        "tutorial_trigger_popup_gap",
        "ui_description_assignment_runtime",
        "ui_direct_text_gap",
        "ui_menu_option_static_description_gap",
        "ui_options_control_description_gap",
        "ui_popup_sink_route_split",
        "ui_screen_console_input_runtime",
        "ui_screen_cybernetics_terminal_runtime",
        "ui_screen_data_bound_runtime",
        "ui_screen_fixed_label_gap",
        "ui_screen_hotkey_control_runtime",
        "ui_screen_inventory_drag_numeric_runtime",
        "ui_screen_left_side_category_runtime",
        "ui_screen_missile_weapon_status_runtime",
        "ui_screen_mod_manager_back_button_runtime",
        "ui_screen_notification_runtime",
        "ui_screen_options_control_runtime",
        "ui_screen_popup_message_runtime",
        "ui_screen_progress_numeric_runtime",
        "ui_screen_route_runtime",
        "ui_screen_status_stat_runtime",
        "ui_screen_trade_drag_numeric_runtime",
        "ui_screen_trade_highlight_runtime",
        "ui_screen_trade_inventory_runtime",
        "ui_screen_world_generation_runtime",
        "world_part_description_gap",
        "world_zone_display_name_runtime",
    }

    assert emitted_buckets <= set(ISSUE719_FOLLOWUP_BY_BUCKET)


def test_policy_promotes_bucketed_issue719_residuals_out_of_unreviewed() -> None:
    """Bucketed #719 residuals are reviewed work queues, not raw unreviewed rows."""
    runtime_family_id = (
        "XRL.World.Effects/UnprovenDisplayName.cs::"
        "UnprovenDisplayName.UnprovenDisplayName()"
    )
    action_family_id = (
        "XRL.World/UnreviewedSifrahToken.cs::"
        "UnreviewedSifrahToken.UnreviewedSifrahToken()"
    )
    inventory = _inventory(
        [
            _family(
                runtime_family_id,
                "XRL.World.Effects/UnprovenDisplayName.cs",
                "UnprovenDisplayName",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                action_family_id,
                "XRL.World/UnreviewedSifrahToken.cs",
                "UnreviewedSifrahToken",
                {"DescriptionAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[runtime_family_id]["closure_status"] == "runtime_required"
    assert "generated_display_name_runtime" in " ".join(entries[runtime_family_id]["closure_evidence"])
    assert entries[action_family_id]["closure_status"] == "action_required"
    assert "sifrah_description_token_dynamic_constructor_gap" in " ".join(
        entries[action_family_id]["closure_evidence"]
    )


def test_policy_promotes_active_effect_display_names_from_existing_inventory() -> None:
    """Issue-719 active-effect DisplayName assignments use the existing effect inventory evidence."""
    fixed_family_id = "XRL.World.Effects/Burning.cs::Burning.Burning()"
    generated_family_id = "XRL.World.Effects/BoostStatistic.cs::BoostStatistic.BoostStatistic(int,string,int)"
    unknown_family_id = (
        "XRL.World.Effects/UnprovenDisplayName.cs::UnprovenDisplayName.UnprovenDisplayName()"
    )
    inventory = _inventory(
        [
            _family(fixed_family_id, "XRL.World.Effects/Burning.cs", "Burning", {"DisplayNameAssignment": 1}),
            _family(
                generated_family_id,
                "XRL.World.Effects/BoostStatistic.cs",
                "BoostStatistic",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                unknown_family_id,
                "XRL.World.Effects/UnprovenDisplayName.cs",
                "UnprovenDisplayName",
                {"DisplayNameAssignment": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[fixed_family_id]["closure_status"] == "covered_by_owner_route"
    assert entries[generated_family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[fixed_family_id]["closure_evidence"])
    assert "docs/active-effect-producer-inventory.json" in evidence
    assert "fixed-leaf translated" in evidence
    generated_evidence = " ".join(entries[generated_family_id]["closure_evidence"])
    assert "generated/composed route translated" in generated_evidence
    assert entries[unknown_family_id]["closure_status"] == "runtime_required"


def test_residual_bucket_payload_splits_remaining_generated_display_name_routes() -> None:
    """Remaining generated display names stay runtime-required but are grouped by owner shape."""
    inventory = _inventory(
        [
            _family(
                "XRL.UI.ObjectFinderSorters/IdSorter.cs::IdSorter.GetDisplayName()",
                "XRL.UI.ObjectFinderSorters/IdSorter.cs",
                "GetDisplayName",
                {"GetDisplayName": 1},
            ),
            _family(
                "XRL.World.Parts.Mutation/BaseMutation.cs::BaseMutation.GetDisplayName(bool)",
                "XRL.World.Parts.Mutation/BaseMutation.cs",
                "GetDisplayName",
                {"GetDisplayName": 1},
            ),
            _family(
                "XRL.World.Skills.Cooking/AppleMatz.cs::AppleMatz.GetDisplayName()",
                "XRL.World.Skills.Cooking/AppleMatz.cs",
                "GetDisplayName",
                {"GetDisplayName": 1},
            ),
            _family(
                "XRL.World.Parts/Miner.cs::Miner.SetupMinerConfiguration()",
                "XRL.World.Parts/Miner.cs",
                "SetupMinerConfiguration",
                {"DisplayNameAssignment": 1},
            ),
            _family(
                "XRL.World/GameObjectFactory.cs::GameObjectFactory.CreateObject(string)",
                "XRL.World/GameObjectFactory.cs",
                "CreateObject",
                {"DisplayNameAssignment": 1},
            ),
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["bucket_counts"] == {
        "generated_display_name_core_invalid_object_gap": 1,
    }
    assert payload["disposition_counts"] == {"likely_implementation_gap": 1}


def test_residual_bucket_payload_splits_ui_generated_display_names_by_owner_shape() -> None:
    """UI generated display-name residuals are fixed labels or exact option owners."""
    families = {
        "autogot_items": (
            "XRL.UI.ObjectFinderContexts/AutogotItems.cs::AutogotItems.GetDisplayName()",
            "XRL.UI.ObjectFinderContexts/AutogotItems.cs",
            "GetDisplayName",
            {"DisplayNameReturn": 1},
            "generated_display_name_ui_object_finder_context_gap",
        ),
        "nearby_items": (
            "XRL.UI.ObjectFinderContexts/NearbyItems.cs::NearbyItems.GetDisplayName()",
            "XRL.UI.ObjectFinderContexts/NearbyItems.cs",
            "GetDisplayName",
            {"DisplayNameReturn": 1},
            "generated_display_name_ui_object_finder_context_gap",
        ),
        "id_sorter": (
            "XRL.UI.ObjectFinderSorters/IdSorter.cs::IdSorter.GetDisplayName()",
            "XRL.UI.ObjectFinderSorters/IdSorter.cs",
            "GetDisplayName",
            {"DisplayNameReturn": 1},
            "generated_display_name_ui_object_finder_sorter_gap",
        ),
        "value_sorter": (
            "XRL.UI.ObjectFinderSorters/ValueSorter.cs::ValueSorter.GetDisplayName()",
            "XRL.UI.ObjectFinderSorters/ValueSorter.cs",
            "GetDisplayName",
            {"DisplayNameReturn": 1},
            "generated_display_name_ui_object_finder_sorter_gap",
        ),
        "cybernetics_install": (
            "XRL.UI/CyberneticsScreenInstall.cs::CyberneticsScreenInstall.OnUpdate()",
            "XRL.UI/CyberneticsScreenInstall.cs",
            "OnUpdate",
            {"DisplayNameAssignment": 1},
            "generated_display_name_ui_cybernetics_install_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-ui-display-name-test.json"))

    for key in ("autogot_items", "nearby_items", "id_sorter", "value_sorter"):
        family_id = families[key][0]
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "ObjectFinderDisplayNameTranslationPatch.cs",
            "ObjectFinderDisplayNameTranslationPatchTests.cs",
        )
    assert entries[families["cybernetics_install"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["cybernetics_install"][0],
        "CyberneticsTerminalTextTranslator.cs",
        "CyberneticsTerminalTextTranslationPatchTests.cs",
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {}
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_mutation_generated_display_names_by_owner_shape() -> None:
    """Mutation generated display-name residuals become specific implementation gaps."""
    families = {
        "base_display": (
            "XRL.World.Parts.Mutation/BaseMutation.cs::BaseMutation.GetDisplayName(bool)",
            "XRL.World.Parts.Mutation/BaseMutation.cs",
            "GetDisplayName",
            {"DisplayNameReturn": 2},
            "generated_display_name_mutation_base_display_gap",
        ),
        "entry_display": (
            "XRL/MutationEntry.cs::MutationEntry.GetDisplayName(bool)",
            "XRL/MutationEntry.cs",
            "GetDisplayName",
            {"DisplayNameReturn": 1},
            "generated_display_name_mutation_base_display_gap",
        ),
        "temporal_fugue": (
            "XRL.World.Parts.Mutation/TemporalFugue.cs::"
            "TemporalFugue.CreateFugueCopyOf(GameObject,GameObject,Cell,GameObject,bool,int,int,string,string,"
            "string,string,string,IPart)",
            "XRL.World.Parts.Mutation/TemporalFugue.cs",
            "CreateFugueCopyOf",
            {"DisplayNameAssignment": 18},
            "generated_display_name_mutation_temporal_fugue_copy_gap",
        ),
        "photosynthetic_skin": (
            "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs::PhotosyntheticSkin.CheckCamouflage()",
            "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs",
            "CheckCamouflage",
            {"DisplayNameAssignment": 2},
            "generated_display_name_mutation_stat_shift_gap",
        ),
        "light_manipulation": (
            "XRL.World.Parts.Mutation/LightManipulation.cs::LightManipulation.SyncAbilityName()",
            "XRL.World.Parts.Mutation/LightManipulation.cs",
            "SyncAbilityName",
            {"DisplayNameAssignment": 1},
            "generated_display_name_mutation_light_manipulation_ability_gap",
        ),
        "metamorphed": (
            "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.Metamorphed()",
            "XRL.World.Parts.Mutation/Metamorphed.cs",
            "Metamorphed",
            {"DisplayNameAssignment": 1},
            "generated_display_name_mutation_effect_display_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-mutation-display-name-test.json"))

    covered_families = {
        families["base_display"][0],
        families["entry_display"][0],
        families["light_manipulation"][0],
        families["metamorphed"][0],
        families["photosynthetic_skin"][0],
        families["temporal_fugue"][0],
    }
    for family_id in covered_families:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    assert {
        entry["closure_status"]
        for family_id, entry in entries.items()
        if family_id not in covered_families
    } == set()
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, _, bucket in families.values()
        if family_id not in covered_families
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_core_generated_display_names_by_owner_shape() -> None:
    """Core display-name residuals separate helper, dynamic metadata, and invalid-object fallbacks."""
    families = {
        "possessive_upper": (
            "XRL.World/GameObject.cs::GameObject.Poss(GameObject,bool,bool?)",
            "XRL.World/GameObject.cs",
            "GameObject",
            "Poss",
            "generated_display_name_core_possessive_gap",
            "covered_by_owner_route",
        ),
        "possessive_lower": (
            "XRL.World/GameObject.cs::GameObject.poss(GameObject,bool,bool?)",
            "XRL.World/GameObject.cs",
            "GameObject",
            "poss",
            "generated_display_name_core_possessive_gap",
            "covered_by_owner_route",
        ),
        "running_behavior": (
            "XRL.World/GetRunningBehaviorEvent.cs::"
            "GetRunningBehaviorEvent.Retrieve(GameObject,out string,out string,out string,"
            "out string,out int,out bool,Templates.StatCollector)",
            "XRL.World/GetRunningBehaviorEvent.cs",
            "GetRunningBehaviorEvent",
            "Retrieve",
            "generated_display_name_core_running_behavior_runtime",
            "covered_by_owner_route",
        ),
        "factory_full": (
            "XRL.World/GameObjectFactory.cs::"
            "GameObjectFactory.CreateObject(string,int,int,string,Action<GameObject>,Action<GameObject>,string,List<GameObject>)",
            "XRL.World/GameObjectFactory.cs",
            "GameObjectFactory",
            "CreateObject",
            "generated_display_name_core_invalid_object_gap",
            "covered_by_owner_route",
        ),
        "factory_simple": (
            "XRL.World/GameObjectFactory.cs::GameObjectFactory.CreateObject(string,Action<GameObject>)",
            "XRL.World/GameObjectFactory.cs",
            "GameObjectFactory",
            "CreateObject",
            "generated_display_name_core_invalid_object_gap",
            "covered_by_owner_route",
        ),
        "cached_object": (
            "XRL.World/ZoneManager.cs::ZoneManager.GetCachedObjects(string)",
            "XRL.World/ZoneManager.cs",
            "ZoneManager",
            "GetCachedObjects",
            "generated_display_name_core_invalid_object_gap",
            "covered_by_owner_route",
        ),
        "faction": (
            "XRL.World/Faction.cs::Faction.DisplayName",
            "XRL.World/Faction.cs",
            "Faction",
            "DisplayName",
            "generated_display_name_core_faction_covered",
            "covered_by_owner_route",
        ),
        "effect": (
            "XRL.World/Effect.cs::Effect.Effect()",
            "XRL.World/Effect.cs",
            "Effect",
            "Effect",
            "generated_display_name_core_metadata_covered",
            "covered_by_owner_route",
        ),
        "poi": (
            "XRL.World/PointOfInterest.cs::PointOfInterest.DisplayName",
            "XRL.World/PointOfInterest.cs",
            "PointOfInterest",
            "DisplayName",
            "generated_display_name_core_metadata_covered",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"DisplayNameAssignment": 1})
            for family_id, source_file, _, member_name, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, _, disposition in families.values():
        if disposition == "covered_by_owner_route":
            expected_status = "covered_by_owner_route"
        else:
            expected_status = "action_required" if disposition == "likely_implementation_gap" else "runtime_required"
        assert entries[family_id]["closure_status"] == expected_status
    assert "event bridge" in " ".join(entries[families["running_behavior"][0]]["closure_evidence"])
    _assert_evidence_contains(
        entries,
        families["factory_full"][0],
        "CoreInvalidObjectDisplayNameTranslationPatch.cs",
        "CoreInvalidObjectDisplayNameTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        families["cached_object"][0],
        "CoreInvalidObjectDisplayNameTranslationPatch.cs",
        "CoreInvalidObjectDisplayNameTranslationPatchTests.cs",
    )

    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-core-display-name-shapes-test.json"),
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_residual_bucket_payload_promotes_cooking_preset_display_names_to_owner_route() -> None:
    """Preset cooking recipe display-name overrides are covered at the owner route."""
    families = {
        "apple_matz": (
            "XRL.World.Skills.Cooking/AppleMatz.cs::AppleMatz.GetDisplayName()",
            "XRL.World.Skills.Cooking/AppleMatz.cs",
            "AppleMatz",
        ),
        "bone_babka": (
            "XRL.World.Skills.Cooking/BoneBabka.cs::BoneBabka.GetDisplayName()",
            "XRL.World.Skills.Cooking/BoneBabka.cs",
            "BoneBabka",
        ),
        "cloaca_surprise": (
            "XRL.World.Skills.Cooking/CloacaSurprise.cs::CloacaSurprise.GetDisplayName()",
            "XRL.World.Skills.Cooking/CloacaSurprise.cs",
            "CloacaSurprise",
        ),
        "crystal_delight": (
            "XRL.World.Skills.Cooking/CrystalDelight.cs::CrystalDelight.GetDisplayName()",
            "XRL.World.Skills.Cooking/CrystalDelight.cs",
            "CrystalDelight",
        ),
        "goat_sweet_leaf": (
            "XRL.World.Skills.Cooking/GoatAndSweetLeaf.cs::GoatAndSweetLeaf.GetDisplayName()",
            "XRL.World.Skills.Cooking/GoatAndSweetLeaf.cs",
            "GoatAndSweetLeaf",
        ),
        "hot_spiny": (
            "XRL.World.Skills.Cooking/HotandSpiny.cs::HotandSpiny.GetDisplayName()",
            "XRL.World.Skills.Cooking/HotandSpiny.cs",
            "HotandSpiny",
        ),
        "mah_lah": (
            "XRL.World.Skills.Cooking/MahLahSoup.cs::MahLahSoup.GetDisplayName()",
            "XRL.World.Skills.Cooking/MahLahSoup.cs",
            "MahLahSoup",
        ),
        "mushroom_cider": (
            "XRL.World.Skills.Cooking/MushroomCider.cs::MushroomCider.GetDisplayName()",
            "XRL.World.Skills.Cooking/MushroomCider.cs",
            "MushroomCider",
        ),
        "porridge": (
            "XRL.World.Skills.Cooking/ThePorridge.cs::ThePorridge.GetDisplayName()",
            "XRL.World.Skills.Cooking/ThePorridge.cs",
            "ThePorridge",
        ),
        "tongue_cheek": (
            "XRL.World.Skills.Cooking/TongueAndCheek.cs::TongueAndCheek.GetDisplayName()",
            "XRL.World.Skills.Cooking/TongueAndCheek.cs",
            "TongueAndCheek",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, "GetDisplayName", {"DisplayNameReturn": 1})
            for family_id, source_file, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "CookingRecipeDisplayNameTranslationPatch.cs",
            "CookingRecipeDisplayNameTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
            "ui-popup-campfire-preset-meals.ja.json",
        )

    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-cooking-preset-display-name-test.json"),
    )
    assert residual["actionable_entries"] == 0


def test_residual_bucket_payload_splits_world_part_generated_display_names_by_owner_shape() -> None:
    """World-part display-name residuals separate fixed labels from generated object routes."""
    families = {
        "bey_lah_terrain": (
            "XRL.World.Parts/BeyLahTerrain.cs::BeyLahTerrain.FireEvent(Event)",
            "XRL.World.Parts/BeyLahTerrain.cs",
            "FireEvent",
            "generated_display_name_world_part_fixed_leaf_gap",
            "covered_by_owner_route",
        ),
        "hydropon_terrain": (
            "XRL.World.Parts/HydroponTerrain.cs::HydroponTerrain.FireEvent(Event)",
            "XRL.World.Parts/HydroponTerrain.cs",
            "FireEvent",
            "generated_display_name_world_part_fixed_leaf_gap",
            "covered_by_owner_route",
        ),
        "molting_basilisk": (
            "XRL.World.Parts/MoltingBasilisk.cs::MoltingBasilisk.SyncState()",
            "XRL.World.Parts/MoltingBasilisk.cs",
            "SyncState",
            "generated_display_name_world_part_fixed_leaf_gap",
            "covered_by_owner_route",
        ),
        "miner_fixed_leaf": (
            "XRL.World.Parts/Miner.cs::Miner.SetupMinerConfiguration()",
            "XRL.World.Parts/Miner.cs",
            "SetupMinerConfiguration",
            "generated_display_name_world_part_fixed_leaf_gap",
            "covered_by_owner_route",
        ),
        "rocket_skates": (
            "XRL.World.Parts/RocketSkates.cs::RocketSkates.HandleEvent(GetRunningBehaviorEvent)",
            "XRL.World.Parts/RocketSkates.cs",
            "HandleEvent",
            "generated_display_name_world_part_fixed_leaf_gap",
            "covered_by_owner_route",
        ),
        "yurtmat_stat_shift": (
            "XRL.World.Parts/Yurtmat.cs::Yurtmat.CheckCamouflage()",
            "XRL.World.Parts/Yurtmat.cs",
            "CheckCamouflage",
            "generated_display_name_stat_shift_gap",
            "covered_by_owner_route",
        ),
        "co_processor_stat_shift": (
            "XRL.World.Parts/ModCoProcessor.cs::ModCoProcessor.ApplyBonus(GameObject)",
            "XRL.World.Parts/ModCoProcessor.cs",
            "ApplyBonus",
            "generated_display_name_stat_shift_gap",
            "covered_by_owner_route",
        ),
        "cybernetics_skillsoft": (
            "XRL.World.Parts/CyberneticsSingleSkillsoft.cs::CyberneticsSingleSkillsoft.InitChip(bool)",
            "XRL.World.Parts/CyberneticsSingleSkillsoft.cs",
            "InitChip",
            "generated_display_name_world_part_cybernetics_skillsoft_gap",
            "covered_by_owner_route",
        ),
        "cybernetics_recoiler": (
            "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs::"
            "CyberneticsOnboardRecoilerImprinting.UpdateName()",
            "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs",
            "UpdateName",
            "generated_display_name_world_part_cybernetics_recoiler_gap",
            "covered_by_owner_route",
        ),
        "generated_object": (
            "XRL.World.Parts/RandomStatue.cs::RandomStatue.SetCreature(GameObject)",
            "XRL.World.Parts/RandomStatue.cs",
            "SetCreature",
            "generated_display_name_world_part_statue_gap",
            "covered_by_owner_route",
        ),
        "item_mod": (
            "XRL.World.Parts/PhaseSticky.cs::PhaseSticky.HandleEvent(RealityStabilizeEvent)",
            "XRL.World.Parts/PhaseSticky.cs",
            "HandleEvent",
            "generated_display_name_world_part_item_mod_covered",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"DisplayNameAssignment": 1})
            for family_id, source_file, member_name, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, disposition in families.values():
        expected_status = (
            "covered_by_owner_route"
            if disposition == "covered_by_owner_route"
            else "action_required"
            if disposition == "likely_implementation_gap"
            else "runtime_required"
        )
        assert entries[family_id]["closure_status"] == expected_status
    _assert_evidence_contains(
        entries,
        families["miner_fixed_leaf"][0],
        "GetDisplayNameRouteTranslator.cs",
        "GetDisplayNameRouteTranslatorTests.cs",
        "GetDisplayNameProcessPatchTests.cs",
        "Miner.SetupMinerConfiguration",
    )
    _assert_evidence_contains(
        entries,
        families["cybernetics_recoiler"][0],
        "ActivatedAbilityNameTranslator.cs",
        "ActivatedAbilityNameTranslatorTests.cs",
        "Recoil to",
    )

    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-display-name-shapes-test.json"),
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_residual_bucket_payload_splits_cybernetics_generated_display_names_by_owner_shape() -> None:
    """Cybernetics generated display-name rows are exact owner implementation gaps."""
    families = {
        "recoiler": (
            "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs::"
            "CyberneticsOnboardRecoilerImprinting.UpdateName()",
            "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs",
            "UpdateName",
            {"DisplayNameAssignment": 2},
            "generated_display_name_world_part_cybernetics_recoiler_gap",
        ),
        "single_skillsoft": (
            "XRL.World.Parts/CyberneticsSingleSkillsoft.cs::CyberneticsSingleSkillsoft.InitChip(bool)",
            "XRL.World.Parts/CyberneticsSingleSkillsoft.cs",
            "InitChip",
            {"DisplayNameAssignment": 1},
            "generated_display_name_world_part_cybernetics_skillsoft_gap",
        ),
        "tree_skillsoft": (
            "XRL.World.Parts/CyberneticsTreeSkillsoft.cs::CyberneticsTreeSkillsoft.InitChip(bool,bool,double)",
            "XRL.World.Parts/CyberneticsTreeSkillsoft.cs",
            "InitChip",
            {"DisplayNameAssignment": 1},
            "generated_display_name_world_part_cybernetics_skillsoft_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-cybernetics-display-name-test.json"),
    )

    assert residual["entries"] == []
    for key in ("recoiler", "single_skillsoft", "tree_skillsoft"):
        family_id = families[key][0]
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["recoiler"][0],
        "ActivatedAbilityNameTranslator.cs",
        "ActivatedAbilityNameTranslatorTests.cs",
        "Recoil to",
    )
    for key in ("single_skillsoft", "tree_skillsoft"):
        family_id = families[key][0]
        _assert_evidence_contains(
            entries,
            family_id,
            "GetDisplayNameRouteTranslator.cs",
            "GetDisplayNameRouteTranslatorTests.cs",
            "Skillsoft",
        )


def test_residual_bucket_payload_splits_world_part_generated_object_display_names_by_owner_shape() -> None:
    """Generated object display-name residuals separate implementable owners from wish-only routes."""
    families = {
        "figurine": (
            "XRL.World.Parts/RandomFigurine.cs::RandomFigurine.HandleEvent(ObjectCreatedEvent)",
            "XRL.World.Parts/RandomFigurine.cs",
            "HandleEvent",
            {"DisplayNameAssignment": 12},
            "generated_display_name_world_part_figurine_gap",
            "covered_by_owner_route",
        ),
        "pet_phylactery": (
            "XRL.World.Parts/PetPhylactery.cs::PetPhylactery.HandleEvent(AfterObjectCreatedEvent)",
            "XRL.World.Parts/PetPhylactery.cs",
            "HandleEvent",
            {"DisplayNameAssignment": 4},
            "generated_display_name_world_part_pet_phylactery_gap",
            "covered_by_owner_route",
        ),
        "wish_asterisk": (
            "XRL.World.Parts/PointedAsteriskBuilder.cs::PointedAsteriskBuilder.AsteriskWish()",
            "XRL.World.Parts/PointedAsteriskBuilder.cs",
            "AsteriskWish",
            {"DisplayNameAssignment": 4},
            "generated_display_name_world_part_wish_debug_gap",
            "covered_by_owner_route",
        ),
        "statue": (
            "XRL.World.Parts/RandomStatue.cs::RandomStatue.SetCreature(GameObject)",
            "XRL.World.Parts/RandomStatue.cs",
            "SetCreature",
            {"DisplayNameAssignment": 4},
            "generated_display_name_world_part_statue_gap",
            "covered_by_owner_route",
        ),
        "hologram": (
            "XRL.World.Parts/ModQuantumReverb.cs::ModQuantumReverb.CreateHologramOf(GameObject)",
            "XRL.World.Parts/ModQuantumReverb.cs",
            "CreateHologramOf",
            {"DisplayNameAssignment": 3},
            "generated_display_name_world_part_hologram_gap",
            "covered_by_owner_route",
        ),
        "tomb_cultist": (
            "XRL.World.Parts/TombCultistTemplate.cs::TombCultistTemplate.Apply(GameObject,HistoricEntitySnapshot)",
            "XRL.World.Parts/TombCultistTemplate.cs",
            "Apply",
            {"DisplayNameAssignment": 3},
            "generated_display_name_world_part_tomb_cultist_gap",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-world-part-generated-object-test.json"))

    for family_id, _, _, _, _, disposition in families.values():
        if disposition == "covered_by_owner_route":
            expected_status = "covered_by_owner_route"
        else:
            expected_status = "action_required" if disposition == "likely_implementation_gap" else "runtime_required"
        assert entries[family_id]["closure_status"] == expected_status
    for key in ("pet_phylactery", "statue", "hologram", "tomb_cultist"):
        _assert_evidence_contains(
            entries,
            families[key][0],
            "WorldPartGeneratedDisplayNameTranslationPatches.cs",
            "WorldPartGeneratedDisplayNameTranslationPatchTests.cs",
        )
    _assert_evidence_contains(
        entries,
        families["figurine"][0],
        "ObjectBlueprints/Items.jp.xml",
        "LocalizationCoverageTests.cs",
    )
    _assert_evidence_contains(
        entries,
        families["wish_asterisk"][0],
        "ui-displayname-atomic.ja.json",
        "LocalizationCoverageTests.cs",
        "The 10-Pointed Asterisk of the Ensemble",
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_residual_bucket_payload_splits_generated_display_name_child_routes_by_owner_shape() -> None:
    """Child generated-name buckets keep exact owner shapes visible."""
    families = {
        "mural": (
            "XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.blankMural(List<Location2D>)",
            "XRL.World.Parts/PlayerMuralController.cs",
            "blankMural",
            "generated_display_name_mural_blank_slate_gap",
            "covered_by_owner_route",
        ),
        "sultan_entity": (
            "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateSultanEntity(GameObject)",
            "XRL.World.ZoneBuilders/VillageCoda.cs",
            "GenerateSultanEntity",
            "generated_display_name_sultan_entity_gap",
            "covered_by_owner_route",
        ),
        "village_faction": (
            "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.CreateVillageFaction(HistoricEntitySnapshot)",
            "XRL.World.ZoneBuilders/VillageBase.cs",
            "CreateVillageFaction",
            "generated_display_name_village_faction_gap",
            "covered_by_owner_route",
        ),
        "signature_dish": (
            "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.generateSignatureDish(string)",
            "XRL.World.ZoneBuilders/VillageBase.cs",
            "generateSignatureDish",
            "generated_display_name_village_signature_dish_runtime",
            "covered_by_owner_route",
        ),
        "signature_item": (
            "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.generateSignatureItems()",
            "XRL.World.ZoneBuilders/VillageBase.cs",
            "generateSignatureItems",
            "generated_display_name_village_signature_item_gap",
            "covered_by_owner_route",
        ),
        "dynamic_quest_reward": (
            "XRL.World/VillageDynamicQuestContext.cs::VillageDynamicQuestContext.getQuestReward()",
            "XRL.World/VillageDynamicQuestContext.cs",
            "getQuestReward",
            "generated_display_name_village_dynamic_quest_reward_gap",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"DisplayNameAssignment": 1})
            for family_id, source_file, member_name, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-generated-display-name-child-shapes-test.json"),
    )

    assert entries[families["signature_dish"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["signature_dish"][0],
        "CookingRecipeDisplayNameTranslationPatch.cs",
        "generateSignatureDish",
        "CookingRecipe.GetDisplayName",
    )
    assert "generated display-name owner patch covers direct producer assignments" in " ".join(
        entries[families["village_faction"][0]]["closure_evidence"]
    )
    _assert_evidence_contains(
        entries,
        families["dynamic_quest_reward"][0],
        "GeneratedDisplayNameOwnerTranslationPatch.cs",
        "GeneratedDisplayNameOwnerTranslationPatchTests.cs",
    )
    _assert_evidence_contains(
        entries,
        families["mural"][0],
        "ui-displayname-atomic.ja.json",
        "GetDisplayNameRouteTranslatorTests.cs",
    )
    _assert_evidence_contains(
        entries,
        families["sultan_entity"][0],
        "GetDisplayNameRouteTranslator.cs",
        "GetDisplayNameRouteTranslatorTests.cs",
        "VillageCoda.cs",
    )
    assert payload["bucket_counts"] == {
        bucket: 1
        for _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }
    assert payload["disposition_counts"] == {}
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_residual_bucket_payload_promotes_village_signature_dishes_to_cooking_display_route() -> None:
    """Village signature-dish rows are covered by the existing recipe display-name owner route."""
    families = {
        "base": (
            "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.generateSignatureDish(string)",
            "XRL.World.ZoneBuilders/VillageBase.cs",
            "generateSignatureDish",
            {"DisplayNameAssignment": 14},
        ),
        "coda": (
            "XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.generateSignatureDish(string)",
            "XRL.World.ZoneBuilders/VillageCodaBase.cs",
            "generateSignatureDish",
            {"DisplayNameAssignment": 14},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-village-signature-dish-test.json"),
    )

    assert residual["actionable_entries"] == 0
    for family_id, _, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "CookingRecipeDisplayNameTranslationPatch.cs",
            "CookingRecipe.GetDisplayName",
            "generateSignatureDish",
        )


def test_policy_promotes_village_signature_items_to_owner_route() -> None:
    """Village signature-item generated display names are closed by the owner patch."""
    families = {
        "base": (
            "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.generateSignatureItems()",
            "XRL.World.ZoneBuilders/VillageBase.cs",
            "generateSignatureItems",
            {"Assignment": 4, "DisplayNameAssignment": 1},
        ),
        "coda": (
            "XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.generateSignatureItems()",
            "XRL.World.ZoneBuilders/VillageCodaBase.cs",
            "generateSignatureItems",
            {"Assignment": 4, "DisplayNameAssignment": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-village-signature-item-test.json"),
    )

    assert residual["bucket_counts"] == {}
    assert residual["disposition_counts"] == {}
    assert residual["entries"] == []
    for family_id, _, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "VillageSignatureItemTranslationPatch.cs",
            "VillageSignatureItemTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )


def test_residual_bucket_payload_promotes_village_coda_generated_names_to_display_routes() -> None:
    """VillageCoda generated display names are covered by existing display-name routes."""
    families = {
        "sultan": (
            "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateSultanEntity(GameObject)",
            "GenerateSultanEntity",
            {"JournalAPI": 12, "DisplayNameAssignment": 10},
        ),
        "statue": (
            "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.SetStatueVisuals(GameObject)",
            "SetStatueVisuals",
            {"DisplayNameAssignment": 8},
        ),
        "golem": (
            "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateMechanicalGolem()",
            "GenerateMechanicalGolem",
            {"DisplayNameAssignment": 3},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, "XRL.World.ZoneBuilders/VillageCoda.cs", member_name, surfaces)
            for family_id, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-village-coda-generated-names-test.json"),
    )

    for family_id, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "GetDisplayNameRouteTranslator.cs",
            "GetDisplayNameRouteTranslatorTests.cs",
            "VillageCoda.cs",
        )
    assert payload["actionable_entries"] == 0


def test_residual_bucket_payload_splits_mural_generated_display_names_by_owner_shape() -> None:
    """Mural display-name and description assignments have exact static owner routes."""
    families = {
        "player_blank": (
            "XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.blankMural(List<Location2D>)",
            "XRL.World.Parts/PlayerMuralController.cs",
            "blankMural",
            "generated_display_name_mural_blank_slate_gap",
        ),
        "sultan_blank": (
            "XRL.World.Parts/SultanMuralController.cs::SultanMuralController.blankMural(List<Cell>)",
            "XRL.World.Parts/SultanMuralController.cs",
            "blankMural",
            "generated_display_name_mural_blank_slate_gap",
        ),
        "player_event": (
            "XRL.World.Parts/PlayerMuralController.cs::"
            "PlayerMuralController.updatePlayerMural(List<Location2D>,JournalAccomplishment,int)",
            "XRL.World.Parts/PlayerMuralController.cs",
            "updatePlayerMural",
            "generated_display_name_mural_player_event_gap",
        ),
        "historic_event": (
            "XRL.World.Parts/SultanMuralController.cs::"
            "SultanMuralController.updateHistoricMural(List<Cell>,HistoricEvent)",
            "XRL.World.Parts/SultanMuralController.cs",
            "updateHistoricMural",
            "generated_display_name_mural_historic_event_gap",
        ),
        "ruined_historic": (
            "XRL.World.Parts/SultanMuralController.cs::"
            "SultanMuralController.ruinMural(List<Cell>,HistoricEvent)",
            "XRL.World.Parts/SultanMuralController.cs",
            "ruinMural",
            "generated_display_name_mural_ruined_historic_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"DisplayNameAssignment": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-mural-display-name-runtime-test.json"),
    )

    for family_id, _, _, _ in families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    for key in ("player_blank", "sultan_blank"):
        _assert_evidence_contains(
            entries,
            families[key][0],
            "ui-displayname-atomic.ja.json",
            "GetDisplayNameRouteTranslatorTests.cs",
        )
    for key in ("player_event", "historic_event", "ruined_historic"):
        _assert_evidence_contains(
            entries,
            families[key][0],
            "GeneratedDisplayNameOwnerTranslationPatch.cs",
            "GeneratedDisplayNameOwnerTranslationPatchTests.cs",
        )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {}
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_producer_runtime_routes_by_owner_shape() -> None:
    """Producer runtime-required rows are grouped by broad owner route shape."""
    inventory = _inventory(
        [
            _family("XRL.UI/OptionsUI.cs::OptionsUI.Show()", "XRL.UI/OptionsUI.cs", "Show", {"Popup": 1}),
            _family(
                "Qud.API/SavesAPI.cs::SavesAPI.FatalSaveError(Exception,string)",
                "Qud.API/SavesAPI.cs",
                "FatalSaveError",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Tick()",
                "XRL.World.Parts.Mutation/SunderMind.cs",
                "Tick",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Conversations.Parts/EndGame.cs::EndGame.HandleEvent(EnterElementEvent)",
                "XRL.World.Conversations.Parts/EndGame.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
                "XRL.World.Parts/Stomach.cs",
                "FireEvent",
                {"Popup": 1},
            ),
            _family(
                "SoundManager.cs::SoundManager._PlaySound(string,float,float,SoundRequest.SoundEffectType)",
                "SoundManager.cs",
                "_PlaySound",
                {"AddPlayerMessage": 1},
            ),
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_api_runtime_routes_by_owner_shape() -> None:
    """Qud.API popup producers split into exact implementation and debug/runtime buckets."""
    families = {
        "equipment": (
            "Qud.API/EquipmentAPI.cs::"
            "EquipmentAPI.ShowInventoryActionMenu(Dictionary<string,InventoryAction>,GameObject,GameObject,bool,bool,"
            "string,IComparer<InventoryAction>,bool)",
            "Qud.API/EquipmentAPI.cs",
            "ShowInventoryActionMenu",
            {"Popup": 12},
            "producer_runtime_api_equipment_action_menu_gap",
            "covered_by_owner_route",
        ),
        "save_error": (
            "Qud.API/SavesAPI.cs::SavesAPI.FatalSaveError(Exception,string)",
            "Qud.API/SavesAPI.cs",
            "FatalSaveError",
            {"Popup": 9},
            "producer_runtime_api_save_error_gap",
            "covered_by_owner_route",
        ),
        "journal_wish": (
            "Qud.API/JournalAPI.cs::JournalAPI.WishGospelAccomplishments()",
            "Qud.API/JournalAPI.cs",
            "WishGospelAccomplishments",
            {"Popup": 7},
            "producer_runtime_api_journal_wish_gospel_runtime",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-api-runtime-test.json"))

    assert {
        family_id: entries[family_id]["closure_status"]
        for family_id, _, _, _, _, _ in families.values()
    } == {
        family_id: (
            "covered_by_owner_route"
            if disposition == "covered_by_owner_route"
            else "action_required"
            if disposition == "likely_implementation_gap"
            else "runtime_required"
        )
        for family_id, _, _, _, _, disposition in families.values()
    }
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_residual_bucket_payload_splits_ui_status_popup_routes_by_owner_shape() -> None:
    """Status/ability UI popup rows are exact owner implementation candidates."""
    families = {
        "factions": (
            "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.HandleCmdOptions()",
            "Qud.UI/FactionsStatusScreen.cs",
            "FactionsStatusScreen",
            "HandleCmdOptions",
            {"Popup": 8},
            "producer_runtime_ui_factions_status_sort_gap",
        ),
        "inventory": (
            "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.HandleShowOptions()",
            "Qud.UI/InventoryAndEquipmentStatusScreen.cs",
            "InventoryAndEquipmentStatusScreen",
            "HandleShowOptions",
            {"Popup": 8},
            "producer_runtime_ui_inventory_status_options_gap",
        ),
        "ability": (
            "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.showScreen(XRL.World.GameObject)",
            "Qud.UI/AbilityManagerScreen.cs",
            "AbilityManagerScreen",
            "showScreen",
            {"Popup": 5},
            "producer_runtime_ui_ability_manager_empty_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(
                family_id,
                source_file,
                member_name,
                surfaces,
            )
            for family_id, source_file, type_name, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-ui-status-popup-test.json"))

    covered_family_ids = {family_id for family_id, _, _, _, _, _ in families.values()}
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        if family_id == families["ability"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "AbilityManagerPopupTranslationPatch.cs",
                "AbilityManagerScreenTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
                "ui-popup.ja.json",
            )
        else:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupPickOptionTranslationPatch.cs",
                "PopupPickOptionTranslationPatchTests.cs",
                "ui-popup.ja.json",
            )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }


def test_residual_bucket_payload_splits_conversation_producer_runtime_routes_by_owner_shape() -> None:
    """Conversation producer runtime residuals are exact popup owner implementation gaps."""
    families = {
        "resheph_secret": (
            "XRL.World.Conversations.Parts/GiveReshephSecret.cs::"
            "GiveReshephSecret.HandleEvent(EnterElementEvent)",
            "XRL.World.Conversations.Parts/GiveReshephSecret.cs",
            "HandleEvent",
            "producer_runtime_conversation_resheph_secret_gap",
        ),
        "endgame": (
            "XRL.World.Conversations.Parts/EndGame.cs::EndGame.HandleEvent(EnterElementEvent)",
            "XRL.World.Conversations.Parts/EndGame.cs",
            "HandleEvent",
            "producer_runtime_conversation_endgame_confirm_gap",
        ),
        "give_artifact": (
            "XRL.World.Conversations.Parts/GiveArtifact.cs::GiveArtifact.HandleEvent(EnterElementEvent)",
            "XRL.World.Conversations.Parts/GiveArtifact.cs",
            "HandleEvent",
            "producer_runtime_conversation_give_artifact_gap",
        ),
        "water_ritual": (
            "XRL.World.Conversations.Parts/WaterRitualSellSecret.cs::WaterRitualSellSecret.Share()",
            "XRL.World.Conversations.Parts/WaterRitualSellSecret.cs",
            "Share",
            "producer_runtime_conversation_water_ritual_secret_gap",
        ),
        "api_reward": (
            "Qud.API/ConversationsAPI.cs::ConversationsAPI.chooseOneItem(List<GameObject>,string,bool)",
            "Qud.API/ConversationsAPI.cs",
            "chooseOneItem",
            "producer_runtime_conversation_api_reward_pick_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"Popup": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-conversation-producer-runtime-test.json"),
    )

    covered_family_ids = {
        families["api_reward"][0],
        families["endgame"][0],
        families["give_artifact"][0],
        families["resheph_secret"][0],
        families["water_ritual"][0],
    }
    assert {
        entry["family_id"]: entry["closure_status"]
        for entry in entries.values()
    } == {
        family_id: "covered_by_owner_route" if family_id in covered_family_ids else "action_required"
        for family_id, _, _, _ in families.values()
    }
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_mutation_producer_runtime_routes_by_owner_shape() -> None:
    """Mutation producer residuals are split into exact implementation-gap owners."""
    families = {
        "sunder_mind": (
            "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Tick()",
            "XRL.World.Parts.Mutation/SunderMind.cs",
            "Tick",
            {"AddPlayerMessage": 1, "Popup": 1},
            "producer_runtime_mutation_sunder_mind_gap",
        ),
        "domination": (
            "XRL.World.Parts.Mutation/Domination.cs::Domination.ProcessTarget(GameObject,ref string)",
            "XRL.World.Parts.Mutation/Domination.cs",
            "ProcessTarget",
            {"Does": 1},
            "producer_runtime_mutation_domination_failure_gap",
        ),
        "temporal_fugue": (
            "XRL.World.Parts.Mutation/TemporalFugue.cs::"
            "TemporalFugue.PerformTemporalFugue(GameObject,GameObject,GameObject,TemporalFugue,IEvent,bool,bool,"
            "int?,int?,int,string,string,string,string,string)",
            "XRL.World.Parts.Mutation/TemporalFugue.cs",
            "PerformTemporalFugue",
            {"Does": 1, "MessageFrame": 1},
            "producer_runtime_mutation_temporal_fugue_gap",
        ),
        "carapace": (
            "XRL.World.Parts.Mutation/Carapace.cs::Carapace.Loosen(bool)",
            "XRL.World.Parts.Mutation/Carapace.cs",
            "Loosen",
            {"Does": 1, "EmitMessage": 1, "Popup": 1},
            "producer_runtime_mutation_carapace_loosen_gap",
        ),
        "base_variant": (
            "XRL.World.Parts.Mutation/BaseMutation.cs::BaseMutation.SelectVariant(GameObject,bool)",
            "XRL.World.Parts.Mutation/BaseMutation.cs",
            "SelectVariant",
            {"Popup": 1},
            "producer_runtime_mutation_base_variant_popup_gap",
        ),
        "wings": (
            "XRL.World.Parts.Mutation/Wings.cs::Wings.HandleEvent(CommandEvent)",
            "XRL.World.Parts.Mutation/Wings.cs",
            "HandleEvent",
            {"Popup": 1},
            "producer_runtime_mutation_wings_flight_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-mutation-producer-test.json"))

    covered_keys = {"sunder_mind", "domination", "temporal_fugue", "carapace", "base_variant", "wings"}
    for key, (family_id, _, _, _, _) in families.items():
        expected_status = "covered_by_owner_route" if key in covered_keys else "action_required"
        assert entries[family_id]["closure_status"] == expected_status
        if key == "base_variant":
            _assert_evidence_contains(
                entries,
                family_id,
                "BaseMutationSelectVariantPopupTranslationPatch.cs",
                "BaseMutationSelectVariantPopupTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if key == "carapace":
            _assert_evidence_contains(
                entries,
                family_id,
                "CarapaceTranslationPatch.cs",
                "CombatAndLogMessageQueuePatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if key == "domination":
            _assert_evidence_contains(
                entries,
                family_id,
                "DominationProcessTargetTranslationPatch.cs",
                "MessageQueueSemanticPipeline.cs",
                "DominationProcessTargetTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if key == "temporal_fugue":
            _assert_evidence_contains(
                entries,
                family_id,
                "verbs.ja.json",
                "ui-popup.ja.json",
                "DoesVerbFamilyTests.cs",
                "PopupShowTranslationPatchTests.cs",
                "MessageFrameTranslatorTests.cs",
            )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for key, (family_id, _, _, _, bucket) in families.items()
        if key not in covered_keys
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_core_system_runtime_routes_by_owner_shape() -> None:
    """Core/system residual routes separate static owner gaps from debug and generic sinks."""
    families = {
        "scores": (
            "XRL.Core/Scores.cs::Scores.Show()",
            "XRL.Core/Scores.cs",
            "Show",
            {"Popup": 1},
            "producer_runtime_core_scores_legacy_screen_gap",
            "covered_by_owner_route",
        ),
        "game_text": (
            "XRL/GameText.cs::GameText.RoughConvertSecondPersonToThirdPerson(string,GameObject)",
            "XRL/GameText.cs",
            "RoughConvertSecondPersonToThirdPerson",
            {"Does": 1},
            "producer_runtime_core_game_text_third_person_death_gap",
            "covered_by_owner_route",
        ),
        "mod_config": (
            "XRL.Core/XRLCore.cs::XRLCore.RestoreModsLoadedAsync(List<string>)",
            "XRL.Core/XRLCore.cs",
            "RestoreModsLoadedAsync",
            {"Popup": 1},
            "producer_runtime_core_mod_config_popup_gap",
            "covered_by_owner_route",
        ),
        "population_wish": (
            "XRL/PopulationManager.cs::PopulationManager.WishFindBlueprint(string)",
            "XRL/PopulationManager.cs",
            "WishFindBlueprint",
            {"Popup": 1},
            "producer_runtime_core_population_wish_find_blueprint_gap",
            "covered_by_owner_route",
        ),
        "mod_failure": (
            "XRL/ModInfo.cs::ModInfo.ConfirmFailure()",
            "XRL/ModInfo.cs",
            "ConfirmFailure",
            {"Popup": 1},
            "producer_runtime_core_mod_failure_popup_gap",
            "covered_by_owner_route",
        ),
        "coda": (
            "XRL/CodaSystem.cs::CodaSystem.EndGamePrompt()",
            "XRL/CodaSystem.cs",
            "EndGamePrompt",
            {"Popup": 1},
            "producer_runtime_core_coda_endgame_popup_gap",
            "covered_by_owner_route",
        ),
        "sound": (
            "SoundManager.cs::SoundManager._PlaySound(string,float,float,SoundRequest.SoundEffectType)",
            "SoundManager.cs",
            "_PlaySound",
            {"AddPlayerMessage": 1},
            "producer_runtime_core_sound_debug_queue_runtime",
            "covered_by_owner_route",
        ),
        "generic_sink": (
            "Extensions.cs::Extensions.ShowSuccess(this XRL.World.GameObject,string,bool)",
            "Extensions.cs",
            "ShowSuccess",
            {"Popup": 1},
            "producer_runtime_core_generic_sink_runtime",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, _, disposition in families.values():
        if disposition == "covered_by_owner_route":
            expected_status = "covered_by_owner_route"
        else:
            expected_status = "action_required" if disposition == "likely_implementation_gap" else "runtime_required"
        assert entries[family_id]["closure_status"] == expected_status
    game_text_evidence = " ".join(entries[families["game_text"][0]]["closure_evidence"])
    assert "GameText.RoughConvertSecondPersonToThirdPerson closure" in game_text_evidence
    assert "GameTextDeathReasonTranslationPatch" in game_text_evidence
    coda_evidence = " ".join(entries[families["coda"][0]]["closure_evidence"])
    assert "CodaSystem.EndGamePrompt" in coda_evidence
    assert "PopupAskStringTranslationPatch" in coda_evidence
    assert "DeathReasonTranslationPatch" in coda_evidence
    population_evidence = " ".join(entries[families["population_wish"][0]]["closure_evidence"])
    assert "PopulationManager.WishFindBlueprint" in population_evidence
    assert "SingleCallsiteOwnerPopupTranslationPatch" in population_evidence
    mod_failure_evidence = " ".join(entries[families["mod_failure"][0]]["closure_evidence"])
    assert "ModInfo.ConfirmFailure" in mod_failure_evidence
    assert "ModInfoTranslationPatch" in mod_failure_evidence
    mod_config_evidence = " ".join(entries[families["mod_config"][0]]["closure_evidence"])
    assert "XRLCore.RestoreModsLoadedAsync" in mod_config_evidence
    assert "XrlCoreRestoreModsLoadedTranslationPatch" in mod_config_evidence

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-core-system-runtime-test.json"),
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_policy_records_scores_show_as_covered_legacy_screen_owner_route() -> None:
    """Legacy Scores.Show is covered by exact screen and delete-popup owner routes."""
    family_id = "XRL.Core/Scores.cs::Scores.Show()"
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.Core/Scores.cs",
                "Show",
                {"Popup": 46},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-scores-show-test.json"),
    )

    assert entries[family_id]["closure_status"] == "covered_by_owner_route"
    evidence = " ".join(entries[family_id]["closure_evidence"])
    assert "legacy high-score screen" in evidence
    assert "LegacyScoresScreenTranslationPatchTests.cs" in evidence
    assert "HighScoresDeletePopupTranslationPatchTests.cs" in evidence
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {}


def test_policy_promotes_sound_manager_debug_queue_rows_as_passthrough() -> None:
    """SoundManager WriteSoundsToLog rows are debug sound identifiers, not localizable text."""
    families = {
        "play_sound": (
            "SoundManager.cs::SoundManager._PlaySound(string,float,float,SoundRequest.SoundEffectType)",
            "_PlaySound",
            {"AddPlayerMessage": 4},
        ),
        "play_world_sound": (
            "SoundManager.cs::SoundManager._PlayWorldSound(string,float,float,float,float,Point2D)",
            "_PlayWorldSound",
            {"AddPlayerMessage": 4},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, "SoundManager.cs", member_name, surfaces)
            for family_id, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-sound-manager-debug-test.json"),
    )

    assert payload["entries"] == []
    assert {entry["closure_status"] for entry in entries.values()} == {"covered_by_owner_route"}
    for family_id, _, _ in families.values():
        _assert_evidence_contains(
            entries,
            family_id,
            "WriteSoundsToLog",
            "SoundManagerSetChannelTrackTranslationPatchTests.cs",
            "LeavesDebugMissingTrackMessageUnchanged",
        )


def test_policy_records_population_manager_popup_rows_as_static_owner_gaps() -> None:
    """PopulationManager popup residual rows have exact static owners."""
    families = {
        "wish_blueprint": (
            "XRL/PopulationManager.cs::PopulationManager.WishFindBlueprint(string)",
            "XRL/PopulationManager.cs",
            "WishFindBlueprint",
            {"Popup": 13},
            "producer_runtime_core_population_wish_find_blueprint_gap",
        ),
        "roll_one": (
            "XRL/PopulationManager.cs::PopulationManager.RollOneFrom(string,Dictionary<string,string>,string)",
            "XRL/PopulationManager.cs",
            "RollOneFrom",
            {"Popup": 1},
            "producer_runtime_core_population_roll_one_error_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-population-manager-popup-test.json"),
    )

    covered_family_ids = {families["wish_blueprint"][0], families["roll_one"][0]}
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "SingleCallsiteOwnerPopupTranslationPatch.cs",
            "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
            "TargetMethodResolutionTests.cs",
        )
    for family_id in set(entries) - covered_family_ids:
        assert entries[family_id]["closure_status"] == "action_required"
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }
    for family_id in entries:
        if family_id in covered_family_ids:
            continue
        _assert_evidence_contains(
            entries,
            family_id,
            "fixed player-visible text and route-local generated captures",
        )


def test_residual_bucket_payload_splits_ui_producer_runtime_routes_by_popup_owner_shape() -> None:
    """UI producer popup rows are split by route shape before runtime evidence work."""
    families = {
        "options": (
            "XRL.UI/OptionsUI.cs::OptionsUI.Show()",
            "XRL.UI/OptionsUI.cs",
            "OptionsUI",
            "Show",
            "producer_runtime_ui_options_legacy_popup_gap",
        ),
        "inventory_trade": (
            "XRL.UI/TradeUI.cs::TradeUI.ShowVendorActions(GameObject,GameObject,bool)",
            "XRL.UI/TradeUI.cs",
            "TradeUI",
            "ShowVendorActions",
            "producer_runtime_ui_trade_vendor_actions_gap",
        ),
        "chargen": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.AddBuild(string)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "QudBuildLibraryModuleWindow",
            "AddBuild",
            "producer_runtime_ui_chargen_build_library_add_gap",
        ),
        "status": (
            "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.HandleCmdOptions()",
            "Qud.UI/FactionsStatusScreen.cs",
            "FactionsStatusScreen",
            "HandleCmdOptions",
            "producer_runtime_ui_factions_status_sort_gap",
        ),
        "tutorial": (
            "XRL.UI/FadeText.cs::FadeText.Update()",
            "XRL.UI/FadeText.cs",
            "FadeText",
            "Update",
            None,
        ),
        "misc": (
            "Qud.UI/ModManagerUI.cs::ModManagerUI.OnCancel()",
            "Qud.UI/ModManagerUI.cs",
            "ModManagerUI",
            "OnCancel",
            "producer_runtime_ui_mod_manager_cancel_gap",
        ),
        "framework_search": (
            "XRL.UI.Framework/FrameworkSearchInput.cs::FrameworkSearchInput.ChangeValue()",
            "XRL.UI.Framework/FrameworkSearchInput.cs",
            "FrameworkSearchInput",
            "ChangeValue",
            "producer_runtime_ui_framework_search_input_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"Popup": 1})
            for family_id, source_file, _type_name, member_name, _ in families.values()
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-ui-producer-runtime-test.json"))

    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_ui_options_popup_runtime_routes_by_owner_shape() -> None:
    """Options UI popup residuals are grouped by exact owner route."""
    families = {
        "legacy_options": (
            "XRL.UI/OptionsUI.cs::OptionsUI.Show()",
            "XRL.UI/OptionsUI.cs",
            "Show",
            "producer_runtime_ui_options_legacy_popup_gap",
        ),
        "command_binding": (
            "XRL.UI/CommandBindingManager.cs::CommandBindingManager.RestoreDefaults()",
            "XRL.UI/CommandBindingManager.cs",
            "RestoreDefaults",
            "producer_runtime_ui_options_command_binding_gap",
        ),
        "modern_help": (
            "Qud.UI/OptionsScreen.cs::OptionsScreen.HandleMenuOption(FrameworkDataElement)",
            "Qud.UI/OptionsScreen.cs",
            "HandleMenuOption",
            "producer_runtime_ui_options_help_popup_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"Popup": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-ui-options-popup-runtime-test.json"),
    )

    covered_family_ids = {
        families["legacy_options"][0],
        families["command_binding"][0],
        families["modern_help"][0],
    }
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        if "OptionsScreen" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "OptionsLocalizationPatch.cs",
                "OptionsLocalizationPatchTests.cs",
            )
        elif "CommandBindingManager" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupPickOptionTranslationPatch.cs",
                "PopupPickOptionTranslationPatchTests.cs",
                "ui-popup.ja.json",
            )
        else:
            _assert_evidence_contains(
                entries,
                family_id,
                "LegacyOptionsUiTranslationPatch.cs",
                "LegacyOptionsUiTranslationPatchTests.cs",
            )
    for family_id in set(entries) - covered_family_ids:
        assert entries[family_id]["closure_status"] == "action_required"
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_ui_inventory_trade_popup_runtime_routes_by_owner_shape() -> None:
    """Inventory/trade UI popup residuals are grouped by exact owner route."""
    families = {
        "trade_vendor_actions": (
            "XRL.UI/TradeUI.cs::TradeUI.ShowVendorActions(GameObject,GameObject,bool)",
            "XRL.UI/TradeUI.cs",
            "TradeUI",
            "ShowVendorActions",
            {"Popup": 17},
            "producer_runtime_ui_trade_vendor_actions_gap",
        ),
        "object_finder_filters": (
            "XRL.UI/ObjectFinder.cs::ObjectFinder.ConfigFilters()",
            "XRL.UI/ObjectFinder.cs",
            "ObjectFinder",
            "ConfigFilters",
            {"Popup": 16},
            "producer_runtime_ui_object_finder_filters_gap",
        ),
        "equipment_slot": (
            "XRL.UI/EquipmentScreen.cs::EquipmentScreen.ShowBodypartEquipUI(GameObject,BodyPart)",
            "XRL.UI/EquipmentScreen.cs",
            "EquipmentScreen",
            "ShowBodypartEquipUI",
            {"Popup": 9},
            "producer_runtime_ui_equipment_slot_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, type_name, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-ui-inventory-trade-popup-runtime-test.json"),
    )

    assert entries[families["equipment_slot"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["equipment_slot"][0],
        "EquipmentScreenBodypartEquipPopupTranslationPatch.cs",
        "EquipmentScreenBodypartEquipPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[families["trade_vendor_actions"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["trade_vendor_actions"][0],
        "TradeUiVendorPopupTranslationPatch.cs",
        "TradeUiPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[families["object_finder_filters"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["object_finder_filters"][0],
        "ObjectFinderConfigFiltersTranslationPatch.cs",
        "ObjectFinderConfigFiltersTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert {
        entries[family_id]["closure_status"]
        for family_id in set(entries)
        - {
            families["equipment_slot"][0],
            families["trade_vendor_actions"][0],
            families["object_finder_filters"][0],
        }
    } == set()
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, _, _, bucket in families.values()
        if family_id
        not in {
            families["equipment_slot"][0],
            families["trade_vendor_actions"][0],
            families["object_finder_filters"][0],
        }
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_chargen_popup_runtime_routes_by_owner_shape() -> None:
    """Chargen popup residuals are grouped by exact owner route shape."""
    families = {
        "build_library_manage": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.HandleMenuOption(MenuOption)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "HandleMenuOption",
            {"Popup": 11},
            "producer_runtime_ui_chargen_build_library_manage_gap",
        ),
        "build_library_add": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.AddBuild(string)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "AddBuild",
            {"Popup": 7},
            "producer_runtime_ui_chargen_build_library_add_gap",
        ),
        "gender_customize": (
            "XRL.World/Gender.cs::Gender.CustomizeProcess(string)",
            "XRL.World/Gender.cs",
            "CustomizeProcess",
            {"Popup": 6},
            "producer_runtime_ui_chargen_gender_customize_gap",
        ),
        "build_library_import": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.onSelect(FrameworkDataElement)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "onSelect",
            {"Popup": 5},
            "producer_runtime_ui_chargen_build_library_import_gap",
        ),
        "build_summary": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs::"
            "QudBuildSummaryModuleWindow.HandleMenuOption(MenuOption)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs",
            "HandleMenuOption",
            {"Popup": 4},
            "producer_runtime_ui_chargen_build_summary_gap",
        ),
        "validation": (
            "XRL.CharacterBuilds/EmbarkBuilder.cs::EmbarkBuilder.checkStateAsync()",
            "XRL.CharacterBuilds/EmbarkBuilder.cs",
            "checkStateAsync",
            {"Popup": 4},
            "producer_runtime_ui_chargen_validation_popup_gap",
        ),
        "mutation_menu": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::"
            "QudMutationsModuleWindow.HandleMenuOption(MenuOption)",
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
            "HandleMenuOption",
            {"Popup": 3},
            "producer_runtime_ui_chargen_mutation_menu_gap",
        ),
        "mutation_variant": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::"
            "QudMutationsModuleWindow.SelectVariant()",
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
            "SelectVariant",
            {"Popup": 2},
            "producer_runtime_ui_chargen_mutation_variant_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-chargen-popup-runtime-test.json"))

    covered_family_ids = {
        families["build_library_manage"][0],
        families["build_library_add"][0],
        families["build_library_import"][0],
        families["build_summary"][0],
        families["gender_customize"][0],
        families["validation"][0],
        families["mutation_menu"][0],
        families["mutation_variant"][0],
    }
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        if family_id == families["gender_customize"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "BasePronounProviderCustomizePopupTranslationPatch.cs",
                "PopupAskStringTranslationPatch.cs",
                "PopupAskStringTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
                "ui-popup.ja.json",
            )
        elif family_id == families["validation"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "EmbarkBuilderValidationPopupTranslationPatch.cs",
                "EmbarkBuilderValidationPopupTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        elif family_id == families["mutation_variant"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "QudMutationsModuleWindowVariantPopupTranslationPatch.cs",
                "QudMutationsModuleWindowTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
                "ui-chargen-supplement.ja.json",
            )
        elif "QudBuildLibraryModuleWindow" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupMessageTranslationPatch.cs",
                "PopupAskStringTranslationPatch.cs",
                "ui-chargen.ja.json",
            )
        elif "QudBuildSummaryModuleWindow" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupMessageTranslationPatch.cs",
                "ui-chargen.ja.json",
            )
        else:
            _assert_evidence_contains(
                entries,
                family_id,
                "QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.cs",
                "QudMutationsModuleWindowTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
    for family_id in set(entries) - covered_family_ids:
        assert entries[family_id]["closure_status"] == "action_required"
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_gameplay_producer_runtime_routes() -> None:
    """Gameplay producer runtime rows are split into more specific owner families."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Capabilities/Firefighting.cs::Firefighting.AttemptFirefightingCore(GameObject,GameObject,int,bool,bool)",
                "XRL.World.Capabilities/Firefighting.cs",
                "AttemptFirefightingCore",
                {"Popup": 1},
            ),
            _family(
                "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffect()",
                "XRL.Liquids/LiquidWarmStatic.cs",
                "WishWarmEffect",
                {"EmitMessage": 1},
            ),
            _family(
                "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::FindASiteDynamicQuestManager.DynamicQuestWhere()",
                "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs",
                "DynamicQuestWhere",
                {"AddPlayerMessage": 1},
            ),
            _family(
                "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AskLowLevelHack(GameObject)",
                "XRL.World.Parts/CyberneticsTerminal2.cs",
                "AskLowLevelHack",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
                "XRL.World.Parts/Crayons.cs",
                "HandleEvent",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.FireEvent(Event)",
                "XRL.World.Parts/ElementalJelly.cs",
                "FireEvent",
                {"Popup": 1},
            ),
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_liquid_runtime_routes_by_owner_shape() -> None:
    """Liquid producer residuals split WishCommand debug frames from gameplay messages."""
    families = {
        "wish_spec": (
            "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffectSpec(string)",
            "XRL.Liquids/LiquidWarmStatic.cs",
            "WishWarmEffectSpec",
            "producer_runtime_liquid_wish_warm_effect_gap",
        ),
        "glitch_components": (
            (
                "XRL.Liquids/LiquidWarmStatic.cs::"
                "LiquidWarmStatic.GlitchLiquidComponents(GameObject,string,int,bool)"
            ),
            "XRL.Liquids/LiquidWarmStatic.cs",
            "GlitchLiquidComponents",
            "producer_runtime_liquid_glitch_components_gap",
        ),
        "wish": (
            "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffect()",
            "XRL.Liquids/LiquidWarmStatic.cs",
            "WishWarmEffect",
            "producer_runtime_liquid_wish_warm_effect_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"EmitMessage": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-liquid-runtime-route-test.json"),
    )

    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {}
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}
    for key in ("wish", "wish_spec", "glitch_components"):
        family_id = families[key][0]
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "LiquidWarmStaticTranslationPatch.cs",
            "CombatAndLogMessageQueuePatchTests.cs",
        )


def test_residual_bucket_payload_splits_quest_runtime_routes_by_owner_shape() -> None:
    """Quest residuals split static reward/debug owners and existing localized quest properties."""
    families = {
        "reward_choice": (
            "XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs::"
            "DynamicQuestRewardElement_ChoiceFromPopulation.award()",
            "XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs",
            "award",
            {"Popup": 6},
            "producer_runtime_quest_reward_choice_gap",
        ),
        "reclamation": (
            "XRL.World.Quests/ReclamationSystem.cs::ReclamationSystem.HandleEvent(EnteringZoneEvent)",
            "XRL.World.Quests/ReclamationSystem.cs",
            "HandleEvent",
            {"Popup": 2},
            None,
        ),
        "dynamic_where": (
            "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::FindASiteDynamicQuestManager.DynamicQuestWhere()",
            "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs",
            "DynamicQuestWhere",
            {"AddPlayerMessage": 1},
            "producer_runtime_quest_find_site_wish_debug_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-quest-runtime-route-test.json"),
    )

    assert entries[families["reclamation"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(entries, families["reclamation"][0], "MessageLeaving", "Quests.jp.xml")
    assert entries[families["dynamic_where"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["dynamic_where"][0],
        "WishCommandQueueTranslationPatch.cs",
        "WishCommandQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[families["reward_choice"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["reward_choice"][0],
        "DynamicQuestRewardElement_ChoiceFromPopulation.award",
        "PopupPickOptionTranslationPatchTests.cs",
        "&WxN",
        "ui-popup.ja.json",
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {}
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_capability_runtime_routes_by_owner_shape() -> None:
    """Capability producer residuals are grouped by exact player-visible owner route shape."""
    families = {
        "firefighting": (
            "XRL.World.Capabilities/Firefighting.cs::"
            "Firefighting.AttemptFirefightingCore(GameObject,GameObject,int,bool,bool)",
            "XRL.World.Capabilities/Firefighting.cs",
            "AttemptFirefightingCore",
            {"MessageFrame": 30, "Popup": 7},
            "producer_runtime_capability_firefighting_gap",
        ),
        "item_naming": (
            "XRL.World.Capabilities/ItemNaming.cs::"
            "ItemNaming.NameItem(GameObject,GameObject,GameObject,GameObject,string,string,bool)",
            "XRL.World.Capabilities/ItemNaming.cs",
            "NameItem",
            {"Popup": 27},
            "producer_runtime_capability_item_naming_gap",
        ),
        "item_naming_wish": (
            "XRL.World.Capabilities/ItemNaming.cs::ItemNaming.HandleItemNamingWish(Match)",
            "XRL.World.Capabilities/ItemNaming.cs",
            "HandleItemNamingWish",
            {"Popup": 5},
            "producer_runtime_capability_item_naming_wish_debug_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    assert entries[families["firefighting"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["firefighting"][0],
        "FirefightingTranslationPatchTests.cs",
        "MessageFrameTranslatorTests.cs",
    )
    assert entries[families["item_naming"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["item_naming"][0],
        "ItemNamingTranslationPatch.cs",
        "PopupShowColorPickerTranslationPatch.cs",
        "ItemNamingTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert entries[families["item_naming_wish"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["item_naming_wish"][0],
        "ItemNaming.HandleItemNamingWish",
        "ItemNamingTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-capability-test.json"))
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, _, bucket in families.values()
        if family_id
        not in {
            families["firefighting"][0],
            families["item_naming"][0],
            families["item_naming_wish"][0],
        }
    }


def test_residual_bucket_payload_splits_cybernetics_runtime_routes_by_owner_shape() -> None:
    """Cybernetics producer residuals are grouped by exact owner route shape."""
    families = {
        "butcher": (
            "XRL.World.Parts/CyberneticsButcherableCybernetic.cs::"
            "CyberneticsButcherableCybernetic.AttemptButcher(GameObject,bool,bool,bool,int,Cell,List<GameObject>)",
            "XRL.World.Parts/CyberneticsButcherableCybernetic.cs",
            "AttemptButcher",
            {"Does": 1, "EmitMessage": 1},
            "producer_runtime_cybernetics_butcher_message_gap",
        ),
        "force_lathe_activation": (
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
            "CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe(GameObject,GameObject,IEvent)",
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs",
            "ActivatePrecisionForceLathe",
            {"Does": 1, "MessageFrame": 1},
            "producer_runtime_cybernetics_force_lathe_activation_gap",
        ),
        "low_level_hack": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AskLowLevelHack(GameObject)",
            "XRL.World.Parts/CyberneticsTerminal2.cs",
            "AskLowLevelHack",
            {"Popup": 1},
            "producer_runtime_cybernetics_low_level_hack_popup_gap",
        ),
        "holographic_visage": (
            "XRL.World.Parts/CyberneticsHolographicVisage.cs::"
            "CyberneticsHolographicVisage.SelectVisage(GameObject)",
            "XRL.World.Parts/CyberneticsHolographicVisage.cs",
            "SelectVisage",
            {"EmitMessage": 1, "Popup": 1},
            "producer_runtime_cybernetics_holographic_visage_gap",
        ),
        "cathedra": (
            "XRL.World.Parts/CyberneticsCathedra.cs::CyberneticsCathedra.HandleEvent(CommandEvent)",
            "XRL.World.Parts/CyberneticsCathedra.cs",
            "HandleEvent",
            {"Popup": 1},
            "producer_runtime_cybernetics_cathedra_flight_popup_gap",
        ),
        "recoiler": (
            "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs::"
            "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport(GameObject,IEvent)",
            "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs",
            "ActuateTeleport",
            {"Popup": 1},
            "producer_runtime_cybernetics_recoiler_popup_gap",
        ),
        "force_lathe_replace": (
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
            "CyberneticsPrecisionForceLathe.HandleEvent(ReplaceThrownWeaponEvent)",
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs",
            "HandleEvent",
            {"MessageFrame": 1},
            "producer_runtime_cybernetics_force_lathe_replace_gap",
        ),
        "terminal_interface": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AttemptInterface(GameObject,IEvent)",
            "XRL.World.Parts/CyberneticsTerminal2.cs",
            "AttemptInterface",
            {"Does": 1},
            "producer_runtime_cybernetics_terminal_interface_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-cybernetics-runtime-test.json"))

    covered_keys = {
        "butcher",
        "force_lathe_activation",
        "low_level_hack",
        "holographic_visage",
        "cathedra",
        "recoiler",
        "force_lathe_replace",
        "terminal_interface",
    }
    for key, (family_id, _, _, _, _) in families.items():
        expected_status = "covered_by_owner_route" if key in covered_keys else "action_required"
        assert entries[family_id]["closure_status"] == expected_status
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for key, (family_id, _, _, _, bucket) in families.items()
        if key not in covered_keys
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_inventory_action_runtime_routes_by_surface_shape() -> None:
    """InventoryActionEvent residuals are grouped by message/popup surface shape."""
    families = {
        "popup": (
            "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Crayons.cs",
            "HandleEvent",
            {"Popup": 1},
            "producer_runtime_inventory_action_crayons_popup_gap",
        ),
        "does_popup": (
            "XRL.World.Parts/Examiner.cs::Examiner.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Examiner.cs",
            "HandleEvent",
            {"Does": 1, "Popup": 1},
            "producer_runtime_inventory_action_examiner_popup_gap",
        ),
        "message_frame_popup": (
            "XRL.World.Parts/Brain.cs::Brain.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Brain.cs",
            "HandleEvent",
            {"MessageFrame": 1, "Popup": 1},
            "producer_runtime_inventory_action_message_frame_popup_route_split",
        ),
        "emit": (
            "XRL.World.Parts/DesalinationPellet.cs::DesalinationPellet.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/DesalinationPellet.cs",
            "HandleEvent",
            {"EmitMessage": 1},
            "producer_runtime_inventory_action_desalination_pellet_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-inventory-action-runtime-test.json"))

    assert entries[families["does_popup"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["does_popup"][0],
        "ExaminerTranslationPatch.cs",
        "ExaminerTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_inventory_action_does_popup_runtime_routes_by_owner_shape() -> None:
    """InventoryActionEvent Does+Popup residuals are grouped by exact owner route."""
    families = {
        "examiner": (
            "XRL.World.Parts/Examiner.cs::Examiner.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Examiner.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_examiner_popup_gap",
        ),
        "tinker_item": (
            "XRL.World.Parts/TinkerItem.cs::TinkerItem.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/TinkerItem.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_tinker_item_popup_gap",
        ),
        "fixit_spray": (
            "XRL.World.Parts/FixitSpray.cs::FixitSpray.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/FixitSpray.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_fixit_spray_popup_gap",
        ),
        "magnetized_applicator": (
            "XRL.World.Parts/MagnetizedApplicator.cs::MagnetizedApplicator.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/MagnetizedApplicator.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_magnetized_applicator_popup_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"Does": 1, "Popup": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-inventory-action-does-popup-runtime-test.json"),
    )

    covered_family_ids = {
        families["examiner"][0],
        families["tinker_item"][0],
        families["fixit_spray"][0],
        families["magnetized_applicator"][0],
    }
    for family_id, _, _, _ in families.values():
        if family_id in covered_family_ids:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            _assert_evidence_contains(
                entries,
                family_id,
                (
                    "ExaminerTranslationPatch.cs"
                    if family_id == families["examiner"][0]
                    else "TinkerItemTranslationPatch.cs"
                    if family_id == families["tinker_item"][0]
                    else "SingleCallsiteOwnerPopupTranslationPatch.cs"
                ),
                (
                    "ExaminerTranslationPatchTests.cs"
                    if family_id == families["examiner"][0]
                    else "TinkerItemTranslationPatchTests.cs"
                    if family_id == families["tinker_item"][0]
                    else "SingleCallsiteOwnerPopupTranslationPatchTests.cs"
                ),
                "TargetMethodResolutionTests.cs"
                if family_id in {families["examiner"][0], families["tinker_item"][0]}
                else "DoesVerbFamilyTests.cs",
            )
        else:
            assert entries[family_id]["closure_status"] == "action_required"
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id not in covered_family_ids and bucket != "producer_runtime_inventory_action_crayons_popup_gap"
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_inventory_action_popup_runtime_routes_by_owner_shape() -> None:
    """InventoryActionEvent pure popup residuals are grouped by exact owner route."""
    families = {
        "crayons": (
            "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Crayons.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_crayons_popup_gap",
        ),
        "description": (
            "XRL.World.Parts/Description.cs::Description.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Description.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_description_look_popup_gap",
        ),
        "inventory": (
            "XRL.World.Parts/Inventory.cs::Inventory.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Inventory.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_inventory_drop_popup_gap",
        ),
        "vehicle": (
            "XRL.World.Parts/Vehicle.cs::Vehicle.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Vehicle.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_vehicle_follower_popup_gap",
        ),
        "grenade": (
            "XRL.World.Parts/IGrenade.cs::IGrenade.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/IGrenade.cs",
            "HandleEvent",
            "producer_runtime_inventory_action_grenade_detonate_popup_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"Popup": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-inventory-action-popup-runtime-test.json"),
    )

    assert entries[families["inventory"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["inventory"][0],
        "PopupAskNumberTranslationPatch.cs",
        "PopupAskNumberTranslationPatchTests.cs",
        "ui-popup.ja.json",
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id
        not in {
            "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Description.cs::Description.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Inventory.cs::Inventory.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/IGrenade.cs::IGrenade.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Vehicle.cs::Vehicle.HandleEvent(InventoryActionEvent)",
        }
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_world_part_runtime_routes_by_surface_shape() -> None:
    """World-part runtime rows are split by sink/producer shape."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.FireEvent(Event)",
                "XRL.World.Parts/ElementalJelly.cs",
                "FireEvent",
                {"MessageFrame": 1},
            ),
            _family(
                "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.DisplayOptions(GameObject)",
                "XRL.World.Parts/GolemQuestMound.cs",
                "DisplayOptions",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Parts/DanceRitualOpponent.cs::DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)",
                "XRL.World.Parts/DanceRitualOpponent.cs",
                "HandleEvent",
                {"AddPlayerMessage": 1},
            ),
            _family(
                "XRL.World.Parts/Harvestable.cs::Harvestable.AttemptHarvest(GameObject,bool,string,Cell,List<GameObject>)",
                "XRL.World.Parts/Harvestable.cs",
                "AttemptHarvest",
                {"Does": 1, "EmitMessage": 1, "MessageFrame": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert entries[
        "XRL.World.Parts/DanceRitualOpponent.cs::"
        "DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)"
    ]["closure_status"] == "covered_by_owner_route"
    assert entries[
        "XRL.World.Parts/GolemQuestMound.cs::"
        "GolemQuestMound.DisplayOptions(GameObject)"
    ]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        "XRL.World.Parts/GolemQuestMound.cs::"
        "GolemQuestMound.DisplayOptions(GameObject)",
        "GolemQuestMoundDisplayOptionsTranslationPatch.cs",
        "GolemQuestMoundDisplayOptionsTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_world_part_does_emit_message_frame_by_owner_shape() -> None:
    """World-part Does+EmitMessage+MessageFrame rows split into exact owner implementation candidates."""
    families = {
        "harvestable": (
            "XRL.World.Parts/Harvestable.cs::"
            "Harvestable.AttemptHarvest(GameObject,bool,string,Cell,List<GameObject>)",
            "XRL.World.Parts/Harvestable.cs",
            "AttemptHarvest",
            {"Does": 30, "EmitMessage": 5, "MessageFrame": 4},
            "producer_runtime_world_part_harvestable_attempt_gap",
        ),
        "campfire": (
            "XRL.World.Parts/Campfire.cs::Campfire.Extinguish(GameObject,GameObject)",
            "XRL.World.Parts/Campfire.cs",
            "Extinguish",
            {"Does": 4, "EmitMessage": 2, "MessageFrame": 2},
            "producer_runtime_world_part_campfire_extinguish_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    assert entries[families["harvestable"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["harvestable"][0],
        "XDidYTranslationPatchTests.cs",
        "MessagePatternTranslatorTests.cs",
    )
    assert entries[families["campfire"][0]]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        families["campfire"][0],
        "XDidYTranslationPatchTests.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "MessageFrames/verbs.ja.json",
    )

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-world-part-does-emit-frame-test.json"))
    assert residual["entries"] == []


def test_residual_bucket_payload_splits_world_part_queue_runtime_by_owner_shape() -> None:
    """Pure world-part queue rows split into exact owner implementation candidates."""
    families = {
        "dance_opponent_turn": (
            "XRL.World.Parts/DanceRitualOpponent.cs::"
            "DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)",
            "XRL.World.Parts/DanceRitualOpponent.cs",
            "HandleEvent",
            "producer_runtime_world_part_dance_opponent_debug_queue_gap",
        ),
        "player_dance": (
            "XRL.World.Parts/PlayerDanceRitual.cs::PlayerDanceRitual.FireEvent(Event)",
            "XRL.World.Parts/PlayerDanceRitual.cs",
            "FireEvent",
            "producer_runtime_world_part_player_dance_ritual_queue_gap",
        ),
        "dance_register": (
            "XRL.World.Parts/DanceRitualOpponent.cs::DanceRitualOpponent.Register(GameObject,IEventRegistrar)",
            "XRL.World.Parts/DanceRitualOpponent.cs",
            "Register",
            "producer_runtime_world_part_dance_opponent_register_queue_gap",
        ),
        "interior_damage": (
            "XRL.World.Parts/Interior.cs::Interior.HandleEvent(TookDamageEvent)",
            "XRL.World.Parts/Interior.cs",
            "HandleEvent",
            "producer_runtime_world_part_interior_damage_queue_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"AddPlayerMessage": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(inventory, inventory_path=Path("issue719-world-part-queue-test.json"))

    covered_family_ids = {
        families["dance_opponent_turn"][0],
        families["player_dance"][0],
        families["dance_register"][0],
        families["interior_damage"][0],
    }
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        if "DanceRitualOpponent" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "DanceRitualOpponentTranslationPatch.cs",
                "MessageQueueSemanticPipeline.cs",
            )
        if "PlayerDanceRitual" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PlayerDanceRitualTranslationPatch.cs",
                "MessageQueueSemanticPipeline.cs",
            )
        if "Interior.cs" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PhysicsProcessTakeDamageTranslationPatch.cs",
                "CombatAndLogMessageQueuePatchTests.cs",
            )
    assert {
        entry["closure_status"]
        for family_id, entry in entries.items()
        if family_id not in covered_family_ids
    } == set()
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_splits_world_part_message_frame_runtime_by_owner_shape() -> None:
    """Pure world-part MessageFrame rows split into exact owner implementation candidates."""
    families = {
        "elemental_jelly": (
            "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.FireEvent(Event)",
            "XRL.World.Parts/ElementalJelly.cs",
            "FireEvent",
            "producer_runtime_world_part_pseudopod_death_frame_gap",
        ),
        "panhumor": (
            "XRL.World.Parts/Panhumor.cs::Panhumor.FireEvent(Event)",
            "XRL.World.Parts/Panhumor.cs",
            "FireEvent",
            "producer_runtime_world_part_pseudopod_death_frame_gap",
        ),
        "pet_frondzie": (
            "XRL.World.Parts/PetFrondzie.cs::PetFrondzie.taunt(GameObject)",
            "XRL.World.Parts/PetFrondzie.cs",
            "taunt",
            "producer_runtime_world_part_pet_taunt_frame_gap",
        ),
        "vortex_periodic": (
            "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.SpaceTimeAnomalyPeriodicEvents()",
            "XRL.World.Parts/SpaceTimeVortex.cs",
            "SpaceTimeAnomalyPeriodicEvents",
            "producer_runtime_world_part_vortex_periodic_frame_gap",
        ),
        "liquid_cleaning": (
            "XRL.World.Parts/LiquidVolume.cs::"
            "LiquidVolume.CleaningMessage(GameObject,List<GameObject>,List<string>,GameObject,LiquidVolume,bool)",
            "XRL.World.Parts/LiquidVolume.cs",
            "CleaningMessage",
            "producer_runtime_world_part_liquid_cleaning_frame_gap",
        ),
        "liquid_contact": (
            "XRL.World.Parts/LiquidVolume.cs::LiquidVolume.ProcessContact(GameObject,bool,bool,bool,GameObject,bool,int)",
            "XRL.World.Parts/LiquidVolume.cs",
            "ProcessContact",
            "producer_runtime_world_part_liquid_contact_frame_gap",
        ),
        "pet_recipe": (
            "XRL.World.Parts/PetEbenshabat.cs::PetEbenshabat.HandleEvent(AfterLevelGainedEvent)",
            "XRL.World.Parts/PetEbenshabat.cs",
            "HandleEvent",
            "producer_runtime_world_part_pet_recipe_frame_gap",
        ),
        "shuttle": (
            "XRL.World.Parts/AIBarathrumShuttle.cs::AIBarathrumShuttle.ActionShipLaunch(GoalHandler)",
            "XRL.World.Parts/AIBarathrumShuttle.cs",
            "ActionShipLaunch",
            "producer_runtime_world_part_shuttle_frame_gap",
        ),
        "heat_self": (
            "XRL.World.Parts/HeatSelfOnFreeze.cs::HeatSelfOnFreeze.FireEvent(Event)",
            "XRL.World.Parts/HeatSelfOnFreeze.cs",
            "FireEvent",
            "producer_runtime_world_part_heat_self_frame_gap",
        ),
        "nephal_absorb": (
            "XRL.World.Parts/NephalProperties.cs::NephalProperties.AbsorbChords(GameObject)",
            "XRL.World.Parts/NephalProperties.cs",
            "AbsorbChords",
            "producer_runtime_world_part_nephal_absorb_frame_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"MessageFrame": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    covered_family_ids = {
        families["elemental_jelly"][0],
        families["panhumor"][0],
        families["heat_self"][0],
        families["nephal_absorb"][0],
        families["pet_frondzie"][0],
        families["pet_recipe"][0],
        families["shuttle"][0],
        families["vortex_periodic"][0],
        families["liquid_cleaning"][0],
        families["liquid_contact"][0],
    }
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        if family_id in {
            families["heat_self"][0],
            families["nephal_absorb"][0],
            families["shuttle"][0],
            families["pet_frondzie"][0],
            families["pet_recipe"][0],
            families["vortex_periodic"][0],
            families["liquid_cleaning"][0],
            families["liquid_contact"][0],
        }:
            _assert_evidence_contains(
                entries,
                family_id,
                "XDidYTranslationPatchTests.cs",
                "MessageFrames/verbs.ja.json",
            )
        else:
            _assert_evidence_contains(
                entries,
                family_id,
                'DidX("explode"',
                "MessageFrames/verbs.ja.json",
            )
    for family_id, _, _, _ in families.values():
        if family_id not in covered_family_ids:
            assert entries[family_id]["closure_status"] == "action_required"

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-message-frame-runtime-test.json"),
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }


def test_residual_bucket_payload_splits_world_part_popup_runtime_routes_by_owner_shape() -> None:
    """Pure world-part popup routes separate static owner gaps from dynamic runtime routes."""
    families = {
        "tinkering": (
            "XRL.World.Tinkering/TinkeringHelpers.cs::"
            "TinkeringHelpers.CheckMakersMark(GameObject,GameObject,IModification,string)",
            "XRL.World.Tinkering/TinkeringHelpers.cs",
            "CheckMakersMark",
            "producer_runtime_world_part_tinkering_popup_gap",
            "covered_by_owner_route",
        ),
        "shrine": (
            "XRL.World.Parts/Shrine.cs::Shrine.DesecrateShrine(GameObject,bool)",
            "XRL.World.Parts/Shrine.cs",
            "DesecrateShrine",
            "producer_runtime_world_part_shrine_popup_gap",
            "covered_by_owner_route",
        ),
        "disguise": (
            "XRL.World.Parts/ModDisguise.cs::ModDisguise.BeingAppliedBy(GameObject,GameObject)",
            "XRL.World.Parts/ModDisguise.cs",
            "BeingAppliedBy",
            "producer_runtime_world_part_disguise_popup_gap",
            "covered_by_owner_route",
        ),
        "ship_ark": (
            "XRL.World.Parts/ArkCore.cs::ArkCore.TryOpen(GameObject)",
            "XRL.World.Parts/ArkCore.cs",
            "TryOpen",
            "producer_runtime_world_part_ship_ark_popup_gap",
            "covered_by_owner_route",
        ),
        "grip_recoil": (
            "XRL.World.Parts/GripChange.cs::GripChange.TryChooseGrip(GameObject)",
            "XRL.World.Parts/GripChange.cs",
            "TryChooseGrip",
            "producer_runtime_world_part_grip_recoil_popup_gap",
            "covered_by_owner_route",
        ),
        "golem": (
            "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.DisplayOptions(GameObject)",
            "XRL.World.Parts/GolemQuestMound.cs",
            "DisplayOptions",
            "producer_runtime_world_part_golem_mound_popup_gap",
            "covered_by_owner_route",
        ),
        "movement": (
            "XRL.World.Parts/Physics.cs::Physics.ProcessTargetedMove"
            "(Cell,string,string,string,int?,bool,bool,bool,bool,bool,bool,string,string,GameObject)",
            "XRL.World.Parts/Physics.cs",
            "ProcessTargetedMove",
            "producer_runtime_world_part_movement_popup_covered",
            "covered_by_owner_route",
        ),
        "wish_debug": (
            "XRL.World.Parts/IZoneLandmark.cs::IZoneLandmark.WishCurrent()",
            "XRL.World.Parts/IZoneLandmark.cs",
            "WishCurrent",
            "producer_runtime_world_part_wish_debug_popup_gap",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"Popup": 1})
            for family_id, source_file, member_name, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, disposition in families.values():
        expected_status = (
            "covered_by_owner_route"
            if disposition == "covered_by_owner_route"
            else "action_required"
            if disposition == "likely_implementation_gap"
            else "runtime_required"
        )
        assert entries[family_id]["closure_status"] == expected_status
        if family_id == families["shrine"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupTranslationPatchTests.cs",
                "PopupPickOptionTranslationPatchTests.cs",
                "ui-popup.ja.json",
            )
        if family_id == families["tinkering"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "TinkeringHelpersMakersMarkTranslationPatch.cs",
                "PopupShowColorPickerTranslationPatch.cs",
                "TinkeringHelpersMakersMarkTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if family_id == families["wish_debug"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "SingleCallsiteOwnerPopupTranslationPatch.cs",
                "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if family_id == families["disguise"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "ModDisguiseBeingAppliedPopupTranslationPatch.cs",
                "ModDisguiseBeingAppliedPopupTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if family_id == families["golem"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "GolemQuestMoundDisplayOptionsTranslationPatch.cs",
                "GolemQuestMoundDisplayOptionsTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-popup-runtime-test.json"),
    )
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_residual_bucket_payload_splits_world_part_mixed_runtime_routes_by_exact_surface_set() -> None:
    """Mixed world-part runtime rows keep exact surface combinations visible."""
    families = {
        "queue_popup": (
            "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
            "XRL.World.Parts/Stomach.cs",
            "FireEvent",
            {"AddPlayerMessage": 1, "Popup": 1},
            "producer_runtime_world_part_stomach_water_queue_popup_gap",
        ),
        "queue_does": (
            "XRL.World.Parts/Physics.cs::Physics.HandleEvent(ObjectEnteringCellEvent)",
            "XRL.World.Parts/Physics.cs",
            "HandleEvent",
            {"AddPlayerMessage": 1, "Does": 1},
            "producer_runtime_world_part_queue_does_route_split",
        ),
        "does_only": (
            "XRL.World.Parts/TrembleEarthquakes.cs::TrembleEarthquakes.RocksFall(Zone)",
            "XRL.World.Parts/TrembleEarthquakes.cs",
            "RocksFall",
            {"Does": 1},
            "producer_runtime_world_part_does_route_split",
        ),
        "does_popup": (
            "XRL.World.Parts/VehicleMeleeInfiltration.cs::"
            "VehicleMeleeInfiltration.TryInfiltrate(GameObject,Interior)",
            "XRL.World.Parts/VehicleMeleeInfiltration.cs",
            "TryInfiltrate",
            {"Does": 1, "Popup": 1},
            "producer_runtime_world_part_vehicle_infiltration_popup_gap",
        ),
        "does_emit": (
            "XRL.World.Parts/Chat.cs::Chat.PerformChat(GameObject,bool)",
            "XRL.World.Parts/Chat.cs",
            "PerformChat",
            {"Does": 1, "EmitMessage": 1},
            "producer_runtime_world_part_chat_emit_gap",
        ),
        "does_message_frame": (
            "XRL.World.Parts/AutomatedExternalDefibrillator.cs::"
            "AutomatedExternalDefibrillator.AttemptDefibrillate(GameObject,IEvent)",
            "XRL.World.Parts/AutomatedExternalDefibrillator.cs",
            "AttemptDefibrillate",
            {"Does": 1, "MessageFrame": 1},
            "producer_runtime_world_part_defibrillator_gap",
        ),
        "popup_message_frame": (
            "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.FireEvent(Event)",
            "XRL.World.Parts/MagazineAmmoLoader.cs",
            "FireEvent",
            {"MessageFrame": 1, "Popup": 1},
            "producer_runtime_world_part_magazine_supply_gap",
        ),
        "emit_popup": (
            "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.CheckTimer()",
            "XRL.World.Parts/ShevaStarshipControl.cs",
            "CheckTimer",
            {"EmitMessage": 1, "Popup": 1},
            "producer_runtime_world_part_ship_ark_popup_gap",
        ),
        "emit_frame_popup": (
            "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.ApplyVortex(GameObject)",
            "XRL.World.Parts/SpaceTimeVortex.cs",
            "ApplyVortex",
            {"EmitMessage": 1, "MessageFrame": 1, "Popup": 1},
            "producer_runtime_world_part_vortex_apply_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    covered_family_id = families["queue_does"][0]
    assert entries[covered_family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        covered_family_id,
        "PhysicsObjectEnteringCellTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
    )
    does_only_family_id = families["does_only"][0]
    assert entries[does_only_family_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        does_only_family_id,
        "TrembleEarthquakes.RocksFall",
        "falling rocks",
        "CombatAndLogMessageQueuePatchTests.cs",
    )

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-mixed-surface-test.json"),
    )

    assert payload["bucket_counts"] == {
        bucket: 1
        for key, (_, _, _, _, bucket) in families.items()
        if key not in {
            "queue_popup",
            "queue_does",
            "does_only",
            "does_popup",
            "does_emit",
            "does_message_frame",
            "popup_message_frame",
            "emit_popup",
            "emit_frame_popup",
        }
    }


def test_policy_promotes_issue719_world_part_queue_does_existing_owner_routes() -> None:
    """Exact Physics/ThiefBot queue+Does rows are already covered by owner routes."""
    families = {
        "physics_entering_cell": (
            "XRL.World.Parts/Physics.cs::Physics.HandleEvent(ObjectEnteringCellEvent)",
            "XRL.World.Parts/Physics.cs",
            "HandleEvent",
            {"AddPlayerMessage": 6, "Does": 6},
        ),
        "thief_bot": (
            "XRL.World.Parts/ThiefBot.cs::ThiefBot.FireEvent(Event)",
            "XRL.World.Parts/ThiefBot.cs",
            "FireEvent",
            {"AddPlayerMessage": 4, "Does": 5},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert {entry["closure_status"] for entry in entries.values()} == {"covered_by_owner_route"}
    _assert_evidence_contains(
        entries,
        families["physics_entering_cell"][0],
        "PhysicsObjectEnteringCellTranslationPatch.cs",
        "CombatAndLogMessageQueuePatchTests.cs",
        "decompiled owner source: XRL.World.Parts/Physics.cs lines 2565-2599",
    )
    _assert_evidence_contains(
        entries,
        families["thief_bot"][0],
        "SingleCallsiteOwnerQueueTranslationPatch.cs",
        "SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "DoesVerbFamilyTests.cs",
        "verbs.ja.json",
        "decompiled owner source: XRL.World.Parts/ThiefBot.cs lines 22-77",
    )

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-queue-does-promotion-test.json"),
    )
    assert payload["entries"] == []
    assert payload["bucket_counts"] == {}


def test_residual_bucket_payload_splits_world_part_does_emit_runtime_by_owner_shape() -> None:
    """World-part Does+Emit rows are exact owner implementation candidates."""
    families = {
        "chat": (
            "XRL.World.Parts/Chat.cs::Chat.PerformChat(GameObject,bool)",
            "XRL.World.Parts/Chat.cs",
            "PerformChat",
            {"Does": 1, "EmitMessage": 16},
            "producer_runtime_world_part_chat_emit_gap",
        ),
        "fungal": (
            "XRL.World.Parts/FungalInfection.cs::FungalInfection.FireEvent(Event)",
            "XRL.World.Parts/FungalInfection.cs",
            "FireEvent",
            {"Does": 2, "EmitMessage": 12},
            "producer_runtime_world_part_fungal_cure_emit_gap",
        ),
        "vehicle": (
            "XRL.World.Parts/VehicleMeleeInfiltration.cs::"
            "VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent)",
            "XRL.World.Parts/VehicleMeleeInfiltration.cs",
            "HandleEvent",
            {"Does": 1, "EmitMessage": 2},
            "producer_runtime_world_part_vehicle_infiltration_emit_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-does-emit-test.json"),
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    assert {entry["closure_status"] for entry in entries.values()} == {"covered_by_owner_route"}
    assert payload["entries"] == []


def test_residual_bucket_payload_splits_world_part_queue_popup_runtime_by_owner_shape() -> None:
    """World-part queue+popup rows split into exact owner implementation candidates."""
    families = {
        "stomach": (
            "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
            "XRL.World.Parts/Stomach.cs",
            "FireEvent",
            "producer_runtime_world_part_stomach_water_queue_popup_gap",
        ),
        "elevator": (
            "XRL.World.Parts/ElevatorSwitch.cs::ElevatorSwitch.FireEvent(Event)",
            "XRL.World.Parts/ElevatorSwitch.cs",
            "FireEvent",
            "producer_runtime_world_part_elevator_switch_queue_popup_gap",
        ),
        "biome": (
            "XRL.World.Biomes/BiomeManager.cs::BiomeManager.DisplaySurfaceDistribution(string)",
            "XRL.World.Biomes/BiomeManager.cs",
            "DisplaySurfaceDistribution",
            "producer_runtime_world_part_biome_distribution_queue_popup_gap",
        ),
        "giant_clam": (
            "XRL.World.Parts/GiantClamProperties.cs::GiantClamProperties.TeleportFromClamWorld(GameObject)",
            "XRL.World.Parts/GiantClamProperties.cs",
            "TeleportFromClamWorld",
            "producer_runtime_world_part_giant_clam_dimension_queue_popup_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, {"AddPlayerMessage": 1, "Popup": 1})
            for family_id, source_file, member_name, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    payload = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-world-part-queue-popup-test.json"),
    )

    assert entries[families["stomach"][0]]["closure_status"] == "covered_by_owner_route"
    covered_family_ids = {
        families["stomach"][0],
        families["elevator"][0],
        families["biome"][0],
        families["giant_clam"][0],
    }
    _assert_evidence_contains(
        entries,
        families["elevator"][0],
        "SingleCallsiteOwnerQueueTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    _assert_evidence_contains(
        entries,
        families["biome"][0],
        "SingleCallsiteOwnerPopupTranslationPatch.cs",
        "SingleCallsiteOwnerQueueTranslationPatch.cs",
        "SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "TargetMethodResolutionTests.cs",
    )
    assert {
        entry["closure_status"]
        for family_id, entry in entries.items()
        if family_id not in covered_family_ids
    } == set()
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
    } == {
        family_id: (bucket, "likely_implementation_gap")
        for family_id, _, _, bucket in families.values()
        if family_id not in covered_family_ids
    }
    assert payload["disposition_counts"] == {}


def test_residual_bucket_payload_records_autoact_description_owner_route_closure() -> None:
    """AutoAct action-description labels are closed by the exact owner-return patch."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Skills.Cooking/AppleMatz.cs::AppleMatz.GetDescription()",
                "XRL.World.Skills.Cooking/AppleMatz.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                "XRL.World.Capabilities/AutoAct.cs::AutoAct.GetDescription(string,OngoingAction)",
                "XRL.World.Capabilities/AutoAct.cs",
                "GetDescription",
                {"EffectDescriptionReturn": 1},
            ),
            _family(
                "XRL.World/WorldFactory.cs::WorldFactory.LoadWorldNode(XmlTextReader)",
                "XRL.World/WorldFactory.cs",
                "LoadWorldNode",
                {"DisplayNameAssignment": 1},
            ),
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["entries"] == []
    assert payload["bucket_counts"] == {}
    assert payload["disposition_counts"] == {}

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    autoact_id = "XRL.World.Capabilities/AutoAct.cs::AutoAct.GetDescription(string,OngoingAction)"
    assert entries[autoact_id]["closure_status"] == "covered_by_owner_route"
    _assert_evidence_contains(
        entries,
        autoact_id,
        "AutoAct.GetDescription",
        "ActionEffectDescriptionReturnTranslationPatch",
        "ActionEffectDescriptionReturnTranslationPatchTests",
        "TargetMethodResolutionTests",
    )


def test_residual_bucket_payload_splits_active_effect_message_routes_by_surface_shape() -> None:
    """Effect popup, queue, and message-frame rows need different follow-up routes."""
    inventory = _inventory(
        [
            _family(
                "XRL.World.Effects/UncoveredFrameEffect.cs::UncoveredFrameEffect.FireEvent(Event)",
                "XRL.World.Effects/UncoveredFrameEffect.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
            _family(
                "XRL.World.Effects/UncoveredPopupEffect.cs::UncoveredPopupEffect.FireEvent(Event)",
                "XRL.World.Effects/UncoveredPopupEffect.cs",
                "FireEvent",
                {"Popup": 1},
            ),
            _family(
                "XRL.World.Effects/UncoveredQueueEffect.cs::UncoveredQueueEffect.Apply(GameObject)",
                "XRL.World.Effects/UncoveredQueueEffect.cs",
                "Apply",
                {"AddPlayerMessage": 1},
            ),
        ]
    )

    payload = residual_bucket_payload(inventory, inventory_path=Path("inventory.json"))

    assert payload["bucket_counts"] == {
        "active_effect_message_frame_route_split": 1,
        "active_effect_popup_route_split": 1,
        "active_effect_queue_route_split": 1,
    }
    assert payload["disposition_counts"] == {"runtime_evidence_required": 3}


def test_policy_promotes_vehicle_repair_does_route_without_overclaiming_unproven_siblings() -> None:
    """VehicleRepair has direct owner evidence; nearby MessageFrame/Does rows still require proof."""
    vehicle_repair_family_id = "XRL.World.Parts/VehicleRepair.cs::VehicleRepair.HandleEvent(InventoryActionEvent)"
    unproven_frame_family_id = "XRL.World.Parts/UnreviewedFrame.cs::UnreviewedFrame.FireEvent(Event)"
    unproven_does_family_id = "XRL.World.Parts/UnreviewedDoes.cs::UnreviewedDoes.HandleEvent(InventoryActionEvent)"
    popup_mixed_family_id = "XRL.World.Parts/UnreviewedPopupFrame.cs::UnreviewedPopupFrame.FireEvent(Event)"
    inventory = _inventory(
        [
            _family(
                vehicle_repair_family_id,
                "XRL.World.Parts/VehicleRepair.cs",
                "HandleEvent",
                {"Does": 15},
            ),
            _family(
                unproven_frame_family_id,
                "XRL.World.Parts/UnreviewedFrame.cs",
                "FireEvent",
                {"MessageFrame": 3},
            ),
            _family(
                unproven_does_family_id,
                "XRL.World.Parts/UnreviewedDoes.cs",
                "HandleEvent",
                {"Does": 2},
            ),
            _family(
                popup_mixed_family_id,
                "XRL.World.Parts/UnreviewedPopupFrame.cs",
                "FireEvent",
                {"MessageFrame": 1, "Popup": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[vehicle_repair_family_id]["closure_lane"] == "combat_message_frame_does"
    assert entries[vehicle_repair_family_id]["closure_status"] == "covered_by_owner_route"
    vehicle_evidence = " ".join(entries[vehicle_repair_family_id]["closure_evidence"])
    assert "VehicleRepair.HandleEvent" in vehicle_evidence
    assert "ClonelingVehicleTranslationPatch.cs" in vehicle_evidence
    assert "WorldPartsProducerTranslationPatchTests.cs" in vehicle_evidence
    assert "TargetMethodResolutionTests.cs" in vehicle_evidence

    for family_id in (unproven_frame_family_id, unproven_does_family_id, popup_mixed_family_id):
        assert entries[family_id]["closure_status"] == "action_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "producer_message_family_audit" in evidence


def test_policy_promotes_reviewed_mutation_action_message_frames() -> None:
    """Reviewed mutation action frames are covered by global XDidY plus concrete repository keys."""
    family_ids = {
        "cloneling": (
            "XRL.World.Parts/Cloneling.cs::Cloneling.PerformCloning(GameObject)",
            "XRL.World.Parts/Cloneling.cs",
            "PerformCloning",
            {"MessageFrame": 22},
        ),
        "stunning_force": (
            "XRL.World.Parts.Mutation/StunningForce.cs::"
            "StunningForce.Concussion(Cell,GameObject,int,int,int,GameObject,bool,bool)",
            "XRL.World.Parts.Mutation/StunningForce.cs",
            "Concussion",
            {"MessageFrame": 22},
        ),
        "delayed_line": (
            "XRL.World.Parts.Mutation/IDelayedLineMutation.cs::"
            "IDelayedLineMutation.Refract(List<Cell>)",
            "XRL.World.Parts.Mutation/IDelayedLineMutation.cs",
            "Refract",
            {"MessageFrame": 17},
        ),
        "decarbonizer": (
            "XRL.World.Parts.Mutation/Decarbonizer.cs::"
            "Decarbonizer.fireBeam(List<Cell>,bool)",
            "XRL.World.Parts.Mutation/Decarbonizer.cs",
            "fireBeam",
            {"MessageFrame": 17},
        ),
        "liquid_spitter": (
            "XRL.World.Parts.Mutation/LiquidSpitter.cs::LiquidSpitter.HandleEvent(CommandEvent)",
            "XRL.World.Parts.Mutation/LiquidSpitter.cs",
            "HandleEvent",
            {"MessageFrame": 10},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in family_ids.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _, _ in family_ids.values():
        assert entries[family_id]["closure_lane"] == "combat_message_frame_does"
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "XDidYTranslationPatch.cs" in evidence
        assert "MessageFrameTranslatorTests.cs" in evidence
        assert "MessageFrames/verbs.ja.json" in evidence

    cloneling_evidence = " ".join(entries[family_ids["cloneling"][0]]["closure_evidence"])
    assert "a clone of {0}" in cloneling_evidence
    assert "TranslatesClonelingProduceCloneFrame" in cloneling_evidence


def test_policy_records_issue719_residual_mutation_command_popup_frame_tranche() -> None:
    """Reviewed residual mutation command popup/frame families are covered by existing routes."""
    family_ids = {
        "sticky_tongue": (
            "XRL.World.Parts.Mutation/StickyTongue.cs::"
            "StickyTongue.HarpoonNearest(GameObject,int,string,int,bool,bool)",
            "XRL.World.Parts.Mutation/StickyTongue.cs",
            "HarpoonNearest",
            {"Popup": 1, "MessageFrame": 3},
        ),
        "slog_glands": (
            "XRL.World.Parts.Mutation/SlogGlands.cs::SlogGlands.FireEvent(Event)",
            "XRL.World.Parts.Mutation/SlogGlands.cs",
            "FireEvent",
            {"Popup": 3, "MessageFrame": 1},
        ),
        "stinger": (
            "XRL.World.Parts.Mutation/Stinger.cs::Stinger.HandleEvent(CommandEvent)",
            "XRL.World.Parts.Mutation/Stinger.cs",
            "HandleEvent",
            {"Popup": 2, "MessageFrame": 1},
        ),
        "ley_shifting": (
            "XRL.World.Parts.Mutation/LeyShifting.cs::LeyShifting.HandleEvent(CommandEvent)",
            "XRL.World.Parts.Mutation/LeyShifting.cs",
            "HandleEvent",
            {"Popup": 2, "MessageFrame": 1},
        ),
        "burgeoning": (
            "XRL.World.Parts.Mutation/Burgeoning.cs::Burgeoning.Burgeon()",
            "XRL.World.Parts.Mutation/Burgeoning.cs",
            "Burgeon",
            {"Popup": 1, "MessageFrame": 1},
        ),
        "phasing": (
            "XRL.World.Parts.Mutation/Phasing.cs::Phasing.FireEvent(Event)",
            "XRL.World.Parts.Mutation/Phasing.cs",
            "FireEvent",
            {"Popup": 1},
        ),
        "spacetime_vortex": (
            "XRL.World.Parts.Mutation/SpacetimeVortex.cs::SpacetimeVortex.FireEvent(Event)",
            "XRL.World.Parts.Mutation/SpacetimeVortex.cs",
            "FireEvent",
            {"Popup": 2},
        ),
        "burrowing": (
            "XRL.World.Parts.Mutation/Burrowing.cs::Burrowing.HandleEvent(CommandEvent)",
            "XRL.World.Parts.Mutation/Burrowing.cs",
            "HandleEvent",
            {"Popup": 2},
        ),
        "spinnerets": (
            "XRL.World.Parts.Mutation/Spinnerets.cs::Spinnerets.FireEvent(Event)",
            "XRL.World.Parts.Mutation/Spinnerets.cs",
            "FireEvent",
            {"Popup": 1},
        ),
        "electrical_generation": (
            "XRL.World.Parts.Mutation/ElectricalGeneration.cs::"
            "ElectricalGeneration.PerformDischarge(bool)",
            "XRL.World.Parts.Mutation/ElectricalGeneration.cs",
            "PerformDischarge",
            {"Popup": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in family_ids.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _, _ in family_ids.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "PopupShowTranslationPatchTests.cs" in evidence
        assert "MessageFrameTranslatorTests.cs" in evidence
        assert "MutationActionFailureTranslationPatchTests.cs" in evidence
        assert "verbs.ja.json" in evidence
        assert "ui-popup.ja.json" in evidence


def test_policy_records_issue719_residual_popup_frame_split_tranche() -> None:
    """Reviewed residual MessageFrame+Popup families are covered or moved to runtime evidence."""
    covered_families = {
        "stomach": (
            "XRL.World.Parts/Stomach.cs::Stomach.HandleEvent(BeginTakeActionEvent)",
            "XRL.World.Parts/Stomach.cs",
            "HandleEvent",
            {"Popup": 4, "MessageFrame": 4},
        ),
        "reshephs_crypt": (
            "XRL.World.Parts/ReshephsCrypt.cs::ReshephsCrypt.FireEvent(Event)",
            "XRL.World.Parts/ReshephsCrypt.cs",
            "FireEvent",
            {"Popup": 4, "MessageFrame": 1},
        ),
        "stilt_well": (
            "XRL.World.Parts/StiltWell.cs::StiltWell.GiveArtifacts(GameObject,GameObject)",
            "XRL.World.Parts/StiltWell.cs",
            "GiveArtifacts",
            {"Popup": 1, "MessageFrame": 1},
        ),
        "reborn": (
            "XRL.World.Parts/RebornOnDeathInThinWorld.cs::RebornOnDeathInThinWorld.FireEvent(Event)",
            "XRL.World.Parts/RebornOnDeathInThinWorld.cs",
            "FireEvent",
            {"Popup": 1, "MessageFrame": 1},
        ),
        "engulfing_descends": (
            "XRL.World.Parts/EngulfingDescends.cs::EngulfingDescends.FireEvent(Event)",
            "XRL.World.Parts/EngulfingDescends.cs",
            "FireEvent",
            {"Popup": 2, "MessageFrame": 1},
        ),
        "infiltrate": (
            "XRL.World.Parts.Mutation/Infiltrate.cs::Infiltrate.FireEvent(Event)",
            "XRL.World.Parts.Mutation/Infiltrate.cs",
            "FireEvent",
            {"Popup": 1, "MessageFrame": 1},
        ),
        "ambient_power": (
            "XRL.World.Parts/AmbientPowerReceiver.cs::AmbientPowerReceiver.HandleEvent(EnteringZoneEvent)",
            "XRL.World.Parts/AmbientPowerReceiver.cs",
            "HandleEvent",
            {"Popup": 1, "MessageFrame": 2},
        ),
        "restore_on_death": (
            "XRL.World.Parts/RestoreOnDeath.cs::RestoreOnDeath.HandleEvent(BeforeDieEvent)",
            "XRL.World.Parts/RestoreOnDeath.cs",
            "HandleEvent",
            {"Popup": 1, "MessageFrame": 1},
        ),
        "mod_displacer": (
            "XRL.World.Parts/ModDisplacer.cs::ModDisplacer.ExamineFailure(IExamineEvent,int)",
            "XRL.World.Parts/ModDisplacer.cs",
            "ExamineFailure",
            {"Popup": 1, "MessageFrame": 1},
        ),
        "magazine_ammo_loader": (
            "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.FireEvent(Event)",
            "XRL.World.Parts/MagazineAmmoLoader.cs",
            "FireEvent",
            {"Popup": 1, "MessageFrame": 1},
        ),
    }
    implementation_gap_families = {}
    runtime_families = {
        "brain": (
            "XRL.World.Parts/Brain.cs::Brain.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Brain.cs",
            "HandleEvent",
            {"Popup": 1, "MessageFrame": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in [
                *covered_families.values(),
                *implementation_gap_families.values(),
                *runtime_families.values(),
            ]
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _, _ in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        if family_id == covered_families["magazine_ammo_loader"][0]:
            assert "PopupAskNumberTranslationPatchTests.cs" in evidence
            assert "XDidYTranslationPatchTests.cs" in evidence
            assert "verbs.ja.json" in evidence
        else:
            assert "PopupShowTranslationPatchTests.cs" in evidence
            assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in evidence
            assert "MessageFrameTranslatorTests.cs" in evidence
            assert "verbs.ja.json" in evidence

    for family_id, _, _, _ in implementation_gap_families.values():
        assert entries[family_id]["closure_status"] == "action_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "MagazineAmmoLoader.FireEvent" in evidence
        assert "SupplyIntegratedHostWithAmmo" in evidence

    for family_id, _, _, _ in runtime_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "DebugInternals" in evidence
        assert "DebugAttitude" in evidence

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-popup-frame-test.json"))
    runtime_entries = {
        entry["family_id"]: entry
        for entry in residual["entries"]
        if entry["family_id"] in {family_id for family_id, _, _, _ in runtime_families.values()}
    }
    assert set(runtime_entries) == set()
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in {family_id for family_id, _, _, _ in implementation_gap_families.values()}
    } == {
        family_id: "likely_implementation_gap"
        for family_id, _, _, _ in implementation_gap_families.values()
    }
    _assert_producer_runtime_residuals(
        residual,
        set(),
    )


def test_policy_records_issue719_residual_does_message_frame_split_tranche() -> None:
    """Reviewed residual Does+MessageFrame families are covered or moved to runtime evidence."""
    covered_families = {
        "pettable": (
            "XRL.World.Parts/Pettable.cs::Pettable.Pet(GameObject)",
            "XRL.World.Parts/Pettable.cs",
            "Pet",
            {"Does": 1, "MessageFrame": 1},
        ),
        "robot": (
            "XRL.World.Parts/Robot.cs::Robot.FireEvent(Event)",
            "XRL.World.Parts/Robot.cs",
            "FireEvent",
            {"Does": 1, "MessageFrame": 1},
        ),
        "programmable_recoiler": (
            "XRL.World.Parts/IProgrammableRecoiler.cs::IProgrammableRecoiler.ProgramRecoiler(GameObject,IEvent)",
            "XRL.World.Parts/IProgrammableRecoiler.cs",
            "ProgramRecoiler",
            {"Does": 2, "MessageFrame": 1},
        ),
        "hookah": (
            "XRL.World.Parts/Hookah.cs::Hookah.SmokeHookah(GameObject,bool)",
            "XRL.World.Parts/Hookah.cs",
            "SmokeHookah",
            {"Does": 1, "MessageFrame": 1},
        ),
    }
    runtime_families = {
        "temporal_fugue": (
            "XRL.World.Parts.Mutation/TemporalFugue.cs::"
            "TemporalFugue.PerformTemporalFugue(GameObject,GameObject,GameObject,TemporalFugue,IEvent,bool,bool,"
            "int?,int?,int,string,string,string,string,string)",
            "XRL.World.Parts.Mutation/TemporalFugue.cs",
            "PerformTemporalFugue",
            {"Does": 1, "MessageFrame": 2},
        ),
        "defibrillator": (
            "XRL.World.Parts/AutomatedExternalDefibrillator.cs::"
            "AutomatedExternalDefibrillator.AttemptDefibrillate(GameObject,IEvent)",
            "XRL.World.Parts/AutomatedExternalDefibrillator.cs",
            "AttemptDefibrillate",
            {"Does": 1, "MessageFrame": 2},
        ),
        "force_lathe": (
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
            "CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe(GameObject,GameObject,IEvent)",
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs",
            "ActivatePrecisionForceLathe",
            {"Does": 1, "MessageFrame": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in [
                *covered_families.values(),
                *runtime_families.values(),
            ]
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for family_id, _, _, _ in covered_families.values():
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "DoesVerbRouteTranslatorTests.cs" in evidence
        assert "MessageFrameTranslatorTests.cs" in evidence
        assert "verbs.ja.json" in evidence

    implementation_gap_family_ids = set()
    covered_family_ids = {
        runtime_families["defibrillator"][0],
        runtime_families["force_lathe"][0],
        runtime_families["temporal_fugue"][0],
    }
    for family_id, _, _, _ in runtime_families.values():
        if family_id in covered_family_ids:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            if "AutomatedExternalDefibrillator" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "AutomatedExternalDefibrillatorTranslationPatch.cs",
                    "AutomatedExternalDefibrillatorTranslationPatchTests.cs",
                    "TargetMethodResolutionTests.cs",
                    "verbs.ja.json",
                )
            if "CyberneticsPrecisionForceLathe" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "CyberneticsPrecisionForceLatheTranslationPatch.cs",
                    "CyberneticsPrecisionForceLatheTranslationPatchTests.cs",
                    "TargetMethodResolutionTests.cs",
                    "verbs.ja.json",
                )
            if "TemporalFugue" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "ui-popup.ja.json",
                    "PopupShowTranslationPatchTests.cs",
                    "MessageFrameTranslatorTests.cs",
                    "verbs.ja.json",
                )
            continue
        if family_id in implementation_gap_family_ids:
            assert entries[family_id]["closure_status"] == "action_required"
            evidence = " ".join(entries[family_id]["closure_evidence"])
            assert "static owner shape" in evidence
            continue
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "runtime-required" in evidence
        assert "generated" in evidence

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-does-frame-test.json"))
    _assert_producer_runtime_residuals(
        residual,
        {
            family_id
            for family_id, _, _, _ in runtime_families.values()
            if family_id not in implementation_gap_family_ids | covered_family_ids
        },
    )
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")
    assert "producer_runtime_world_part_defibrillator_gap" not in {
        entry["residual_bucket"] for entry in residual["entries"]
    }


def test_policy_records_issue719_residual_pure_popup_top_split_tranche() -> None:
    """Broad pure-popup families are split from exact fixed popup and inventory owner coverage."""
    covered_family_id = "XRL.UI/GritGateTerminalScreenRoot.cs::GritGateTerminalScreenRoot.UpdatePowerOptions()"
    runtime_families = {
        "options": ("XRL.UI/OptionsUI.cs::OptionsUI.Show()", "XRL.UI/OptionsUI.cs", "Show", {"Popup": 67}),
        "scores": ("XRL.Core/Scores.cs::Scores.Show()", "XRL.Core/Scores.cs", "Show", {"Popup": 46}),
        "item_naming": (
            "XRL.World.Capabilities/ItemNaming.cs::"
            "ItemNaming.NameItem(GameObject,GameObject,GameObject,GameObject,string,string,bool)",
            "XRL.World.Capabilities/ItemNaming.cs",
            "NameItem",
            {"Popup": 27},
        ),
        "crayons": (
            "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Crayons.cs",
            "HandleEvent",
            {"Popup": 20},
        ),
        "description": (
            "XRL.World.Parts/Description.cs::Description.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Description.cs",
            "HandleEvent",
            {"Popup": 20},
        ),
        "inventory": (
            "XRL.World.Parts/Inventory.cs::Inventory.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Inventory.cs",
            "HandleEvent",
            {"Popup": 18},
        ),
        "trade": (
            "XRL.UI/TradeUI.cs::TradeUI.ShowVendorActions(GameObject,GameObject,bool)",
            "XRL.UI/TradeUI.cs",
            "ShowVendorActions",
            {"Popup": 17},
        ),
    }
    inventory = _inventory(
        [
            _family(covered_family_id, "XRL.UI/GritGateTerminalScreenRoot.cs", "UpdatePowerOptions", {"Popup": 23}),
            *[
                _family(family_id, source_file, member_name, surfaces)
                for family_id, source_file, member_name, surfaces in runtime_families.values()
            ],
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert entries[covered_family_id]["closure_status"] == "covered_by_owner_route"
    covered_evidence = " ".join(entries[covered_family_id]["closure_evidence"])
    assert "GritGateTerminalScreenRoot.UpdatePowerOptions" in covered_evidence
    assert "PopupShowTranslationPatchTests.cs" in covered_evidence
    assert "ui-popup.ja.json" in covered_evidence

    implementation_gap_family_ids: set[str] = set()
    covered_family_ids = {
        runtime_families["options"][0],
        runtime_families["scores"][0],
        runtime_families["item_naming"][0],
        runtime_families["crayons"][0],
        runtime_families["description"][0],
        runtime_families["inventory"][0],
        runtime_families["trade"][0],
    }
    for family_id, _, _, _ in runtime_families.values():
        if family_id in covered_family_ids:
            expected_status = "covered_by_owner_route"
        else:
            expected_status = "action_required" if family_id in implementation_gap_family_ids else "runtime_required"
        assert entries[family_id]["closure_status"] == expected_status
        evidence = " ".join(entries[family_id]["closure_evidence"])
        if family_id in covered_family_ids:
            assert (
                "LegacyScoresScreenTranslationPatchTests.cs" in evidence
                or "LegacyOptionsUiTranslationPatchTests.cs" in evidence
                or "CrayonsPopupTranslationPatchTests.cs" in evidence
                or "ItemNamingTranslationPatchTests.cs" in evidence
                or "DescriptionLookPopupTranslationPatchTests.cs" in evidence
                or "PopupAskNumberTranslationPatchTests.cs" in evidence
                or "TradeUiPopupTranslationPatchTests.cs" in evidence
            )
        elif family_id in implementation_gap_family_ids:
            assert (
                "fixed player-visible text and route-local generated captures" in evidence
                or "residual_disposition=likely_implementation_gap" in evidence
            )
        else:
            assert "runtime-required" in evidence
            assert "static inventory cannot split" in evidence

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-pure-popup-top-test.json"))
    _assert_producer_runtime_residuals(
        residual,
        {family_id for family_id, _, _, _ in runtime_families.values()}
        - implementation_gap_family_ids
        - covered_family_ids,
    )
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")


def test_policy_records_issue719_residual_ui_popup_runtime_tranche() -> None:  # noqa: C901, PLR0912
    """UI screen and picker pure-Popup families require runtime route evidence."""
    runtime_families = {
        "object_finder": (
            "XRL.UI/ObjectFinder.cs::ObjectFinder.ConfigFilters()",
            "XRL.UI/ObjectFinder.cs",
            "ConfigFilters",
            {"Popup": 16},
        ),
        "equipment_api": (
            "Qud.API/EquipmentAPI.cs::"
            "EquipmentAPI.ShowInventoryActionMenu(Dictionary<string,InventoryAction>,GameObject,GameObject,bool,bool,"
            "string,IComparer<InventoryAction>,bool)",
            "Qud.API/EquipmentAPI.cs",
            "ShowInventoryActionMenu",
            {"Popup": 12},
        ),
        "build_library_options": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.HandleMenuOption(MenuOption)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "HandleMenuOption",
            {"Popup": 11},
        ),
        "build_library_add": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.AddBuild(string)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "AddBuild",
            {"Popup": 7},
        ),
        "build_library_select": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
            "QudBuildLibraryModuleWindow.onSelect(FrameworkDataElement)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs",
            "onSelect",
            {"Popup": 5},
        ),
        "equipment_screen": (
            "XRL.UI/EquipmentScreen.cs::EquipmentScreen.ShowBodypartEquipUI(GameObject,BodyPart)",
            "XRL.UI/EquipmentScreen.cs",
            "ShowBodypartEquipUI",
            {"Popup": 9},
        ),
        "inventory_status": (
            "Qud.UI/InventoryAndEquipmentStatusScreen.cs::"
            "InventoryAndEquipmentStatusScreen.HandleShowOptions()",
            "Qud.UI/InventoryAndEquipmentStatusScreen.cs",
            "HandleShowOptions",
            {"Popup": 8},
        ),
        "factions_status": (
            "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.HandleCmdOptions()",
            "Qud.UI/FactionsStatusScreen.cs",
            "HandleCmdOptions",
            {"Popup": 8},
        ),
        "command_binding": (
            "XRL.UI/CommandBindingManager.cs::CommandBindingManager.RestoreDefaults()",
            "XRL.UI/CommandBindingManager.cs",
            "RestoreDefaults",
            {"Popup": 7},
        ),
        "ability_manager": (
            "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.showScreen(XRL.World.GameObject)",
            "Qud.UI/AbilityManagerScreen.cs",
            "showScreen",
            {"Popup": 5},
        ),
        "embark_builder": (
            "XRL.CharacterBuilds/EmbarkBuilder.cs::EmbarkBuilder.checkStateAsync()",
            "XRL.CharacterBuilds/EmbarkBuilder.cs",
            "checkStateAsync",
            {"Popup": 4},
        ),
        "build_summary": (
            "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs::"
            "QudBuildSummaryModuleWindow.HandleMenuOption(MenuOption)",
            "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs",
            "HandleMenuOption",
            {"Popup": 4},
        ),
        "mod_manager_cancel": (
            "Qud.UI/ModManagerUI.cs::ModManagerUI.OnCancel()",
            "Qud.UI/ModManagerUI.cs",
            "OnCancel",
            {"Popup": 4},
        ),
        "mutation_menu": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::"
            "QudMutationsModuleWindow.HandleMenuOption(MenuOption)",
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
            "HandleMenuOption",
            {"Popup": 3},
        ),
        "mutation_variant": (
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::"
            "QudMutationsModuleWindow.SelectVariant()",
            "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs",
            "SelectVariant",
            {"Popup": 2},
        ),
        "framework_search": (
            "XRL.UI.Framework/FrameworkSearchInput.cs::FrameworkSearchInput.ChangeValue()",
            "XRL.UI.Framework/FrameworkSearchInput.cs",
            "ChangeValue",
            {"Popup": 2},
        ),
        "options_screen": (
            "Qud.UI/OptionsScreen.cs::OptionsScreen.HandleMenuOption(FrameworkDataElement)",
            "Qud.UI/OptionsScreen.cs",
            "HandleMenuOption",
            {"Popup": 1},
        ),
        "gender_customize": (
            "XRL.World/Gender.cs::Gender.CustomizeProcess(string)",
            "XRL.World/Gender.cs",
            "CustomizeProcess",
            {"Popup": 6},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in runtime_families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert sum(entry["text_construction_count"] for entry in entries.values()) == 114
    implementation_gap_keys = set()
    implementation_gap_family_ids = {runtime_families[key][0] for key in implementation_gap_keys}
    covered_family_ids = {
        runtime_families["ability_manager"][0],
        runtime_families["framework_search"][0],
        runtime_families["mod_manager_cancel"][0],
        runtime_families["mutation_menu"][0],
        runtime_families["mutation_variant"][0],
        runtime_families["options_screen"][0],
        runtime_families["build_library_options"][0],
        runtime_families["build_library_add"][0],
        runtime_families["build_library_select"][0],
        runtime_families["build_summary"][0],
        runtime_families["gender_customize"][0],
        runtime_families["embark_builder"][0],
        runtime_families["inventory_status"][0],
        runtime_families["factions_status"][0],
        runtime_families["command_binding"][0],
        runtime_families["equipment_screen"][0],
        runtime_families["object_finder"][0],
        runtime_families["equipment_api"][0],
    }
    runtime_family_ids = (
        {family_id for family_id, _, _, _ in runtime_families.values()}
        - implementation_gap_family_ids
        - covered_family_ids
    )
    assert sum(entries[family_id]["text_construction_count"] for family_id in implementation_gap_family_ids) == 0
    assert sum(entries[family_id]["text_construction_count"] for family_id in runtime_family_ids) == 0
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        if (
            "QudMutationsModuleWindow" not in family_id
                and "OptionsScreen" not in family_id
                and "QudBuildLibraryModuleWindow" not in family_id
                and "QudBuildSummaryModuleWindow" not in family_id
                and "EmbarkBuilder" not in family_id
                and "EquipmentScreen.cs::EquipmentScreen.ShowBodypartEquipUI" not in family_id
                and "ObjectFinder.cs::ObjectFinder.ConfigFilters" not in family_id
                and "EquipmentAPI.cs::EquipmentAPI.ShowInventoryActionMenu" not in family_id
            ):
            _assert_evidence_contains(
                entries,
                family_id,
                "ui-popup.ja.json",
            )
        if "FrameworkSearchInput" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupAskStringTranslationPatch.cs",
                "PopupAskStringTranslationPatchTests.cs",
            )
        if "ModManagerUI" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupMessageTranslationPatch.cs",
                "PopupMessageTranslationPatchTests.cs",
            )
        if "AbilityManagerScreen" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "AbilityManagerPopupTranslationPatch.cs",
                "AbilityManagerScreenTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if "EquipmentAPI.cs::EquipmentAPI.ShowInventoryActionMenu" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "InventoryActionMenu:",
                "PopupPickOptionTranslationPatchTests.cs",
                "UiDictionaryOwnershipTests.cs",
                "ui-inventory-actions.ja.json",
            )
        if "QudMutationsModuleWindow.cs::QudMutationsModuleWindow.SelectVariant" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "QudMutationsModuleWindowVariantPopupTranslationPatch.cs",
                "QudMutationsModuleWindowTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
                "ui-chargen-supplement.ja.json",
            )
        if "QudMutationsModuleWindow.cs::QudMutationsModuleWindow.HandleMenuOption" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.cs",
                "QudMutationsModuleWindowTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if "OptionsScreen" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "OptionsLocalizationPatch.cs",
                "OptionsLocalizationPatchTests.cs",
            )
        if "QudBuildLibraryModuleWindow" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupMessageTranslationPatch.cs",
                "PopupAskStringTranslationPatch.cs",
                "PopupTranslationPatch.cs",
                "ui-chargen.ja.json",
            )
        if "QudBuildSummaryModuleWindow" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupMessageTranslationPatch.cs",
                "PopupMessageTranslationPatchTests.cs",
                "ui-chargen.ja.json",
            )
        if "Gender.cs::Gender.CustomizeProcess" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "BasePronounProviderCustomizePopupTranslationPatch.cs",
                "PopupAskStringTranslationPatch.cs",
                "PopupAskStringTranslationPatchTests.cs",
                "ui-popup.ja.json",
            )
        if "EmbarkBuilder.cs::EmbarkBuilder.checkStateAsync" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "EmbarkBuilderValidationPopupTranslationPatch.cs",
                "EmbarkBuilderValidationPopupTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if (
            "FactionsStatusScreen" in family_id
            or "InventoryAndEquipmentStatusScreen" in family_id
            or "CommandBindingManager" in family_id
        ):
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupPickOptionTranslationPatch.cs",
                "PopupPickOptionTranslationPatchTests.cs",
                "ui-popup.ja.json",
            )
        if "EquipmentScreen.cs::EquipmentScreen.ShowBodypartEquipUI" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "EquipmentScreenBodypartEquipPopupTranslationPatch.cs",
                "EquipmentScreenBodypartEquipPopupTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
        if "ObjectFinder.cs::ObjectFinder.ConfigFilters" in family_id:
            _assert_evidence_contains(
                entries,
                family_id,
                "ObjectFinderConfigFiltersTranslationPatch.cs",
                "ObjectFinderConfigFiltersTranslationPatchTests.cs",
                "TargetMethodResolutionTests.cs",
            )
    for family_id in runtime_family_ids:
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "UI screen, picker, and config pure-Popup" in evidence
        assert "static inventory cannot split" in evidence
    for family_id in implementation_gap_family_ids:
        assert entries[family_id]["closure_status"] == "action_required"

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-ui-popup-runtime-test.json"))
    _assert_producer_runtime_residuals(residual, runtime_family_ids)
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")


def test_policy_records_issue719_residual_pure_popup_remainder_runtime_tranche() -> None:  # noqa: C901, PLR0912, PLR0915
    """Remaining pure-Popup producer families are runtime-required until owner tests split them."""
    runtime_families = {
        "tinkering_mark": (
            "XRL.World.Tinkering/TinkeringHelpers.cs::"
            "TinkeringHelpers.CheckMakersMark(GameObject,GameObject,IModification,string)",
            "XRL.World.Tinkering/TinkeringHelpers.cs",
            "CheckMakersMark",
            {"Popup": 15},
        ),
        "restore_mods": (
            "XRL.Core/XRLCore.cs::XRLCore.RestoreModsLoadedAsync(List<string>)",
            "XRL.Core/XRLCore.cs",
            "RestoreModsLoadedAsync",
            {"Popup": 15},
        ),
        "wish_blueprint": (
            "XRL/PopulationManager.cs::PopulationManager.WishFindBlueprint(string)",
            "XRL/PopulationManager.cs",
            "WishFindBlueprint",
            {"Popup": 13},
        ),
        "shrine": (
            "XRL.World.Parts/Shrine.cs::Shrine.DesecrateShrine(GameObject,bool)",
            "XRL.World.Parts/Shrine.cs",
            "DesecrateShrine",
            {"Popup": 13},
        ),
        "mod_info": ("XRL/ModInfo.cs::ModInfo.ConfirmFailure()", "XRL/ModInfo.cs", "ConfirmFailure", {"Popup": 12}),
        "coda": ("XRL/CodaSystem.cs::CodaSystem.EndGamePrompt()", "XRL/CodaSystem.cs", "EndGamePrompt", {"Popup": 10}),
        "physics": (
            "XRL.World.Parts/Physics.cs::"
            "Physics.ProcessTargetedMove(Cell,string,string,string,int?,bool,bool,bool,bool,bool,bool,"
            "string,string,GameObject)",
            "XRL.World.Parts/Physics.cs",
            "ProcessTargetedMove",
            {"Popup": 9},
        ),
        "disguise": (
            "XRL.World.Parts/ModDisguise.cs::ModDisguise.BeingAppliedBy(GameObject,GameObject)",
            "XRL.World.Parts/ModDisguise.cs",
            "BeingAppliedBy",
            {"Popup": 9},
        ),
        "cybernetics_terminal": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AskLowLevelHack(GameObject)",
            "XRL.World.Parts/CyberneticsTerminal2.cs",
            "AskLowLevelHack",
            {"Popup": 9},
        ),
        "resheph_secret": (
            "XRL.World.Conversations.Parts/GiveReshephSecret.cs::GiveReshephSecret.HandleEvent(EnterElementEvent)",
            "XRL.World.Conversations.Parts/GiveReshephSecret.cs",
            "HandleEvent",
            {"Popup": 9},
        ),
        "save_error": (
            "Qud.API/SavesAPI.cs::SavesAPI.FatalSaveError(Exception,string)",
            "Qud.API/SavesAPI.cs",
            "FatalSaveError",
            {"Popup": 9},
        ),
        "starship": (
            "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.AttemptLaunch(GameObject)",
            "XRL.World.Parts/ShevaStarshipControl.cs",
            "AttemptLaunch",
            {"Popup": 7},
        ),
        "journal_wish": (
            "Qud.API/JournalAPI.cs::JournalAPI.WishGospelAccomplishments()",
            "Qud.API/JournalAPI.cs",
            "WishGospelAccomplishments",
            {"Popup": 7},
        ),
        "reward_population": (
            "XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs::"
            "DynamicQuestRewardElement_ChoiceFromPopulation.award()",
            "XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs",
            "award",
            {"Popup": 6},
        ),
        "grip": (
            "XRL.World.Parts/GripChange.cs::GripChange.TryChooseGrip(GameObject)",
            "XRL.World.Parts/GripChange.cs",
            "TryChooseGrip",
            {"Popup": 6},
        ),
        "ark": (
            "XRL.World.Parts/ArkCore.cs::ArkCore.TryOpen(GameObject)",
            "XRL.World.Parts/ArkCore.cs",
            "TryOpen",
            {"Popup": 6},
        ),
        "vehicle": (
            "XRL.World.Parts/Vehicle.cs::Vehicle.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Vehicle.cs",
            "HandleEvent",
            {"Popup": 5},
        ),
        "base_mutation": (
            "XRL.World.Parts.Mutation/BaseMutation.cs::BaseMutation.SelectVariant(GameObject,bool)",
            "XRL.World.Parts.Mutation/BaseMutation.cs",
            "SelectVariant",
            {"Popup": 5},
        ),
        "give_artifact": (
            "XRL.World.Conversations.Parts/GiveArtifact.cs::GiveArtifact.HandleEvent(EnterElementEvent)",
            "XRL.World.Conversations.Parts/GiveArtifact.cs",
            "HandleEvent",
            {"Popup": 5},
        ),
        "conversation_endgame": (
            "XRL.World.Conversations.Parts/EndGame.cs::EndGame.HandleEvent(EnterElementEvent)",
            "XRL.World.Conversations.Parts/EndGame.cs",
            "HandleEvent",
            {"Popup": 5},
        ),
        "item_naming_wish": (
            "XRL.World.Capabilities/ItemNaming.cs::ItemNaming.HandleItemNamingWish(Match)",
            "XRL.World.Capabilities/ItemNaming.cs",
            "HandleItemNamingWish",
            {"Popup": 5},
        ),
        "cathedra": (
            "XRL.World.Parts/CyberneticsCathedra.cs::CyberneticsCathedra.HandleEvent(CommandEvent)",
            "XRL.World.Parts/CyberneticsCathedra.cs",
            "HandleEvent",
            {"Popup": 4},
        ),
        "water_ritual_secret": (
            "XRL.World.Conversations.Parts/WaterRitualSellSecret.cs::WaterRitualSellSecret.Share()",
            "XRL.World.Conversations.Parts/WaterRitualSellSecret.cs",
            "Share",
            {"Popup": 4},
        ),
        "landmark_wish": (
            "XRL.World.Parts/IZoneLandmark.cs::IZoneLandmark.WishCurrent()",
            "XRL.World.Parts/IZoneLandmark.cs",
            "WishCurrent",
            {"Popup": 3},
        ),
        "grenade": (
            "XRL.World.Parts/IGrenade.cs::IGrenade.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/IGrenade.cs",
            "HandleEvent",
            {"Popup": 3},
        ),
        "onboard_recoiler": (
            "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs::"
            "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport(GameObject,IEvent)",
            "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs",
            "ActuateTeleport",
            {"Popup": 3},
        ),
        "conversation_choose": (
            "Qud.API/ConversationsAPI.cs::ConversationsAPI.chooseOneItem(List<GameObject>,string,bool)",
            "Qud.API/ConversationsAPI.cs",
            "chooseOneItem",
            {"Popup": 3},
        ),
        "reclamation": (
            "XRL.World.Quests/ReclamationSystem.cs::ReclamationSystem.HandleEvent(EnteringZoneEvent)",
            "XRL.World.Quests/ReclamationSystem.cs",
            "HandleEvent",
            {"Popup": 2},
        ),
        "recoil": (
            "XRL.World.Parts/RecoilAbility.cs::RecoilAbility.HandleEvent(CommandEvent)",
            "XRL.World.Parts/RecoilAbility.cs",
            "HandleEvent",
            {"Popup": 2},
        ),
        "wings": (
            "XRL.World.Parts.Mutation/Wings.cs::Wings.HandleEvent(CommandEvent)",
            "XRL.World.Parts.Mutation/Wings.cs",
            "HandleEvent",
            {"Popup": 2},
        ),
        "roll_one": (
            "XRL/PopulationManager.cs::PopulationManager.RollOneFrom(string,Dictionary<string,string>,string)",
            "XRL/PopulationManager.cs",
            "RollOneFrom",
            {"Popup": 1},
        ),
        "extradimensional": (
            "XRL.World.Parts/ModExtradimensional.cs::ModExtradimensional.MakeExtradimensional()",
            "XRL.World.Parts/ModExtradimensional.cs",
            "MakeExtradimensional",
            {"Popup": 1},
        ),
        "extension": (
            "Extensions.cs::Extensions.ShowSuccess(this XRL.World.GameObject,string,bool)",
            "Extensions.cs",
            "ShowSuccess",
            {"Popup": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in runtime_families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    implementation_gap_family_ids: set[str] = set()
    covered_family_ids = {
        runtime_families["restore_mods"][0],
        runtime_families["journal_wish"][0],
        runtime_families["physics"][0],
        runtime_families["reclamation"][0],
        runtime_families["extension"][0],
        runtime_families["grip"][0],
        runtime_families["recoil"][0],
        runtime_families["grenade"][0],
        runtime_families["cathedra"][0],
        runtime_families["wings"][0],
        runtime_families["extradimensional"][0],
        runtime_families["starship"][0],
        runtime_families["ark"][0],
        runtime_families["shrine"][0],
        runtime_families["onboard_recoiler"][0],
        runtime_families["cybernetics_terminal"][0],
        runtime_families["roll_one"][0],
        runtime_families["conversation_choose"][0],
        runtime_families["coda"][0],
        runtime_families["conversation_endgame"][0],
        runtime_families["give_artifact"][0],
        runtime_families["resheph_secret"][0],
        runtime_families["water_ritual_secret"][0],
        runtime_families["base_mutation"][0],
        runtime_families["reward_population"][0],
        runtime_families["vehicle"][0],
        runtime_families["tinkering_mark"][0],
        runtime_families["landmark_wish"][0],
        runtime_families["save_error"][0],
        runtime_families["item_naming_wish"][0],
        runtime_families["disguise"][0],
        runtime_families["wish_blueprint"][0],
        runtime_families["mod_info"][0],
    }
    assert sum(entry["text_construction_count"] for entry in entries.values()) == 209
    for family_id, _, _, _ in runtime_families.values():
        if family_id in covered_family_ids:
            expected_status = "covered_by_owner_route"
        elif family_id in implementation_gap_family_ids:
            expected_status = "action_required"
        else:
            expected_status = "runtime_required"
        assert entries[family_id]["closure_status"] == expected_status
        evidence = " ".join(entries[family_id]["closure_evidence"])
        if family_id in implementation_gap_family_ids:
            if family_id in {
                runtime_families["landmark_wish"][0],
                runtime_families["extradimensional"][0],
            }:
                assert "WishCommand/debug rows" in evidence
                assert "statically identified" in evidence
            else:
                assert "fixed player-visible text and route-local generated captures" in evidence
        elif family_id in covered_family_ids:
            if family_id == runtime_families["physics"][0]:
                assert "PhysicsProcessTargetedMoveOwner" in evidence
                assert "NoTeleport" in evidence
            elif family_id == runtime_families["journal_wish"][0]:
                assert "WishCommand debug popup" in evidence
                assert "JournalAccomplishment" in evidence
            elif family_id == runtime_families["extension"][0]:
                assert "ShowSuccess forwards caller-owned Message to Popup.Show" in evidence
            elif family_id == runtime_families["grenade"][0]:
                assert "IGrenade closure" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatch" in evidence
            elif family_id == runtime_families["cathedra"][0]:
                assert "CyberneticsCathedra.HandleEvent(CommandEvent)" in evidence
                assert "MechanicalWingsPopupTranslationPatch" in evidence
            elif family_id == runtime_families["wings"][0]:
                assert "Wings.HandleEvent(CommandEvent)" in evidence
                assert "MechanicalWingsPopupTranslationPatch" in evidence
            elif family_id in {
                runtime_families["grip"][0],
                runtime_families["recoil"][0],
                runtime_families["extradimensional"][0],
            }:
                assert "world-part popup closure" in evidence
                assert "PopupPickOptionTranslationPatch" in evidence
            elif family_id in {
                runtime_families["starship"][0],
                runtime_families["ark"][0],
            }:
                assert "ship/ark popup review" in evidence
            elif family_id == runtime_families["shrine"][0]:
                assert "Shrine.DesecrateShrine" in evidence
                assert "PopupTranslationPatchTests.cs" in evidence
                assert "PopupPickOptionTranslationPatchTests.cs" in evidence
            elif family_id == runtime_families["cybernetics_terminal"][0]:
                assert "CyberneticsTerminal2.AskLowLevelHack" in evidence
                assert "CyberneticsLowLevelHackPopupTranslationPatch" in evidence
            elif family_id == runtime_families["onboard_recoiler"][0]:
                assert "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport" in evidence
                assert "CyberneticsOnboardRecoilerPopupTranslationPatch" in evidence
            elif family_id == runtime_families["roll_one"][0]:
                assert "PopulationManager.RollOneFrom" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatch" in evidence
            elif family_id == runtime_families["conversation_choose"][0]:
                assert "ConversationsAPI.chooseOneItem" in evidence
                assert "PopupPickOptionTranslationPatch" in evidence
                assert "ui-popup.ja.json" in evidence
            elif family_id == runtime_families["coda"][0]:
                assert "CodaSystem.EndGamePrompt" in evidence
                assert "PopupAskStringTranslationPatch" in evidence
                assert "DeathReasonTranslationPatch" in evidence
            elif family_id == runtime_families["conversation_endgame"][0]:
                assert "EndGame.HandleEvent" in evidence
                assert "PopupAskStringTranslationPatch" in evidence
            elif family_id == runtime_families["give_artifact"][0]:
                assert "GiveArtifact.HandleEvent" in evidence
                assert "PopupPickOptionTranslationPatch" in evidence
            elif family_id == runtime_families["resheph_secret"][0]:
                assert "GiveReshephSecret.HandleEvent" in evidence
                assert "ConversationRewardPopupTranslationPatch" in evidence
            elif family_id == runtime_families["water_ritual_secret"][0]:
                assert "WaterRitualSellSecret.Share" in evidence
                assert "WaterRitualPopupTranslationPatch" in evidence
            elif family_id == runtime_families["base_mutation"][0]:
                assert "BaseMutation.SelectVariant" in evidence
                assert "BaseMutationSelectVariantPopupTranslationPatch" in evidence
            elif family_id == runtime_families["reward_population"][0]:
                assert "DynamicQuestRewardElement_ChoiceFromPopulation.award" in evidence
                assert "PopupPickOptionTranslationPatch" in evidence
                assert "ui-popup.ja.json" in evidence
            elif family_id == runtime_families["vehicle"][0]:
                assert "Vehicle.HandleEvent(InventoryActionEvent)" in evidence
                assert "VehicleFollowerPopupTranslationPatch" in evidence
            elif family_id == runtime_families["tinkering_mark"][0]:
                assert "TinkeringHelpers.CheckMakersMark" in evidence
                assert "TinkeringHelpersMakersMarkTranslationPatch" in evidence
            elif family_id == runtime_families["landmark_wish"][0]:
                assert "IZoneLandmark.WishCurrent" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatch" in evidence
            elif family_id == runtime_families["save_error"][0]:
                assert "SavesAPI.FatalSaveError" in evidence
                assert "SavesApiFatalSaveErrorTranslationPatch" in evidence
            elif family_id == runtime_families["item_naming_wish"][0]:
                assert "ItemNaming.HandleItemNamingWish" in evidence
                assert "ItemNamingTranslationPatch" in evidence
            elif family_id == runtime_families["disguise"][0]:
                assert "ModDisguise.BeingAppliedBy" in evidence
                assert "ModDisguiseBeingAppliedPopupTranslationPatch" in evidence
            elif family_id == runtime_families["wish_blueprint"][0]:
                assert "PopulationManager.WishFindBlueprint" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatch" in evidence
            elif family_id == runtime_families["mod_info"][0]:
                assert "ModInfo.ConfirmFailure" in evidence
                assert "ModInfoTranslationPatch" in evidence
            elif family_id == runtime_families["restore_mods"][0]:
                assert "XRLCore.RestoreModsLoadedAsync" in evidence
                assert "XrlCoreRestoreModsLoadedTranslationPatch" in evidence
            else:
                assert "MessageLeaving" in evidence
                assert "Quests.jp.xml" in evidence
        else:
            assert "remaining pure-Popup producer families" in evidence
            assert "focused owner-route tests are required" in evidence

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-pure-popup-remainder-test.json"))
    _assert_producer_runtime_residuals(
        residual,
        {family_id for family_id, _, _, _ in runtime_families.values()}
        - implementation_gap_family_ids
        - covered_family_ids,
    )
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")


def test_policy_records_issue719_residual_frame_does_runtime_tranche() -> None:
    """Remaining pure MessageFrame and pure Does producers require runtime route evidence."""
    runtime_families = {
        "rough_convert": (
            "XRL/GameText.cs::GameText.RoughConvertSecondPersonToThirdPerson(string,GameObject)",
            "XRL/GameText.cs",
            "RoughConvertSecondPersonToThirdPerson",
            {"Does": 24},
        ),
        "domination": (
            "XRL.World.Parts.Mutation/Domination.cs::Domination.ProcessTarget(GameObject,ref string)",
            "XRL.World.Parts.Mutation/Domination.cs",
            "ProcessTarget",
            {"Does": 21},
        ),
        "temperature": (
            "XRL.World.Parts/Physics.cs::Physics.UpdateTemperature()",
            "XRL.World.Parts/Physics.cs",
            "UpdateTemperature",
            {"Does": 14},
        ),
        "cybernetics_menu": (
            "XRL.UI/CyberneticsScreenMainMenu.cs::CyberneticsScreenMainMenu.CyberneticsScreenMainMenu()",
            "XRL.UI/CyberneticsScreenMainMenu.cs",
            "CyberneticsScreenMainMenu",
            {"Does": 9},
        ),
        "earthquake": (
            "XRL.World.Parts/TrembleEarthquakes.cs::TrembleEarthquakes.RocksFall(Zone)",
            "XRL.World.Parts/TrembleEarthquakes.cs",
            "RocksFall",
            {"Does": 5},
        ),
        "loot_on_step": (
            "XRL.World.Parts/LootOnStep.cs::LootOnStep.SteppedOn(GameObject,bool)",
            "XRL.World.Parts/LootOnStep.cs",
            "SteppedOn",
            {"Does": 4},
        ),
        "neutron_warning": (
            "XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.GetWarningMessage()",
            "XRL.World.Parts/NeutronFluxContainment.cs",
            "GetWarningMessage",
            {"Does": 3},
        ),
        "cybernetics_interface": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AttemptInterface(GameObject,IEvent)",
            "XRL.World.Parts/CyberneticsTerminal2.cs",
            "AttemptInterface",
            {"Does": 2},
        ),
        "pet_frondzie": (
            "XRL.World.Parts/PetFrondzie.cs::PetFrondzie.taunt(GameObject)",
            "XRL.World.Parts/PetFrondzie.cs",
            "taunt",
            {"MessageFrame": 8},
        ),
        "vortex": (
            "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.SpaceTimeAnomalyPeriodicEvents()",
            "XRL.World.Parts/SpaceTimeVortex.cs",
            "SpaceTimeAnomalyPeriodicEvents",
            {"MessageFrame": 8},
        ),
        "liquid_cleaning": (
            "XRL.World.Parts/LiquidVolume.cs::"
            "LiquidVolume.CleaningMessage(GameObject,List<GameObject>,List<string>,GameObject,LiquidVolume,bool)",
            "XRL.World.Parts/LiquidVolume.cs",
            "CleaningMessage",
            {"MessageFrame": 7},
        ),
        "hunter_summoner": (
            "XRL.World.Parts/ExtradimensionalHunterSummoner.cs::ExtradimensionalHunterSummoner.Summon(int)",
            "XRL.World.Parts/ExtradimensionalHunterSummoner.cs",
            "Summon",
            {"MessageFrame": 6},
        ),
        "swoop": (
            "XRL.World.Parts/Combat.cs::Combat.SwoopAttack(GameObject,string)",
            "XRL.World.Parts/Combat.cs",
            "SwoopAttack",
            {"MessageFrame": 5},
        ),
        "liquid_contact": (
            "XRL.World.Parts/LiquidVolume.cs::LiquidVolume.ProcessContact(GameObject,bool,bool,bool,GameObject,bool,int)",
            "XRL.World.Parts/LiquidVolume.cs",
            "ProcessContact",
            {"MessageFrame": 5},
        ),
        "shrine": (
            "XRL.World.Parts/Shrine.cs::Shrine.PerformDesecration(GameObject,bool,bool,bool,bool)",
            "XRL.World.Parts/Shrine.cs",
            "PerformDesecration",
            {"MessageFrame": 5},
        ),
        "psychic": (
            "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.PsychicPresenceMessage(int,bool)",
            "XRL/PsychicHunterSystem.cs",
            "PsychicPresenceMessage",
            {"MessageFrame": 5},
        ),
        "pet_ebenshabat": (
            "XRL.World.Parts/PetEbenshabat.cs::PetEbenshabat.HandleEvent(AfterLevelGainedEvent)",
            "XRL.World.Parts/PetEbenshabat.cs",
            "HandleEvent",
            {"MessageFrame": 4},
        ),
        "skill_add": (
            "XRL.World.Parts/Skills.cs::Skills.WishSkillAdd(string)",
            "XRL.World.Parts/Skills.cs",
            "WishSkillAdd",
            {"MessageFrame": 4},
        ),
        "skill_all": (
            "XRL.World.Parts/Skills.cs::Skills.WishSkillAll()",
            "XRL.World.Parts/Skills.cs",
            "WishSkillAll",
            {"MessageFrame": 4},
        ),
        "award_xp": (
            "XRL.World.Conversations/ConversationDelegates.cs::ConversationDelegates.AwardXP(DelegateContext)",
            "XRL.World.Conversations/ConversationDelegates.cs",
            "AwardXP",
            {"MessageFrame": 3},
        ),
        "webs": (
            "XRL.World.Parts.Mutation/SpiderWebs.cs::SpiderWebs.HandleEvent(LeftCellEvent)",
            "XRL.World.Parts.Mutation/SpiderWebs.cs",
            "HandleEvent",
            {"MessageFrame": 3},
        ),
        "shuttle": (
            "XRL.World.Parts/AIBarathrumShuttle.cs::AIBarathrumShuttle.ActionShipLaunch(GoalHandler)",
            "XRL.World.Parts/AIBarathrumShuttle.cs",
            "ActionShipLaunch",
            {"MessageFrame": 3},
        ),
        "baetyl_hostility": (
            "XRL.World.Parts/BaetylHostility.cs::BaetylHostility.CheckBaetylHostility()",
            "XRL.World.Parts/BaetylHostility.cs",
            "CheckBaetylHostility",
            {"MessageFrame": 3},
        ),
        "force_lathe": (
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
            "CyberneticsPrecisionForceLathe.HandleEvent(ReplaceThrownWeaponEvent)",
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs",
            "HandleEvent",
            {"MessageFrame": 3},
        ),
        "heat_self": (
            "XRL.World.Parts/HeatSelfOnFreeze.cs::HeatSelfOnFreeze.FireEvent(Event)",
            "XRL.World.Parts/HeatSelfOnFreeze.cs",
            "FireEvent",
            {"MessageFrame": 3},
        ),
        "mutation_wish": (
            "XRL.World.Parts/Mutations.cs::Mutations.WishMutationAdd(string,string)",
            "XRL.World.Parts/Mutations.cs",
            "WishMutationAdd",
            {"MessageFrame": 3},
        ),
        "nephal": (
            "XRL.World.Parts/NephalProperties.cs::NephalProperties.AbsorbChords(GameObject)",
            "XRL.World.Parts/NephalProperties.cs",
            "AbsorbChords",
            {"MessageFrame": 3},
        ),
        "baetyl_reward": (
            "XRL.World.Units/GameObjectBaetylUnit.cs::GameObjectBaetylUnit.GiveRewards(GameObject,int,int)",
            "XRL.World.Units/GameObjectBaetylUnit.cs",
            "GiveRewards",
            {"MessageFrame": 2},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in runtime_families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert sum(entry["text_construction_count"] for entry in entries.values()) == 169
    covered_keys = {
        "earthquake",
        "domination",
        "temperature",
        "cybernetics_menu",
        "loot_on_step",
        "neutron_warning",
        "cybernetics_interface",
        "hunter_summoner",
        "swoop",
        "shrine",
        "psychic",
        "skill_add",
        "skill_all",
        "award_xp",
        "webs",
        "baetyl_hostility",
        "force_lathe",
        "heat_self",
        "mutation_wish",
        "nephal",
        "pet_ebenshabat",
        "pet_frondzie",
        "shuttle",
        "vortex",
        "baetyl_reward",
        "liquid_cleaning",
        "liquid_contact",
        "rough_convert",
    }
    covered_family_ids = {runtime_families[key][0] for key in covered_keys}
    implementation_gap_keys: set[str] = set()
    implementation_gap_family_ids = {runtime_families[key][0] for key in implementation_gap_keys}
    runtime_family_ids = (
        {family_id for family_id, _, _, _ in runtime_families.values()}
        - covered_family_ids
        - implementation_gap_family_ids
    )

    assert sum(entries[family_id]["text_construction_count"] for family_id in covered_family_ids) == 169
    assert sum(entries[family_id]["text_construction_count"] for family_id in implementation_gap_family_ids) == 0
    assert sum(entries[family_id]["text_construction_count"] for family_id in runtime_family_ids) == 0

    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        if family_id == runtime_families["earthquake"][0]:
            assert "TrembleEarthquakes.RocksFall" in evidence
            assert "falling rocks" in evidence
            assert "CombatAndLogMessageQueuePatchTests.cs" in evidence
        elif family_id == runtime_families["domination"][0]:
            assert "Domination.ProcessTarget" in evidence
            assert "DominationProcessTargetTranslationPatchTests.cs" in evidence
            assert "MessageQueueSemanticPipeline.cs" in evidence
        elif family_id == runtime_families["cybernetics_interface"][0]:
            assert "CyberneticsTerminalInterfacePopupTranslationPatch.cs" in evidence
            assert "CyberneticsTerminalInterfacePopupTranslationPatchTests.cs" in evidence
            assert "TargetMethodResolutionTests.cs" in evidence
        elif family_id == runtime_families["rough_convert"][0]:
            assert "GameText.RoughConvertSecondPersonToThirdPerson closure" in evidence
            assert "GameTextDeathReasonTranslationPatch" in evidence
        else:
            assert "fixed pure MessageFrame and pure Does" in evidence
            assert "MessageFrameTranslatorTests.cs" in evidence

    for family_id in runtime_family_ids:
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "pure MessageFrame and pure Does" in evidence
        assert "runtime-required" in evidence

    for family_id in implementation_gap_family_ids:
        assert entries[family_id]["closure_status"] == "action_required"

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-frame-does-test.json"))
    covered_family_ids = {runtime_families["shrine"][0]}
    for family_id in covered_family_ids:
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        _assert_evidence_contains(
            entries,
            family_id,
            "PopupTranslationPatchTests.cs",
            "PopupPickOptionTranslationPatchTests.cs",
            "ui-popup.ja.json",
        )
    _assert_producer_runtime_residuals(residual, runtime_family_ids - covered_family_ids)
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")


def test_policy_records_issue719_residual_mixed_popup_runtime_tranche() -> None:  # noqa: C901, PLR0912
    """Mixed queue/popup and Does/popup producers require runtime route evidence."""
    runtime_families = {
        "sunder_mind": (
            "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Tick()",
            "XRL.World.Parts.Mutation/SunderMind.cs",
            "Tick",
            {"AddPlayerMessage": 1, "Popup": 24},
        ),
        "stomach": (
            "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
            "XRL.World.Parts/Stomach.cs",
            "FireEvent",
            {"AddPlayerMessage": 4, "Popup": 20},
        ),
        "elevator": (
            "XRL.World.Parts/ElevatorSwitch.cs::ElevatorSwitch.FireEvent(Event)",
            "XRL.World.Parts/ElevatorSwitch.cs",
            "FireEvent",
            {"AddPlayerMessage": 3, "Popup": 6},
        ),
        "biome": (
            "XRL.World.Biomes/BiomeManager.cs::BiomeManager.DisplaySurfaceDistribution(string)",
            "XRL.World.Biomes/BiomeManager.cs",
            "DisplaySurfaceDistribution",
            {"AddPlayerMessage": 6, "Popup": 2},
        ),
        "clam": (
            "XRL.World.Parts/GiantClamProperties.cs::GiantClamProperties.TeleportFromClamWorld(GameObject)",
            "XRL.World.Parts/GiantClamProperties.cs",
            "TeleportFromClamWorld",
            {"AddPlayerMessage": 1, "Popup": 2},
        ),
        "examiner": (
            "XRL.World.Parts/Examiner.cs::Examiner.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/Examiner.cs",
            "HandleEvent",
            {"Does": 10, "Popup": 5},
        ),
        "tinker": (
            "XRL.World.Parts/TinkerItem.cs::TinkerItem.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/TinkerItem.cs",
            "HandleEvent",
            {"Does": 6, "Popup": 9},
        ),
        "fixit": (
            "XRL.World.Parts/FixitSpray.cs::FixitSpray.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/FixitSpray.cs",
            "HandleEvent",
            {"Does": 2, "Popup": 9},
        ),
        "magnetized": (
            "XRL.World.Parts/MagnetizedApplicator.cs::MagnetizedApplicator.HandleEvent(InventoryActionEvent)",
            "XRL.World.Parts/MagnetizedApplicator.cs",
            "HandleEvent",
            {"Does": 7, "Popup": 4},
        ),
        "vehicle_infiltration": (
            "XRL.World.Parts/VehicleMeleeInfiltration.cs::"
            "VehicleMeleeInfiltration.TryInfiltrate(GameObject,Interior)",
            "XRL.World.Parts/VehicleMeleeInfiltration.cs",
            "TryInfiltrate",
            {"Does": 6, "Popup": 2},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in runtime_families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert sum(entry["text_construction_count"] for entry in entries.values()) == 129
    implementation_gap_family_ids: set[str] = set()
    covered_family_ids = {
        runtime_families["sunder_mind"][0],
        runtime_families["stomach"][0],
        runtime_families["elevator"][0],
        runtime_families["biome"][0],
        runtime_families["clam"][0],
        runtime_families["vehicle_infiltration"][0],
        runtime_families["examiner"][0],
        runtime_families["tinker"][0],
        runtime_families["fixit"][0],
        runtime_families["magnetized"][0],
    }
    for family_id, _, _, _ in runtime_families.values():
        if family_id in covered_family_ids:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            evidence = " ".join(entries[family_id]["closure_evidence"])
            if family_id == runtime_families["sunder_mind"][0]:
                assert "SunderMindTranslationPatch.cs" in evidence
                assert "CombatAndLogMessageQueuePatchTests.cs" in evidence
            elif family_id == runtime_families["elevator"][0]:
                assert "SingleCallsiteOwnerQueueTranslationPatch.cs" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatch.cs" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in evidence
            elif family_id == runtime_families["biome"][0]:
                assert "SingleCallsiteOwnerPopupTranslationPatch.cs" in evidence
                assert "SingleCallsiteOwnerQueueTranslationPatch.cs" in evidence
                assert "SingleCallsiteOwnerQueueTranslationPatchTests.cs" in evidence
            elif family_id == runtime_families["clam"][0]:
                assert "GiantClamTeleportTranslationPatch.cs" in evidence
                assert "GiantClamTeleportTranslationPatchTests.cs" in evidence
                assert "CombatAndLogMessageQueuePatchTests.cs" in evidence
            elif family_id == runtime_families["vehicle_infiltration"][0]:
                assert "PopupTranslationPatchTests.cs" in evidence
                assert "GameObjectEmitMessageTranslationPatch.cs" in evidence
                assert "verbs.ja.json" in evidence
            elif family_id in {
                runtime_families["examiner"][0],
                runtime_families["tinker"][0],
            }:
                assert (
                    "ExaminerTranslationPatch.cs" in evidence
                    or "TinkerItemTranslationPatch.cs" in evidence
                )
                assert (
                    "ExaminerTranslationPatchTests.cs" in evidence
                    or "TinkerItemTranslationPatchTests.cs" in evidence
                )
            elif family_id in {
                runtime_families["fixit"][0],
                runtime_families["magnetized"][0],
            }:
                assert "SingleCallsiteOwnerPopupTranslationPatch.cs" in evidence
                assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in evidence
                assert "DoesVerbFamilyTests.cs" in evidence
            else:
                assert "StomachTranslationPatch.cs" in evidence
                assert "StomachOverdrink" in evidence
            continue
        if family_id in implementation_gap_family_ids:
            assert entries[family_id]["closure_status"] == "action_required"
            continue
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "mixed AddPlayerMessage+Popup and Does+Popup" in evidence
        assert "runtime-required" in evidence

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-mixed-popup-test.json"))
    _assert_producer_runtime_residuals(
        residual,
        {family_id for family_id, _, _, _ in runtime_families.values()}
        - implementation_gap_family_ids
        - covered_family_ids,
    )
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")


def test_policy_records_issue719_residual_queue_does_runtime_tranche() -> None:  # noqa: C901
    """Pure queues and Does+EmitMessage producers require runtime route evidence."""
    runtime_families = {
        "cybernetic_butcher": (
            (
                "XRL.World.Parts/CyberneticsButcherableCybernetic.cs::"
                "CyberneticsButcherableCybernetic.AttemptButcher"
                "(GameObject,bool,bool,bool,int,Cell,List<GameObject>)"
            ),
            "XRL.World.Parts/CyberneticsButcherableCybernetic.cs",
            "AttemptButcher",
            {"Does": 8, "EmitMessage": 16},
        ),
        "chat": (
            "XRL.World.Parts/Chat.cs::Chat.PerformChat(GameObject,bool)",
            "XRL.World.Parts/Chat.cs",
            "PerformChat",
            {"Does": 4, "EmitMessage": 12},
        ),
        "fungal_infection": (
            "XRL.World.Parts/FungalInfection.cs::FungalInfection.FireEvent(Event)",
            "XRL.World.Parts/FungalInfection.cs",
            "FireEvent",
            {"Does": 10, "EmitMessage": 4},
        ),
        "vehicle_infiltration_can_enter": (
            (
                "XRL.World.Parts/VehicleMeleeInfiltration.cs::"
                "VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent)"
            ),
            "XRL.World.Parts/VehicleMeleeInfiltration.cs",
            "HandleEvent",
            {"Does": 1, "EmitMessage": 2},
        ),
        "dance_opponent_turn": (
            (
                "XRL.World.Parts/DanceRitualOpponent.cs::"
                "DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)"
            ),
            "XRL.World.Parts/DanceRitualOpponent.cs",
            "HandleEvent",
            {"AddPlayerMessage": 22},
        ),
        "player_dance_fire_event": (
            "XRL.World.Parts/PlayerDanceRitual.cs::PlayerDanceRitual.FireEvent(Event)",
            "XRL.World.Parts/PlayerDanceRitual.cs",
            "FireEvent",
            {"AddPlayerMessage": 14},
        ),
        "dance_opponent_register": (
            (
                "XRL.World.Parts/DanceRitualOpponent.cs::"
                "DanceRitualOpponent.Register(GameObject,IEventRegistrar)"
            ),
            "XRL.World.Parts/DanceRitualOpponent.cs",
            "Register",
            {"AddPlayerMessage": 6},
        ),
        "sound_play": (
            (
                "SoundManager.cs::"
                "SoundManager._PlaySound(string,float,float,SoundRequest.SoundEffectType)"
            ),
            "SoundManager.cs",
            "_PlaySound",
            {"AddPlayerMessage": 4},
        ),
        "sound_world_play": (
            (
                "SoundManager.cs::"
                "SoundManager._PlayWorldSound(string,float,float,float,float,Point2D)"
            ),
            "SoundManager.cs",
            "_PlayWorldSound",
            {"AddPlayerMessage": 4},
        ),
        "interior_damage": (
            "XRL.World.Parts/Interior.cs::Interior.HandleEvent(TookDamageEvent)",
            "XRL.World.Parts/Interior.cs",
            "HandleEvent",
            {"AddPlayerMessage": 3},
        ),
        "message_queue_char": (
            "XRL.Messages/MessageQueue.cs::MessageQueue.AddPlayerMessage(string,char,bool)",
            "XRL.Messages/MessageQueue.cs",
            "AddPlayerMessage",
            {"AddPlayerMessage": 1},
        ),
        "dynamic_quest_where": (
            (
                "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::"
                "FindASiteDynamicQuestManager.DynamicQuestWhere()"
            ),
            "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs",
            "DynamicQuestWhere",
            {"AddPlayerMessage": 1},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in runtime_families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert sum(entry["text_construction_count"] for entry in entries.values()) == 112
    implementation_gap_family_ids = set()
    covered_family_ids = {
        runtime_families["cybernetic_butcher"][0],
        runtime_families["vehicle_infiltration_can_enter"][0],
        runtime_families["dance_opponent_turn"][0],
        runtime_families["player_dance_fire_event"][0],
        runtime_families["dance_opponent_register"][0],
        runtime_families["sound_play"][0],
        runtime_families["sound_world_play"][0],
        runtime_families["message_queue_char"][0],
        runtime_families["dynamic_quest_where"][0],
        runtime_families["interior_damage"][0],
        runtime_families["fungal_infection"][0],
        runtime_families["chat"][0],
    }
    for family_id, _, _, _ in runtime_families.values():
        if family_id in covered_family_ids:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            if "DanceRitualOpponent" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "DanceRitualOpponentTranslationPatch.cs",
                    "MessageQueueSemanticPipeline.cs",
                )
            if "PlayerDanceRitual" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "PlayerDanceRitualTranslationPatch.cs",
                    "MessageQueueSemanticPipeline.cs",
                )
            if "FindASiteDynamicQuestManager" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "WishCommandQueueTranslationPatch.cs",
                    "WishCommandQueueTranslationPatchTests.cs",
                )
            if "Interior.cs" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "PhysicsProcessTakeDamageTranslationPatch.cs",
                    "CombatAndLogMessageQueuePatchTests.cs",
                )
            if "VehicleMeleeInfiltration" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "GameObjectEmitMessageTranslationPatch.cs",
                    "DoesVerbRouteTranslator.cs",
                    "CombatAndLogMessageQueuePatchTests.cs",
                    "verbs.ja.json",
                )
            if "FungalInfection.cs" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "verbs.ja.json",
                    "messages.ja.json",
                    "DoesVerbFamilyTests.cs",
                    "MessagePatternTranslatorTests.cs",
                    "CombatAndLogMessageQueuePatchTests.cs",
                )
            if "Chat.cs" in family_id:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "verbs.ja.json",
                    "DoesVerbFamilyTests.cs",
                    "ObjectBlueprints/Furniture.jp.xml",
                    "static_producer_closure.py",
                )
            continue
        if family_id in implementation_gap_family_ids:
            assert entries[family_id]["closure_status"] == "action_required"
            continue
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "pure AddPlayerMessage and Does+EmitMessage" in evidence
        assert "runtime-required" in evidence

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-queue-does-test.json"))
    _assert_producer_runtime_residuals(
        residual,
        {
            family_id
            for family_id, _, _, _ in runtime_families.values()
            if family_id not in implementation_gap_family_ids | covered_family_ids
        },
    )
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == dict.fromkeys(implementation_gap_family_ids, "likely_implementation_gap")


def test_policy_records_issue719_residual_message_mixed_remainder_runtime_tranche() -> None:
    """The final heterogeneous message producers require runtime route evidence."""
    runtime_families = {
        "sheva_launch": (
            "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.CheckTimer()",
            "XRL.World.Parts/ShevaStarshipControl.cs",
            "CheckTimer",
            {"EmitMessage": 12, "Popup": 6},
        ),
        "space_time_vortex": (
            "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.ApplyVortex(GameObject)",
            "XRL.World.Parts/SpaceTimeVortex.cs",
            "ApplyVortex",
            {"EmitMessage": 10, "MessageFrame": 6, "Popup": 1},
        ),
        "carapace_loosen": (
            "XRL.World.Parts.Mutation/Carapace.cs::Carapace.Loosen(bool)",
            "XRL.World.Parts.Mutation/Carapace.cs",
            "Loosen",
            {"Does": 6, "EmitMessage": 2, "Popup": 4},
        ),
        "physics_entering_cell": (
            "XRL.World.Parts/Physics.cs::Physics.HandleEvent(ObjectEnteringCellEvent)",
            "XRL.World.Parts/Physics.cs",
            "HandleEvent",
            {"AddPlayerMessage": 6, "Does": 6},
        ),
        "golem_mound": (
            "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.DisplayOptions(GameObject)",
            "XRL.World.Parts/GolemQuestMound.cs",
            "DisplayOptions",
            {"Popup": 10},
        ),
        "thief_bot": (
            "XRL.World.Parts/ThiefBot.cs::ThiefBot.FireEvent(Event)",
            "XRL.World.Parts/ThiefBot.cs",
            "FireEvent",
            {"AddPlayerMessage": 4, "Does": 5},
        ),
        "campfire_extinguish": (
            "XRL.World.Parts/Campfire.cs::Campfire.Extinguish(GameObject,GameObject)",
            "XRL.World.Parts/Campfire.cs",
            "Extinguish",
            {"Does": 4, "EmitMessage": 2, "MessageFrame": 2},
        ),
        "holographic_visage": (
            (
                "XRL.World.Parts/CyberneticsHolographicVisage.cs::"
                "CyberneticsHolographicVisage.SelectVisage(GameObject)"
            ),
            "XRL.World.Parts/CyberneticsHolographicVisage.cs",
            "SelectVisage",
            {"EmitMessage": 2, "Popup": 4},
        ),
        "warm_static_wish_spec": (
            "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffectSpec(string)",
            "XRL.Liquids/LiquidWarmStatic.cs",
            "WishWarmEffectSpec",
            {"EmitMessage": 5},
        ),
        "warm_static_glitch_liquid": (
            (
                "XRL.Liquids/LiquidWarmStatic.cs::"
                "LiquidWarmStatic.GlitchLiquidComponents(GameObject,string,int,bool)"
            ),
            "XRL.Liquids/LiquidWarmStatic.cs",
            "GlitchLiquidComponents",
            {"EmitMessage": 4},
        ),
        "warm_static_wish": (
            "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffect()",
            "XRL.Liquids/LiquidWarmStatic.cs",
            "WishWarmEffect",
            {"EmitMessage": 3},
        ),
        "desalination_pellet": (
            (
                "XRL.World.Parts/DesalinationPellet.cs::"
                "DesalinationPellet.HandleEvent(InventoryActionEvent)"
            ),
            "XRL.World.Parts/DesalinationPellet.cs",
            "HandleEvent",
            {"EmitMessage": 3},
        ),
        "fade_text": (
            "XRL.UI/FadeText.cs::FadeText.Update()",
            "XRL.UI/FadeText.cs",
            "Update",
            {"TutorialManagerPopup": 2},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces in runtime_families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    assert sum(entry["text_construction_count"] for entry in entries.values()) == 109
    implementation_gap_family_ids = set()
    covered_family_ids = {
        runtime_families["sheva_launch"][0],
        runtime_families["physics_entering_cell"][0],
        runtime_families["thief_bot"][0],
        runtime_families["fade_text"][0],
        runtime_families["desalination_pellet"][0],
        runtime_families["holographic_visage"][0],
        runtime_families["warm_static_wish_spec"][0],
        runtime_families["warm_static_wish"][0],
        runtime_families["warm_static_glitch_liquid"][0],
        runtime_families["campfire_extinguish"][0],
        runtime_families["carapace_loosen"][0],
        runtime_families["golem_mound"][0],
        runtime_families["space_time_vortex"][0],
    }
    for family_id, _, _, _ in runtime_families.values():
        if family_id in covered_family_ids:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            if family_id == runtime_families["carapace_loosen"][0]:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "CarapaceTranslationPatch.cs",
                    "CombatAndLogMessageQueuePatchTests.cs",
                    "TargetMethodResolutionTests.cs",
                    "DoesVerbFamilyTests.cs",
                    "verbs.ja.json",
                )
            if family_id == runtime_families["golem_mound"][0]:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "GolemQuestMoundDisplayOptionsTranslationPatch.cs",
                    "GolemQuestMoundDisplayOptionsTranslationPatchTests.cs",
                    "TargetMethodResolutionTests.cs",
                )
            if family_id == runtime_families["space_time_vortex"][0]:
                _assert_evidence_contains(
                    entries,
                    family_id,
                    "verbs.ja.json",
                    "DoesVerbFamilyTests.cs",
                    "SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    "TargetMethodResolutionTests.cs",
                )
            continue
        if family_id in implementation_gap_family_ids:
            assert entries[family_id]["closure_status"] == "action_required"
            continue
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "final heterogeneous producer message families" in evidence
        assert "runtime-required" in evidence

    residual = residual_bucket_payload(
        inventory,
        inventory_path=Path("issue719-message-mixed-remainder-test.json"),
    )
    _assert_producer_runtime_residuals(
        residual,
        {
            family_id
            for family_id, _, _, _ in runtime_families.values()
            if family_id not in implementation_gap_family_ids | covered_family_ids
        },
    )
    assert {
        entry["family_id"]: entry["residual_disposition"]
        for entry in residual["entries"]
        if entry["family_id"] in implementation_gap_family_ids
    } == {}


def test_policy_records_issue719_residual_sifrah_route_split_runtime_tranche() -> None:
    """Remaining Sifrah routes stay in #719 but descriptions split by owner shape."""
    residual_families = {
        "psychic_combat_constructor": (
            "XRL.World/PsychicCombatSifrah.cs::"
            "PsychicCombatSifrah.PsychicCombatSifrah(GameObject,string,int,int,string)",
            "XRL.World/PsychicCombatSifrah.cs",
            "PsychicCombatSifrah",
            {"Popup": 72},
        ),
        "beguiling_constructor": (
            "XRL.World/BeguilingSifrah.cs::"
            "BeguilingSifrah.BeguilingSifrah(GameObject,int,bool,int,int)",
            "XRL.World/BeguilingSifrah.cs",
            "BeguilingSifrah",
            {"Popup": 13},
        ),
        "social_secret_use": (
            "XRL.World/SocialSifrahTokenSecret.cs::"
            "SocialSifrahTokenSecret.UseToken(SifrahGame,SifrahSlot,GameObject)",
            "XRL.World/SocialSifrahTokenSecret.cs",
            "UseToken",
            {"Popup": 6},
        ),
        "social_secret_description": (
            "XRL.World/SocialSifrahTokenSecret.cs::"
            "SocialSifrahTokenSecret.GetDescription(SifrahGame,SifrahSlot,GameObject)",
            "XRL.World/SocialSifrahTokenSecret.cs",
            "GetDescription",
            {"EffectDescriptionReturn": 1},
        ),
        "ritual_effect_constructor": (
            "XRL.World/RitualSifrahTokenEffectBleeding.cs::"
            "RitualSifrahTokenEffectBleeding.RitualSifrahTokenEffectBleeding(int)",
            "XRL.World/RitualSifrahTokenEffectBleeding.cs",
            "RitualSifrahTokenEffectBleeding",
            {"DescriptionAssignment": 1},
        ),
    }
    covered_haggling_family_id = "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultFailure()"
    inventory = _inventory(
        [
            *[
                _family(family_id, source_file, member_name, surfaces)
                for family_id, source_file, member_name, surfaces in residual_families.values()
            ],
            _family(
                covered_haggling_family_id,
                "XRL.World/HagglingSifrah.cs",
                "ResultFailure",
                {"EffectDescriptionReturn": 1},
            ),
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    for key, (family_id, _, _, _) in residual_families.items():
        if key == "social_secret_use":
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            evidence = " ".join(entries[family_id]["closure_evidence"])
            assert "SocialSifrahTokenSecret.UseToken" in evidence
            assert "PopupPickOptionTranslationPatch" in evidence
            assert "ui-popup.ja.json" in evidence
            continue
        if key in {"social_secret_description", "ritual_effect_constructor"}:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            evidence = " ".join(entries[family_id]["closure_evidence"])
            assert "SifrahTokenDescriptionTranslationPatch" in evidence
            assert "SifrahTokenDescriptionTranslatorTests.cs" in evidence
            continue
        if key in {"psychic_combat_constructor", "beguiling_constructor"}:
            assert entries[family_id]["closure_status"] == "covered_by_owner_route"
            evidence = " ".join(entries[family_id]["closure_evidence"])
            assert "not used in the base game" in evidence
            assert "static unused-base-game classification" in evidence
            continue
        assert entries[family_id]["closure_status"] == "runtime_required"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "remaining Sifrah description and popup route-split" in evidence
        assert "runtime-required" in evidence

    assert entries[covered_haggling_family_id]["closure_status"] == "covered_by_owner_route"

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-sifrah-routes-test.json"))
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {}


def test_policy_splits_issue719_sifrah_popup_residuals_by_static_owner_shape() -> None:
    """Sifrah popup residuals are routed by exact owner shape before runtime deferral."""
    families = {
        "check_out_of_options": (
            "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.CheckOutOfOptions(GameObject)",
            "XRL.World/FormalWaterRitualSifrah.cs",
            "CheckOutOfOptions",
            {"Popup": 1},
            "sifrah_popup_check_out_of_options_gap",
            "covered_by_owner_route",
        ),
        "result_owner": (
            "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ResultFailure(GameObject)",
            "XRL.World/ItemNamingSifrah.cs",
            "ResultFailure",
            {"Popup": 1},
            "sifrah_popup_result_owner_gap",
            "covered_by_owner_route",
        ),
        "token_check_use": (
            "XRL.World/RitualSifrahTokenScourging.cs::"
            "RitualSifrahTokenScourging.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)",
            "XRL.World/RitualSifrahTokenScourging.cs",
            "CheckTokenUse",
            {"Popup": 1},
            "sifrah_popup_token_check_use_gap",
            "covered_by_owner_route",
        ),
        "secret_use_token": (
            "XRL.World/SocialSifrahTokenSecret.cs::"
            "SocialSifrahTokenSecret.UseToken(SifrahGame,SifrahSlot,GameObject)",
            "XRL.World/SocialSifrahTokenSecret.cs",
            "UseToken",
            {"Popup": 6},
            "sifrah_popup_secret_use_token_gap",
            "covered_by_owner_route",
        ),
        "hacking_partial_success": (
            "XRL.World.Parts/CyberneticsTerminal2.cs::"
            "CyberneticsTerminal2.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)",
            "XRL.World.Parts/CyberneticsTerminal2.cs",
            "HackingResultPartialSuccess",
            {"Popup": 1},
            "sifrah_popup_hacking_partial_success_gap",
            "covered_by_owner_route",
        ),
        "unused_psychic_combat": (
            "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.CheckOutOfOptions(GameObject)",
            "XRL.World/PsychicCombatSifrah.cs",
            "CheckOutOfOptions",
            {"Popup": 1},
            "sifrah_popup_unused_base_game_covered",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, _, disposition in families.values():
        expected_status = (
            "covered_by_owner_route"
            if disposition == "covered_by_owner_route"
            else "action_required"
            if disposition == "likely_implementation_gap"
            else "runtime_required"
        )
        assert entries[family_id]["closure_status"] == expected_status
        if family_id in {
            families["check_out_of_options"][0],
            families["result_owner"][0],
            families["token_check_use"][0],
        }:
            _assert_evidence_contains(
                entries,
                family_id,
                "PopupShowTranslationPatchTests.cs",
                "ui-popup.ja.json",
                "fixed Sifrah popup",
            )
        if family_id == families["hacking_partial_success"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "HackingSifrahResultTranslationPatch.cs",
                "CombatAndLogMessageQueuePatchTests.cs",
            )
        if family_id == families["secret_use_token"][0]:
            _assert_evidence_contains(
                entries,
                family_id,
                "SocialSifrahTokenSecret.UseToken",
                "PopupPickOptionTranslationPatch",
                "ui-popup.ja.json",
            )

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-sifrah-popup-static-shapes-test.json"))
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_policy_records_issue719_final_child_buckets_as_runtime_tranche() -> None:
    """Final non-Sifrah child buckets stay in #719 as runtime-required evidence work."""
    families = {
        "broad_gameobject_popup": (
            "XRL.World/GameObject.cs::GameObject.AutoEquip(GameObject,bool,bool,bool)",
            "XRL.World/GameObject.cs",
            "AutoEquip",
            {"Popup": 72},
            "producer_broad_gameobject_autoequip_gap",
        ),
        "generated_display_name": (
            "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.CreateVillageFaction(HistoricEntitySnapshot)",
            "XRL.World.ZoneBuilders/VillageBase.cs",
            "CreateVillageFaction",
            {"DisplayNameAssignment": 24},
            "generated_display_name_village_faction_gap",
        ),
        "misc_text_filter": (
            "XRL.Language/TextFilters.cs::TextFilters.Angry(string)",
            "XRL.Language/TextFilters.cs",
            "Angry",
            {"HistoricStringExpander": 3},
            "history_text_filter_speech_status_gap",
        ),
        "ui_popup_sink": (
            (
                "XRL.UI/Popup.cs::"
                "Popup.WaitNewPopupMessage(string,List<QudMenuItem>,Action<QudMenuItem>,"
                "List<QudMenuItem>,string,string,int,string,IRenderable,IRenderable,bool,"
                "bool,Location2D,string,bool)"
            ),
            "XRL.UI/Popup.cs",
            "WaitNewPopupMessage",
            {"SetText": 15},
            "ui_popup_sink_route_split",
        ),
        "active_effect_popup": (
            (
                "XRL.World.Effects/FungalSporeInfection.cs::"
                "FungalSporeInfection.ChooseLimbForInfection"
                "(List<BodyPart>,string,out BodyPart,out string,bool)"
            ),
            "XRL.World.Effects/FungalSporeInfection.cs",
            "ChooseLimbForInfection",
            {"Popup": 4},
            "active_effect_fungal_spore_infection_popup_gap",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}

    covered_family_ids = {
        families["broad_gameobject_popup"][0],
        families["generated_display_name"][0],
        families["misc_text_filter"][0],
        families["active_effect_popup"][0],
    }
    implementation_gap_family_ids: set[str] = set()
    not_owner_family_ids = {
        families["ui_popup_sink"][0],
    }
    for family_id, _, _, _, _ in families.values():
        expected_status = (
            "not_owner_surface"
            if family_id in not_owner_family_ids
            else "covered_by_owner_route"
            if family_id in covered_family_ids
            else "action_required"
            if family_id in implementation_gap_family_ids
            else "runtime_required"
        )
        assert entries[family_id]["closure_status"] == expected_status

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-final-child-test.json"))
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (
            bucket,
            "likely_implementation_gap" if family_id in implementation_gap_family_ids else "runtime_evidence_required",
        )
        for family_id, _, _, _, bucket in families.values()
        if family_id not in not_owner_family_ids | covered_family_ids
    }


def test_residual_bucket_payload_splits_broad_producer_routes_by_exact_member_shape() -> None:
    """Broad producer rows split into exact GameObject and missile route buckets."""
    families = {
        "autoequip": (
            "XRL.World/GameObject.cs::GameObject.AutoEquip(GameObject,bool,bool,bool)",
            "XRL.World/GameObject.cs",
            "AutoEquip",
            {"Popup": 72},
            "producer_broad_gameobject_autoequip_gap",
            "covered_by_owner_route",
        ),
        "inventory_companion": (
            "XRL.World/GameObject.cs::GameObject.HandleInventoryActionEvent(InventoryActionEvent)",
            "XRL.World/GameObject.cs",
            "HandleInventoryActionEvent",
            {"Popup": 51},
            "producer_broad_gameobject_inventory_companion_gap",
            "covered_by_owner_route",
        ),
        "missile_trajectory": (
            "XRL.World.Parts/MissileWeapon.cs::"
            "MissileWeapon.CalculateBulletTrajectory(out bool,out bool,out Cell,MissilePath,"
            "GameObject,GameObject,GameObject,Zone,string,int,int,bool)",
            "XRL.World.Parts/MissileWeapon.cs",
            "CalculateBulletTrajectory",
            {"MessageFrame": 45},
            "producer_broad_missile_trajectory_message_runtime",
            "covered_by_owner_route",
        ),
        "death": (
            "XRL.World/GameObject.cs::"
            "GameObject.Die(GameObject,string,string,string,bool,GameObject,GameObject,bool,bool,string,string,string)",
            "XRL.World/GameObject.cs",
            "Die",
            {"Popup": 44},
            "producer_broad_gameobject_death_gap",
            "covered_by_owner_route",
        ),
        "destroy": (
            "XRL.World/GameObject.cs::GameObject.Destroy(string,bool,bool,string)",
            "XRL.World/GameObject.cs",
            "Destroy",
            {"Popup": 22},
            "producer_broad_gameobject_destroy_gap",
            "covered_by_owner_route",
        ),
        "pulldown": (
            "XRL.World/GameObject.cs::GameObject.PullDown(bool)",
            "XRL.World/GameObject.cs",
            "PullDown",
            {"Popup": 15},
            "producer_broad_gameobject_pulldown_gap",
            "covered_by_owner_route",
        ),
        "regenera": (
            "XRL.World/GameObject.cs::GameObject.FireEvent(Event)",
            "XRL.World/GameObject.cs",
            "FireEvent",
            {"Does": 13},
            "producer_broad_gameobject_regenera_runtime",
            "covered_by_owner_route",
        ),
        "explode": (
            "XRL.World/GameObject.cs::GameObject.Explode(int,GameObject,string,float,bool,bool,bool,int,List<GameObject>)",
            "XRL.World/GameObject.cs",
            "Explode",
            {"Does": 7},
            "producer_broad_gameobject_explode_death_gap",
            "covered_by_owner_route",
        ),
        "hostile_spot": (
            "XRL.World/GameObject.cs::"
            "GameObject.ArePerceptibleHostilesNearby(bool,bool,string,OngoingAction,string,int,int,bool,bool)",
            "XRL.World/GameObject.cs",
            "ArePerceptibleHostilesNearby",
            {"Popup": 3},
            "producer_broad_gameobject_hostile_spot_gap",
            "covered_by_owner_route",
        ),
        "replace_cell": (
            "XRL.World/GameObject.cs::GameObject.PerformReplaceCell(GameObject)",
            "XRL.World/GameObject.cs",
            "PerformReplaceCell",
            {"Popup": 2},
            "producer_broad_gameobject_replace_cell_gap",
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    for family_id, _, _, _, _, disposition in families.values():
        expected_status = {
            "covered_by_owner_route": "covered_by_owner_route",
            "likely_implementation_gap": "action_required",
            "runtime_evidence_required": "runtime_required",
        }[disposition]
        assert entries[family_id]["closure_status"] == expected_status

    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-broad-producer-static-shapes.json"))
    inventory_companion_evidence = " ".join(entries[families["inventory_companion"][0]]["closure_evidence"])
    assert "GameObjectPopupTranslationPatch.cs" in inventory_companion_evidence
    assert "HandleInventoryActionEvent" in inventory_companion_evidence
    hostile_spot_evidence = " ".join(entries[families["hostile_spot"][0]]["closure_evidence"])
    assert "GameObjectSpotTranslationPatch.cs" in hostile_spot_evidence
    assert "GameObjectSpot_TranslatesSpotPopup_WhenPatched" in hostile_spot_evidence
    explode_evidence = " ".join(entries[families["explode"][0]]["closure_evidence"])
    assert "GameObject.Explode closure" in explode_evidence
    assert "DeathReasonTranslationPatch.cs" in explode_evidence
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if disposition != "covered_by_owner_route"
    }


def test_policy_promotes_missile_trajectory_message_frame_route() -> None:
    """Missile trajectory reflection/refraction frames are closed by MessageFrame ownership."""
    family_id = (
        "XRL.World.Parts/MissileWeapon.cs::"
        "MissileWeapon.CalculateBulletTrajectory(out bool,out bool,out Cell,MissilePath,"
        "GameObject,GameObject,GameObject,Zone,string,int,int,bool)"
    )
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World.Parts/MissileWeapon.cs",
                "CalculateBulletTrajectory",
                {"MessageFrame": 45},
            )
        ]
    )

    entry = next(entry for entry in valuable_surface_queue(inventory) if entry["family_id"] == family_id)
    evidence = " ".join(entry["closure_evidence"])
    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-missile-trajectory.json"))

    assert entry["closure_status"] == "covered_by_owner_route"
    assert "XDidYTranslationPatch.cs" in evidence
    assert "MessageFrameTranslatorTests.cs" in evidence
    assert "tier1 verb=reflect" in evidence
    assert residual["entries"] == []


def test_policy_closes_gameobject_die_as_route_split_owner_coverage() -> None:
    """GameObject.Die route split is closed by the existing owner route set."""
    family_id = (
        "XRL.World/GameObject.cs::"
        "GameObject.Die(GameObject,string,string,string,bool,GameObject,GameObject,bool,bool,string,string,string)"
    )
    inventory = _inventory(
        [
            _family(
                family_id,
                "XRL.World/GameObject.cs",
                "Die",
                {"JournalAPI": 1, "MessageFrame": 2, "Popup": 1, "TutorialManagerPopup": 3},
            )
        ]
    )

    entry = next(entry for entry in valuable_surface_queue(inventory) if entry["family_id"] == family_id)
    evidence = " ".join(entry["closure_evidence"])
    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-gameobject-die.json"))

    assert entry["closure_status"] == "covered_by_owner_route"
    assert "GameObject.Die review closes the route split" in evidence
    assert "DeathReasonTranslationPatch.cs" in evidence
    assert "GameObjectDieTranslationPatch.cs" in evidence
    assert "JournalTextTranslator.cs" in evidence
    assert "JournalApiAddTranslationPatchTests.cs" in evidence
    assert residual["entries"] == []


def test_policy_promotes_popup_message_wrappers_as_sink_pass_through() -> None:
    """Popup message wrappers pass caller-owned text through without owning fixed leaves."""
    families = {
        "new_popup": (
            (
                "XRL.UI/Popup.cs::"
                "Popup.NewPopupMessageAsync(string,List<QudMenuItem>,List<QudMenuItem>,string,string,int,string,"
                "IRenderable,IRenderable,bool,bool,bool,CancellationToken,bool,string,string,Location2D,string)"
            ),
            "NewPopupMessageAsync",
            {"DirectTextAssignment": 11},
        ),
        "wait_popup": (
            (
                "XRL.UI/Popup.cs::"
                "Popup.WaitNewPopupMessage(string,List<QudMenuItem>,Action<QudMenuItem>,List<QudMenuItem>,"
                "string,string,int,string,IRenderable,IRenderable,bool,bool,Location2D,string,bool)"
            ),
            "WaitNewPopupMessage",
            {"DirectTextAssignment": 15},
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, "XRL.UI/Popup.cs", member_name, surfaces)
            for family_id, member_name, surfaces in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-popup-wrapper-sink.json"))

    assert residual["entries"] == []
    for family_id, _, _ in families.values():
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert entries[family_id]["closure_status"] == "not_owner_surface"
        assert "generic PopupMessage.ShowPopup wrappers" in evidence
        assert "no route-local fixed English leaf" in evidence


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
        "XRL.World/Reputation.cs::Reputation.Modify(Faction,int,string,StringBuilder,string,bool,bool,bool,bool)"
    )
    gives_rep_family_id = "XRL.World.Parts/GivesRep.cs::GivesRep.HandleEvent(BeforeDeathRemovalEvent)"
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
        "XRL.World.Parts/LocateRelicQuestManager.cs::LocateRelicQuestManagerSystem.CheckCompleted(GameObject)"
    )
    interact_family_id = (
        "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestManager.cs::System.FinishEntry(QuestEntry,GameObject)"
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
    body_family_id = "XRL.World.Parts/Body.cs::Body.Dismember(BodyPart,GameObject,IInventory,bool,bool,IEvent)"
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
    assert "JournalApiAddTranslationPatchTests.cs" in " ".join(entries[village_surface_family_id]["closure_evidence"])
    assert "RevealString data" in " ".join(entries[village_surface_family_id]["closure_evidence"])

    assert "SingleCallsiteOwnerPopupTranslationPatchTests.cs" in " ".join(
        entries[animator_family_id]["closure_evidence"]
    )
    assert "BodyTranslationPatch.cs" in " ".join(entries[body_family_id]["closure_evidence"])
    assert "StatusScreenPopupTranslationPatchTests.cs" in " ".join(entries[status_family_id]["closure_evidence"])


def test_policy_records_hse_owner_plan_closure_for_existing_covered_families() -> None:
    """Existing HSE owner-plan families should not remain unreviewed after evidence-backed review."""
    cooking_family_id = (
        "XRL.World.Skills.Cooking/CookingRecipe.cs::CookingRecipe.GenerateRecipeName(List<string>,List<string>,string)"
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
    heb_family_id = "XRL.World.Parts/GenerateFriendOrFoe_HEB.cs::GenerateFriendOrFoe_HEB.replacePlaceholders(string)"
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


def test_policy_records_text_filters_speech_status_owner_route_closure() -> None:
    """TextFilters HSE calls are covered by speech/status owner patches."""
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
        assert entries[family_id]["closure_status"] == "covered_by_owner_route"
        evidence = " ".join(entries[family_id]["closure_evidence"])
        assert "TextFilterSpeechStatusTranslationPatches.cs" in evidence
        assert "TextFilterSpeechStatusTranslationPatchTests.cs" in evidence
        assert "TargetMethodResolutionTests.cs" in evidence
        assert "TextFilters.Angry" in evidence
        assert "TextFilters.Lallated" in evidence


def test_policy_splits_misc_runtime_routes_by_static_owner_shape() -> None:
    """Misc residual rows are bucketed by source route instead of staying in one catch-all."""
    families = {
        "angry": (
            "XRL.Language/TextFilters.cs::TextFilters.Angry(string)",
            "XRL.Language/TextFilters.cs",
            "Angry",
            {"HistoricStringExpander": 3},
            None,
            "covered_by_owner_route",
        ),
        "lallated": (
            "XRL.Language/TextFilters.cs::TextFilters.Lallated(string,string)",
            "XRL.Language/TextFilters.cs",
            "Lallated",
            {"HistoricStringExpander": 3},
            None,
            "covered_by_owner_route",
        ),
        "book_line": (
            "XRL.World.Conversations.Parts/InsertRandomBookLine.cs::InsertRandomBookLine.HandleEvent(PrepareTextEvent)",
            "XRL.World.Conversations.Parts/InsertRandomBookLine.cs",
            "HandleEvent",
            {"ConversationTextAppend": 2},
            "conversation_book_line_data_covered",
            "covered_by_owner_route",
        ),
        "glotrot": (
            "XRL.World.Conversations.Parts/GlotrotFilter.cs::GlotrotFilter.HandleEvent(PrepareTextEvent)",
            "XRL.World.Conversations.Parts/GlotrotFilter.cs",
            "HandleEvent",
            {"ConversationTextAppend": 2},
            None,
            "covered_by_owner_route",
        ),
    }
    inventory = _inventory(
        [
            _family(family_id, source_file, member_name, surfaces)
            for family_id, source_file, member_name, surfaces, _, _ in families.values()
        ]
    )

    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(inventory, inventory_path=Path("issue719-misc-route-split-test.json"))

    assert entries[families["glotrot"][0]]["closure_status"] == "covered_by_owner_route"
    assert "N/G + n* + period gibberish" in " ".join(entries[families["glotrot"][0]]["closure_evidence"])
    assert {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in residual["entries"]
    } == {
        family_id: (bucket, disposition)
        for family_id, _, _, _, bucket, disposition in families.values()
        if bucket is not None and disposition != "covered_by_owner_route"
    }


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
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateSpindleNegotiationRelic(string,string,string,string,int)"
    )
    select_element_family_id = (
        "XRL.World/RelicGenerator.cs::RelicGenerator.SelectElement(GameObject,GameObject,GameObject,GameObject)"
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


def test_queue_payload_needs_work_includes_reviewed_action_required_families() -> None:
    """Known #719 residual work remains visible after leaving raw unreviewed state."""
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

    assert [entry["family_id"] for entry in payload["entries"]] == [
        partial_family_id,
        "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging()",
    ]
    assert [entry["closure_status"] for entry in payload["entries"]] == [
        "partial_coverage",
        "action_required",
    ]


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


def test_load_inventory_applies_issue719_gameobject_closeout_to_docs_producer_ids(tmp_path: Path) -> None:
    """Docs inventory producer_family_id rows use the same Issue #719 closeout overlays."""
    inventory_path = tmp_path / "static-producer-inventory.json"
    inventory_path.write_text(
        """
{
  "schema_version": "1.0",
  "game_version": "1.0.4",
  "totals": {},
  "families": [
    {
      "producer_family_id": "XRL.World/GameObject.cs::XRL.World.GameObject.Die",
      "file": "XRL.World/GameObject.cs",
      "type_name": "XRL.World.GameObject",
      "member_name": "Die",
      "member_start_line": 14491,
      "surface_counts": {"Popup": 1},
      "representative_calls": [{"line": 14510, "target_surface": "Popup"}]
    },
    {
      "producer_family_id": "XRL.World/GameObject.cs::XRL.World.GameObject.Destroy",
      "file": "XRL.World/GameObject.cs",
      "type_name": "XRL.World.GameObject",
      "member_name": "Destroy",
      "member_start_line": 3306,
      "surface_counts": {"Popup": 1},
      "representative_calls": [{"line": 3330, "target_surface": "Popup"}]
    },
    {
      "producer_family_id": "XRL.World/GameObject.cs::XRL.World.GameObject.ArePerceptibleHostilesNearby",
      "file": "XRL.World/GameObject.cs",
      "type_name": "XRL.World.GameObject",
      "member_name": "ArePerceptibleHostilesNearby",
      "member_start_line": 11100,
      "surface_counts": {"Popup": 1},
      "representative_calls": [{"line": 11128, "target_surface": "Popup"}]
    }
  ]
}
""",
        encoding="utf-8",
    )

    inventory = load_inventory(inventory_path)
    entries = {entry["family_id"]: entry for entry in valuable_surface_queue(inventory)}
    residual = residual_bucket_payload(inventory, inventory_path=inventory_path)

    assert {
        family_id: entry["closure_status"]
        for family_id, entry in entries.items()
    } == {
        "XRL.World/GameObject.cs::XRL.World.GameObject.Die": "covered_by_owner_route",
        "XRL.World/GameObject.cs::XRL.World.GameObject.Destroy": "covered_by_owner_route",
        (
            "XRL.World/GameObject.cs::"
            "XRL.World.GameObject.ArePerceptibleHostilesNearby"
        ): "covered_by_owner_route",
    }
    assert "GameObjectDieTranslationPatch.cs" in " ".join(
        entries["XRL.World/GameObject.cs::XRL.World.GameObject.Die"]["closure_evidence"]
    )
    assert "GameObjectDestroyTranslationPatch.cs" in " ".join(
        entries["XRL.World/GameObject.cs::XRL.World.GameObject.Destroy"]["closure_evidence"]
    )
    assert "GameObjectSpotTranslationPatch.cs" in " ".join(
        entries["XRL.World/GameObject.cs::XRL.World.GameObject.ArePerceptibleHostilesNearby"]["closure_evidence"]
    )
    assert residual["entries"] == []


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


def test_followup_issue_payload_filters_covered_entries_when_called_with_valuable_include() -> None:
    """Follow-up grouping remains residual-only even if the caller asks for valuable entries."""
    covered_book_line = (
        "XRL.World.Conversations.Parts/InsertRandomBookLine.cs::"
        "InsertRandomBookLine.HandleEvent(PrepareTextEvent)"
    )
    action_required_status_line = "Qud.UI/LeftSideCategory.cs::LeftSideCategory.setData(object)"
    inventory = _inventory(
        [
            _family(
                covered_book_line,
                "XRL.World.Conversations.Parts/InsertRandomBookLine.cs",
                "HandleEvent",
                {"ConversationTextAppend": 1},
            ),
            _family(
                action_required_status_line,
                "Qud.UI/LeftSideCategory.cs",
                "setData",
                {"SetText": 1},
            ),
        ]
    )

    payload = followup_issue_payload(inventory, inventory_path=Path("issue809-followup-test.json"), include="valuable")

    issue = payload["issues"]["issue719-consolidated-residuals"]
    assert payload["actionable_entries"] == 1
    assert payload["issue_counts"] == {"issue719-consolidated-residuals": 1}
    assert issue["entry_count"] == 1
    assert issue["top_entries"][0]["family_id"] == action_required_status_line


def _inventory(families: list[TextConstructionFamily]) -> TextConstructionInventory:
    return {
        "schema_version": "1.0",
        "game_version": "1.0.4",
        "totals": {},
        "families": families,
    }


def _assert_producer_runtime_residuals(payload: dict[str, Any], expected_family_ids: set[str]) -> None:
    actual = {
        entry["family_id"]: (entry["residual_bucket"], entry["residual_disposition"])
        for entry in payload["entries"]
        if entry["family_id"] in expected_family_ids
    }
    assert set(actual) == expected_family_ids
    for residual_bucket, residual_disposition in actual.values():
        assert residual_disposition == "runtime_evidence_required"
        assert residual_bucket.startswith("producer_runtime_")


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
