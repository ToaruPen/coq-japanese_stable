from __future__ import annotations

from pathlib import Path

from scripts.text_construction_surface_policy import (
    TextConstructionFamily,
    TextConstructionInventory,
    build_surface_queue,
    classify_family,
    format_surface_queue,
    lane_summary_payload,
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
    assert entries[tombstone_family_id]["closure_status"] == "runtime_required"
    assert entries[mod_gigantic_family_id]["closure_status"] == "likely_true_gap"


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
