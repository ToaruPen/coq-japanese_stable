"""Classify Roslyn text-construction surfaces by localization value."""

from __future__ import annotations

import json
import sys
from argparse import ArgumentParser
from functools import lru_cache
from pathlib import Path
from typing import Final, Literal, TypedDict, cast

if __package__ in {None, ""}:  # pragma: no cover - direct script execution path
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from scripts.static_producer_closure import COVERED_OWNER_FAMILIES

Classification = Literal[
    "player_visible_api",
    "player_visible_owner_candidate",
    "candidate_only",
    "non_target",
]
OutputFormat = Literal["text", "json", "lanes-json", "residual-buckets-json", "followup-issues-json"]
ClosureStatus = Literal[
    "action_required",
    "covered_by_owner_route",
    "not_owner_surface",
    "partial_coverage",
    "runtime_required",
    "likely_true_gap",
    "unreviewed",
]
ClosureLane = Literal[
    "activated_ability_names",
    "combat_message_frame_does",
    "conversation_routes",
    "description_effect_detail",
    "display_name_composition",
    "history_generated_text",
    "journal_quest_routes",
    "producer_message_popup",
    "screen_ui_direct_text",
    "other_owner_candidate",
]
ResidualDisposition = Literal[
    "existing_evidence_policy_overlay",
    "covered_by_existing_route",
    "child_issue_needed",
    "runtime_evidence_required",
    "likely_implementation_gap",
]


class ClosureOverlayEntry(TypedDict):
    """Reviewed closure evidence for one text-construction family."""

    closure_status: ClosureStatus
    closure_evidence: list[str]


PLAYER_VISIBLE_API_SURFACES: Final = {
    "ActivatedAbility",
    "AddPlayerMessage",
    "ConversationChoiceTag",
    "ConversationTextAppend",
    "ConversationTextReplace",
    "Description",
    "DescriptionReturn",
    "DisplayNameReturn",
    "DisplayTextReturn",
    "Does",
    "EffectDescriptionReturn",
    "EmitMessage",
    "GetDisplayName",
    "HistoricStringExpander",
    "JournalAPI",
    "MessageFrame",
    "Popup",
    "TutorialManagerPopup",
}
CONTEXTUAL_OWNER_SURFACES: Final = {
    "DescriptionAssignment",
    "DirectTextAssignment",
    "DisplayNameAssignment",
    "SetText",
}
CONSTRUCTION_ONLY_SURFACES: Final = {
    "Assignment",
    "ReplaceBuilder",
    "ReplaceChain",
    "Return",
    "StringBuilderAppend",
    "StringFormat",
}
NON_TARGET_SURFACES: Final = {
    "Attribute",
    "Initializer",
    "Other",
    "OtherInvocation",
}
UI_OWNER_FILE_PREFIXES: Final = (
    "Qud.UI/",
    "XRL.UI/",
    "XRL.CharacterBuilds.Qud.UI/",
)
GAMEPLAY_OWNER_FILE_PREFIXES: Final = (
    "Qud.API/",
    "XRL.Annals/",
    "XRL.World/",
    "XRL.World.Effects/",
    "XRL.World.Parts/",
    "XRL.World.Parts.Mutation/",
    "XRL.World.Skills.Cooking/",
    "XRL.World.ZoneBuilders/",
)
NON_PLAYER_FILE_PREFIXES: Final = (
    "Overlay.MapEditor/",
    "UnityStandardAssets.",
    "XRL.Wish/",
)
NON_PLAYER_FILE_NAMES: Final = (
    "UAP_",
    "uGUI",
)
LOW_VALUE_FILE_PARTS: Final = (
    "/ObjectFinderTests.cs",
    "/StatWishHandler.cs",
    "/WishMenu.cs",
    "/Wishing.cs",
    "Debug",
    "MetricsManager.cs",
    "Test",
    "WorkshopUploader",
)
CLASSIFICATION_ORDER: Final = {
    "player_visible_api": 0,
    "player_visible_owner_candidate": 1,
    "candidate_only": 2,
    "non_target": 3,
}
VALUABLE_CLASSIFICATIONS: Final = frozenset({"player_visible_api", "player_visible_owner_candidate"})
ACTIONABLE_CLOSURE_STATUSES: Final = frozenset({"action_required", "partial_coverage", "likely_true_gap"})
LANE_ORDER: Final = {
    "combat_message_frame_does": 0,
    "conversation_routes": 1,
    "history_generated_text": 2,
    "screen_ui_direct_text": 3,
    "display_name_composition": 4,
    "description_effect_detail": 5,
    "journal_quest_routes": 6,
    "activated_ability_names": 7,
    "producer_message_popup": 8,
    "other_owner_candidate": 9,
}
COMBAT_MELEE_ATTACK_FAMILY_ID: Final = (
    "XRL.World.Parts/Combat.cs::Combat.MeleeAttackWithWeaponInternal("
    "GameObject,GameObject,GameObject,BodyPart,string,int,int,int,int,int,bool,bool)"
)
CUDGEL_SLAM_CAST_FAMILY_ID: Final = (
    "XRL.World.Parts.Skill/Cudgel_Slam.cs::Cudgel_Slam.Cast(GameObject,Cudgel_Slam,string,GameObject,bool,int,string)"
)
SHIELD_SLAM_SLAM_FAMILY_ID: Final = (
    "XRL.World.Parts.Skill/Shield_Slam.cs::Shield_Slam.Slam(GameObject,GameObject,Cell,bool)"
)
CONVERSATION_CHOICE_TAG_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationDisplayTextPatchTests.cs",
    "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
]
CONVERSATION_BODY_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationDisplayTextPatchTests.cs",
    "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
]
QUEST_SIGNPOST_PARTIAL_EVIDENCE: Final = "QuestSignpost directions translated; names use data routes"
TINKERING_RECIPE_PARTIAL_EVIDENCE: Final = "Tinkering recipe label translated; names use data routes"
HERMIT_OATH_PARTIAL_EVIDENCE: Final = "Default hermit address translated; custom addresses use data routes"
LEARN_SKILL_PARTIAL_EVIDENCE: Final = "Initiatory prompt translated; skill names use dictionary routes"
ISSUE737_HSE_AUDIT_EVIDENCE: Final = "docs/reports/2026-05-19-issue-737-hse-route-audit.md"
ISSUE726_TEXT_FILTER_ROUTE_EVIDENCE: Final = [
    "https://github.com/ToaruPen/coq-japanese_stable/issues/726",
    (
        "TextFilters.Angry/Lallated have static owner evidence but remain "
        "speech/status transformation routes requiring owner-specific runtime evidence"
    ),
    (
        "semantic-probe TextFilters.Filter: resolved owners XRL.World.Parts.Preacher "
        "and XRL.World.Conversations.Parts.TextFilter"
    ),
    (
        "semantic-probe TextFilters.Angry: resolved owner XRL.World.Capabilities.StyledStatus "
        "plus TextFilters.Filter switch"
    ),
    "static data source: DomesticatedSlave assigns Lallated to Preacher and ConversationScript",
    "decompiled owner: StyledStatus.Format angry style calls TextFilters.Angry(Name) and TextFilters.Angry(Value)",
    "decompiled owner: Preacher.PreacherHomily filters lineText before EmitMessage and ParticleText",
    "decompiled owner: ConversationScript installs XRL.World.Conversations.Parts.TextFilter for conversation text",
    (
        "runtime deferral reason: filtered outputs mutate already-composed speech/status text, "
        "so completion needs an observed owner-specific final output rather than a raw HSE leaf"
    ),
]
ISSUE719_TEXT_FILTER_SPEECH_STATUS_ROUTE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 speech/status filter owner route covers TextFilters.Angry/Lallated "
        "at the source methods instead of relying on downstream status or conversation sinks."
    ),
    (
        "TextFilters.Angry expands <spice.textFilters.angry.!random> and passes it "
        "to Grammar.Stutterize; TextFilters.Lallated expands "
        "<spice.textFilters.lallated.!random> with *Text* and *Noise* variables."
    ),
    (
        "TextFilterSpeechStatusTranslationPatches.cs targets XRL.Language.TextFilters.Angry(string) "
        "and XRL.Language.TextFilters.Lallated(string,string); L2 tests prove angry leaves and "
        "carried lallated speech text translate while unknown speech remains pass-through."
    ),
    "Mods/QudJP/Assemblies/src/Patches/TextFilterSpeechStatusTranslationPatches.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TextFilterSpeechStatusTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.Language/TextFilters.cs lines 197-224",
]
ISSUE719_INSERT_RANDOM_BOOK_LINE_DATA_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 final runtime audit promotes InsertRandomBookLine because the "
        "conversation part inserts BookUI.Books[BookID] page lines, and the only "
        "localized in-repo use points at the Japanese AlchemistMutterings book data."
    ),
    'Mods/QudJP/Localization/Conversations.jp.xml SusaAlchemist uses BookID="AlchemistMutterings".',
    "Mods/QudJP/Localization/Books.jp.xml ships AlchemistMutterings title and page text in Japanese.",
    "decompiled source: XRL.World.Conversations.Parts/InsertRandomBookLine.cs lines 22-32",
]
ISSUE719_CORE_DISPLAY_NAME_DATA_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 final runtime audit promotes core display-name metadata rows "
        "whose visible text is either data-owned or an empty/sentinel fallback."
    ),
    (
        "Faction.DisplayName falls back to Name or CultDisplayName game state; "
        "Factions.jp.xml and world-factions dictionary own SultanCult display names."
    ),
    "Effect.Effect() initializes DisplayName to an empty string and does not own a player-visible English leaf.",
    (
        "PointOfInterest.DisplayName falls back to object/explanation data or '?' sentinel, "
        "not a fixed English localization leaf."
    ),
]
ISSUE719_CORE_POSSESSIVE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 final runtime audit closes GameObject.Poss/poss with "
        "GameObjectPossessiveDisplayNameTranslationPatch on the exact owner helper route."
    ),
    (
        "The helpers compose 'Your'/'your' second-person definite article fallbacks "
        "or Grammar.MakePossessive(GetDisplayName(...)) owner prefixes before "
        "delegating to Object.GetDisplayName."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameObjectPossessiveDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectPossessiveDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World/GameObject.cs lines 6840-6867",
]
ISSUE719_PHASE_STICKY_DATA_SENTINEL_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 final runtime audit promotes PhaseSticky.HandleEvent because "
        "the English 'phase web' -> 'web' branch is a base-data display-name sentinel."
    ),
    (
        "QudJP replaces PhaseWeb Render.DisplayName with Japanese data, so "
        'ParentObject.DisplayNameOnlyDirect == "phase web" is not the localized '
        "runtime path."
    ),
    'Mods/QudJP/Localization/ObjectBlueprints/ZoneTerrain.jp.xml PhaseWeb Render.DisplayName="位相の巣".',
    "decompiled source: XRL.World.Parts/PhaseSticky.cs lines 122-129",
]
ISSUE719_CORE_DISPLAY_NAME_DATA_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World/Faction.cs::Faction.DisplayName",
        "XRL.World/Effect.cs::Effect.Effect()",
        "XRL.World/PointOfInterest.cs::PointOfInterest.DisplayName",
    }
)
ISSUE719_CORE_POSSESSIVE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World/GameObject.cs::GameObject.Poss(GameObject,bool,bool?)",
        "XRL.World/GameObject.cs::GameObject.poss(GameObject,bool,bool?)",
    }
)
ISSUE719_CORE_INVALID_OBJECT_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 invalid-object fallback review closes core invalid object "
        "display names with CoreInvalidObjectDisplayNameTranslationPatch on "
        "GameObjectFactory.CreateObject and ZoneManager.GetCachedObjects owner routes."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CoreInvalidObjectDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CoreInvalidObjectDisplayNameTranslationPatchTests.cs",
    (
        "decompiled owner sources: XRL.World/GameObjectFactory.cs lines "
        "1153-1192 and XRL.World/ZoneManager.cs lines 304-312"
    ),
]
ISSUE719_CORE_INVALID_OBJECT_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World/GameObjectFactory.cs::"
            "GameObjectFactory.CreateObject(string,int,int,string,Action<GameObject>,Action<GameObject>,string,List<GameObject>)"
        ),
        "XRL.World/GameObjectFactory.cs::GameObjectFactory.CreateObject(string,Action<GameObject>)",
        "XRL.World/ZoneManager.cs::ZoneManager.GetCachedObjects(string)",
    }
)
ISSUE719_PHASE_STICKY_DATA_SENTINEL_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/PhaseSticky.cs::PhaseSticky.HandleEvent(RealityStabilizeEvent)",
    }
)
ISSUE737_COOKING_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CampfireRollIngredientsTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CampfireDescribeMealTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CampfireCookFromIngredientsTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CookingRecipeDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CookingIngredientFragmentTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/CookingIngredientFragmentTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/CookingMealDescriptionTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/CookingRecipeDisplayNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageLogPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfirePreserveTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireRollIngredientsTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireDescribeMealTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireCookFromIngredientsTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingRecipeDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    (
        "Issue #737 cooking family is covered at owner routes: preserve popup/message-log "
        "frames, RollIngredients ingredient fragments, DescribeMeal cook templates, "
        "CookFromIngredients owner popups, and CookingRecipe.GetDisplayName component/"
        "suffix/preposition grammar"
    ),
    (
        "spice.cooking.terrain.* direct coverage is 290/290; recipeNames direct "
        "coverage is 524/531 with raw placeholders and route-local prepositions "
        "handled by the recipe display-name owner route"
    ),
    "spice.cooking.ate[0] fixed popup leaf is covered by the existing ^You eat the meal\\.$ popup pattern",
]
ISSUE737_COOK_RECIPE_PRESET_ROUTE_EVIDENCE: Final = [
    *ISSUE737_COOKING_ROUTE_EVIDENCE,
    "Mods/QudJP/Assemblies/src/Patches/CampfireCookFromRecipeTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CampfireCookPresetMealTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireCookFromRecipeTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireCookPresetMealTranslationPatchTests.cs",
    (
        "CookFromRecipe and CookPresetMeal have route-specific owner evidence for "
        "menu-line popups, ingredient-shortage popups, preset meal popups, "
        "direct-marker pass-through, and color/empty edge cases"
    ),
]
ISSUE737_JOURNAL_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalLineTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalTextTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsCandidateInventoryTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsCollisionTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalEntryDisplayTextPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalLineTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ReshephHistoryTranslationTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/Fixtures/annals-samples.json",
    "scripts/_artifacts/annals/candidates_pending.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    (
        "Issue #737 runtime sample fixed for sultan date/title/Abdicate annal body, "
        "map-note location, generated relationship-title fragments, and storage-time "
        "JournalAPI.AddAccomplishment text/mural/gospel variants; accepted annals "
        "candidates are merged into annals-patterns.ja.json"
    ),
]
HSE_JOURNAL_OBSERVATION_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalObservationAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ISSUE737_HSE_AUDIT_EVIDENCE,
]
HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/DynamicQuestConversationTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DynamicQuestConstructorConversationTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DynamicQuestGeneratedQuestTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DynamicQuestSignpostConversationTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/VillageDynamicQuestItemNameMutationTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DynamicQuestConversationTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DynamicQuestConstructorConversationTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DynamicQuestGeneratedQuestTextTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DynamicQuestSignpostConversationTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/VillageDynamicQuestItemNameMutationTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_JOURNAL_ACCOMPLISHMENT_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/Fixtures/annals-samples.json",
    "docs/reports/2026-05-05-issue-497-dynamic-dictionary-audit.md",
    ISSUE737_HSE_AUDIT_EVIDENCE,
]
HSE_DYNAMIC_QUEST_COMPLETION_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    (
        "static producer evidence: InteractWithAnObjectDynamicQuestManager "
        "uses finite QuestableVerb tags from Base ObjectBlueprints/Furniture.xml"
    ),
]
HSE_JOURNAL_STORY_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
]
HSE_COOKING_DISPLAY_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/CookingRecipeDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CookingRecipeGenerateRecipeTileTranslationScopePatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CookbookDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/CookingRecipeDisplayNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/CookbookDisplayNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingRecipeDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookbookDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
ISSUE719_COOKING_PRESET_RECIPE_DISPLAY_NAME_EVIDENCE: Final = [
    *HSE_COOKING_DISPLAY_ROUTE_EVIDENCE,
    "Mods/QudJP/Localization/Dictionaries/Scoped/ui-popup-campfire-preset-meals.ja.json",
    (
        "Issue #719 preset cooking recipe display-name overrides are covered by "
        "CookingRecipeDisplayNameTranslationPatch targeting each preset "
        "GetDisplayName override and translating the fixed authored names through "
        "the campfire preset meal scoped dictionary."
    ),
]
ISSUE719_COOKING_PRESET_RECIPE_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Skills.Cooking/AppleMatz.cs::AppleMatz.GetDisplayName()",
        "XRL.World.Skills.Cooking/BoneBabka.cs::BoneBabka.GetDisplayName()",
        "XRL.World.Skills.Cooking/CloacaSurprise.cs::CloacaSurprise.GetDisplayName()",
        "XRL.World.Skills.Cooking/CrystalDelight.cs::CrystalDelight.GetDisplayName()",
        "XRL.World.Skills.Cooking/GoatAndSweetLeaf.cs::GoatAndSweetLeaf.GetDisplayName()",
        "XRL.World.Skills.Cooking/HotandSpiny.cs::HotandSpiny.GetDisplayName()",
        "XRL.World.Skills.Cooking/MahLahSoup.cs::MahLahSoup.GetDisplayName()",
        "XRL.World.Skills.Cooking/MushroomCider.cs::MushroomCider.GetDisplayName()",
        "XRL.World.Skills.Cooking/ThePorridge.cs::ThePorridge.GetDisplayName()",
        "XRL.World.Skills.Cooking/TongueAndCheek.cs::TongueAndCheek.GetDisplayName()",
    }
)
ISSUE719_VILLAGE_SIGNATURE_DISH_EVIDENCE: Final = [
    *HSE_COOKING_DISPLAY_ROUTE_EVIDENCE,
    (
        "Issue #719 village signature-dish review promotes VillageBase and "
        "VillageCodaBase generateSignatureDish because both methods assign "
        "signatureDish through CookingRecipe.FromIngredients or an authored "
        "signatureDishName property, and visible recipe names are served by the "
        "CookingRecipe.GetDisplayName owner route."
    ),
    (
        "decompiled owner sources: XRL.World.ZoneBuilders/VillageBase.cs lines "
        "2535-2613 and XRL.World.ZoneBuilders/VillageCodaBase.cs lines 2811-2893"
    ),
]
ISSUE719_VILLAGE_SIGNATURE_DISH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.generateSignatureDish(string)",
        "XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.generateSignatureDish(string)",
    }
)
ISSUE719_VILLAGE_SIGNATURE_ITEM_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 village signature-item review closes generateSignatureItems "
        "through VillageSignatureItemTranslationPatch because both VillageBase "
        "and VillageCodaBase directly assign signatureHistoricObjectInstance.DisplayName "
        "from the generated signatureHistoricObjectName snapshot property."
    ),
    (
        "The postfix translates the finite SignatureHistoricObject HistorySpice "
        "frames while preserving unknown owner names and routing the item capture "
        "through GetDisplayNameRouteTranslator."
    ),
    "Mods/QudJP/Assemblies/src/Patches/VillageSignatureItemTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/VillageSignatureItemTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    (
        "decompiled owner sources: XRL.World.ZoneBuilders/VillageBase.cs lines "
        "1337-1350 and XRL.World.ZoneBuilders/VillageCodaBase.cs lines 1639-1652"
    ),
    (
        "decompiled source: XRL.Annals/BecomesKnownFor.cs lines 159-161 creates "
        "signatureHistoricObjectName from <spice.villages.SignatureHistoricObject>."
    ),
]
ISSUE719_VILLAGE_SIGNATURE_ITEM_STATIC_GAP_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.generateSignatureItems()",
        "XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.generateSignatureItems()",
    }
)
ISSUE719_VILLAGE_CODA_SULTAN_ENTITY_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 VillageCoda generated display-name review closes Coda sultan "
        "names through existing display-name owner routes: GetDisplayNameRouteTranslator "
        "handles 'shrine to' and 'Cult of' generated prefixes, and the display-name "
        "modifier route handles 'mechanical' golem names."
    ),
    (
        "GenerateSultanEntity JournalAPI.AddSultanNote callsites pass existing "
        "JournalAccomplishment.GospelText values; they do not construct new fixed "
        "English display-name leaves in this owner."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricSpiceGeneratedNameTranslatorTests.cs",
    (
        "decompiled owner source: XRL.World.ZoneBuilders/VillageCoda.cs lines "
        "571-582, 2136-2176, and 2446-2451"
    ),
]
ISSUE719_VILLAGE_CODA_SULTAN_ENTITY_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateSultanEntity(GameObject)",
        "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.SetStatueVisuals(GameObject)",
        "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateMechanicalGolem()",
    }
)
ISSUE719_MURAL_BLANK_SLATE_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 mural blank-slate display-name review closes fixed leaf "
        "assignments through the existing GetDisplayName display-name dictionary route."
    ),
    "Mods/QudJP/Localization/Dictionaries/ui-displayname-atomic.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
    (
        "decompiled owner sources: XRL.World.Parts/PlayerMuralController.cs lines "
        "303-325 and XRL.World.Parts/SultanMuralController.cs lines 374-400"
    ),
]
ISSUE719_MURAL_BLANK_SLATE_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.blankMural(List<Location2D>)",
        "XRL.World.Parts/SultanMuralController.cs::SultanMuralController.blankMural(List<Cell>)",
    }
)
ISSUE719_GENERATED_DISPLAY_NAME_OWNER_PATCH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 generated display-name owner patch covers direct producer "
        "assignments for village factions, temporal fugue copies, mural panels, "
        "and village quest reward recoilers."
    ),
    (
        "VillageBase.CreateVillageFaction translates Faction.DisplayName while "
        "leaving Faction.Name and FormatWithArticle as owner metadata."
    ),
    (
        "TemporalFugue.CreateFugueCopyOf translates Render.DisplayName and the "
        "PlayerCopyDescription string property after copy creation."
    ),
    (
        "SultanMuralController and PlayerMuralController display-name assignments "
        "are translated through the existing mural-of/ruined-mural-of route."
    ),
    (
        "VillageDynamicQuestContext.getQuestReward scopes DynamicQuestRewardElement_GameObject "
        "construction so the generated village recoiler Render.DisplayName is translated "
        "without touching reputation faction keys."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GeneratedDisplayNameOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GeneratedDisplayNameOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "existing helper: Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
    "existing helper: Mods/QudJP/Assemblies/src/Patches/ImportedFoodOrDrinkFactionNameTranslator.cs",
]
ISSUE719_GENERATED_DISPLAY_NAME_OWNER_PATCH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.CreateVillageFaction(HistoricEntitySnapshot)",
        "XRL.World.Parts.Mutation/TemporalFugue.cs::TemporalFugue.CreateFugueCopyOf(GameObject,GameObject,Cell,GameObject,bool,int,int,string,string,string,string,string,IPart)",
        "XRL.World.Parts/SultanMuralController.cs::SultanMuralController.updateHistoricMural(List<Cell>,HistoricEvent)",
        "XRL.World.Parts/SultanMuralController.cs::SultanMuralController.ruinMural(List<Cell>,HistoricEvent)",
        "XRL.World/VillageDynamicQuestContext.cs::VillageDynamicQuestContext.getQuestReward()",
        "XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.updatePlayerMural(List<Location2D>,JournalAccomplishment,int)",
    }
)
ISSUE719_RUNNING_BEHAVIOR_EVENT_BRIDGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 GetRunningBehaviorEvent review promotes Retrieve out of "
        "runtime-required because the method is an event bridge, not a generated "
        "display-name owner."
    ),
    (
        "Retrieve initializes and forwards AbilityName, Verb, EffectDisplayName, "
        "EffectMessageName, EffectDuration, and SpringingEffective through legacy "
        "GetRunningBehavior events and pooled GetRunningBehaviorEvent handlers, "
        "then copies handler-supplied values back to out parameters."
    ),
    (
        "Visible running names are supplied by handlers such as Tactics_Run and "
        "RocketSkates, while Run.SyncAbility consumes the returned values for "
        "activated-ability state. The bridge owns no fixed English visible leaf."
    ),
    "decompiled bridge source: XRL.World/GetRunningBehaviorEvent.cs lines 50-101",
    "decompiled consumer: XRL.World.Parts/Run.cs lines 115-135",
    (
        "decompiled handlers: XRL.World.Parts.Skill/Tactics_Run.cs lines 26-40 "
        "and XRL.World.Parts/RocketSkates.cs lines 48-60"
    ),
]
ISSUE719_RUNNING_BEHAVIOR_EVENT_BRIDGE_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World/GetRunningBehaviorEvent.cs::"
            "GetRunningBehaviorEvent.Retrieve(GameObject,out string,out string,out string,"
            "out string,out int,out bool,Templates.StatCollector)"
        ),
    }
)
ISSUE719_ROCKET_SKATES_RUNNING_BEHAVIOR_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 RocketSkates.GetRunningBehavior review closes the row by "
        "splitting its event fields across existing consumers: Run.SyncAbility "
        "translates the Power Skate ability name, RunStartRunningPopup translates "
        "the power skate verb in world-map failure popups, and Running/message-frame "
        "routes translate the power skating effect/message names."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityMiscProviderTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/RunStartRunningPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActivatedAbilityNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActivatedAbilityMiscProviderTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/RunStartRunningPopupTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_ROCKET_SKATES_RUNNING_BEHAVIOR_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/RocketSkates.cs::RocketSkates.HandleEvent(GetRunningBehaviorEvent)",
    }
)
ISSUE719_WORLD_PART_QUEUE_DOES_EXISTING_OWNER_EVIDENCE_BY_FAMILY: Final[
    dict[str, list[str]]
] = {
    "XRL.World.Parts/Physics.cs::Physics.HandleEvent(ObjectEnteringCellEvent)": [
        (
            "Issue #719 world-part queue/Does review promotes Physics.HandleEvent"
            "(ObjectEnteringCellEvent) because its AddPlayerMessage branches and "
            "Does-composed overland block branch are already served by the exact "
            "Physics object-entering-cell owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PhysicsObjectEnteringCellTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "decompiled owner source: XRL.World.Parts/Physics.cs lines 2565-2599",
    ],
    "XRL.World.Parts/ThiefBot.cs::ThiefBot.FireEvent(Event)": [
        (
            "Issue #719 world-part queue/Does review promotes ThiefBot.FireEvent "
            "because the pincer AddPlayerMessage branches are served by the exact "
            "single-callsite owner queue route, while the snag Does/EmitMessage "
            "branch is served by the existing Does verb route and repository verb "
            "dictionary."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
        "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "decompiled owner source: XRL.World.Parts/ThiefBot.cs lines 22-77",
    ],
    "XRL.World.Parts/Interior.cs::Interior.HandleEvent(TookDamageEvent)": [
        (
            "Issue #719 world-part queue review promotes Interior.HandleEvent(TookDamageEvent) "
            "because its only player-visible queue branch emits the standard damage frame "
            "'<object> takes <amount> damage!', which is already served by the existing "
            "PhysicsProcessTakeDamageTranslationPatch damage-frame route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "decompiled owner source: XRL.World.Parts/Interior.cs lines 644-657",
    ],
    (
        "XRL.World.Parts/VehicleMeleeInfiltration.cs::"
        "VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent)"
    ): [
        (
            "Issue #719 VehicleMeleeInfiltration.HandleEvent review promotes the "
            "player infiltration EmitMessage branch because the emitted "
            'Does("infiltrate") fragment is now translated by the GameObject '
            "EmitMessage owner route before the generic direct-marker pass-through."
        ),
        "decompiled owner source: XRL.World.Parts/VehicleMeleeInfiltration.cs lines 71-104",
        "Mods/QudJP/Assemblies/src/Patches/GameObjectEmitMessageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
        "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    ],
}
ISSUE719_WORLD_PART_QUEUE_DOES_EXISTING_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    ISSUE719_WORLD_PART_QUEUE_DOES_EXISTING_OWNER_EVIDENCE_BY_FAMILY
)
ISSUE719_SOUND_MANAGER_DEBUG_PASSTHROUGH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 SoundManager review promotes _PlaySound and _PlayWorldSound "
        "out of runtime-required because their AddPlayerMessage rows are gated by "
        "WriteSoundsToLog and emit sound identifiers plus missing/invalid diagnostics, "
        "not localizable gameplay text."
    ),
    (
        "decompiled owner source: SoundManager.cs lines 462-487 and 540-565; "
        "XRL.UI/Options.cs line 1325 sets WriteSoundsToLog from OptionWriteSoundsToLog."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SoundManagerSetChannelTrackTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SoundManagerSetChannelTrackTranslationPatchTests.cs",
    "SoundManagerSetChannelTrack_LeavesDebugMissingTrackMessageUnchanged_WhenOwnerPatched",
]
ISSUE719_SOUND_MANAGER_DEBUG_PASSTHROUGH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "SoundManager.cs::SoundManager._PlaySound(string,float,float,SoundRequest.SoundEffectType)",
        "SoundManager.cs::SoundManager._PlayWorldSound(string,float,float,float,float,Point2D)",
    }
)
ISSUE719_MISSILE_TRAJECTORY_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 MissileWeapon review promotes CalculateBulletTrajectory "
        "because the only player-visible text construction is the static "
        "Messaging.XDidYToZ reflection/refraction frame."
    ),
    (
        "decompiled owner source: XRL.World.Parts/MissileWeapon.cs lines "
        "1372-1480 builds RefractLight/ReflectProjectile events, defaults "
        'Verb to "refract"/"reflect", and emits IComponent<GameObject>.XDidYToZ.'
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml",
    "scripts/tests/test_object_blueprint_merge_semantics.py",
    "MessageFrame keys: tier1 verb=refract and tier1 verb=reflect for projectile-object frames",
]
ISSUE719_MISSILE_TRAJECTORY_MESSAGE_FRAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/MissileWeapon.cs::"
            "MissileWeapon.CalculateBulletTrajectory(out bool,out bool,out Cell,MissilePath,"
            "GameObject,GameObject,GameObject,Zone,string,int,int,bool)"
        ),
    }
)
ISSUE719_GAMEOBJECT_DIE_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 GameObject.Die review closes the route split through "
        "existing owner routes: tutorial popups, checkpoint death wrappers, "
        "death-reason parameters, companion queued death messages, custom "
        "EmitMessage death messages, DidX death verbs, and JournalAPI "
        "death-accomplishment storage."
    ),
    (
        "decompiled owner source: XRL.World/GameObject.cs lines 14491-14641; "
        "DeathReasonTranslationPatch covers Reason and ThirdPersonReason, "
        "GameObjectDieTranslationPatch covers companion queued death messages, "
        "JournalTextTranslator covers the death accomplishment date wrapper, "
        "DeathWrapperFamilyTranslator covers checkpoint death wrappers, and "
        "TutorialManagerTranslationPatch covers tutorial death intermissions."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/GameObjectDieTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/TutorialManagerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
]
ISSUE719_GAMEOBJECT_DIE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World/GameObject.cs::"
            "GameObject.Die(GameObject,string,string,string,bool,GameObject,GameObject,bool,bool,string,string,string)"
        ),
        "XRL.World/GameObject.cs::XRL.World.GameObject.Die",
    }
)
ISSUE719_GAMEOBJECT_DESTROY_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 GameObject.Destroy is covered by an exact owner patch for "
        "fixed player death literals, fallback destroyed/obliterated death "
        "reasons, and companion death popup/queue messages."
    ),
    "decompiled owner source: XRL.World/GameObject.cs lines 3306-3402",
    "Mods/QudJP/Assemblies/src/Patches/GameObjectDestroyTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectDestroyTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_GAMEOBJECT_DESTROY_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World/GameObject.cs::GameObject.Destroy(string,bool,bool,string)",
        "XRL.World/GameObject.cs::XRL.World.GameObject.Destroy",
    }
)
ISSUE719_GAME_TEXT_THIRD_PERSON_DEATH_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 GameText.RoughConvertSecondPersonToThirdPerson closure "
        "uses GameTextDeathReasonTranslationPatch to translate helper-owned "
        "third-person death reasons after the game converts player-facing "
        "'You were ...' death text into object-facing narration."
    ),
    (
        "Physics.UpdateTemperature assigns LastThirdPersonDeathReason from "
        "GameText.RoughConvertSecondPersonToThirdPerson when no explicit "
        "ThirdPersonDeathReason parameter exists; GameObject.Die uses the "
        "same helper for companion death narration when ThirdPersonReason is empty."
    ),
    (
        "GameTextDeathReasonTranslationPatch derives the original player death "
        "reason key, reuses the ui-death dictionary, and preserves unknown "
        "converted reasons unchanged."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameTextDeathReasonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameTextDeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled helper source: XRL/GameText.cs lines 426-465",
    "decompiled caller: XRL.World.Parts/Physics.cs lines 3573-3584",
    "decompiled caller: XRL.World/GameObject.cs lines 3363-3373",
    "existing route: Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs",
]
ISSUE719_GAMEOBJECT_EXPLODE_DEATH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 GameObject.Explode closure: visible explosion effects are "
        "owned by Physics.ApplyExplosion, while the two creature-death branches "
        "delegate to GameObject.Die with fixed player death reasons and "
        "third-person @@ death-reason frames."
    ),
    (
        "DeathWrapperFamilyTranslator covers the player-facing 'You exploded.' "
        "and 'You were crushed under the weight of a thousand suns.' death bodies; "
        "DeathReasonTranslationPatch now covers the generated third-person "
        "'{subject} @@exploded.' and '{subject} @@crushed under the weight of a "
        "thousand suns.' reasons."
    ),
    "decompiled owner source: XRL.World/GameObject.cs lines 14668-14708",
    "Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DeathWrapperFamilyTranslatorTests.cs",
]
ISSUE719_POPUP_MESSAGE_WRAPPER_SINK_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Popup wrapper review classifies NewPopupMessageAsync and "
        "WaitNewPopupMessage as reviewed not-owner surfaces because both methods "
        "are generic PopupMessage.ShowPopup wrappers, not fixed-text owners."
    ),
    (
        "decompiled owner source: XRL.UI/Popup.cs lines 751-920 pass caller "
        "message/title/options/inputDefault through to PopupMessage.ShowPopup, "
        "perform CP437/input escaping, and return selected/input text; no "
        "route-local fixed English leaf is owned by these methods."
    ),
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
]
ISSUE719_POPUP_MESSAGE_WRAPPER_SINK_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.UI/Popup.cs::"
            "Popup.NewPopupMessageAsync(string,List<QudMenuItem>,List<QudMenuItem>,string,string,int,string,"
            "IRenderable,IRenderable,bool,bool,bool,CancellationToken,bool,string,string,Location2D,string)"
        ),
        (
            "XRL.UI/Popup.cs::"
            "Popup.WaitNewPopupMessage(string,List<QudMenuItem>,Action<QudMenuItem>,List<QudMenuItem>,"
            "string,string,int,string,IRenderable,IRenderable,bool,bool,Location2D,string,bool)"
        ),
    }
)
HSE_MEMORIAL_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/EaterCryptPlaqueTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MemorialInscriptionIntroTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/TombstoneDeathCauseTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/EaterCryptPlaqueTextTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MemorialInscriptionIntroTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TombstoneDeathCauseTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_RELIC_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/RelicGeneratorGeneratedNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/RelicDescriptionAddendumTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PseudoRelicGeneratedNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ItemNamingGeneratedNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/RelicGeneratorGeneratedNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/RelicDescriptionAddendumTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PseudoRelicGeneratedNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ItemNamingGeneratedNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/VillageWallDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/VillageTerrainRevealDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SultanRegionRevealDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SultanRegionRevealDescriptionTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/VillageWallDescriptionTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/VillageTerrainRevealDescriptionTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/SultanRegionRevealDescriptionTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/VillageWallDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/VillageTerrainRevealDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SultanRegionRevealDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_VILLAGE_CONVERSATION_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/VillageLeaderConversationTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/VillagePetConversationTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/VillageLeaderConversationTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/VillagePetConversationTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/VillageLeaderConversationTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/VillagePetConversationTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_VILLAGE_BUILDZONE_ROUTE_EVIDENCE: Final = [
    *HSE_VILLAGE_CONVERSATION_ROUTE_EVIDENCE,
    "Mods/QudJP/Assemblies/src/Patches/AddVillageGospelsTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/GenerateVillageEraHistoryTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/HistoricNarrativeTranslationPatchesResolutionTests.cs",
    (
        "static producer evidence: Village/VillageCoda.BuildZone HSE pet origin-story "
        "flows into AddVillagerConversation and is covered by the pet conversation owner route"
    ),
]
HSE_DIMENSION_PSYCHIC_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/DimensionManagerGeneratedNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DimensionManagerExtraDimensionNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PsychicHunterGeneratedTitleTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DimensionManagerGeneratedNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/PsychicHunterGeneratedTitleTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DimensionManagerGeneratedNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PsychicHunterGeneratedTitleTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_MISC_OWNER_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/BroadcastPowerOcclusionReasonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MerchantAdvertisementTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/TempleDedicationPlaqueInscriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SettlementFarmNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/BroadcastPowerOcclusionReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MerchantAdvertisementTextTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TempleDedicationPlaqueInscriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SettlementFarmNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
HSE_FRIEND_OR_FOE_REASON_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/FriendOrFoeReasonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/FriendOrFoeReasonTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/FriendOrFoeReasonTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/FriendOrFoeReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ISSUE737_HSE_AUDIT_EVIDENCE,
]
HSE_SULTANATE_YEAR_NAME_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/SultanateYearNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/HistoricSpiceGeneratedNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricSpiceGeneratedNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SultanateYearNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ISSUE737_HSE_AUDIT_EVIDENCE,
]
HSE_IMPORTED_FOOD_DRINK_FACTION_NAME_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/ImportedFoodOrDrinkFactionNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ImportedFoodOrDrinkFactionNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ImportedFoodOrDrinkFactionNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ImportedFoodOrDrinkFactionNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/Scoped/historyspice-common.ja.json",
    "Mods/QudJP/Localization/Dictionaries/world-gospels.ja.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
]
HSE_HISTORY_ITEM_NAME_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/QudHistoryHelpersItemNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/HistoricSpiceGeneratedNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricSpiceGeneratedNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/QudHistoryHelpersItemNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/Scoped/historyspice-common.ja.json",
    "Mods/QudJP/Localization/Dictionaries/world-gospels.ja.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    "source helper coverage for generated blessing item-name frames; suffix pseudo-names remain pass-through",
]
HSE_VILLAGE_PROVERB_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/AddVillageGospelsTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs",
    "Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs",
    "Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    (
        "storage-route coverage for VillageProverb proverb entity property plus focused "
        "proverbs/proverbsCoda template patterns"
    ),
]
HSE_VILLAGE_CODA_END_EVENT_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalEntryDisplayTextPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalEntryDisplayTextPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json",
    "Mods/QudJP/Localization/Dictionaries/Scoped/historyspice-common.ja.json",
    "scripts/_artifacts/annals/candidates_pending.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    (
        "display-route coverage for VillageCoda.GenerateEndEvent JournalSultanNote "
        "coda branch prose plus focused annals patterns"
    ),
]
HSE_QUD_HISTORY_FACTORY_GENERATED_NAME_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/QudHistoryFactoryNameRuinsSiteTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/QudHistoryFactoryGenerateCultNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/HistoricSpiceGeneratedNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricSpiceGeneratedNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/QudHistoryFactoryGeneratedNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/Scoped/historyspice-common.ja.json",
    "Mods/QudJP/Localization/Dictionaries/world-gospels.ja.json",
    ISSUE737_HSE_AUDIT_EVIDENCE,
    (
        "QudHistoryFactory storage/source-owner coverage for generated ruins-site modifier "
        "names and sultan cultName frames; proper roots and some forgotten ruins remain pass-through"
    ),
]
HSE_NAMESTYLE_XML_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Localization/Naming.jp.xml",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/NamingXmlTests.cs",
    "docs/reports/2026-05-17-historic-string-expander-owner-plan.md",
]
ISSUE747_CUDGEL_SLAM_ROUTE_EVIDENCE: Final = [
    f"Issue #747 reviewed skill-originated static family: {CUDGEL_SLAM_CAST_FAMILY_ID}",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    (
        "Cudgel_Slam.Cast owner popups/failures are translated for confirmed "
        "decompiled source strings; Cudgel_Slam.Slam message-frame traffic remains "
        "separate from this popup/failure route"
    ),
    "Issue #747 skill-originated Cudgel_Slam.Cast row is closed by exact owner-route evidence.",
]
ISSUE747_SHIELD_SLAM_ROUTE_EVIDENCE: Final = [
    f"Issue #747 reviewed skill-originated static family: {SHIELD_SLAM_SLAM_FAMILY_ID}",
    "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ("Shield_Slam.Slam owner message-log traffic covers the source-backed shield slam possessive capture"),
]
ISSUE747_SKILL_MESSAGE_FRAME_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    (
        "Issue #747 skill-originated message-frame and AddPlayerMessage rows are "
        "covered by source-backed owner routes plus MessageFrame verb/template leaves"
    ),
]
ISSUE747_SKILL_SINGLE_CALLSITE_POPUP_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ("Issue #747 source-backed single-callsite skill popups are translated by owner keys before generic popup sinks"),
]
ISSUE747_SKILL_REPAIR_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Issue #747 Tinkering_Repair popups/messages are translated at the repair owner route",
]
ISSUE747_SKILL_SURVIVAL_CAMP_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SurvivalCampAttemptCampPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Issue #747 Survival_Camp.AttemptCamp owner popups are translated by source-backed cases",
]
ISSUE747_SKILL_ASK_NUMBER_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskNumberTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Issue #747 Tinkering_Tinker1 recharge amount prompts are translated at the active owner route",
]
ISSUE747_SKILL_PHYSIC_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/PhysicAmputateLimbTranslationPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Issue #747 Physic_AmputateLimb field-amputation owner popups/messages are source-backed",
]
ISSUE747_SKILL_FIXED_POPUP_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/ShortBladesHobbleTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ShortBladesShankTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CudgelConkPopupTranslationPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Issue #747 fixed skill picker/failure leaves are translated only for stable source strings",
]
ISSUE747_SKILL_TINKERING_MINE_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Issue #747 Tinkering_Mine arm/disarm message rows are translated through message-owner frames",
]
ISSUE747_SKILL_LONG_BLADES_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/LongBladesCoreTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Issue #747 LongBladesCore stance popups/messages are translated by the LongBlades owner route",
]
ISSUE747_JOURNAL_QUEST_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalMapNoteAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalObservationAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/DynamicQuestGeneratedQuestTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalTextTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalEntryDisplayTextPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DynamicQuestGeneratedQuestTextTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json",
    (
        "Issue #747 JournalAPI rows are covered by owner routes at journal entry "
        "creation/display and dynamic quest text producers"
    ),
]
ISSUE747_JOURNAL_QUEST_MIXED_SURFACE_EVIDENCE: Final = [
    *ISSUE747_JOURNAL_QUEST_ROUTE_EVIDENCE,
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MessageLogPatchTests.cs",
    (
        "Issue #747 mixed JournalAPI/Popup/AddPlayerMessage families carry "
        "journal owner-route evidence plus popup/message sink-owner evidence for the same reviewed family"
    ),
]
ISSUE747_JOURNAL_QUEST_REVIEWED_FAMILY_IDS: Final = frozenset(
    {
        "HistoryKit/HistoricEvent.cs::HistoricEvent.PerformRegionReveal()",
        "XRL.Core/XRLCore.cs::XRLCore.CreateMarkOfDeath()",
        "XRL.Core/XRLCore.cs::XRLCore.PlayerTurn()",
        "XRL.Liquids/LiquidLava.cs::LiquidLava.Drank(LiquidVolume,int,GameObject,StringBuilder,ref bool)",
        "XRL.Liquids/LiquidSunSlag.cs::LiquidSunSlag.Drank(LiquidVolume,int,GameObject,StringBuilder,ref bool)",
        "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.GlitchMutations(GameObject)",
        "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.GlitchSkills(GameObject)",
        (
            "XRL.Liquids/LiquidWarmStatic.cs::"
            "LiquidWarmStatic.PourIntoCell(LiquidVolume,GameObject,Cell,ref int,bool,ref bool)"
        ),
        "XRL.UI/JournalScreen.cs::JournalScreen.HandleInsert(string,GameObject)",
        "XRL.UI/JournalScreen.cs::JournalScreen.Show(GameObject)",
        "XRL.World.Anatomy/BodyPart.cs::BodyPart.Implant(GameObject,bool,bool)",
        "XRL.World.Capabilities/PsychicGlimmer.cs::PsychicGlimmer.Update(GameObject)",
        "XRL.World.Conversations.Parts/ChavvahAttune.cs::ChavvahAttune.HandleEvent(GetTargetElementEvent)",
        "XRL.World.Conversations.Parts/PaxInfectLimb.cs::PaxInfectLimb.InfectLimb(List<BodyPart>,BodyPart,string)",
        "XRL.World.Conversations.Parts/WaterRitual.cs::WaterRitual.AddAccomplishment()",
        "XRL.World.Effects/CookingDomainAttributes_UnitPermanentAllStats_25Percent.cs::CookingDomainAttributes_UnitPermanentAllStats_25Percent.Apply(GameObject,Effect)",
        "XRL.World.Effects/CookingDomainDensity_UnitPermanentAV.cs::CookingDomainDensity_UnitPermanentAV.Apply(GameObject,Effect)",
        "XRL.World.Effects/CookingDomainSpecial_UnitCrystalTransform.cs::CookingDomainSpecial_UnitCrystalTransform.ApplyTo(GameObject)",
        "XRL.World.Effects/CookingDomainSpecial_UnitSlogTransform.cs::CookingDomainSpecial_UnitSlogTransform.ApplyTo(GameObject)",
        "XRL.World.Effects/DeepDream.cs::DeepDream.Crungle()",
        "XRL.World.Effects/FungalSporeInfection.cs::FungalSporeInfection.ApplyFungalInfection(GameObject,string,BodyPart)",
        "XRL.World.Effects/Glotrot.cs::Glotrot.AdvanceGlotrot(int)",
        "XRL.World.Effects/Glotrot.cs::Glotrot.Apply(GameObject)",
        "XRL.World.Effects/Glotrot.cs::Glotrot.RegrowTongue()",
        "XRL.World.Effects/Ironshank.cs::Ironshank.Apply(GameObject)",
        "XRL.World.Effects/Ironshank.cs::Ironshank.FireEvent(Event)",
        "XRL.World.Effects/Lovesick.cs::Lovesick.Remove(GameObject)",
        "XRL.World.Effects/Monochrome.cs::Monochrome.Apply(GameObject)",
        "XRL.World.Effects/Monochrome.cs::Monochrome.FireEvent(Event)",
        "XRL.World.Effects/MutationInfection.cs::MutationInfection.FireEvent(Event)",
        "XRL.World.Effects/WakingDream.cs::WakingDream.Remove(GameObject)",
        "XRL.World.Parts.Mutation/Domination.cs::Domination.Metempsychosis(GameObject,bool)",
        "XRL.World.Parts/AbsorbablePsyche.cs::AbsorbablePsyche.HandleEvent(BeforeDeathRemovalEvent)",
        "XRL.World.Parts/BeyLahSurface.cs::BeyLahSurface.FireEvent(Event)",
        "XRL.World.Parts/Book.cs::Book.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts/Cookbook.cs::Cookbook.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts/DromadCaravan.cs::DromadCaravan.Render(RenderEvent)",
        "XRL.World.Parts/EatenAccomplishment.cs::EatenAccomplishment.HandleEvent(AfterInventoryActionEvent)",
        "XRL.World.Parts/FungalInfection.cs::FungalInfection.Cure()",
        "XRL.World.Parts/GiantClamProperties.cs::GiantClamProperties.TeleportToClamWorld(GameObject)",
        "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.Place(GameObject)",
        "XRL.World.Parts/HydroponSurface.cs::HydroponSurface.FireEvent(Event)",
        "XRL.World.Parts/LocationFinder.cs::LocationFinder.TriggerFind()",
        "XRL.World.Parts/MarkovBook.cs::MarkovBook.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts/PointedAsteriskBuilder.cs::PointedAsteriskBuilder.HandleEvent(AfterObjectCreatedEvent)",
        "XRL.World.Parts/RandomAltarBaetyl.cs::RandomAltarBaetyl.BaetylWantsSacrifice()",
        "XRL.World.Parts/RandomAltarBaetyl.cs::RandomAltarBaetyl.UpdateJournalNote()",
        "XRL.World.Parts/SkrefCorpseLoot.cs::SkrefCorpseLoot.Render(RenderEvent)",
        "XRL.World.Parts/SoupSludge.cs::SoupSludge.CatalyzeMessage(string,string)",
        "XRL.World.Parts/StatOnEat.cs::StatOnEat.FireEvent(Event)",
        "XRL.World.Parts/SultanRegionSurface.cs::SultanRegionSurface.FireEvent(Event)",
        "XRL.World.Parts/TakenAccomplishment.cs::TakenAccomplishment.Trigger(GameObject)",
        "XRL.World.Parts/ThinWorld.cs::ThinWorld.CrossIntoBrightsheol()",
        "XRL.World.Parts/ThinWorld.cs::ThinWorld.ReturnToQud()",
        "XRL.World.Parts/ThinWorld.cs::ThinWorld.RunThinWorldIntroSequence(bool)",
        "XRL.World.Parts/ThinWorld.cs::ThinWorld.TransitToThinWorld(GameObject,bool)",
        "XRL.World.QuestManagers/SpreadPax.cs::SpreadPax.Finish()",
        "XRL.World.Quests.GolemQuest/GolemIncantationSelection.cs::GolemIncantationSelection.CreateAccomplishments()",
        "XRL.World.Quests/LandingPadsSystem.cs::LandingPadsSystem.Finish()",
        "XRL.World.ZoneBuilders/ChildrenOfTheTombQuestHandler.cs::ChildrenOfTheTombQuestHandler.AddAccomplishment(string)",
        "XRL.World/KithAndKinGameState.cs::KithAndKinGameState.initItemClue(string,string,string,string,string)",
        "XRL.World/KithAndKinGameState.cs::KithAndKinGameState.initLookClue(string,string,string,string,string,string)",
        "XRL.World/KithAndKinGameState.cs::KithAndKinGameState.initRumorClue(string,string,string,string,string,string)",
        "XRL/ChavvahSystem.cs::ChavvahSystem.Reveal(bool)",
        "XRL/XRLGame.cs::XRLGame.FinishQuest(Quest)",
    }
)
ISSUE747_SKILL_REVIEWED_FAMILY_IDS: Final = frozenset(
    {
        "XRL.World.Parts.Skill/Acrobatics_Jump.cs::Acrobatics_Jump.Jump(GameObject,int,Cell,string)",
        "XRL.World.Parts.Skill/Axe_Berserk.cs::Axe_Berserk.FireEvent(Event)",
        "XRL.World.Parts.Skill/Axe_Cleave.cs::Axe_Cleave.PerformCleave(GameObject,GameObject,GameObject,string,string,int,int,int?)",
        "XRL.World.Parts.Skill/Axe_Decapitate.cs::Axe_Decapitate.Decapitate(GameObject,GameObject,Cell,BodyPart,GameObject,GameObject,bool,bool)",
        "XRL.World.Parts.Skill/Axe_Dismember.cs::Axe_Dismember.Cast(GameObject,Axe_Dismember,GameObject)",
        "XRL.World.Parts.Skill/Axe_Dismember.cs::Axe_Dismember.CastForceSuccess(GameObject,Axe_Dismember,GameObject)",
        "XRL.World.Parts.Skill/Axe_Dismember.cs::Axe_Dismember.Dismember(GameObject,GameObject,Cell,BodyPart,GameObject,GameObject,string,bool,bool,bool,bool)",
        "XRL.World.Parts.Skill/Axe_HookAndDrag.cs::Axe_HookAndDrag.FireEvent(Event)",
        "XRL.World.Parts.Skill/BaseInitiatorySkill.cs::BaseInitiatorySkill.GetCompletedText(GameObject,GameObject,SkillEntry,string)",
        "XRL.World.Parts.Skill/BaseInitiatorySkill.cs::BaseInitiatorySkill.GetExpendedText(GameObject,GameObject,SkillEntry,string)",
        "XRL.World.Parts.Skill/BaseSkill.cs::BaseSkill.ShowAddPopup(BeforeAddSkillEvent)",
        "XRL.World.Parts.Skill/Cudgel_Backswing.cs::Cudgel_Backswing.FireEvent(Event)",
        "XRL.World.Parts.Skill/Cudgel_Bludgeon.cs::Cudgel_Bludgeon.FireEvent(Event)",
        "XRL.World.Parts.Skill/Cudgel_Conk.cs::Cudgel_Conk.PerformConk()",
        "XRL.World.Parts.Skill/Cudgel_Slam.cs::Cudgel_Slam.Cast(GameObject,Cudgel_Slam,string,GameObject,bool,int,string)",
        "XRL.World.Parts.Skill/Cudgel_Slam.cs::Cudgel_Slam.Slam(GameObject,string,int,int,int,Dictionary<GameObject,string>)",
        "XRL.World.Parts.Skill/Cudgel_SmashUp.cs::Cudgel_SmashUp.FireEvent(Event)",
        "XRL.World.Parts.Skill/Discipline_IronMind.cs::Discipline_IronMind.FireEvent(Event)",
        "XRL.World.Parts.Skill/Discipline_Meditate.cs::Discipline_Meditate.HandleEvent(CommandEvent)",
        "XRL.World.Parts.Skill/Endurance_ShakeItOff.cs::Endurance_ShakeItOff.FireEvent(Event)",
        "XRL.World.Parts.Skill/Endurance_ShakeItOff.cs::Endurance_ShakeItOff.TryToShakeItOff()",
        "XRL.World.Parts.Skill/Multiweapon_Flurry.cs::Multiweapon_Flurry.PerformFlurry()",
        "XRL.World.Parts.Skill/Persuasion_Berate.cs::Persuasion_Berate.ApplyBerate(GameObject,Cell,bool?,object)",
        "XRL.World.Parts.Skill/Persuasion_Intimidate.cs::Persuasion_Intimidate.ApplyIntimidate(Cell,GameObject,bool)",
        "XRL.World.Parts.Skill/Persuasion_Intimidate.cs::Persuasion_Intimidate.Terrify(MentalAttackEvent)",
        "XRL.World.Parts.Skill/Persuasion_MenacingStare.cs::Persuasion_MenacingStare.ApplyStare(GameObject,Cell,string,int)",
        "XRL.World.Parts.Skill/Persuasion_Proselytize.cs::Persuasion_Proselytize.AttemptProselytization()",
        "XRL.World.Parts.Skill/Persuasion_Proselytize.cs::Persuasion_Proselytize.Proselytize(MentalAttackEvent)",
        "XRL.World.Parts.Skill/Persuasion_RebukeRobot.cs::Persuasion_RebukeRobot.AttemptRebuke()",
        "XRL.World.Parts.Skill/Persuasion_RebukeRobot.cs::Persuasion_RebukeRobot.Rebuke(MentalAttackEvent)",
        "XRL.World.Parts.Skill/Physic_AmputateLimb.cs::Physic_AmputateLimb.FireEvent(Event)",
        "XRL.World.Parts.Skill/Rifle_DrawABead.cs::Rifle_DrawABead.SetMark(GameObject)",
        "XRL.World.Parts.Skill/Rifle_DrawABead.cs::Rifle_DrawABead.ValidateMark()",
        "XRL.World.Parts.Skill/Rifle_SuppressiveFire.cs::Rifle_SuppressiveFire.FireEvent(Event)",
        "XRL.World.Parts.Skill/Rifle_WoundingFire.cs::Rifle_WoundingFire.FireEvent(Event)",
        "XRL.World.Parts.Skill/Shield_Slam.cs::Shield_Slam.Slam(GameObject,GameObject,Cell,bool)",
        "XRL.World.Parts.Skill/ShortBlades_Hobble.cs::ShortBlades_Hobble.FireEvent(Event)",
        "XRL.World.Parts.Skill/ShortBlades_Rejoinder.cs::ShortBlades_Rejoinder.FireEvent(Event)",
        "XRL.World.Parts.Skill/ShortBlades_Shank.cs::ShortBlades_Shank.Cast(GameObject,ShortBlades_Shank,GameObject)",
        "XRL.World.Parts.Skill/ShortBlades_Shank.cs::ShortBlades_Shank.FireEvent(Event)",
        "XRL.World.Parts.Skill/SingleWeaponFighting_OpportuneAttacks.cs::SingleWeaponFighting_OpportuneAttacks.Refresh(int)",
        "XRL.World.Parts.Skill/Smash_Floor.cs::Smash_Floor.FireEvent(Event)",
        "XRL.World.Parts.Skill/Snapjaw_Howl.cs::Snapjaw_Howl.FireEvent(Event)",
        "XRL.World.Parts.Skill/Submersion.cs::Submersion.HandleEvent(CommandEvent)",
        "XRL.World.Parts.Skill/Survival_Camp.cs::Survival_Camp.AttemptCamp(GameObject)",
        "XRL.World.Parts.Skill/Tactics_Charge.cs::Tactics_Charge.PerformCharge()",
        "XRL.World.Parts.Skill/Tactics_DeathFromAbove.cs::Tactics_DeathFromAbove.PerformDeathFromAbove(GameObject,GameObject,string)",
        "XRL.World.Parts.Skill/Tactics_Juke.cs::Tactics_Juke.HandleEvent(CommandEvent)",
        "XRL.World.Parts.Skill/Tactics_Kickback.cs::Tactics_Kickback.HandleEvent(BeforeFireMissileWeaponsEvent)",
        "XRL.World.Parts.Skill/TenfoldPath_Ket.cs::TenfoldPath_Ket.HandleEvent(BeforeDieEvent)",
        "XRL.World.Parts.Skill/TenfoldPath_Ret.cs::TenfoldPath_Ret.HandleEvent(ApplyEffectEvent)",
        "XRL.World.Parts.Skill/TenfoldPath_Ret.cs::TenfoldPath_Ret.HandleEvent(EndTurnEvent)",
        "XRL.World.Parts.Skill/TenfoldPath_Vur.cs::TenfoldPath_Vur.FireEvent(Event)",
        "XRL.World.Parts.Skill/TenfoldPath_Yis.cs::TenfoldPath_Yis.AddSkill(GameObject)",
        "XRL.World.Parts.Skill/Tinkering.cs::Tinkering.LearnNewRecipe(GameObject,int,int)",
        "XRL.World.Parts.Skill/Tinkering_DeployTurret.cs::Tinkering_DeployTurret.FireEvent(Event)",
        "XRL.World.Parts.Skill/Tinkering_LayMine.cs::Tinkering_LayMine.AttemptLayMine(bool)",
        "XRL.World.Parts.Skill/Tinkering_Repair.cs::Tinkering_Repair.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts.Skill/Tinkering_Repair.cs::Tinkering_Repair.RepairResultCriticalFailure(GameObject,GameObject)",
        "XRL.World.Parts.Skill/Tinkering_Repair.cs::Tinkering_Repair.RepairResultExceptionalSuccess(GameObject,GameObject)",
        "XRL.World.Parts.Skill/Tinkering_Repair.cs::Tinkering_Repair.RepairResultFailure(GameObject,GameObject)",
        "XRL.World.Parts.Skill/Tinkering_Repair.cs::Tinkering_Repair.RepairResultPartialSuccess(GameObject,GameObject)",
        "XRL.World.Parts.Skill/Tinkering_Repair.cs::Tinkering_Repair.RepairResultSuccess(GameObject,GameObject)",
        "XRL.World.Parts.Skill/Tinkering_Tinker1.cs::Tinkering_Tinker1.FireEvent(Event)",
        "XRL.World.Parts.Skill/Tinkering_Tinker1.cs::Tinkering_Tinker1.Recharge(GameObject,IEvent)",
        "XRL.World.Parts/LongBladesCore.cs::LongBladesCore.ChangeStance(string)",
        "XRL.World.Parts/LongBladesCore.cs::LongBladesCore.FireEvent(Event)",
        "XRL.World.Parts/Tinkering_Mine.cs::Tinkering_Mine.AttemptArm(GameObject)",
        "XRL.World.Parts/Tinkering_Mine.cs::Tinkering_Mine.AttemptDisarm(GameObject,IEvent,bool)",
        "XRL.World.Parts/Tinkering_Mine.cs::Tinkering_Mine.DisarmingResultExceptionalSuccess(GameObject,GameObject,bool)",
        "XRL.World.Parts/Tinkering_Mine.cs::Tinkering_Mine.DisarmingResultPartialSuccess(GameObject,GameObject,bool)",
        "XRL.World.Parts/Tinkering_Mine.cs::Tinkering_Mine.DisarmingResultSuccess(GameObject,GameObject,bool)",
    }
)
ISSUE747_SKILL_EXACT_FAMILY_EVIDENCE: Final = {
    CUDGEL_SLAM_CAST_FAMILY_ID: ISSUE747_CUDGEL_SLAM_ROUTE_EVIDENCE,
    SHIELD_SLAM_SLAM_FAMILY_ID: ISSUE747_SHIELD_SLAM_ROUTE_EVIDENCE,
}
ISSUE747_SKILL_MARKER_ROUTE_EVIDENCE: Final = (
    (("Physic_AmputateLimb.cs",), ISSUE747_SKILL_PHYSIC_ROUTE_EVIDENCE),
    (("Survival_Camp.cs",), ISSUE747_SKILL_SURVIVAL_CAMP_ROUTE_EVIDENCE),
    (("Tinkering_Repair.cs",), ISSUE747_SKILL_REPAIR_ROUTE_EVIDENCE),
    (("Tinkering_Tinker1.cs::Tinkering_Tinker1.Recharge",), ISSUE747_SKILL_ASK_NUMBER_ROUTE_EVIDENCE),
    (("LongBladesCore.cs",), ISSUE747_SKILL_LONG_BLADES_ROUTE_EVIDENCE),
    (("Tinkering_Mine.cs",), ISSUE747_SKILL_TINKERING_MINE_ROUTE_EVIDENCE),
    (
        (
            "Axe_Dismember.cs::Axe_Dismember.Cast",
            "Axe_HookAndDrag.cs",
            "Cudgel_Slam.cs::Cudgel_Slam.Cast",
            "Cudgel_SmashUp.cs",
            "Discipline_Meditate.cs",
            "Persuasion_Proselytize.cs",
            "Rifle_SuppressiveFire.cs",
            "Rifle_WoundingFire.cs",
            "ShortBlades_Shank.cs",
            "Submersion.cs",
            "Tactics_Charge.cs",
            "Tactics_DeathFromAbove.cs",
            "Tactics_Juke.cs",
            "Tinkering.cs::Tinkering.LearnNewRecipe",
            "Tinkering_DeployTurret.cs",
            "Tinkering_LayMine.cs",
            "Tinkering_Tinker1.cs::Tinkering_Tinker1.FireEvent",
            "TenfoldPath_Yis.cs",
        ),
        ISSUE747_SKILL_SINGLE_CALLSITE_POPUP_ROUTE_EVIDENCE,
    ),
    (
        (
            "BaseSkill.cs",
            "Cudgel_Conk.cs",
            "ShortBlades_Hobble.cs",
        ),
        ISSUE747_SKILL_FIXED_POPUP_ROUTE_EVIDENCE,
    ),
)
ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact mutation command families whose "
        "visible DidX/Fail/Popup shapes are already served by the existing "
        "MessageFrame, popup, and message-log routes with concrete dictionary keys."
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
]
ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact fixed DidX/XDidYToZ message-frame "
        "families whose verb/extra shapes are already served by the existing "
        "XDidY/MessageFrame route and concrete MessageFrames dictionary keys."
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_HIDDEN_REVEAL_INTERNAL_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers Hidden.RevealInternal(bool) "
        "through HiddenRenderTranslationPatch owner queue/message-log routing."
    ),
    "Mods/QudJP/Assemblies/src/Patches/HiddenRenderTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HiddenRenderTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_TRANCHE38_RUNNING_REMOVE_EVIDENCE: Final[list[str]] = [
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    'Running.Remove source frame: DidX("stop", MessageName)',
    "MessageFrame key: verb=stop extra=power skating",
    "MessageFrame key: verb=stop extra=sprinting",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesTranche38ActiveEffectFrames"
    ),
]
ISSUE719_TRANCHE38_RESUMMON_GLOAMING_EVIDENCE: Final[list[str]] = [
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    'ResummonGloaming.HandleEvent source frame: XDidY(gameObject, "reappear")',
    "MessageFrame key: verb=reappear extra=<none>",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesTranche38ActiveEffectFrames"
    ),
]
ISSUE719_TRANCHE38_ARTIFACT_IDENTIFY_EVIDENCE: Final[list[str]] = [
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    (
        "CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.Apply "
        'source frame: XDidYToZ(go, "flush", "with understanding of", target)'
    ),
    "MessageFrame key: verb=flush extra=with understanding of {0}",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesTranche38ActiveEffectFrames"
    ),
]
ISSUE719_TRANCHE39_LIFE_DRAIN_APPLY_EVIDENCE: Final[list[str]] = [
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    'LifeDrain.Apply source frame: XDidYToZ(Drainer, "bond", "with", Object)',
    'LifeDrain.Apply source frame: XDidYToZ(Drainer, "begin", "to drain life essence from", Object)',
    "MessageFrame key: verb=bond extra=with {0}",
    "MessageFrame key: verb=begin extra=to drain life essence from {0}",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesTranche39ActiveEffectFrames"
    ),
]
ISSUE719_TRANCHE39_LIFE_DRAIN_INVENTORY_EVIDENCE: Final[list[str]] = [
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    (
        'LifeDrain.HandleEvent(InventoryActionEvent) source frame: XDidYToZ(E.Actor, "release", '
        'base.Object, "from " + E.Actor.its + " life drain", UsePopup: true)'
    ),
    "MessageFrame key: verb=release extra={0} from {1} life drain",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesTranche39ActiveEffectFrames"
    ),
]
ISSUE719_TRANCHE39_BLEEDING_START_EVIDENCE: Final[list[str]] = [
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    'Bleeding.StartMessage source frame: DidX("begin", DisplayNameStripped)',
    'Bleeding.StartMessage source frame: DidX("begin", DisplayNameStripped + " from another wound")',
    "Existing generic circulatory MessageFrame templates cover the bleeding-start display-name tails.",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesTranche39ActiveEffectFrames"
    ),
]
ISSUE719_TRANCHE40_BEGUILED_REMOVE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Beguiled.Remove because the fixed "
        "DidXToY lose-interest shape routes through the existing XDidYToZ/"
        "MessageFrame path with a concrete dictionary key."
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    'Beguiled.Remove source frame: DidXToY("lose", "interest in", Beguiler)',
    "IComponent.DidXToY overload delegates to Messaging.XDidYToZ.",
    "MessageFrame key: verb=lose extra=interest in {0}",
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidYToZ_RepositoryDictionary_"
        "TranslatesTranche40BeguiledLoseInterestFrame"
    ),
]
ISSUE719_TRANCHE40_CONFUSED_CONVERSATION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Confused conversation responsiveness "
        "messages through the existing ConversationScript owner popup route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ConversationScriptPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationScriptPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "XRL.World.Events/IsConversationallyResponsiveEvent.cs",
    "XRL.World.Parts/ConversationScript.cs",
    'Confused conversation source: Does("don\'t") + " seem to understand you."',
    'Confused mental source: Poss("mind") + " is in disarray."',
    "ConversationScriptPopupTranslationPatch detail: DoesNotUnderstand",
    "ConversationScriptPopupTranslationPatch detail: MindInDisarray",
]
ISSUE719_TRANCHE40_DOMINATING_CONVERSATION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Dominating conversation responsiveness "
        "messages through the existing ConversationScript owner popup route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ConversationScriptPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationScriptPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "XRL.World.Events/IsConversationallyResponsiveEvent.cs",
    "XRL.World.Parts/ConversationScript.cs",
    'Dominating conversation source: Does("are") + " utterly unresponsive."',
    'Dominating mental source: Poss("mind") + " seems to be elsewhere."',
    "ConversationScriptPopupTranslationPatch detail: UtterlyUnresponsive",
    "ConversationScriptPopupTranslationPatch detail: MindElsewhere",
]
ISSUE719_TRANCHE41_ACTIVE_EFFECT_DIDX_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 tranche 41 closes exact active-effect DidX message-frame "
        "families through a method-scoped owner patch, L2 owner-scope "
        "translation tests, and L2G target-method resolution."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectMessageFrameOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectMessageFrameOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    'Immobilized.Apply source frame: DidX("are", Text)',
    'Stuck.Apply source frame: DidX("are", DisplayName)',
    'LatchedOnto.HandleEvent(BeginTakeActionEvent) source frame: DidX("break", "free from " + text)',
    "MessageFrame key: verb=are extra=immobilized",
    "MessageFrame key: verb=are extra=stuck",
    "MessageFrame key: verb=are extra=stuck in {0}",
    "MessageFrame key: verb=are extra=grabbed by {0}",
    "MessageFrame key: verb=break extra=free from {0}",
    (
        "L2 owner-route coverage: "
        "ActiveEffectMessageFrameOwnerTranslationPatchTests.OwnerPatch_"
        "RecordsMessageFrameTranslation_WhenActiveEffectOwnerIsPatched"
    ),
    ("L2G target coverage: TargetMethodResolutionTests.TargetMethods_ResolveExpectedOverloads"),
]
ISSUE719_TRANCHE42_SOCIAL_ACTIVE_EFFECT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 tranche 42 closes social active-effect Apply families through "
        "method-scoped MessageFrame owner evidence plus JournalAPI storage-time "
        "patterns for each AddAccomplishment text/mural/gospel argument."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectMessageFrameOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectMessageFrameOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json",
    'Lovesick.Apply source frame: DidXToY("fall", "in love with", Beauty)',
    'Beguiled.Apply source frame: XDidYToZ(Object, "ogle", Beguiler, "lovingly")',
    'Proselytized.Apply source frame: XDidYToZ(Proselytizer, "convince", Object, "to join " + Proselytizer.them)',
    'Rebuked.Apply source frame: XDidYToZ(Rebuker, "rebuke", Object, "into submission")',
    "JournalAPI.AddAccomplishment social text/mural/gospel patterns covered from assets.",
    (
        "Rebuked.Apply HSE mural is covered after expansion by the "
        "JournalAPI storage route; HistoricStringExpanderPatch remains disabled."
    ),
    (
        "L1 dictionary coverage: "
        "MessageFrameTranslatorTests.TryTranslateXDidYToZ_RepositoryDictionary_"
        "TranslatesTranche42SocialActiveEffectFrames"
    ),
    (
        "L1 journal coverage: "
        "JournalPatternTranslatorTests.Translate_AppliesTranche42SocialActiveEffectAccomplishmentPatterns_FromAssets"
    ),
    (
        "L2 JournalAPI coverage: "
        "JournalApiAddTranslationPatchTests.AddAccomplishment_"
        "TranslatesTranche42SocialActiveEffectVariants_FromAssets_WhenPatched"
    ),
    (
        "L2 owner-route coverage: "
        "ActiveEffectMessageFrameOwnerTranslationPatchTests.OwnerPatch_"
        "RecordsXDidYToZMessageFrameTranslation_WhenSocialActiveEffectOwnerIsPatched"
    ),
    ("L2G target coverage: TargetMethodResolutionTests.TargetMethods_ResolveExpectedOverloads"),
]
ISSUE719_TRANCHE43_CARDIAC_ARREST_REMOVE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 tranche 43 closes CardiacArrest.Remove by keeping its "
        "non-player DidX frame under the method-scoped active-effect owner "
        "route and adding exact owner-scope coverage for the player restart "
        "popups plus nested Ill.Apply popup message."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectMessageFrameOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectMessageFrameOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    'CardiacArrest.Remove player popup: Popup.Show("{{G|Your heart restarts!}}")',
    'CardiacArrest.Remove player popup: Popup.Show("{{G|Your hearts restart!}}")',
    'CardiacArrest.Remove non-player source frame: DidX("look", "less stricken")',
    'CardiacArrest.Remove nested Ill.Apply popup source: "You feel shaken and infirm."',
    (
        "L2 owner-route coverage: "
        "ActiveEffectMessageFrameOwnerTranslationPatchTests.OwnerPatch_"
        "RecordsCardiacArrestRemovePlayerPopupTranslations_WhenOwnerIsPatched"
    ),
    (
        "L2 popup dictionary coverage: "
        "PopupTranslationPatchTests.TranslatePopupText_RepositoryDictionary_"
        "TranslatesTranche41CardiacArrestRestartPopups"
    ),
    ("L2G target coverage: TargetMethodResolutionTests.TargetMethods_ResolveExpectedOverloads"),
]
ISSUE719_DEPLOYMENT_GRENADE_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes DeploymentGrenade.DoDetonate because "
        "the ActivationVerb blueprint values are finite detonate/deploy verbs "
        "already served by the existing XDidY/MessageFrame route."
    ),
    *ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE[1:],
    "Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml",
]
ISSUE719_FORCE_PROJECTOR_UNRESPONSIVE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes ForceProjector activation and "
        "deactivation fixed unresponsive ShowFailure branches through the "
        "existing Does marker and popup translation route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbRouteTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact fixed EmitMessage pattern "
        "families already served by GameObjectEmitMessageTranslationPatch and "
        "MessagePatternTranslator dictionary tests."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameObjectEmitMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
]
ISSUE719_FIXED_DOES_MESSAGE_PATTERN_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact fixed Does message families "
        "already served by Does fragment marking and the message-log pattern route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact pure Does families served by "
        "Does fragment/plain-sentence routing and concrete MessageFrame verb leaves."
    ),
    (
        "reviewed source frames: active-part status/failure messages, stasis "
        "entangler/arena status, glass-armor reflect damage, vomiting, ammo-loader "
        "status, floating equipment status/drop messages, conversation busy/nothing-to-say, "
        "AIWiring ignore, and Templar phylactery hacked/unresponsive messages"
    ),
    "Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbRouteTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_BLEEDING_STOP_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Bleeding.StopMessage because the "
        "plural wound EmitMessage pattern and singular circulatory-loss DidX "
        "frame are both already covered by existing message pattern/frame routes."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameObjectEmitMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes fixed producer MessageFrame families "
        "served by the repository XDidY/XDidYToZ route and MessageFrame verb dictionary."
    ),
    "source frames: GeomagneticDisc pass through/flinch out of the way, Leveler gain a level, "
    "CryptFerret filch, matter recompositer teleport, PlaceTurret place, GasGeneration start/stop releasing",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes combat MessageFrame producer families "
        "served by the repository XDidY/XDidYToZ/WDidXToYWithZ route and "
        "MessageFrame verb dictionary."
    ),
    "source frames: PointDefense intercept/pass-through/no-effect, GreaterVoider teleport-to-lair, "
    "RunOver charge/run-over/stopped-in-tracks, AjiConch blow conch, Disarming disarm-of, "
    "EngulfingClones refract/try-to-refract, Fan blown-back variants, HookOnMissileHit dragged-toward",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes physical MessageFrame producer families "
        "served by the repository XDidY/XDidYToZ/WDidXToYWithZ route and "
        "MessageFrame verb dictionary."
    ),
    "source frames: SunderMind no-effect sunder attempt, Physics knock/collide, "
    "Butcherable success/failure frames, PluckablePolyp pluck/reveal, Interior enter-denial messages, "
    "CyberneticsStasisProjector stasis field projection, TimeDilation distort-around, SwapOnHit position swap",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes environmental and utility MessageFrame "
        "producer families served by the repository XDidY/XDidYToZ route and "
        "MessageFrame verb dictionary."
    ),
    "source frames: LayMine place, BurgeonOnHit germinate, BurnOffGas burn-off, "
    "GrabberArm grab-and-hold, Ironshroom impale, DropOnDamage drop, Sweeper consume, "
    "PetPhylactery/TemplarPhylactery activate/appear, ReflectShame shame reflection, "
    "EelSpawn spot sewage eel, EjectionSeat eject, DiThermoBeam flip polarity, "
    "StickyOnHit entangle, Tonic accidentally prick",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes utility and mutation MessageFrame "
        "producer families served by the repository XDidY/XDidYToZ/WDidXToYWithZ "
        "route and MessageFrame verb dictionary."
    ),
    "source frames: EnergyCellSocket remove/pop cell, Domination take-control/prevent/resist, "
    "SlipRing slip-away, LavaSludge cool-to-shale, NoStandUp stand/try-stand, "
    "StairsDown locked stairs, Thurible incense, Disintegration air-vibrates, "
    "Metamorphed revert, BlinkOnDamage blink-away, Interdiction lock-onto, QuantumFugue cohere, "
    "SapOnPenetration stat drain",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes fixed pure MessageFrame families "
        "served by the repository XDidY/XDidYToZ route and concrete "
        "MessageFrame dictionary keys."
    ),
    (
        "source frames: FeelingOnTarget calm decision, TimeDilation distort-around, "
        "Chair stand up, IrisdualBeam damage-from, EngulfingHandOff hand-off, "
        "IStingerProperties venom resist, ReflectProjectiles shield activation/deactivation, "
        "RunOver charge/run-over/stopped-in-tracks, SkybearShroud dash, Banner raise, "
        "CooldownOnStep neuronal thorns, Cybernetics cathedra black/white activation, "
        "IfThenElseQuestWidget disappear, PsychicMeridian psychic barbs"
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact fixed-popup families whose "
        "player-visible Popup/Show/ShowFail/ShowYesNo strings are already "
        "present in the ui-popup dictionary and served by the existing generic "
        "popup route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSpaceTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowSpaceTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_SINGLE_CALLSITE_POPUP_EXACT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact SingleCallsiteOwnerPopup families "
        "whose popup strings are already served by resolved owner targets and "
        "ui-popup dictionary entries."
    ),
    "owner targets: XRL.XRLGame|LoadGame, XRL.World.Parts.Food|HandleEvent, "
    "XRL.World.Parts.Container|AttemptOpen, XRL.PopulationManager|WishGenerate",
    "patterns: XrlGameMissingSave, FoodConsumptionFrame, ContainerCannotTrade, "
    "ContainerEmptyStore, PopulationManagerInvalidCount, PopulationManagerMissingTable",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_RESIDUAL_LIFE_DRAIN_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes LifeDrain.FireEvent popups through "
        "the exact mutation owner route, including invalid target and no-target "
        "ShowFail branches from the decompiled producer."
    ),
    "owner target: XRL.World.Parts.Mutation.LifeDrain|FireEvent",
    "patterns: LifeDrainInvalidTarget, LifeDrainNoTarget",
    "Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationGeneratedTextTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_RESIDUAL_WISH_MUTATION_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Mutations.WishMutation popups through "
        "the exact SingleCallsiteOwnerPopup route for did-you-mean, missing-name, "
        "and missing-variant branches from the decompiled producer."
    ),
    "owner target: XRL.World.Parts.Mutations|WishMutation",
    "patterns: MutationWishDidYouMean, MutationWishMissingName, MutationWishMissingVariant",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_RESIDUAL_WATER_RITUAL_RANDOM_MUTATION_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes WaterRitualRandomMutation.HandleEvent "
        "popups through the exact water-ritual owner route, including non-mutant, "
        "physical-incompatible, mental-incompatible, and generated grant-message branches."
    ),
    "owner target: XRL.World.Conversations.Parts.WaterRitualRandomMutation|HandleEvent",
    "patterns: RandomMutationNonMutant, RandomMutationIncompatible, GainMutation",
    "Mods/QudJP/Assemblies/src/Patches/WaterRitualPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_FIXED_PRODUCER_POPUP_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes fixed producer popup surfaces whose "
        "visible Popup/PickOption/ShowSpace text is already served by shipped "
        "ui-popup dictionary entries and existing generic popup routes."
    ),
    "reviewed source popups: EndGame.PickState fixed PickOption title/options; "
    "PronounAndGenderSets fixed discard/no-base-gender popups; "
    "CheckpointingSystem death menu fixed options and retire prompt",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSpaceTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowSpaceTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_GAMEOBJECT_AUTOEQUIP_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes GameObject.AutoEquip because the "
        "method's direct player-visible Popup.ShowFail leaves are the fixed "
        "AutoEquip ammunition failure string; broad auto-equip failure text is "
        "delegated to GameObject.AutoEquipFail outside this family."
    ),
    (
        "AutoEquip fixed ammunition ShowFail branch: "
        '"You don\'t have a missile weapon equipped that uses that ammunition."'
    ),
    "decompiled source: XRL.World/GameObject.cs AutoEquip Ammo branch",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_GAMEOBJECT_INVENTORY_COMPANION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes GameObject.HandleInventoryActionEvent "
        "companion follow-distance popup through the exact GameObject owner "
        "route; non-popup action/id/sound strings in the broad family are not "
        "player-visible localization leaves."
    ),
    (
        "owner target: XRL.World.GameObject|HandleInventoryActionEvent|"
        "System.Boolean|XRL.World.InventoryActionEvent"
    ),
    (
        "translated popup payload: Instruct {companion} to follow at what "
        "distance?, close, medium, far"
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_GAMEOBJECT_PULLDOWN_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes GameObject.PullDown alternate "
        "destination pick-option text through the exact GameObject owner route; "
        "dynamic map-note labels remain source-owned and are preserved."
    ),
    (
        "owner target: XRL.World.GameObject|PullDown|"
        "System.Void|System.Boolean"
    ),
    (
        "translated popup payload: Select a destination, Current location, "
        "Arrival location, Center; map-note text and direction suffixes are "
        "preserved."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_FIREFIGHTING_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Firefighting.AttemptFirefightingCore "
        "through the existing owner popup patch, generic fixed popup dictionary "
        "route, and MessageFrame verb/extra dictionaries for rolling and beating "
        "at flames."
    ),
    (
        "owner target: XRL.World.Capabilities.Firefighting|AttemptFirefightingCore|"
        "System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.Int32|System.Boolean|System.Boolean"
    ),
    "Mods/QudJP/Assemblies/src/Patches/FirefightingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/FirefightingTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_HARVESTABLE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Harvestable.AttemptHarvest because "
        "its harvest success/failure output is covered by existing "
        "WDidXToYWithZ/XDidYToZ/XDidY message-frame routes and harvest "
        "message-pattern dictionaries."
    ),
    "owner route: XDidYTranslationPatch and message-pattern translation",
    "covered leaves: You harvest ..., {actor} harvests ..., There is nothing left to harvest.",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
]
ISSUE719_WORLD_PART_FIXED_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes BeyLahTerrain, HydroponTerrain, and "
        "MoltingBasilisk fixed display-name/short-description leaves through an "
        "exact owner patch on the producer methods."
    ),
    "owner targets: BeyLahTerrain.FireEvent(Event), HydroponTerrain.FireEvent(Event), MoltingBasilisk.SyncState()",
    "Mods/QudJP/Assemblies/src/Patches/WorldPartFixedDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartFixedDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_WORLD_PART_FIXED_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/BeyLahTerrain.cs::BeyLahTerrain.FireEvent(Event)",
        "XRL.World.Parts/HydroponTerrain.cs::HydroponTerrain.FireEvent(Event)",
        "XRL.World.Parts/MoltingBasilisk.cs::MoltingBasilisk.SyncState()",
    }
)
ISSUE719_WORLD_PART_GENERATED_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 generated world-part display-name review closes hologram, "
        "random statue, pet phylactery, and tomb-cultist owner producers through "
        "narrow postfix patches on the exact assignment owners."
    ),
    (
        "The patches reuse GetDisplayNameRouteTranslator for existing generated "
        "display-name grammar and only add a tomb-cultist suffix rewrite for the "
        "death-pilgrim frame."
    ),
    "Mods/QudJP/Assemblies/src/Patches/WorldPartGeneratedDisplayNameTranslationPatches.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartGeneratedDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_WORLD_PART_GENERATED_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/ModQuantumReverb.cs::ModQuantumReverb.CreateHologramOf(GameObject)",
        "XRL.World.Parts/RandomStatue.cs::RandomStatue.SetCreature(GameObject)",
        "XRL.World.Parts/PetPhylactery.cs::PetPhylactery.HandleEvent(AfterObjectCreatedEvent)",
        "XRL.World.Parts/TombCultistTemplate.cs::TombCultistTemplate.Apply(GameObject,HistoricEntitySnapshot)",
    }
)
ISSUE719_RANDOM_FIGURINE_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 random figurine display-name review closes RandomFigurine.HandleEvent "
        "through localized ObjectBlueprint Render.DisplayName templates that keep the "
        "*creature* placeholder but own the surrounding Japanese figurine frame."
    ),
    "Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
    (
        "decompiled owner source: XRL.World.Parts/RandomFigurine.cs HandleEvent replaces "
        "*creature* in ParentObject.Render.DisplayName"
    ),
]
ISSUE719_RANDOM_FIGURINE_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/RandomFigurine.cs::RandomFigurine.HandleEvent(ObjectCreatedEvent)",
    }
)
ISSUE719_MINER_GENERATED_ROLE_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Miner.SetupMinerConfiguration display-name review closes generated "
        "miner/bomber mk suffix names through GetDisplayNameRouteTranslator's "
        "DisplayName.MinerGeneratedRoleSuffix route."
    ),
    "producer: XRL.World.Parts.Miner.SetupMinerConfiguration assigns <MineName>miner mk I / <MineName>bomber mk I",
    "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GetDisplayNameProcessPatchTests.cs",
]
ISSUE719_MINER_GENERATED_ROLE_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Miner.cs::Miner.SetupMinerConfiguration()",
    }
)
ISSUE719_POINTED_ASTERISK_WISH_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 PointedAsteriskBuilder.AsteriskWish debug display-name review closes "
        "the fixed The 10-Pointed Asterisk of the Ensemble leaf through the display-name "
        "atomic dictionary."
    ),
    "Mods/QudJP/Localization/Dictionaries/ui-displayname-atomic.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
    "covered leaf: The 10-Pointed Asterisk of the Ensemble",
]
ISSUE719_POINTED_ASTERISK_WISH_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/PointedAsteriskBuilder.cs::PointedAsteriskBuilder.AsteriskWish()",
    }
)
ISSUE719_SHIP_ARK_POPUP_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 ship/ark popup review closes ShevaStarshipControl and "
        "ArkCore fixed popup/AskString leaves through existing generic popup "
        "and AskString routes plus shipped exact dictionary entries."
    ),
    (
        "covered leaves include launch-state failures, launch confirmation, "
        "InteriorBlockEntrance docking-bay failure, cherubim ark block, and "
        "OPEN ARK confirmation; countdown and moor-rattle leaves already had "
        "ui-phase3d-endings/messages/ui-popup coverage."
    ),
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-phase3d-endings.ja.json",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
]
ISSUE719_SHIP_ARK_POPUP_DICTIONARY_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.AttemptLaunch(GameObject)",
        "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.CheckTimer()",
        "XRL.World.Parts/ArkCore.cs::ArkCore.TryOpen(GameObject)",
    }
)
ISSUE719_CRAYONS_POPUP_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 crayons popup review closes Crayons.HandleEvent through a "
        "method-local owner transpiler for the fixed draw prompts, color-picker "
        "title, direction picker title, nanocrayon failure, and success popups."
    ),
    "owner target: XRL.World.Parts.Crayons|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    "Mods/QudJP/Assemblies/src/Patches/CrayonsPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CrayonsPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_CRAYONS_POPUP_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_POPUP_PICK_OPTION_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact PickOption popup families whose "
        "title and option strings are already present in shipped dictionaries "
        "and served by the existing PopupPickOption route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-options.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-keybinds.ja.json",
]
ISSUE719_RECLAMATION_MESSAGE_LEAVING_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 quest-route review promotes ReclamationSystem.HandleEvent "
        "because the visible Popup.ShowYesNo text is loaded from the localized "
        "Reclamation quest property MessageLeaving."
    ),
    (
        'decompiled XRL.World.Quests/ReclamationSystem.cs line 152 calls '
        'Popup.ShowYesNo(GetProperty("MessageLeaving"), ...)'
    ),
    'Mods/QudJP/Localization/Quests.jp.xml contains quest Name="Reclamation" property Name="MessageLeaving"',
]
ISSUE719_POPUP_MESSAGE_DELETE_SAVE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes delete-save PopupMessage callers whose "
        "message/title template and completion popup are already served by the "
        "existing PopupMessage field translation route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-default.ja.json",
]
ISSUE719_POPUP_MESSAGE_FIXED_FIELD_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact fixed PopupMessage callers whose "
        "message and title fields are already served by the existing PopupMessage "
        "field translation route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-modpage.ja.json",
]
ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 active-effect popup/queue tranche closes IrisdualCallow.Apply, "
        "CookingDomainTongue three-tongues Apply, ShadeOil_Tonic.FireEvent, "
        "BrainBrineCurse.FireEvent, SphynxSalt_Tonic.Apply popup text, and selected "
        "effect onset/status, Remove recovery, and selected FireEvent recovery messages through a "
        "scoped owner route that translates only their active Popup.Show/ShowYesNo "
        "and AddPlayerMessage text."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectPopupQueueTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActiveEffectPopupQueueTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectPopupQueueTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_SPHYNX_SALT_APPLY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers SphynxSalt_Tonic.Apply through the "
        "active-effect popup owner route for the clarity popup and the existing "
        "single-callsite owner queue route for the subtle psychic disturbance message."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectPopupQueueTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActiveEffectPopupQueueTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectPopupQueueTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_VEHICLE_REPAIR_DOES_ROUTE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers VehicleRepair.HandleEvent"
        "(InventoryActionEvent) through the existing ClonelingVehicle owner route; "
        "nearby MessageFrame/Does producer rows remain unpromoted until their "
        "exact owner or global route evidence is proven."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ClonelingVehicleTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldPartsFragmentTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_REVIEWED_MUTATION_ACTION_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers these exact mutation action "
        "MessageFrame families through the global Messaging.XDidY/XDidYToZ route "
        "plus concrete repository MessageFrame dictionary keys."
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "MessageFrameTranslatorTests.TryTranslateXDidY_RepositoryDictionary_TranslatesMutationActionFrames",
]
ISSUE719_CLONELING_PRODUCE_CLONE_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    *ISSUE719_REVIEWED_MUTATION_ACTION_MESSAGE_FRAME_EVIDENCE,
    (
        'Cloneling.PerformCloning source frame: XDidYToZ(ParentObject, "produce", '
        '"a clone of", Target, "in a flurry of {{C|flashing chrome}} and '
        '{{cloning|spurting liquid}}", ...)'
    ),
    (
        "MessageFrame key: verb=produce extra=a clone of {0} in a flurry of "
        "{{C|flashing chrome}} and {{cloning|spurting liquid}}"
    ),
    (
        "MessageFrameTranslatorTests.TryTranslateXDidYToZ_RepositoryDictionary_"
        "TranslatesClonelingProduceCloneFrame"
    ),
]
ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers fixed mutation command popup failures "
        "through the existing Popup.Show/ShowFail exact dictionary route, "
        "ElectricalGeneration grounding through its owner patch, and mutation "
        "action XDidY/XDidYToZ frames through concrete MessageFrame keys."
    ),
    "reviewed source families: StickyTongue.HarpoonNearest, SlogGlands.FireEvent, "
    "Stinger.HandleEvent(CommandEvent), LeyShifting.HandleEvent(CommandEvent), "
    "Burgeoning.Burgeon, Phasing.FireEvent, SpacetimeVortex.FireEvent, "
    "Burrowing.HandleEvent(CommandEvent), Spinnerets.FireEvent, "
    "ElectricalGeneration.PerformDischarge",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MutationActionFailureTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationActionFailureTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers exact MessageFrame+Popup families "
        "whose visible shapes are fully served by existing fixed popup dictionaries, "
        "existing single-callsite popup owners, and concrete XDidY/MessageFrame leaves."
    ),
    "reviewed source families: Stomach.HandleEvent(BeginTakeActionEvent), "
    "ReshephsCrypt.FireEvent, StiltWell.GiveArtifacts, "
    "RebornOnDeathInThinWorld.FireEvent, EngulfingDescends.FireEvent, "
    "Infiltrate.FireEvent, AmbientPowerReceiver.HandleEvent(EnteringZoneEvent), "
    "RestoreOnDeath.HandleEvent(BeforeDieEvent), ModDisplacer.ExamineFailure",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
    "Mods/QudJP/Localization/Dictionaries/world-mods.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_RESIDUAL_POPUP_FRAME_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps these MessageFrame+Popup families "
        "runtime-required because static inventory cannot split their live popup "
        "picker/debug routes from adjacent MessageFrame routes without runtime evidence."
    ),
    "reviewed source families: MagazineAmmoLoader.FireEvent and Brain.HandleEvent(InventoryActionEvent)",
    "MagazineAmmoLoader.FireEvent mixes PickItem/AskNumber supply popups with transfer MessageFrames.",
    (
        "Brain.HandleEvent(InventoryActionEvent) mixes chronology/feeling debug "
        "popups with thinking-out-loud MessageFrames."
    ),
]
ISSUE719_RESIDUAL_POPUP_FRAME_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Brain.cs::Brain.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_MAGAZINE_AMMO_SUPPLY_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 MagazineAmmoLoader.FireEvent closure: SupplyIntegratedHostWithAmmo "
        "AskNumber prompts are covered by PopupAskNumberTranslationPatch, and ammo "
        "transfer frames are covered by repository MessageFrame transfer templates."
    ),
    "decompiled owner source: XRL.World.Parts/MagazineAmmoLoader.cs lines 630-686",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskNumberTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
]
ISSUE719_MAGAZINE_AMMO_SUPPLY_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.FireEvent(Event)",
    }
)
ISSUE719_BRAIN_DEBUG_INTERNAL_PASSTHROUGH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes Brain.HandleEvent(InventoryActionEvent) "
        "because all popup branches are DebugInternals/DebugAttitude inventory actions "
        "and the adjacent thinking-out-loud branch is a debug MessageFrame route."
    ),
    "decompiled owner source: XRL.World.Parts/Brain.cs lines 1923-2029",
    "Show Attitude is only added when Options.DebugInternals or Options.DebugAttitude is enabled.",
    "ToggleThinkOutLoud is only added when Options.DebugInternals is enabled.",
]
ISSUE719_BRAIN_DEBUG_INTERNAL_PASSTHROUGH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Brain.cs::Brain.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_SPLIT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers exact Does+MessageFrame families whose "
        "visible Does and XDidY/XDidYToZ frames are served by Does fragment marking, "
        "the repository MessageFrame route, and concrete MessageFrame verb leaves."
    ),
    (
        "reviewed source families: Pettable.Pet, Robot.FireEvent, "
        "IProgrammableRecoiler.ProgramRecoiler, Hookah.SmokeHookah"
    ),
    "Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbRouteTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps mixed Does+MessageFrame families "
        "runtime-required when static inventory cannot prove the generated "
        "Fail/Popup/body-part branches from the same owner method."
    ),
    (
        "reviewed source families: TemporalFugue.PerformTemporalFugue, "
        "AutomatedExternalDefibrillator.AttemptDefibrillate, and "
        "CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe"
    ),
    "TemporalFugue mixes Does/XDidY frames with fixed and generated Actor.Fail branches.",
    (
        "AutomatedExternalDefibrillator mixes Does/WDidXToYWithZ frames with "
        "generated target and confirmation messages."
    ),
    (
        "CyberneticsPrecisionForceLathe mixes Does/XDidYToZ frames with generated "
        "held-item/body-part failure and success branches."
    ),
]
ISSUE719_TEMPORAL_FUGUE_PERFORM_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 TemporalFugue.PerformTemporalFugue closure: the exact owner "
        "method's Does/XDidY frames are covered by message-frame verbs, while "
        "its fixed and generated Actor.Fail branches are covered by popup "
        "dictionary leaves/templates."
    ),
    "decompiled owner source: XRL.World.Parts.Mutation/TemporalFugue.cs lines 120-251",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
]
ISSUE719_TEMPORAL_FUGUE_PERFORM_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts.Mutation/TemporalFugue.cs::"
            "TemporalFugue.PerformTemporalFugue(GameObject,GameObject,GameObject,TemporalFugue,IEvent,bool,bool,"
            "int?,int?,int,string,string,string,string,string)"
        ),
    }
)
ISSUE719_DEFIBRILLATOR_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 AutomatedExternalDefibrillator review closes "
        "AttemptDefibrillate through an owner-scoped queue/popup translator "
        "for the generated item/target failure and confirmation branches."
    ),
    (
        "The method builds Actor.Fail text for missing skill, power/status "
        "failure, and no-target branches, builds a Popup.ShowYesNo confirmation "
        "for non-cardiac-arrest targets, and emits WDidXToYWithZ success/dodge frames."
    ),
    (
        "Existing MessageFrame coverage handles the defibrillator WDidXToYWithZ "
        "verb frame; AutomatedExternalDefibrillatorTranslationPatch owns the "
        "remaining route-local Fail and ShowYesNo text."
    ),
    "Mods/QudJP/Assemblies/src/Patches/AutomatedExternalDefibrillatorTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/AutomatedExternalDefibrillatorTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "decompiled owner source: XRL.World.Parts/AutomatedExternalDefibrillator.cs lines 129-188",
]
ISSUE719_JOURNAL_WISH_GOSPEL_DATA_ROUTE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes JournalAPI.WishGospelAccomplishments "
        "because it is a WishCommand debug popup over stored JournalAccomplishment "
        "Text/GospelText data; GospelText is translated at the JournalAPI.Add owner route."
    ),
    "decompiled owner source: Qud.API/JournalAPI.cs lines 1460-1469",
    "Mods/QudJP/Assemblies/src/Patches/JournalAccomplishmentAddTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Quests/Quests.jp.xml",
]
ISSUE719_JOURNAL_WISH_GOSPEL_DATA_ROUTE_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.API/JournalAPI.cs::JournalAPI.WishGospelAccomplishments()",
    }
)
ISSUE719_TRADE_HIGHLIGHT_DATA_BINDING_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes TradeScreen.HandleHighlightObject because "
        "it binds DisplayNameSingle plus weight/price glyph data and clears fields "
        "when no TradeLineData object is highlighted."
    ),
    "decompiled owner source: Qud.UI/TradeScreen.cs lines 946-965",
    "TradeScreen.HandleHighlightObject binds DisplayNameSingle plus weight/price glyph data.",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_TRADE_HIGHLIGHT_DATA_BINDING_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/TradeScreen.cs::TradeScreen.HandleHighlightObject(FrameworkDataElement)",
    }
)
ISSUE719_TREMBLE_EARTHQUAKE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes TrembleEarthquakes.RocksFall because "
        "the falling-rocks damage source, death reason, and Does frame are already "
        "covered by the combat/log message and MessageFrame owner routes."
    ),
    "decompiled owner source: XRL.World.Parts/TrembleEarthquakes.cs lines 52-85",
    "TrembleEarthquakes.RocksFall damage source: from falling rocks",
    "Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/SystemStaticMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-death.ja.json",
]
ISSUE719_TREMBLE_EARTHQUAKE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/TrembleEarthquakes.cs::TrembleEarthquakes.RocksFall(Zone)",
    }
)
ISSUE719_VEHICLE_MELEE_INFILTRATION_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 VehicleMeleeInfiltration.TryInfiltrate is covered by "
        "Does/message-frame translation for the hostile-entry confirmation popup "
        "and the paired infiltration EmitMessage success frame."
    ),
    "decompiled owner source: XRL.World.Parts/VehicleMeleeInfiltration.cs lines 26-104",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/GameObjectEmitMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
]
ISSUE719_VEHICLE_MELEE_INFILTRATION_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/VehicleMeleeInfiltration.cs::"
            "VehicleMeleeInfiltration.TryInfiltrate(GameObject,Interior)"
        ),
    }
)
ISSUE719_UI_WIDGET_DATA_BINDING_PASS_THROUGH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes UI widget SetText/direct-text rows "
        "that only bind caller-owned data, numeric/control glyphs, debug input, "
        "or already translated upstream screen data."
    ),
    "Qud.UI/PopupMessage.cs Update only manages hide/cancel/input runtime state.",
    "Qud.UI/EquipmentLine.cs and InventoryLine.cs UpdateHotkey emit hotkey glyphs only.",
    "Qud.UI/InventoryLine.cs OnBeginDragObject clears numeric weight text while dragging.",
    "Qud.UI/ProgressBar.cs Set emits numeric progress only.",
    "Qud.UI/ConsoleWindow.cs Update executes and clears debug console input.",
    "Qud.UI/Notification.cs Routine displays Notification.Enqueue caller-owned Title/Text data.",
    (
        "Qud.UI/CyberneticsTerminalRow.cs displays CyberneticsTerminalLineData.Text "
        "after CyberneticsTerminalTextTranslationPatch translates TerminalScreen output."
    ),
    (
        "Qud.UI/MissileWeaponAreaInfo.cs binds MissileWeaponAreaWeaponStatus.text; "
        "MissileWeapon/EnergyAmmoLoader owners remain responsible for status text."
    ),
    "Qud.UI/StatusBarStatBlock.cs emits stat short-name abbreviations plus numeric values.",
    "Qud.UI/ModManagerUI.cs SetBackButtonText wraps caller-owned bottom-context text.",
    "Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsTerminalTextTranslationPatchTests.cs",
]
ISSUE719_UI_WIDGET_DATA_BINDING_PASS_THROUGH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/PopupMessage.cs::PopupMessage.Update()",
        "Qud.UI/EquipmentLine.cs::EquipmentLine.UpdateHotkey()",
        "Qud.UI/InventoryLine.cs::InventoryLine.UpdateHotkey()",
        "Qud.UI/InventoryLine.cs::InventoryLine.OnBeginDragObject()",
        "Qud.UI/ProgressBar.cs::ProgressBar.Set(int,int)",
        "Qud.UI/ConsoleWindow.cs::ConsoleWindow.Update()",
        "Qud.UI/Notification.cs::Notification.Routine()",
        "Qud.UI/CyberneticsTerminalRow.cs::CyberneticsTerminalRow.setData(FrameworkDataElement)",
        "Qud.UI/CyberneticsTerminalRow.cs::CyberneticsTerminalRow.Update()",
        (
            "Qud.UI/MissileWeaponAreaInfo.cs::"
            "MissileWeaponAreaInfo.UpdateFrom(MissileWeaponArea.MissileWeaponAreaWeaponStatus)"
        ),
        "Qud.UI/StatusBarStatBlock.cs::StatusBarStatBlock.Update()",
        "Qud.UI/StatusBarStatBlock.cs::StatusBarStatBlock.UpdateStats(Dictionary<string,string>)",
        "Qud.UI/ModManagerUI.cs::ModManagerUI.SetBackButtonText(string)",
    }
)
ISSUE719_LEFT_SIDE_CATEGORY_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes LeftSideCategory.setData through an exact owner "
        "postfix for the rendered category text field."
    ),
    "decompiled owner source: Qud.UI/LeftSideCategory.cs lines 16-31",
    "Mods/QudJP/Assemblies/src/Patches/LeftSideCategoryTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SinkPrereqTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_LEFT_SIDE_CATEGORY_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/LeftSideCategory.cs::LeftSideCategory.setData(FrameworkDataElement)",
    }
)
ISSUE719_MOD_MANAGER_CANCEL_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes ModManagerUI.OnCancel through the existing PopupMessage "
        "generic route plus a fixed ui-popup dictionary leaf; Yes/No command data is preserved."
    ),
    "decompiled owner source: Qud.UI/ModManagerUI.cs lines 265-285",
    "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_FRAMEWORK_SEARCH_INPUT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes FrameworkSearchInput.ChangeValue through the existing "
        "Popup.AskStringAsync generic prompt route plus a fixed ui-popup dictionary leaf."
    ),
    "decompiled owner source: XRL.UI.Framework/FrameworkSearchInput.cs lines 85-89",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_ABILITY_MANAGER_EMPTY_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes AbilityManagerScreen.showScreen through the existing "
        "AbilityManager popup owner route for the fixed no-activated-abilities message."
    ),
    "decompiled owner source: Qud.UI/AbilityManagerScreen.cs lines 173-188",
    "Mods/QudJP/Assemblies/src/Patches/AbilityManagerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerScreenTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_FINAL_SINK_PASS_THROUGH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes final sink helpers that only forward "
        "caller-owned text and do not own fixed player-visible English leaves."
    ),
    "Extensions.cs ShowSuccess forwards caller-owned Message to Popup.Show.",
    (
        "XRL.Messages/MessageQueue.cs AddPlayerMessage(string,char,bool) converts "
        "the color char to a string and delegates to AddPlayerMessage(string,string,bool)."
    ),
    "decompiled sink sources: Extensions.cs lines 3033-3040; XRL.Messages/MessageQueue.cs lines 106-135",
]
ISSUE719_FINAL_SINK_PASS_THROUGH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Extensions.cs::Extensions.ShowSuccess(this XRL.World.GameObject,string,bool)",
        "XRL.Messages/MessageQueue.cs::MessageQueue.AddPlayerMessage(string,char,bool)",
    }
)
ISSUE719_TUTORIAL_SENTINEL_PASS_THROUGH_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review promotes FadeText.Update because it sends only "
        "the <nohighlight> tutorial control sentinel to ShowIntermissionPopupAsync."
    ),
    "decompiled owner source: XRL.UI/FadeText.cs lines 45-75",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/Issue289OrphanRoutePatchTests.cs",
]
ISSUE719_TUTORIAL_SENTINEL_PASS_THROUGH_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.UI/FadeText.cs::FadeText.Update()",
    }
)
ISSUE719_TUTORIAL_LATEUPDATE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 tutorial LateUpdate review promotes exact JoppaTutorial "
        "LateUpdate rows because their visible text is routed through the "
        "existing TutorialManager popup/highlight owner patches or existing "
        "generic Popup dictionary route."
    ),
    (
        "owner patches: TutorialManagerTranslationPatch, "
        "TutorialManagerCellPopupTranslationPatch, TutorialManagerHighlightTranslationPatch, "
        "TutorialManagerCellHighlightTranslationPatch"
    ),
    (
        "localization sources: Mods/QudJP/Localization/Dictionaries/ui-tutorial.ja.json "
        "and ui-popup.ja.json"
    ),
    "decompiled owner sources: JoppaTutorial/* LateUpdate methods",
]
ISSUE719_TUTORIAL_LATEUPDATE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "JoppaTutorial/BattleRemains.cs::BattleRemains.LateUpdate()",
        "JoppaTutorial/ExamineChemcell.cs::ExamineChemcell.LateUpdate()",
        "JoppaTutorial/ExploreJoppa.cs::ExploreJoppa.LateUpdate()",
        "JoppaTutorial/FightBear.cs::FightBear.LateUpdate()",
        "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.LateUpdate()",
        "JoppaTutorial/GetBooks.cs::GetBooks.LateUpdate()",
        "JoppaTutorial/MakeCamp.cs::MakeCamp.LateUpdate()",
        "JoppaTutorial/MoveToChest.cs::MoveToChest.LateUpdate()",
    }
)
ISSUE719_TUTORIAL_POPUP_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 tutorial review promotes command/cell guard Popup.Show rows "
        "because the player-visible literals are fixed tutorial guidance strings "
        "served by the generic Popup.Show dictionary route."
    ),
    "owner route: PopupShowTranslationPatch -> PopupShowSemanticPipeline",
    "localization source: Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    (
        "decompiled owner sources: JoppaTutorial AllowCommand/AllowTargetPick/"
        "AllowInventoryInteract/BeforePlayerEnterCell methods"
    ),
]
ISSUE719_TUTORIAL_POPUP_DICTIONARY_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "JoppaTutorial/FightBear.cs::FightBear.AllowCommand(string)",
        "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.AllowCommand(string)",
        "JoppaTutorial/FightBear.cs::FightBear.AllowTargetPick(GameObject,Type,List<Cell>)",
        "JoppaTutorial/BattleRemains.cs::BattleRemains.AllowInventoryInteract(GameObject)",
        "JoppaTutorial/ExploreWorldMap.cs::ExploreWorldMap.BeforePlayerEnterCell(Cell)",
        "JoppaTutorial/FightBear.cs::FightBear.BeforePlayerEnterCell(Cell)",
        "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.BeforePlayerEnterCell(Cell)",
        "JoppaTutorial/MoveToChest.cs::MoveToChest.BeforePlayerEnterCell(Cell)",
    }
)
ISSUE719_TUTORIAL_MANAGER_TRIGGER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 tutorial review promotes seen/trigger rows because the "
        "visible strings route through TutorialManager cell/intermission popup "
        "owners and shipped tutorial dictionaries."
    ),
    "owner patches: TutorialManagerTranslationPatch and TutorialManagerCellPopupTranslationPatch",
    "TutorialManager.ShowIntermissionPopupAsync delegates to ShowCIDPopupAsync",
    "localization source: Mods/QudJP/Localization/Dictionaries/ui-tutorial.ja.json",
    "decompiled owner sources: JoppaTutorial BearSeen/SnapjawSeen/OnTrigger methods",
]
ISSUE719_TUTORIAL_MANAGER_TRIGGER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "JoppaTutorial/FightBear.cs::FightBear.BearSeen(Location2D)",
        "JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.SnapjawSeen(Location2D)",
        "JoppaTutorial/MakeCamp.cs::MakeCamp.OnTrigger(string)",
        "JoppaTutorial/MoveToChest.cs::MoveToChest.OnTrigger(string)",
    }
)
ISSUE719_FINAL_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review reclassifies these final runtime rows as "
        "implementation gaps because exact owner methods and fixed/generated "
        "player-visible text are visible in decompiled source."
    ),
    (
        "Existing generic popup/message sinks may observe the output, but the "
        "route-local owners still need focused implementation coverage."
    ),
    (
        "decompiled owner sources: XRL.World.Effects/FungalSporeInfection.cs lines "
        "140-159"
    ),
]
ISSUE719_FINAL_STATIC_GAP_FAMILIES: Final[frozenset[str]] = frozenset(
    {
    }
)
ISSUE719_FUNGAL_SPORE_CHOOSE_LIMB_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 FungalSporeInfection closure: FungalSporeInfectionTranslationPatch now "
        "targets ChooseLimbForInfection and translates the no-infectable-body-parts popup, "
        "the generated PickOption title, and route-local body-part options."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/FungalSporeInfectionTranslationPatch.cs",
    "implementation: Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Effects/FungalSporeInfection.cs lines 140-159",
]
ISSUE719_FUNGAL_SPORE_CHOOSE_LIMB_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Effects/FungalSporeInfection.cs::"
            "FungalSporeInfection.ChooseLimbForInfection(List<BodyPart>,string,out BodyPart,out string,bool)"
        ),
    }
)
ISSUE719_DESALINATION_PELLET_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 DesalinationPellet closure: DesalinationPelletTranslationPatch scopes "
        "Popup.Show/ShowFail during HandleEvent(InventoryActionEvent), and "
        "DesalinationPelletFragmentTranslator covers both the fixed no-effect failure and "
        "the generated You drop ... into ... frame."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/DesalinationPelletTranslationPatch.cs",
    "implementation: Mods/QudJP/Assemblies/src/Patches/DesalinationPelletFragmentTranslator.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldPartsFragmentTranslatorTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Parts/DesalinationPellet.cs lines 63-86 and 185-218",
]
ISSUE719_DESALINATION_PELLET_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/DesalinationPellet.cs::DesalinationPellet.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_IGRENADE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 IGrenade closure: SingleCallsiteOwnerPopupTranslationPatch now scopes "
        "IGrenade.HandleEvent(InventoryActionEvent) and translates the fixed detonate-world-map "
        "failure popup."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Parts/IGrenade.cs lines 47-63",
]
ISSUE719_IGRENADE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/IGrenade.cs::IGrenade.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_BIOME_SURFACE_DISTRIBUTION_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes BiomeManager.DisplaySurfaceDistribution through exact owner-scoped "
        "popup and queue routes for the surfacebiomes wish output."
    ),
    "popup implementation: Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "queue implementation: Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Biomes/BiomeManager.cs lines 96-137",
]
ISSUE719_BIOME_SURFACE_DISTRIBUTION_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Biomes/BiomeManager.cs::BiomeManager.DisplaySurfaceDistribution(string)",
    }
)
ISSUE719_ELEVATOR_SWITCH_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes ElevatorSwitch.FireEvent through exact owner-scoped queue and popup "
        "routes for switch no-op and platform movement messages."
    ),
    "queue implementation: Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
    "popup implementation: Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Parts/ElevatorSwitch.cs lines 34-55",
]
ISSUE719_ELEVATOR_SWITCH_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/ElevatorSwitch.cs::ElevatorSwitch.FireEvent(Event)",
    }
)
ISSUE719_VEHICLE_FOLLOWER_POPUP_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Vehicle.HandleEvent(InventoryActionEvent) closure: "
        "VehicleFollowerPopupTranslationPatch scopes the CompanionEnterVehicle popup "
        "route and translates both the generated no-follower failure and the fixed "
        "PickGameObject follower title."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/VehicleFollowerPopupTranslationPatch.cs",
    "implementation: Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "implementation: Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/VehicleFollowerPopupTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Parts/Vehicle.cs lines 401-421",
]
ISSUE719_VEHICLE_FOLLOWER_POPUP_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Vehicle.cs::Vehicle.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/AutomatedExternalDefibrillator.cs::"
            "AutomatedExternalDefibrillator.AttemptDefibrillate(GameObject,IEvent)"
        ),
    }
)
ISSUE719_FORCE_LATHE_ACTIVATION_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe "
        "closure: owner-scoped popup/queue translation covers generated no-hold-slot "
        "and powered-status failures, while existing message-frame leaves cover "
        "successful shimmer-into-existence output."
    ),
    "decompiled owner source: XRL.World.Parts/CyberneticsPrecisionForceLathe.cs lines 108-155",
    "Mods/QudJP/Assemblies/src/Patches/CyberneticsPrecisionForceLatheTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsPrecisionForceLatheTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_FORCE_LATHE_ACTIVATION_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
            "CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe(GameObject,GameObject,IEvent)"
        ),
    }
)
ISSUE719_RESIDUAL_PURE_POPUP_TOP_SPLIT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers GritGateTerminalScreenRoot.UpdatePowerOptions "
        "through exact fixed popup leaves already served by the generic Popup.Show route."
    ),
    "reviewed source popup literals: remote-management offline chain-laser and force-projector warnings",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_RESIDUAL_PURE_POPUP_TOP_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps broad pure-Popup families runtime-required "
        "because static inventory cannot split live UI picker, generated prompt/body, "
        "debug/detail, and owner-data branches from fixed popup leaves."
    ),
    (
        "reviewed source families: OptionsUI.Show, Scores.Show, ItemNaming.NameItem, "
        "Crayons.HandleEvent, Description.HandleEvent(InventoryActionEvent), "
        "Inventory.HandleEvent(InventoryActionEvent), and TradeUI.ShowVendorActions"
    ),
    "OptionsUI.Show mixes option-setting UI prompts with rendered options screen state.",
    "Scores.Show has been split to a static legacy high-score screen owner gap.",
    "ItemNaming.NameItem mixes naming pickers, ask-string/color-pickers, generated item names, and debug output.",
    "Crayons.HandleEvent mixes player-authored drawing text, color picker routes, and fixed result popups.",
    "Description.HandleEvent displays dynamic description/story popup bodies owned by object description routes.",
    "Inventory.HandleEvent mixes PickItem/PickOption routes, dynamic ownership prompts, and failure strings.",
    "TradeUI.ShowVendorActions mixes trader/item/water-debt generated popups with trade-specific fixed leaves.",
]
ISSUE719_ITEM_NAMING_INTERACTIVE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 ItemNaming.NameItem interactive overload review closes the "
        "player naming picker, ask-string prompt, and color-picker prompt through "
        "ItemNamingTranslationPatch plus Popup.PickOption/AskString/ShowColorPicker routes."
    ),
    (
        "The owner translator handles the fixed picker frames while preserving generated "
        "item/name/culture captures; relic-style generated names remain owned by the "
        "existing ItemNaming.GenerateRelicStyleName/QudHistorySpice route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ItemNamingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowColorPickerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ItemNamingTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.World.Capabilities/ItemNaming.cs lines 397-492",
]
ISSUE719_ITEM_NAMING_INTERACTIVE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Capabilities/ItemNaming.cs::"
            "ItemNaming.NameItem(GameObject,GameObject,GameObject,GameObject,string,string,bool)"
        ),
    }
)
ISSUE719_ITEM_NAMING_WISH_DEBUG_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 ItemNaming.HandleItemNamingWish review closes the itemnaming "
        "WishCommand debug popups through ItemNamingTranslationPatch owner scope."
    ),
    (
        "The owner translator handles the created kill/InfluencedBy debug lines and "
        "the naming-failed debug popup while preserving generated DebugName captures."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ItemNamingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ItemNamingTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.World.Capabilities/ItemNaming.cs lines 729-755",
]
ISSUE719_ITEM_NAMING_WISH_DEBUG_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Capabilities/ItemNaming.cs::ItemNaming.HandleItemNamingWish(Match)",
    }
)
ISSUE719_SCORES_SHOW_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Scores.Show review closes the legacy high-score screen through "
        "an exact XRL.Core.Scores.Show owner transpiler for fixed Buffer.Write "
        "screen labels and score-detail summary lines."
    ),
    (
        "The delete-confirmation popup remains covered by the existing "
        "HighScoresDeletePopupTranslationPatch owner scope on the same "
        "Scores.Show method."
    ),
    "Mods/QudJP/Assemblies/src/Patches/LegacyScoresScreenTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyScoresScreenTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/src/Patches/HighScoresDeletePopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HighScoresDeletePopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/src/Patches/HighScoresScreenTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HighScoresScreenTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.Core/Scores.cs lines 51-504",
]
ISSUE719_OPTIONS_UI_SHOW_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 OptionsUI.Show review closes the legacy options screen through "
        "an exact XRL.UI.OptionsUI.Show owner transpiler for ScreenBuffer.Write "
        "chrome, category/display/value text, and restart confirmation prompts."
    ),
    "owner target: XRL.UI.OptionsUI|Show|XRL.UI.ScreenReturn",
    "Mods/QudJP/Assemblies/src/Patches/LegacyOptionsUiTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyOptionsUiTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.UI/OptionsUI.cs lines 67-557",
]
ISSUE719_OPTIONS_UI_SHOW_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.UI/OptionsUI.cs::OptionsUI.Show()",
    }
)
ISSUE719_OPTIONS_CONTROL_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Options control review closes Unity Options control menu-option "
        "descriptions through UiMenuOptionDescriptionTranslationPatch owner routes "
        "for OptionsCategoryControl, OptionsCheckboxControl, OptionsSliderControl, "
        "and OptionsComboBoxControl."
    ),
    "Mods/QudJP/Assemblies/src/Patches/UiMenuOptionDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/UiMenuOptionDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-options.ja.json",
    (
        "decompiled owner sources: Qud.UI/OptionsCategoryControl.cs, "
        "Qud.UI/OptionsCheckboxControl.cs, Qud.UI/OptionsSliderControl.cs, "
        "Qud.UI/OptionsComboBoxControl.cs"
    ),
]
ISSUE719_OPTIONS_CONTROL_DESCRIPTION_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/OptionsCategoryControl.cs::OptionsCategoryControl.TOGGLE_OPTION",
        "Qud.UI/OptionsCheckboxControl.cs::OptionsCheckboxControl.TOGGLE_OPTION",
        "Qud.UI/OptionsSliderControl.cs::OptionsSliderControl.CHANGE_VALUE",
        "Qud.UI/OptionsSliderControl.cs::OptionsSliderControl.ARROWS_CHANGE_VALUE",
        "Qud.UI/OptionsSliderControl.cs::OptionsSliderControl.SAVE_VALUE",
        "Qud.UI/OptionsSliderControl.cs::OptionsSliderControl.CANCEL_VALUE",
        "Qud.UI/OptionsComboBoxControl.cs::OptionsComboBoxControl.Render()",
    }
)
ISSUE719_OBJECT_FINDER_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 ObjectFinder display-name review closes fixed context and "
        "sorter labels through ObjectFinderDisplayNameTranslationPatch targeting "
        "each GetDisplayName owner."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ObjectFinderDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ObjectFinderDisplayNameTranslationPatchTests.cs",
    (
        "decompiled owner sources: XRL.UI.ObjectFinderContexts/AutogotItems.cs, "
        "XRL.UI.ObjectFinderContexts/NearbyItems.cs, XRL.UI.ObjectFinderSorters/IdSorter.cs, "
        "and XRL.UI.ObjectFinderSorters/ValueSorter.cs"
    ),
]
ISSUE719_OBJECT_FINDER_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.UI.ObjectFinderContexts/AutogotItems.cs::AutogotItems.GetDisplayName()",
        "XRL.UI.ObjectFinderContexts/NearbyItems.cs::NearbyItems.GetDisplayName()",
        "XRL.UI.ObjectFinderSorters/IdSorter.cs::IdSorter.GetDisplayName()",
        "XRL.UI.ObjectFinderSorters/ValueSorter.cs::ValueSorter.GetDisplayName()",
    }
)
ISSUE719_CYBERNETICS_SKILLSOFT_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 cybernetics skillsoft display-name review closes Skillsoft "
        "and Skillsoft Plus generated chip names through the GetDisplayNameRouteTranslator "
        "CyberneticsSkillsoft route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
    (
        "decompiled owner sources: XRL.World.Parts/CyberneticsSingleSkillsoft.cs "
        "and XRL.World.Parts/CyberneticsTreeSkillsoft.cs InitChip display-name assignments"
    ),
]
ISSUE719_CYBERNETICS_SKILLSOFT_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/CyberneticsSingleSkillsoft.cs::CyberneticsSingleSkillsoft.InitChip(bool)",
        "XRL.World.Parts/CyberneticsTreeSkillsoft.cs::CyberneticsTreeSkillsoft.InitChip(bool,bool,double)",
    }
)
ISSUE719_CYBERNETICS_RECOILER_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 cybernetics recoiler display-name review closes Recoil to <zone> "
        "activated ability names through the shared ActivatedAbilityNameTranslator route."
    ),
    "producer: CyberneticsOnboardRecoilerImprinting.UpdateName assigns Recoil / Recoil to <zone>",
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActivatedAbilityNameTranslatorTests.cs",
    "covered pattern: Recoil to {{Y|Joppa}}",
]
ISSUE719_CYBERNETICS_RECOILER_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs::CyberneticsOnboardRecoilerImprinting.UpdateName()",
    }
)
ISSUE719_STAT_SHIFT_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 stat-shift display-name review closes camouflage and "
        "co-processor StatShifter source labels through StatisticStatShiftDisplayNameTranslationPatch "
        "at Statistic.AddShift."
    ),
    "Mods/QudJP/Assemblies/src/Patches/StatisticStatShiftDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatisticStatShiftDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    (
        "decompiled owner sources: XRL.World.Parts.Mutation/PhotosyntheticSkin.cs, "
        "XRL.World.Parts/Yurtmat.cs, and XRL.World.Parts/ModCoProcessor.cs"
    ),
]
ISSUE719_STAT_SHIFT_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs::PhotosyntheticSkin.CheckCamouflage()",
        "XRL.World.Parts/Yurtmat.cs::Yurtmat.CheckCamouflage()",
        "XRL.World.Parts/ModCoProcessor.cs::ModCoProcessor.ApplyBonus(GameObject)",
    }
)
ISSUE719_MUTATION_BASE_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 mutation display-name review closes BaseMutation.GetDisplayName "
        "and MutationEntry.GetDisplayName with MutationDisplayNameTranslationPatch "
        "on the exact mutation owner APIs."
    ),
    (
        "The postfix translates only the returned display name through the shipped "
        "Mutations.jp.xml / HiddenMutations.jp.xml display-name map and preserves "
        "the internal cached _DisplayName value."
    ),
    "Mods/QudJP/Assemblies/src/Patches/MutationDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    (
        "decompiled owner sources: XRL.World.Parts.Mutation/BaseMutation.cs "
        "lines 157-174 and XRL/MutationEntry.cs lines 261-269"
    ),
]
ISSUE719_MUTATION_BASE_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts.Mutation/BaseMutation.cs::BaseMutation.GetDisplayName(bool)",
        "XRL/MutationEntry.cs::MutationEntry.GetDisplayName(bool)",
    }
)
ISSUE719_MUTATION_EFFECT_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 mutation effect display-name review closes Metamorphed's "
        "fixed effect DisplayName assignment through the active-effect description route."
    ),
    "Mods/QudJP/Localization/Dictionaries/world-effects-status.ja.json",
    "Mods/QudJP/Assemblies/src/Patches/EffectDescriptionPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs",
    "decompiled owner source: XRL.World.Parts.Mutation/Metamorphed.cs lines 13-16",
]
ISSUE719_MUTATION_EFFECT_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.Metamorphed()",
    }
)
ISSUE719_LIGHT_MANIPULATION_ABILITY_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 LightManipulation ability display-name review closes "
        "SyncAbilityName through MutationActivatedAbilityNameTranslationPatch and "
        "the shared ActivatedAbilityNameTranslator Lase charge pattern."
    ),
    "Mods/QudJP/Assemblies/src/Patches/MutationActivatedAbilityNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationActivatedAbilityNameTranslationPatchTests.cs",
    "decompiled owner source: XRL.World.Parts.Mutation/LightManipulation.cs lines 431-437",
]
ISSUE719_LIGHT_MANIPULATION_ABILITY_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts.Mutation/LightManipulation.cs::LightManipulation.SyncAbilityName()",
    }
)
ISSUE719_CYBERNETICS_INSTALL_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 cybernetics install option review closes CyberneticsScreenInstall.OnUpdate "
        "through the existing TerminalScreen.Update transpiler and CyberneticsTerminalTextTranslator "
        "display-name option templates."
    ),
    (
        "The translator handles only recognized install option suffixes such as license points, "
        "already-installed states, and will-replace states, while sending the implant label through "
        "GetDisplayNameRouteTranslator."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalTextTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalTextTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsTerminalTextTranslationPatchTests.cs",
    "decompiled owner source: XRL.UI/CyberneticsScreenInstall.cs OnUpdate implant Options.Add path",
]
ISSUE719_CYBERNETICS_INSTALL_DISPLAY_NAME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.UI/CyberneticsScreenInstall.cs::CyberneticsScreenInstall.OnUpdate()",
    }
)
ISSUE719_UI_SCREEN_FIXED_LABEL_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 UI fixed-label review closes SkillsAndPowers owner title "
        "through the existing SkillsAndPowersStatusScreenTranslationPatch nameBlockText route "
        "and KeybindBox edit-mode prompt through KeybindBoxTranslationPatch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenTemplateTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/src/Patches/KeybindBoxTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeybindBoxTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_UI_SCREEN_FIXED_LABEL_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/SkillsAndPowersStatusScreen.cs::SkillsAndPowersStatusScreen.ShowScreen(XRL.World.GameObject,StatusScreensScreen)",
        "Qud.UI/KeybindBox.cs::KeybindBox.Update()",
    }
)
ISSUE719_EQUIPMENT_SCREEN_BODYPART_EQUIP_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 EquipmentScreen.ShowBodypartEquipUI review closes the fixed "
        "body-part equipment failure popups through an exact owner scope."
    ),
    "Mods/QudJP/Assemblies/src/Patches/EquipmentScreenBodypartEquipPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/EquipmentScreenBodypartEquipPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.UI/EquipmentScreen.cs lines 26-59",
]
ISSUE719_EQUIPMENT_SCREEN_BODYPART_EQUIP_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.UI/EquipmentScreen.cs::EquipmentScreen.ShowBodypartEquipUI(GameObject,BodyPart)",
    }
)
ISSUE719_RESIDUAL_PURE_POPUP_TOP_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.Core/Scores.cs::Scores.Show()",
        "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts/Inventory.cs::Inventory.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_DESCRIPTION_LOOK_POPUP_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Description.HandleEvent(InventoryActionEvent) review closes the "
        "Look popup chrome through an owner-scoped transpiler while tooltip display "
        "names and long descriptions remain owned by LookTooltipInformationWrapPatch "
        "and DescriptionTextTranslator."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DescriptionLookPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/LookTooltipInformationWrapPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionLookPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.World.Parts/Description.cs lines 333-381",
]
ISSUE719_TRADE_UI_SHOW_VENDOR_ACTIONS_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 TradeUI.ShowVendorActions review closes the vendor action "
        "picker through the existing TradeUiVendorPopupTranslationPatch owner scope. "
        "Only the Popup.PickOption display payload is translated; the original "
        "English command list remains available for downstream selection comparisons."
    ),
    "Mods/QudJP/Assemblies/src/Patches/TradeUiVendorPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.UI/TradeUI.cs lines 1139-1206",
]
ISSUE719_OBJECT_FINDER_CONFIG_FILTERS_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 ObjectFinder.ConfigFilters review closes the filter/action "
        "picker chrome through ObjectFinderConfigFiltersTranslationPatch. The owner "
        "translator handles fixed titles, filter action labels, and bracketed state "
        "suffixes while preserving classifier display-name captures."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ObjectFinderConfigFiltersTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ObjectFinderDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ObjectFinderConfigFiltersTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.UI/ObjectFinder.cs lines 301-365",
]
ISSUE719_EQUIPMENT_ACTION_MENU_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 EquipmentAPI.ShowInventoryActionMenu closure: the method "
        "passes action labels to Popup.PickOption with an InventoryActionMenu: "
        "popup id, which routes labels through the inventory-action scoped "
        "dictionary while preserving menu command data."
    ),
    "decompiled owner source: Qud.API/EquipmentAPI.cs lines 116-138",
    "InventoryActionMenu:",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/UiDictionaryOwnershipTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-inventory-actions.ja.json",
]
ISSUE719_EQUIPMENT_ACTION_MENU_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "Qud.API/EquipmentAPI.cs::"
            "EquipmentAPI.ShowInventoryActionMenu(Dictionary<string,InventoryAction>,GameObject,GameObject,bool,bool,"
            "string,IComparer<InventoryAction>,bool)"
        ),
    }
)
ISSUE719_RESIDUAL_PURE_POPUP_REMAINDER_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review clears the remaining pure-Popup producer families "
        "to runtime-required because whole-family static rows mix fixed prompt leaves "
        "with generated names, wish/debug paths, mod/save error bodies, confirmation "
        "tokens, conversation rewards, object-name composition, or generic extension sinks."
    ),
    (
        "reviewed source families include TinkeringHelpers.CheckMakersMark, "
        "XRLCore.RestoreModsLoadedAsync, PopulationManager.WishFindBlueprint, "
        "Shrine.DesecrateShrine, ModInfo.ConfirmFailure, CodaSystem.EndGamePrompt, "
        "Physics.ProcessTargetedMove, "
        "CyberneticsTerminal2.AskLowLevelHack, conversation reward/share routes, "
        "ArkCore.TryOpen, and ShevaStarshipControl.AttemptLaunch"
    ),
    (
        "Some fixed leaves already exist in ui-popup.ja.json, but focused owner-route "
        "tests are required before promoting these whole families."
    ),
]
ISSUE719_TINKERING_MAKERS_MARK_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 TinkeringHelpers.CheckMakersMark review closes maker's mark "
        "picker and color-picker prompts through a CheckMakersMark owner patch and "
        "the Popup.PickOption/ShowColorPicker routes."
    ),
    "Mods/QudJP/Assemblies/src/Patches/TinkeringHelpersMakersMarkTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowColorPickerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringHelpersMakersMarkTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.World.Tinkering/TinkeringHelpers.cs lines 72-101",
]
ISSUE719_TINKERING_MAKERS_MARK_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Tinkering/TinkeringHelpers.cs::"
            "TinkeringHelpers.CheckMakersMark(GameObject,GameObject,IModification,string)"
        ),
    }
)
ISSUE719_SAVES_API_FATAL_SAVE_ERROR_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 SavesAPI.FatalSaveError review closes the save-directory "
        "fatal error body, title, and Quit button through a FatalSaveError owner "
        "scope plus the PopupMessage producer route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SavesApiFatalSaveErrorTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SavesApiFatalSaveErrorTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: Qud.API/SavesAPI.cs lines 123-150",
]
ISSUE719_SAVES_API_FATAL_SAVE_ERROR_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.API/SavesAPI.cs::SavesAPI.FatalSaveError(Exception,string)",
    }
)
ISSUE719_MOD_DISGUISE_BEING_APPLIED_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 ModDisguise.BeingAppliedBy review closes the player disguise "
        "selection failure popup and picker title through an exact owner scope."
    ),
    (
        "The owner translator intentionally preserves generated blueprint display-name "
        "options; display-name translation remains owned by the display-name routes."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ModDisguiseBeingAppliedPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModDisguiseBeingAppliedPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.World.Parts/ModDisguise.cs lines 53-99",
]
ISSUE719_MOD_DISGUISE_BEING_APPLIED_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/ModDisguise.cs::ModDisguise.BeingAppliedBy(GameObject,GameObject)",
    }
)
ISSUE719_SHRINE_DESECRATE_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes Shrine.DesecrateShrine fixed popup/pick-option leaves "
        "through the existing generic popup routes and shipped ui-popup dictionary entries."
    ),
    "decompiled owner source: XRL.World.Parts/Shrine.cs lines 171-202",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_POPULATION_ROLL_ONE_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes PopulationManager.RollOneFrom through the existing "
        "SingleCallsiteOwnerPopupTranslationPatch owner route for the generated "
        "population error popup frame."
    ),
    "decompiled owner source: XRL/PopulationManager.cs lines 472-485",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_POPULATION_WISH_FIND_BLUEPRINT_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes PopulationManager.WishFindBlueprint through the "
        "SingleCallsiteOwnerPopupTranslationPatch owner route for the debug wish "
        "blueprint population-table report frames."
    ),
    "decompiled owner source: XRL/PopulationManager.cs lines 1019-1081",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_MODINFO_CONFIRM_FAILURE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes ModInfo.ConfirmFailure through the existing "
        "ModInfoTranslationPatch transpiler and ModManagementSemanticPipeline "
        "literal translations for the error title, retry/workshop commands, "
        "extra-error suffix, and clipboard forwarding note."
    ),
    "decompiled owner source: XRL/ModInfo.cs lines 489-528",
    "Mods/QudJP/Assemblies/src/Patches/ModInfoTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ModManagementSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModInfoTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_MODINFO_CONFIRM_FAILURE_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL/ModInfo.cs::ModInfo.ConfirmFailure()",
    }
)
ISSUE719_XRLCORE_RESTORE_MODS_LOADED_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes XRLCore.RestoreModsLoadedAsync through an exact "
        "async-state-machine transpiler that translates the fixed mod-configuration "
        "popup frames and option labels while preserving generated mod titles."
    ),
    "decompiled owner source: XRL.Core/XRLCore.cs lines 3319-3344",
    "Mods/QudJP/Assemblies/src/Patches/XrlCoreRestoreModsLoadedTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XrlCoreRestoreModsLoadedTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_XRLCORE_RESTORE_MODS_LOADED_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.Core/XRLCore.cs::XRLCore.RestoreModsLoadedAsync(List<string>)",
    }
)
ISSUE719_CONVERSATIONS_API_REWARD_PICK_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes ConversationsAPI.chooseOneItem through the existing "
        "PopupPickOptionTranslationPatch route and shipped ui-popup dictionary "
        "entry for the fixed reward prompt."
    ),
    "decompiled owner source: Qud.API/ConversationsAPI.cs lines 245-258",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_DYNAMIC_QUEST_REWARD_CHOICE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes DynamicQuestRewardElement_ChoiceFromPopulation.award "
        "by splitting the fixed popup title from the generated reward option payloads."
    ),
    (
        "The fixed 'Choose a reward' title is covered by PopupPickOptionTranslationPatch "
        "and the shipped ui-popup dictionary entry; the option strings are composed "
        "from GameObject.GetDisplayName(... AsIfKnown: true) or DisplayNameOnlyDirect "
        "plus a count suffix, so they remain GameObject display-name owner output."
    ),
    "decompiled owner source: XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs lines 27-63",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_CODA_ENDGAME_PROMPT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes CodaSystem.EndGamePrompt through existing AskString "
        "and death-reason dictionary routes for the fixed end-game prompt and "
        "fixed death reason."
    ),
    "decompiled owner source: XRL/CodaSystem.cs lines 66-84",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DeathReasonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-death.ja.json",
]
ISSUE719_CONVERSATION_ENDGAME_CONFIRM_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes EndGame.HandleEvent through the AskString producer "
        "template for the dynamic confirm token in 'End game?' prompts."
    ),
    "decompiled owner source: XRL.World.Conversations.Parts/EndGame.cs lines 134-153",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_CONVERSATION_GIVE_ARTIFACT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes GiveArtifact.HandleEvent through the generic Popup.Show "
        "dictionary route and PopupPickOption/PickGameObject title dictionary route."
    ),
    "decompiled owner source: XRL.World.Conversations.Parts/GiveArtifact.cs lines 31-48",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_CONVERSATION_RESHEPH_SECRET_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes GiveReshephSecret.HandleEvent through the existing "
        "conversation reward owner patch plus PopupPickSeveral/PickOption dictionary titles."
    ),
    "decompiled owner source: XRL.World.Conversations.Parts/GiveReshephSecret.cs lines 35-58",
    "Mods/QudJP/Assemblies/src/Patches/ConversationRewardPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickSeveralTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationRewardPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_CONVERSATION_WATER_RITUAL_SELL_SECRET_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes WaterRitualSellSecret.Share through PickOption "
        "dictionary titles; its no-reputation failure remains covered by the "
        "WaterRitualPopup owner patch."
    ),
    "decompiled owner source: XRL.World.Conversations.Parts/WaterRitualSellSecret.cs lines 54-62",
    "Mods/QudJP/Assemblies/src/Patches/WaterRitualPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_RESIDUAL_PURE_POPUP_REMAINDER_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL/PopulationManager.cs::PopulationManager.WishFindBlueprint(string)",
        "XRL/ModInfo.cs::ModInfo.ConfirmFailure()",
        (
            "XRL.World.Parts/Physics.cs::"
            "Physics.ProcessTargetedMove(Cell,string,string,string,int?,bool,bool,bool,bool,bool,bool,"
            "string,string,GameObject)"
        ),
        "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AskLowLevelHack(GameObject)",
        "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.AttemptLaunch(GameObject)",
        "Qud.API/JournalAPI.cs::JournalAPI.WishGospelAccomplishments()",
        "XRL.World.Parts/GripChange.cs::GripChange.TryChooseGrip(GameObject)",
        "XRL.World.Parts/ArkCore.cs::ArkCore.TryOpen(GameObject)",
        "XRL.World.Parts/Vehicle.cs::Vehicle.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts/CyberneticsCathedra.cs::CyberneticsCathedra.HandleEvent(CommandEvent)",
        "XRL.World.Parts/IZoneLandmark.cs::IZoneLandmark.WishCurrent()",
        (
            "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs::"
            "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport(GameObject,IEvent)"
        ),
        "XRL.World.Quests/ReclamationSystem.cs::ReclamationSystem.HandleEvent(EnteringZoneEvent)",
        "XRL.World.Parts/RecoilAbility.cs::RecoilAbility.HandleEvent(CommandEvent)",
        "XRL.World.Parts/ModExtradimensional.cs::ModExtradimensional.MakeExtradimensional()",
        "Extensions.cs::Extensions.ShowSuccess(this XRL.World.GameObject,string,bool)",
    }
)
ISSUE719_RESIDUAL_FRAME_DOES_PROMOTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review promotes fixed pure MessageFrame and pure Does "
        "families already covered by existing owner-route or dictionary-focused tests."
    ),
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DeathReasonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsTerminalTextTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsTerminalInterfacePopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationRewardPopupTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_RESIDUAL_FRAME_DOES_PROMOTION_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Physics.cs::Physics.UpdateTemperature()",
        (
            "XRL.UI/CyberneticsScreenMainMenu.cs::"
            "CyberneticsScreenMainMenu.CyberneticsScreenMainMenu()"
        ),
        "XRL.World.Parts/LootOnStep.cs::LootOnStep.SteppedOn(GameObject,bool)",
        (
            "XRL.World.Parts/NeutronFluxContainment.cs::"
            "NeutronFluxContainment.GetWarningMessage()"
        ),
        (
            "XRL.World.Parts/CyberneticsTerminal2.cs::"
            "CyberneticsTerminal2.AttemptInterface(GameObject,IEvent)"
        ),
        (
            "XRL.World.Parts/ExtradimensionalHunterSummoner.cs::"
            "ExtradimensionalHunterSummoner.Summon(int)"
        ),
        "XRL.World.Parts/Combat.cs::Combat.SwoopAttack(GameObject,string)",
        (
            "XRL.World.Parts/Shrine.cs::"
            "Shrine.PerformDesecration(GameObject,bool,bool,bool,bool)"
        ),
        "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.PsychicPresenceMessage(int,bool)",
        "XRL.World.Parts/Skills.cs::Skills.WishSkillAdd(string)",
        "XRL.World.Parts/Skills.cs::Skills.WishSkillAll()",
        (
            "XRL.World.Conversations/ConversationDelegates.cs::"
            "ConversationDelegates.AwardXP(DelegateContext)"
        ),
        "XRL.World.Parts.Mutation/SpiderWebs.cs::SpiderWebs.HandleEvent(LeftCellEvent)",
        "XRL.World.Parts/BaetylHostility.cs::BaetylHostility.CheckBaetylHostility()",
        "XRL.World.Parts/PetFrondzie.cs::PetFrondzie.taunt(GameObject)",
        (
            "XRL.World.Parts/PetEbenshabat.cs::"
            "PetEbenshabat.HandleEvent(AfterLevelGainedEvent)"
        ),
        (
            "XRL.World.Parts/SpaceTimeVortex.cs::"
            "SpaceTimeVortex.SpaceTimeAnomalyPeriodicEvents()"
        ),
        (
            "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
            "CyberneticsPrecisionForceLathe.HandleEvent(ReplaceThrownWeaponEvent)"
        ),
        "XRL.World.Parts/HeatSelfOnFreeze.cs::HeatSelfOnFreeze.FireEvent(Event)",
        (
            "XRL.World.Parts/AIBarathrumShuttle.cs::"
            "AIBarathrumShuttle.ActionShipLaunch(GoalHandler)"
        ),
        "XRL.World.Parts/Mutations.cs::Mutations.WishMutationAdd(string,string)",
        (
            "XRL.World.Units/GameObjectBaetylUnit.cs::"
            "GameObjectBaetylUnit.GiveRewards(GameObject,int,int)"
        ),
        "XRL.World.Parts/NephalProperties.cs::NephalProperties.AbsorbChords(GameObject)",
        (
            "XRL.World.Parts/LiquidVolume.cs::"
            "LiquidVolume.CleaningMessage(GameObject,List<GameObject>,List<string>,GameObject,LiquidVolume,bool)"
        ),
        (
            "XRL.World.Parts/LiquidVolume.cs::"
            "LiquidVolume.ProcessContact(GameObject,bool,bool,bool,GameObject,bool,int)"
        ),
    }
)
ISSUE719_RESIDUAL_FRAME_DOES_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps unresolved pure MessageFrame and pure Does "
        "producer families to runtime-required because whole-family static rows mix "
        "second-to-third-person conversion, death reasons, generated object/liquid names, "
        "wish/debug commands, UsePopup MessageFrame routing, conversation rewards, and "
        "UI/body text that need live owner-route evidence before promotion."
    ),
    (
        "reviewed pure Does families: Domination.ProcessTarget, "
        "Physics.UpdateTemperature, CyberneticsScreenMainMenu, "
        "TrembleEarthquakes.RocksFall, LootOnStep.SteppedOn, "
        "and NeutronFluxContainment.GetWarningMessage"
    ),
    (
        "reviewed pure MessageFrame families: PetFrondzie.taunt, "
        "SpaceTimeVortex.SpaceTimeAnomalyPeriodicEvents, "
        "ExtradimensionalHunterSummoner.Summon, Combat.SwoopAttack, Shrine.PerformDesecration, "
        "PsychicHunterSystem.PsychicPresenceMessage, pet/reward/wish/debug routes, and "
        "small owner event frames."
    ),
]
ISSUE719_DOMINATION_PROCESS_TARGET_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes Domination.ProcessTarget through an exact owner scope "
        "and MessageQueueSemanticPipeline coverage for the domination failure "
        "message frames."
    ),
    "decompiled owner source: XRL.World.Parts.Mutation/Domination.cs lines 334-384",
    "Mods/QudJP/Assemblies/src/Patches/DominationProcessTargetTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DominationProcessTargetTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_DOMINATION_PROCESS_TARGET_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts.Mutation/Domination.cs::Domination.ProcessTarget(GameObject,ref string)",
    }
)
ISSUE719_RESIDUAL_FRAME_DOES_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/TrembleEarthquakes.cs::TrembleEarthquakes.RocksFall(Zone)",
    }
)
ISSUE719_RESIDUAL_MIXED_POPUP_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps mixed AddPlayerMessage+Popup and Does+Popup "
        "producer families runtime-required because the static rows combine queue, popup, "
        "inventory action, object-name composition, debug, and generated owner branches."
    ),
    (
        "reviewed source families: Stomach.FireEvent, "
        "ElevatorSwitch.FireEvent, BiomeManager.DisplaySurfaceDistribution, "
        "Examiner.HandleEvent(InventoryActionEvent), "
        "TinkerItem.HandleEvent(InventoryActionEvent), FixitSpray.HandleEvent, "
        "MagnetizedApplicator.HandleEvent, and VehicleMeleeInfiltration.TryInfiltrate"
    ),
]
ISSUE719_RESIDUAL_MIXED_POPUP_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
        "XRL.World.Parts/ElevatorSwitch.cs::ElevatorSwitch.FireEvent(Event)",
        (
            "XRL.World.Biomes/BiomeManager.cs::"
            "BiomeManager.DisplaySurfaceDistribution(string)"
        ),
        "XRL.World.Parts/Examiner.cs::Examiner.HandleEvent(InventoryActionEvent)",
        "XRL.World.Parts/TinkerItem.cs::TinkerItem.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_STOMACH_FIRE_EVENT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Stomach.FireEvent is covered by owner-route queue translation plus "
        "existing popup and Does/dictionary routes for the fixed water/thirst/vomit frames."
    ),
    (
        "owner patch: Mods/QudJP/Assemblies/src/Patches/StomachTranslationPatch.cs "
        "targets XRL.World.Parts.Stomach|FireEvent|System.Boolean|XRL.World.Event and "
        "translates AddWater moisture drain, overdrinking, and overdrinking-vomit "
        "queue frames before the generic message-log sink."
    ),
    (
        "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/StomachTranslationPatchTests.cs "
        "covers StomachMoistureBody, StomachMoistureThroat, StomachOverdrink, "
        "StomachOverdrinkVomiting, color wrappers, direct marker stripping, owner-absent "
        "queue pass-through, and unrelated owner-active queue pass-through."
    ),
    (
        "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs "
        "covers StomachTranslationPatch target resolution for "
        "XRL.World.Parts.Stomach|FireEvent|System.Boolean|XRL.World.Event."
    ),
    (
        "dictionary/route evidence: world-parts.ja.json owns Stomach popup/status/vomit "
        "fixed literals and DoesVerbFamilyTests covers third-person vomits everywhere."
    ),
]
ISSUE719_STOMACH_FIRE_EVENT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Stomach.cs::Stomach.FireEvent(Event)",
    }
)
ISSUE719_FIXIT_SPRAY_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 FixitSpray.HandleEvent closure: route-local sticky-goop "
        "Popup.Show branches are covered by SingleCallsiteOwnerPopupTranslationPatch, "
        "self/fixed failure popups are covered by ui-popup/world-parts leaves, "
        "and the object-covered branch is also covered by the sticky-goop DoesVerb route."
    ),
    "decompiled owner source: XRL.World.Parts/FixitSpray.cs lines 24-91",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_FIXIT_SPRAY_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/FixitSpray.cs::FixitSpray.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_MAGNETIZED_APPLICATOR_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 MagnetizedApplicator.HandleEvent closure: fixed failure "
        "popups are covered by ui-popup leaves, DoesVerb handles the does-nothing "
        "and becomes-magnetized frames, and SingleCallsiteOwnerPopupTranslationPatch "
        "owns the magnetic-charge crumble popup."
    ),
    "decompiled owner source: XRL.World.Parts/MagnetizedApplicator.cs lines 20-68",
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_MAGNETIZED_APPLICATOR_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/MagnetizedApplicator.cs::"
            "MagnetizedApplicator.HandleEvent(InventoryActionEvent)"
        ),
    }
)
ISSUE719_INVENTORY_DROP_ASK_NUMBER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes Inventory.HandleEvent drop-count popup through the "
        "generic PopupAskNumberTranslationPatch route and the shipped ui-popup "
        "dictionary leaf for the fixed 'How many do you want to drop?' prompt."
    ),
    "decompiled owner source: XRL.World.Parts/Inventory.cs line 2449",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskNumberTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_INVENTORY_DROP_ASK_NUMBER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Inventory.cs::Inventory.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_EXAMINER_HANDLE_EVENT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes Examiner.HandleEvent inventory-action popups through "
        "the existing ExaminerTranslationPatch owner route for broken-item, "
        "owned-examine, container-owned-examine, and confused failure prompts."
    ),
    "decompiled owner source: XRL.World.Parts/Examiner.cs lines 399-465",
    "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ExaminerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_EXAMINER_HANDLE_EVENT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Examiner.cs::Examiner.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_TINKER_ITEM_HANDLE_EVENT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes TinkerItem.HandleEvent inventory-action disassembly "
        "popups through the existing TinkerItemTranslationPatch owner route for "
        "owned item, container-owned item, and disassembly confirmation prompts."
    ),
    "decompiled owner source: XRL.World.Parts/TinkerItem.cs lines 321-385",
    "Mods/QudJP/Assemblies/src/Patches/TinkerItemTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkerItemTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_TINKER_ITEM_HANDLE_EVENT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/TinkerItem.cs::TinkerItem.HandleEvent(InventoryActionEvent)",
    }
)
ISSUE719_RESIDUAL_QUEUE_DOES_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps pure AddPlayerMessage and "
        "Does+EmitMessage producer families runtime-required because the static "
        "rows combine debug queues, generic message queue helpers, generated object "
        "names, sound-log diagnostics, chat data, and Does-based emitted combat/world "
        "messages."
    ),
    (
        "reviewed source families: CyberneticsButcherableCybernetic.AttemptButcher, "
        "Chat.PerformChat, FungalInfection.FireEvent, "
        "VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent), "
        "SoundManager._PlaySound, SoundManager._PlayWorldSound, "
        "Interior.HandleEvent(TookDamageEvent), MessageQueue.AddPlayerMessage(char), "
        "and FindASiteDynamicQuestManager.DynamicQuestWhere"
    ),
]
ISSUE719_FIND_SITE_DYNAMIC_QUEST_WHERE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes FindASiteDynamicQuestManager.DynamicQuestWhere through "
        "WishCommandQueueTranslationPatch on the exact WishCommand debug owner route."
    ),
    "decompiled owner source: XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs lines 157-170",
    "Mods/QudJP/Assemblies/src/Patches/WishCommandQueueTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WishCommandQueueTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_FUNGAL_INFECTION_FIRE_EVENT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes FungalInfection.FireEvent through existing Does-verb "
        "message-frame and message-pattern coverage for the fungal cure emitted "
        "messages."
    ),
    "decompiled owner source: XRL.World.Parts/FungalInfection.cs lines 59-124",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
]
ISSUE719_FUNGAL_INFECTION_FIRE_EVENT_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/FungalInfection.cs::FungalInfection.FireEvent(Event)",
    }
)
ISSUE719_RESIDUAL_QUEUE_DOES_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/CyberneticsButcherableCybernetic.cs::"
            "CyberneticsButcherableCybernetic.AttemptButcher"
            "(GameObject,bool,bool,bool,int,Cell,List<GameObject>)"
        ),
        (
            "SoundManager.cs::"
            "SoundManager._PlaySound(string,float,float,SoundRequest.SoundEffectType)"
        ),
        (
            "SoundManager.cs::"
            "SoundManager._PlayWorldSound(string,float,float,float,float,Point2D)"
        ),
        "XRL.World.Parts/Interior.cs::Interior.HandleEvent(TookDamageEvent)",
        "XRL.Messages/MessageQueue.cs::MessageQueue.AddPlayerMessage(string,char,bool)",
    }
)
ISSUE719_CHAT_PERFORM_CHAT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Chat.PerformChat closure: bracket/star Says payloads remain "
        "runtime/data pass-through, while the generated says wrapper is covered "
        "by the repository say message frame and localized Chat Says XML payloads."
    ),
    "decompiled owner source: XRL.World.Parts/Chat.cs lines 171-218",
    "scripts/static_producer_closure.py",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Localization/ObjectBlueprints/Furniture.jp.xml",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
]
ISSUE719_CHAT_PERFORM_CHAT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Chat.cs::Chat.PerformChat(GameObject,bool)",
    }
)
ISSUE719_RESIDUAL_MESSAGE_MIXED_REMAINDER_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps the final heterogeneous producer message "
        "families runtime-required because the static rows combine EmitMessage, popup, "
        "MessageFrame, AddPlayerMessage, Does, tutorial popup, wish/debug, object-name, "
        "and generated world-message branches that cannot be promoted as whole families."
    ),
    (
        "reviewed source families: ShevaStarshipControl.CheckTimer, "
        "SpaceTimeVortex.ApplyVortex, Carapace.Loosen, "
        "GolemQuestMound.DisplayOptions, "
        "CyberneticsHolographicVisage.SelectVisage, "
        "LiquidWarmStatic.WishWarmEffectSpec, LiquidWarmStatic.GlitchLiquidComponents, "
        "LiquidWarmStatic.WishWarmEffect, DesalinationPellet.HandleEvent, and "
        "FadeText.Update"
    ),
]
ISSUE719_CAMPFIRE_EXTINGUISH_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Campfire.Extinguish closure: the actor branch is covered by "
        "the repository extinguish MessageFrame leaf, and the object branch is "
        "covered by the existing are-extinguished-by Does frame through "
        "GameObjectEmitMessageTranslationPatch."
    ),
    "decompiled owner source: XRL.World.Parts/Campfire.cs lines 1928-1937",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/XDidYTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
]
ISSUE719_CAMPFIRE_EXTINGUISH_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/Campfire.cs::Campfire.Extinguish(GameObject,GameObject)",
    }
)
ISSUE719_HOLOGRAPHIC_VISAGE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 CyberneticsHolographicVisage.SelectVisage closure: the exact "
        "owner method now routes its PickOption title/options through "
        "PopupPickOptionTranslationPatch and its glissade EmitMessage through "
        "the existing Holographic Visage DoesVerb family."
    ),
    (
        "source review: SelectVisage builds the fixed title 'Choose a model faction "
        "for your holographic glamour.', the fixed option 'none', visible faction "
        "DisplayName options, and the glissade-of-light EmitMessage after a faction "
        "selection."
    ),
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
]
ISSUE719_HOLOGRAPHIC_VISAGE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/CyberneticsHolographicVisage.cs::"
            "CyberneticsHolographicVisage.SelectVisage(GameObject)"
        ),
    }
)
ISSUE719_CARAPACE_LOOSEN_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Carapace.Loosen closure: player-visible Popup.Show loosen "
        "messages are owned by CarapaceTranslationPatch, while non-player "
        "EmitMessage loosen lines are covered by the existing loosen DoesVerb "
        "and message-log routes."
    ),
    "decompiled owner source: XRL.World.Parts.Mutation/Carapace.cs lines 197-230",
    "Mods/QudJP/Assemblies/src/Patches/CarapaceTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]
ISSUE719_CARAPACE_LOOSEN_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts.Mutation/Carapace.cs::Carapace.Loosen(bool)",
    }
)
ISSUE719_RESIDUAL_MESSAGE_MIXED_REMAINDER_RUNTIME_FAMILIES: Final[
    frozenset[str]
] = frozenset(
    {
        "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.CheckTimer()",
        "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.DisplayOptions(GameObject)",
        (
            "XRL.Liquids/LiquidWarmStatic.cs::"
            "LiquidWarmStatic.WishWarmEffectSpec(string)"
        ),
        (
            "XRL.Liquids/LiquidWarmStatic.cs::"
            "LiquidWarmStatic.GlitchLiquidComponents(GameObject,string,int,bool)"
        ),
        "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffect()",
        "XRL.UI/FadeText.cs::FadeText.Update()",
    }
)
ISSUE719_SPACE_TIME_VORTEX_APPLY_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 SpaceTimeVortex.ApplyVortex closure: vortex-contact and "
        "sucked-into message frames are covered by message-log/message-frame "
        "patterns, and the companion popup branch is covered by the existing "
        "single-callsite owner popup translator."
    ),
    "decompiled owner source: XRL.World.Parts/SpaceTimeVortex.cs lines 373-438",
    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_SPACE_TIME_VORTEX_APPLY_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.ApplyVortex(GameObject)",
    }
)
ISSUE719_WORLD_PART_MIXED_STATIC_GAP_EVIDENCE_BY_FAMILY: Final[
    dict[str, list[str]]
] = {
    "XRL.World.Parts/ShevaStarshipControl.cs::ShevaStarshipControl.CheckTimer()": [
        (
            "Issue #719 ShevaStarshipControl.CheckTimer review reclassifies the "
            "row as a static implementation gap because the exact owner method "
            "builds the launch countdown EmitMessage/Popup text and the post-launch "
            "starship-entry failure message."
        ),
        (
            "Existing dictionaries cover several Exodus countdown leaves and the "
            "moor-rattle popup, but CheckTimer still owns unverified countdown, "
            "launch-state, and InteriorBlockEntrance message branches."
        ),
        "decompiled owner source: XRL.World.Parts/ShevaStarshipControl.cs lines 108-180",
        "existing dictionaries: ui-phase3d-endings.ja.json, messages.ja.json, ui-popup.ja.json",
    ],
    "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.FireEvent(Event)": [
        (
            "Issue #719 MagazineAmmoLoader.FireEvent review reclassifies the row "
            "as a static implementation gap because the exact old-event owner "
            "handles SupplyIntegratedHostWithAmmo popups and transfer MessageFrames."
        ),
        (
            "Adjacent MagazineAmmoLoader Load/CheckLoadAmmo/CommandReload routes "
            "already have focused coverage, but this SupplyIntegratedHostWithAmmo "
            "old-event branch still needs owner-route handling for AskNumber text "
            "and generated ammo transfer frames."
        ),
        "decompiled owner source: XRL.World.Parts/MagazineAmmoLoader.cs lines 630-686",
        (
            "existing routes: MagazineAmmoLoader.HandleEvent(LoadAmmoEvent), "
            "HandleEvent(CheckLoadAmmoEvent), Load, and CommandReloadEvent overlays"
        ),
    ],
    "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.ApplyVortex(GameObject)": [
        (
            "Issue #719 SpaceTimeVortex.ApplyVortex review reclassifies the row "
            "as a static implementation gap because the exact owner method mixes "
            "vortex-contact EmitMessage, XDidYToZ, and companion Popup branches."
        ),
        (
            "Existing tests cover the companion sucked popup and two-vortices "
            "Does-family text, but the ApplyVortex owner still needs route-local "
            "coverage for the full generated object/vortex transfer frame set."
        ),
        "decompiled owner source: XRL.World.Parts/SpaceTimeVortex.cs lines 373-430",
        (
            "existing tests: DoesVerbFamilyTests.Translate_SpaceTimeVortexFamily "
            "and SingleCallsiteOwnerPopupTranslationPatchTests SpaceTimeVortexCompanionSucked"
        ),
    ],
}
ISSUE719_WORLD_FACTORY_DISPLAY_NAME_DATA_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 WorldFactory.LoadWorldNode review promotes the row because "
        "the method only loads world DisplayName attributes from XML data."
    ),
    (
        "QudJP owns the localized data route through Mods/QudJP/Localization/Worlds.jp.xml, "
        "and runtime zone-display output is separately covered by ZoneDisplayNameTranslationPatch."
    ),
    "decompiled source: XRL.World/WorldFactory.cs lines 348-356",
    "localized data: Mods/QudJP/Localization/Worlds.jp.xml",
    "tests: ZoneDisplayNameTranslationPatchTests.cs and MessageLogProducerTranslationHelpersTests.cs",
]
ISSUE719_WORLD_FACTORY_DISPLAY_NAME_DATA_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World/WorldFactory.cs::WorldFactory.LoadWorldNode(XmlTextReader)",
    }
)
ISSUE719_GAMEOBJECT_REGENERA_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 GameObject.FireEvent(Regenera) review promotes the row because "
        "the Regenera branch is already covered by a dedicated owner patch."
    ),
    (
        "GameObjectRegeneraTranslationPatch tracks only Event.ID == Regenera, translates "
        "cure/malady messages through message patterns, and handles regenerated limb frames."
    ),
    "decompiled source: XRL.World/GameObject.cs lines 7570-7638",
    "patch: Mods/QudJP/Assemblies/src/Patches/GameObjectRegeneraTranslationPatch.cs",
    "tests: CombatAndLogMessageQueuePatchTests GameObjectRegenera_* and TargetMethodResolutionTests",
]
ISSUE719_GAMEOBJECT_REGENERA_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World/GameObject.cs::GameObject.FireEvent(Event)",
    }
)
ISSUE719_VILLAGE_DYNAMIC_QUEST_REWARD_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 VillageDynamicQuestContext.getQuestReward review reclassifies the row "
        "as a static implementation gap because the owner directly assigns generated reward text."
    ),
    (
        "The method creates a Blank Recoiler, assigns Render.DisplayName to "
        "villageSnapshot name + ' recoiler', and adds villagers-of reputation rewards."
    ),
    (
        "Existing villagers-of and dynamic quest item-name routes cover adjacent shapes, "
        "but this quest-reward display-name assignment still needs owner handling."
    ),
    "decompiled source: XRL.World/VillageDynamicQuestContext.cs lines 100-145",
]
ISSUE719_VILLAGE_DYNAMIC_QUEST_REWARD_STATIC_GAP_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World/VillageDynamicQuestContext.cs::VillageDynamicQuestContext.getQuestReward()",
    }
)
ISSUE719_GOLEM_MOUND_DISPLAY_OPTIONS_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "GolemQuestMound.DisplayOptions is covered by an owner-route transpiler that "
        "translates the route-local Build command labels while preserving popup "
        "selection text, hotkey descriptors, commands, and sound identifiers."
    ),
    (
        "DisplayOptions feeds Popup.PickOption with the mound Description.Short, "
        "GolemQuestSelection option text, and a route-local Build command label."
    ),
    (
        "GolemQuestMoundDisplayOptionsTranslationPatch only rewrites the exact "
        "Build literals; the description and quest selection option text remain "
        "owned by their upstream producers."
    ),
    "patch: Mods/QudJP/Assemblies/src/Patches/GolemQuestMoundDisplayOptionsTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/GolemQuestMoundDisplayOptionsTranslationPatchTests.cs",
    "signature guard: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Parts/GolemQuestMound.cs lines 134-179",
]
ISSUE719_GOLEM_MOUND_DISPLAY_OPTIONS_STATIC_GAP_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.DisplayOptions(GameObject)",
    }
)
ISSUE719_AUTOACT_GET_DESCRIPTION_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 AutoAct.GetDescription closure: ActionEffectDescriptionReturnTranslationPatch "
        "now targets the exact AutoAct.GetDescription(string,OngoingAction) owner route and translates "
        "the fixed action labels returned to GameObject.GenerateSpotMessage."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/ActionEffectDescriptionReturnTranslationPatch.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/ActionEffectDescriptionReturnTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.World.Capabilities/AutoAct.cs lines 346-363",
]
ISSUE719_AUTOACT_GET_DESCRIPTION_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Capabilities/AutoAct.cs::AutoAct.GetDescription(string,OngoingAction)",
    }
)
ISSUE719_GAMEOBJECT_HOSTILE_SPOT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes GameObject.ArePerceptibleHostilesNearby hostile-spot "
        "popup/queue messages through the GameObjectSpot owner route."
    ),
    (
        "GameObjectSpotTranslationPatch targets ArePerceptibleHostilesNearby and now "
        "feeds both MessageQueue and Popup.Show spot messages through the same "
        "message-pattern route with owner observability."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/GameObjectSpotTranslationPatch.cs",
    "implementation: Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "tests: GameObjectSpot_TranslatesSpotMessage_WhenPatched",
    "tests: GameObjectSpot_TranslatesSpotPopup_WhenPatched",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_GAMEOBJECT_HOSTILE_SPOT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World/GameObject.cs::"
            "GameObject.ArePerceptibleHostilesNearby(bool,bool,string,OngoingAction,string,int,int,bool,bool)"
        ),
        "XRL.World/GameObject.cs::XRL.World.GameObject.ArePerceptibleHostilesNearby",
    }
)
ISSUE719_WISH_DEBUG_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 static review reclassifies remaining WishCommand/debug rows "
        "as implementation gaps: each owner is statically identified, but the "
        "debug route still owns fixed or generated English text."
    ),
    (
        "PointedAsteriskBuilder.AsteriskWish assigns a fixed 10-pointed asterisk display name "
        "inside the slynthasterisk WishCommand helper."
    ),
    (
        "IZoneLandmark.WishCurrent owns the landmark debug popup frame and composes "
        "preposition/article/owner/display-name text."
    ),
    "ModExtradimensional.MakeExtradimensional owns the WishCommand PickGameObject title.",
    (
        "decompiled owner sources: XRL.World.Parts/PointedAsteriskBuilder.cs lines 121-154; "
        "XRL.World.Parts/IZoneLandmark.cs lines 126-140; "
        "XRL.World.Parts/ModExtradimensional.cs lines 628-632"
    ),
]
ISSUE719_IZONE_LANDMARK_WISH_CURRENT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 IZoneLandmark.WishCurrent review closes the landmark WishCommand "
        "popup through SingleCallsiteOwnerPopupTranslationPatch."
    ),
    (
        "The owner translator handles the fixed missing-landmark failure and the "
        "generated landmark location frame while preserving the composed landmark capture."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled owner source: XRL.World.Parts/IZoneLandmark.cs lines 126-140",
]
ISSUE719_IZONE_LANDMARK_WISH_CURRENT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/IZoneLandmark.cs::IZoneLandmark.WishCurrent()",
    }
)
ISSUE719_WISH_DEBUG_STATIC_GAP_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/PointedAsteriskBuilder.cs::PointedAsteriskBuilder.AsteriskWish()",
    }
)
ISSUE719_WORLD_PART_PICKOPTION_DICTIONARY_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 world-part popup closure: PopupPickOptionTranslationPatch covers the "
        "GripChange style picker, the RecoilAbility PickGameObject title, and the "
        "ModExtradimensional WishCommand PickGameObject title through reviewed ui-popup and "
        "skill-name dictionary leaves."
    ),
    (
        "RecoilAbility's no-recoiler failure is covered by PopupShowTranslationPatch with "
        "the reviewed ui-popup dictionary leaf."
    ),
    "implementation: Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "implementation: Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
    "localization source: Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    "localization source: Mods/QudJP/Localization/Dictionaries/ui-default.ja.json",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "tests: Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
    "decompiled source: XRL.World.Parts/GripChange.cs lines 104-108",
    "decompiled source: XRL.World.Parts/RecoilAbility.cs lines 51-63",
    "decompiled source: XRL.World.Parts/ModExtradimensional.cs lines 628-632",
]
ISSUE719_WORLD_PART_PICKOPTION_DICTIONARY_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.World.Parts/GripChange.cs::GripChange.TryChooseGrip(GameObject)",
        "XRL.World.Parts/RecoilAbility.cs::RecoilAbility.HandleEvent(CommandEvent)",
        "XRL.World.Parts/ModExtradimensional.cs::ModExtradimensional.MakeExtradimensional()",
    }
)
ISSUE719_PHYSICS_TARGETED_MOVE_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Physics.ProcessTargetedMove review promotes the row because the only player-facing "
        "popup literals are either owner-scoped or localized data."
    ),
    (
        "The attack confirmation is covered by SingleCallsiteOwnerPopupTranslationPatch through "
        "PhysicsProcessTargetedMoveOwner and focused ShowYesNo tests for English and already-Japanese names."
    ),
    (
        "The NoTeleport popup body is read from the NoTeleport tag/property, and the shipped "
        "HiddenObjects.jp.xml overlay localizes that data leaf."
    ),
    "decompiled source: XRL.World.Parts/Physics.cs lines 3899-3972",
    "patch: Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
    "tests: SingleCallsiteOwnerPopupTranslationPatchTests ProcessTargetedMove cases",
    "data: Mods/QudJP/Localization/ObjectBlueprints/HiddenObjects.jp.xml NoTeleport tag",
]
ISSUE719_PHYSICS_TARGETED_MOVE_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        (
            "XRL.World.Parts/Physics.cs::"
            "Physics.ProcessTargetedMove(Cell,string,string,string,int?,bool,bool,bool,bool,bool,bool,"
            "string,string,GameObject)"
        ),
    }
)
ISSUE719_WORLD_GENERATION_SCREEN_QUOTES_DATA_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 WorldGenerationScreen._ShowWorldGenerationScreen review promotes the row because "
        'the visible quote/attribution text is data-owned by BookUI.Books["Quotes"].'
    ),
    (
        'The method assigns empty/space placeholders, then selects BookUI.Books["Quotes"] page lines '
        "and sends those already-loaded lines to quoteText and attributionText."
    ),
    (
        'BookUI.InitBooks loads books from DataManager.YieldXMLStreamsWithRoot("books"), and '
        "Mods/QudJP/Localization/Books.jp.xml ships the Quotes book pages in Japanese."
    ),
    "decompiled source: Qud.UI/WorldGenerationScreen.cs lines 217-260",
    "decompiled source: XRL.UI/BookUI.cs lines 79-151",
    'data: Mods/QudJP/Localization/Books.jp.xml book ID="Quotes"',
]
ISSUE719_WORLD_GENERATION_SCREEN_QUOTES_DATA_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/WorldGenerationScreen.cs::WorldGenerationScreen._ShowWorldGenerationScreen(int)",
    }
)
ISSUE719_LIQUID_WISH_WARM_EFFECT_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 warm static review closes WishWarmEffect, WishWarmEffectSpec, "
        "and GlitchLiquidComponents with LiquidWarmStaticTranslationPatch on the "
        "exact owner routes."
    ),
    "Mods/QudJP/Assemblies/src/Patches/LiquidWarmStaticTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "decompiled source: XRL.Liquids/LiquidWarmStatic.cs lines 405-423 and 824-849",
]
ISSUE719_LIQUID_WISH_WARM_EFFECT_OWNER_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffect()",
        "XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.WishWarmEffectSpec(string)",
        (
            "XRL.Liquids/LiquidWarmStatic.cs::"
            "LiquidWarmStatic.GlitchLiquidComponents(GameObject,string,int,bool)"
        ),
    }
)
ISSUE719_RESIDUAL_RUNTIME_EVIDENCE_BY_FAMILY: Final[dict[str, list[str]]] = {
    **dict.fromkeys(ISSUE719_RESIDUAL_POPUP_FRAME_RUNTIME_FAMILIES, ISSUE719_RESIDUAL_POPUP_FRAME_RUNTIME_EVIDENCE),
    **dict.fromkeys(
        ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_FAMILIES,
        ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_EVIDENCE,
    ),
    **dict.fromkeys(
        ISSUE719_RESIDUAL_PURE_POPUP_TOP_RUNTIME_FAMILIES,
        ISSUE719_RESIDUAL_PURE_POPUP_TOP_RUNTIME_EVIDENCE,
    ),
    **dict.fromkeys(
        ISSUE719_RESIDUAL_PURE_POPUP_REMAINDER_RUNTIME_FAMILIES,
        ISSUE719_RESIDUAL_PURE_POPUP_REMAINDER_RUNTIME_EVIDENCE,
    ),
    **dict.fromkeys(ISSUE719_RESIDUAL_FRAME_DOES_RUNTIME_FAMILIES, ISSUE719_RESIDUAL_FRAME_DOES_RUNTIME_EVIDENCE),
    **dict.fromkeys(ISSUE719_RESIDUAL_MIXED_POPUP_RUNTIME_FAMILIES, ISSUE719_RESIDUAL_MIXED_POPUP_RUNTIME_EVIDENCE),
    **dict.fromkeys(ISSUE719_RESIDUAL_QUEUE_DOES_RUNTIME_FAMILIES, ISSUE719_RESIDUAL_QUEUE_DOES_RUNTIME_EVIDENCE),
    **dict.fromkeys(
        ISSUE719_RESIDUAL_MESSAGE_MIXED_REMAINDER_RUNTIME_FAMILIES,
        ISSUE719_RESIDUAL_MESSAGE_MIXED_REMAINDER_RUNTIME_EVIDENCE,
    ),
}
ISSUE719_RESIDUAL_PRODUCER_RUNTIME_FAMILIES: Final[frozenset[str]] = frozenset(
    ISSUE719_RESIDUAL_RUNTIME_EVIDENCE_BY_FAMILY
)
ISSUE719_RESIDUAL_SIFRAH_ROUTE_SPLIT_RUNTIME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps remaining Sifrah description and popup "
        "route-split families runtime-required after the exact owner-route Sifrah "
        "overlays have been applied; residual buckets remain "
        "sifrah_description_route_split / sifrah_popup_route_split."
    ),
    (
        "The residual Sifrah rows combine constructor text, token descriptions, "
        "GetDescription returns, CheckOutOfOptions/result popups, token-use popups, "
        "and generated object/secret/faction/item/liquid slots. Static review cannot "
        "safely promote the whole route-split bucket without live route evidence."
    ),
]
ISSUE719_SIFRAH_POPUP_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 Sifrah popup static reclassification: fixed owner methods "
        "for CheckOutOfOptions, Result*, token CheckTokenUse, SocialSifrahTokenSecret.UseToken, "
        "and CyberneticsTerminal2.HackingResultPartialSuccess are static implementation-gap "
        "candidates, not sink-only runtime-evidence rows."
    ),
    "decompiled Sifrah/CyberneticsTerminal2 owner methods contain fixed Popup.Show/ShowFail callsites.",
]
ISSUE719_SOCIAL_SIFRAH_SECRET_USE_TOKEN_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes SocialSifrahTokenSecret.UseToken by splitting the fixed "
        "Popup.PickOption title from journal-derived secret option payloads."
    ),
    (
        "The fixed 'Choose a secret to share:' title is covered by "
        "PopupPickOptionTranslationPatch and shipped ui-popup dictionary entries; "
        "the option strings are IBaseJournalEntry.GetShortText output with a "
        "map-note 'The location of ...' prefix, and the post-selection history "
        "suffix is journal/faction owner data rather than popup title text."
    ),
    "decompiled owner source: XRL.World/SocialSifrahTokenSecret.cs lines 138-176",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_SIFRAH_POPUP_UNUSED_BASE_GAME_EVIDENCE: Final[list[str]] = [
    "decompiled XRL.World/PsychicCombatSifrah.cs notes: This class is not used in the base game.",
    *ISSUE719_RESIDUAL_SIFRAH_ROUTE_SPLIT_RUNTIME_EVIDENCE,
]
ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE: Final[dict[str, list[str]]] = {
    "vehicle_unpowered": [
        (
            "Issue #719 tranche 35 covers VehicleUnpowered.PreventActionMessage "
            "cell-drained, insert-cell, and lacks-power popups through the exact "
            "active-effect popup owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/VehicleUnpoweredTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/VehicleUnpoweredTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mechanical_wings_fire_event": [
        ("Issue #719 tranche 35 extends the MechanicalWings popup owner route to FireEvent long-fall warnings."),
        "Mods/QudJP/Assemblies/src/Patches/MechanicalWingsPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MechanicalWingsPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "cathedra_long_fall": [
        (
            "Issue #719 tranche 35 extends the MechanicalWings long-fall popup owner "
            "route to CyberneticsCathedra.HandleEvent(CommandEvent)."
        ),
        "Mods/QudJP/Assemblies/src/Patches/MechanicalWingsPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MechanicalWingsPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mutation_wings_flight": [
        (
            "Issue #719 extends the MechanicalWings long-fall popup owner route to "
            "Wings.HandleEvent(CommandEvent) and covers the EMP will-not-move failure popup."
        ),
        "Mods/QudJP/Assemblies/src/Patches/MechanicalWingsPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MechanicalWingsPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "cybernetics_low_level_hack": [
        (
            "Issue #719 closes CyberneticsTerminal2.AskLowLevelHack through an exact "
            "owner-scoped low-level hack prompt patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsLowLevelHackPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsLowLevelHackPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "cybernetics_butcherable_cybernetic": [
        (
            "Issue #719 closes CyberneticsButcherableCybernetic.AttemptButcher "
            "butcher/rip message-frame output through an exact owner-scoped queue patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsButcherableCyberneticTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsButcherableCyberneticTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "cybernetics_onboard_recoiler": [
        (
            "Issue #719 closes CyberneticsOnboardRecoilerTeleporter.ActuateTeleport "
            "cooldown popups through an exact owner-scoped popup patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsOnboardRecoilerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsOnboardRecoilerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "cybernetics_terminal_interface": [
        (
            "Issue #719 closes CyberneticsTerminal2.AttemptInterface powered-status "
            "failure popups through an exact owner-scoped Does popup patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalInterfacePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsTerminalInterfacePopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    ],
    "sunder_mind_tick": [
        (
            "Issue #719 closes SunderMind.Tick queue and popup output through the "
            "owner-scoped SunderMind route plus fixed popup dictionary literals."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SunderMindTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "dance_ritual_opponent_debug_queue": [
        (
            "Issue #719 closes DanceRitualOpponent.HandleEvent debug queue output "
            "and Register startup debug queue output through the owner-scoped "
            "DanceRitualOpponent route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DanceRitualOpponentTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "player_dance_ritual_debug_queue": [
        (
            "Issue #719 closes PlayerDanceRitual.FireEvent turn tick debug queue "
            "output through the existing owner-scoped PlayerDanceRitual route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerDanceRitualTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "hooked_owner": [
        (
            "Issue #719 tranche 35 covers Hooked.HandleEvent break-free messages "
            "through the exact active-effect queue owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/HookedOwnerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/HookedOwnerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
}
TEXT_CONSTRUCTION_CLOSURE_OVERLAY: Final[dict[str, ClosureOverlayEntry]] = {
    "XRL.World.Parts/VehicleRepair.cs::VehicleRepair.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_VEHICLE_REPAIR_DOES_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.Parts/GeomagneticDisc.cs::GeomagneticDisc.DoThrow("
        "GameObject,List<FindPath>,bool,bool,List<GameObject>,GameObject,int,int?,"
        "IThrownWeaponFlexPhaseProvider,IEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Leveler.cs::Leveler.LevelUp(GameObject,GameObject,string,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CryptFerretBehavior.cs::CryptFerretBehavior.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsHighFidelityMatterRecompositer.cs::"
        "CyberneticsHighFidelityMatterRecompositer.HandleEvent(CommandEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.AI.GoalHandlers/PlaceTurretGoal.cs::PlaceTurretGoal.TakeAction()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CyberneticsMatterRecompositer.cs::CyberneticsMatterRecompositer.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CyberneticsCathedra.cs::CyberneticsCathedra.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cathedra_long_fall"],
    },
    "XRL.World.Parts.Mutation/Wings.cs::Wings.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["mutation_wings_flight"],
    },
    "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AskLowLevelHack(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_low_level_hack"],
    },
    (
        "XRL.World.Parts/CyberneticsButcherableCybernetic.cs::"
        "CyberneticsButcherableCybernetic.AttemptButcher(GameObject,bool,bool,bool,int,Cell,List<GameObject>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_butcherable_cybernetic"],
    },
    (
        "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs::"
        "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport(GameObject,IEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_onboard_recoiler"],
    },
    "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AttemptInterface(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_terminal_interface"],
    },
    "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PRODUCER_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Tick()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["sunder_mind_tick"],
    },
    (
        "XRL.World.Parts/DanceRitualOpponent.cs::"
        "DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["dance_ritual_opponent_debug_queue"],
    },
    (
        "XRL.World.Parts/DanceRitualOpponent.cs::"
        "DanceRitualOpponent.Register(GameObject,IEventRegistrar)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["dance_ritual_opponent_debug_queue"],
    },
    "XRL.World.Parts/PlayerDanceRitual.cs::PlayerDanceRitual.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["player_dance_ritual_debug_queue"],
    },
    "XRL.World.Parts/PointDefense.cs::PointDefense.HandleEvent(ProjectileMovingEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/GreaterVoider.cs::GreaterVoider.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/RunOver.cs::RunOver.PerformCharge(List<Cell>,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/AjiConch.cs::AjiConch.ActivateAjiConch()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    (
        "XRL.World.Capabilities/Disarming.cs::"
        "Disarming.Disarm(GameObject,GameObject,int,string,string,GameObject,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/EngulfingClones.cs::EngulfingClones.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Fan.cs::Fan.TurnTick(long,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/HookOnMissileHit.cs::HookOnMissileHit.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_COMBAT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Blast(MentalAttackEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    (
        "XRL.World.Parts/Physics.cs::"
        "Physics.AccelerateInternal(int,string,Cell,Cell,string,GameObject,bool,GameObject,string,double,"
        "bool,bool,bool,bool,bool,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    (
        "XRL.World.Parts/Butcherable.cs::"
        "Butcherable.AttemptButcher(GameObject,bool,bool,bool,string,Cell,List<GameObject>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/PluckablePolyp.cs::PluckablePolyp.Pluck(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Interior.cs::Interior.ShowMessage(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CyberneticsStasisProjector.cs::CyberneticsStasisProjector.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/TimeDilation.cs::TimeDilation.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/SwapOnHit.cs::SwapOnHit.SwapPositions(GameObject,Cell,GameObject,Cell,Event,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_PHYSICAL_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Cloneling.cs::Cloneling.PerformCloning(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CLONELING_PRODUCE_CLONE_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.AI.GoalHandlers/LayMineGoal.cs::LayMineGoal.TakeAction()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/BurgeonOnHit.cs::BurgeonOnHit.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/BurnOffGas.cs::BurnOffGas.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/GrabberArm.cs::GrabberArm.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Ironshroom.cs::Ironshroom.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/DropOnDamage.cs::DropOnDamage.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Sweeper.cs::Sweeper.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/PetPhylactery.cs::PetPhylactery.Spawn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.Spawn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ReflectShame.cs::ReflectShame.Shame(MentalAttackEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/EelSpawn.cs::EelSpawn.Reveal(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/EjectionSeat.cs::EjectionSeat.Message(GameObject,List<GameObject>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/DiThermoBeam.cs::DiThermoBeam.FlipBeam(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/StickyOnHit.cs::StickyOnHit.Entangle(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Tonic.cs::Tonic.HandleEvent(ExamineCriticalFailureEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_ENVIRONMENT_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/EnergyCellSocket.cs::EnergyCellSocket.AttemptRemoveCell(GameObject,InventoryActionEvent,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Domination.cs::Domination.Dominate(MentalAttackEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/SlipRing.cs::SlipRing.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/LavaSludge.cs::LavaSludge.HandleEvent(BeforeDieEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/NoStandUp.cs::NoStandUp.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/StairsDown.cs::StairsDown.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Thurible.cs::Thurible.SmokeThurible(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Disintegration.cs::Disintegration.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/BlinkOnDamage.cs::BlinkOnDamage.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Interdiction.cs::Interdiction.BeginInterdiction(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/QuantumFugue.cs::QuantumFugue.Cohere(Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/SapOnPenetration.cs::SapOnPenetration.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REVIEWED_UTILITY_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/FeelingOnTarget.cs::FeelingOnTarget.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/TimeDilation.cs::TimeDilation.ApplyField(GameObject,int,bool,int,int,IPart)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Chair.cs::Chair.StandUp(GameObject,IEvent,Sitting)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.InflictDamage(GameObject,Projectile)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/EngulfingHandOff.cs::EngulfingHandOff.AttemptHandOff(Engulfing,Engulfing,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/IStingerProperties.cs::IStingerProperties.FailureMessage(GameObject,GameObject,Effect)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/ReflectProjectiles.cs::ReflectProjectiles.Check()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/ReflectProjectiles.cs::ReflectProjectiles.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/RunOver.cs::RunOver.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/SkybearShroud.cs::SkybearShroud.ActivateSkyshroud()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Banner.cs::Banner.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CooldownOnStep.cs::CooldownOnStep.HandleEvent(ObjectEnteredCellEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CyberneticsCathedraBlackOpal.cs::CyberneticsCathedraBlackOpal.Activate(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/CyberneticsCathedraWhiteOpal.cs::CyberneticsCathedraWhiteOpal.Activate(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/IfThenElseQuestWidget.cs::IfThenElseQuestWidget.TurnTick(long,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/PsychicMeridian.cs::PsychicMeridian.AfflictNosebleed(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_MESSAGE_FRAME_DICTIONARY_EVIDENCE,
    },
    (
        "XRL.World.Parts.Mutation/StunningForce.cs::"
        "StunningForce.Concussion(Cell,GameObject,int,int,int,GameObject,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *ISSUE719_REVIEWED_MUTATION_ACTION_MESSAGE_FRAME_EVIDENCE,
            'StunningForce.Concussion source frames: XDidY(..., "invoke"/"feel", "a concussive blast ...")',
            "MessageFrame keys: concussive blast around/to/from direction variants",
        ],
    },
    "XRL.World.Parts.Mutation/IDelayedLineMutation.cs::IDelayedLineMutation.Refract(List<Cell>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *ISSUE719_REVIEWED_MUTATION_ACTION_MESSAGE_FRAME_EVIDENCE,
            'IDelayedLineMutation.Refract source frame: XDidYToZ(Object, "refract", Projectile, ...)',
            "MessageFrame keys: verb=refract extra=the laser beam and object fallback",
        ],
    },
    "XRL.World.Parts.Mutation/Decarbonizer.cs::Decarbonizer.fireBeam(List<Cell>,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *ISSUE719_REVIEWED_MUTATION_ACTION_MESSAGE_FRAME_EVIDENCE,
            'Decarbonizer.fireBeam source frame: DidX("fire", ParentObject.its + " molecular cannon", ...)',
            "MessageFrame key: verb=fire extra=(?:your|his|her|its|their) molecular cannon",
        ],
    },
    "XRL.World.Parts.Mutation/LiquidSpitter.cs::LiquidSpitter.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *ISSUE719_REVIEWED_MUTATION_ACTION_MESSAGE_FRAME_EVIDENCE,
            'LiquidSpitter.HandleEvent source frame: DidX("spit", "a puddle of " + LiquidName, ...)',
            "MessageFrame key: verb=spit extra=a puddle of {0}",
        ],
    },
    "XRL.World.Conversations.Parts/WaterRitual.cs::WaterRitual.HandleEvent(DisplayTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": CONVERSATION_BODY_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/MoundContext.cs::MoundContext.HandleEvent(PrepareTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": CONVERSATION_BODY_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/QuestSignpost.cs::QuestSignpost.HandleEvent(PrepareTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            QUEST_SIGNPOST_PARTIAL_EVIDENCE,
            (
                "Generated questgiver names and landmark text are display-name/data-source routes, "
                "not residual static producer work."
            ),
        ],
    },
    (
        "XRL.World.Conversations.Parts/WaterRitualTinkeringRecipe.cs::"
        "WaterRitualTinkeringRecipe.HandleEvent(PrepareTextEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            TINKERING_RECIPE_PARTIAL_EVIDENCE,
            (
                "Generated item and recipe names are object/tinkering display-name routes, "
                "not residual static producer work."
            ),
        ],
    },
    ("XRL.World.Conversations.Parts/WaterRitualHermitOath.cs::WaterRitualHermitOath.HandleEvent(PrepareTextEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            HERMIT_OATH_PARTIAL_EVIDENCE,
            (
                "Speaker-specific HermitOathAddressAs values are runtime/data-source owned, "
                "not residual static producer work."
            ),
        ],
    },
    ("XRL.World.Conversations.Parts/WaterRitualLearnSkill.cs::WaterRitualLearnSkill.HandleEvent(PrepareTextEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            LEARN_SKILL_PARTIAL_EVIDENCE,
            "Generated skill names remain exact dictionary/display-name routes, not residual static producer work.",
        ],
    },
    "XRL.World.Conversations.Parts/KithAndKinExclusion.cs::KithAndKinExclusion.HandleEvent(PrepareTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            "thief name replacement is a Kith-and-Kin game-state/display-name route, not a static producer route",
        ],
    },
    "XRL.World.Conversations.Parts/KithAndKinMotive.cs::KithAndKinMotive.HandleEvent(PrepareTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            "circumstance influence replacement is a Kith-and-Kin clue/game-state route, not a static producer route",
        ],
    },
    "XRL.World.Conversations.Parts/GlotrotFilter.cs::GlotrotFilter.HandleEvent(PrepareTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            (
                "Glotrot intentionally rewrites text into disease speech at runtime; "
                "static source shows only N/G + n* + period gibberish, not translatable English."
            ),
        ],
    },
    ("XRL.World.Conversations.Parts/InsertRandomBookLine.cs::InsertRandomBookLine.HandleEvent(PrepareTextEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_INSERT_RANDOM_BOOK_LINE_DATA_EVIDENCE,
    },
    COMBAT_MELEE_ATTACK_FAMILY_ID: {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        ],
    },
    CUDGEL_SLAM_CAST_FAMILY_ID: {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE747_CUDGEL_SLAM_ROUTE_EVIDENCE,
    },
    SHIELD_SLAM_SLAM_FAMILY_ID: {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE747_SHIELD_SLAM_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.MissileHit("
        "GameObject,GameObject,GameObject,GameObject,Projectile,GameObject,GameObject,"
        "MissilePath,Cell,FireType,int,int,int,bool,GameObject,bool,ref bool,ref bool,ref bool,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/MissileWeaponHitTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "docs/reports/2026-05-15-issue-699-static-producer-message-candidates.md",
        ],
    },
    "XRL.World/GameObject.cs::GameObject.PerformThrow(GameObject,Cell,GameObject,MissilePath,int,int?,int?,int?)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/GameObjectPerformThrowTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    (
        "XRL.World/GameObject.cs::GameObject.Move("
        "string,out GameObject,bool,bool,bool,bool,bool,bool,GameObject,GameObject,"
        "bool,int?,string,int?,bool,bool,GameObject,GameObject,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/GameObjectMoveTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World/GameObject.cs::GameObject.PerformReplaceCell(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 closes GameObject.PerformReplaceCell fixed popup text through exact ui-popup leaves.",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
        ],
    },
    "XRL.World.Parts/GiantClamProperties.cs::GiantClamProperties.TeleportFromClamWorld(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 closes GiantClamProperties.TeleportFromClamWorld queue and popup text through its owner route.",
            "Mods/QudJP/Assemblies/src/Patches/GiantClamTeleportTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
            "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/GiantClamTeleportTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    (
        "XRL.UI/PickTarget.cs::PickTarget.ShowPicker("
        "PickStyle,int,int,int,int,bool,AllowVis,Predicate<XRL.World.GameObject>,"
        "Predicate<XRL.World.GameObject>,XRL.World.GameObject,Point2D?,string,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/PickTargetShowPickerTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickTargetShowPickerTranslationPatchTests.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        ],
    },
    "Qud.UI/GameSummaryScreen.cs::GameSummaryScreen._ShowGameSummary(string,string,string,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/GameSummaryScreenShowTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/GameSummaryTextTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GameSummaryTextTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameSummaryAndAsleepTranslationPatchTests.cs",
        ],
    },
    "Qud.UI/SkillsAndPowersStatusScreen.cs::SkillsAndPowersStatusScreen.UpdateDetailsFromNode(SPNode)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenDetailsPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SkillsAndAbilitiesOwnerPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs::PhotosyntheticSkin.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Localization/ActivatedAbilities.jp.xml",
            "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
        ],
    },
    "XRL.World.Parts/Inventory.cs::Inventory.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "InventoryFireEventTranslationPatch covers the owner-owned "
                "graveyard recovery queue, container ownership prompt, stuck "
                "equip/remove popups, Inventory fallback cannot-equip and "
                "cannot-budge "
                "popups; BeginBeingUnequippedEvent covers the confirmed "
                "You can't remove {item} FailureMessage helper shape; other "
                "event-supplied FailureMessage payloads remain split to their "
                "true producers."
            ),
            "Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryFireEventTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/src/Patches/BeginBeingUnequippedFailureMessageTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeginBeingUnequippedFailureMessageTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "MissileWeapon.FireEvent direct surfaces are covered by "
                "message-frame/message-pattern routes for emitted projectiles "
                "and shot-goes-wild messages; CheckLoadAmmoEvent and "
                "LoadAmmoEvent payloads are split to loader true producers "
                "such as BioAmmoLoader/LiquidAmmoLoader/ModLiquidCooled and "
                "generic ammo loader patterns."
            ),
            "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
            "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
            "Mods/QudJP/Assemblies/src/Patches/LiquidLoaderTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
            "docs/reports/2026-05-15-issue-699-static-producer-message-candidates.md",
        ],
    },
    "XRL.UI/TradeUI.cs::TradeUI.ShowTradeScreen(GameObject,float,TradeScreenMode)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "TradeUI.ShowTradeScreen popups and water-debt prompts are "
                "covered by TradeUiPopupTranslationPatch; modern TradeScreen "
                "menu/AskNumber/update-total surfaces are covered by "
                "TradeScreenUiTranslationPatch/TradeScreenUpdateTotals; "
                "legacy console chrome, ownership badges, inventory title, "
                "dram/weight units, and bottom action prompts are covered by "
                "TradeUiLegacyScreenTranslationPatch. Dynamic item/category "
                "names are owner data/display-name routes."
            ),
            "Mods/QudJP/Assemblies/src/Patches/TradeUiLegacyScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/TradeScreenUiTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/TradeScreenUpdateTotalsTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiPopupTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeScreenUiTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LegacyGamepadPromptTranslationHelperTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.World.Parts/LongBladesCore.cs::LongBladesCore.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LongBladesCoreTranslationPatchTests.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
        ],
    },
    "XRL.World.Parts/LongBladesCore.cs::LongBladesCore.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers LongBladesCore generated activated ability "
                "names, class, and descriptions for Aggressive/Defensive Stance."
            ),
            "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityBarButtonTextTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerLineTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerScreenTranslationPatchTests.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
        ],
    },
    "XRL.World.Parts/LiquidVolume.cs::LiquidVolume.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers LiquidVolume.HandleEvent fixed failures, "
                "ownership/confirmation popups, fill-from picker titles, "
                "AskNumber dram prompts, status popups, and generated liquid "
                "collection messages through owner-scoped LiquidVolume routes."
            ),
            "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/PickItemShowPickerTitleTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldPartsFragmentTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.World.Parts/Tonic.cs::Tonic.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Tonic.HandleEvent inventory-action failures, apply-to target "
                "failures, visible third-party use messages, and the Apply "
                "direction prompt are covered through the Tonic owner route "
                "and pick-target label route; runtime MakeUnderstood popup "
                "text remains a separate part-supplied message."
            ),
            "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/PickTargetWindowTextTranslator.cs",
            "Mods/QudJP/Assemblies/src/Patches/TonicTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickTargetWindowUpdateTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.World.ZoneBuilders/Village.cs::Village.BuildZone(Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_BUILDZONE_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.BuildZone(Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_BUILDZONE_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateEndEvent()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_CODA_END_EVENT_ROUTE_EVIDENCE,
    },
    "XRL.World/ZoneManager.cs::ZoneManager.SetActiveZone(Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *ISSUE737_JOURNAL_ROUTE_EVIDENCE,
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/ZoneManagerSetActiveZoneTranslationPatchTests.cs",
            "static producer evidence: finite SetActiveZone journey AddAccomplishment branches "
            "are covered by journal patterns",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
            "docs/reports/2026-05-15-static-uncovered-coverage-triage.md",
        ],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.CookFromIngredients(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE737_COOKING_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Campfire.cs::Campfire.RollIngredients(int,IReadOnlyList<GameObject>,System.Random)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE737_COOKING_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Campfire.cs::Campfire.CookFromRecipe()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE737_COOK_RECIPE_PRESET_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Campfire.cs::Campfire.CookPresetMeal(int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE737_COOK_RECIPE_PRESET_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Campfire.cs::Campfire.DescribeMeal(IReadOnlyList<GameObject>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE737_COOKING_ROUTE_EVIDENCE,
    },
    (
        "Qud.API/JournalAPI.cs::JournalAPI.AddAccomplishment("
        "string,string,string,string,string,MuralCategory,MuralWeight,string,long,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE737_JOURNAL_ROUTE_EVIDENCE,
    },
    ("XRL.World/Reputation.cs::Reputation.Modify(Faction,int,string,StringBuilder,string,bool,bool,bool,bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_JOURNAL_ACCOMPLISHMENT_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/GivesRep.cs::GivesRep.HandleEvent(BeforeDeathRemovalEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_JOURNAL_ACCOMPLISHMENT_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/OpeningStory.cs::OpeningStory.AddAccomplishment(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_JOURNAL_STORY_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/AnimatorSpray.cs::AnimatorSpray.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *HSE_JOURNAL_STORY_ROUTE_EVIDENCE,
            "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts/Body.cs::Body.Dismember(BodyPart,GameObject,IInventory,bool,bool,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *HSE_JOURNAL_STORY_ROUTE_EVIDENCE,
            "Mods/QudJP/Assemblies/src/Patches/BodyTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.UI/StatusScreen.cs::StatusScreen.BuyRandomMutation(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *HSE_JOURNAL_STORY_ROUTE_EVIDENCE,
            "Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenPopupTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts/VillageSurface.cs::VillageSurface.CheckReveal()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            *HSE_JOURNAL_STORY_ROUTE_EVIDENCE,
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalApiAddTranslationPatchTests.cs",
            (
                "VillageSurface.CheckReveal visit accomplishments are covered by "
                "JournalAPI owner patterns; reveal popups are preauthored RevealString "
                "data rather than a generated English text-construction owner"
            ),
        ],
    },
    "XRL.World.Parts/GenerateFriendOrFoe.cs::GenerateFriendOrFoe.replacePlaceholders(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_FRIEND_OR_FOE_REASON_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/GenerateFriendOrFoe_HEB.cs::GenerateFriendOrFoe_HEB.replacePlaceholders(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_FRIEND_OR_FOE_REASON_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Gossip.cs::Gossip.GenerateGossip_OneFaction(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_JOURNAL_OBSERVATION_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Gossip.cs::Gossip.GenerateGossip_TwoFactions(string,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_JOURNAL_OBSERVATION_ROUTE_EVIDENCE,
    },
    "XRL.Language/TextFilters.cs::TextFilters.Angry(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TEXT_FILTER_SPEECH_STATUS_ROUTE_EVIDENCE,
    },
    "XRL.Language/TextFilters.cs::TextFilters.Lallated(string,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TEXT_FILTER_SPEECH_STATUS_ROUTE_EVIDENCE,
    },
    ("XRL.World/RelicGenerator.cs::RelicGenerator.GenerateSpindleNegotiationRelic(string,string,string,string,int)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    ("XRL.World/RelicGenerator.cs::RelicGenerator.SelectElement(GameObject,GameObject,GameObject,GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.GenerateSultanateYearName()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_SULTANATE_YEAR_NAME_ROUTE_EVIDENCE,
    },
    "XRL.Annals/ImportedFoodorDrink.cs::ImportedFoodorDrink.generateFactionName(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_IMPORTED_FOOD_DRINK_FACTION_NAME_ROUTE_EVIDENCE,
    },
    "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItem(string,History,HistoricEntity)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_HISTORY_ITEM_NAME_ROUTE_EVIDENCE,
    },
    "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItemNounRoot(string,History,HistoricEntity)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_HISTORY_ITEM_NAME_ROUTE_EVIDENCE,
    },
    "XRL.Annals/QudHistoryHelpers.cs::QudHistoryHelpers.NameItemAdjRoot(string,History,HistoricEntity)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_HISTORY_ITEM_NAME_ROUTE_EVIDENCE,
    },
    "XRL.Annals/VillageProverb.cs::VillageProverb.Generate()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_PROVERB_ROUTE_EVIDENCE,
    },
    ("XRL.Annals/QudHistoryFactory.cs::QudHistoryFactory.NameRuinsSite(History,out bool,out string)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_QUD_HISTORY_FACTORY_GENERATED_NAME_ROUTE_EVIDENCE,
    },
    ("XRL.Annals/QudHistoryFactory.cs::QudHistoryFactory.GenerateCultName(HistoricEntity,History)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_QUD_HISTORY_FACTORY_GENERATED_NAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts/LocateRelicQuestManager.cs::LocateRelicQuestManagerSystem.CheckCompleted(GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_COMPLETION_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::"
        "FindASiteDynamicQuestManagerSystem.CheckCompleted(Zone,JournalMapNote)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_COMPLETION_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestManager.cs::"
        "FindASpecificItemDynamicQuestManagerSystem.CheckCompleted(GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_COMPLETION_ROUTE_EVIDENCE,
    },
    ("XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestManager.cs::System.FinishEntry(QuestEntry,GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_COMPLETION_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver("
        "GameObject,Quest,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver(GameObject,Quest)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver("
        "GameObject,Quest,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/DynamicQuestConversationHelper.cs::DynamicQuestConversationHelper."
        "appendQuestCompletionSequence("
        "ConversationXMLBlueprint,Quest,ConversationXMLBlueprint,string,string,"
        "Action<ConversationXMLBlueprint>,Action<ConversationXMLBlueprint>,Action<ConversationXMLBlueprint>,"
        "Action<ConversationXMLBlueprint>,Action<ConversationXMLBlueprint>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.Parts/DynamicQuestSignpostConversation.cs::"
        "DynamicQuestSignpostConversation.HandleEvent(BeforeConversationEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.fabricateFindASpecificItemQuest("
        "GameObject,string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs::"
        "InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.fabricateInteractWithAnObjectQuest("
        "GameObject,string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    "XRL.World/VillageDynamicQuestContext.cs::VillageDynamicQuestContext.getQuestItemNameMutation(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DYNAMIC_QUEST_ROUTE_EVIDENCE,
    },
    ("XRL.World.Skills.Cooking/CookingRecipe.cs::CookingRecipe.GenerateRecipeName(List<string>,List<string>,string)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_COOKING_DISPLAY_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Cookbook.cs::Cookbook.GenerateCookbook()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_COOKING_DISPLAY_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/EaterCryptPlaque.cs::EaterCryptPlaque.GeneratePlaque()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MEMORIAL_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/EaterUrn.cs::EaterUrn.GenerateUrn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MEMORIAL_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/RachelsTombstone.cs::RachelsTombstone.GenerateTombstone()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MEMORIAL_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelic("
        "string,int,HistoricEntitySnapshot,List<string>,Dictionary<string,List<string>>,string,string,string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelicName(string,HistoricEntitySnapshot,string,out string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelicNameByRegion("
        "string,HistoricEntitySnapshot,string,out string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.Capabilities/ItemNaming.cs::ItemNaming.GenerateRelicStyleName("
        "GameObject,GameObject,GameObject,GameObject,string,ref string,ref string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.Capabilities/ItemNaming.cs::ItemNaming.NameItem("
        "GameObject,GameObject,string,string,string,string,bool,bool,GameObject,GameObject,string,bool,int,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/RandomAltarBaetyl.cs::RandomAltarBaetyl.GenerateItem(string,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    "XRL.World/Faction.cs::Faction.GenerateHeirloom(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/VillageTerrain.cs::VillageTerrain.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.getAVillageCanvas()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.getAVillageCanvas()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/Village.cs::Village.generateWarden(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_CONVERSATION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.generateWarden(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_CONVERSATION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/Village.cs::Village.generateMayor(GameObject,string,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_CONVERSATION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.generateMayor(GameObject,string,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_CONVERSATION_ROUTE_EVIDENCE,
    },
    "XRL.World.Encounters/DimensionManager.cs::DimensionManager.InitializeFaction()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DIMENSION_PSYCHIC_ROUTE_EVIDENCE,
    },
    "XRL.World.Encounters/DimensionManager.cs::DimensionManager.GenerateMoreDimensions()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DIMENSION_PSYCHIC_ROUTE_EVIDENCE,
    },
    "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CreateSeekerHunters(int,Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DIMENSION_PSYCHIC_ROUTE_EVIDENCE,
    },
    (
        "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CreateExtradimensionalSoloHunters("
        "Zone,int,List<XRL.World.GameObject>,bool,bool,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DIMENSION_PSYCHIC_ROUTE_EVIDENCE,
    },
    "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CreateExtradimensionalSoloDeviant(Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_DIMENSION_PSYCHIC_ROUTE_EVIDENCE,
    },
    "XRL.Names/SettlementNames.cs::SettlementNames.GenerateFarmNameInner(History,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MISC_OWNER_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/BroadcastPowerReceiver.cs::BroadcastPowerReceiver.HandleEvent(GetShortDescriptionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MISC_OWNER_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/MerchantRevealer.cs::MerchantRevealer.GenerateMerchantLocation()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MISC_OWNER_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/TempleDedicationPlaque.cs::TempleDedicationPlaque.GenerateInscription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MISC_OWNER_ROUTE_EVIDENCE,
    },
    (
        "XRL.Names/NameStyle.cs::NameStyle.Generate("
        "GameObject,string,string,string,string,string,string,string,List<string>,string,string,string,"
        "Dictionary<string,string>,bool,bool,NameStyle,List<NameStyle>,int?,int?,bool?,bool?)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_NAMESTYLE_XML_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.HandleEvent(BeforeObjectCreatedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers generated element display/rules text via "
                "BestowElement and base organic/mechanical cherub descriptions "
                "through the CherubimSpawner owner route; faction-derived "
                "object-name composition remains dynamic data and should not be "
                "closed with exact leaves."
            ),
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerReplaceDescriptionPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerReplaceDescriptionPatchTests.cs",
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerHandleEventTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerBestowElementTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerGeneratedTextTranslationPatchTests.cs",
        ],
    },
    "XRL.World.Parts/SultanShrine.cs::SultanShrine.ShrineInitialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Sultan shrine display names are covered by the generated "
                "`shrine to <target>` display-name wrapper route and shrine "
                "descriptions are covered by the SultanShrine wrapper "
                "translator; generated sultan names/cognomina remain dynamic "
                "fragments, not fixed leaves."
            ),
            "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "Mods/QudJP/Assemblies/src/Patches/SultanShrineWrapperTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SultanShrineWrapperTranslatorTests.cs",
        ],
    },
    "XRL.UI/StatusScreen.cs::StatusScreen.Show(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Legacy StatusScreen.Show chrome, footer, stat labels, "
                "resistance labels, mutation/effect labels, and dynamic "
                "attribute/mutation popups are covered by the legacy screen "
                "transpiler and owner-scoped status popup routes; genotype, "
                "subtype, mutation, and effect display names are data/display "
                "routes rather than residual static producer gaps."
            ),
            "Mods/QudJP/Assemblies/src/Patches/StatusScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/LegacyGamepadPromptTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenPopupTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.Core/XRLCore.cs::XRLCore.PlayerTurn()": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/XrlCorePlayerTurnTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.UI/TinkeringScreen.cs::TinkeringScreen.Show(GameObject,GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Legacy fixed chrome labels are covered by "
                "TinkeringScreenTranslationPatch, including title, mode tabs, "
                "empty-state prompts, Bit Locker labels, bit descriptions, "
                "ingredient alternatives, and footer commands; dynamic recipe "
                "display names/descriptions remain owner-data surfaces."
            ),
            "Mods/QudJP/Assemblies/src/Patches/TinkeringScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
        ],
    },
    "XRL.UI/InventoryScreen.cs::InventoryScreen.Show(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Legacy InventoryScreen.Show fixed chrome, footer commands, "
                "scroll markers, total-weight label, quick-key prompt, and "
                "filter-hidden count are covered by the legacy screen "
                "transpiler; item/category names, counts, and weights are "
                "owner-data/display-name surfaces."
            ),
            "Mods/QudJP/Assemblies/src/Patches/InventoryScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/LegacyGamepadPromptTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LegacyGamepadPromptTranslationHelperTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
        ],
    },
    "XRL.UI/AbilityManager.cs::AbilityManager.Show(XRL.World.GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Legacy ability-manager fixed chrome, category/row status fragments, "
                "cooldown detail lines, and footer commands are covered by the legacy "
                "ScreenBuffer owner route; queued ability cooldown messages remain "
                "covered by the owner-scoped message route."
            ),
            "Mods/QudJP/Assemblies/src/Patches/AbilityManagerLegacyScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/AbilityManagerShowTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/LegacyGamepadPromptTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "TinkeringDetailsLineTranslationPatch covers setData sinks for "
                "display names, build/mod descriptions, mod descriptions, "
                "bit-cost labels, ingredients labels, and -or- separators."
            ),
            "Mods/QudJP/Assemblies/src/Patches/TinkeringDetailsLineTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorRouteCatalogTests.cs",
        ],
    },
    "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.PsychicCombatSifrah(GameObject,string,int,int,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "decompiled XRL.World/PsychicCombatSifrah.cs notes: This class is not used in the base game.",
            "Issue #719 static unused-base-game classification: no base-game runtime route to localize.",
            "Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SifrahPureOwnerPopupTranslationPatchTests.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
            "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
        ],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.BeguilingSifrah(GameObject,int,bool,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "decompiled XRL.World/BeguilingSifrah.cs notes: This class is not used in the base game.",
            "Issue #719 static unused-base-game classification: no base-game runtime route to localize.",
        ],
    },
    "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers Wrecking Charge through the activated ability "
                "route and the runtime Triple Horn/Horns/Horn/Antlers mutation "
                "display names through status display exact leaves."
            ),
            "Mods/QudJP/Localization/Dictionaries/ui-displayname-atomic.ja.json",
            "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs",
        ],
    },
    (
        "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.ShowPicker("
        "int,int,bool,AllowVis,int,bool,GameObject,ref FireType,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "MissileWeapon.ShowPicker fire-target footer, fire-mode menu "
                "labels, marked-target prompts, and legacy ScreenBuffer writes "
                "are covered by the pick-target owner route."
            ),
            "Mods/QudJP/Assemblies/src/Patches/MissileWeaponShowPickerTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/PickTargetWindowTextTranslator.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
            "Mods/QudJP/Localization/Dictionaries/ui-pick-target.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/UITextSkinTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts/SultanRegion.cs::SultanRegion.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Tombstone.cs::Tombstone.GenerateTombstone()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_MEMORIAL_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.getAVillageWall()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE,
    },
    "XRL.World.ZoneBuilders/VillageCodaBase.cs::VillageCodaBase.getAVillageWall()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_VILLAGE_DESCRIPTION_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.BestowElement(GameObject,string,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the finite cherubim element display-name "
                "prefixes and RulesDescription caste text added by "
                "BestowElement(GameObject,string,bool); PrependName=false "
                "intentionally leaves display names unchanged while translating "
                "the added rules part."
            ),
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerBestowElementTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerGeneratedTextTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts/HexacherubimSpawner.cs::HexacherubimSpawner.HandleEvent(BeforeObjectCreatedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the hexacherubim generated display name, "
                "localized base description, and delegated BestowElement "
                "RulesDescription text."
            ),
            "Mods/QudJP/Assemblies/src/Patches/HexacherubimSpawnerHandleEventTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerBestowElementTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerGeneratedTextTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts/CyberneticsSchemasoft.cs::CyberneticsSchemasoft.InitChip(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers finite generated schemasoft display names from "
                "InitChip(bool); behavior-description text remains a separate family."
            ),
            "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
        ],
    },
    (
        "XRL.World.Parts/CyberneticsSchemasoft.cs::"
        "CyberneticsSchemasoft.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers finite generated schemasoft behavior-description "
                "text after GetCyberneticsBehaviorDescriptionEvent.GetFor composes "
                "base and AddOn lines."
            ),
            "Mods/QudJP/Assemblies/src/Patches/CyberneticsBehaviorDescriptionTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsBehaviorDescriptionTranslationPatchTests.cs",
            "scripts/tests/test_cybernetics_behavior_descriptions.py",
        ],
    },
    "XRL.World.Parts/TurretTinker.cs::TurretTinker.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the generated activated ability display name "
                "`Tinker Turret [N remaining]`; the CommandTinkerTurret direction "
                "prompt and out-of-turrets failure popup are covered by fixed "
                "dictionary leaves through PickTarget and Popup owner routes."
            ),
            "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityBarButtonTextTranslationPatchTests.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-pick-target.ja.json",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickTargetWindowUpdateTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.Core/XRLCore.cs::XRLCore._Start()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Legacy XRLCore._Start main-menu ScreenBuffer.Write/WriteAt "
                "labels, mod warnings, title, and copyright text are covered "
                "by the legacy main-menu owner route."
            ),
            "Mods/QudJP/Assemblies/src/Patches/XrlCoreStartMainMenuTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/LegacyGamepadPromptTranslationHelpers.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the six finite CyclopeanPrism runtime "
                "DisplayName assignments through the display-name owner route; "
                "PtohAnnoyed popup and Die text remain separate."
            ),
            "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.ResetPrism()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers ResetPrism's finite amaranthine prism "
                "DisplayName assignment through the same CyclopeanPrism "
                "display-name owner route."
            ),
            "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts/PitMaterial.cs::PitMaterial.PaintPit()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers PitMaterial's finite open-air/craggy-ledge "
                "DisplayName assignments and runtime short description; "
                "paint/color properties are not localization text."
            ),
            "Mods/QudJP/Localization/Dictionaries/ui-displayname-atomic.ja.json",
            "Mods/QudJP/Localization/Dictionaries/descriptions.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
        ],
    },
    "XRL.World.Parts/PitMaterial.cs::PitMaterial.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the FirmPitEdges finite open-air/craggy-ledge "
                "DisplayName assignments and runtime short description; "
                "PaintPit and FireEvent are tracked separately to avoid overclaiming."
            ),
            "Mods/QudJP/Localization/Dictionaries/ui-displayname-atomic.ja.json",
            "Mods/QudJP/Localization/Dictionaries/descriptions.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
        ],
    },
    (
        "XRL.World.Parts.Mutation/EvilTwin.cs::"
        "EvilTwin.CreateEvilTwin(GameObject,string,Cell,string,string,GameObject,string,bool,string,string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the known finite EvilTwin/HexCrystal/"
                "EngulfingClones generated display-name prefixes and runtime "
                "short descriptions; arbitrary caller-supplied Prefix, Message, "
                "and MessageForActor values are deferred until a concrete "
                "producer/callsite proves a visible localization gap."
            ),
            "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
            "Mods/QudJP/Localization/Dictionaries/descriptions.ja.json",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts.Mutation/EvilTwin.cs::EvilTwin.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 verified the Evil Twin mutation long description is "
                "already covered by mutation-description owner routes; runtime "
                "clone creation display/popup text is tracked separately."
            ),
            "Mods/QudJP/Localization/Dictionaries/mutation-descriptions.ja.json",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
            "Mods/QudJP/Assemblies/src/Patches/ChargenStructuredTextTranslator.cs",
            "scripts/tests/test_mutation_description_semantics.py",
        ],
    },
    "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.HandleEvent(AfterObjectCreatedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 verified the `phylactery of <target>` runtime "
                "DisplayName composition is already handled by the generated "
                "English-prefix display-name route; hacking popup and spawn "
                "message families remain separate."
            ),
            "Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/GetDisplayNameRouteTranslatorTests.cs",
            "Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml",
            "Mods/QudJP/Localization/Naming.jp.xml",
        ],
    },
    "XRL.World/RandomAltarBaetylRewardManager.cs::RandomAltarBaetylRewardManager.HandleRewardNode(XmlDataHelper)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the sparking baetyl reward Description "
                "data source through the localized SparkingBaetyls owner XML; "
                "baetyl popup and wish routes remain separate."
            ),
            "Mods/QudJP/Localization/SparkingBaetyls.jp.xml",
            "scripts/tests/test_sparking_baetyl_rewards.py",
            "scripts/tests/test_text_construction_surface_policy.py",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts/ModGigantic.cs::ModGigantic.GetDescription(int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the fixed ModGigantic data-disk/short-description "
                "leaf through the existing world-mods dictionary and description "
                "owner route."
            ),
            "Mods/QudJP/Localization/Dictionaries/world-mods.ja.json",
            "Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldModsTextTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
            "scripts/tests/test_text_construction_surface_policy.py",
        ],
    },
    "XRL.World.Parts/ModGigantic.cs::ModGigantic.GetDescription(int,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers ModGigantic generated item modification "
                "descriptions through WorldModsTextTranslator on both description "
                "owner text and the tinkering details modDescriptionText sink."
            ),
            "Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs",
            "Mods/QudJP/Assemblies/src/Patches/TinkeringDetailsLineTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldModsTextTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionLongDescriptionPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
            "scripts/tests/test_text_construction_surface_policy.py",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts/BandageMedication.cs::BandageMedication.PerformBandaging(GameObject,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the bandage direction prompt, Actor.Fail "
                "failure messages, and successful plus phase/stasis "
                "MessageFrame shapes."
            ),
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
            "Mods/QudJP/Localization/Dictionaries/ui-pick-target.ja.json",
            "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
            "docs/reports/2026-05-15-static-uncovered-coverage-triage.md",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts.Skill/Tactics_Charge.cs::Tactics_Charge.PerformCharge()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #747 reviewed skill-originated static family: "
                "XRL.World.Parts.Skill/Tactics_Charge.cs::Tactics_Charge.PerformCharge()"
            ),
            "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
            "Issue #747 skill-originated Tactics_Charge popup failures and message-frame rows are owner-route covered.",
        ],
    },
    "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.PerformCharge(List<Cell>,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #762 covers the `charge`, `stomp with bestial fury`, and "
                "`stopped in its tracks by` MessageFrame shapes, the shoved-by-charge "
                "MessageFrame, and charge wall-slam damage tails through the "
                "Physics.ProcessTakeDamage owner route."
            ),
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
            "docs/reports/2026-05-15-static-uncovered-coverage-triage.md",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.World.Parts.Mutation/SlimeGlands.cs::SlimeGlands.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/AcidSlimeGlands.cs::AcidSlimeGlands.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Clairvoyance.cs::Clairvoyance.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ForceWall.cs::ForceWall.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/WaveformWorm.cs::WaveformWorm.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/BurrowingClaws.cs::BurrowingClaws.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_MESSAGE_FRAME_FIXED_EVIDENCE,
    },
    "XRL.World.Parts/BreakableInMelee.cs::BreakableInMelee.HandleEvent(DefendMeleeHitEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ExistenceSupport.cs::ExistenceSupport.Unsupported(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/HologramProjector.cs::HologramProjector.Enable(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/HologramProjector.cs::HologramProjector.Disable(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Slumberling.cs::Slumberling.CheckHibernate(int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Temporary.cs::Temporary.Expire(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/SpiralIron.cs::SpiralIron.PressSpiralIron(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Capacitor.cs::Capacitor.HandleEvent(BeforeDeathRemovalEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/LightDimmer.cs::LightDimmer.Tick(int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ModQuantumReverb.cs::ModQuantumReverb.PlaceHologram(GameObject,Cell)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FearAura.cs::FearAura.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts.Mutation/Dystechnia.cs::Dystechnia.CauseExplosion(GameObject,GameObject,IEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.Refract(int,List<GameObject>,bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts.Mutation/SpontaneousCombustion.cs::SpontaneousCombustion.TurnTick(long,int)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Kindle.cs::Kindle.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Decarbonizer.cs::Decarbonizer.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FrostWebs.cs::FrostWebs.FrostWeb(List<Cell>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ElectromagneticPulse.cs::ElectromagneticPulse.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Narcolepsy.cs::Narcolepsy.HandleEvent(EndTurnEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ModPsionic.cs::ModPsionic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/RepellingForce.cs::RepellingForce.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts.Mutation/ElectricalGeneration.cs::ElectricalGeneration.DischargeMessage(int)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/BlastOnHit.cs::BlastOnHit.Detonate(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/EMPGrenade.cs::EMPGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/HEGrenade.cs::HEGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ThermalGrenade.cs::ThermalGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/PhaseGrenade.cs::PhaseGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/GasGrenade.cs::GasGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/GravityGrenade.cs::GravityGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts/TimeDilationGrenade.cs::TimeDilationGrenade.DoDetonate(Cell,GameObject,GameObject,bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/FlashbangGrenade.cs::FlashbangGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/DeploymentGrenade.cs::DeploymentGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DEPLOYMENT_GRENADE_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Parts/ExplodeOnHit.cs::ExplodeOnHit.Detonate(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/FusionReactor.cs::FusionReactor.Explode(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ShattersOnHit.cs::ShattersOnHit.Shatter(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/SunderGrenade.cs::SunderGrenade.DoDetonate(Cell,GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/CryptSitterBehavior.cs::CryptSitterBehavior.Alert()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/CryptSitterBehavior.cs::CryptSitterBehavior.Unalert()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/CrumblesOnHit.cs::CrumblesOnHit.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/TemperatureVenting.cs::TemperatureVenting.Trigger()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/FactionRank.cs::FactionRank.PromoteIfBelow(string,string,bool,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/GenericInventoryRestocker.cs::GenericInventoryRestocker.PerformStock(bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Forcefield.cs::Forcefield.HandleEvent(RealityStabilizeEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ForcefieldMaterial.cs::ForcefieldMaterial.HandleEvent(RealityStabilizeEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Hidden.cs::Hidden.RevealInternal(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HIDDEN_REVEAL_INTERNAL_OWNER_EVIDENCE,
    },
    "XRL.World.Parts/LavaSludge.cs::LavaSludge.CheckTemperature()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Shrine.cs::Shrine.PrayAtShrine(GameObject,bool,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/BubbleLevel.cs::BubbleLevel.FlipBubbleLevel(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/EjectionSlot.cs::EjectionSlot.LockSeats(Cell,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/HolographicIvory.cs::HolographicIvory.HandleEvent(ObjectEnteredCellEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/PetPhylactery.cs::PetPhylactery.Despawn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.Despawn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/SoupSludge.cs::SoupSludge.ReactWith(string,LiquidVolume)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/SpaceTimeVortex.cs::SpaceTimeVortex.HandleEvent(RealityStabilizeEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/DisperseEMP.cs::DisperseEMP.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/CloneOnHit.cs::CloneOnHit.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/RocketSkates.cs::RocketSkates.EmitFlamePlume(Cell,Cell,GameObject,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Hidden.cs::Hidden.HideInternal(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ExplodeAfterTurns.cs::ExplodeAfterTurns.Detonate(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.CheckExplosion()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/Rummager.cs::Rummager.CheckPickUp()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/StrideMason.cs::StrideMason.ExamineFailure(IExamineEvent,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/TrollKing.cs::TrollKing.Spawn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.HandleEvent(CheckLoadAmmoEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_DOES_MESSAGE_PATTERN_EVIDENCE,
    },
    ("XRL.Liquids/LiquidWarmStatic.cs::LiquidWarmStatic.ApplyRandomEffectTo(GameObject,int,bool,bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/ChargeUsedEvent.cs::"
        "ChargeUsedEvent.Send(GameObject,GameObject,int,int,int,long,bool,bool,bool,bool,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/ForceProjector.cs::ForceProjector.ForceProjectorDeactivate(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FORCE_PROJECTOR_UNRESPONSIVE_EVIDENCE,
    },
    "XRL.Liquids/LiquidSludge.cs::LiquidSludge.ObjectGoingProne(LiquidVolume,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.Liquids/LiquidGoo.cs::LiquidGoo.ObjectGoingProne(LiquidVolume,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.Liquids/LiquidOoze.cs::LiquidOoze.ObjectGoingProne(LiquidVolume,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/RefreshCooldownsOnEat.cs::RefreshCooldownsOnEat.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/CherubimLock.cs::CherubimLock.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.Load(GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.HandleEvent(CommandReloadEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    ("XRL.World.Parts/Combat.cs::Combat.PerformMeleeAttack(GameObject,GameObject,int,int,int,int,string,bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/ChevronWall.cs::ChevronWall.HandleEvent(AfterDieEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/HexCrystal.cs::HexCrystal.HandleEvent(AfterDieEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/EquipStatBoost.cs::EquipStatBoost.ExamineFailure(IExamineEvent,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/Door.cs::Door.AttemptClose(GameObject,bool,bool,bool,bool,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/HelpingHands.cs::HelpingHands.ExamineFailure(IExamineEvent,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.DestroyHolograms(GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/Mutations.cs::Mutations.AddChimericBodyPart(bool,string,BodyPart)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.Liquids/LiquidProteanGunk.cs::LiquidProteanGunk.ProcessTurns(LiquidVolume,GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/GeomagneticDisc.cs::GeomagneticDisc.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ForceBubble.cs::ForceBubble.CreateBubble()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.PlaceHologram(Cell,GameObject,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/RocketSkates.cs::RocketSkates.HandleEvent(JumpedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL/PsychicHunterSystem.cs::PsychicHunterSystem.CheckPsychicHunters(Zone)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/CursedCellSocket.cs::CursedCellSocket.HandleEvent(CellDepletedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Parts/ForceProjector.cs::ForceProjector.ForceProjectorActivate(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FORCE_PROJECTOR_UNRESPONSIVE_EVIDENCE,
    },
    "XRL.World.Effects/EmptyTheClips.cs::EmptyTheClips.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Immobilized.cs::Immobilized.EndImmobilization()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Rebuked.cs::Rebuked.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Scintillating.cs::Scintillating.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Terrified.cs::Terrified.Attack(MentalAttackEvent,GameObject,Cell,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Bleeding.cs::Bleeding.StopMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BLEEDING_STOP_MESSAGE_EVIDENCE,
    },
    "XRL.World.AI.GoalHandlers/DustAnUrnGoal.cs::DustAnUrnGoal.MoveToAndDustUrn()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.AI.GoalHandlers/GiveATreatToPartyLeader.cs::GiveATreatToPartyLeader.TakeAction()"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    ("XRL.World.Parts.Mutation/IDelayedLineMutation.cs::IDelayedLineMutation.HandleEvent(CommandEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Parts/DeathGate.cs::DeathGate.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL/ChavvahSystem.cs::ChavvahSystem.Hide()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/Switch.cs::Switch.FlipSwitch(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/InteriorPortal.cs::InteriorPortal.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Effects/Famished.cs::Famished.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Effects/Glotrot.cs::Glotrot.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Effects/Paralyzed.cs::Paralyzed.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Effects/WakingDream.cs::WakingDream.Award(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Anatomy/BodyPart.cs::BodyPart.SetAsPreferredDefault(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/DefensiveChromatophores.cs::DefensiveChromatophores.AttemptScintillate(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/UnwelcomeGermination.cs::UnwelcomeGermination.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts/TeleportGate.cs::TeleportGate.CheckPossibleSubject(GameObject,IEvent,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs::"
        "CyberneticsOnboardRecoilerImprinting.HandleEvent(InventoryActionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL/XRLGame.cs::XRLGame.LoadGame(string,bool,bool,Dictionary<string,object>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SINGLE_CALLSITE_POPUP_EXACT_OWNER_EVIDENCE,
    },
    "XRL.World.Parts/Food.cs::Food.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SINGLE_CALLSITE_POPUP_EXACT_OWNER_EVIDENCE,
    },
    "XRL.World.Parts/Container.cs::Container.AttemptOpen(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SINGLE_CALLSITE_POPUP_EXACT_OWNER_EVIDENCE,
    },
    "XRL/PopulationManager.cs::PopulationManager.WishGenerate(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SINGLE_CALLSITE_POPUP_EXACT_OWNER_EVIDENCE,
    },
    "XRL/PopulationManager.cs::PopulationManager.WishFindBlueprint(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_POPULATION_WISH_FIND_BLUEPRINT_POPUP_EVIDENCE,
    },
    "XRL/ModInfo.cs::ModInfo.ConfirmFailure()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MODINFO_CONFIRM_FAILURE_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/EndGame.cs::EndGame.PickState()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_PRODUCER_POPUP_DICTIONARY_EVIDENCE,
    },
    (
        "XRL/PronounAndGenderSets.cs::"
        "PronounAndGenderSets.ShowPickGenderAndPronounSet(GameObject,string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_PRODUCER_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL/CheckpointingSystem.cs::CheckpointingSystem.ShowDeathMessage(string,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_PRODUCER_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL/PronounAndGenderSets.cs::PronounAndGenderSets.ShowChangePronounSet(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_PRODUCER_POPUP_DICTIONARY_EVIDENCE,
    },
    "XRL.World/GameObject.cs::GameObject.AutoEquip(GameObject,bool,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_GAMEOBJECT_AUTOEQUIP_POPUP_EVIDENCE,
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.SelectInputType()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_POPUP_PICK_OPTION_DICTIONARY_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Physic_AmputateLimb.cs::Physic_AmputateLimb.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #747 reviewed skill-originated static family: "
                "XRL.World.Parts.Skill/Physic_AmputateLimb.cs::"
                "Physic_AmputateLimb.FireEvent(Event)"
            ),
            "Mods/QudJP/Assemblies/src/Patches/PhysicAmputateLimbTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        ],
    },
}

ISSUE719_PRONE_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps Prone message-frame coverage on the "
        "existing MessageFrame verb route; Prone display-name construction rows "
        "remain runtime/display-name follow-up work."
    ),
    "docs/reports/2026-04-12-issue-354-stale-bucket-reclassification-batch-01.md",
    "docs/reports/2026-04-11-didx-prone-review.md",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]

ISSUE719_STUN_FIXED_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes only exact Stun message routes that "
        "are already covered by MessageFrame/Does route tests; the remain-stunned "
        "turn-loop row is covered separately by the generated effect owner route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DoesFragmentMarkingPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DoesVerbRouteTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbRouteTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]

ISSUE719_HOLOGRAPHIC_BLEEDING_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review keeps HolographicBleeding start/stop "
        "message-frame rows on the existing MessageFrame verb route."
    ),
    "docs/reports/2026-04-12-issue-354-stale-bucket-reclassification-batch-01.md",
    "docs/reports/2026-04-11-didx-holographicbleeding-review.md",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]

ISSUE719_ASLEEP_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes only the exact Asleep Apply/"
        "HandleEvent message routes targeted by the existing Asleep owner and "
        "message patches; FireEvent/Remove rows remain separate residuals."
    ),
    "Mods/QudJP/Assemblies/src/Patches/AsleepOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/AsleepMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameSummaryAndAsleepTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes only exact generated effect message "
        "targets covered by EffectGeneratedMessageTranslationPatch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/EffectGeneratedMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
]

ISSUE719_BRAIN_BRINE_GAIN_CHOICE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes only BrainBrineCurse.GainChoice "
        "reward popup text through the existing owner patch; BrainBrineCurse "
        "FireEvent popup rows remain separate."
    ),
    "Mods/QudJP/Assemblies/src/Patches/BrainBrineCurseTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/BrainBrineCurseTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact cooking runtime FireEvent "
        "queued-message targets that are already owner-scoped by "
        "CookingRuntimeTranslationPatch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CookingRuntimeTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingRuntimeTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact BasicCookingEffect ApplyEffect "
        "popup targets that are already owner-scoped by CookingRuntimeTranslationPatch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CookingRuntimeTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingRuntimeTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_IRONSHANK_ONSET_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes IronshankOnset.FireEvent queued "
        "messages through the existing owner-scoped queue patch; constructor "
        "display-name rows remain runtime follow-up work."
    ),
    "Mods/QudJP/Assemblies/src/Patches/IronshankOnsetTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_EFFECT_MOBILITY_BLOCK_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact mobility-block popup/queue "
        "families covered by EffectMobilityBlockTranslationPatch; mixed "
        "MessageFrame families remain residual until that surface is proven."
    ),
    "Mods/QudJP/Assemblies/src/Patches/EffectMobilityBlockTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/EffectMobilityBlockTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_REALITY_STABILIZED_EVENT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact RealityStabilized event "
        "popup/queue targets covered by RealityStabilizedEventTranslationPatch; "
        "constructor/display-name and ambient rows remain separate."
    ),
    "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedEventTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_REALITY_STABILIZED_INTERDICT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact RealityStabilized interdict "
        "popup targets covered by RealityStabilizedInterdictTranslationPatch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedInterdictTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_GLOTROT_ONSET_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes GlotrotOnset.FireEvent queued/popup "
        "message shapes through the existing owner-scoped queue patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GlotrotOnsetTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_MONOCHROME_ONSET_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes MonochromeOnset.FireEvent queued "
        "messages through the existing owner-scoped queue patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/MonochromeOnsetTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_PHASED_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact Phased queue-message targets "
        "covered by PhasedTranslationPatch; Phased display-name rows remain "
        "runtime follow-up work."
    ),
    "Mods/QudJP/Assemblies/src/Patches/PhasedTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_LATCHED_ONTO_EXPIRED_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes LatchedOnto.Expired queued release "
        "messages through the existing owner-scoped queue patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/LatchedOntoExpiredTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_NOSEBLEED_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Nosebleed.StartMessage and StopMessage "
        "EmitMessage shapes through the existing Nosebleed message-pattern family "
        "tests, including singular/plural nose, simple/heavy bleeding, color, and "
        "circulatory-loss variants."
    ),
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
]

ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact queue-only active-effect "
        "targets covered by EffectStaticMessageTranslationPatch; mixed "
        "MessageFrame rows remain residual."
    ),
    "Mods/QudJP/Assemblies/src/Patches/EffectStaticMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CRIPPLE_APPLY_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Cripple.Apply queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/CrippleApplyTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_BUDDING_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Budding Apply/Remove queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/BuddingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CYBERNETIC_REJECTION_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes CyberneticRejectionSyndrome queued "
        "message targets through the existing owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CyberneticRejectionSyndromeTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_EMBOLDENED_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Emboldened Apply/Remove queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/EmboldenedTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_HEALING_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Healing FireEvent/UseEnergyEvent "
        "queued messages through the existing owner patch; Healing.Apply "
        "MessageFrame rows remain separate."
    ),
    "Mods/QudJP/Assemblies/src/Patches/HealingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_STASIS_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Stasis.HandleEvent queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/StasisTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_STRESSED_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Stressed Apply/Remove queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/StressedTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_BLAZE_TONIC_REMOVE_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Blaze_Tonic.Remove queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/BlazeTonicRemoveTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_BOOST_STATISTIC_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes BoostStatistic Apply/Remove queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/BoostStatisticTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_FUNGAL_SPORE_INFECTION_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes FungalSporeInfection.FireEvent "
        "queued messages through the existing owner patch; ApplyFungalInfection "
        "popup/journal rows are tracked separately."
    ),
    "Mods/QudJP/Assemblies/src/Patches/FungalSporeInfectionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_MUTATING_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact Mutating Apply/EndTurn "
        "message and popup rows through the existing owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/MutatingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_BLINKING_TIC_MESSAGE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes BlinkingTicSickness.FireEvent "
        "queued messages through the existing BlinkingTic owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/BlinkingTicTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_MEDITATING_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review closes Meditating.Remove queued messages through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/MeditatingTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_ILL_REMOVE_MESSAGE_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual implementation closes Ill.Remove queued recovery message through the owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/IllRemoveTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_UI_SCREEN_OWNER_EVIDENCE: Final[dict[str, list[str]]] = {
    "character_attribute_line": [
        "Issue #719 residual review covers CharacterAttributeLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/CharacterAttributeLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "character_effect_line": [
        "Issue #719 residual review covers CharacterEffectLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/CharacterEffectLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mod_menu_line": [
        "Issue #719 residual review covers ModMenuLine Update text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ModMenuLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModMenuLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "equipment_line": [
        "Issue #719 residual review covers EquipmentLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EquipmentLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EquipmentLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "help_row": [
        "Issue #719 residual review covers HelpRow setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/HelpRowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/HelpRowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "ability_manager_line": [
        "Issue #719 residual review covers AbilityManagerLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/AbilityManagerLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "inventory_status": [
        (
            "Issue #719 residual review covers InventoryAndEquipmentStatusScreen "
            "UpdateViewFromData text through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/InventoryAndEquipmentStatusScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryAndEquipmentStatusScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "inventory_line": [
        "Issue #719 residual review covers InventoryLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "trade_line": [
        "Issue #719 residual review covers TradeLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "tinkering_status": [
        (
            "Issue #719 residual review covers TinkeringStatusScreen "
            "UpdateViewFromData text through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TinkeringStatusScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "popup_message": [
        "Issue #719 residual review covers PopupMessage ShowPopup text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "popup_get_option": [
        (
            "Issue #719 residual review covers Popup.GetPopupOption as an exact "
            "menu-item helper route, not a broad popup sink."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupGetPopupOptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupGetPopupOptionTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "popup_pick_several": [
        (
            "Issue #719 residual review covers Popup.PickSeveral fixed selection-limit "
            "popup text and generated Accept/Select All/Deselect All button handoff."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupPickSeveralTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickSeveralTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "tinkering_line": [
        "Issue #719 residual review covers TinkeringLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TinkeringLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "factions_line": [
        "Issue #719 residual review covers FactionsLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/FactionsLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FactionsLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "selectable_menu_item": [
        "Issue #719 residual review covers SelectableTextMenuItem SelectChanged text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/SelectableTextMenuItemTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "tinkering_bits": [
        "Issue #719 residual review covers TinkeringBitsLine setData text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TinkeringBitsLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "keybinds_screen": [
        "Issue #719 residual review covers KeybindsScreen QueryKeybinds text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/KeybindsScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeybindsScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mod_manager": [
        "Issue #719 residual review covers ModManagerUI OnSelect text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ModManagerUITranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModManagerUITranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "achievement_view_row": [
        (
            "Issue #719 residual review covers AchievementViewRow SetAchievementData "
            "and SetHiddenData text through the setData owner patch; decompiled "
            "callers dispatch these helpers only from setData."
        ),
        "Mods/QudJP/Assemblies/src/Patches/AchievementViewRowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AchievementViewRowTranslationPatchTests.cs",
    ],
}

ISSUE719_DESCRIPTION_SHORT_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers only the exact Description.GetShortDescription "
        "owner route through the existing short-description patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DescriptionShortDescriptionPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE: Final[dict[str, list[str]]] = {
    "disassembly": [
        (
            "Issue #719 residual review covers Disassembly.Continue and "
            "Disassembly.End popup/queue text through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DisassemblyStartTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "tinkering_build": [
        "Issue #719 residual review covers TinkeringScreen.PerformUITinkerBuild "
        "popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TinkeringBuildPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringBuildPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "zone_generate": [
        (
            "Issue #719 residual review covers ZoneManager.GenerateZone build-failure "
            "queue text and build-loop popups through the exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ZoneManagerGenerateZoneTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ZoneManagerGenerateZoneTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "key_mapping": [
        "Issue #719 residual review covers KeyMappingUI.Show popup text through the exact owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/KeyMappingUiTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeyMappingUiTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "keybinds_handle_menu_option": [
        (
            "Issue #719 residual review covers KeybindsScreen.HandleMenuOption "
            "last-binding and clear-binding popups through the existing exact "
            "KeyMappingUI owner patch, plus the fixed restore-defaults literal "
            "through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/KeyMappingUiTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeyMappingUiTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-options.ja.json",
    ],
    "trade_offer": [
        (
            "Issue #719 residual review covers TradeUI.PerformOffer fresh-water "
            "settlement popups through the existing trade owner templates."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/TradeUiPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "spiral_borer_curio": [
        (
            "Issue #719 residual review covers SpiralBorerCurio.HandleEvent "
            "activation popup text through the single-callsite owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
    ],
    "telekinesis": [
        (
            "Issue #719 residual review covers Telekinesis.HandleEvent, Activate, "
            "and AttemptTelekinesis popups through the exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TelekinesisTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TelekinesisTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "trade_screen_ask_number": [
        (
            "Issue #719 residual review covers TradeScreen.HandleTradeSome "
            "AskNumber text through the existing TradeScreen template route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TradeScreenUiTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupAskNumberTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeScreenUiTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskNumberTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/templates-format.ja.json",
    ],
    "activated_ability_entry_world_map": [
        (
            "Issue #719 residual review covers the fixed ActivatedAbilityEntry "
            "world-map queue message through the existing single-callsite owner route."
        ),
        "scripts/static_producer_closure.py",
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "fetches_runs_off": [
        (
            "Issue #719 residual review covers the Fetches fetch-message queue "
            "construction through the existing single-callsite owner route; the "
            "separate sniff message remains runtime-owned."
        ),
        "scripts/static_producer_closure.py",
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "destroy_on_unequip_confirmation": [
        (
            "Issue #719 residual review covers DestroyOnUnequip.HandleEvent "
            "confirmation popup text through the existing single-callsite owner route."
        ),
        "scripts/static_producer_closure.py",
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mod_info_dependencies": [
        (
            "Issue #719 residual review covers ModInfo.ConfirmDependencies "
            "popup text through the existing owner transpiler."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ModInfoTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/ModManagementTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModInfoTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mod_info_update": [
        "Issue #719 residual review covers ModInfo.ConfirmUpdate popup text through the existing owner transpiler.",
        "Mods/QudJP/Assemblies/src/Patches/ModInfoTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/ModManagementTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModInfoTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mod_scroller_one": [
        "Issue #719 residual review covers ModScrollerOne.OnActivate scripting-disabled popup suffix.",
        "Mods/QudJP/Assemblies/src/Patches/ModScrollerOneTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/ModManagementTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModScrollerOneTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "skills_and_powers_select_node": [
        (
            "Issue #719 residual review covers SkillsAndPowersScreen.SelectNode "
            "popup and queued-message text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersSelectNodePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SkillsAndPowersSelectNodePopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "status_screen_mutation_popup": [
        (
            "Issue #719 residual review covers StatusScreen.ShowMutationPopup "
            "mutation rank popup text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "campfire_nostrums": [
        ("Issue #719 residual review covers Campfire nostrum treatment popups through the existing exact owner patch."),
        "Mods/QudJP/Assemblies/src/Patches/CampfireNostrumsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireNostrumsTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "door_attempt_open": [
        ("Issue #719 residual review covers Door.AttemptOpen queued messages through the existing exact owner patch."),
        "Mods/QudJP/Assemblies/src/Patches/DoorAttemptOpenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "door_hacking_result": [
        (
            "Issue #719 residual review covers Door hacking-result popups through "
            "the existing exact HackingSifrah result owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/HackingSifrahResultTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "hacking_sifrah_result": [
        (
            "Issue #719 residual review covers exact PowerSwitch, TemplarPhylactery, "
            "and CyberneticsTerminal2 hacking-result popups through the existing "
            "HackingSifrah result owner patch, including CyberneticsTerminal2 "
            "partial-success."
        ),
        "Mods/QudJP/Assemblies/src/Patches/HackingSifrahResultTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "leveler_rapid_advancement": [
        ("Issue #719 residual review covers Leveler.RapidAdvancement popups through the existing exact owner patch."),
        "Mods/QudJP/Assemblies/src/Patches/LevelerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/LevelerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "vehicle_seat": [
        (
            "Issue #719 residual review covers VehicleSeat.AttemptPilot pilot-console "
            "popups through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/VehicleSeatTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/VehicleSeatTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "decoy_hologram_emitter_activate": [
        (
            "Issue #719 residual review covers DecoyHologramEmitter.ActivateHologramBracelet "
            "popups through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/DecoyHologramEmitterActivateTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DecoyHologramEmitterActivateTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "teleporter_pair": [
        (
            "Issue #719 residual review covers TeleporterPair.AttemptTeleport cooldown "
            "popups through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TeleporterPairTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TeleporterPairTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "campfire_preserve": [
        (
            "Issue #719 residual review covers Campfire.Preserve and PreserveExotic "
            "preservation-result popups through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfirePreserveTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "zealot_declaim": [
        (
            "Issue #719 residual review covers JoppaZealot and SixDayZealot "
            "declaim EmitMessage/floating text through the existing exact owner patches."
        ),
        "Mods/QudJP/Assemblies/src/Patches/JoppaZealotTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SixDayZealotTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FloatingYellTextTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_companion_ability": [
        (
            "Issue #719 residual review covers GameObject.ChangeCompanionAbilityUse "
            "companion ability popups through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_check_companion_direction": [
        (
            "Issue #719 residual review covers GameObject.CheckCompanionDirection "
            "can't-hear-you popup text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_confirm_use_important": [
        (
            "Issue #719 residual review covers GameObject.ConfirmUseImportant "
            "and ConfirmUseImportantAsync popups through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_toggle_activated_ability": [
        (
            "Issue #719 residual review covers GameObject.ToggleActivatedAbility "
            "queue text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/GameObjectToggleActivatedAbilityTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_stat_popup": [
        (
            "Issue #719 residual review covers exact GameObject stat-gain popup "
            "methods through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectStatPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "proselytization_sifrah_result": [
        (
            "Issue #719 residual review covers the five exact ProselytizationSifrah "
            "result popup methods through the existing owner patch; constructor "
            "and check popup routes remain separate residual rows."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "beguiling_sifrah_result": [
        (
            "Issue #719 residual review covers the five exact BeguilingSifrah "
            "result popup methods through the existing owner patch; constructor "
            "and check popup routes remain separate residual rows."
        ),
        "Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "item_modding_sifrah_result": [
        (
            "Issue #719 residual review covers exact ItemModdingSifrah result "
            "popup methods through the existing owner patch; constructor and "
            "untargeted critical-failure routes remain separate residual rows."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ItemModdingSifrahTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "rebuking_sifrah_result": [
        (
            "Issue #719 residual review covers exact RebukingSifrah critical-failure, "
            "failure, and partial-success popup methods through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/RebukingSifrahTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/RebukingSifrahTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "fabricate_from_self_activate": [
        (
            "Issue #719 residual review covers FabricateFromSelf.Activate generated "
            "fabrication queue text through the exact owner patch, fixed failure "
            "popups through the generic popup dictionary route, and raw-materials "
            "damage text through the existing combat message route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/FabricateFromSelfTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FabricateFromSelfTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "psychometry_inventory_action": [
        (
            "Issue #719 residual review covers Psychometry.HandleEvent(InventoryActionEvent) "
            "artifact-understanding, disassembly, reverse-engineering, and blueprint-learn "
            "popups through the exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PsychometryTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "summoning_curio_inventory_action": [
        (
            "Issue #719 residual review covers SummoningCurio.HandleEvent(InventoryActionEvent) "
            "activation popups through the exact single-callsite owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "scripts/static_producer_closure.py",
    ],
    "conversation_check_lost": [
        (
            "Issue #719 residual review covers ConversationUI.CheckLost Does+popup "
            "lost-recovery text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ConversationCheckLostPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationCheckLostPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "scripts/static_producer_closure.py",
    ],
    "mutation_generated_text": [
        (
            "Issue #719 residual review covers exact mutation generated-text "
            "families whose visible shapes are fully proven by the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationGeneratedTextTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "mass_mind_fire_event": [
        (
            "Issue #719 residual review covers MassMind.FireEvent queued messages "
            "through the exact owner patch and the fixed too-far popup through the "
            "generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/MassMindTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "pack_rat_fire_event": [
        (
            "Issue #719 residual review covers PackRat.FireEvent generated queue/drop "
            "cooldown text through the exact owner patch and the fixed insufficient-junk "
            "popup through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationGeneratedTextTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "precognition_fire_event": [
        (
            "Issue #719 residual review covers Precognition.FireEvent queued vision "
            "messages through the exact owner patch and fixed vision-state popups "
            "through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PrecognitionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PrecognitionTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "eros_teleportation_cast": [
        (
            "Issue #719 residual review covers ErosTeleportation.Cast yell/floating "
            "text through the exact owner patch, reality-stabilized interdict text "
            "through its existing owner route, and fixed teleport failure popups "
            "through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ErosTeleportationTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedInterdictTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FloatingYellTextTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "terrain_travel": [
        (
            "Issue #719 residual review covers TerrainTravel.HandleEvent "
            "debug encounter queue text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TerrainTravelTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TerrainTravelTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "terrain_travel_leaving_cell": [
        (
            "Issue #719 residual review covers TerrainTravel.HandleLeavingCell "
            "queue text and stop-travel prompt through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TerrainTravelTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TerrainTravelTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "journal_screen_handle_delete": [
        (
            "Issue #719 residual review covers JournalScreen.HandleDelete recipe "
            "delete popups through the existing owner patch and the fixed delete-entry "
            "confirmation through the ui-popup dictionary."
        ),
        "Mods/QudJP/Assemblies/src/Patches/JournalScreenPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalScreenPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "polygel": [
        (
            "Issue #719 residual review covers Polygel.HandleEvent identified/morph "
            "popups through the existing single-callsite owner route and fixed fail "
            "literals through the ui-popup dictionary."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "script_call_to_arms_warning": [
        (
            "Issue #719 residual review covers ScriptCallToArms.ShowWarning Otho "
            "yell text through the existing single-callsite owner route and fixed "
            "warning literals through the ui-popup dictionary."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "game_object_factory_blueprint_xml": [
        (
            "Issue #719 residual review covers GameObjectFactory.HandleBlueprintXML "
            "missing-blueprint popup text through the existing single-callsite owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "player_mural_controller": [
        (
            "Issue #719 residual review covers PlayerMuralController.HandleEvent "
            "completion popups through the existing single-callsite owner route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "code_redemption": [
        (
            "Issue #719 residual review covers CodeRedemptionManager redeem "
            "popups through the existing owner route for dynamic download errors "
            "and the generic popup dictionary route for fixed redemption literals."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CodeRedemptionPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CodeRedemptionPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "xrl_core_save_management": [
        (
            "Issue #719 residual review covers XRLCore.SaveManagement old-save "
            "popup text through the existing old-save owner route and fixed "
            "delete/no-save literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/OldSaveContinueMenuPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "examiner_critical_failure": [
        (
            "Issue #719 residual review covers Examiner.ResultCriticalFailure "
            "critical/puzzled popup text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ExaminerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "examiner_make_understanding": [
        (
            "Issue #719 residual review covers Examiner.MakeUnderstood and "
            "MakePartiallyUnderstood popups through the exact Examiner owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ExaminerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "quest_lifecycle_finish_step": [
        (
            "Issue #719 residual review covers Quest.ShowFinishStepPopup "
            "ShowBlock and queued step-completion text through the existing "
            "QuestLifecycle owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/QuestLifecyclePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "single_callsite_misc_popup": [
        (
            "Issue #719 residual review covers exact single-callsite popup families "
            "whose visible text is fully proven by existing owner-route evidence."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "imodification_wish_modify": [
        (
            "Issue #719 residual review covers IModification.WishModify missing "
            "modification/blueprint popups through the existing single-callsite owner "
            "route and the fixed no-modification literal through the ui-popup dictionary."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "sifrah_pure_owner_popup": [
        (
            "Issue #719 residual review covers exact Sifrah pure-owner popup "
            "families whose visible shapes are fully proven by the existing "
            "SifrahPure owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SifrahPureOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "sifrah_token_item_popup": [
        (
            "Issue #719 residual review covers exact SocialSifrahTokenGift and "
            "SocialSifrahTokenItem item-use popup failures through the existing "
            "Sifrah token item owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SifrahTokenItemPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SifrahTokenItemPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "deployable_infrastructure_deploy_one": [
        (
            "Issue #719 residual review covers DeployableInfrastructure.DeployOne "
            "deployment EmitMessage text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/DeployableInfrastructureTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "scripts/static_producer_closure.py",
    ],
}

ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE: Final[dict[str, list[str]]] = {
    "latches_on": [
        "Issue #719 residual review covers LatchesOn.FireEvent messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/LatchesOnTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "tattoo_gun": [
        "Issue #719 residual review covers TattooGun.AttemptTattoo messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TattooGunTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TattooGunTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "beguiling": [
        "Issue #719 residual review covers Beguiling.Cast messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/BeguilingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "engraver": [
        "Issue #719 residual review covers Engraver.AttemptEngrave messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EngraverTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EngraverTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "physics_inventory": [
        "Issue #719 residual review covers Physics.HandleEvent(InventoryActionEvent) through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/PhysicsInventoryActionPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PhysicsInventoryActionPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "iteleporter": [
        "Issue #719 residual review covers ITeleporter.AttemptTeleport messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ITeleporterTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ITeleporterTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "energy_loader": [
        "Issue #719 residual review covers energy loader FireEvent messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EnergyLoaderCannotTakeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EnergyLoaderCannotTakeTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "data_disk": [
        "Issue #719 residual review covers DataDisk.HandleEvent learn popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/DataDiskLearnPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DataDiskLearnPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "pet_either_or": [
        "Issue #719 residual review covers PetEitherOr.explode messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/PetEitherOrExplodeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "bed_chair": [
        "Issue #719 residual review covers bed/chair messages through the existing owner patches.",
        "Mods/QudJP/Assemblies/src/Patches/BedTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ChairTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/BedChairProducerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "liquid_volume": [
        "Issue #719 residual review covers LiquidVolume.Pour messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "stairs_down": [
        "Issue #719 residual review covers StairsDown.CheckPullDown messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerQueueTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "garbage": [
        "Issue #719 residual review covers Garbage.AttemptRifle generated queue "
        "messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/GeneratedQueueDoesVerbTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "energy_cell_socket": [
        "Issue #719 residual review covers EnergyCellSocket.AttemptReplaceCell "
        "popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EnergyCellSocketAccessPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EnergyCellSocketAccessPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "enclosing": [
        "Issue #719 residual review covers Enclosing.EnterEnclosure/ExitEnclosure messages "
        "through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EnclosingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "vehicle_recall": [
        "Issue #719 residual review covers VehicleRecall.HandleEvent messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ClonelingVehicleTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_rename": [
        "Issue #719 residual review covers GameObject.HandleRename popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_object_heal": [
        (
            "Issue #719 residual review covers GameObject.Heal queued healing "
            "and HP-loss messages through the existing exact owner patch and "
            "static producer owner-family evidence."
        ),
        "scripts/static_producer_closure.py",
        "Mods/QudJP/Assemblies/src/Patches/GameObjectHealTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
    ],
    "faction_deed": [
        "Issue #719 residual review covers FactionDeed.HandleEvent map-reveal popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/MapRevealPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MapRevealPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "animate_object": [
        "Issue #719 residual review covers AnimateObject.HandleEvent messages through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/AnimateObjectTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AnimateObjectTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "eel_spawn": [
        "Issue #719 residual review covers EelSpawn.HandleEvent queue and popup routes through the exact owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EelSpawnTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EelSpawnTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "water_ritual_buy_secret": [
        "Issue #719 residual review covers WaterRitualBuySecret.RevealEntry through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/WaterRitualPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "equipment_api_twiddle": [
        "Issue #719 residual review covers EquipmentAPI.TwiddleObject popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/EquipmentApiTwiddleObjectTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EquipmentApiTwiddleObjectTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "campfire_cook": [
        "Issue #719 residual review covers Campfire.Cook availability popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/CampfireCookAvailabilityTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireCookAvailabilityTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "examiner_partial_success": [
        "Issue #719 residual review covers Examiner.ResultPartialSuccess popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ExaminerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "submerged_burrowed_owner": [
        (
            "Issue #719 residual review covers Submerged/Burrowed owner-routed "
            "queue and popup message frames through the exact effect owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SubmergedBurrowedOwnerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SubmergedBurrowedOwnerTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "conversation_physical": [
        "Issue #719 residual review covers physical conversation failure popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ConversationScriptPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationScriptPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "conversation_mental": [
        "Issue #719 residual review covers mental conversation failure popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/ConversationScriptPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationScriptPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "trade_ui_vendor_examine": [
        "Issue #719 residual review covers TradeUI.DoVendorExamine popups through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TradeUiVendorPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiVendorPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "trade_ui_vendor_recharge": [
        (
            "Issue #719 residual review covers TradeUI.DoVendorRecharge "
            "Does+popup recharge text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/TradeUiVendorPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiVendorPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "precognition_before_die": [
        (
            "Issue #719 residual review covers Precognition.OnBeforeDie "
            "message/popup text through the existing exact owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PrecognitionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PrecognitionTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "neutron_flux_pour_explodes": [
        (
            "Issue #719 residual review covers NeutronFluxContainment.HandleEvent"
            "(NeutronFluxPourExplodesEvent) no-containment confirmation through the "
            "single-callsite owner popup route and poured-into/from status failures "
            "through existing MessageFrame verb templates."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    ],
    "neutron_flux_begin_take_action": [
        (
            "Issue #719 residual review covers NeutronFluxContainment.HandleEvent"
            "(BeginTakeActionEvent) travel-warning popup text through the exact "
            "single-callsite owner popup route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "repair_result_critical_failure": [
        (
            "Issue #719 residual review covers Repair.RepairResultCriticalFailure "
            "critical popup text through the exact owner patch and the accidental "
            "destroy XDidYToZ frame through existing MessageFrame verb templates."
        ),
        "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    ],
}


def _static_producer_family_key(family_id: str) -> tuple[str, str, str] | None:
    if "::" not in family_id:
        return None

    source_file, member_id = family_id.split("::", maxsplit=1)
    if "." not in member_id:
        return None

    type_id, member_name = member_id.rsplit(".", maxsplit=1)
    type_name = type_id.rsplit(".", maxsplit=1)[-1]
    return source_file, type_name, member_name


def _static_producer_owner_evidence(
    family_id: str,
    evidence_paths: tuple[str, ...],
) -> list[str]:
    return [
        (f"Issue #719 residual review reuses the static producer owner-patch registry entry: {family_id}"),
        "scripts/static_producer_closure.py",
        *evidence_paths,
    ]


ISSUE719_STATIC_PRODUCER_OWNER_EVIDENCE_BY_KEY: Final[dict[tuple[str, str, str], list[str]]] = {
    key: _static_producer_owner_evidence(
        covered.family_id,
        tuple(dict.fromkeys(evidence.path for evidence in covered.evidence_files)),
    )
    for covered in COVERED_OWNER_FAMILIES
    if covered.inventory_statuses == ("owner_patch_required",)
    if (key := _static_producer_family_key(covered.family_id)) is not None
}

ISSUE719_PLAYER_STATUS_BAR_UI_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review covers PlayerStatusBar.Update direct UI text through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerStatusBarProducerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/PlayerStatusBarProducerTranslationPatchResolutionTests.cs",
]

ISSUE719_ABILITY_MANAGER_SCREEN_UI_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers AbilityManagerScreen.HandleHighlightLeft "
        "direct UI text through the existing owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/AbilityManagerScreenTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerScreenTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/AbilityManagerScreenTranslationPatchResolutionTests.cs",
]

ISSUE719_MAIN_MENU_UI_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review covers MainMenu.Show direct UI text through the existing owner patch.",
    "Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MainMenuLocalizationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_MISSILE_WEAPON_AREA_UI_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers MissileWeaponArea.AfterRender "
        "direct UI text through the existing owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/MissileWeaponAreaTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/UITextSkinTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_TRADE_SCREEN_UI_EVIDENCE: Final[list[str]] = [
    "Issue #719 residual review covers TradeScreen UI text through exact owner patches.",
    "Mods/QudJP/Assemblies/src/Patches/TradeScreenUpdateTotalsTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/TradeScreenUiTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeScreenUpdateTotalsTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_SKILLS_AND_POWERS_LINE_UI_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers SkillsAndPowersLine.setData direct UI "
        "text through the existing exact owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersLineTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SkillsAndPowersLineTranslationPatchTests.cs",
]

ISSUE719_STATUS_LINE_UI_EVIDENCE: Final[dict[str, list[str]]] = {
    "character_mutation_line": [
        (
            "Issue #719 residual review covers CharacterMutationLine.setData "
            "direct UI text through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CharacterMutationLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "quests_line": [
        (
            "Issue #719 residual review covers QuestsLine.setData direct UI text "
            "and static expand/collapse menu descriptions through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/QuestsLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/QuestUiTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "high_scores": [
        "Issue #719 residual review covers HighScoresScreen.Show direct UI text through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/HighScoresScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/HighScoresScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
}

ISSUE719_CHARACTER_STATUS_MUTATION_MENU_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers CharacterStatusScreen static mutation "
        "menu option descriptions through the existing mutation-details owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenMutationDetailsPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CharacterStatusScreenMutationDetailsPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CHERUBIM_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers CherubimSpawner.ReplaceDescription "
        "description text through the existing owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerReplaceDescriptionPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerReplaceDescriptionPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_SAVES_API_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers SavesAPI.ReadSaveJson save-size "
        "description text through the existing exact owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SavesApiReadSaveJsonTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SavesApiReadSaveJsonTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE: Final[dict[str, list[str]]] = {
    "ability_manager": [
        (
            "Issue #719 residual review covers AbilityManagerScreen menu "
            "descriptions through the existing exact owner patches."
        ),
        "Mods/QudJP/Assemblies/src/Patches/AbilityManagerScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/AbilityManagerScreenTranslationPatchResolutionTests.cs",
    ],
    "main_menu": [
        "Issue #719 residual review covers MainMenu menu descriptions through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MainMenuLocalizationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "game_summary": [
        "Issue #719 residual review covers GameSummaryScreen.UpdateMenuBars descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/GameSummaryScreenMenuBarsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameSummaryAndAsleepTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "pick_game_object": [
        "Issue #719 residual review covers PickGameObjectScreen menu descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/PickGameObjectScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickGameObjectScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "trade_screen": [
        "Issue #719 residual review covers TradeScreen menu descriptions through the exact owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/TradeScreenUiTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeScreenUiTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "trade_line_numeric": [
        (
            "Issue #719 residual review covers TradeLine drag count SetText rows as "
            "numeric-only pass-through, not translatable text."
        ),
        (
            "~/dev/coq-decompiled_stable/Qud.UI/TradeLine.cs lines "
            "388, 536, 561, and 619 set dragIndicatorText to {{W|number}}."
        ),
    ],
    "help_screen": [
        "Issue #719 residual review covers HelpScreen.UpdateMenuBars descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/HelpScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/Issue289OrphanRoutePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "keybinds_screen": [
        "Issue #719 residual review covers KeybindsScreen menu descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/KeybindsScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeybindsScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "ability_manager_line": [
        "Issue #719 residual review covers AbilityManagerLine static menu descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/AbilityManagerLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "keybind_row": [
        "Issue #719 residual review covers KeybindRow static menu descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/KeybindRowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeybindRowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "message_log_line": [
        "Issue #719 residual review covers MessageLogLine static menu descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/MessageLogLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/Issue289OrphanRoutePatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "pick_game_object_line": [
        "Issue #719 residual review covers PickGameObjectLine static menu descriptions through the owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/PickGameObjectLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickGameObjectLineTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "qud_mutations_module": [
        (
            "Issue #719 residual review covers QudMutationsModuleWindow.UpdateControls "
            "description rows through the owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/QudMutationsModuleWindowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/QudMutationsModuleWindowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "achievement_view": [
        (
            "Issue #719 residual review covers AchievementView.UpdateMenuBars "
            "descriptions through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/AchievementViewTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AchievementViewTranslationPatchTests.cs",
    ],
    "high_scores_static_menu": [
        (
            "Issue #719 residual review covers HighScoresScreen static menu "
            "descriptions through the existing Show owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/HighScoresScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/HighScoresScreenTranslationPatchTests.cs",
    ],
    "ui_menu_option_description": [
        (
            "Issue #719 residual review covers exact MenuOption.Description owner "
            "families for factions, high scores, keybinds, and character attributes."
        ),
        "Mods/QudJP/Assemblies/src/Patches/UiMenuOptionDescriptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/UiMenuOptionDescriptionTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "filter_bar_category_button": [
        (
            "Issue #719 residual review covers FilterBarCategoryButton static "
            "MenuOption.Description families through the existing SetCategory owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/FilterBarCategoryButtonTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FilterBarCategoryButtonTranslationPatchTests.cs",
    ],
    "static_line_menu_options": [
        (
            "Issue #719 residual review covers static MenuOption.Description "
            "families whose owner line setup/setData routes are translated by "
            "UiMenuOptionDescriptionTranslationPatch."
        ),
        (
            "covered descriptions: Expand, Collapse, Select, Expand All, "
            "Collapse All on ButtonBarButton, FactionsLine/FactionsStatusScreen, "
            "InventoryLine, JournalSultanStatueLine, SkillsAndPowersLine, "
            "Tinkering*Line, and TradeLine."
        ),
        "Mods/QudJP/Assemblies/src/Patches/UiMenuOptionDescriptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/UiMenuOptionDescriptionTranslationPatchTests.cs",
    ],
    "options_screen_static_menu": [
        (
            "Issue #719 residual review covers OptionsScreen default menu "
            "descriptions through the existing Show owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/OptionsLocalizationPatchTests.cs",
    ],
    "options_screen_controls": [
        (
            "Issue #719 residual review covers Options control SetText routes "
            "through the existing OptionsScreen.Show owner patch, which "
            "translates Title and HelpText before control binding."
        ),
        "Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/OptionsLocalizationPatchTests.cs",
        "Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/UITextSkinTranslationPatchTests.cs",
    ],
    "cybernetics_terminal": [
        (
            "Issue #719 residual review covers CyberneticsTerminalScreen.UpdateMenuBars "
            "menu descriptions through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsTerminalScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsTerminalScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "status_screens": [
        "Issue #719 residual review covers StatusScreensScreen menu descriptions through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/StatusScreensScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreensScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "inventory_equipment_status": [
        (
            "Issue #719 residual review covers InventoryAndEquipmentStatusScreen "
            "menu descriptions through the existing owner patch."
        ),
        "Mods/QudJP/Assemblies/src/Patches/InventoryAndEquipmentStatusScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryAndEquipmentStatusScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "journal_status": [
        "Issue #719 residual review covers JournalStatusScreen menu descriptions through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/JournalStatusScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalStatusScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "book_screen": [
        "Issue #719 residual review covers BookScreen menu descriptions through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/BookScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/BookScreenTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
    "credits": [
        "Issue #719 residual review covers Credits.UpdateMenuBars descriptions through the existing owner patch.",
        "Mods/QudJP/Assemblies/src/Patches/CreditsMenuBarsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CreditsMenuBarsTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    ],
}

ISSUE719_STATIC_LINE_MENU_OPTION_FAMILIES: Final[frozenset[str]] = frozenset(
    {
        "Qud.UI/ButtonBarButton.cs::ButtonBarButton.itemOptions",
        "Qud.UI/FactionsLine.cs::FactionsLine.categoryExpandOptions",
        "Qud.UI/FactionsLine.cs::FactionsLine.categoryCollapseOptions",
        "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.EXPAND_ALL",
        "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.COLLAPSE_ALL",
        "Qud.UI/InventoryLine.cs::InventoryLine.categoryExpandOptions",
        "Qud.UI/InventoryLine.cs::InventoryLine.categoryCollapseOptions",
        "Qud.UI/JournalSultanStatueLine.cs::JournalSultanStatueLine.categoryExpandOptions",
        "Qud.UI/JournalSultanStatueLine.cs::JournalSultanStatueLine.categoryCollapseOptions",
        "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.categoryExpandOptions",
        "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.categoryCollapseOptions",
        "Qud.UI/TinkeringBitsLine.cs::TinkeringBitsLine.categoryExpandOptions",
        "Qud.UI/TinkeringBitsLine.cs::TinkeringBitsLine.categoryCollapseOptions",
        "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.categoryExpandOptions",
        "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.categoryCollapseOptions",
        "Qud.UI/TinkeringLine.cs::TinkeringLine.categoryExpandOptions",
        "Qud.UI/TinkeringLine.cs::TinkeringLine.categoryCollapseOptions",
        "Qud.UI/TradeLine.cs::TradeLine.categoryExpandOptions",
        "Qud.UI/TradeLine.cs::TradeLine.categoryCollapseOptions",
        "Qud.UI/TradeLine.cs::TradeLine.itemOptions",
    }
)

ISSUE719_JOURNAL_ENTRY_DISPLAY_TEXT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers IBaseJournalEntry.GetDisplayText "
        "and derived journal note display text through the existing exact owner patch."
    ),
    "Mods/QudJP/Assemblies/src/Patches/JournalEntryDisplayTextPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalEntryDisplayTextPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_ELEMENTAL_PSEUDOPOD_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #781 covers ElementalJelly/Panhumor SetupPod direct Render.DisplayName "
        "overrides through exact owner patches."
    ),
    "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
    "Mods/QudJP/Assemblies/src/Patches/ElementalPseudopodDisplayNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ElementalPseudopodDisplayNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]
ISSUE719_PSEUDOPOD_DEATH_MESSAGE_FRAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 pseudopod death review promotes ElementalJelly/Panhumor "
        "BeforeDeathRemoval frames because both exact owners call "
        'DidX("explode", null, "!", ...), and the MessageFrame dictionary '
        "already owns the explode verb frame."
    ),
    "decompiled sources: XRL.World.Parts/ElementalJelly.cs lines 246-258; XRL.World.Parts/Panhumor.cs lines 168-181",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
]

ISSUE719_GAS_GENERATION_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #781 covers GasGeneration.SyncFromBlueprint generated mutation "
        "description text through an exact owner patch."
    ),
    "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
    "Mods/QudJP/Assemblies/src/Patches/GasGenerationDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/GasGenerationDescriptionTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GasGenerationDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #781 covers selected non-mutation activated ability provider "
        "registration/update names through exact owner patches."
    ),
    "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityMiscProviderTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActivatedAbilityNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActivatedAbilityMiscProviderTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers selected mutation activated "
        "ability registration names through exact Mutate owner patches."
    ),
    "https://github.com/ToaruPen/coq-japanese_stable/issues/719",
    "Mods/QudJP/Assemblies/src/Patches/MutationActivatedAbilityNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActivatedAbilityNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationActivatedAbilityNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers selected skill activated "
        "ability registration names through exact skill owner patches."
    ),
    "https://github.com/ToaruPen/coq-japanese_stable/issues/719",
    "Mods/QudJP/Assemblies/src/Patches/SkillActivatedAbilityNameTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActivatedAbilityNameTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SkillActivatedAbilityNameTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CHARGEN_DIRECT_UI_EVIDENCE: Final[list[str]] = [
    (
        "Issue #781 covers remaining chargen direct UI text through exact "
        "AttributeSelectionControl and QudSubtypeModuleWindow owner patches."
    ),
    "https://github.com/ToaruPen/coq-japanese_stable/issues/781",
    "Mods/QudJP/Assemblies/src/Patches/CharGenDirectUiTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CharGenDirectUiTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers selected chargen menu option "
        "and build-library selection text through exact owner iterator postfix "
        "coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CharGenMenuOptionOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CharGenMenuOptionOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_URCHIN_BELCHER_CTOR_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers UrchinBelcher constructor "
        "description and command text through exact ctor owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/UrchinBelcherTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/UrchinBelcherTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers selected cybernetics "
        "description assignments through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CyberneticsDescriptionAssignmentTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsDescriptionAssignmentTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_MISC_DESCRIPTION_ASSIGNMENT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers selected miscellaneous "
        "description assignments through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DescriptionAssignmentOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionAssignmentOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_WINGS_DEFAULT_EQUIPMENT_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers Wings default-equipment "
        "body-part description assignment through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/WingsDefaultEquipmentDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DescriptionAssignmentOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionAssignmentOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_BANNER_DESCRIPTION_ASSIGNMENT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers Banner generated short-description "
        "rules through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/BannerDescriptionAssignmentTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/DescriptionAssignmentOwnerTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionAssignmentOwnerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_DECOY_HOLOGRAM_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers DecoyHologramEmitter generated "
        "hologram Description.Short text through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DecoyHologramDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DecoyHologramDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_GAME_OBJECT_ACTIVATED_ABILITY_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers GameObject DescribeActivatedAbility "
        "template output assignment through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/GameObjectActivatedAbilityDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectActivatedAbilityDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_FABRICATE_ABILITY_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers FabricateFromSelf "
        "AbilityDescription through exact property getter postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/FabricateFromSelfAbilityDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/FabricateFromSelfAbilityDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_HAGGLING_SIFRAH_RESULT_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers HagglingSifrah fixed result "
        "Description assignments through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/HagglingSifrahResultDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HagglingSifrahResultDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_SIFRAH_TOKEN_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers selected Sifrah token "
        "Description assignments through exact token constructor/setter owner "
        "postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/SifrahTokenDescriptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/SifrahTokenDescriptionTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SifrahTokenDescriptionTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_SIFRAH_TOKEN_NO_ARG_DESCRIPTION_FAMILIES: Final[frozenset[str]] = frozenset(
    f"XRL.World/{type_name}.cs::{type_name}.{type_name}()"
    for type_name in (
        "PsionicSifrahTokenApplyAncientLore",
        "PsionicSifrahTokenApplyIntellect",
        "PsionicSifrahTokenCalmMind",
        "PsionicSifrahTokenDiscipline",
        "PsionicSifrahTokenEffectNosebleed",
        "PsionicSifrahTokenEmpathy",
        "PsionicSifrahTokenExertWill",
        "PsionicSifrahTokenTelepathy",
        "PsionicSifrahTokenTenfoldPathBin",
        "PsionicSifrahTokenTenfoldPathHod",
        "PsionicSifrahTokenTenfoldPathHok",
        "PsionicSifrahTokenTenfoldPathKet",
        "PsionicSifrahTokenTenfoldPathKhu",
        "PsionicSifrahTokenTenfoldPathRet",
        "PsionicSifrahTokenTenfoldPathSed",
        "PsionicSifrahTokenTenfoldPathTza",
        "PsionicSifrahTokenTenfoldPathVur",
        "PsionicSifrahTokenTenfoldPathYis",
        "PsionicSifrahTokenThePowerOfLove",
        "RitualSifrahTokenAttributeSacrifice",
        "RitualSifrahTokenBit",
        "RitualSifrahTokenEffectAsleep",
        "RitualSifrahTokenEffectBleeding",
        "RitualSifrahTokenEffectCardiacArrest",
        "RitualSifrahTokenEffectConfused",
        "RitualSifrahTokenEffectDazed",
        "RitualSifrahTokenEffectDisoriented",
        "RitualSifrahTokenEffectExhausted",
        "RitualSifrahTokenEffectIll",
        "RitualSifrahTokenEffectLost",
        "RitualSifrahTokenEffectPoisoned",
        "RitualSifrahTokenEffectShaken",
        "RitualSifrahTokenEffectShatterMentalArmor",
        "RitualSifrahTokenEffectTerrified",
        "RitualSifrahTokenFood",
        "RitualSifrahTokenGift",
        "RitualSifrahTokenHookah",
        "RitualSifrahTokenInvokeHigherBeing",
        "RitualSifrahTokenItem",
        "RitualSifrahTokenLiquid",
        "RitualSifrahTokenPrayHumbly",
        "RitualSifrahTokenRecountAccomplishments",
        "RitualSifrahTokenScourging",
        "RitualSifrahTokenSingAHistoricalEpic",
        "RitualSifrahTokenSingHymn",
        "RitualSifrahTokenThePowerOfLove",
        "SocialSifrahTokenApplySocialCoprocessor",
        "SocialSifrahTokenBit",
        "SocialSifrahTokenBoastOfAccomplishments",
        "SocialSifrahTokenCharge",
        "SocialSifrahTokenCrackAJoke",
        "SocialSifrahTokenDebateRationally",
        "SocialSifrahTokenDisplayABarathrumiteToken",
        "SocialSifrahTokenDisplayAFarmersToken",
        "SocialSifrahTokenDisplayAMerchantsToken",
        "SocialSifrahTokenDisplayAMinstrelsToken",
        "SocialSifrahTokenEffectLovesick",
        "SocialSifrahTokenEffectShamed",
        "SocialSifrahTokenEmpathy",
        "SocialSifrahTokenFlatterInsincerely",
        "SocialSifrahTokenFlirtSuggestively",
        "SocialSifrahTokenGift",
        "SocialSifrahTokenHookah",
        "SocialSifrahTokenInvokeAncientCompacts",
        "SocialSifrahTokenItem",
        "SocialSifrahTokenLeverageBeingFavored",
        "SocialSifrahTokenLeverageBeingLoved",
        "SocialSifrahTokenLeverageBeingTrueKin",
        "SocialSifrahTokenLiquid",
        "SocialSifrahTokenListenSympathetically",
        "SocialSifrahTokenOfferMaintenanceServices",
        "SocialSifrahTokenPayACompliment",
        "SocialSifrahTokenPostureIntimidatingly",
        "SocialSifrahTokenRailAgainstInjustice",
        "SocialSifrahTokenReadFromTheCanticlesChromaic",
        "SocialSifrahTokenScanning",
        "SocialSifrahTokenSecret",
        "SocialSifrahTokenSociableChat",
        "SocialSifrahTokenSpinATaleOfWoe",
        "SocialSifrahTokenTelepathy",
        "SocialSifrahTokenTellAnInspiringTale",
        "SocialSifrahTokenTenfoldPathSed",
        "SocialSifrahTokenThePowerOfLove",
        "TinkeringSifrahTokenAdvancedToolkit",
        "TinkeringSifrahTokenBit",
        "TinkeringSifrahTokenCharge",
        "TinkeringSifrahTokenComputePower",
        "TinkeringSifrahTokenCopperWire",
        "TinkeringSifrahTokenCreationKnowledge",
        "TinkeringSifrahTokenLiquid",
        "TinkeringSifrahTokenPhysicalManipulation",
        "TinkeringSifrahTokenPsychometry",
        "TinkeringSifrahTokenScanning",
        "TinkeringSifrahTokenTelekinesis",
        "TinkeringSifrahTokenTenfoldPathBin",
        "TinkeringSifrahTokenTenfoldPathHok",
        "TinkeringSifrahTokenToolkit",
        "TinkeringSifrahTokenVisualInspection",
    )
)
ISSUE719_SIFRAH_TOKEN_DYNAMIC_DESCRIPTION_FAMILIES: Final[frozenset[str]] = (
    frozenset(
        f"XRL.World/{type_name}.cs::{type_name}.{type_name}(int)"
        for type_name in (
            "PsionicSifrahTokenEffectNosebleed",
            "RitualSifrahTokenEffectAsleep",
            "RitualSifrahTokenEffectBleeding",
            "RitualSifrahTokenEffectCardiacArrest",
            "RitualSifrahTokenEffectConfused",
            "RitualSifrahTokenEffectDazed",
            "RitualSifrahTokenEffectDisoriented",
            "RitualSifrahTokenEffectExhausted",
            "RitualSifrahTokenEffectIll",
            "RitualSifrahTokenEffectLost",
            "RitualSifrahTokenEffectPoisoned",
            "RitualSifrahTokenEffectShaken",
            "RitualSifrahTokenEffectShatterMentalArmor",
            "RitualSifrahTokenEffectTerrified",
            "SocialSifrahTokenCharge",
            "SocialSifrahTokenEffectLovesick",
            "SocialSifrahTokenEffectShamed",
            "TinkeringSifrahTokenCharge",
            "TinkeringSifrahTokenComputePower",
        )
    )
    | frozenset(
        f"XRL.World/{type_name}.cs::{type_name}.{type_name}(string)"
        for type_name in (
            "RitualSifrahTokenFood",
            "RitualSifrahTokenGift",
            "RitualSifrahTokenItem",
            "RitualSifrahTokenLiquid",
            "SocialSifrahTokenGift",
            "SocialSifrahTokenItem",
            "SocialSifrahTokenLiquid",
            "TinkeringSifrahTokenCreationKnowledge",
            "TinkeringSifrahTokenLiquid",
        )
    )
    | frozenset(
        f"XRL.World/{type_name}.cs::{type_name}.{type_name}(BitType)"
        for type_name in (
            "RitualSifrahTokenBit",
            "SocialSifrahTokenBit",
            "TinkeringSifrahTokenBit",
        )
    )
    | frozenset(
        f"XRL.World/{type_name}.cs::{type_name}.{type_name}(Scanning.Scan)"
        for type_name in (
            "SocialSifrahTokenScanning",
            "TinkeringSifrahTokenScanning",
        )
    )
)
ISSUE719_SIFRAH_TOKEN_GET_DESCRIPTION_FAMILIES: Final[frozenset[str]] = frozenset(
    f"XRL.World/{type_name}.cs::{type_name}.GetDescription(SifrahGame,SifrahSlot,GameObject)"
    for type_name in (
        "SocialSifrahTokenGift",
        "SocialSifrahTokenItem",
        "SocialSifrahTokenLeverageBeingFavored",
        "SocialSifrahTokenLeverageBeingLoved",
        "SocialSifrahTokenSecret",
        "TinkeringSifrahTokenBit",
        "TinkeringSifrahTokenCopperWire",
        "TinkeringSifrahTokenLiquid",
    )
)

ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers preset CookingRecipe "
        "GetDescription() methods through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CookingEffectTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/CookingEffectFragmentTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingEffectTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_ACTION_EFFECT_DESCRIPTION_RETURN_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers small action/effect "
        "description-return owners through exact owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/ActionEffectDescriptionReturnTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActionEffectDescriptionReturnTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActionEffectDescriptionReturnTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 implementation queue covers CyberneticsChoice, TinkerData, "
        "and all tracked GameObjectUnit description-return owners through exact "
        "owner postfix coverage."
    ),
    "Mods/QudJP/Assemblies/src/Patches/DescriptionDetailReturnTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/DescriptionDetailReturnTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionDetailReturnTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_CHARGEN_CUSTOMIZE_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes exact QudCustomizeCharacterModuleWindow "
        "literals already targeted by CharGenCustomizeTranslationPatch, including "
        "selection labels and async gender/pronoun/pet popups."
    ),
    "Mods/QudJP/Assemblies/src/Patches/CharGenCustomizeTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CharGenProducerTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/CharGenProducerTranslationPatchResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json",
]
ISSUE719_BUILD_LIBRARY_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes QudBuildLibraryModuleWindow AddBuild/onSelect/"
        "HandleMenuOption popup text through existing PopupMessage, AskString, "
        "and popup template dictionary routes."
    ),
    "decompiled owner source: XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs lines 56-139",
    "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json",
]
ISSUE719_BUILD_SUMMARY_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes QudBuildSummaryModuleWindow.HandleMenuOption popup text "
        "through the existing PopupMessage dictionary route and the shared "
        "QudBuildLibraryModuleWindow.AddBuild path."
    ),
    "decompiled owner source: XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs lines 92-104",
    "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json",
]
ISSUE719_QUD_MUTATION_VARIANT_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes QudMutationsModuleWindow.SelectVariant through a "
        "variant-picker owner route for the fixed Choose variant title."
    ),
    "decompiled owner source: XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs lines 373-389",
    "Mods/QudJP/Assemblies/src/Patches/QudMutationsModuleWindowVariantPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/QudMutationsModuleWindowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-chargen-supplement.ja.json",
]
ISSUE719_BASE_MUTATION_VARIANT_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes BaseMutation.SelectVariant through an owner route "
        "for the fixed Choose variant title while leaving variant option text "
        "to mutation display-name and validity-message owners."
    ),
    "decompiled owner source: XRL.World.Parts.Mutation/BaseMutation.cs lines 993-1021",
    "Mods/QudJP/Assemblies/src/Patches/BaseMutationSelectVariantPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/BaseMutationSelectVariantPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-chargen-supplement.ja.json",
]
ISSUE719_GENDER_CUSTOMIZE_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes Gender.CustomizeProcess through the shared "
        "BasePronounProvider customize owner scope for duplicate-name popups "
        "and a PopupAskString template for the fixed name prompt."
    ),
    "decompiled owner source: XRL.World/Gender.cs lines 246-267",
    "Mods/QudJP/Assemblies/src/Patches/BasePronounProviderCustomizePopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/BasePronounProviderCustomizePopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_EMBARK_BUILDER_VALIDATION_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes EmbarkBuilder.checkStateAsync by translating fixed "
        "validation titles and the Continue anyway suffix while leaving dynamic "
        "DataErrors/DataWarnings bodies to their window owners."
    ),
    "decompiled owner source: XRL.CharacterBuilds/EmbarkBuilder.cs lines 188-201",
    "Mods/QudJP/Assemblies/src/Patches/EmbarkBuilderValidationPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/EmbarkBuilderValidationPopupTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json",
]
ISSUE719_STATUS_AND_KEYBIND_OPTION_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes fixed status/options PickOption menus through the "
        "shared PopupPickOption route and dictionary leaves, with route-local "
        "toggle patterns preserving selected-option color markup."
    ),
    "decompiled owner sources: Qud.UI/FactionsStatusScreen.cs lines 188-200; "
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs lines 608-632; "
    "XRL.UI/CommandBindingManager.cs lines 933-945",
    "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
]
ISSUE719_QUD_MUTATION_MENU_POPUP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 closes QudMutationsModuleWindow.HandleMenuOption through "
        "an exact owner route for the ShowPoints popup title."
    ),
    "decompiled owner source: XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs lines 353-360",
    "Mods/QudJP/Assemblies/src/Patches/QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/QudMutationsModuleWindowTranslationPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_LOOK_TOOLTIP_CONTENT_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review closes Look tooltip UI assignment rows "
        "whose BodyText is produced by the already patched "
        "Look.GenerateTooltipContent(GameObject) owner route."
    ),
    "Mods/QudJP/Assemblies/src/Patches/LookTooltipContentPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/LookTooltipContentPatchTests.cs",
]

ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE: Final[dict[str, list[str]]] = {
    "burrowing_claws_check_dig": [
        (
            "Issue #719 residual review covers the exact fixed Popup.ShowFail "
            "literal in BurrowingClaws.CheckDig through the generic popup "
            "dictionary route; no owner patch is required for this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "main_menu_quit": [
        (
            "Issue #719 residual review covers the exact fixed Popup.ShowYesNoAsync "
            "literal in MainMenu.Quit through the generic popup dictionary route; "
            "no owner patch is required for this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-default.ja.json",
    ],
    "keybinds_exit_save_prompt": [
        (
            "Issue #719 residual review covers the exact fixed "
            "Popup.ShowYesNoCancelAsync literal in KeybindsScreen.Exit through "
            "the generic popup dictionary route; no owner patch is required for "
            "this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-options.ja.json",
    ],
    "teleprojector_end_domination": [
        (
            "Issue #719 residual review covers the exact fixed Popup.Show literal "
            "in Teleprojector.EndDomination through the generic popup dictionary "
            "route; no owner patch is required for this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "tutorial_step_current_zone": [
        (
            "Issue #719 residual review covers the exact fixed Popup.Show literal "
            "in TutorialStep.ConstrainToCurrentZone through the generic popup "
            "dictionary route; no owner patch is required for this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "golem_quest_mound_ready": [
        (
            "Issue #719 residual review covers the exact fixed Popup.Show literal "
            "in GolemQuestMound.CheckCompletion through the generic popup dictionary "
            "route; no owner patch is required for this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "pax_klanq_under_construction": [
        (
            "Issue #719 residual review covers the exact fixed Popup.ShowSpace "
            "literal in PaxKlanqIPresumeSystem.UnderConstructionMessage through "
            "the generic popup dictionary route; no owner patch is required for "
            "this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSpaceTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowSpaceTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "ambient_stabilization": [
        (
            "Issue #719 residual review covers the exact fixed Popup.Show literal "
            "in AmbientStabilization.Stabilize through the generic popup dictionary "
            "route; no owner patch is required for this stable leaf."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "reality_distortion_check_early_exit": [
        (
            "Issue #719 residual review covers the exact fixed CheckEarlyExit "
            "confirmation literals in RealityDistortionSifrah through the generic "
            "popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "sifrah_game_completion_prompts": [
        (
            "Issue #719 residual review covers exact fixed SifrahGame incomplete-turn "
            "and early-exit confirmation literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "baetyl_offering_fixed_popup_results": [
        (
            "Issue #719 residual review covers BaetylOfferingSifrah.CheckOutOfOptions "
            "and fixed result Popup.Show/ShowFail literals through existing generic "
            "popup/message dictionary coverage; no owner patch is required for these "
            "stable leaves."
        ),
        "scripts/static_producer_closure.py",
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
        "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
    ],
    "sifrah_token_fixed_use_failures": [
        (
            "Issue #719 residual review covers exact fixed Sifrah token CheckTokenUse "
            "failure literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "sifrah_fixed_popup_leafs": [
        (
            "Issue #719 residual review covers fixed Sifrah popup result, "
            "out-of-options, and token-use failure literals through the generic "
            "popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "game_object_check_frozen": [
        (
            "Issue #719 residual review covers the exact fixed CheckFrozen "
            "Popup.ShowFail literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "mouse_blocker_enable_mouse": [
        (
            "Issue #719 residual review covers the exact fixed MouseBlocker "
            "Popup.ShowYesNoAsync literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "script_call_to_arms_spawn_parties": [
        (
            "Issue #719 residual review covers the exact fixed collapse popup literal "
            "in ScriptCallToArms.spawnParties through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "qud_specific_boot_handlers_game_start": [
        (
            "Issue #719 residual review covers the exact fixed embark popup literal "
            "in QudSpecificBootHandlersModule.handleBootEvent through the generic "
            "popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "ascension_system_wait_prompts": [
        (
            "Issue #719 residual review covers the exact fixed AscensionSystem "
            "wait and Barathrum confirmation Popup.ShowYesNo literals through "
            "the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "golem_quest_selection_wish_finish": [
        (
            "Issue #719 residual review covers the exact fixed "
            "GolemQuestSelection.WishFinishGolem Popup.Show literal through the "
            "generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "cloneling_fire_event_refresh": [
        (
            "Issue #719 residual review covers the exact fixed Cloneling.FireEvent "
            "Popup.Show refresh literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
    ],
    "exhausted_fixed_popups": [
        (
            "Issue #719 residual review covers exact fixed Exhausted popup literals "
            "through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "lost_remove": [
        (
            "Issue #719 residual review covers exact fixed Lost.Remove "
            "Popup.ShowSpace literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSpaceTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowSpaceTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "glotrot_ask_pulldown": [
        (
            "Issue #719 residual review covers the exact fixed Glotrot.AskPulldown "
            "Popup.ShowYesNo literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "ark_core_start_end": [
        (
            "Issue #719 residual review covers the exact fixed ArkCore.StartEnd "
            "Popup.Show literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "main_menu_redeem_code": [
        (
            "Issue #719 residual review covers the exact fixed MainMenu.SelectedInfo "
            "AskStringAsync prompt through the generic ask-string popup route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupAskStringTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupAskStringTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "qud_chargen_last_character": [
        (
            "Issue #719 residual review covers the exact fixed QudChartypeModule "
            "Popup.ShowAsync last-character failure literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "sunder_mind_targeting": [
        (
            "Issue #719 residual review covers exact fixed SunderMind.FireEvent "
            "targeting Popup.ShowYesNo/ShowFail literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "switch_fire_event": [
        (
            "Issue #719 residual review covers exact fixed Switch.FireEvent "
            "Popup.Show literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "stairs_up_key_standin": [
        (
            "Issue #719 residual review covers the exact fixed StairsUp.FireEvent "
            "Popup.Show key-standin literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "cherubim_lock_chime": [
        (
            "Issue #719 residual review covers the exact fixed CherubimLock.Chime "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/world-parts.ja.json",
    ],
    "teleport_on_eat": [
        (
            "Issue #719 residual review covers the exact fixed TeleportOnEat.FireEvent "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "dynamic_quest_find_target": [
        (
            "Issue #719 residual review covers the exact fixed DynamicQuestsGameState "
            "FindQuestTarget Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "mod_disguise_already_wearing": [
        (
            "Issue #719 residual review covers the exact fixed ModDisguise.FireEvent "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "psionic_migraines_equip": [
        (
            "Issue #719 residual review covers the exact fixed PsionicMigraines.FireEvent "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "frost_webs_out_of_range": [
        (
            "Issue #719 residual review covers the exact fixed FrostWebs.FireEvent "
            "Popup.ShowFail range literal through the existing popup template route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "cell_invalid_physics_duplication": [
        (
            "Issue #719 residual review covers the exact fixed Cell.LogInvalidPhysics "
            "Popup.Show duplication-glitch literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "gas_disease_apply_sick": [
        (
            "Issue #719 residual review covers the exact fixed GasDisease.ApplyDisease "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "skittish_startled": [
        (
            "Issue #719 residual review covers the exact fixed Skittish.LoseControl "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "time_cube_fraudulent": [
        (
            "Issue #719 residual review covers the exact fixed TimeCube.Activate "
            "Popup.Show color-marked literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "terrain_travel_fungal_lost": [
        (
            "Issue #719 residual review covers the exact fixed TerrainTravelFungal.FireEvent "
            "Popup.ShowBlock literal through the generic ShowBlock popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "xrl_game_save_error": [
        (
            "Issue #719 residual review covers the exact fixed XRLGame.SaveGameError "
            "Popup.ShowFailAsync literal through the ShowAsync popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "cyclopean_prism_ptoh_annoyed": [
        (
            "Issue #719 residual review covers the exact fixed CyclopeanPrism.PtohAnnoyed "
            "Popup.Show literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "domination_break": [
        (
            "Issue #719 residual review covers exact fixed Domination.BreakDomination "
            "and Metempsychosis Popup.Show literals through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "time_cubed_apply": [
        (
            "Issue #719 residual review covers the exact fixed TimeCubed.Apply "
            "Popup.ShowBlock color-marked literal through the generic ShowBlock popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "sticky_tongue_missing_tongue": [
        (
            "Issue #719 residual review covers the exact fixed StickyTongue.HandleEvent "
            "Popup.Show missing-tongue literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "crungle_gaze_drowsy": [
        (
            "Issue #719 residual review covers the exact fixed CrungleGaze.FireLine "
            "Popup.Show drowsy literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "psychometry_bonus_unusable": [
        (
            "Issue #719 residual review covers the exact fixed Psychometry.HandleEvent(GetTinkeringBonusEvent) "
            "Popup.ShowYesNo unusable-Psychometry literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "skills_wish_skill_missing": [
        (
            "Issue #719 residual review covers the exact fixed Skills.WishSkill "
            "Popup.Show missing-skill literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "tonic_fixed_popup_effects": [
        (
            "Issue #719 residual review covers exact fixed tonic effect Popup.Show/ShowYesNo "
            "literals through the existing generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/world-effects-tonics.ja.json",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "ambient_reality_stabilized_diffuses": [
        (
            "Issue #719 residual review covers the exact fixed AmbientRealityStabilized "
            "Popup.Show diffusion literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "pax_infect_limb_rejects": [
        (
            "Issue #719 residual review covers the exact fixed PaxInfectLimb.Infect "
            "Popup.Show rejection literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "water_ritual_learn_skill_points": [
        (
            "Issue #719 residual review covers the exact fixed WaterRitualLearnSkill.HandleEvent "
            "Popup.ShowFail skill-points literal through the generic popup dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "tinker_data_data_disk_pick_blueprint": [
        (
            "Issue #719 residual review covers the exact fixed TinkerData.DataDisk "
            "Popup.PickOption blueprint title through the generic PickOption dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
    "cybernetics_custom_visage_pick_faction": [
        (
            "Issue #719 residual review covers the exact fixed CyberneticsCustomVisage.ApplyVisage "
            "Popup.PickOption title through the generic PickOption dictionary route."
        ),
        "Mods/QudJP/Assemblies/src/Patches/PopupPickOptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs",
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
    ],
}

ISSUE719_RESIDUAL_CLOSURE_OVERLAY: Final[dict[str, ClosureOverlayEntry]] = {
    "XRL.World.Parts.Mutation/LifeDrain.cs::LifeDrain.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_LIFE_DRAIN_POPUP_EVIDENCE,
    },
    "XRL.World.Parts/Mutations.cs::Mutations.WishMutation(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_WISH_MUTATION_POPUP_EVIDENCE,
    },
    "XRL.World.Parts/Shrine.cs::Shrine.DesecrateShrine(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SHRINE_DESECRATE_POPUP_EVIDENCE,
    },
    "Qud.UI/ModManagerUI.cs::ModManagerUI.OnCancel()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MOD_MANAGER_CANCEL_EVIDENCE,
    },
    "XRL/PopulationManager.cs::PopulationManager.RollOneFrom(string,Dictionary<string,string>,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_POPULATION_ROLL_ONE_POPUP_EVIDENCE,
    },
    "Qud.API/ConversationsAPI.cs::ConversationsAPI.chooseOneItem(List<GameObject>,string,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CONVERSATIONS_API_REWARD_PICK_EVIDENCE,
    },
    (
        "XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs::"
        "DynamicQuestRewardElement_ChoiceFromPopulation.award()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DYNAMIC_QUEST_REWARD_CHOICE_EVIDENCE,
    },
    "XRL/CodaSystem.cs::CodaSystem.EndGamePrompt()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CODA_ENDGAME_PROMPT_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/EndGame.cs::EndGame.HandleEvent(EnterElementEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CONVERSATION_ENDGAME_CONFIRM_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/GiveArtifact.cs::GiveArtifact.HandleEvent(EnterElementEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CONVERSATION_GIVE_ARTIFACT_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/GiveReshephSecret.cs::GiveReshephSecret.HandleEvent(EnterElementEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CONVERSATION_RESHEPH_SECRET_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/WaterRitualSellSecret.cs::WaterRitualSellSecret.Share()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CONVERSATION_WATER_RITUAL_SELL_SECRET_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::"
        "QudMutationsModuleWindow.HandleMenuOption(MenuOption)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_QUD_MUTATION_MENU_POPUP_EVIDENCE,
    },
    (
        "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs::"
        "FindASiteDynamicQuestManager.DynamicQuestWhere()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIND_SITE_DYNAMIC_QUEST_WHERE_EVIDENCE,
    },
    "XRL.UI.Framework/FrameworkSearchInput.cs::FrameworkSearchInput.ChangeValue()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FRAMEWORK_SEARCH_INPUT_EVIDENCE,
    },
    "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.showScreen(XRL.World.GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ABILITY_MANAGER_EMPTY_POPUP_EVIDENCE,
    },
    (
        "XRL.World.Conversations.Parts/WaterRitualRandomMutation.cs::"
        "WaterRitualRandomMutation.HandleEvent(EnteredElementEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_WATER_RITUAL_RANDOM_MUTATION_POPUP_EVIDENCE,
    },
    "XRL.World.Parts/QuickenMind.cs::QuickenMind.Activate(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsStasisEntangler.cs::"
        "CyberneticsStasisEntangler.ActivateStasisEntangler(GameObject,GameObject,IEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/ModGlassArmor.cs::ModGlassArmor.HandleEvent(BeforeApplyDamageEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsStasisArena.cs::"
        "CyberneticsStasisArena.ActivateStasisArena(GameObject,GameObject,IEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/Stomach.cs::Stomach.HandleEvent(InduceVomitingEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/CooldownAmmoLoader.cs::CooldownAmmoLoader.GetCoolingDownMessage()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/LiquidAmmoLoader.cs::LiquidAmmoLoader.GetStatusMessage(ActivePartStatus)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/PoweredFloating.cs::PoweredFloating.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    (
        "XRL.World.Parts/ConversationScript.cs::"
        "ConversationScript.AttemptConversation(GameObject,GameObject,GameObject,GameObject,"
        "ConversationXMLBlueprint,int,bool,bool,bool?,IEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    (
        "XRL.World.Parts/ElectricalDischargeLoader.cs::"
        "ElectricalDischargeLoader.GetStatusMessage(ActivePartStatus)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.HandleEvent(LoadAmmoEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/EnergyAmmoLoader.cs::EnergyAmmoLoader.GetStatusMessage(ActivePartStatus)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/ModLiquidCooled.cs::ModLiquidCooled.GetStatusMessage(ActivePartStatus)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/AIWiring.cs::AIWiring.HandleEvent(IsConversationallyResponsiveEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    "XRL.World.Parts/TemplarPhylactery.cs::TemplarPhylactery.HandleEvent(GetShortDescriptionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_DOES_VERB_EVIDENCE,
    },
    (
        "XRL.World.Parts.Mutation/StickyTongue.cs::"
        "StickyTongue.HarpoonNearest(GameObject,int,string,int,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SlogGlands.cs::SlogGlands.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Stinger.cs::Stinger.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/LeyShifting.cs::LeyShifting.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Burgeoning.cs::Burgeoning.Burgeon()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Phasing.cs::Phasing.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SpacetimeVortex.cs::SpacetimeVortex.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Burrowing.cs::Burrowing.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Spinnerets.cs::Spinnerets.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ElectricalGeneration.cs::ElectricalGeneration.PerformDischarge(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_MUTATION_COMMAND_POPUP_FRAME_EVIDENCE,
    },
    "XRL.World.Parts/Stomach.cs::Stomach.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/ReshephsCrypt.cs::ReshephsCrypt.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/StiltWell.cs::StiltWell.GiveArtifacts(GameObject,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/RebornOnDeathInThinWorld.cs::RebornOnDeathInThinWorld.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/EngulfingDescends.cs::EngulfingDescends.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Infiltrate.cs::Infiltrate.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/AmbientPowerReceiver.cs::AmbientPowerReceiver.HandleEvent(EnteringZoneEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/RestoreOnDeath.cs::RestoreOnDeath.HandleEvent(BeforeDieEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/ModDisplacer.cs::ModDisplacer.ExamineFailure(IExamineEvent,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/MagazineAmmoLoader.cs::MagazineAmmoLoader.FireEvent(Event)": {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE719_RESIDUAL_POPUP_FRAME_RUNTIME_EVIDENCE,
    },
    "XRL.World.Parts/Brain.cs::Brain.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BRAIN_DEBUG_INTERNAL_PASSTHROUGH_EVIDENCE,
    },
    "XRL.World.Parts/Pettable.cs::Pettable.Pet(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/Robot.cs::Robot.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/IProgrammableRecoiler.cs::IProgrammableRecoiler.ProgramRecoiler(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_SPLIT_EVIDENCE,
    },
    "XRL.World.Parts/Hookah.cs::Hookah.SmokeHookah(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_SPLIT_EVIDENCE,
    },
    (
        "XRL.World.Parts.Mutation/TemporalFugue.cs::"
        "TemporalFugue.PerformTemporalFugue(GameObject,GameObject,GameObject,TemporalFugue,IEvent,bool,bool,"
        "int?,int?,int,string,string,string,string,string)"
    ): {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_EVIDENCE,
    },
    (
        "XRL.World.Parts/AutomatedExternalDefibrillator.cs::"
        "AutomatedExternalDefibrillator.AttemptDefibrillate(GameObject,IEvent)"
    ): {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs::"
        "CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe(GameObject,GameObject,IEvent)"
    ): {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE719_RESIDUAL_DOES_MESSAGE_FRAME_RUNTIME_EVIDENCE,
    },
    "XRL.UI/GritGateTerminalScreenRoot.cs::GritGateTerminalScreenRoot.UpdatePowerOptions()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_POPUP_TOP_SPLIT_EVIDENCE,
    },
    "XRL.Core/Scores.cs::Scores.Show()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SCORES_SHOW_STATIC_GAP_EVIDENCE,
    },
    (
        "XRL.World.Capabilities/ItemNaming.cs::"
        "ItemNaming.NameItem(GameObject,GameObject,GameObject,GameObject,string,string,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ITEM_NAMING_INTERACTIVE_OWNER_EVIDENCE,
    },
    "XRL.World.Parts/Crayons.cs::Crayons.HandleEvent(InventoryActionEvent)": {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_POPUP_TOP_RUNTIME_EVIDENCE,
    },
    "XRL.World.Parts/Description.cs::Description.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_LOOK_POPUP_OWNER_EVIDENCE,
    },
    "XRL.World.Parts/Inventory.cs::Inventory.HandleEvent(InventoryActionEvent)": {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE719_RESIDUAL_PURE_POPUP_TOP_RUNTIME_EVIDENCE,
    },
    "XRL.UI/TradeUI.cs::TradeUI.ShowVendorActions(GameObject,GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRADE_UI_SHOW_VENDOR_ACTIONS_OWNER_EVIDENCE,
    },
    "XRL.UI/ObjectFinder.cs::ObjectFinder.ConfigFilters()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_OBJECT_FINDER_CONFIG_FILTERS_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/VehicleUnpowered.cs::VehicleUnpowered.PreventActionMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["vehicle_unpowered"],
    },
    "XRL.World.Parts/MechanicalWings.cs::MechanicalWings.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["mechanical_wings_fire_event"],
    },
    "XRL.World.Parts/CyberneticsCathedra.cs::CyberneticsCathedra.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cathedra_long_fall"],
    },
    "XRL.World.Parts.Mutation/Wings.cs::Wings.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["mutation_wings_flight"],
    },
    "XRL.World.Parts/CyberneticsTerminal2.cs::CyberneticsTerminal2.AskLowLevelHack(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_low_level_hack"],
    },
    (
        "XRL.World.Parts/CyberneticsButcherableCybernetic.cs::"
        "CyberneticsButcherableCybernetic.AttemptButcher(GameObject,bool,bool,bool,int,Cell,List<GameObject>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_butcherable_cybernetic"],
    },
    (
        "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs::"
        "CyberneticsOnboardRecoilerTeleporter.ActuateTeleport(GameObject,IEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["cybernetics_onboard_recoiler"],
    },
    "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Tick()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["sunder_mind_tick"],
    },
    (
        "XRL.World.Parts/DanceRitualOpponent.cs::"
        "DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["dance_ritual_opponent_debug_queue"],
    },
    (
        "XRL.World.Parts/DanceRitualOpponent.cs::"
        "DanceRitualOpponent.Register(GameObject,IEventRegistrar)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["dance_ritual_opponent_debug_queue"],
    },
    "XRL.World.Parts/PlayerDanceRitual.cs::PlayerDanceRitual.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["player_dance_ritual_debug_queue"],
    },
    "XRL.World.Effects/Hooked.cs::Hooked.HandleEvent(CommandTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE35_OWNER_ROUTE_EVIDENCE["hooked_owner"],
    },
    "XRL.World.Effects/IrisdualCallow.cs::IrisdualCallow.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    (
        "XRL.World.Effects/CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.cs::"
        "CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.Apply(GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/BrainBrineCurse.cs::BrainBrineCurse.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/SphynxSalt_Tonic.cs::SphynxSalt_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SPHYNX_SALT_APPLY_EVIDENCE,
    },
    "XRL.World.Effects/Hobbled.cs::Hobbled.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Terrified.cs::Terrified.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/GeometricHeal.cs::GeometricHeal.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Trance.cs::Trance.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/StingerPoisoned.cs::StingerPoisoned.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/FuriouslyConfused.cs::FuriouslyConfused.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Confused.cs::Confused.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Poisoned.cs::Poisoned.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/PhasePoisoned.cs::PhasePoisoned.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Poisoned.cs::Poisoned.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/PhasePoisoned.cs::PhasePoisoned.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/AshPoison.cs::AshPoison.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/BasiliskPoison.cs::BasiliskPoison.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Cripple.cs::Cripple.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/PoisonGasPoison.cs::PoisonGasPoison.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Luminous.cs::Luminous.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Meditating.cs::Meditating.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Scintillating.cs::Scintillating.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Suppressed.cs::Suppressed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Asleep.cs::Asleep.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Healing.cs::Healing.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Dazed.cs::Dazed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Paralyzed.cs::Paralyzed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVE_EFFECT_POPUP_QUEUE_EVIDENCE,
    },
    "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.SetupPod(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ELEMENTAL_PSEUDOPOD_DISPLAY_NAME_EVIDENCE,
    },
    "XRL.World.Parts/Panhumor.cs::Panhumor.SetupPod(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ELEMENTAL_PSEUDOPOD_DISPLAY_NAME_EVIDENCE,
    },
    "XRL.World.Parts/ElementalJelly.cs::ElementalJelly.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PSEUDOPOD_DEATH_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Parts/Panhumor.cs::Panhumor.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PSEUDOPOD_DEATH_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.SyncFromBlueprint()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_GAS_GENERATION_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Parts/Cloneling.cs::Cloneling.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/Digging.cs::Digging.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/Engulfing.cs::Engulfing.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/FabricateFromSelf.cs::FabricateFromSelf.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/RecoilAbility.cs::RecoilAbility.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/Run.cs::Run.SyncAbility(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/RunOver.cs::RunOver.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts/TrashRifling.cs::TrashRifling.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTIVATED_ABILITY_MISC_PROVIDER_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/WillForce.cs::WillForce.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/BurrowingClaws.cs::BurrowingClaws.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ElectricalGeneration.cs::ElectricalGeneration.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/LightManipulation.cs::LightManipulation.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Precognition.cs::Precognition.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SlogGlands.cs::SlogGlands.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Beguiling.cs::Beguiling.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/AcidSlimeGlands.cs::AcidSlimeGlands.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/AdrenalControl2.cs::AdrenalControl2.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Burgeoning.cs::Burgeoning.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Burrowing.cs::Burrowing.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Carapace.cs::Carapace.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Clairvoyance.cs::Clairvoyance.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Confusion.cs::Confusion.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Decarbonizer.cs::Decarbonizer.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/DefensiveChromatophores.cs::DefensiveChromatophores.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Domination.cs::Domination.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ElectromagneticPulse.cs::ElectromagneticPulse.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ErosTeleportation.cs::ErosTeleportation.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ForceWall.cs::ForceWall.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FreezeBreath.cs::FreezeBreath.AddAbility()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FrostWebs.cs::FrostWebs.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Infiltrate.cs::Infiltrate.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/IrisdualBeam.cs::IrisdualBeam.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Kindle.cs::Kindle.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/LeyShifting.cs::LeyShifting.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/LifeDrain.cs::LifeDrain.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/LiquidSpitter.cs::LiquidSpitter.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/MassMind.cs::MassMind.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/MentalMirror.cs::MentalMirror.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Metamorphosis.cs::Metamorphosis.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Phasing.cs::Phasing.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Serenity.cs::Serenity.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SpacetimeVortex.cs::SpacetimeVortex.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SpiderWebs.cs::SpiderWebs.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Spinnerets.cs::Spinnerets.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/StickyTongue.cs::StickyTongue.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Stinger.cs::Stinger.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/StunningForce.cs::StunningForce.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/TeleportOther.cs::TeleportOther.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/TimeDilation.cs::TimeDilation.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/WaveformWorm.cs::WaveformWorm.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Cryokinesis.cs::Cryokinesis.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Disintegration.cs::Disintegration.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FearAura.cs::FearAura.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FlamingRay.cs::FlamingRay.AddAbility()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/ForceBubble.cs::ForceBubble.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/FreezingRay.cs::FreezingRay.AddAbility()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/MagneticPulse.cs::MagneticPulse.AddAbility(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Pyrokinesis.cs::Pyrokinesis.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/RepellingForce.cs::RepellingForce.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/SlimeGlands.cs::SlimeGlands.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Telepathy.cs::Telepathy.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Teleportation.cs::Teleportation.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Belcher.cs::Belcher.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/BreatherBase.cs::BreatherBase.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/GasGeneration.cs::GasGeneration.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/IDelayedLineMutation.cs::IDelayedLineMutation.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Quills.cs::Quills.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/TemporalFugue.cs::TemporalFugue.Mutate(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATION_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Tinkering_LayMine.cs::Tinkering_LayMine.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Pistol_EmptyTheClips.cs::Pistol_EmptyTheClips.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Tinkering_Tinker1.cs::Tinkering_Tinker1.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Axe_Decapitate.cs::Axe_Decapitate.AddAbility()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Axe_Dismember.cs::Axe_Dismember.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Axe_HookAndDrag.cs::Axe_HookAndDrag.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/CookingAndGathering_Harvestry.cs::CookingAndGathering_Harvestry.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/LongBladesDuelingStance.cs::LongBladesDuelingStance.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Persuasion_RebukeRobot.cs::Persuasion_RebukeRobot.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/ShortBlades_Shank.cs::ShortBlades_Shank.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Axe_Berserk.cs::Axe_Berserk.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/CookingAndGathering_Butchery.cs::CookingAndGathering_Butchery.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Cudgel_Slam.cs::Cudgel_Slam.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Cudgel_SmashUp.cs::Cudgel_SmashUp.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Discipline_Meditate.cs::Discipline_Meditate.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/LongBladesDeathblow.cs::LongBladesDeathblow.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/LongBladesLunge.cs::LongBladesLunge.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/LongBladesSwipe.cs::LongBladesSwipe.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Multiweapon_Flurry.cs::Multiweapon_Flurry.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Persuasion_Proselytize.cs::Persuasion_Proselytize.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Physic_AmputateLimb.cs::Physic_AmputateLimb.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Pistol_Akimbo.cs::Pistol_Akimbo.AddAbility()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/ShortBlades_Hobble.cs::ShortBlades_Hobble.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/ShortBlades_Rejoinder.cs::ShortBlades_Rejoinder.AddAbility()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Survival_Camp.cs::Survival_Camp.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Tinkering_DeployTurret.cs::Tinkering_DeployTurret.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Smash_Floor.cs::Smash_Floor.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Snapjaw_Howl.cs::Snapjaw_Howl.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Submersion.cs::Submersion.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Cudgel_Conk.cs::Cudgel_Conk.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/HeavyWeapons_Sweep.cs::HeavyWeapons_Sweep.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Persuasion_Berate.cs::Persuasion_Berate.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Persuasion_Intimidate.cs::Persuasion_Intimidate.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Rifle_DrawABead.cs::Rifle_DrawABead.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Shield_ShieldWall.cs::Shield_ShieldWall.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Shield_Slam.cs::Shield_Slam.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Tactics_Charge.cs::Tactics_Charge.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Tactics_DeathFromAbove.cs::Tactics_DeathFromAbove.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Tactics_Juke.cs::Tactics_Juke.AddSkill(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.World.Parts.Skill/Acrobatics_Jump.cs::Acrobatics_Jump.SyncAbility(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILL_ACTIVATED_ABILITY_NAME_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/AttributeSelectionControl.cs::AttributeSelectionControl.Updated()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_DIRECT_UI_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudSubtypeModuleWindow.cs::"
        "QudSubtypeModuleWindow.BeforeShow(EmbarkBuilderModuleWindowDescriptor)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_DIRECT_UI_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs::QudBuildSummaryModuleWindow.GetKeyMenuBar()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::QudMutationsModuleWindow.GetKeyMenuBar()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::QudBuildLibraryModuleWindow.GetSelections()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::QudBuildLibraryModuleWindow.GetKeyMenuBar()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
        "QudBuildLibraryModuleWindow.HandleMenuOption(MenuOption)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BUILD_LIBRARY_POPUP_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::QudBuildLibraryModuleWindow.AddBuild(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BUILD_LIBRARY_POPUP_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs::"
        "QudBuildLibraryModuleWindow.onSelect(FrameworkDataElement)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BUILD_LIBRARY_POPUP_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs::"
        "QudBuildSummaryModuleWindow.HandleMenuOption(MenuOption)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BUILD_SUMMARY_POPUP_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::QudCustomizeCharacterModuleWindow.GetPets()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudGamemodeModuleWindow.cs::QudGamemodeModuleWindow.GetSelections()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudGamemodeModuleWindow.cs::QudGamemodeModuleWindow.QUICKSTART": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudAttributesModuleWindow.cs::QudAttributesModuleWindow.GetKeyMenuBar()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_MENU_OPTION_OWNER_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/UrchinBelcher.cs::UrchinBelcher.UrchinBelcher()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_URCHIN_BELCHER_CTOR_EVIDENCE,
    },
    "XRL.World.Parts/CyberneticsMotorizedTreads.cs::CyberneticsMotorizedTreads.HandleEvent(ImplantedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsStasisArena.cs::"
        "CyberneticsStasisArena.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsOpticalMultiscanner.cs::"
        "CyberneticsOpticalMultiscanner.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsSingleSkillsoft.cs::"
        "CyberneticsSingleSkillsoft.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsTreeSkillsoft.cs::"
        "CyberneticsTreeSkillsoft.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsSocialCoprocessor.cs::"
        "CyberneticsSocialCoprocessor.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World.Parts/CyberneticsTechIndexer.cs::"
        "CyberneticsTechIndexer.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETICS_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    (
        "XRL.World/GetMovementCapabilitiesEvent.cs::"
        "GetMovementCapabilitiesEvent.Add(string,string,int,ActivatedAbilityEntry,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MISC_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    "XRL.World.Parts/Biocapacitor.cs::Biocapacitor.Biocapacitor()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MISC_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    "XRL.World.Parts/FoliageCamouflage.cs::FoliageCamouflage.FoliageCamouflage()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MISC_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    "XRL.World.Parts/UrbanCamouflage.cs::UrbanCamouflage.UrbanCamouflage()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MISC_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    "XRL.World.Parts/MechanimistLibrarian.cs::MechanimistLibrarian.Initialize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MISC_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Wings.cs::Wings.OnRegenerateDefaultEquipment(Body)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_WINGS_DEFAULT_EQUIPMENT_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Parts/Banner.cs::Banner.HandleEvent(GetShortDescriptionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BANNER_DESCRIPTION_ASSIGNMENT_EVIDENCE,
    },
    "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.CreateHologramOf(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DECOY_HOLOGRAM_DESCRIPTION_EVIDENCE,
    },
    ("XRL.World/GameObject.cs::GameObject.DescribeActivatedAbility(Guid,Action<Templates.StatCollector>)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_GAME_OBJECT_ACTIVATED_ABILITY_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Parts/FabricateFromSelf.cs::FabricateFromSelf.AbilityDescription": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FABRICATE_ABILITY_DESCRIPTION_EVIDENCE,
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultCriticalFailure()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HAGGLING_SIFRAH_RESULT_DESCRIPTION_EVIDENCE,
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultFailure()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HAGGLING_SIFRAH_RESULT_DESCRIPTION_EVIDENCE,
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultPartialSuccess()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HAGGLING_SIFRAH_RESULT_DESCRIPTION_EVIDENCE,
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultSuccess()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HAGGLING_SIFRAH_RESULT_DESCRIPTION_EVIDENCE,
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.ResultExceptionalSuccess()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HAGGLING_SIFRAH_RESULT_DESCRIPTION_EVIDENCE,
    },
    ("XRL.World/TinkeringSifrahTokenLiquid.cs::TinkeringSifrahTokenLiquid.TinkeringSifrahTokenLiquid(string)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SIFRAH_TOKEN_DESCRIPTION_EVIDENCE,
    },
    (
        "XRL.World/RitualSifrahTokenAttributeSacrifice.cs::"
        "RitualSifrahTokenAttributeSacrifice.RitualSifrahTokenAttributeSacrifice(string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SIFRAH_TOKEN_DESCRIPTION_EVIDENCE,
    },
    (
        "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs::"
        "RitualSifrahTokenInvokeHigherBeing.SetBeing(Worshippable,List<Worshippable>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SIFRAH_TOKEN_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/AppleMatz.cs::AppleMatz.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/BoneBabka.cs::BoneBabka.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/CloacaSurprise.cs::CloacaSurprise.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/CrystalDelight.cs::CrystalDelight.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/GoatAndSweetLeaf.cs::GoatAndSweetLeaf.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/HotandSpiny.cs::HotandSpiny.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/MahLahSoup.cs::MahLahSoup.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/MushroomCider.cs::MushroomCider.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/ThePorridge.cs::ThePorridge.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Skills.Cooking/TongueAndCheek.cs::TongueAndCheek.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRESET_COOKING_RECIPE_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.AI.GoalHandlers/Kill.cs::Kill.GetDetails()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTION_EFFECT_DESCRIPTION_RETURN_EVIDENCE,
    },
    "XRL.World.Tinkering/Disassembly.cs::Disassembly.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTION_EFFECT_DESCRIPTION_RETURN_EVIDENCE,
    },
    "XRL/OngoingAction.cs::OngoingAction.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTION_EFFECT_DESCRIPTION_RETURN_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/Metamorphed.cs::Metamorphed.GetDetails()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTION_EFFECT_DESCRIPTION_RETURN_EVIDENCE,
    },
    "XRL.World.Parts/IStingerProperties.cs::IStingerProperties.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ACTION_EFFECT_DESCRIPTION_RETURN_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud/QudCyberneticsModule.cs::CyberneticsChoice.GetDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud/QudCyberneticsModule.cs::CyberneticsChoice.GetLongDescription()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Tinkering/TinkerData.cs::TinkerData.UnclippedDescription": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Tinkering/TinkerData.cs::TinkerData.Description": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectCyberneticsUnit.cs::GameObjectCyberneticsUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectSkillUnit.cs::GameObjectSkillUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectRelicUnit.cs::GameObjectRelicUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    ("XRL.World.Units/GameObjectGolemQuestRandomUnit.cs::GameObjectGolemQuestRandomUnit.GetDescription(bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectMetachromeUnit.cs::GameObjectMetachromeUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectBodyPartUnit.cs::GameObjectBodyPartUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectExperienceUnit.cs::GameObjectExperienceUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectMutationUnit.cs::GameObjectMutationUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectBaetylUnit.cs::GameObjectBaetylUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectCloneUnit.cs::GameObjectCloneUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectReputationUnit.cs::GameObjectReputationUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectSecretUnit.cs::GameObjectSecretUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectUnit.cs::GameObjectUnit.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Units/GameObjectUnitAggregate.cs::GameObjectUnitAggregate.GetDescription(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_DETAIL_RETURN_EVIDENCE,
    },
    "XRL.World.Effects/Prone.cs::Prone.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRONE_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Effects/Prone.cs::Prone.StandUp(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRONE_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Effects/Stun.cs::Stun.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STUN_FIXED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Stun.cs::Stun.HandleEvent(IsConversationallyResponsiveEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STUN_FIXED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Stun.cs::Stun.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/HolographicBleeding.cs::HolographicBleeding.StartMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HOLOGRAPHIC_BLEEDING_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Effects/HolographicBleeding.cs::HolographicBleeding.StopMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HOLOGRAPHIC_BLEEDING_MESSAGE_FRAME_EVIDENCE,
    },
    "XRL.World.Effects/Asleep.cs::Asleep.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ASLEEP_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Asleep.cs::Asleep.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ASLEEP_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Asleep.cs::Asleep.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ASLEEP_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Rusted.cs::Rusted.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Sitting.cs::Sitting.StandUp(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Frenzied.cs::Frenzied.TriggerBerserk()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/SporeCloudPoison.cs::SporeCloudPoison.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_MESSAGE_FRAME_ROUTE_EVIDENCE,
    },
    "XRL.World.Effects/Running.cs::Running.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE38_RUNNING_REMOVE_EVIDENCE,
    },
    "XRL.World.Effects/ResummonGloaming.cs::ResummonGloaming.HandleEvent(EnteredCellEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE38_RESUMMON_GLOAMING_EVIDENCE,
    },
    (
        "XRL.World.Effects/CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.cs::"
        "CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.Apply(GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE38_ARTIFACT_IDENTIFY_EVIDENCE,
    },
    "XRL.World.Effects/LifeDrain.cs::LifeDrain.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE39_LIFE_DRAIN_APPLY_EVIDENCE,
    },
    "XRL.World.Effects/LifeDrain.cs::LifeDrain.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE39_LIFE_DRAIN_INVENTORY_EVIDENCE,
    },
    "XRL.World.Effects/Bleeding.cs::Bleeding.StartMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE39_BLEEDING_START_EVIDENCE,
    },
    "XRL.World.Effects/Beguiled.cs::Beguiled.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE40_BEGUILED_REMOVE_EVIDENCE,
    },
    "XRL.World.Effects/Confused.cs::Confused.HandleEvent(IsConversationallyResponsiveEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE40_CONFUSED_CONVERSATION_EVIDENCE,
    },
    "XRL.World.Effects/Dominating.cs::Dominating.HandleEvent(IsConversationallyResponsiveEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE40_DOMINATING_CONVERSATION_EVIDENCE,
    },
    "XRL.World.Effects/Immobilized.cs::Immobilized.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE41_ACTIVE_EFFECT_DIDX_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/Stuck.cs::Stuck.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE41_ACTIVE_EFFECT_DIDX_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE41_ACTIVE_EFFECT_DIDX_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/Lovesick.cs::Lovesick.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE42_SOCIAL_ACTIVE_EFFECT_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/Beguiled.cs::Beguiled.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE42_SOCIAL_ACTIVE_EFFECT_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/Proselytized.cs::Proselytized.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE42_SOCIAL_ACTIVE_EFFECT_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/Rebuked.cs::Rebuked.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE42_SOCIAL_ACTIVE_EFFECT_OWNER_EVIDENCE,
    },
    "XRL.World.Effects/CardiacArrest.cs::CardiacArrest.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRANCHE43_CARDIAC_ARREST_REMOVE_EVIDENCE,
    },
    "XRL.World.Effects/Asleep.cs::Asleep.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/EmptyTheClips.cs::EmptyTheClips.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Ill.cs::Ill.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/ShatteredArmor.cs::ShatteredArmor.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/LifeDrain.cs::LifeDrain.HandleEvent(EndTurnEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Proselytized.cs::Proselytized.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Rebuked.cs::Rebuked.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Running.cs::Running.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/StunGasStun.cs::StunGasStun.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/ShieldWall.cs::ShieldWall.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_GENERATED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/BrainBrineCurse.cs::BrainBrineCurse.GainChoice(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BRAIN_BRINE_GAIN_CHOICE_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_Hitpoints.cs::BasicCookingEffect_Hitpoints.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_MA.cs::BasicCookingEffect_MA.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_MS.cs::BasicCookingEffect_MS.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_Quickness.cs::BasicCookingEffect_Quickness.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_ToHit.cs::BasicCookingEffect_ToHit.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_XP.cs::BasicCookingEffect_XP.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    ("XRL.World.Effects/BasicCookingEffect_Regeneration.cs::BasicCookingEffect_Regeneration.ApplyEffect(GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    "XRL.World.Effects/BasicCookingEffect_RandomStat.cs::BasicCookingEffect_RandomStat.ApplyEffect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASIC_COOKING_EFFECT_POPUP_EVIDENCE,
    },
    (
        "XRL.World.Effects/CookingDomainReflect_UnitReflectDamage.cs::"
        "CookingDomainReflect_UnitReflectDamage.FireEvent(Event)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE,
    },
    (
        "XRL.World.Effects/CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.cs::"
        "CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.FireEvent(Event)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/CookingDomainTeleport_UnitBlink.cs::CookingDomainTeleport_UnitBlink.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/CookingDomainRubber_Extra2Jumps.cs::CookingDomainRubber_Extra2Jumps.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/CookingDomainRubber_ExtraJump.cs::CookingDomainRubber_ExtraJump.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE,
    },
    (
        "XRL.World.Effects/NoPhase_ProceduralCookingTriggeredAction_Effect.cs::"
        "NoPhase_ProceduralCookingTriggeredAction_Effect.FireEvent(Event)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COOKING_RUNTIME_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/IronshankOnset.cs::IronshankOnset.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_IRONSHANK_ONSET_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Engulfed.cs::Engulfed.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_MOBILITY_BLOCK_EVIDENCE,
    },
    "XRL.World.Effects/Immobilized.cs::Immobilized.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_MOBILITY_BLOCK_EVIDENCE,
    },
    "XRL.World.Effects/Stuck.cs::Stuck.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_MOBILITY_BLOCK_EVIDENCE,
    },
    "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.FailedToContest(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REALITY_STABILIZED_EVENT_EVIDENCE,
    },
    ("XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShortCircuitDevice(GameObject,GameObject,Event)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REALITY_STABILIZED_EVENT_EVIDENCE,
    },
    "XRL.World.Effects/RealityStabilized.cs::RealityStabilized.TryContest(GameObject,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REALITY_STABILIZED_EVENT_EVIDENCE,
    },
    ("XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShowGenericInterdictMessage(GameObject,Event)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REALITY_STABILIZED_INTERDICT_EVIDENCE,
    },
    ("XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShowDistantInterdictMessage(GameObject,Event)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REALITY_STABILIZED_INTERDICT_EVIDENCE,
    },
    ("XRL.World.Effects/RealityStabilized.cs::RealityStabilized.ShowDualInterdictMessage(GameObject,Event)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_REALITY_STABILIZED_INTERDICT_EVIDENCE,
    },
    "XRL.World.Effects/GlotrotOnset.cs::GlotrotOnset.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_GLOTROT_ONSET_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/MonochromeOnset.cs::MonochromeOnset.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MONOCHROME_ONSET_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Phased.cs::Phased.HandleEvent(EffectAppliedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PHASED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Phased.cs::Phased.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PHASED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Phased.cs::Phased.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PHASED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/LatchedOnto.cs::LatchedOnto.Expired()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_LATCHED_ONTO_EXPIRED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Nosebleed.cs::Nosebleed.StartMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_NOSEBLEED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Nosebleed.cs::Nosebleed.StopMessage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_NOSEBLEED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/FungalCureQueasy.cs::FungalCureQueasy.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Effects/IrisdualCallow.cs::IrisdualCallow.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Effects/Luminous.cs::Luminous.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_EMIT_MESSAGE_PATTERN_EVIDENCE,
    },
    "XRL.World.Effects/Cripple.cs::Cripple.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CRIPPLE_APPLY_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Budding.cs::Budding.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BUDDING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Budding.cs::Budding.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BUDDING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/AxonsInflated.cs::AxonsInflated.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/AxonsDeflated.cs::AxonsDeflated.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Cudgel_SmashingUp.cs::Cudgel_SmashingUp.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Berserk.cs::Berserk.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Exhausted.cs::Exhausted.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Flagging.cs::Flagging.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/NocturnalApexed.cs::NocturnalApexed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    "XRL.World.Effects/Paralyzed.cs::Paralyzed.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EFFECT_STATIC_QUEUE_EVIDENCE,
    },
    ("XRL.World.Effects/CyberneticRejectionSyndrome.cs::CyberneticRejectionSyndrome.Apply(GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETIC_REJECTION_MESSAGE_EVIDENCE,
    },
    ("XRL.World.Effects/CyberneticRejectionSyndrome.cs::CyberneticRejectionSyndrome.Remove(GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETIC_REJECTION_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/CyberneticRejectionSyndrome.cs::CyberneticRejectionSyndrome.Reduce(int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CYBERNETIC_REJECTION_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Emboldened.cs::Emboldened.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EMBOLDENED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Emboldened.cs::Emboldened.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EMBOLDENED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Healing.cs::Healing.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HEALING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Healing.cs::Healing.HandleEvent(UseEnergyEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_HEALING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Stasis.cs::Stasis.HandleEvent(BeforeApplyDamageEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STASIS_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Stressed.cs::Stressed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STRESSED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Stressed.cs::Stressed.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STRESSED_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BLAZE_TONIC_REMOVE_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/BoostStatistic.cs::BoostStatistic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BOOST_STATISTIC_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/BoostStatistic.cs::BoostStatistic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BOOST_STATISTIC_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/FungalSporeInfection.cs::FungalSporeInfection.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FUNGAL_SPORE_INFECTION_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Mutating.cs::Mutating.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Mutating.cs::Mutating.HandleEvent(EndTurnEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MUTATING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/BlinkingTicSickness.cs::BlinkingTicSickness.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BLINKING_TIC_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Meditating.cs::Meditating.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MEDITATING_MESSAGE_EVIDENCE,
    },
    "XRL.World.Effects/Ill.cs::Ill.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ILL_REMOVE_MESSAGE_EVIDENCE,
    },
    "XRL.Core/ActionManager.cs::ActionManager.RunSegment()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #719 residual review covers the main-turn popup/log owner "
                "route through the existing ActionManager.RunSegment patch; "
                "specific GameObject popup producers remain separate rows."
            ),
            "Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActionManagerRunSegmentTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts/SpindleNegotiation.cs::SpindleNegotiation.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review connects this popup owner route to the existing Issue #762 evidence.",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SpindleNegotiationTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.UI/Look.cs::Look.ShowLooker(int,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #719 residual review closes only the single-callsite "
                "Look.ShowLooker popup owner; broad Look UI routes remain separate."
            ),
            "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.UI/Look.cs::Look.SetupItemTooltipAsync(XRL.World.GameObject,TooltipTrigger)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_LOOK_TOOLTIP_CONTENT_EVIDENCE,
    },
    ("XRL.UI/Look.cs::Look.ShowItemTooltipAsync(Vector3,XRL.World.GameObject,bool,UnityEngine.GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_LOOK_TOOLTIP_CONTENT_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/LightManipulation.cs::LightManipulation.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #719 residual review covers LightManipulation queued "
                "message and popup shapes through existing owner-route tests."
            ),
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            "LightManipulation popup and queued message route evidence from Issue #762.",
        ],
    },
    (
        "XRL.UI/TinkeringScreen.cs::TinkeringScreen.PerformUITinkerMod("
        "GameObject,GameObject,TinkerData,BitCost,IEvent,ref bool,List<GameObject>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            (
                "Issue #719 residual review covers the Tinker Mod popup/Does/"
                "prompt route through existing TinkeringScreen owner tests."
            ),
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringModPopupTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/AbilityBar.cs::AbilityBar.Update()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers ability-bar direct text through the existing button text patch.",
            "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/AbilityBar.cs::AbilityBar.UpdateAbilitiesText()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers ability-bar direct text through the existing button text patch.",
            "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/AbilityBarButton.cs::AbilityBarButton.UpdateText()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers ability button text through the existing button text patch.",
            "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.UpdateViewFromData()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers CharacterStatusScreen direct text through existing owner patches.",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.HandleHighlightMutation(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers CharacterStatusScreen mutation highlight text.",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.HandleHighlightAttribute(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers CharacterStatusScreen attribute highlight text.",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.HandleHighlightEffect(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers CharacterStatusScreen effect highlight text.",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/CharacterAttributeLine.cs::CharacterAttributeLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["character_attribute_line"],
    },
    "Qud.UI/CharacterEffectLine.cs::CharacterEffectLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["character_effect_line"],
    },
    "Qud.UI/ModMenuLine.cs::ModMenuLine.Update()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["mod_menu_line"],
    },
    "Qud.UI/EquipmentLine.cs::EquipmentLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["equipment_line"],
    },
    "Qud.UI/HelpRow.cs::HelpRow.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["help_row"],
    },
    "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["ability_manager_line"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.UpdateViewFromData()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["inventory_status"],
    },
    "Qud.UI/InventoryLine.cs::InventoryLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["inventory_line"],
    },
    "Qud.UI/TradeLine.cs::TradeLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["trade_line"],
    },
    "Qud.UI/TinkeringStatusScreen.cs::TinkeringStatusScreen.UpdateViewFromData()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["tinkering_status"],
    },
    (
        "Qud.UI/PopupMessage.cs::PopupMessage.ShowPopup("
        "string,List<QudMenuItem>,Action<QudMenuItem>,List<QudMenuItem>,Action<QudMenuItem>,"
        "string,bool,string,int,Action,IRenderable,string,IRenderable,bool,bool,CancellationToken,"
        "bool,string,string,Location2D,string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["popup_message"],
    },
    (
        "XRL.UI/Popup.cs::Popup.GetPopupOption("
        "int,IReadOnlyList<string>,IReadOnlyList<char>,IReadOnlyList<IRenderable>)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["popup_get_option"],
    },
    (
        "XRL.UI/Popup.cs::Popup.PickSeveral("
        "string,string,string,string,IReadOnlyList<string>,IReadOnlyList<char>,IReadOnlyList<int>,"
        "IReadOnlyList<IRenderable>,XRL.World.GameObject,IRenderable,Action<int>,int,int,int,int,int,bool,bool,bool,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["popup_pick_several"],
    },
    "Qud.UI/TinkeringLine.cs::TinkeringLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["tinkering_line"],
    },
    "Qud.UI/FactionsLine.cs::FactionsLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["factions_line"],
    },
    "Qud.UI/SelectableTextMenuItem.cs::SelectableTextMenuItem.SelectChanged(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["selectable_menu_item"],
    },
    "Qud.UI/TinkeringBitsLine.cs::TinkeringBitsLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["tinkering_bits"],
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.QueryKeybinds()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["keybinds_screen"],
    },
    "Qud.UI/ModManagerUI.cs::ModManagerUI.OnSelect(ModInfo)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["mod_manager"],
    },
    "Qud.UI/AchievementViewRow.cs::AchievementViewRow.SetAchievementData(AchievementInfoData)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["achievement_view_row"],
    },
    "Qud.UI/AchievementViewRow.cs::AchievementViewRow.SetHiddenData(HiddenAchievementData)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_SCREEN_OWNER_EVIDENCE["achievement_view_row"],
    },
    "Qud.UI/JournalLine.cs::JournalLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers JournalLine setData text through the existing owner patch.",
            "Mods/QudJP/Assemblies/src/Patches/JournalLineTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/JournalLineTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/KeybindRow.cs::KeybindRow.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers KeybindRow setData text through the existing owner patch.",
            "Mods/QudJP/Assemblies/src/Patches/KeybindRowTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/KeybindRowTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": [
            "Issue #719 residual review covers PickGameObjectLine setData text through the existing owner patch.",
            "Mods/QudJP/Assemblies/src/Patches/PickGameObjectLineTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickGameObjectLineTranslationPatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        ],
    },
    "XRL.World.Parts/Description.cs::Description.GetShortDescription(bool,bool,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_DESCRIPTION_SHORT_DESCRIPTION_EVIDENCE,
    },
    "XRL.World.Tinkering/Disassembly.cs::Disassembly.Continue()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["disassembly"],
    },
    "XRL.World.Tinkering/Disassembly.cs::Disassembly.End()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["disassembly"],
    },
    "XRL.UI/TinkeringScreen.cs::TinkeringScreen.PerformUITinkerBuild(GameObject,TinkerData,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["tinkering_build"],
    },
    "XRL.World/ZoneManager.cs::ZoneManager.GenerateZone(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["zone_generate"],
    },
    "XRL.UI/KeyMappingUI.cs::KeyMappingUI.Show()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["key_mapping"],
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.HandleMenuOption(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["keybinds_handle_menu_option"],
    },
    "XRL.UI/TradeUI.cs::TradeUI.PerformOffer(int,bool,GameObject,TradeScreenMode,List<TradeEntry>[],int[][])": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["trade_offer"],
    },
    "XRL.World.Parts/SpiralBorerCurio.cs::SpiralBorerCurio.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["spiral_borer_curio"],
    },
    "XRL.World.Parts.Mutation/Telekinesis.cs::Telekinesis.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["telekinesis"],
    },
    "XRL.World.Parts.Mutation/Telekinesis.cs::Telekinesis.Activate(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["telekinesis"],
    },
    "XRL.World.Parts.Mutation/Telekinesis.cs::Telekinesis.AttemptTelekinesis()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["telekinesis"],
    },
    ("XRL.World.Parts/DestroyOnUnequip.cs::DestroyOnUnequip.HandleEvent(BeginBeingUnequippedEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["destroy_on_unequip_confirmation"],
    },
    "XRL.World.Parts.Mutation/BurrowingClaws.cs::BurrowingClaws.CheckDig()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["burrowing_claws_check_dig"],
    },
    "Qud.UI/MainMenu.cs::MainMenu.Quit()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["main_menu_quit"],
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.Exit()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["keybinds_exit_save_prompt"],
    },
    "XRL.World.Parts/Teleprojector.cs::Teleprojector.EndDomination(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["teleprojector_end_domination"],
    },
    "TutorialStep.cs::TutorialStep.ConstrainToCurrentZone(Cell)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tutorial_step_current_zone"],
    },
    "XRL.World.Parts/GolemQuestMound.cs::GolemQuestMound.CheckCompletion()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["golem_quest_mound_ready"],
    },
    ("XRL.World.Quests/PaxKlanqIPresumeSystem.cs::PaxKlanqIPresumeSystem.UnderConstructionMessage()"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["pax_klanq_under_construction"],
    },
    "XRL.World.ZoneParts/AmbientStabilization.cs::AmbientStabilization.Stabilize()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["ambient_stabilization"],
    },
    "XRL.World/RealityDistortionSifrah.cs::RealityDistortionSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["reality_distortion_check_early_exit"],
    },
    "XRL/SifrahGame.cs::SifrahGame.CheckIncompleteTurn(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_game_completion_prompts"],
    },
    "XRL/SifrahGame.cs::SifrahGame.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_game_completion_prompts"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["baetyl_offering_fixed_popup_results"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["baetyl_offering_fixed_popup_results"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["baetyl_offering_fixed_popup_results"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["baetyl_offering_fixed_popup_results"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["baetyl_offering_fixed_popup_results"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.ResultExceptionalSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["baetyl_offering_fixed_popup_results"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.ResultSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.ResultExceptionalSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ResultSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ResultExceptionalSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/RealityDistortionSifrah.cs::RealityDistortionSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/RebukingSifrah.cs::RebukingSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/RebukingSifrah.cs::RebukingSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    "XRL.World/ReverseEngineeringSifrah.cs::ReverseEngineeringSifrah.CheckOutOfOptions(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/RitualSifrahTokenScourging.cs::"
        "RitualSifrahTokenScourging.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/SocialSifrahTokenDisplayABarathrumiteToken.cs::"
        "SocialSifrahTokenDisplayABarathrumiteToken.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/SocialSifrahTokenDisplayAFarmersToken.cs::"
        "SocialSifrahTokenDisplayAFarmersToken.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/SocialSifrahTokenDisplayAMerchantsToken.cs::"
        "SocialSifrahTokenDisplayAMerchantsToken.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/SocialSifrahTokenDisplayAMinstrelsToken.cs::"
        "SocialSifrahTokenDisplayAMinstrelsToken.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/SocialSifrahTokenReadFromTheCanticlesChromaic.cs::"
        "SocialSifrahTokenReadFromTheCanticlesChromaic.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_fixed_popup_leafs"],
    },
    (
        "XRL.World/TinkeringSifrahTokenToolkit.cs::"
        "TinkeringSifrahTokenToolkit.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_token_fixed_use_failures"],
    },
    (
        "XRL.World/TinkeringSifrahTokenAdvancedToolkit.cs::"
        "TinkeringSifrahTokenAdvancedToolkit.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_token_fixed_use_failures"],
    },
    (
        "XRL.World/TinkeringSifrahTokenCopperWire.cs::"
        "TinkeringSifrahTokenCopperWire.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_token_fixed_use_failures"],
    },
    "XRL.World/SocialSifrahTokenHookah.cs::SocialSifrahTokenHookah.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sifrah_token_fixed_use_failures"],
    },
    "XRL.World/GameObject.cs::GameObject.CheckFrozen(bool,bool,bool,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["game_object_check_frozen"],
    },
    "Qud.UI/MouseBlocker.cs::MouseBlocker.OnPointerClick(PointerEventData)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["mouse_blocker_enable_mouse"],
    },
    "XRL.World.ZoneParts/ScriptCallToArms.cs::ScriptCallToArms.spawnParties(int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["script_call_to_arms_spawn_parties"],
    },
    (
        "XRL.CharacterBuilds.Qud/QudSpecificBootHandlersModule.cs::"
        "QudSpecificBootHandlersModule.handleBootEvent(string,XRLGame,EmbarkInfo,object)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["qud_specific_boot_handlers_game_start"],
    },
    "XRL.World.Quests/AscensionSystem.cs::AscensionSystem.HandleEvent(EndTurnEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["ascension_system_wait_prompts"],
    },
    "XRL.World.Quests/AscensionSystem.cs::AscensionSystem.HandleEvent(AfterConversationEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["ascension_system_wait_prompts"],
    },
    "XRL.World.Quests/AscensionSystem.cs::AscensionSystem.HandleEvent(GenericQueryEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["ascension_system_wait_prompts"],
    },
    "XRL.World.Quests.GolemQuest/GolemQuestSelection.cs::GolemQuestSelection.WishFinishGolem()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["golem_quest_selection_wish_finish"],
    },
    "XRL.World.Parts/Cloneling.cs::Cloneling.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["cloneling_fire_event_refresh"],
    },
    "Qud.UI/MainMenu.cs::MainMenu.HandleDelete()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_POPUP_MESSAGE_DELETE_SAVE_EVIDENCE,
    },
    "Qud.UI/SaveManagement.cs::SaveManagement.HandleDelete()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_POPUP_MESSAGE_DELETE_SAVE_EVIDENCE,
    },
    "Qud.UI/ModManagerUI.cs::ModManagerUI.PromptScripting()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_POPUP_MESSAGE_FIXED_FIELD_EVIDENCE,
    },
    "XRL.World.Effects/Exhausted.cs::Exhausted.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["exhausted_fixed_popups"],
    },
    "XRL.World.Effects/Exhausted.cs::Exhausted.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["exhausted_fixed_popups"],
    },
    "XRL.World.Effects/Lost.cs::Lost.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["lost_remove"],
    },
    "XRL.World.Effects/Glotrot.cs::Glotrot.AskPulldown()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["glotrot_ask_pulldown"],
    },
    "XRL.World.Parts/ArkCore.cs::ArkCore.StartEnd(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["ark_core_start_end"],
    },
    "Qud.UI/MainMenu.cs::MainMenu.SelectedInfo(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["main_menu_redeem_code"],
    },
    "XRL.CharacterBuilds.Qud/QudChartypeModule.cs::QudChartypeModule.selectType(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["qud_chargen_last_character"],
    },
    "XRL.World.Parts.Mutation/SunderMind.cs::SunderMind.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sunder_mind_targeting"],
    },
    "XRL.World.Parts/Switch.cs::Switch.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["switch_fire_event"],
    },
    "XRL.World.Parts/StairsUp.cs::StairsUp.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["stairs_up_key_standin"],
    },
    "XRL.World.Parts/CherubimLock.cs::CherubimLock.Chime()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["cherubim_lock_chime"],
    },
    "XRL.World.Parts/TeleportOnEat.cs::TeleportOnEat.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["teleport_on_eat"],
    },
    "XRL.World/DynamicQuestsGameState.cs::DynamicQuestsGameState.FindQuestTarget(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["dynamic_quest_find_target"],
    },
    "XRL.World.Parts/ModDisguise.cs::ModDisguise.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["mod_disguise_already_wearing"],
    },
    "XRL.World.Parts.Mutation/PsionicMigraines.cs::PsionicMigraines.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["psionic_migraines_equip"],
    },
    "XRL.World.Parts.Mutation/FrostWebs.cs::FrostWebs.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["frost_webs_out_of_range"],
    },
    "XRL.World/Cell.cs::Cell.LogInvalidPhysics(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["cell_invalid_physics_duplication"],
    },
    "XRL.World.Parts/GasDisease.cs::GasDisease.ApplyDisease(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["gas_disease_apply_sick"],
    },
    "XRL.World.Parts.Mutation/Skittish.cs::Skittish.LoseControl()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["skittish_startled"],
    },
    "XRL.World.Parts/TimeCube.cs::TimeCube.Activate(GameObject,bool,IExamineEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["time_cube_fraudulent"],
    },
    "XRL.World.Parts/TerrainTravelFungal.cs::TerrainTravelFungal.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["terrain_travel_fungal_lost"],
    },
    "XRL/XRLGame.cs::XRLGame.SaveGameError(string,Exception,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["xrl_game_save_error"],
    },
    "XRL.World.Parts/CyclopeanPrism.cs::CyclopeanPrism.PtohAnnoyed(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["cyclopean_prism_ptoh_annoyed"],
    },
    "XRL.World.Parts.Mutation/Domination.cs::Domination.BreakDomination()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["domination_break"],
    },
    "XRL.World.Effects/TimeCubed.cs::TimeCubed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["time_cubed_apply"],
    },
    "XRL.World.Parts.Mutation/StickyTongue.cs::StickyTongue.HandleEvent(CommandEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["sticky_tongue_missing_tongue"],
    },
    "XRL.World.Parts/CyberneticsCustomVisage.cs::CyberneticsCustomVisage.ApplyVisage(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["cybernetics_custom_visage_pick_faction"],
    },
    "XRL.World.Parts/SummoningCurio.cs::SummoningCurio.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["summoning_curio_inventory_action"],
    },
    "XRL.World.Parts.Mutation/CrungleGaze.cs::CrungleGaze.FireLine(List<Cell>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["crungle_gaze_drowsy"],
    },
    "XRL.World.Parts.Mutation/Psychometry.cs::Psychometry.HandleEvent(GetTinkeringBonusEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["psychometry_bonus_unusable"],
    },
    "XRL.World.Parts/Skills.cs::Skills.WishSkill(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["skills_wish_skill_missing"],
    },
    "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.HandleEvent(BeginTakeActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Blaze_Tonic.cs::Blaze_Tonic.ApplyOverdose(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Hoarshroom_Tonic.cs::Hoarshroom_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Hoarshroom_Tonic.cs::Hoarshroom_Tonic.ApplyOverdose(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Hoarshroom_Tonic.cs::Hoarshroom_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.ApplyAllergy(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/HulkHoney_Tonic.cs::HulkHoney_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/LoveTonic.cs::LoveTonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/LoveTonic.cs::LoveTonic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/LoveTonic.cs::LoveTonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.ApplyAllergy(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Rubbergum_Tonic.cs::Rubbergum_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Salve_Tonic.cs::Salve_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Salve_Tonic.cs::Salve_Tonic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Salve_Tonic.cs::Salve_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Skulk_Tonic.cs::Skulk_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Skulk_Tonic.cs::Skulk_Tonic.ApplyOverdose(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Skulk_Tonic.cs::Skulk_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/SphynxSalt_Tonic.cs::SphynxSalt_Tonic.ApplyOverdose(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/SphynxSalt_Tonic.cs::SphynxSalt_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/ShadeOil_Tonic.cs::ShadeOil_Tonic.ApplyOverdose(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Ubernostrum_Tonic.cs::Ubernostrum_Tonic.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Ubernostrum_Tonic.cs::Ubernostrum_Tonic.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/Ubernostrum_Tonic.cs::Ubernostrum_Tonic.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tonic_fixed_popup_effects"],
    },
    "XRL.World.Effects/AmbientRealityStabilized.cs::AmbientRealityStabilized.HandleEvent(EndTurnEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["ambient_reality_stabilized_diffuses"],
    },
    "XRL.World.Conversations.Parts/PaxInfectLimb.cs::PaxInfectLimb.Infect(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["pax_infect_limb_rejects"],
    },
    (
        "XRL.World.Conversations.Parts/WaterRitualLearnSkill.cs::WaterRitualLearnSkill.HandleEvent(EnteredElementEvent)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["water_ritual_learn_skill_points"],
    },
    "XRL.World.Tinkering/TinkerData.cs::TinkerData.DataDisk()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_FIXED_LITERAL_POPUP_EVIDENCE["tinker_data_data_disk_pick_blueprint"],
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
        "QudCustomizeCharacterModuleWindow.GetSelections()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_CUSTOMIZE_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
        "QudCustomizeCharacterModuleWindow.SelectMenuOption(FrameworkDataElement)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_CUSTOMIZE_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
        "QudCustomizeCharacterModuleWindow.OnChooseGenderAsync()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_CUSTOMIZE_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
        "QudCustomizeCharacterModuleWindow.OnChoosePronounSetAsync()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_CUSTOMIZE_EVIDENCE,
    },
    (
        "XRL.CharacterBuilds.Qud.UI/QudCustomizeCharacterModuleWindow.cs::"
        "QudCustomizeCharacterModuleWindow.OnChoosePet()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARGEN_CUSTOMIZE_EVIDENCE,
    },
    "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::QudMutationsModuleWindow.SelectVariant()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_QUD_MUTATION_VARIANT_POPUP_EVIDENCE,
    },
    "XRL.World.Parts.Mutation/BaseMutation.cs::BaseMutation.SelectVariant(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_BASE_MUTATION_VARIANT_POPUP_EVIDENCE,
    },
    "XRL.World/Gender.cs::Gender.CustomizeProcess(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_GENDER_CUSTOMIZE_POPUP_EVIDENCE,
    },
    "XRL.CharacterBuilds/EmbarkBuilder.cs::EmbarkBuilder.checkStateAsync()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_EMBARK_BUILDER_VALIDATION_POPUP_EVIDENCE,
    },
    "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.HandleCmdOptions()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_AND_KEYBIND_OPTION_POPUP_EVIDENCE,
    },
    (
        "Qud.UI/InventoryAndEquipmentStatusScreen.cs::"
        "InventoryAndEquipmentStatusScreen.HandleShowOptions()"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_AND_KEYBIND_OPTION_POPUP_EVIDENCE,
    },
    "XRL.UI/CommandBindingManager.cs::CommandBindingManager.RestoreDefaults()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_AND_KEYBIND_OPTION_POPUP_EVIDENCE,
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.HandleTradeSome(TradeLine)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["trade_screen_ask_number"],
    },
    ("XRL.World.Parts/ActivatedAbilityEntry.cs::ActivatedAbilityEntry.TrySendCommandEventOnPlayer()"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["activated_ability_entry_world_map"],
    },
    "XRL.World.Parts/Fetches.cs::Fetches.HandleEvent(AIBoredEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["fetches_runs_off"],
    },
    "XRL/ModInfo.cs::ModInfo.ConfirmDependencies()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["mod_info_dependencies"],
    },
    "XRL/ModInfo.cs::ModInfo.ConfirmUpdate()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["mod_info_update"],
    },
    "Qud.UI/ModScrollerOne.cs::ModScrollerOne.OnActivate(ModInfo)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["mod_scroller_one"],
    },
    "XRL.UI/SkillsAndPowersScreen.cs::SkillsAndPowersScreen.SelectNode(SPNode,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["skills_and_powers_select_node"],
    },
    "XRL.UI/StatusScreen.cs::StatusScreen.ShowMutationPopup(GameObject,BaseMutation)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["status_screen_mutation_popup"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.NostrumsTreatDiseaseOnset()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["campfire_nostrums"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.NostrumsTreatPoison()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["campfire_nostrums"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.NostrumsTreatIllness()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["campfire_nostrums"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.NostrumsStopBleeding()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["campfire_nostrums"],
    },
    "XRL.World.Parts/Door.cs::Door.AttemptOpen(GameObject,bool,bool,bool,bool,bool,bool,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["door_attempt_open"],
    },
    "XRL.World.Parts/Door.cs::Door.HackingResultSuccess(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["door_hacking_result"],
    },
    "XRL.World.Parts/Door.cs::Door.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["door_hacking_result"],
    },
    "XRL.World.Parts/Door.cs::Door.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["door_hacking_result"],
    },
    "XRL.World.Parts/Door.cs::Door.HackingResultFailure(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["door_hacking_result"],
    },
    "XRL.World.Parts/Door.cs::Door.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["door_hacking_result"],
    },
    "XRL.World.Parts/PowerSwitch.cs::PowerSwitch.HackingResultSuccess(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/PowerSwitch.cs::"
        "PowerSwitch.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    "XRL.World.Parts/PowerSwitch.cs::PowerSwitch.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    "XRL.World.Parts/PowerSwitch.cs::PowerSwitch.HackingResultFailure(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    "XRL.World.Parts/PowerSwitch.cs::PowerSwitch.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/TemplarPhylactery.cs::"
        "TemplarPhylactery.HackingResultSuccess(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/TemplarPhylactery.cs::"
        "TemplarPhylactery.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/TemplarPhylactery.cs::"
        "TemplarPhylactery.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/TemplarPhylactery.cs::"
        "TemplarPhylactery.HackingResultFailure(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/TemplarPhylactery.cs::"
        "TemplarPhylactery.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/CyberneticsTerminal2.cs::"
        "CyberneticsTerminal2.HackingResultExceptionalSuccess(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/CyberneticsTerminal2.cs::"
        "CyberneticsTerminal2.HackingResultPartialSuccess(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/CyberneticsTerminal2.cs::"
        "CyberneticsTerminal2.HackingResultFailure(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    (
        "XRL.World.Parts/CyberneticsTerminal2.cs::"
        "CyberneticsTerminal2.HackingResultCriticalFailure(GameObject,GameObject,HackingSifrah)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["hacking_sifrah_result"],
    },
    "XRL.World.Parts/Leveler.cs::Leveler.RapidAdvancement(int,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["leveler_rapid_advancement"],
    },
    "XRL.World.Parts/VehicleSeat.cs::VehicleSeat.AttemptPilot(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["vehicle_seat"],
    },
    "XRL.World.Parts/DecoyHologramEmitter.cs::DecoyHologramEmitter.ActivateHologramBracelet(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["decoy_hologram_emitter_activate"],
    },
    "XRL.World.Parts/TeleporterPair.cs::TeleporterPair.AttemptTeleport(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["teleporter_pair"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.Preserve()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["campfire_preserve"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.PreserveExotic()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["campfire_preserve"],
    },
    "XRL.World.Parts/JoppaZealot.cs::JoppaZealot.ZealotDeclaim(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["zealot_declaim"],
    },
    "XRL.World.Parts/SixDayZealot.cs::SixDayZealot.ZealotDeclaim(GameObject,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["zealot_declaim"],
    },
    "XRL.World/GameObject.cs::GameObject.ChangeCompanionAbilityUse(GameObject,ActivatedAbilities)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_companion_ability"],
    },
    "XRL.World/GameObject.cs::GameObject.CheckCompanionDirection(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_check_companion_direction"],
    },
    "XRL.World/GameObject.cs::GameObject.ConfirmUseImportantAsync(GameObject,string,string,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_confirm_use_important"],
    },
    "XRL.World/GameObject.cs::GameObject.ConfirmUseImportant(GameObject,string,string,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_confirm_use_important"],
    },
    "XRL.World/GameObject.cs::GameObject.ToggleActivatedAbility(Guid,bool,bool?)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_toggle_activated_ability"],
    },
    "XRL.World/GameObject.cs::GameObject.Heal(int,bool,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["game_object_heal"],
    },
    "XRL.World/GameObject.cs::GameObject.GainSP(int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_stat_popup"],
    },
    "XRL.World/GameObject.cs::GameObject.GainEgo(int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_stat_popup"],
    },
    "XRL.World/GameObject.cs::GameObject.LoseEgo(int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_stat_popup"],
    },
    "XRL.World/GameObject.cs::GameObject.GainIntelligence(int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_stat_popup"],
    },
    "XRL.World/GameObject.cs::GameObject.GainWillpower(int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_stat_popup"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["proselytization_sifrah_result"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["proselytization_sifrah_result"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["proselytization_sifrah_result"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["proselytization_sifrah_result"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ResultExceptionalSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["proselytization_sifrah_result"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["beguiling_sifrah_result"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["beguiling_sifrah_result"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["beguiling_sifrah_result"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["beguiling_sifrah_result"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.ResultExceptionalSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["beguiling_sifrah_result"],
    },
    "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["item_modding_sifrah_result"],
    },
    "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["item_modding_sifrah_result"],
    },
    "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["item_modding_sifrah_result"],
    },
    "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ResultCriticalSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["item_modding_sifrah_result"],
    },
    "XRL.World/RebukingSifrah.cs::RebukingSifrah.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["rebuking_sifrah_result"],
    },
    "XRL.World/RebukingSifrah.cs::RebukingSifrah.ResultPartialSuccess(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["rebuking_sifrah_result"],
    },
    "XRL.UI/ConversationUI.cs::ConversationUI.CheckLost()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["conversation_check_lost"],
    },
    "XRL.World.Parts.Mutation/Belcher.cs::Belcher.Cast(Belcher,string,bool,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["mutation_generated_text"],
    },
    "XRL.World.Parts.Mutation/MassMind.cs::MassMind.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["mass_mind_fire_event"],
    },
    "XRL.World.Parts.Mutation/PackRat.cs::PackRat.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["pack_rat_fire_event"],
    },
    "XRL.World.Parts.Mutation/Precognition.cs::Precognition.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["precognition_fire_event"],
    },
    ("XRL.World.Parts.Mutation/ErosTeleportation.cs::ErosTeleportation.Cast(ErosTeleportation,string,Event,Cell)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["eros_teleportation_cast"],
    },
    "XRL.World.Parts/TerrainTravel.cs::TerrainTravel.HandleEvent(ObjectEnteredCellEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["terrain_travel"],
    },
    "XRL.World.Parts/TerrainTravel.cs::TerrainTravel.HandleLeavingCell(GameObject,ref int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["terrain_travel_leaving_cell"],
    },
    "XRL.UI/JournalScreen.cs::JournalScreen.HandleDelete(string,IBaseJournalEntry,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["journal_screen_handle_delete"],
    },
    "XRL.World.Parts/Polygel.cs::Polygel.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["polygel"],
    },
    "XRL.World.ZoneParts/ScriptCallToArms.cs::ScriptCallToArms.ShowWarning()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["script_call_to_arms_warning"],
    },
    "XRL.World/GameObjectFactory.cs::GameObjectFactory.HandleBlueprintXML(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["game_object_factory_blueprint_xml"],
    },
    "XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.HandleEvent(EndTurnEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["player_mural_controller"],
    },
    "CodeRedemptionManager.cs::CodeRedemptionManager.redeemNoProgress(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["code_redemption"],
    },
    "CodeRedemptionManager.cs::CodeRedemptionManager.redeem(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["code_redemption"],
    },
    "XRL.Core/XRLCore.cs::XRLCore.SaveManagement()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["xrl_core_save_management"],
    },
    "XRL.World.Parts/Examiner.cs::Examiner.ResultCriticalFailure(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["examiner_critical_failure"],
    },
    "XRL.World.Parts/Examiner.cs::Examiner.MakeUnderstood(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["examiner_make_understanding"],
    },
    "XRL.World.Parts/Examiner.cs::Examiner.MakePartiallyUnderstood(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["examiner_make_understanding"],
    },
    "XRL.World/Quest.cs::Quest.ShowFinishStepPopup(QuestStep)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["quest_lifecycle_finish_step"],
    },
    "XRL.World/DynamicQuestRewardElement_GameObject.cs::DynamicQuestRewardElement_GameObject.award()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["single_callsite_misc_popup"],
    },
    "XRL.World.Parts/IModification.cs::IModification.WishModify(string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["imodification_wish_modify"],
    },
    "XRL.World.Parts/CursedCellSocket.cs::CursedCellSocket.HandleEvent(CellChangedEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["single_callsite_misc_popup"],
    },
    ("XRL.World.Parts/NephalProperties.cs::NephalProperties.HandleEvent(BeforeDeathRemovalEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["single_callsite_misc_popup"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.BaetylOfferingSifrah(GameObject,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.FormalWaterRitualSifrah(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.HagglingSifrah(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/DisarmingSifrah.cs::DisarmingSifrah.DisarmingSifrah(GameObject,int,int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ExamineSifrah.cs::ExamineSifrah.ExamineSifrah(GameObject,int,int,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/HackingSifrah.cs::HackingSifrah.HackingSifrah(GameObject,int,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ProselytizationSifrah(GameObject,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/RebukingSifrah.cs::RebukingSifrah.RebukingSifrah(GameObject,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ItemModdingSifrah.cs::ItemModdingSifrah.ItemModdingSifrah(GameObject,int,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ItemNamingSifrah(GameObject,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/RepairSifrah.cs::RepairSifrah.RepairSifrah(GameObject,int,int,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/ReverseEngineeringSifrah.cs::"
        "ReverseEngineeringSifrah.ReverseEngineeringSifrah(GameObject,int,int,int,TinkerData)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/RealityDistortionSifrah.cs::"
        "RealityDistortionSifrah.RealityDistortionSifrah(GameObject,string,string,int,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    ("XRL.World/ReverseEngineeringSifrah.cs::ReverseEngineeringSifrah.CheckEarlyExit(GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/BeguilingSifrah.cs::BeguilingSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    ("XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.CheckEarlyExit(GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/HagglingSifrah.cs::HagglingSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/RebukingSifrah.cs::RebukingSifrah.CheckEarlyExit(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL.World/ReverseEngineeringSifrah.cs::ReverseEngineeringSifrah.Finish(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/RitualSifrahTokenAttributeSacrifice.cs::"
        "RitualSifrahTokenAttributeSacrifice.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs::"
        "RitualSifrahTokenInvokeHigherBeing.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    ("XRL.World/SocialSifrahTokenSecret.cs::SocialSifrahTokenSecret.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/SocialSifrahTokenSecret.cs::"
        "SocialSifrahTokenSecret.UseToken(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SOCIAL_SIFRAH_SECRET_USE_TOKEN_EVIDENCE,
    },
    "XRL.World/TinkeringSifrahTokenBit.cs::TinkeringSifrahTokenBit.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/TinkeringSifrahTokenCharge.cs::"
        "TinkeringSifrahTokenCharge.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/TinkeringSifrahTokenComputePower.cs::"
        "TinkeringSifrahTokenComputePower.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    (
        "XRL.World/TinkeringSifrahTokenLiquid.cs::"
        "TinkeringSifrahTokenLiquid.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL/SifrahGame.cs::SifrahGame.MakeMoveForSlot(int,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    "XRL/SifrahGame.cs::SifrahGame.UseInsight(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_pure_owner_popup"],
    },
    ("XRL.World/SocialSifrahTokenGift.cs::SocialSifrahTokenGift.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_token_item_popup"],
    },
    ("XRL.World/SocialSifrahTokenItem.cs::SocialSifrahTokenItem.CheckTokenUse(SifrahGame,SifrahSlot,GameObject)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["sifrah_token_item_popup"],
    },
    ("XRL.World.Parts/DeployableInfrastructure.cs::DeployableInfrastructure.DeployOne(GameObject,Cell,bool,bool)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["deployable_infrastructure_deploy_one"],
    },
    "XRL.World.Parts/FabricateFromSelf.cs::FabricateFromSelf.Activate(bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["fabricate_from_self_activate"],
    },
    "XRL.World.Parts.Mutation/Psychometry.cs::Psychometry.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PRODUCER_MESSAGE_OWNER_EVIDENCE["psychometry_inventory_action"],
    },
    "XRL.World.Parts/LatchesOn.cs::LatchesOn.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["latches_on"],
    },
    "XRL.World.Parts/TattooGun.cs::TattooGun.AttemptTattoo(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["tattoo_gun"],
    },
    "XRL.World.Parts.Mutation/Beguiling.cs::Beguiling.Cast(GameObject,Beguiling,Event,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["beguiling"],
    },
    "XRL.World.Parts/Engraver.cs::Engraver.AttemptEngrave(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["engraver"],
    },
    "XRL.World.Parts/Physics.cs::Physics.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["physics_inventory"],
    },
    "XRL.World.Parts/ITeleporter.cs::ITeleporter.AttemptTeleport(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["iteleporter"],
    },
    "XRL.World.Parts/EnergyAmmoLoader.cs::EnergyAmmoLoader.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["energy_loader"],
    },
    "XRL.World.Parts/ElectricalDischargeLoader.cs::ElectricalDischargeLoader.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["energy_loader"],
    },
    "XRL.World.Parts/DataDisk.cs::DataDisk.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["data_disk"],
    },
    "XRL.World.Parts/PetEitherOr.cs::PetEitherOr.explode()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["pet_either_or"],
    },
    "XRL.World.Parts/Bed.cs::Bed.AttemptSleep(GameObject,out bool,out bool,out bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["bed_chair"],
    },
    "XRL.World.Parts/LiquidVolume.cs::LiquidVolume.Pour(ref bool,GameObject,Cell,bool,bool,int,bool)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["liquid_volume"],
    },
    "XRL.World.Parts/Chair.cs::Chair.SitDown(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["bed_chair"],
    },
    "XRL.World.Parts/StairsDown.cs::StairsDown.CheckPullDown(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["stairs_down"],
    },
    ("XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.HandleEvent(NeutronFluxPourExplodesEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["neutron_flux_pour_explodes"],
    },
    ("XRL.World.Parts/NeutronFluxContainment.cs::NeutronFluxContainment.HandleEvent(BeginTakeActionEvent)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["neutron_flux_begin_take_action"],
    },
    "XRL.World.Parts/Garbage.cs::Garbage.AttemptRifle(GameObject,bool,Cell,List<GameObject>)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["garbage"],
    },
    (
        "XRL.World.Parts/EnergyCellSocket.cs::"
        "EnergyCellSocket.AttemptReplaceCell(GameObject,InventoryActionEvent,int,GameObject)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["energy_cell_socket"],
    },
    "XRL.World.Parts/Enclosing.cs::Enclosing.EnterEnclosure(GameObject,IEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["enclosing"],
    },
    "XRL.World.Parts/Enclosing.cs::Enclosing.ExitEnclosure(GameObject,IEvent,Enclosed)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["enclosing"],
    },
    "XRL.World.Parts/VehicleRecall.cs::VehicleRecall.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["vehicle_recall"],
    },
    "XRL.World/GameObject.cs::GameObject.HandleRename(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["game_object_rename"],
    },
    "XRL.World.Parts/FactionDeed.cs::FactionDeed.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["faction_deed"],
    },
    "XRL.World.Parts/AnimateObject.cs::AnimateObject.HandleEvent(InventoryActionEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["animate_object"],
    },
    "XRL.World.Parts/EelSpawn.cs::EelSpawn.HandleEvent(ObjectEnteredCellEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["eel_spawn"],
    },
    ("XRL.World.Conversations.Parts/WaterRitualBuySecret.cs::WaterRitualBuySecret.RevealEntry(IBaseJournalEntry)"): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["water_ritual_buy_secret"],
    },
    (
        "Qud.API/EquipmentAPI.cs::"
        "EquipmentAPI.TwiddleObject(GameObject,GameObject,ref bool,out InventoryAction,bool,bool,bool)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["equipment_api_twiddle"],
    },
    "XRL.World.Parts/Campfire.cs::Campfire.Cook()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["campfire_cook"],
    },
    "XRL.World.Parts/Examiner.cs::Examiner.ResultPartialSuccess(GameObject,int)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["examiner_partial_success"],
    },
    "XRL.World.Effects/Submerged.cs::Submerged.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["submerged_burrowed_owner"],
    },
    "XRL.World.Effects/Submerged.cs::Submerged.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["submerged_burrowed_owner"],
    },
    "XRL.World.Effects/Submerged.cs::Submerged.Remove(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["submerged_burrowed_owner"],
    },
    "XRL.World.Effects/Burrowed.cs::Burrowed.Apply(GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["submerged_burrowed_owner"],
    },
    "XRL.World.Effects/Burrowed.cs::Burrowed.FireEvent(Event)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["submerged_burrowed_owner"],
    },
    "XRL.World.Effects/Burrowed.cs::Burrowed.Emerge()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["submerged_burrowed_owner"],
    },
    "XRL.World.Parts/Repair.cs::Repair.RepairResultCriticalFailure(GameObject,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["repair_result_critical_failure"],
    },
    (
        "XRL.World.Parts/ConversationScript.cs::"
        "ConversationScript.IsPhysicalConversationPossible(GameObject,GameObject,bool,bool,bool,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["conversation_physical"],
    },
    (
        "XRL.World.Parts/ConversationScript.cs::"
        "ConversationScript.IsMentalConversationPossible(GameObject,GameObject,bool,bool,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["conversation_mental"],
    },
    "XRL.UI/TradeUI.cs::TradeUI.DoVendorExamine(GameObject,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["trade_ui_vendor_examine"],
    },
    "XRL.UI/TradeUI.cs::TradeUI.DoVendorRecharge(GameObject,GameObject)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["trade_ui_vendor_recharge"],
    },
    (
        "XRL.World.Parts.Mutation/Precognition.cs::"
        "Precognition.OnBeforeDie(GameObject,Guid,Guid,ref int,ref int,ref int,ref long,bool,bool,IPart)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_COMBAT_MESSAGE_OWNER_EVIDENCE["precognition_before_die"],
    },
    "Qud.UI/PlayerStatusBar.cs::PlayerStatusBar.Update()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_PLAYER_STATUS_BAR_UI_EVIDENCE,
    },
    "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.HandleHighlightLeft(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_ABILITY_MANAGER_SCREEN_UI_EVIDENCE,
    },
    "Qud.UI/MainMenu.cs::MainMenu.Show()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MAIN_MENU_UI_EVIDENCE,
    },
    "Qud.UI/MissileWeaponArea.cs::MissileWeaponArea.AfterRender(XRLCore,ScreenBuffer)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_MISSILE_WEAPON_AREA_UI_EVIDENCE,
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.UpdateTotals()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRADE_SCREEN_UI_EVIDENCE,
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_TRADE_SCREEN_UI_EVIDENCE,
    },
    "Qud.UI/SkillsAndPowersLine.cs::SkillsAndPowersLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SKILLS_AND_POWERS_LINE_UI_EVIDENCE,
    },
    "Qud.UI/CharacterMutationLine.cs::CharacterMutationLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_LINE_UI_EVIDENCE["character_mutation_line"],
    },
    "Qud.UI/QuestsLine.cs::QuestsLine.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_LINE_UI_EVIDENCE["quests_line"],
    },
    "Qud.UI/QuestsLine.cs::QuestsLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_LINE_UI_EVIDENCE["quests_line"],
    },
    "Qud.UI/QuestsLine.cs::QuestsLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_LINE_UI_EVIDENCE["quests_line"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.Show()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_STATUS_LINE_UI_EVIDENCE["high_scores"],
    },
    "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.BUY_MUTATION": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARACTER_STATUS_MUTATION_MENU_EVIDENCE,
    },
    "Qud.UI/CharacterStatusScreen.cs::CharacterStatusScreen.SHOW_EFFECTS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHARACTER_STATUS_MUTATION_MENU_EVIDENCE,
    },
    "XRL.World.Parts/CherubimSpawner.cs::CherubimSpawner.ReplaceDescription(GameObject,string,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_CHERUBIM_DESCRIPTION_EVIDENCE,
    },
    "Qud.API/SavesAPI.cs::SavesAPI.ReadSaveJson(string,string)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_SAVES_API_DESCRIPTION_EVIDENCE,
    },
    "Qud.UI/CyberneticsTerminalScreen.cs::CyberneticsTerminalScreen.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["cybernetics_terminal"],
    },
    "Qud.UI/StatusScreensScreen.cs::StatusScreensScreen.SET_FILTER": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["status_screens"],
    },
    "Qud.UI/StatusScreensScreen.cs::StatusScreensScreen.defaultMenuOptionOrder": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["status_screens"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.CMD_SHOWCYBERNETICS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.CMD_OPTIONS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.SET_PRIMARY_LIMB": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.SHOW_TOOLTIP": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.QUICK_DROP": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.QUICK_EAT": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.QUICK_DRINK": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/InventoryAndEquipmentStatusScreen.cs::InventoryAndEquipmentStatusScreen.QUICK_APPLY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["inventory_equipment_status"],
    },
    "Qud.UI/JournalStatusScreen.cs::JournalStatusScreen.CMD_INSERT": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["journal_status"],
    },
    "Qud.UI/JournalStatusScreen.cs::JournalStatusScreen.CMD_DELETE": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["journal_status"],
    },
    "Qud.UI/BookScreen.cs::BookScreen.PREV_PAGE": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["book_screen"],
    },
    "Qud.UI/BookScreen.cs::BookScreen.NEXT_PAGE": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["book_screen"],
    },
    "Qud.UI/BookScreen.cs::BookScreen.getItemMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["book_screen"],
    },
    "Qud.UI/Credits.cs::Credits.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["credits"],
    },
    "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.defaultMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager"],
    },
    "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.TOGGLE_SORT": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager"],
    },
    "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.FILTER_ITEMS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager"],
    },
    "Qud.UI/AbilityManagerScreen.cs::AbilityManagerScreen.FilterItems()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager"],
    },
    "Qud.UI/MainMenu.cs::MainMenu.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["main_menu"],
    },
    "Qud.UI/GameSummaryScreen.cs::GameSummaryScreen.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["game_summary"],
    },
    "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.defaultMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object"],
    },
    "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.getItemMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object"],
    },
    "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.TOGGLE_SORT": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object"],
    },
    "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.TAKE_ALL": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object"],
    },
    "Qud.UI/PickGameObjectScreen.cs::PickGameObjectScreen.STORE_ITEM": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.defaultMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.getItemMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.SET_FILTER": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.TOGGLE_SORT": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.OFFER_TRADE": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.ADD_ONE": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.REMOVE_ONE": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.TOGGLE_ALL": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/TradeScreen.cs::TradeScreen.VENDOR_ACTIONS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_screen"],
    },
    "Qud.UI/HelpScreen.cs::HelpScreen.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["help_screen"],
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.REMOVE_BIND": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["keybinds_screen"],
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.RESTORE_DEFAULTS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["keybinds_screen"],
    },
    "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.BIND_KEY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager_line"],
    },
    "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.MOVE_DOWN": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager_line"],
    },
    "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.MOVE_UP": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager_line"],
    },
    "Qud.UI/AbilityManagerLine.cs::AbilityManagerLine.UNBIND_KEY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ability_manager_line"],
    },
    "Qud.UI/KeybindRow.cs::KeybindRow.dataRow": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["keybind_row"],
    },
    "Qud.UI/MessageLogLine.cs::MessageLogLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["message_log_line"],
    },
    "Qud.UI/MessageLogLine.cs::MessageLogLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["message_log_line"],
    },
    "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object_line"],
    },
    "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object_line"],
    },
    "Qud.UI/PickGameObjectLine.cs::PickGameObjectLine.itemOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["pick_game_object_line"],
    },
    "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs::QudMutationsModuleWindow.UpdateControls()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["qud_mutations_module"],
    },
    "Qud.UI/AchievementView.cs::AchievementView.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["achievement_view"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.ACHIEVEMENTS": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["high_scores_static_menu"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.LOCAL_SCORES": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["high_scores_static_menu"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.GLOBAL_DAILY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["high_scores_static_menu"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.FRIENDS_DAILY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["high_scores_static_menu"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.PREVIOUS_DAY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["high_scores_static_menu"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.NEXT_DAY": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["high_scores_static_menu"],
    },
    "Qud.UI/FactionsStatusScreen.cs::FactionsStatusScreen.ShowScreen(GameObject,StatusScreensScreen)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/HighScoresScreen.cs::HighScoresScreen.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/KeybindsScreen.cs::KeybindsScreen.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/CharacterAttributeLine.cs::CharacterAttributeLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/CharacterAttributeLine.cs::CharacterAttributeLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/FilterBarCategoryButton.cs::FilterBarCategoryButton.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["filter_bar_category_button"],
    },
    "Qud.UI/FilterBarCategoryButton.cs::FilterBarCategoryButton.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["filter_bar_category_button"],
    },
    "Qud.UI/FilterBarCategoryButton.cs::FilterBarCategoryButton.itemOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["filter_bar_category_button"],
    },
    "Qud.UI/AskNumberScreen.cs::AskNumberScreen.getItemMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/SaveManagement.cs::SaveManagement.UpdateMenuBars()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/CharacterEffectLine.cs::CharacterEffectLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/CharacterEffectLine.cs::CharacterEffectLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/CharacterMutationLine.cs::CharacterMutationLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/CharacterMutationLine.cs::CharacterMutationLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/EquipmentLine.cs::EquipmentLine.categoryExpandOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/EquipmentLine.cs::EquipmentLine.categoryCollapseOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["ui_menu_option_description"],
    },
    "Qud.UI/OptionsScreen.cs::OptionsScreen.defaultMenuOptions": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_static_menu"],
    },
    "Qud.UI/OptionsScreen.cs::OptionsScreen.COLLAPSE_ALL": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_static_menu"],
    },
    "Qud.UI/OptionsScreen.cs::OptionsScreen.EXPAND_ALL": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_static_menu"],
    },
    "Qud.UI/OptionsScreen.cs::OptionsScreen.HELP_TEXT": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_static_menu"],
    },
    "Qud.UI/OptionsScreen.cs::OptionsScreen.HandleMenuOption(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_controls"],
    },
    "Qud.UI/OptionsCategoryControl.cs::OptionsCategoryControl.Render()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_controls"],
    },
    "Qud.UI/OptionsCheckboxControl.cs::OptionsCheckboxControl.Render()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_controls"],
    },
    "Qud.UI/OptionsRow.cs::OptionsRow.setData(FrameworkDataElement)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_controls"],
    },
    "Qud.UI/OptionsButtonControl.cs::OptionsButtonControl.Render()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["options_screen_controls"],
    },
    "Qud.UI/TradeLine.cs::TradeLine.Update()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_line_numeric"],
    },
    "Qud.UI/TradeLine.cs::TradeLine.OnBeginDrag(PointerEventData)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_line_numeric"],
    },
    "Qud.UI/TradeLine.cs::TradeLine.OnDrag(PointerEventData)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_line_numeric"],
    },
    "Qud.UI/TradeLine.cs::TradeLine.OnScroll(PointerEventData)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["trade_line_numeric"],
    },
    "Qud.API/IBaseJournalEntry.cs::IBaseJournalEntry.GetDisplayText()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_JOURNAL_ENTRY_DISPLAY_TEXT_EVIDENCE,
    },
    "Qud.API/JournalVillageNote.cs::JournalVillageNote.GetDisplayText()": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": ISSUE719_JOURNAL_ENTRY_DISPLAY_TEXT_EVIDENCE,
    },
}

ISSUE719_ACTIVE_EFFECT_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers XRL.World.Effect base GetDetails and "
        "XRL.World.Effects GetDescription/GetDetails EffectDescriptionReturn rows "
        "through the active-effect owner route; effect popup/message/display-name "
        "rows remain separate."
    ),
    "Mods/QudJP/Assemblies/src/Patches/EffectDescriptionPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/EffectDetailsPatch.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
]

ISSUE719_ACTIVE_EFFECT_DISPLAY_NAME_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers XRL.World.Effects DisplayNameAssignment "
        "rows listed in the active-effect producer inventory through the existing "
        "active-effect text route; non-effect display-name composition remains separate."
    ),
    "docs/active-effect-producer-inventory.json",
    "Mods/QudJP/Assemblies/src/Patches/EffectDescriptionPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/EffectDetailsPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenHighlightEffectPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ActiveEffectTextTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
]

ISSUE719_MUTATION_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers mutation GetDescription() long "
        "description rows through the mutation-description owner routes; "
        "activated ability registrations and runtime popup/message rows remain separate."
    ),
    "Mods/QudJP/Localization/Dictionaries/mutation-descriptions.ja.json",
    "Mods/QudJP/Localization/Dictionaries/mutation-ranktext.ja.json",
    "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenMutationDetailsPatch.cs",
    "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/ChargenStructuredTextTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CharacterStatusScreenMutationDetailsPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/ChargenStructuredTextTranslatorTests.cs",
    "scripts/tests/test_mutation_description_semantics.py",
]

ISSUE719_WORLD_MOD_DESCRIPTION_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 residual review covers Mod*.GetDescription(...) rows "
        "through the world-mods description owner route; object display names, "
        "tinkering action popups, and unrelated producer messages remain separate."
    ),
    "Mods/QudJP/Localization/Dictionaries/world-mods.ja.json",
    "Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs",
    "Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldModsTextTranslatorTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs",
    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionLongDescriptionPatchTests.cs",
]


class TextConstructionFamily(TypedDict):
    """Family record emitted by TextConstructionInventory."""

    family_id: str
    file: str
    namespace: str | None
    type_name: str
    member_name: str
    member_signature: str
    member_kind: str
    member_start_line: int
    text_construction_count: int
    shape_counts: dict[str, int]
    context_counts: dict[str, int]
    surface_counts: dict[str, int]
    first_lines: list[int]


class TextConstructionInventory(TypedDict):
    """TextConstructionInventory JSON payload."""

    schema_version: str
    game_version: str
    totals: dict[str, object]
    families: list[TextConstructionFamily]


class SurfaceQueueEntry(TypedDict):
    """One classified text-construction family for localization planning."""

    classification: Classification
    closure_lane: ClosureLane
    closure_status: ClosureStatus
    closure_evidence: list[str]
    family_id: str
    source_file: str
    type_name: str
    member_name: str
    member_signature: str
    member_start_line: int
    text_construction_count: int
    player_visible_surfaces: list[str]
    contextual_surfaces: list[str]
    construction_only_surfaces: list[str]
    non_target_surfaces: list[str]
    first_lines: list[int]
    reason: str
    action: str


class SurfaceQueuePayload(TypedDict):
    """Serialized classified surface queue."""

    schema_version: str
    inventory: str
    counts: dict[str, int]
    lane_counts: dict[str, int]
    entries: list[SurfaceQueueEntry]


class LaneSummary(TypedDict):
    """Aggregated closure-lane handoff summary."""

    entry_count: int
    text_construction_count: int
    closure_status_counts: dict[str, int]
    top_entries: list[SurfaceQueueEntry]


class LaneSummaryPayload(TypedDict):
    """Serialized closure-lane summary."""

    schema_version: str
    inventory: str
    lane_counts: dict[str, int]
    lanes: dict[str, LaneSummary]


class ResidualBucketEntry(SurfaceQueueEntry):
    """One unreviewed entry assigned to a follow-up execution bucket."""

    residual_bucket: str
    residual_disposition: ResidualDisposition


class ResidualBucketSummary(TypedDict):
    """Aggregated issue-719 residual bucket summary."""

    entry_count: int
    text_construction_count: int
    disposition: ResidualDisposition
    lane_counts: dict[str, int]
    top_entries: list[ResidualBucketEntry]


class ResidualBucketPayload(TypedDict):
    """Serialized issue-719 residual execution queue."""

    schema_version: str
    inventory: str
    total_entries: int
    bucket_counts: dict[str, int]
    disposition_counts: dict[str, int]
    lane_counts: dict[str, int]
    buckets: dict[str, ResidualBucketSummary]
    entries: list[ResidualBucketEntry]


class FollowupIssueDefinition(TypedDict):
    """Static metadata for one issue-719 residual follow-up work item."""

    title: str
    github_issue_number: int
    track: str
    buckets: tuple[str, ...]
    acceptance_criteria: tuple[str, ...]


class FollowupIssueSummary(TypedDict):
    """Aggregated follow-up issue payload."""

    title: str
    github_issue_number: int
    track: str
    buckets: list[str]
    entry_count: int
    text_construction_count: int
    disposition_counts: dict[str, int]
    lane_counts: dict[str, int]
    acceptance_criteria: list[str]
    top_entries: list[ResidualBucketEntry]


class FollowupIssuePayload(TypedDict):
    """Serialized issue-719 residual tracker payload."""

    schema_version: str
    inventory: str
    total_entries: int
    issue_counts: dict[str, int]
    track_counts: dict[str, int]
    issues: dict[str, FollowupIssueSummary]


class ClassifiedSurface(TypedDict):
    """Internal classification result."""

    classification: Classification
    player_visible_surfaces: list[str]
    contextual_surfaces: list[str]
    construction_only_surfaces: list[str]
    non_target_surfaces: list[str]
    reason: str
    action: str


ISSUE719_FOLLOWUP_ISSUES: Final[dict[str, FollowupIssueDefinition]] = {
    "issue719-consolidated-residuals": {
        "title": "Issue #719 consolidated residual text-construction tracker",
        "github_issue_number": 719,
        "track": "consolidated",
        "buckets": (
            "action_description_autoact_gap",
            "action_description_runtime",
            "activated_ability_asset_bridge",
            "activated_ability_misc_provider_gap",
            "active_effect_message_frame_route_split",
            "active_effect_fungal_spore_infection_popup_gap",
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
            "generated_display_name_core_running_behavior_runtime",
            "generated_display_name_cooking_preset_recipe_gap",
            "generated_display_name_cooking_recipe_runtime",
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
            "generated_display_name_village_signature_item_gap",
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
            "producer_broad_gameobject_autoequip_gap",
            "producer_broad_gameobject_autoequip_runtime",
            "producer_broad_gameobject_death_gap",
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
            "producer_runtime_core_population_roll_one_error_gap",
            "producer_runtime_core_population_wish_find_blueprint_gap",
            "producer_runtime_core_scores_legacy_screen_gap",
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
            "producer_runtime_inventory_action_desalination_pellet_gap",
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
            "producer_runtime_ui_equipment_slot_gap",
            "producer_runtime_ui_inventory_trade_popup_route_split",
            "producer_runtime_ui_mod_manager_cancel_gap",
            "producer_runtime_ui_object_finder_filters_gap",
            "producer_runtime_ui_trade_vendor_actions_gap",
            "producer_runtime_ui_misc_popup_route_split",
            "producer_runtime_ui_ability_manager_empty_gap",
            "producer_runtime_ui_options_command_binding_gap",
            "producer_runtime_ui_options_help_popup_gap",
            "producer_runtime_ui_options_legacy_popup_gap",
            "producer_runtime_ui_options_popup_route_split",
            "producer_runtime_ui_factions_status_sort_gap",
            "producer_runtime_ui_inventory_status_options_gap",
            "producer_runtime_ui_route_split",
            "producer_runtime_ui_status_popup_route_split",
            "producer_runtime_ui_tutorial_popup_route_split",
            "producer_runtime_world_part_message_frame_route_split",
            "producer_runtime_world_part_does_emit_message_frame_route_split",
            "producer_runtime_world_part_does_emit_route_split",
            "producer_runtime_world_part_does_message_frame_route_split",
            "producer_runtime_world_part_does_popup_route_split",
            "producer_runtime_world_part_does_route_split",
            "producer_runtime_world_part_emit_message_frame_popup_route_split",
            "producer_runtime_world_part_emit_popup_route_split",
            "producer_runtime_world_part_defibrillator_gap",
            "producer_runtime_world_part_disguise_popup_gap",
            "producer_runtime_world_part_golem_popup_runtime",
            "producer_runtime_world_part_grip_recoil_popup_gap",
            "producer_runtime_world_part_golem_mound_popup_gap",
            "producer_runtime_world_part_heat_self_frame_gap",
            "producer_runtime_world_part_biome_distribution_queue_popup_gap",
            "producer_runtime_world_part_elevator_switch_queue_popup_gap",
            "producer_runtime_world_part_liquid_cleaning_frame_gap",
            "producer_runtime_world_part_liquid_contact_frame_gap",
            "producer_runtime_world_part_mixed_route_split",
            "producer_runtime_world_part_movement_popup_runtime",
            "producer_runtime_world_part_magazine_supply_gap",
            "producer_runtime_world_part_dance_opponent_debug_queue_gap",
            "producer_runtime_world_part_dance_opponent_register_queue_gap",
            "producer_runtime_world_part_campfire_extinguish_gap",
            "producer_runtime_world_part_chat_emit_gap",
            "producer_runtime_world_part_fungal_cure_emit_gap",
            "producer_runtime_world_part_interior_damage_queue_gap",
            "producer_runtime_world_part_harvestable_attempt_gap",
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
            "producer_runtime_world_part_vehicle_infiltration_popup_gap",
            "producer_runtime_world_part_vortex_apply_gap",
            "producer_runtime_world_part_vortex_periodic_frame_gap",
            "producer_runtime_world_part_wish_debug_popup_gap",
            "producer_runtime_world_part_wish_debug_popup_runtime",
            "sifrah_description_route_split",
            "sifrah_description_token_dynamic_constructor_gap",
            "sifrah_description_token_getdescription_gap",
            "sifrah_description_unused_base_game_runtime",
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
            "ui_menu_option_static_description_gap",
            "ui_options_control_description_gap",
            "ui_description_assignment_runtime",
            "ui_direct_text_gap",
            "ui_screen_console_input_runtime",
            "ui_screen_cybernetics_terminal_runtime",
            "ui_screen_data_bound_runtime",
            "ui_screen_fixed_label_gap",
            "ui_screen_left_side_category_gap",
            "ui_screen_hotkey_control_runtime",
            "ui_screen_inventory_drag_numeric_runtime",
            "ui_screen_left_side_category_runtime",
            "ui_screen_missile_weapon_status_runtime",
            "ui_screen_mod_manager_back_button_runtime",
            "ui_screen_notification_runtime",
            "ui_screen_options_control_runtime",
            "ui_screen_popup_message_runtime",
            "ui_screen_progress_numeric_runtime",
            "ui_screen_status_stat_runtime",
            "ui_screen_trade_drag_numeric_runtime",
            "ui_screen_trade_highlight_runtime",
            "ui_popup_sink_route_split",
            "ui_screen_route_runtime",
            "ui_screen_trade_inventory_runtime",
            "ui_screen_world_generation_runtime",
            "world_part_description_gap",
            "world_zone_display_name_runtime",
        ),
        "acceptance_criteria": (
            "Keep all remaining Issue #719 residual buckets in this single consolidated tracker.",
            "Do not create additional follow-up issues for residual buckets without explicit user approval.",
            "Promote rows only with exact owner-route evidence or documented runtime evidence.",
            "Cover implemented routes with focused unit or route-resolution tests before promotion.",
        ),
    },
}


ISSUE719_FOLLOWUP_BY_BUCKET: Final[dict[str, str]] = {
    bucket: followup_id
    for followup_id, definition in ISSUE719_FOLLOWUP_ISSUES.items()
    for bucket in definition["buckets"]
}


def load_inventory(path: Path) -> TextConstructionInventory:
    """Load a TextConstructionInventory JSON payload."""
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict) and isinstance(payload.get("families"), list):
        payload = {
            **payload,
            "families": [_normalize_family_payload(family) for family in payload["families"]],
        }
    return cast("TextConstructionInventory", payload)


def _normalize_family_payload(family: object) -> object:
    if not isinstance(family, dict):
        return family

    surface_counts = family.get("surface_counts")
    callsite_count = family.get("callsite_count")
    representative_calls = family.get("representative_calls")
    first_lines = (
        [call["line"] for call in representative_calls if isinstance(call, dict) and isinstance(call.get("line"), int)]
        if isinstance(representative_calls, list)
        else []
    )
    existing_text_construction_count = family.get("text_construction_count")

    return {
        **family,
        "family_id": family.get("family_id", family.get("producer_family_id", "")),
        "member_signature": family.get("member_signature", family.get("member_name", "")),
        "text_construction_count": (
            existing_text_construction_count
            if isinstance(existing_text_construction_count, int)
            else callsite_count
            if isinstance(callsite_count, int)
            else sum(surface_counts.values())
            if isinstance(surface_counts, dict)
            else 0
        ),
        "shape_counts": family.get("shape_counts", {}),
        "context_counts": family.get("context_counts", {}),
        "first_lines": family.get("first_lines") or first_lines,
    }


def classify_family(family: TextConstructionFamily) -> ClassifiedSurface:
    """Classify whether a family is a valuable localization surface."""
    surfaces = set(family["surface_counts"])
    player_visible_surfaces = sorted(surfaces & PLAYER_VISIBLE_API_SURFACES)
    contextual_surfaces = sorted(surfaces & CONTEXTUAL_OWNER_SURFACES)
    construction_only_surfaces = sorted(surfaces & CONSTRUCTION_ONLY_SURFACES)
    non_target_surfaces = sorted(surfaces & NON_TARGET_SURFACES)

    if player_visible_surfaces and _is_low_value_source_file(family["file"]):
        return {
            "classification": "candidate_only",
            "player_visible_surfaces": player_visible_surfaces,
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "debug, test, metrics, workshop, wish, or tool-like source is not normal gameplay coverage",
            "action": "promote only if runtime evidence shows this route matters to ordinary player localization",
        }

    if player_visible_surfaces:
        return {
            "classification": "player_visible_api",
            "player_visible_surfaces": player_visible_surfaces,
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "known player-visible API or route-return surface",
            "action": "trace owner route, add or extend owner translator, and test the route",
        }

    if contextual_surfaces and _is_player_visible_owner_candidate(family):
        return {
            "classification": "player_visible_owner_candidate",
            "player_visible_surfaces": [],
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "text assignment occurs in a likely player-visible owner class or method",
            "action": "confirm screen/owner field, then patch the screen-specific route if needed",
        }

    if contextual_surfaces or construction_only_surfaces:
        return {
            "classification": "candidate_only",
            "player_visible_surfaces": [],
            "contextual_surfaces": contextual_surfaces,
            "construction_only_surfaces": construction_only_surfaces,
            "non_target_surfaces": non_target_surfaces,
            "reason": "string construction exists, but player visibility is not proven by this surface",
            "action": "promote only if a visible owner route or runtime evidence proves player exposure",
        }

    return {
        "classification": "non_target",
        "player_visible_surfaces": [],
        "contextual_surfaces": [],
        "construction_only_surfaces": [],
        "non_target_surfaces": non_target_surfaces,
        "reason": "attribute, initializer, generic invocation, or other non-surface text",
        "action": "do not use for localization coverage unless another route promotes it",
    }


def build_surface_queue(inventory: TextConstructionInventory) -> list[SurfaceQueueEntry]:
    """Classify every text-construction family and return a stable queue."""
    return sorted(
        (_queue_entry(family) for family in inventory["families"]),
        key=lambda entry: (
            CLASSIFICATION_ORDER[entry["classification"]],
            -entry["text_construction_count"],
            entry["source_file"],
            entry["member_start_line"],
            entry["family_id"],
        ),
    )


def valuable_surface_queue(inventory: TextConstructionInventory) -> list[SurfaceQueueEntry]:
    """Return only families worth considering for localization ownership."""
    return [entry for entry in build_surface_queue(inventory) if entry["classification"] in VALUABLE_CLASSIFICATIONS]


def queue_payload(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "valuable",
) -> SurfaceQueuePayload:
    """Build a JSON-serializable classified queue payload."""
    entries = _filter_entries(build_surface_queue(inventory), include)
    counts: dict[str, int] = {}
    lane_counts: dict[str, int] = {}
    for entry in entries:
        counts[entry["classification"]] = counts.get(entry["classification"], 0) + 1
        lane_counts[entry["closure_lane"]] = lane_counts.get(entry["closure_lane"], 0) + 1

    return {
        "schema_version": "1.0",
        "inventory": str(inventory_path),
        "counts": counts,
        "lane_counts": dict(sorted(lane_counts.items(), key=lambda item: LANE_ORDER[item[0]])),
        "entries": entries,
    }


def lane_summary_payload(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "valuable",
    top_per_lane: int = 5,
) -> LaneSummaryPayload:
    """Build an actionable closure-lane summary with representative top families."""
    payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
    lanes: dict[str, LaneSummary] = {}
    for entry in payload["entries"]:
        lane = entry["closure_lane"]
        summary = lanes.setdefault(
            lane,
            {
                "entry_count": 0,
                "text_construction_count": 0,
                "closure_status_counts": {},
                "top_entries": [],
            },
        )
        summary["entry_count"] += 1
        summary["text_construction_count"] += entry["text_construction_count"]
        closure_status = entry["closure_status"]
        summary["closure_status_counts"][closure_status] = summary["closure_status_counts"].get(closure_status, 0) + 1
        if top_per_lane > 0:
            summary["top_entries"] = sorted(
                [*summary["top_entries"], entry],
                key=lambda top_entry: (
                    -top_entry["text_construction_count"],
                    top_entry["source_file"],
                    top_entry["member_name"],
                ),
            )[:top_per_lane]

    return {
        "schema_version": "1.0",
        "inventory": str(inventory_path),
        "lane_counts": payload["lane_counts"],
        "lanes": dict(sorted(lanes.items(), key=lambda item: LANE_ORDER[item[0]])),
    }


def residual_bucket_payload(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "unreviewed",
    top_per_bucket: int = 8,
) -> ResidualBucketPayload:
    """Build an issue-719 residual queue with every unreviewed row bucketed."""
    payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
    if include == "unreviewed":
        valuable_payload = queue_payload(inventory, inventory_path=inventory_path, include="valuable")
        source_entries = _issue719_residual_entries(valuable_payload["entries"])
    else:
        source_entries = payload["entries"]
    entries: list[ResidualBucketEntry] = []
    buckets: dict[str, ResidualBucketSummary] = {}
    bucket_counts: dict[str, int] = {}
    disposition_counts: dict[str, int] = {}
    lane_counts: dict[str, int] = {}

    for entry in source_entries:
        residual_bucket, residual_disposition = _residual_bucket_for_entry(entry)
        residual_entry: ResidualBucketEntry = {
            **entry,
            "residual_bucket": residual_bucket,
            "residual_disposition": residual_disposition,
        }
        entries.append(residual_entry)
        bucket_counts[residual_bucket] = bucket_counts.get(residual_bucket, 0) + 1
        disposition_counts[residual_disposition] = disposition_counts.get(residual_disposition, 0) + 1
        lane = entry["closure_lane"]
        lane_counts[lane] = lane_counts.get(lane, 0) + 1

        summary = buckets.setdefault(
            residual_bucket,
            {
                "entry_count": 0,
                "text_construction_count": 0,
                "disposition": residual_disposition,
                "lane_counts": {},
                "top_entries": [],
            },
        )
        summary["entry_count"] += 1
        summary["text_construction_count"] += entry["text_construction_count"]
        summary["lane_counts"][lane] = summary["lane_counts"].get(lane, 0) + 1
        if top_per_bucket > 0:
            summary["top_entries"] = sorted(
                [*summary["top_entries"], residual_entry],
                key=lambda top_entry: (
                    -top_entry["text_construction_count"],
                    top_entry["source_file"],
                    top_entry["member_name"],
                ),
            )[:top_per_bucket]

    return {
        "schema_version": "1.0",
        "inventory": str(inventory_path),
        "total_entries": len(entries),
        "bucket_counts": dict(sorted(bucket_counts.items())),
        "disposition_counts": dict(sorted(disposition_counts.items())),
        "lane_counts": dict(sorted(lane_counts.items(), key=lambda item: LANE_ORDER[item[0]])),
        "buckets": dict(sorted(buckets.items())),
        "entries": entries,
    }


def _issue719_residual_entries(entries: list[SurfaceQueueEntry]) -> list[SurfaceQueueEntry]:
    residual_statuses = {"unreviewed", "action_required", "runtime_required"}
    return [entry for entry in entries if entry["closure_status"] in residual_statuses]


def followup_issue_payload(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "unreviewed",
    top_per_issue: int = 8,
) -> FollowupIssuePayload:
    """Group issue-719 residual buckets into the consolidated tracker."""
    residual_payload = residual_bucket_payload(
        inventory,
        inventory_path=inventory_path,
        include=include,
        top_per_bucket=top_per_issue,
    )
    issues: dict[str, FollowupIssueSummary] = {
        followup_id: {
            "title": definition["title"],
            "github_issue_number": definition["github_issue_number"],
            "track": definition["track"],
            "buckets": list(definition["buckets"]),
            "entry_count": 0,
            "text_construction_count": 0,
            "disposition_counts": {},
            "lane_counts": {},
            "acceptance_criteria": list(definition["acceptance_criteria"]),
            "top_entries": [],
        }
        for followup_id, definition in ISSUE719_FOLLOWUP_ISSUES.items()
    }
    issue_counts: dict[str, int] = {}
    track_counts: dict[str, int] = {}

    for entry in residual_payload["entries"]:
        residual_bucket = entry["residual_bucket"]
        followup_id = ISSUE719_FOLLOWUP_BY_BUCKET.get(residual_bucket)
        if followup_id is None:
            raise KeyError(residual_bucket)

        issue = issues[followup_id]
        if residual_bucket not in issue["buckets"]:
            issue["buckets"].append(residual_bucket)
        issue["entry_count"] += 1
        issue["text_construction_count"] += entry["text_construction_count"]
        disposition = entry["residual_disposition"]
        issue["disposition_counts"][disposition] = issue["disposition_counts"].get(disposition, 0) + 1
        lane = entry["closure_lane"]
        issue["lane_counts"][lane] = issue["lane_counts"].get(lane, 0) + 1
        if top_per_issue > 0:
            issue["top_entries"] = sorted(
                [*issue["top_entries"], entry],
                key=lambda top_entry: (
                    -top_entry["text_construction_count"],
                    top_entry["source_file"],
                    top_entry["member_name"],
                ),
            )[:top_per_issue]

    for followup_id, issue in issues.items():
        issue_counts[followup_id] = issue["entry_count"]
        track = issue["track"]
        track_counts[track] = track_counts.get(track, 0) + issue["entry_count"]
        issue["disposition_counts"] = dict(sorted(issue["disposition_counts"].items()))
        issue["lane_counts"] = dict(sorted(issue["lane_counts"].items(), key=lambda item: LANE_ORDER[item[0]]))

    return {
        "schema_version": "1.0",
        "inventory": str(inventory_path),
        "total_entries": residual_payload["total_entries"],
        "issue_counts": dict(sorted(issue_counts.items())),
        "track_counts": dict(sorted(track_counts.items())),
        "issues": dict(sorted(issues.items())),
    }


def format_surface_queue(
    inventory: TextConstructionInventory,
    *,
    inventory_path: Path,
    include: str = "valuable",
    limit: int | None = 50,
) -> str:
    """Format classified player-visible surface candidates for agent handoff."""
    payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
    entries = payload["entries"] if limit is None else payload["entries"][:limit]
    total_entries = len(payload["entries"])
    lines = [
        f"text construction surface queue: {total_entries} entries; counts={_format_counter(payload['counts'])}",
        f"closure lanes: {_format_counter(payload['lane_counts'])}",
    ]

    for index, entry in enumerate(entries, start=1):
        surfaces = (
            entry["player_visible_surfaces"] or entry["contextual_surfaces"] or entry["construction_only_surfaces"]
        )
        lines.append(
            "".join(
                (
                    f"{index}. [{entry['classification']}/{entry['closure_lane']}] {entry['source_file']}:",
                    f"{entry['member_start_line']} {entry['type_name']}.{entry['member_signature']} ",
                    f"surfaces={','.join(surfaces)} ",
                    f"count={entry['text_construction_count']} ",
                    f"closure={entry['closure_status']}",
                )
            )
        )
        lines.append(f"   reason: {entry['reason']}")
        lines.append(f"   action: {entry['action']}")

    if limit is not None and total_entries > limit:
        lines.append(f"... {total_entries - limit} more entries omitted")

    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    """Classify a TextConstructionInventory JSON file."""
    parser = ArgumentParser(description="Classify player-visible text-construction surfaces.")
    _ = parser.add_argument("--inventory", type=Path, required=True)
    _ = parser.add_argument(
        "--format",
        choices=("text", "json", "lanes-json", "residual-buckets-json", "followup-issues-json"),
        default="text",
    )
    _ = parser.add_argument(
        "--include",
        choices=("valuable", "needs-work", "unreviewed", "all", "candidate-only", "non-target"),
        default="valuable",
    )
    _ = parser.add_argument("--limit", type=int, default=50, help="maximum text rows; 0 means all")
    args = parser.parse_args(argv)

    inventory_path = cast("Path", args.inventory)
    inventory = load_inventory(inventory_path)
    include = cast("str", args.include)
    output_format = cast("OutputFormat", args.format)
    limit_arg = cast("int", args.limit)
    limit = None if limit_arg == 0 else limit_arg

    if output_format == "json":
        payload = queue_payload(inventory, inventory_path=inventory_path, include=include)
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    if output_format == "lanes-json":
        payload = lane_summary_payload(inventory, inventory_path=inventory_path, include=include)
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    if output_format == "residual-buckets-json":
        payload = residual_bucket_payload(inventory, inventory_path=inventory_path, include=include)
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    if output_format == "followup-issues-json":
        payload = followup_issue_payload(inventory, inventory_path=inventory_path, include=include)
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    text = format_surface_queue(inventory, inventory_path=inventory_path, include=include, limit=limit)
    _ = sys.stdout.write(text + "\n")
    return 0


def _queue_entry(family: TextConstructionFamily) -> SurfaceQueueEntry:
    classified = classify_family(family)
    closure_lane = _closure_lane(family, classified)
    closure_status, closure_evidence = _closure_overlay(family, closure_lane)
    entry: SurfaceQueueEntry = {
        "classification": classified["classification"],
        "closure_lane": closure_lane,
        "closure_status": closure_status,
        "closure_evidence": closure_evidence,
        "family_id": family["family_id"],
        "source_file": family["file"],
        "type_name": family["type_name"],
        "member_name": family["member_name"],
        "member_signature": family["member_signature"],
        "member_start_line": family["member_start_line"],
        "text_construction_count": family["text_construction_count"],
        "player_visible_surfaces": classified["player_visible_surfaces"],
        "contextual_surfaces": classified["contextual_surfaces"],
        "construction_only_surfaces": classified["construction_only_surfaces"],
        "non_target_surfaces": classified["non_target_surfaces"],
        "first_lines": family["first_lines"],
        "reason": classified["reason"],
        "action": classified["action"],
    }
    if entry["classification"] in VALUABLE_CLASSIFICATIONS and entry["closure_status"] == "unreviewed":
        residual_bucket, residual_disposition = _residual_bucket_for_entry(entry)
        entry["closure_status"], entry["closure_evidence"] = _issue719_residual_status(
            residual_bucket,
            residual_disposition,
        )
    return entry


def _closure_overlay(family: TextConstructionFamily, closure_lane: ClosureLane) -> tuple[ClosureStatus, list[str]]:
    family_id = family["family_id"]
    issue719_evidence = _issue719_closure_overlay(family, closure_lane)
    if issue719_evidence is not None:
        return issue719_evidence

    if closure_lane == "journal_quest_routes":
        journal_evidence = _issue747_journal_quest_evidence_for(family)
        if journal_evidence is not None:
            return "covered_by_owner_route", journal_evidence

    skill_evidence = _issue747_skill_evidence_for(family)
    if skill_evidence is not None:
        return "covered_by_owner_route", skill_evidence

    overlay = TEXT_CONSTRUCTION_CLOSURE_OVERLAY.get(family_id)
    if overlay is not None:
        return overlay["closure_status"], list(overlay["closure_evidence"])

    if overlay is None and _is_conversation_choice_tag_family_id(family_id):
        return "covered_by_owner_route", list(CONVERSATION_CHOICE_TAG_EVIDENCE)
    return "unreviewed", []


def _issue719_closure_overlay(  # noqa: C901, PLR0911, PLR0912, PLR0915
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> tuple[ClosureStatus, list[str]] | None:
    if family["family_id"] in TEXT_CONSTRUCTION_CLOSURE_OVERLAY:
        return None
    evidence: list[str] | None = None
    if _is_issue719_active_effect_description_family(family, closure_lane):
        evidence = ISSUE719_ACTIVE_EFFECT_DESCRIPTION_EVIDENCE
    elif _is_issue719_active_effect_display_name_family(family, closure_lane):
        classification = _active_effect_display_name_classifications()[family["family_id"]]
        evidence = [
            *ISSUE719_ACTIVE_EFFECT_DISPLAY_NAME_EVIDENCE,
            f"active-effect producer inventory classification: {classification}",
        ]
    elif _is_issue719_mutation_description_family(family, closure_lane):
        evidence = ISSUE719_MUTATION_DESCRIPTION_EVIDENCE
    elif _is_issue719_world_mod_description_family(family, closure_lane):
        evidence = ISSUE719_WORLD_MOD_DESCRIPTION_EVIDENCE
    elif family["family_id"] in (
        ISSUE719_SIFRAH_TOKEN_NO_ARG_DESCRIPTION_FAMILIES
        | ISSUE719_SIFRAH_TOKEN_DYNAMIC_DESCRIPTION_FAMILIES
        | ISSUE719_SIFRAH_TOKEN_GET_DESCRIPTION_FAMILIES
    ):
        evidence = ISSUE719_SIFRAH_TOKEN_DESCRIPTION_EVIDENCE
    if evidence is not None:
        return "covered_by_owner_route", evidence

    static_producer_evidence = _issue719_static_producer_owner_evidence_for(family, closure_lane)
    if static_producer_evidence is not None:
        return "covered_by_owner_route", static_producer_evidence

    if family["family_id"] in ISSUE719_VILLAGE_SIGNATURE_DISH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_VILLAGE_SIGNATURE_DISH_EVIDENCE

    if family["family_id"] in ISSUE719_COOKING_PRESET_RECIPE_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_COOKING_PRESET_RECIPE_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_VILLAGE_CODA_SULTAN_ENTITY_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_VILLAGE_CODA_SULTAN_ENTITY_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_MURAL_BLANK_SLATE_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MURAL_BLANK_SLATE_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_VILLAGE_SIGNATURE_ITEM_STATIC_GAP_FAMILIES:
        return "covered_by_owner_route", ISSUE719_VILLAGE_SIGNATURE_ITEM_STATIC_GAP_EVIDENCE

    if family["family_id"] in ISSUE719_GENERATED_DISPLAY_NAME_OWNER_PATCH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GENERATED_DISPLAY_NAME_OWNER_PATCH_EVIDENCE

    if family["family_id"] in ISSUE719_RUNNING_BEHAVIOR_EVENT_BRIDGE_FAMILIES:
        return "covered_by_owner_route", ISSUE719_RUNNING_BEHAVIOR_EVENT_BRIDGE_EVIDENCE

    if family["family_id"] in ISSUE719_ROCKET_SKATES_RUNNING_BEHAVIOR_FAMILIES:
        return "covered_by_owner_route", ISSUE719_ROCKET_SKATES_RUNNING_BEHAVIOR_EVIDENCE

    if family["family_id"] in ISSUE719_SOUND_MANAGER_DEBUG_PASSTHROUGH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_SOUND_MANAGER_DEBUG_PASSTHROUGH_EVIDENCE

    if family["family_id"] in ISSUE719_BRAIN_DEBUG_INTERNAL_PASSTHROUGH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_BRAIN_DEBUG_INTERNAL_PASSTHROUGH_EVIDENCE

    if family["family_id"] in ISSUE719_MISSILE_TRAJECTORY_MESSAGE_FRAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MISSILE_TRAJECTORY_MESSAGE_FRAME_EVIDENCE

    if family["family_id"] in ISSUE719_GAMEOBJECT_DIE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_DIE_STATIC_GAP_EVIDENCE

    if family["family_id"] in ISSUE719_GAMEOBJECT_DESTROY_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_DESTROY_OWNER_EVIDENCE

    if family["family_id"] == "XRL/GameText.cs::GameText.RoughConvertSecondPersonToThirdPerson(string,GameObject)":
        return "covered_by_owner_route", ISSUE719_GAME_TEXT_THIRD_PERSON_DEATH_GAP_EVIDENCE

    if family["family_id"] == (
        "XRL.World/GameObject.cs::"
        "GameObject.Explode(int,GameObject,string,float,bool,bool,bool,int,List<GameObject>)"
    ):
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_EXPLODE_DEATH_EVIDENCE

    if (
        family["family_id"]
        == "XRL.World/GameObject.cs::GameObject.HandleInventoryActionEvent(InventoryActionEvent)"
    ):
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_INVENTORY_COMPANION_EVIDENCE

    if family["family_id"] == "XRL.World/GameObject.cs::GameObject.PullDown(bool)":
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_PULLDOWN_EVIDENCE

    if (
        family["family_id"]
        == (
            "XRL.World.Capabilities/Firefighting.cs::"
            "Firefighting.AttemptFirefightingCore(GameObject,GameObject,int,bool,bool)"
        )
    ):
        return "covered_by_owner_route", ISSUE719_FIREFIGHTING_OWNER_EVIDENCE

    if (
        family["family_id"]
        == (
            "XRL.World.Parts/Harvestable.cs::"
            "Harvestable.AttemptHarvest(GameObject,bool,string,Cell,List<GameObject>)"
        )
    ):
        return "covered_by_owner_route", ISSUE719_HARVESTABLE_OWNER_EVIDENCE

    if family["family_id"] in ISSUE719_WORLD_PART_FIXED_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_WORLD_PART_FIXED_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_WORLD_PART_GENERATED_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_WORLD_PART_GENERATED_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_RANDOM_FIGURINE_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_RANDOM_FIGURINE_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_MINER_GENERATED_ROLE_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MINER_GENERATED_ROLE_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_POINTED_ASTERISK_WISH_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_POINTED_ASTERISK_WISH_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_SHIP_ARK_POPUP_DICTIONARY_FAMILIES:
        return "covered_by_owner_route", ISSUE719_SHIP_ARK_POPUP_DICTIONARY_EVIDENCE

    if family["family_id"] in ISSUE719_CRAYONS_POPUP_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CRAYONS_POPUP_OWNER_EVIDENCE

    if family["family_id"] in ISSUE719_POPUP_MESSAGE_WRAPPER_SINK_FAMILIES:
        return "not_owner_surface", ISSUE719_POPUP_MESSAGE_WRAPPER_SINK_EVIDENCE

    if family["family_id"] in ISSUE719_STATIC_LINE_MENU_OPTION_FAMILIES:
        return (
            "covered_by_owner_route",
            ISSUE719_UI_DESCRIPTION_MENU_EVIDENCE["static_line_menu_options"],
        )

    if family["family_id"] == "XRL.Core/Scores.cs::Scores.Show()":
        return "covered_by_owner_route", ISSUE719_SCORES_SHOW_STATIC_GAP_EVIDENCE

    if family["family_id"] in ISSUE719_OPTIONS_UI_SHOW_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_OPTIONS_UI_SHOW_OWNER_EVIDENCE

    if family["family_id"] in ISSUE719_OPTIONS_CONTROL_DESCRIPTION_FAMILIES:
        return "covered_by_owner_route", ISSUE719_OPTIONS_CONTROL_DESCRIPTION_EVIDENCE

    if family["family_id"] in ISSUE719_OBJECT_FINDER_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_OBJECT_FINDER_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_CYBERNETICS_SKILLSOFT_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CYBERNETICS_SKILLSOFT_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_CYBERNETICS_RECOILER_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CYBERNETICS_RECOILER_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_STAT_SHIFT_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_STAT_SHIFT_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_MUTATION_BASE_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MUTATION_BASE_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_MUTATION_EFFECT_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MUTATION_EFFECT_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_LIGHT_MANIPULATION_ABILITY_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_LIGHT_MANIPULATION_ABILITY_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_CYBERNETICS_INSTALL_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CYBERNETICS_INSTALL_DISPLAY_NAME_EVIDENCE

    if family["family_id"] in ISSUE719_UI_SCREEN_FIXED_LABEL_FAMILIES:
        return "covered_by_owner_route", ISSUE719_UI_SCREEN_FIXED_LABEL_EVIDENCE
    if family["family_id"] in ISSUE719_EQUIPMENT_SCREEN_BODYPART_EQUIP_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_EQUIPMENT_SCREEN_BODYPART_EQUIP_OWNER_EVIDENCE

    if family["family_id"] in ISSUE719_STOMACH_FIRE_EVENT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_STOMACH_FIRE_EVENT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_FIXIT_SPRAY_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_FIXIT_SPRAY_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_MAGNETIZED_APPLICATOR_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MAGNETIZED_APPLICATOR_OWNER_EVIDENCE

    world_part_queue_does_evidence = (
        ISSUE719_WORLD_PART_QUEUE_DOES_EXISTING_OWNER_EVIDENCE_BY_FAMILY.get(family["family_id"])
    )
    if world_part_queue_does_evidence is not None:
        return "covered_by_owner_route", world_part_queue_does_evidence

    if family["family_id"] == "XRL.World.Quests/ReclamationSystem.cs::ReclamationSystem.HandleEvent(EnteringZoneEvent)":
        return "covered_by_owner_route", ISSUE719_RECLAMATION_MESSAGE_LEAVING_EVIDENCE

    if family["family_id"] in ISSUE719_RESIDUAL_FRAME_DOES_PROMOTION_FAMILIES:
        return "covered_by_owner_route", ISSUE719_RESIDUAL_FRAME_DOES_PROMOTION_EVIDENCE
    if family["family_id"] in ISSUE719_DOMINATION_PROCESS_TARGET_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_DOMINATION_PROCESS_TARGET_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_MAGAZINE_AMMO_SUPPLY_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MAGAZINE_AMMO_SUPPLY_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_INVENTORY_DROP_ASK_NUMBER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_INVENTORY_DROP_ASK_NUMBER_EVIDENCE
    if family["family_id"] in ISSUE719_EXAMINER_HANDLE_EVENT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_EXAMINER_HANDLE_EVENT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_TINKER_ITEM_HANDLE_EVENT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TINKER_ITEM_HANDLE_EVENT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_TEMPORAL_FUGUE_PERFORM_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TEMPORAL_FUGUE_PERFORM_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_FORCE_LATHE_ACTIVATION_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_FORCE_LATHE_ACTIVATION_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_EQUIPMENT_ACTION_MENU_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_EQUIPMENT_ACTION_MENU_OWNER_EVIDENCE
    if family["family_id"] == (
        "XRL.World.Parts/AutomatedExternalDefibrillator.cs::"
        "AutomatedExternalDefibrillator.AttemptDefibrillate(GameObject,IEvent)"
    ):
        return "covered_by_owner_route", ISSUE719_DEFIBRILLATOR_STATIC_GAP_EVIDENCE
    if family["family_id"] in ISSUE719_CHAT_PERFORM_CHAT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CHAT_PERFORM_CHAT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_SPACE_TIME_VORTEX_APPLY_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_SPACE_TIME_VORTEX_APPLY_OWNER_EVIDENCE
    world_part_mixed_evidence = ISSUE719_WORLD_PART_MIXED_STATIC_GAP_EVIDENCE_BY_FAMILY.get(
        family["family_id"]
    )
    if world_part_mixed_evidence is not None:
        return "action_required", world_part_mixed_evidence
    if family["family_id"] in ISSUE719_WORLD_FACTORY_DISPLAY_NAME_DATA_FAMILIES:
        return "covered_by_owner_route", ISSUE719_WORLD_FACTORY_DISPLAY_NAME_DATA_EVIDENCE
    if family["family_id"] in ISSUE719_GAMEOBJECT_REGENERA_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_REGENERA_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_VILLAGE_DYNAMIC_QUEST_REWARD_STATIC_GAP_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GENERATED_DISPLAY_NAME_OWNER_PATCH_EVIDENCE
    if family["family_id"] in ISSUE719_GOLEM_MOUND_DISPLAY_OPTIONS_STATIC_GAP_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GOLEM_MOUND_DISPLAY_OPTIONS_STATIC_GAP_EVIDENCE
    if family["family_id"] in ISSUE719_AUTOACT_GET_DESCRIPTION_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_AUTOACT_GET_DESCRIPTION_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_GAMEOBJECT_HOSTILE_SPOT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_GAMEOBJECT_HOSTILE_SPOT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_WORLD_PART_PICKOPTION_DICTIONARY_FAMILIES:
        return "covered_by_owner_route", ISSUE719_WORLD_PART_PICKOPTION_DICTIONARY_EVIDENCE
    if family["family_id"] in ISSUE719_HOLOGRAPHIC_VISAGE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_HOLOGRAPHIC_VISAGE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_CAMPFIRE_EXTINGUISH_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CAMPFIRE_EXTINGUISH_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_CARAPACE_LOOSEN_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CARAPACE_LOOSEN_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_IZONE_LANDMARK_WISH_CURRENT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_IZONE_LANDMARK_WISH_CURRENT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_WISH_DEBUG_STATIC_GAP_FAMILIES:
        return "action_required", ISSUE719_WISH_DEBUG_STATIC_GAP_EVIDENCE
    if family["family_id"] in ISSUE719_CORE_DISPLAY_NAME_DATA_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CORE_DISPLAY_NAME_DATA_EVIDENCE
    if family["family_id"] in ISSUE719_CORE_POSSESSIVE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CORE_POSSESSIVE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_CORE_INVALID_OBJECT_DISPLAY_NAME_FAMILIES:
        return "covered_by_owner_route", ISSUE719_CORE_INVALID_OBJECT_DISPLAY_NAME_EVIDENCE
    if family["family_id"] in ISSUE719_PHASE_STICKY_DATA_SENTINEL_FAMILIES:
        return "covered_by_owner_route", ISSUE719_PHASE_STICKY_DATA_SENTINEL_EVIDENCE
    if family["family_id"] in ISSUE719_PHYSICS_TARGETED_MOVE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_PHYSICS_TARGETED_MOVE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_WORLD_GENERATION_SCREEN_QUOTES_DATA_FAMILIES:
        return "covered_by_owner_route", ISSUE719_WORLD_GENERATION_SCREEN_QUOTES_DATA_EVIDENCE
    if family["family_id"] in ISSUE719_JOURNAL_WISH_GOSPEL_DATA_ROUTE_FAMILIES:
        return "covered_by_owner_route", ISSUE719_JOURNAL_WISH_GOSPEL_DATA_ROUTE_EVIDENCE
    if family["family_id"] in ISSUE719_TRADE_HIGHLIGHT_DATA_BINDING_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TRADE_HIGHLIGHT_DATA_BINDING_EVIDENCE
    if family["family_id"] in ISSUE719_UI_WIDGET_DATA_BINDING_PASS_THROUGH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_UI_WIDGET_DATA_BINDING_PASS_THROUGH_EVIDENCE
    if family["family_id"] in ISSUE719_LEFT_SIDE_CATEGORY_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_LEFT_SIDE_CATEGORY_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_FINAL_SINK_PASS_THROUGH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_FINAL_SINK_PASS_THROUGH_EVIDENCE
    if family["family_id"] in ISSUE719_TUTORIAL_SENTINEL_PASS_THROUGH_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TUTORIAL_SENTINEL_PASS_THROUGH_EVIDENCE
    if family["family_id"] in ISSUE719_TUTORIAL_LATEUPDATE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TUTORIAL_LATEUPDATE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_TUTORIAL_POPUP_DICTIONARY_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TUTORIAL_POPUP_DICTIONARY_EVIDENCE
    if family["family_id"] in ISSUE719_TUTORIAL_MANAGER_TRIGGER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TUTORIAL_MANAGER_TRIGGER_EVIDENCE
    if family["family_id"] in ISSUE719_FUNGAL_SPORE_CHOOSE_LIMB_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_FUNGAL_SPORE_CHOOSE_LIMB_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_FUNGAL_INFECTION_FIRE_EVENT_FAMILIES:
        return "covered_by_owner_route", ISSUE719_FUNGAL_INFECTION_FIRE_EVENT_EVIDENCE
    if family["family_id"] in ISSUE719_DESALINATION_PELLET_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_DESALINATION_PELLET_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_IGRENADE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_IGRENADE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_BIOME_SURFACE_DISTRIBUTION_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_BIOME_SURFACE_DISTRIBUTION_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_ELEVATOR_SWITCH_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_ELEVATOR_SWITCH_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_ITEM_NAMING_INTERACTIVE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_ITEM_NAMING_INTERACTIVE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_ITEM_NAMING_WISH_DEBUG_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_ITEM_NAMING_WISH_DEBUG_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_TINKERING_MAKERS_MARK_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TINKERING_MAKERS_MARK_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_SAVES_API_FATAL_SAVE_ERROR_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_SAVES_API_FATAL_SAVE_ERROR_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_XRLCORE_RESTORE_MODS_LOADED_FAMILIES:
        return "covered_by_owner_route", ISSUE719_XRLCORE_RESTORE_MODS_LOADED_EVIDENCE
    if family["family_id"] in ISSUE719_MOD_DISGUISE_BEING_APPLIED_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_MOD_DISGUISE_BEING_APPLIED_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_VEHICLE_FOLLOWER_POPUP_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_VEHICLE_FOLLOWER_POPUP_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_FINAL_STATIC_GAP_FAMILIES:
        return "action_required", ISSUE719_FINAL_STATIC_GAP_EVIDENCE
    if family["family_id"] in ISSUE719_TREMBLE_EARTHQUAKE_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_TREMBLE_EARTHQUAKE_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_VEHICLE_MELEE_INFILTRATION_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_VEHICLE_MELEE_INFILTRATION_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_LIQUID_WISH_WARM_EFFECT_OWNER_FAMILIES:
        return "covered_by_owner_route", ISSUE719_LIQUID_WISH_WARM_EFFECT_OWNER_EVIDENCE
    if family["family_id"] in ISSUE719_RESIDUAL_PRODUCER_RUNTIME_FAMILIES:
        bucket, disposition = _producer_runtime_family_bucket_with_disposition(family)
        if disposition == "likely_implementation_gap":
            return "action_required", [
                *ISSUE719_PRODUCER_RUNTIME_STATIC_GAP_EVIDENCE,
                f"residual bucket: {bucket}",
            ]
    residual_runtime_evidence = ISSUE719_RESIDUAL_RUNTIME_EVIDENCE_BY_FAMILY.get(family["family_id"])
    if residual_runtime_evidence is not None:
        return "runtime_required", residual_runtime_evidence

    overlay = ISSUE719_RESIDUAL_CLOSURE_OVERLAY.get(family["family_id"])
    if overlay is not None:
        return overlay["closure_status"], list(overlay["closure_evidence"])
    if "Sifrah" in family["family_id"] and closure_lane == "producer_message_popup":
        bucket, disposition = _sifrah_popup_residual_bucket_for_parts(
            source_file=family["file"],
            member_name=family["member_name"],
        )
        if disposition == "likely_implementation_gap":
            return "action_required", [
                *ISSUE719_SIFRAH_POPUP_STATIC_GAP_EVIDENCE,
                f"residual bucket: {bucket}",
            ]
        if bucket == "sifrah_popup_unused_base_game_runtime":
            return "covered_by_owner_route", ISSUE719_SIFRAH_POPUP_UNUSED_BASE_GAME_EVIDENCE
        return "runtime_required", ISSUE719_RESIDUAL_SIFRAH_ROUTE_SPLIT_RUNTIME_EVIDENCE
    if "Sifrah" in family["family_id"] and closure_lane == "combat_message_frame_does":
        return "runtime_required", ISSUE719_RESIDUAL_SIFRAH_ROUTE_SPLIT_RUNTIME_EVIDENCE
    return None


def _issue747_journal_quest_evidence_for(family: TextConstructionFamily) -> list[str] | None:
    family_id = family["family_id"]
    if family_id not in ISSUE747_JOURNAL_QUEST_REVIEWED_FAMILY_IDS:
        return None
    surfaces = set(family["surface_counts"])
    route_evidence = (
        ISSUE747_JOURNAL_QUEST_MIXED_SURFACE_EVIDENCE
        if surfaces & {"Popup", "AddPlayerMessage", "EmitMessage"}
        else ISSUE747_JOURNAL_QUEST_ROUTE_EVIDENCE
    )
    return [
        f"Issue #747 reviewed journal/quest static family: {family_id}",
        f"Issue #747 reviewed surfaces: {', '.join(sorted(surfaces))}",
        *route_evidence,
    ]


def _issue747_skill_evidence_for(family: TextConstructionFamily) -> list[str] | None:
    family_id = family["family_id"]
    if family_id not in ISSUE747_SKILL_REVIEWED_FAMILY_IDS:
        return None

    surfaces = set(family["surface_counts"])
    exact_evidence = ISSUE747_SKILL_EXACT_FAMILY_EVIDENCE.get(family_id)
    if exact_evidence is not None:
        return [
            f"Issue #747 reviewed surfaces: {', '.join(sorted(surfaces))}",
            *exact_evidence,
        ]

    owner_evidence = next(
        (
            evidence
            for markers, evidence in ISSUE747_SKILL_MARKER_ROUTE_EVIDENCE
            if any(marker in family_id for marker in markers)
        ),
        ISSUE747_SKILL_MESSAGE_FRAME_ROUTE_EVIDENCE,
    )
    route_evidence = _issue747_skill_surface_evidence(surfaces, owner_evidence)

    return [
        f"Issue #747 reviewed skill-originated static family: {family_id}",
        f"Issue #747 reviewed surfaces: {', '.join(sorted(surfaces))}",
        *route_evidence,
    ]


def _issue747_skill_surface_evidence(surfaces: set[str], owner_evidence: list[str]) -> list[str]:
    if surfaces & {"Popup", "TutorialManagerPopup"}:
        if surfaces & {"AddPlayerMessage", "Does", "EmitMessage", "MessageFrame"}:
            return _combine_evidence(owner_evidence, ISSUE747_SKILL_MESSAGE_FRAME_ROUTE_EVIDENCE)
        return list(owner_evidence)
    return _combine_evidence(owner_evidence, ISSUE747_SKILL_MESSAGE_FRAME_ROUTE_EVIDENCE)


def _combine_evidence(*evidence_groups: list[str]) -> list[str]:
    return list(dict.fromkeys(evidence for group in evidence_groups for evidence in group))


def _is_conversation_choice_tag_family_id(family_id: str) -> bool:
    return family_id.startswith("XRL.World.Conversations.Parts/") and family_id.endswith(
        ".HandleEvent(GetChoiceTagEvent)"
    )


def _closure_lane(family: TextConstructionFamily, classified: ClassifiedSurface) -> ClosureLane:
    surfaces = set(classified["player_visible_surfaces"]) | set(classified["contextual_surfaces"])
    file_path = family["file"]

    if surfaces & {"MessageFrame", "Does"}:
        lane: ClosureLane = "combat_message_frame_does"
    elif surfaces & {"ConversationChoiceTag", "ConversationTextAppend", "ConversationTextReplace"}:
        lane = "conversation_routes"
    elif "HistoricStringExpander" in surfaces:
        lane = "history_generated_text"
    elif surfaces & {"SetText", "DirectTextAssignment"} and _has_prefix(file_path, UI_OWNER_FILE_PREFIXES):
        lane = "screen_ui_direct_text"
    elif surfaces & {"DisplayNameAssignment", "DisplayNameReturn", "DisplayTextReturn", "GetDisplayName"}:
        lane = "display_name_composition"
    elif surfaces & {"Description", "DescriptionAssignment", "DescriptionReturn", "EffectDescriptionReturn"}:
        lane = "description_effect_detail"
    elif "JournalAPI" in surfaces:
        lane = "journal_quest_routes"
    elif "ActivatedAbility" in surfaces:
        lane = "activated_ability_names"
    elif surfaces & {"AddPlayerMessage", "EmitMessage", "Popup", "TutorialManagerPopup"}:
        lane = "producer_message_popup"
    else:
        lane = "other_owner_candidate"
    return lane


def _is_issue719_active_effect_description_family(
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> bool:
    if (
        closure_lane == "description_effect_detail"
        and family["file"] == "XRL.World/Effect.cs"
        and family["member_name"] == "GetDetails"
        and "EffectDescriptionReturn" in family["surface_counts"]
    ):
        return True
    return (
        closure_lane == "description_effect_detail"
        and family["file"].startswith("XRL.World.Effects/")
        and family["member_name"] in {"GetDescription", "GetDetails"}
        and "EffectDescriptionReturn" in family["surface_counts"]
    )


def _is_issue719_active_effect_display_name_family(
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> bool:
    return (
        closure_lane == "display_name_composition"
        and "DisplayNameAssignment" in family["surface_counts"]
        and family["family_id"] in _active_effect_display_name_classifications()
    )


@lru_cache(maxsize=1)
def _active_effect_display_name_classifications() -> dict[str, str]:
    inventory_path = Path(__file__).resolve().parents[1] / "docs" / "active-effect-producer-inventory.json"
    with inventory_path.open(encoding="utf-8") as handle:
        inventory = json.load(handle)
    classifications: dict[str, str] = {}
    for item in inventory.get("items", []):
        surface_counts = item.get("surface_counts", {})
        if not isinstance(surface_counts, dict) or "DisplayNameAssignment" not in surface_counts:
            continue
        family_id = item.get("family_id")
        classification = item.get("classification")
        if isinstance(family_id, str) and isinstance(classification, str):
            classifications[family_id] = classification
    return classifications


def _is_issue719_mutation_description_family(
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> bool:
    return (
        closure_lane == "description_effect_detail"
        and family["file"].startswith("XRL.World.Parts.Mutation/")
        and family["member_name"] == "GetDescription"
        and "EffectDescriptionReturn" in family["surface_counts"]
    )


def _is_issue719_world_mod_description_family(
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> bool:
    return (
        closure_lane == "description_effect_detail"
        and family["file"].startswith("XRL.World.Parts/Mod")
        and family["member_name"] == "GetDescription"
        and "EffectDescriptionReturn" in family["surface_counts"]
    )


def _issue719_static_producer_owner_evidence_for(
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> list[str] | None:
    if not _is_issue719_producer_message_family_audit_candidate(family, closure_lane):
        return None
    return ISSUE719_STATIC_PRODUCER_OWNER_EVIDENCE_BY_KEY.get(
        (family["file"], family["type_name"], family["member_name"])
    )


def _is_issue719_producer_message_family_audit_candidate(
    family: TextConstructionFamily,
    closure_lane: ClosureLane,
) -> bool:
    if closure_lane not in {"producer_message_popup", "combat_message_frame_does"}:
        return False

    family_id = family["family_id"]
    source_file = family["file"]
    if source_file.startswith("JoppaTutorial/"):
        return False
    if "Sifrah" in family_id:
        return False
    if _is_broad_producer_message_family(family_id):
        return False
    if source_file.startswith("XRL.World.Effects/"):
        return False
    return not any(name in family_id for name in ("Firefighting.", "ElementalJelly.", "Panhumor.", "Harvestable."))


def _issue719_residual_status(
    residual_bucket: str,
    residual_disposition: ResidualDisposition,
) -> tuple[ClosureStatus, list[str]]:
    evidence = [
        "Issue #719 consolidated residual tracker",
        f"residual_bucket={residual_bucket}",
        f"residual_disposition={residual_disposition}",
    ]
    if residual_disposition == "runtime_evidence_required":
        return "runtime_required", [
            *evidence,
            "Static policy review cannot prove this route without runtime evidence.",
        ]
    if residual_disposition == "covered_by_existing_route":
        return "covered_by_owner_route", [
            *evidence,
            "Existing owner-route or data-route evidence already covers this row.",
        ]
    return "action_required", [
        *evidence,
        "Exact owner-route implementation, promotion evidence, or narrower split is still required.",
    ]


def _residual_bucket_for_entry(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    lane = entry["closure_lane"]
    handlers = {
        "activated_ability_names": _activated_ability_residual_bucket,
        "screen_ui_direct_text": _screen_ui_residual_bucket,
        "description_effect_detail": _description_residual_bucket,
        "display_name_composition": _display_name_residual_bucket,
        "producer_message_popup": _producer_message_residual_bucket,
        "combat_message_frame_does": _producer_message_residual_bucket,
        "conversation_routes": _conversation_residual_bucket,
        "history_generated_text": _history_generated_text_residual_bucket,
    }
    handler = handlers.get(lane)
    return handler(entry) if handler is not None else ("misc_route_split", "runtime_evidence_required")


def _conversation_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    if entry["source_file"] == "XRL.World.Conversations.Parts/InsertRandomBookLine.cs":
        return "conversation_book_line_data_covered", "covered_by_existing_route"
    return "misc_route_split", "runtime_evidence_required"


def _history_generated_text_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    if entry["source_file"] == "XRL.Language/TextFilters.cs":
        return "history_text_filter_speech_status_gap", "likely_implementation_gap"
    return "misc_route_split", "runtime_evidence_required"


def _activated_ability_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    if entry["source_file"].startswith("XRL.World.Parts/"):
        return "activated_ability_misc_provider_gap", "likely_implementation_gap"
    return "activated_ability_asset_bridge", "runtime_evidence_required"


def _screen_ui_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    if source_file.startswith("XRL.UI/Popup.cs"):
        return "ui_popup_sink_route_split", "runtime_evidence_required"
    if source_file.startswith(("Qud.UI/", "XRL.UI/")):
        return _qud_ui_screen_residual_bucket(entry)
    return "ui_direct_text_gap", "likely_implementation_gap"


def _qud_ui_screen_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:  # noqa: PLR0911
    source_file = entry["source_file"]
    if source_file in {
        "Qud.UI/SkillsAndPowersStatusScreen.cs",
        "Qud.UI/KeybindBox.cs",
    }:
        return "ui_screen_fixed_label_gap", "likely_implementation_gap"
    if source_file == "Qud.UI/LeftSideCategory.cs":
        return "ui_screen_left_side_category_gap", "likely_implementation_gap"
    if source_file == "Qud.UI/WorldGenerationScreen.cs":
        return "ui_screen_world_generation_runtime", "runtime_evidence_required"
    if source_file == "Qud.UI/PopupMessage.cs":
        return "ui_screen_popup_message_runtime", "runtime_evidence_required"
    if source_file.startswith("Qud.UI/Options"):
        return "ui_screen_options_control_runtime", "runtime_evidence_required"
    if source_file in {
        "Qud.UI/EquipmentLine.cs",
        "Qud.UI/InventoryLine.cs",
        "Qud.UI/TradeLine.cs",
        "Qud.UI/TradeScreen.cs",
        "Qud.UI/ProgressBar.cs",
        "Qud.UI/StatusBarStatBlock.cs",
    }:
        return _qud_ui_trade_inventory_screen_bucket(entry)
    return _qud_ui_data_bound_screen_bucket(entry), "runtime_evidence_required"


def _qud_ui_data_bound_screen_bucket(entry: SurfaceQueueEntry) -> str:
    buckets_by_source = {
        "Qud.UI/LeftSideCategory.cs": "ui_screen_left_side_category_runtime",
        "Qud.UI/ModManagerUI.cs": "ui_screen_mod_manager_back_button_runtime",
        "Qud.UI/Notification.cs": "ui_screen_notification_runtime",
        "Qud.UI/ConsoleWindow.cs": "ui_screen_console_input_runtime",
        "Qud.UI/CyberneticsTerminalRow.cs": "ui_screen_cybernetics_terminal_runtime",
        "Qud.UI/MissileWeaponAreaInfo.cs": "ui_screen_missile_weapon_status_runtime",
    }
    return buckets_by_source.get(entry["source_file"], "ui_screen_data_bound_runtime")


def _qud_ui_trade_inventory_screen_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    member_name = entry["member_name"]

    bucket_by_source = {
        "Qud.UI/InventoryLine.cs": "ui_screen_inventory_drag_numeric_runtime",
        "Qud.UI/ProgressBar.cs": "ui_screen_progress_numeric_runtime",
        "Qud.UI/StatusBarStatBlock.cs": "ui_screen_status_stat_runtime",
        "Qud.UI/TradeLine.cs": "ui_screen_trade_drag_numeric_runtime",
        "Qud.UI/TradeScreen.cs": "ui_screen_trade_highlight_runtime",
    }
    if (
        source_file in {"Qud.UI/EquipmentLine.cs", "Qud.UI/InventoryLine.cs"}
        and member_name == "UpdateHotkey"
    ):
        bucket = "ui_screen_hotkey_control_runtime"
    else:
        bucket = bucket_by_source.get(source_file, "ui_screen_trade_inventory_runtime")
    return bucket, "runtime_evidence_required"


def _description_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    family_id = entry["family_id"]
    source_file = entry["source_file"]
    surfaces = set(entry["player_visible_surfaces"]) | set(entry["contextual_surfaces"])

    if "Sifrah" in family_id:
        return _sifrah_description_residual_bucket(entry, surfaces)
    if "GasGeneration" in family_id:
        return "world_part_description_gap", "likely_implementation_gap"
    if source_file.startswith("XRL.World.Effects/"):
        return "active_effect_non_description_route_split", "child_issue_needed"
    if "EffectDescriptionReturn" in surfaces:
        return _effect_description_return_residual_bucket(entry)
    if "DescriptionAssignment" in surfaces:
        return _description_assignment_residual_bucket(entry)
    return "description_detail_route_split", "child_issue_needed"


def _sifrah_description_residual_bucket(
    entry: SurfaceQueueEntry,
    surfaces: set[str],
) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]

    if source_file in {
        "XRL.World/BeguilingSifrah.cs",
        "XRL.World/PsychicCombatSifrah.cs",
    }:
        return "sifrah_description_unused_base_game_runtime", "runtime_evidence_required"
    if "EffectDescriptionReturn" in surfaces or entry["member_name"] == "GetDescription":
        return "sifrah_description_token_getdescription_gap", "likely_implementation_gap"
    if "DescriptionAssignment" in surfaces:
        return "sifrah_description_token_dynamic_constructor_gap", "likely_implementation_gap"
    return "sifrah_description_route_split", "runtime_evidence_required"


def _effect_description_return_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    if entry["family_id"] == "XRL.World.Capabilities/AutoAct.cs::AutoAct.GetDescription(string,OngoingAction)":
        return "action_description_autoact_gap", "likely_implementation_gap"
    if source_file.startswith("XRL.World.Units/"):
        return "game_object_unit_description_runtime", "runtime_evidence_required"
    if source_file.startswith("XRL.World.Skills.Cooking/"):
        return "cooking_description_route_split", "child_issue_needed"
    if source_file.startswith("XRL.CharacterBuilds.Qud/"):
        return "chargen_cybernetics_description_runtime", "runtime_evidence_required"
    if source_file == "XRL/OngoingAction.cs" or source_file.startswith(
        ("XRL.World.Capabilities/", "XRL.World.AI.GoalHandlers/", "XRL.World.Tinkering/")
    ):
        return "action_description_runtime", "runtime_evidence_required"
    return "effect_description_route_split", "child_issue_needed"


def _description_assignment_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    if source_file.startswith(("Qud.UI/", "XRL.UI/")):
        if source_file.startswith("Qud.UI/Options"):
            return "ui_options_control_description_gap", "likely_implementation_gap"
        if source_file.startswith("Qud.UI/"):
            return "ui_menu_option_static_description_gap", "likely_implementation_gap"
        return "ui_description_assignment_runtime", "runtime_evidence_required"
    return "description_assignment_route_split", "child_issue_needed"


def _display_name_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    family_id = entry["family_id"]
    source_file = entry["source_file"]
    bucket = "generated_display_name_runtime"
    disposition: ResidualDisposition = "runtime_evidence_required"
    if any(name in family_id for name in ("ElementalJelly.SetupPod", "Panhumor.SetupPod")):
        bucket = "generated_display_name_gap"
        disposition = "likely_implementation_gap"
    elif any(name in family_id for name in ("WorldFactory.LoadWorldNode", "ZoneDisplayName")):
        bucket = "world_zone_display_name_runtime"
    elif any(name in family_id for name in ("Village", "Sultan", "Mural", "Signature")):
        bucket, disposition = _generated_display_name_child_residual_bucket(entry)
    elif source_file.startswith(("Qud.UI/", "XRL.UI/", "XRL.UI.", "XRL.CharacterBuilds.Qud.UI/")):
        bucket, disposition = _ui_display_name_residual_bucket(entry)
    elif source_file.startswith("XRL.World.Skills.Cooking/"):
        bucket, disposition = _cooking_display_name_residual_bucket(entry)
    elif source_file.startswith("XRL.World.Parts.Mutation/") or source_file == "XRL/MutationEntry.cs":
        bucket, disposition = _mutation_display_name_residual_bucket(entry)
    elif source_file.startswith("XRL.World.Parts/"):
        bucket, disposition = _world_part_display_name_residual_bucket(entry)
    elif source_file.startswith("XRL.World/") or source_file == "XRL.World/Effect.cs":
        bucket, disposition = _core_display_name_residual_bucket(entry)
    return bucket, disposition


def _mutation_display_name_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    buckets_by_source = {
        "XRL.World.Parts.Mutation/BaseMutation.cs": "generated_display_name_mutation_base_display_gap",
        "XRL/MutationEntry.cs": "generated_display_name_mutation_base_display_gap",
        "XRL.World.Parts.Mutation/TemporalFugue.cs": "generated_display_name_mutation_temporal_fugue_copy_gap",
        "XRL.World.Parts.Mutation/PhotosyntheticSkin.cs": "generated_display_name_mutation_stat_shift_gap",
        "XRL.World.Parts.Mutation/LightManipulation.cs": (
            "generated_display_name_mutation_light_manipulation_ability_gap"
        ),
        "XRL.World.Parts.Mutation/Metamorphed.cs": "generated_display_name_mutation_effect_display_gap",
    }
    implementation_members = {
        "GetDisplayName",
        "CreateFugueCopyOf",
        "CheckCamouflage",
        "SyncAbilityName",
        "Metamorphed",
    }
    if member_name in implementation_members:
        return buckets_by_source.get(source_file, "generated_display_name_mutation_route_split"), (
            "likely_implementation_gap"
        )
    return "generated_display_name_mutation_route_split", "runtime_evidence_required"


def _core_display_name_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.World/GameObject.cs" and member_name in {"Poss", "poss"}:
        return "generated_display_name_core_possessive_gap", "likely_implementation_gap"
    if source_file == "XRL.World/GetRunningBehaviorEvent.cs":
        return "generated_display_name_core_running_behavior_runtime", "runtime_evidence_required"
    if source_file in {"XRL.World/GameObjectFactory.cs", "XRL.World/ZoneManager.cs"}:
        return "generated_display_name_core_invalid_object_gap", "likely_implementation_gap"
    if source_file == "XRL.World/Faction.cs":
        return "generated_display_name_core_faction_covered", "covered_by_existing_route"
    if source_file in {"XRL.World/Effect.cs", "XRL.World/PointOfInterest.cs"}:
        return "generated_display_name_core_metadata_covered", "covered_by_existing_route"
    return "generated_display_name_core_runtime", "runtime_evidence_required"


def _ui_display_name_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    if source_file.startswith("XRL.UI.ObjectFinderContexts/"):
        return "generated_display_name_ui_object_finder_context_gap", "likely_implementation_gap"
    if source_file.startswith("XRL.UI.ObjectFinderSorters/"):
        return "generated_display_name_ui_object_finder_sorter_gap", "likely_implementation_gap"
    if source_file == "XRL.UI/CyberneticsScreenInstall.cs":
        return "generated_display_name_ui_cybernetics_install_gap", "likely_implementation_gap"
    return "generated_display_name_ui_runtime", "runtime_evidence_required"


def _cooking_display_name_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file != "XRL.World.Skills.Cooking/CookingRecipe.cs" and member_name == "GetDisplayName":
        return "generated_display_name_cooking_preset_recipe_gap", "likely_implementation_gap"
    return "generated_display_name_cooking_recipe_runtime", "runtime_evidence_required"


def _generated_display_name_child_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    type_name = entry["type_name"]
    member_name = entry["member_name"]
    bucket = "generated_display_name_child_issue"
    disposition: ResidualDisposition = "runtime_evidence_required"

    if "MuralController" in type_name:
        bucket = _mural_generated_display_name_bucket(entry)
        if bucket != "generated_display_name_mural_runtime":
            disposition = "likely_implementation_gap"
    elif member_name == "getQuestReward":
        bucket = "generated_display_name_village_dynamic_quest_reward_gap"
        disposition = "likely_implementation_gap"
    elif member_name == "CreateVillageFaction":
        bucket = "generated_display_name_village_faction_gap"
        disposition = "likely_implementation_gap"
    elif member_name == "generateSignatureDish":
        bucket = "generated_display_name_village_signature_dish_runtime"
    elif member_name == "generateSignatureItems":
        bucket = "generated_display_name_village_signature_item_gap"
        disposition = "likely_implementation_gap"
    elif source_file == "XRL.World.ZoneBuilders/VillageCoda.cs":
        bucket = "generated_display_name_sultan_entity_gap"
        disposition = "likely_implementation_gap"
    elif source_file == "XRL.World.Parts/PointedAsteriskBuilder.cs" and member_name == "AsteriskWish":
        bucket = "generated_display_name_world_part_wish_debug_gap"
        disposition = "likely_implementation_gap"
    return bucket, disposition


def _mural_generated_display_name_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if member_name == "blankMural" and source_file in {
        "XRL.World.Parts/PlayerMuralController.cs",
        "XRL.World.Parts/SultanMuralController.cs",
    }:
        return "generated_display_name_mural_blank_slate_gap"
    if source_file == "XRL.World.Parts/PlayerMuralController.cs" and member_name == "updatePlayerMural":
        return "generated_display_name_mural_player_event_gap"
    if source_file == "XRL.World.Parts/SultanMuralController.cs" and member_name == "updateHistoricMural":
        return "generated_display_name_mural_historic_event_gap"
    if source_file == "XRL.World.Parts/SultanMuralController.cs" and member_name == "ruinMural":
        return "generated_display_name_mural_ruined_historic_gap"
    return "generated_display_name_mural_runtime"


def _world_part_display_name_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    type_name = entry["type_name"]

    implementation_gap_buckets = {
        "XRL.World.Parts/BeyLahTerrain.cs": "generated_display_name_world_part_fixed_leaf_gap",
        "XRL.World.Parts/HydroponTerrain.cs": "generated_display_name_world_part_fixed_leaf_gap",
        "XRL.World.Parts/MoltingBasilisk.cs": "generated_display_name_world_part_fixed_leaf_gap",
        "XRL.World.Parts/Miner.cs": "generated_display_name_world_part_fixed_leaf_gap",
        "XRL.World.Parts/RocketSkates.cs": "generated_display_name_world_part_fixed_leaf_gap",
        "XRL.World.Parts/Yurtmat.cs": "generated_display_name_stat_shift_gap",
        "XRL.World.Parts/ModCoProcessor.cs": "generated_display_name_stat_shift_gap",
        "XRL.World.Parts/CyberneticsOnboardRecoilerImprinting.cs": (
            "generated_display_name_world_part_cybernetics_recoiler_gap"
        ),
        "XRL.World.Parts/CyberneticsSingleSkillsoft.cs": (
            "generated_display_name_world_part_cybernetics_skillsoft_gap"
        ),
        "XRL.World.Parts/CyberneticsTreeSkillsoft.cs": (
            "generated_display_name_world_part_cybernetics_skillsoft_gap"
        ),
    }
    if source_file in implementation_gap_buckets:
        return implementation_gap_buckets[source_file], "likely_implementation_gap"
    if type_name.startswith("Cybernetics"):
        return "generated_display_name_world_part_cybernetics_runtime", "runtime_evidence_required"
    if source_file in {
        "XRL.World.Parts/RandomFigurine.cs",
        "XRL.World.Parts/PetPhylactery.cs",
        "XRL.World.Parts/PointedAsteriskBuilder.cs",
        "XRL.World.Parts/RandomStatue.cs",
        "XRL.World.Parts/TombCultistTemplate.cs",
        "XRL.World.Parts/ModQuantumReverb.cs",
    }:
        return _world_part_generated_object_display_name_bucket(entry)
    if source_file == "XRL.World.Parts/PhaseSticky.cs":
        return "generated_display_name_world_part_item_mod_covered", "covered_by_existing_route"
    return "generated_display_name_world_part_route_split", "runtime_evidence_required"


def _world_part_generated_object_display_name_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    buckets_by_source: dict[str, tuple[str, ResidualDisposition]] = {
        "XRL.World.Parts/RandomFigurine.cs": (
            "generated_display_name_world_part_figurine_gap",
            "likely_implementation_gap",
        ),
        "XRL.World.Parts/PetPhylactery.cs": (
            "generated_display_name_world_part_pet_phylactery_gap",
            "likely_implementation_gap",
        ),
        "XRL.World.Parts/PointedAsteriskBuilder.cs": (
            "generated_display_name_world_part_wish_debug_gap",
            "likely_implementation_gap",
        ),
        "XRL.World.Parts/RandomStatue.cs": (
            "generated_display_name_world_part_statue_gap",
            "likely_implementation_gap",
        ),
        "XRL.World.Parts/TombCultistTemplate.cs": (
            "generated_display_name_world_part_tomb_cultist_gap",
            "likely_implementation_gap",
        ),
        "XRL.World.Parts/ModQuantumReverb.cs": (
            "generated_display_name_world_part_hologram_gap",
            "likely_implementation_gap",
        ),
    }
    return buckets_by_source.get(
        source_file,
        ("generated_display_name_world_part_generated_object_runtime", "runtime_evidence_required"),
    )


def _producer_message_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:  # noqa: PLR0911
    family_id = entry["family_id"]
    source_file = entry["source_file"]
    if family_id == "XRL/GameText.cs::GameText.RoughConvertSecondPersonToThirdPerson(string,GameObject)":
        return "producer_runtime_core_game_text_third_person_death_gap", "likely_implementation_gap"
    if family_id in ISSUE719_RESIDUAL_PRODUCER_RUNTIME_FAMILIES:
        return _producer_runtime_residual_bucket_with_disposition(entry)
    if source_file.startswith("JoppaTutorial/"):
        return _tutorial_popup_residual_bucket(entry)
    if "Sifrah" in family_id:
        return _sifrah_popup_residual_bucket(entry)
    if _is_broad_producer_message_family(family_id):
        return _producer_broad_message_residual_bucket(entry)
    if source_file.startswith("XRL.World.Effects/"):
        return _active_effect_message_residual_bucket(entry)
    if source_file in {"XRL.UI/OptionsUI.cs", "XRL.UI/CommandBindingManager.cs", "Qud.UI/OptionsScreen.cs"}:
        return _producer_runtime_residual_bucket_with_disposition(entry)
    if any(name in family_id for name in ("Firefighting.", "ElementalJelly.", "Panhumor.", "Harvestable.")):
        return _producer_runtime_residual_bucket_with_disposition(entry)
    return "producer_message_family_audit", "child_issue_needed"


def _sifrah_popup_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    return _sifrah_popup_residual_bucket_for_parts(
        source_file=entry["source_file"],
        member_name=entry["member_name"],
    )


def _tutorial_popup_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    member_name = entry["member_name"]
    if member_name == "LateUpdate":
        return "tutorial_lateupdate_popup_gap", "likely_implementation_gap"
    if member_name in {"AllowCommand", "AllowTargetPick", "AllowInventoryInteract"}:
        return "tutorial_command_guard_popup_gap", "likely_implementation_gap"
    if member_name == "BeforePlayerEnterCell":
        return "tutorial_cell_guard_popup_gap", "likely_implementation_gap"
    if member_name in {"BearSeen", "SnapjawSeen"}:
        return "tutorial_seen_popup_gap", "likely_implementation_gap"
    if member_name == "OnTrigger":
        return "tutorial_trigger_popup_gap", "likely_implementation_gap"
    return "tutorial_popup_runtime", "runtime_evidence_required"


def _sifrah_popup_residual_bucket_for_parts(
    *,
    source_file: str,
    member_name: str,
) -> tuple[str, ResidualDisposition]:
    bucket = "sifrah_popup_route_split"
    disposition: ResidualDisposition = "runtime_evidence_required"
    if source_file == "XRL.World/PsychicCombatSifrah.cs":
        bucket = "sifrah_popup_unused_base_game_runtime"
    elif source_file == "XRL.World.Parts/CyberneticsTerminal2.cs" and member_name == "HackingResultPartialSuccess":
        bucket = "sifrah_popup_hacking_partial_success_gap"
        disposition = "likely_implementation_gap"
    elif source_file == "XRL.World/SocialSifrahTokenSecret.cs" and member_name == "UseToken":
        bucket = "sifrah_popup_secret_use_token_gap"
        disposition = "likely_implementation_gap"
    elif member_name == "CheckOutOfOptions":
        bucket = "sifrah_popup_check_out_of_options_gap"
        disposition = "likely_implementation_gap"
    elif member_name.startswith("Result"):
        bucket = "sifrah_popup_result_owner_gap"
        disposition = "likely_implementation_gap"
    elif member_name == "CheckTokenUse":
        bucket = "sifrah_popup_token_check_use_gap"
        disposition = "likely_implementation_gap"
    return bucket, disposition


ISSUE719_PRODUCER_RUNTIME_IMPLEMENTATION_GAP_BUCKETS: Final[frozenset[str]] = frozenset(
    {
            "producer_runtime_world_part_disguise_popup_gap",
            "producer_runtime_world_part_defibrillator_gap",
            "producer_runtime_world_part_grip_recoil_popup_gap",
        "producer_runtime_world_part_ship_ark_popup_gap",
        "producer_runtime_world_part_tinkering_popup_gap",
        "producer_runtime_core_coda_endgame_popup_gap",
        "producer_runtime_core_mod_config_popup_gap",
        "producer_runtime_core_mod_failure_popup_gap",
        "producer_runtime_core_population_wish_find_blueprint_gap",
        "producer_runtime_core_scores_legacy_screen_gap",
        "producer_runtime_cybernetics_force_lathe_activation_gap",
        "producer_runtime_cybernetics_force_lathe_replace_gap",
        "producer_runtime_cybernetics_holographic_visage_gap",
        "producer_runtime_api_equipment_action_menu_gap",
        "producer_runtime_api_save_error_gap",
        "producer_runtime_capability_firefighting_gap",
        "producer_runtime_capability_item_naming_gap",
        "producer_runtime_capability_item_naming_wish_debug_gap",
        "producer_broad_gameobject_autoequip_gap",
        "producer_broad_gameobject_death_gap",
        "producer_broad_gameobject_hostile_spot_gap",
        "producer_runtime_core_game_text_third_person_death_gap",
        "producer_runtime_ui_chargen_build_library_add_gap",
        "producer_runtime_ui_chargen_build_library_import_gap",
        "producer_runtime_ui_chargen_build_library_manage_gap",
        "producer_runtime_ui_chargen_build_summary_gap",
        "producer_runtime_ui_chargen_gender_customize_gap",
        "producer_runtime_ui_chargen_mutation_menu_gap",
        "producer_runtime_ui_chargen_validation_popup_gap",
        "producer_runtime_ui_options_command_binding_gap",
        "producer_runtime_ui_options_legacy_popup_gap",
        "producer_runtime_ui_equipment_slot_gap",
        "producer_runtime_ui_object_finder_filters_gap",
        "producer_runtime_ui_trade_vendor_actions_gap",
        "producer_runtime_ui_factions_status_sort_gap",
        "producer_runtime_ui_inventory_status_options_gap",
        "producer_runtime_mutation_base_variant_popup_gap",
        "producer_runtime_mutation_carapace_loosen_gap",
        "producer_runtime_mutation_domination_failure_gap",
        "producer_runtime_mutation_temporal_fugue_gap",
        "producer_runtime_inventory_action_crayons_popup_gap",
        "producer_runtime_inventory_action_desalination_pellet_gap",
        "producer_runtime_inventory_action_description_look_popup_gap",
        "producer_runtime_inventory_action_grenade_detonate_popup_gap",
        "producer_runtime_inventory_action_inventory_drop_popup_gap",
        "producer_runtime_inventory_action_vehicle_follower_popup_gap",
        "producer_runtime_inventory_action_examiner_popup_gap",
        "producer_runtime_inventory_action_fixit_spray_popup_gap",
        "producer_runtime_inventory_action_magnetized_applicator_popup_gap",
        "producer_runtime_inventory_action_tinker_item_popup_gap",
        "producer_runtime_conversation_api_reward_pick_gap",
        "producer_runtime_conversation_endgame_confirm_gap",
        "producer_runtime_conversation_give_artifact_gap",
        "producer_runtime_conversation_resheph_secret_gap",
        "producer_runtime_conversation_water_ritual_secret_gap",
        "producer_broad_gameobject_destroy_gap",
        "producer_broad_gameobject_explode_death_gap",
        "producer_broad_gameobject_inventory_companion_gap",
        "producer_broad_gameobject_pulldown_gap",
        "producer_runtime_liquid_glitch_components_gap",
        "producer_runtime_liquid_wish_warm_effect_gap",
        "producer_runtime_quest_reward_choice_gap",
        "producer_runtime_world_part_heat_self_frame_gap",
        "producer_runtime_world_part_liquid_cleaning_frame_gap",
        "producer_runtime_world_part_liquid_contact_frame_gap",
        "producer_runtime_world_part_magazine_supply_gap",
        "producer_runtime_world_part_nephal_absorb_frame_gap",
        "producer_runtime_world_part_pet_recipe_frame_gap",
        "producer_runtime_world_part_pet_taunt_frame_gap",
        "producer_runtime_world_part_pseudopod_death_frame_gap",
        "producer_runtime_world_part_shuttle_frame_gap",
        "producer_runtime_world_part_vortex_periodic_frame_gap",
        "producer_runtime_world_part_interior_damage_queue_gap",
        "producer_runtime_world_part_player_dance_ritual_queue_gap",
        "producer_runtime_world_part_biome_distribution_queue_popup_gap",
        "producer_runtime_world_part_campfire_extinguish_gap",
        "producer_runtime_world_part_chat_emit_gap",
        "producer_runtime_world_part_elevator_switch_queue_popup_gap",
        "producer_runtime_world_part_fungal_cure_emit_gap",
        "producer_runtime_world_part_golem_mound_popup_gap",
        "producer_runtime_world_part_harvestable_attempt_gap",
        "producer_runtime_world_part_stomach_water_queue_popup_gap",
        "producer_runtime_world_part_vehicle_infiltration_emit_gap",
        "producer_runtime_world_part_vehicle_infiltration_popup_gap",
        "producer_runtime_world_part_vortex_apply_gap",
        "producer_runtime_world_part_wish_debug_popup_gap",
    }
)

ISSUE719_PRODUCER_RUNTIME_STATIC_GAP_EVIDENCE: Final[list[str]] = [
    (
        "Issue #719 runtime reclassification promotes this static owner shape to "
        "likely_implementation_gap because the decompiled producer has an exact "
        "owner method with fixed player-visible text and route-local generated captures."
    ),
    (
        "Generated labels, object names, pronouns, or options may still need route-local "
        "capture translation, but the row no longer needs live runtime evidence to "
        "identify its owner."
    ),
]


def _producer_runtime_family_bucket_with_disposition(
    family: TextConstructionFamily,
) -> tuple[str, ResidualDisposition]:
    classified = classify_family(family)
    entry: SurfaceQueueEntry = {
        "classification": classified["classification"],
        "closure_lane": _closure_lane(family, classified),
        "closure_status": "runtime_required",
        "closure_evidence": [],
        "family_id": family["family_id"],
        "source_file": family["file"],
        "type_name": family["type_name"],
        "member_name": family["member_name"],
        "member_signature": family["member_signature"],
        "member_start_line": family["member_start_line"],
        "text_construction_count": family["text_construction_count"],
        "player_visible_surfaces": classified["player_visible_surfaces"],
        "contextual_surfaces": classified["contextual_surfaces"],
        "construction_only_surfaces": classified["construction_only_surfaces"],
        "non_target_surfaces": classified["non_target_surfaces"],
        "reason": classified["reason"],
        "action": classified["action"],
        "first_lines": family["first_lines"],
    }
    return _producer_runtime_residual_bucket_with_disposition(entry)


def _producer_runtime_residual_bucket_with_disposition(
    entry: SurfaceQueueEntry,
) -> tuple[str, ResidualDisposition]:
    bucket = _producer_runtime_residual_bucket(entry)
    disposition: ResidualDisposition = (
        "likely_implementation_gap"
        if bucket in ISSUE719_PRODUCER_RUNTIME_IMPLEMENTATION_GAP_BUCKETS
        else "runtime_evidence_required"
    )
    return bucket, disposition


def _producer_runtime_residual_bucket(entry: SurfaceQueueEntry) -> str:  # noqa: C901, PLR0911, PLR0912
    source_file = entry["source_file"]
    family_id = entry["family_id"]
    if source_file.startswith("XRL.World.Parts/") and "InventoryActionEvent" in family_id:
        return _producer_runtime_inventory_action_surface_bucket(entry)
    if source_file.startswith("XRL.World.Parts/") and "Cybernetics" in source_file:
        return _producer_runtime_cybernetics_surface_bucket(entry)
    if source_file.startswith("XRL.World.Capabilities/"):
        return _producer_runtime_capability_bucket(entry)
    if source_file.startswith("XRL.Liquids/"):
        return _producer_runtime_liquid_bucket(entry)
    if source_file.startswith(("XRL.World.Quests/", "XRL.World.ZoneBuilders/", "XRL.World/DynamicQuest")):
        return _producer_runtime_quest_bucket(entry)
    if source_file.startswith(("XRL.World.Biomes/", "XRL.World.Parts/", "XRL.World.Tinkering/")):
        return _producer_runtime_world_part_surface_bucket(entry)
    ui_prefixes = ("Qud.UI/", "XRL.UI/", "XRL.UI.", "XRL.CharacterBuilds", "XRL.World/Gender.cs")
    if source_file.startswith(ui_prefixes):
        return _producer_runtime_ui_surface_bucket(entry)
    if source_file.startswith(("Extensions.cs", "SoundManager.cs", "XRL.Core/", "XRL.Messages/", "XRL/")):
        return _producer_runtime_core_system_bucket(entry)
    if source_file.startswith(("XRL.World.Conversations", "Qud.API/ConversationsAPI.cs")):
        return _producer_runtime_conversation_bucket(entry)
    if source_file.startswith("Qud.API/"):
        return _producer_runtime_api_bucket(entry)
    if source_file.startswith("XRL.World.Parts.Mutation/"):
        return _producer_runtime_mutation_surface_bucket(entry)
    return "producer_runtime_evidence_required"


def _producer_runtime_api_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "Qud.API/EquipmentAPI.cs" and member_name == "ShowInventoryActionMenu":
        return "producer_runtime_api_equipment_action_menu_gap"
    if source_file == "Qud.API/SavesAPI.cs" and member_name == "FatalSaveError":
        return "producer_runtime_api_save_error_gap"
    if source_file == "Qud.API/JournalAPI.cs" and member_name == "WishGospelAccomplishments":
        return "producer_runtime_api_journal_wish_gospel_runtime"
    return "producer_runtime_api_route_split"


def _producer_runtime_capability_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.World.Capabilities/Firefighting.cs":
        return "producer_runtime_capability_firefighting_gap"
    if source_file == "XRL.World.Capabilities/ItemNaming.cs" and member_name == "NameItem":
        return "producer_runtime_capability_item_naming_gap"
    if source_file == "XRL.World.Capabilities/ItemNaming.cs" and member_name == "HandleItemNamingWish":
        return "producer_runtime_capability_item_naming_wish_debug_gap"
    return "producer_runtime_capability_route_split"


def _producer_runtime_liquid_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.Liquids/LiquidWarmStatic.cs" and member_name == "GlitchLiquidComponents":
        return "producer_runtime_liquid_glitch_components_gap"
    if source_file == "XRL.Liquids/LiquidWarmStatic.cs" and member_name in {
        "WishWarmEffect",
        "WishWarmEffectSpec",
    }:
        return "producer_runtime_liquid_wish_warm_effect_gap"
    return "producer_runtime_liquid_route_split"


def _producer_runtime_quest_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if (
        source_file == "XRL.World/DynamicQuestRewardElement_ChoiceFromPopulation.cs"
        and member_name == "award"
    ):
        return "producer_runtime_quest_reward_choice_gap"
    if (
        source_file == "XRL.World.ZoneBuilders/FindASiteDynamicQuestManager.cs"
        and member_name == "DynamicQuestWhere"
    ):
        return "producer_runtime_quest_find_site_wish_debug_gap"
    return "producer_runtime_quest_route_split"


def _producer_runtime_conversation_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    buckets_by_source = {
        "XRL.World.Conversations.Parts/GiveReshephSecret.cs": (
            "producer_runtime_conversation_resheph_secret_gap"
        ),
        "XRL.World.Conversations.Parts/EndGame.cs": "producer_runtime_conversation_endgame_confirm_gap",
        "XRL.World.Conversations.Parts/GiveArtifact.cs": "producer_runtime_conversation_give_artifact_gap",
        "XRL.World.Conversations.Parts/WaterRitualSellSecret.cs": (
            "producer_runtime_conversation_water_ritual_secret_gap"
        ),
    }
    if source_file == "Qud.API/ConversationsAPI.cs" and member_name == "chooseOneItem":
        return "producer_runtime_conversation_api_reward_pick_gap"
    return buckets_by_source.get(source_file, "producer_runtime_conversation_route_split")


def _producer_runtime_mutation_surface_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    buckets_by_source = {
        "XRL.World.Parts.Mutation/SunderMind.cs": "producer_runtime_mutation_sunder_mind_gap",
        "XRL.World.Parts.Mutation/Domination.cs": "producer_runtime_mutation_domination_failure_gap",
        "XRL.World.Parts.Mutation/TemporalFugue.cs": "producer_runtime_mutation_temporal_fugue_gap",
        "XRL.World.Parts.Mutation/Carapace.cs": "producer_runtime_mutation_carapace_loosen_gap",
        "XRL.World.Parts.Mutation/BaseMutation.cs": "producer_runtime_mutation_base_variant_popup_gap",
        "XRL.World.Parts.Mutation/Wings.cs": "producer_runtime_mutation_wings_flight_gap",
    }
    implementation_members = {"Tick", "ProcessTarget", "PerformTemporalFugue", "Loosen", "SelectVariant", "HandleEvent"}
    if member_name in implementation_members:
        return buckets_by_source.get(source_file, "producer_runtime_mutation_route_split")
    return "producer_runtime_mutation_route_split"


def _producer_runtime_cybernetics_surface_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    source_buckets = {
        "XRL.World.Parts/CyberneticsButcherableCybernetic.cs": "producer_runtime_cybernetics_butcher_message_gap",
        "XRL.World.Parts/CyberneticsHolographicVisage.cs": "producer_runtime_cybernetics_holographic_visage_gap",
        "XRL.World.Parts/CyberneticsCathedra.cs": "producer_runtime_cybernetics_cathedra_flight_popup_gap",
        "XRL.World.Parts/CyberneticsOnboardRecoilerTeleporter.cs": "producer_runtime_cybernetics_recoiler_popup_gap",
    }
    precision_force_lathe_buckets = {
        "ActivatePrecisionForceLathe": "producer_runtime_cybernetics_force_lathe_activation_gap",
        "HandleEvent": "producer_runtime_cybernetics_force_lathe_replace_gap",
    }
    terminal_buckets = {
        "AskLowLevelHack": "producer_runtime_cybernetics_low_level_hack_popup_gap",
        "AttemptInterface": "producer_runtime_cybernetics_terminal_interface_gap",
    }
    if source_file == "XRL.World.Parts/CyberneticsPrecisionForceLathe.cs":
        return precision_force_lathe_buckets.get(member_name, "producer_runtime_cybernetics_route_split")
    if source_file == "XRL.World.Parts/CyberneticsTerminal2.cs":
        return terminal_buckets.get(member_name, "producer_runtime_cybernetics_route_split")
    return source_buckets.get(source_file, "producer_runtime_cybernetics_route_split")


def _producer_broad_message_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    bucket = "producer_broad_route_split"

    if source_file == "XRL.World.Parts/MissileWeapon.cs":
        bucket = "producer_broad_missile_trajectory_message_runtime"
    elif source_file == "XRL.World/GameObject.cs":
        bucket = _producer_broad_gameobject_bucket(member_name)

    disposition: ResidualDisposition = (
        "likely_implementation_gap"
        if bucket in ISSUE719_PRODUCER_RUNTIME_IMPLEMENTATION_GAP_BUCKETS
        else "runtime_evidence_required"
    )
    return bucket, disposition


def _producer_broad_gameobject_bucket(member_name: str) -> str:
    buckets_by_member = {
        "ArePerceptibleHostilesNearby": "producer_broad_gameobject_hostile_spot_gap",
        "AutoEquip": "producer_broad_gameobject_autoequip_gap",
        "Destroy": "producer_broad_gameobject_destroy_gap",
        "Die": "producer_broad_gameobject_death_gap",
        "Explode": "producer_broad_gameobject_explode_death_gap",
        "FireEvent": "producer_broad_gameobject_regenera_runtime",
        "HandleInventoryActionEvent": "producer_broad_gameobject_inventory_companion_gap",
        "PerformReplaceCell": "producer_broad_gameobject_replace_cell_gap",
        "PullDown": "producer_broad_gameobject_pulldown_gap",
    }
    return buckets_by_member.get(member_name, "producer_broad_route_split")


def _producer_runtime_core_system_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    bucket = "producer_runtime_core_system_route_split"

    if source_file == "XRL.Core/Scores.cs":
        bucket = "producer_runtime_core_scores_legacy_screen_gap"
    elif (
        source_file == "XRL/GameText.cs"
        and entry["member_name"] == "RoughConvertSecondPersonToThirdPerson"
    ):
        bucket = "producer_runtime_core_game_text_third_person_death_gap"
    elif source_file == "XRL.Core/XRLCore.cs":
        bucket = "producer_runtime_core_mod_config_popup_gap"
    elif source_file == "XRL/PopulationManager.cs":
        bucket = _producer_runtime_population_manager_bucket(entry["member_name"])
    elif source_file == "XRL/ModInfo.cs":
        bucket = "producer_runtime_core_mod_failure_popup_gap"
    elif source_file == "XRL/CodaSystem.cs":
        bucket = "producer_runtime_core_coda_endgame_popup_gap"
    elif source_file == "SoundManager.cs":
        bucket = "producer_runtime_core_sound_debug_queue_runtime"
    elif source_file in {"Extensions.cs", "XRL.Messages/MessageQueue.cs"}:
        bucket = "producer_runtime_core_generic_sink_runtime"
    return bucket


def _producer_runtime_population_manager_bucket(member_name: str) -> str:
    if member_name == "WishFindBlueprint":
        return "producer_runtime_core_population_wish_find_blueprint_gap"
    if member_name == "RollOneFrom":
        return "producer_runtime_core_population_roll_one_error_gap"
    return "producer_runtime_core_population_wish_popup_runtime"


def _producer_runtime_inventory_action_surface_bucket(entry: SurfaceQueueEntry) -> str:
    surfaces = set(entry["player_visible_surfaces"])
    if surfaces == {"Popup"}:
        return _producer_runtime_inventory_action_popup_bucket(entry)
    if surfaces == {"Does", "Popup"}:
        return _producer_runtime_inventory_action_does_popup_bucket(entry)
    if surfaces == {"MessageFrame", "Popup"}:
        return "producer_runtime_inventory_action_message_frame_popup_route_split"
    if surfaces == {"EmitMessage"}:
        if entry["source_file"] == "XRL.World.Parts/DesalinationPellet.cs":
            return "producer_runtime_inventory_action_desalination_pellet_gap"
        return "producer_runtime_inventory_action_emit_route_split"
    return "producer_runtime_inventory_action_route_split"


def _producer_runtime_inventory_action_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    buckets_by_source_file = {
        "XRL.World.Parts/Crayons.cs": "producer_runtime_inventory_action_crayons_popup_gap",
        "XRL.World.Parts/Description.cs": "producer_runtime_inventory_action_description_look_popup_gap",
        "XRL.World.Parts/IGrenade.cs": "producer_runtime_inventory_action_grenade_detonate_popup_gap",
        "XRL.World.Parts/Inventory.cs": "producer_runtime_inventory_action_inventory_drop_popup_gap",
        "XRL.World.Parts/Vehicle.cs": "producer_runtime_inventory_action_vehicle_follower_popup_gap",
    }
    return buckets_by_source_file.get(source_file, "producer_runtime_inventory_action_popup_route_split")


def _producer_runtime_inventory_action_does_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    buckets_by_source_file = {
        "XRL.World.Parts/Examiner.cs": "producer_runtime_inventory_action_examiner_popup_gap",
        "XRL.World.Parts/TinkerItem.cs": "producer_runtime_inventory_action_tinker_item_popup_gap",
        "XRL.World.Parts/FixitSpray.cs": "producer_runtime_inventory_action_fixit_spray_popup_gap",
        "XRL.World.Parts/MagnetizedApplicator.cs": (
            "producer_runtime_inventory_action_magnetized_applicator_popup_gap"
        ),
    }
    return buckets_by_source_file.get(source_file, "producer_runtime_inventory_action_does_popup_route_split")


def _producer_runtime_ui_surface_bucket(entry: SurfaceQueueEntry) -> str:  # noqa: PLR0911
    source_file = entry["source_file"]
    type_name = entry["type_name"]
    member_name = entry["member_name"]
    if source_file == "XRL.UI/FadeText.cs":
        return "producer_runtime_ui_tutorial_popup_route_split"
    if source_file == "Qud.UI/ModManagerUI.cs" and member_name == "OnCancel":
        return "producer_runtime_ui_mod_manager_cancel_gap"
    if source_file == "XRL.UI.Framework/FrameworkSearchInput.cs" and member_name == "ChangeValue":
        return "producer_runtime_ui_framework_search_input_gap"
    if source_file.startswith("XRL.CharacterBuilds") or source_file == "XRL.World/Gender.cs":
        return _producer_runtime_ui_chargen_popup_bucket(entry)
    if type_name in {"OptionsUI", "OptionsScreen", "CommandBindingManager"}:
        return _producer_runtime_ui_options_popup_bucket(entry)
    if type_name in {"TradeUI", "ObjectFinder", "EquipmentScreen"}:
        return _producer_runtime_ui_inventory_trade_popup_bucket(entry)
    if type_name in {"FactionsStatusScreen", "InventoryAndEquipmentStatusScreen", "AbilityManagerScreen"}:
        return _producer_runtime_ui_status_popup_bucket(entry)
    return "producer_runtime_ui_misc_popup_route_split"


def _producer_runtime_ui_status_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "Qud.UI/FactionsStatusScreen.cs" and member_name == "HandleCmdOptions":
        return "producer_runtime_ui_factions_status_sort_gap"
    if source_file == "Qud.UI/InventoryAndEquipmentStatusScreen.cs" and member_name == "HandleShowOptions":
        return "producer_runtime_ui_inventory_status_options_gap"
    if source_file == "Qud.UI/AbilityManagerScreen.cs" and member_name == "showScreen":
        return "producer_runtime_ui_ability_manager_empty_gap"
    return "producer_runtime_ui_status_popup_route_split"


def _producer_runtime_ui_inventory_trade_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.UI/TradeUI.cs" and member_name == "ShowVendorActions":
        return "producer_runtime_ui_trade_vendor_actions_gap"
    if source_file == "XRL.UI/ObjectFinder.cs" and member_name == "ConfigFilters":
        return "producer_runtime_ui_object_finder_filters_gap"
    if source_file == "XRL.UI/EquipmentScreen.cs" and member_name == "ShowBodypartEquipUI":
        return "producer_runtime_ui_equipment_slot_gap"
    return "producer_runtime_ui_inventory_trade_popup_route_split"


def _producer_runtime_ui_options_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.UI/OptionsUI.cs" and member_name == "Show":
        return "producer_runtime_ui_options_legacy_popup_gap"
    if source_file == "XRL.UI/CommandBindingManager.cs" and member_name == "RestoreDefaults":
        return "producer_runtime_ui_options_command_binding_gap"
    if source_file == "Qud.UI/OptionsScreen.cs" and member_name == "HandleMenuOption":
        return "producer_runtime_ui_options_help_popup_gap"
    return "producer_runtime_ui_options_popup_route_split"


def _producer_runtime_ui_chargen_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.World/Gender.cs":
        return "producer_runtime_ui_chargen_gender_customize_gap"
    if source_file == "XRL.CharacterBuilds/EmbarkBuilder.cs":
        return "producer_runtime_ui_chargen_validation_popup_gap"
    if source_file == "XRL.CharacterBuilds.Qud.UI/QudBuildSummaryModuleWindow.cs":
        return "producer_runtime_ui_chargen_build_summary_gap"
    if source_file == "XRL.CharacterBuilds.Qud.UI/QudMutationsModuleWindow.cs":
        mutation_buckets = {
            "HandleMenuOption": "producer_runtime_ui_chargen_mutation_menu_gap",
            "SelectVariant": "producer_runtime_ui_chargen_mutation_variant_gap",
        }
        return mutation_buckets.get(member_name, "producer_runtime_ui_chargen_popup_route_split")
    if source_file == "XRL.CharacterBuilds.Qud.UI/QudBuildLibraryModuleWindow.cs":
        build_library_buckets = {
            "HandleMenuOption": "producer_runtime_ui_chargen_build_library_manage_gap",
            "AddBuild": "producer_runtime_ui_chargen_build_library_add_gap",
            "onSelect": "producer_runtime_ui_chargen_build_library_import_gap",
        }
        return build_library_buckets.get(member_name, "producer_runtime_ui_chargen_popup_route_split")
    return "producer_runtime_ui_chargen_popup_route_split"


def _producer_runtime_world_part_surface_bucket(entry: SurfaceQueueEntry) -> str:  # noqa: C901, PLR0911, PLR0912
    surfaces = set(entry["player_visible_surfaces"])
    if surfaces == {"MessageFrame"}:
        return _producer_runtime_world_part_message_frame_bucket(entry)
    if surfaces == {"Popup"}:
        return _producer_runtime_world_part_popup_surface_bucket(entry)
    if surfaces == {"AddPlayerMessage"}:
        return _producer_runtime_world_part_queue_bucket(entry)
    if surfaces == {"Does", "EmitMessage"}:
        return _producer_runtime_world_part_does_emit_bucket(entry)
    if surfaces == {"Does", "MessageFrame"}:
        return _producer_runtime_world_part_does_message_frame_bucket(entry)
    if surfaces == {"Does", "EmitMessage", "MessageFrame"}:
        return _producer_runtime_world_part_does_emit_message_frame_bucket(entry)
    if surfaces == {"MessageFrame", "Popup"}:
        return _producer_runtime_world_part_popup_message_frame_bucket(entry)
    if surfaces == {"EmitMessage", "Popup"}:
        return _producer_runtime_world_part_emit_popup_bucket(entry)
    if surfaces == {"EmitMessage", "MessageFrame", "Popup"}:
        return _producer_runtime_world_part_emit_message_frame_popup_bucket(entry)
    if surfaces == {"AddPlayerMessage", "Popup"}:
        return _producer_runtime_world_part_queue_popup_bucket(entry)
    if (
        surfaces == {"Does", "Popup"}
        and entry["source_file"] == "XRL.World.Parts/VehicleMeleeInfiltration.cs"
        and entry["member_name"] == "TryInfiltrate"
    ):
        return "producer_runtime_world_part_vehicle_infiltration_popup_gap"
    mixed_surface_buckets = {
        frozenset({"AddPlayerMessage", "Does"}): "producer_runtime_world_part_queue_does_route_split",
        frozenset({"Does"}): "producer_runtime_world_part_does_route_split",
        frozenset({"Does", "Popup"}): "producer_runtime_world_part_does_popup_route_split",
    }
    if bucket := mixed_surface_buckets.get(frozenset(surfaces)):
        return bucket
    return "producer_runtime_world_part_mixed_route_split"


def _producer_runtime_world_part_popup_message_frame_bucket(entry: SurfaceQueueEntry) -> str:
    if (
        entry["source_file"] == "XRL.World.Parts/MagazineAmmoLoader.cs"
        and entry["member_name"] == "FireEvent"
    ):
        return "producer_runtime_world_part_magazine_supply_gap"
    return "producer_runtime_world_part_popup_message_frame_route_split"


def _producer_runtime_world_part_emit_popup_bucket(entry: SurfaceQueueEntry) -> str:
    if (
        entry["source_file"] == "XRL.World.Parts/ShevaStarshipControl.cs"
        and entry["member_name"] == "CheckTimer"
    ):
        return "producer_runtime_world_part_ship_ark_popup_gap"
    return "producer_runtime_world_part_emit_popup_route_split"


def _producer_runtime_world_part_emit_message_frame_popup_bucket(
    entry: SurfaceQueueEntry,
) -> str:
    if entry["source_file"] == "XRL.World.Parts/SpaceTimeVortex.cs" and entry["member_name"] == "ApplyVortex":
        return "producer_runtime_world_part_vortex_apply_gap"
    return "producer_runtime_world_part_emit_message_frame_popup_route_split"


def _producer_runtime_world_part_does_message_frame_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.World.Parts/AutomatedExternalDefibrillator.cs" and member_name == "AttemptDefibrillate":
        return "producer_runtime_world_part_defibrillator_gap"
    return "producer_runtime_world_part_does_message_frame_route_split"


def _producer_runtime_world_part_does_emit_message_frame_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.World.Parts/Harvestable.cs" and member_name == "AttemptHarvest":
        return "producer_runtime_world_part_harvestable_attempt_gap"
    if source_file == "XRL.World.Parts/Campfire.cs" and member_name == "Extinguish":
        return "producer_runtime_world_part_campfire_extinguish_gap"
    return "producer_runtime_world_part_does_emit_message_frame_route_split"


def _producer_runtime_world_part_does_emit_bucket(entry: SurfaceQueueEntry) -> str:
    buckets_by_source_file = {
        "XRL.World.Parts/Chat.cs": "producer_runtime_world_part_chat_emit_gap",
        "XRL.World.Parts/FungalInfection.cs": "producer_runtime_world_part_fungal_cure_emit_gap",
        "XRL.World.Parts/VehicleMeleeInfiltration.cs": "producer_runtime_world_part_vehicle_infiltration_emit_gap",
    }
    return buckets_by_source_file.get(entry["source_file"], "producer_runtime_world_part_does_emit_route_split")


def _producer_runtime_world_part_queue_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    if source_file == "XRL.World.Parts/DanceRitualOpponent.cs" and member_name == "HandleEvent":
        return "producer_runtime_world_part_dance_opponent_debug_queue_gap"
    if source_file == "XRL.World.Parts/DanceRitualOpponent.cs" and member_name == "Register":
        return "producer_runtime_world_part_dance_opponent_register_queue_gap"
    if source_file == "XRL.World.Parts/PlayerDanceRitual.cs":
        return "producer_runtime_world_part_player_dance_ritual_queue_gap"
    if source_file == "XRL.World.Parts/Interior.cs":
        return "producer_runtime_world_part_interior_damage_queue_gap"
    return "producer_runtime_world_part_queue_route_split"


def _producer_runtime_world_part_queue_popup_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    buckets_by_source_file = {
        "XRL.World.Parts/Stomach.cs": "producer_runtime_world_part_stomach_water_queue_popup_gap",
        "XRL.World.Parts/ElevatorSwitch.cs": "producer_runtime_world_part_elevator_switch_queue_popup_gap",
        "XRL.World.Biomes/BiomeManager.cs": "producer_runtime_world_part_biome_distribution_queue_popup_gap",
        "XRL.World.Parts/GiantClamProperties.cs": (
            "producer_runtime_world_part_giant_clam_dimension_queue_popup_gap"
        ),
    }
    return buckets_by_source_file.get(source_file, "producer_runtime_world_part_queue_popup_route_split")


def _producer_runtime_world_part_message_frame_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    source_buckets = {
        "XRL.World.Parts/AIBarathrumShuttle.cs": "producer_runtime_world_part_shuttle_frame_gap",
        "XRL.World.Parts/ElementalJelly.cs": "producer_runtime_world_part_pseudopod_death_frame_gap",
        "XRL.World.Parts/HeatSelfOnFreeze.cs": "producer_runtime_world_part_heat_self_frame_gap",
        "XRL.World.Parts/Panhumor.cs": "producer_runtime_world_part_pseudopod_death_frame_gap",
        "XRL.World.Parts/PetEbenshabat.cs": "producer_runtime_world_part_pet_recipe_frame_gap",
        "XRL.World.Parts/PetFrondzie.cs": "producer_runtime_world_part_pet_taunt_frame_gap",
    }
    member_buckets = {
        ("XRL.World.Parts/LiquidVolume.cs", "CleaningMessage"): (
            "producer_runtime_world_part_liquid_cleaning_frame_gap"
        ),
        ("XRL.World.Parts/LiquidVolume.cs", "ProcessContact"): (
            "producer_runtime_world_part_liquid_contact_frame_gap"
        ),
        ("XRL.World.Parts/NephalProperties.cs", "AbsorbChords"): (
            "producer_runtime_world_part_nephal_absorb_frame_gap"
        ),
        ("XRL.World.Parts/SpaceTimeVortex.cs", "SpaceTimeAnomalyPeriodicEvents"): (
            "producer_runtime_world_part_vortex_periodic_frame_gap"
        ),
    }
    return member_buckets.get(
        (source_file, member_name),
        source_buckets.get(source_file, "producer_runtime_world_part_message_frame_route_split"),
    )


def _producer_runtime_world_part_popup_surface_bucket(entry: SurfaceQueueEntry) -> str:
    source_file = entry["source_file"]
    member_name = entry["member_name"]
    family_id = entry["family_id"]
    bucket = "producer_runtime_world_part_popup_route_split"

    if source_file == "XRL.World.Tinkering/TinkeringHelpers.cs":
        bucket = "producer_runtime_world_part_tinkering_popup_gap"
    elif source_file == "XRL.World.Parts/Shrine.cs":
        bucket = "producer_runtime_world_part_shrine_popup_gap"
    elif source_file == "XRL.World.Parts/ModDisguise.cs":
        bucket = "producer_runtime_world_part_disguise_popup_gap"
    elif source_file in {"XRL.World.Parts/ShevaStarshipControl.cs", "XRL.World.Parts/ArkCore.cs"}:
        bucket = "producer_runtime_world_part_ship_ark_popup_gap"
    elif source_file == "XRL.World.Parts/GripChange.cs" or "RecoilAbility." in family_id:
        bucket = "producer_runtime_world_part_grip_recoil_popup_gap"
    elif source_file == "XRL.World.Parts/GolemQuestMound.cs":
        bucket = "producer_runtime_world_part_golem_mound_popup_gap"
    elif source_file == "XRL.World.Parts/Physics.cs":
        bucket = "producer_runtime_world_part_movement_popup_runtime"
    elif member_name.startswith("Wish") or source_file == "XRL.World.Parts/ModExtradimensional.cs":
        bucket = "producer_runtime_world_part_wish_debug_popup_gap"
    return bucket


def _active_effect_message_residual_bucket(entry: SurfaceQueueEntry) -> tuple[str, ResidualDisposition]:
    surfaces = set(entry["player_visible_surfaces"]) | set(entry["contextual_surfaces"])
    if (
        entry["source_file"] == "XRL.World.Effects/FungalSporeInfection.cs"
        and entry["member_name"] == "ChooseLimbForInfection"
    ):
        return "active_effect_fungal_spore_infection_popup_gap", "likely_implementation_gap"
    if surfaces & {"MessageFrame", "Does"}:
        return "active_effect_message_frame_route_split", "runtime_evidence_required"
    if surfaces & {"Popup", "TutorialManagerPopup"}:
        return "active_effect_popup_route_split", "runtime_evidence_required"
    if surfaces & {"AddPlayerMessage", "EmitMessage"}:
        return "active_effect_queue_route_split", "runtime_evidence_required"
    return "active_effect_misc_route_split", "runtime_evidence_required"


def _is_broad_producer_message_family(family_id: str) -> bool:
    broad_family_markers = (
        "GameObject.",
        "MissileWeapon.CalculateBulletTrajectory",
        "GameObject.Die",
    )
    return any(name in family_id for name in broad_family_markers)


def _is_player_visible_owner_candidate(family: TextConstructionFamily) -> bool:
    file_path = family["file"]
    if file_path.startswith(NON_PLAYER_FILE_NAMES) or _is_low_value_source_file(file_path):
        return False
    if _has_prefix(file_path, UI_OWNER_FILE_PREFIXES):
        return True
    if _has_prefix(file_path, GAMEPLAY_OWNER_FILE_PREFIXES):
        return _member_name_suggests_visible_text(family["member_name"]) or _has_semantic_assignment_surface(family)
    return False


def _has_semantic_assignment_surface(family: TextConstructionFamily) -> bool:
    surfaces = set(family["surface_counts"])
    return bool(surfaces & {"DescriptionAssignment", "DisplayNameAssignment"})


def _member_name_suggests_visible_text(member_name: str) -> bool:
    visible_terms = (
        "Description",
        "DisplayName",
        "DisplayText",
        "GetDetails",
        "GetTabString",
        "Render",
        "SetData",
        "Show",
        "UpdateView",
    )
    return any(term in member_name for term in visible_terms)


def _filter_entries(entries: list[SurfaceQueueEntry], include: str) -> list[SurfaceQueueEntry]:
    match include:
        case "valuable":
            return [entry for entry in entries if entry["classification"] in VALUABLE_CLASSIFICATIONS]
        case "needs-work":
            return [
                entry
                for entry in entries
                if entry["classification"] in VALUABLE_CLASSIFICATIONS
                and entry["closure_status"] in ACTIONABLE_CLOSURE_STATUSES
            ]
        case "unreviewed":
            return [
                entry
                for entry in entries
                if entry["classification"] in VALUABLE_CLASSIFICATIONS and entry["closure_status"] == "unreviewed"
            ]
        case "candidate-only":
            return [entry for entry in entries if entry["classification"] == "candidate_only"]
        case "non-target":
            return [entry for entry in entries if entry["classification"] == "non_target"]
        case "all":
            return entries
        case _:
            msg = f"unsupported include mode: {include}"
            raise ValueError(msg)


def _has_prefix(value: str, prefixes: tuple[str, ...]) -> bool:
    return any(value.startswith(prefix) for prefix in prefixes)


def _is_low_value_source_file(file_path: str) -> bool:
    return _has_prefix(file_path, NON_PLAYER_FILE_PREFIXES) or any(part in file_path for part in LOW_VALUE_FILE_PARTS)


def _format_counter(counter: dict[str, int]) -> str:
    return ",".join(f"{key}:{counter[key]}" for key in sorted(counter))


if __name__ == "__main__":
    raise SystemExit(main())
