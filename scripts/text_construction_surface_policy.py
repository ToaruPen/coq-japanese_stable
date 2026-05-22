"""Classify Roslyn text-construction surfaces by localization value."""

from __future__ import annotations

import json
import sys
from argparse import ArgumentParser
from pathlib import Path
from typing import Final, Literal, TypedDict, cast

Classification = Literal[
    "player_visible_api",
    "player_visible_owner_candidate",
    "candidate_only",
    "non_target",
]
OutputFormat = Literal["text", "json", "lanes-json"]
ClosureStatus = Literal[
    "action_required",
    "covered_by_owner_route",
    "partial_coverage",
    "runtime_required",
    "likely_true_gap",
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
    "XRL.World.Parts.Skill/Cudgel_Slam.cs::"
    "Cudgel_Slam.Cast(GameObject,Cudgel_Slam,string,GameObject,bool,int,string)"
)
SHIELD_SLAM_SLAM_FAMILY_ID: Final = (
    "XRL.World.Parts.Skill/Shield_Slam.cs::"
    "Shield_Slam.Slam(GameObject,GameObject,Cell,bool)"
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
    (
        "Shield_Slam.Slam owner message-log traffic covers the source-backed "
        "shield slam possessive capture"
    ),
]
ISSUE747_SKILL_MESSAGE_FRAME_ROUTE_EVIDENCE: Final = [
    "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
    "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
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
    (
        "Issue #747 source-backed single-callsite skill popups are translated by "
        "owner keys before generic popup sinks"
    ),
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
TEXT_CONSTRUCTION_CLOSURE_OVERLAY: Final[dict[str, ClosureOverlayEntry]] = {
    "XRL.World.Conversations.Parts/WaterRitual.cs::WaterRitual.HandleEvent(DisplayTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": CONVERSATION_BODY_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/MoundContext.cs::MoundContext.HandleEvent(PrepareTextEvent)": {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": CONVERSATION_BODY_EVIDENCE,
    },
    "XRL.World.Conversations.Parts/QuestSignpost.cs::QuestSignpost.HandleEvent(PrepareTextEvent)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            QUEST_SIGNPOST_PARTIAL_EVIDENCE,
        ],
    },
    (
        "XRL.World.Conversations.Parts/WaterRitualTinkeringRecipe.cs::"
        "WaterRitualTinkeringRecipe.HandleEvent(PrepareTextEvent)"
    ): {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            TINKERING_RECIPE_PARTIAL_EVIDENCE,
        ],
    },
    (
        "XRL.World.Conversations.Parts/WaterRitualHermitOath.cs::"
        "WaterRitualHermitOath.HandleEvent(PrepareTextEvent)"
    ): {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            HERMIT_OATH_PARTIAL_EVIDENCE,
        ],
    },
    (
        "XRL.World.Conversations.Parts/WaterRitualLearnSkill.cs::"
        "WaterRitualLearnSkill.HandleEvent(PrepareTextEvent)"
    ): {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            *CONVERSATION_BODY_EVIDENCE,
            LEARN_SKILL_PARTIAL_EVIDENCE,
        ],
    },
    "XRL.World.Conversations.Parts/KithAndKinExclusion.cs::KithAndKinExclusion.HandleEvent(PrepareTextEvent)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            "thief name replacement is a Kith-and-Kin game-state/display-name route, not a static producer route",
        ],
    },
    "XRL.World.Conversations.Parts/KithAndKinMotive.cs::KithAndKinMotive.HandleEvent(PrepareTextEvent)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            "circumstance influence replacement is a Kith-and-Kin clue/game-state route, not a static producer route",
        ],
    },
    "XRL.World.Conversations.Parts/GlotrotFilter.cs::GlotrotFilter.HandleEvent(PrepareTextEvent)": {
        "closure_status": "runtime_required",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            "Glotrot intentionally rewrites text into disease speech at runtime",
        ],
    },
    (
        "XRL.World.Conversations.Parts/InsertRandomBookLine.cs::"
        "InsertRandomBookLine.HandleEvent(PrepareTextEvent)"
    ): {
        "closure_status": "runtime_required",
        "closure_evidence": [
            "docs/reports/2026-05-16-issue-719-conversation-text-construction-routes.md",
            "inserted book lines must be verified through book/data localization runtime evidence",
        ],
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryFireEventTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.FireEvent(Event)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
            "docs/reports/2026-05-15-issue-699-static-producer-message-candidates.md",
        ],
    },
    "XRL.UI/TradeUI.cs::TradeUI.ShowTradeScreen(GameObject,float,TradeScreenMode)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiPopupTranslationPatchTests.cs",
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/WorldPartsFragmentTranslatorTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "XRL.World.Parts/Tonic.cs::Tonic.HandleEvent(InventoryActionEvent)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/TonicTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerQueueTranslationPatchTests.cs",
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
    (
        "XRL.World/Reputation.cs::Reputation.Modify("
        "Faction,int,string,StringBuilder,string,bool,bool,bool,bool)"
    ): {
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
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE726_TEXT_FILTER_ROUTE_EVIDENCE,
    },
    "XRL.Language/TextFilters.cs::TextFilters.Lallated(string,string)": {
        "closure_status": "runtime_required",
        "closure_evidence": ISSUE726_TEXT_FILTER_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateSpindleNegotiationRelic("
        "string,string,string,string,int)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_RELIC_ROUTE_EVIDENCE,
    },
    (
        "XRL.World/RelicGenerator.cs::RelicGenerator.SelectElement("
        "GameObject,GameObject,GameObject,GameObject)"
    ): {
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
    (
        "XRL.Annals/QudHistoryFactory.cs::"
        "QudHistoryFactory.NameRuinsSite(History,out bool,out string)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_QUD_HISTORY_FACTORY_GENERATED_NAME_ROUTE_EVIDENCE,
    },
    (
        "XRL.Annals/QudHistoryFactory.cs::"
        "QudHistoryFactory.GenerateCultName(HistoricEntity,History)"
    ): {
        "closure_status": "covered_by_owner_route",
        "closure_evidence": HSE_QUD_HISTORY_FACTORY_GENERATED_NAME_ROUTE_EVIDENCE,
    },
    (
        "XRL.World.Parts/LocateRelicQuestManager.cs::"
        "LocateRelicQuestManagerSystem.CheckCompleted(GameObject)"
    ): {
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
    (
        "XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestManager.cs::"
        "System.FinishEntry(QuestEntry,GameObject)"
    ): {
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
    (
        "XRL.World.Skills.Cooking/CookingRecipe.cs::"
        "CookingRecipe.GenerateRecipeName(List<string>,List<string>,string)"
    ): {
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
        "XRL.World/RelicGenerator.cs::RelicGenerator.GenerateRelicName("
        "string,HistoricEntitySnapshot,string,out string)"
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            (
                "Issue #762 covers the generated element display/rules text via "
                "BestowElement; the base cherub description route remains split."
            ),
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerReplaceDescriptionPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerReplaceDescriptionPatchTests.cs",
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerHandleEventTranslationPatch.cs",
            "Mods/QudJP/Assemblies/src/Patches/CherubimSpawnerBestowElementTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CherubimSpawnerGeneratedTextTranslationPatchTests.cs",
        ],
    },
    "XRL.World.Parts/SultanShrine.cs::SultanShrine.ShrineInitialize()": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/SultanShrineWrapperTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SultanShrineWrapperTranslatorTests.cs",
        ],
    },
    "XRL.UI/StatusScreen.cs::StatusScreen.Show(GameObject)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs",
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/TinkeringScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
        ],
    },
    "XRL.UI/InventoryScreen.cs::InventoryScreen.Show(GameObject)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/InventoryScreenTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/LegacyGamepadPromptTranslationPatchTests.cs",
        ],
    },
    "XRL.UI/AbilityManager.cs::AbilityManager.Show(XRL.World.GameObject)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/AbilityManagerShowTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
        ],
    },
    "Qud.UI/TinkeringDetailsLine.cs::TinkeringDetailsLine.setData(FrameworkDataElement)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/TinkeringDetailsLineTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringTranslationPatchTests.cs",
            "docs/reports/2026-05-15-static-uncovered-coverage-triage.md",
        ],
    },
    "XRL.World/PsychicCombatSifrah.cs::PsychicCombatSifrah.PsychicCombatSifrah(GameObject,string,int,int,string)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/SifrahPureOwnerPopupTranslationPatchTests.cs",
        ],
    },
    "XRL.World.Parts.Mutation/MultiHorns.cs::MultiHorns.Mutate(GameObject,int)": {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs",
        ],
    },
    (
        "XRL.World.Parts/MissileWeapon.cs::MissileWeapon.ShowPicker("
        "int,int,bool,AllowVis,int,bool,GameObject,ref FireType,int)"
    ): {
        "closure_status": "partial_coverage",
        "closure_evidence": [
            "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/UITextSkinTranslationPatchTests.cs",
        ],
    },
    "XRL.UI/OptionsUI.cs::OptionsUI.Show()": {
        "closure_status": "runtime_required",
        "closure_evidence": [
            "docs/reports/2026-05-15-issue-576-static-producer-runtime-deferrals.md",
            "Mods/QudJP/Localization/Dictionaries/ui-options.ja.json",
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            (
                "Issue #762 covers the generated activated ability display name "
                "`Tinker Turret [N remaining]`; remaining prompt/failure branches "
                "need separate route review."
            ),
            "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityBarButtonTextTranslationPatchTests.cs",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
    },
    "XRL.Core/XRLCore.cs::XRLCore._Start()": {
        "closure_status": "likely_true_gap",
        "closure_evidence": [
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            (
                "Issue #762 covers the known finite EvilTwin/HexCrystal/"
                "EngulfingClones generated display-name prefixes and runtime "
                "short descriptions; arbitrary caller-supplied Prefix, Message, "
                "and MessageForActor values remain split from this route proof."
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            (
                "Issue #762 covers the successful bandage/staunch MessageFrame "
                "shapes; failure prompt and phase/stasis branches remain split "
                "for focused owner-route review."
            ),
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
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
        "closure_status": "partial_coverage",
        "closure_evidence": [
            (
                "Issue #762 covers the `charge`, `stomp with bestial fury`, and "
                "`stopped in its tracks by` MessageFrame shapes; damage/shoved "
                "side effects remain split from this route proof."
            ),
            "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs",
            "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
            "docs/reports/2026-05-15-static-uncovered-coverage-triage.md",
            "docs/reports/2026-05-16-issue-711-text-construction-separation.md",
        ],
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


class ClassifiedSurface(TypedDict):
    """Internal classification result."""

    classification: Classification
    player_visible_surfaces: list[str]
    contextual_surfaces: list[str]
    construction_only_surfaces: list[str]
    non_target_surfaces: list[str]
    reason: str
    action: str


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
    first_lines = [
        call["line"]
        for call in representative_calls
        if isinstance(call, dict) and isinstance(call.get("line"), int)
    ] if isinstance(representative_calls, list) else []
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
            else sum(surface_counts.values()) if isinstance(surface_counts, dict) else 0
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
    return [
        entry
        for entry in build_surface_queue(inventory)
        if entry["classification"] in VALUABLE_CLASSIFICATIONS
    ]


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
            entry["player_visible_surfaces"]
            or entry["contextual_surfaces"]
            or entry["construction_only_surfaces"]
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
    _ = parser.add_argument("--format", choices=("text", "json", "lanes-json"), default="text")
    _ = parser.add_argument(
        "--include",
        choices=("valuable", "all", "candidate-only", "non-target"),
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

    text = format_surface_queue(inventory, inventory_path=inventory_path, include=include, limit=limit)
    _ = sys.stdout.write(text + "\n")
    return 0


def _queue_entry(family: TextConstructionFamily) -> SurfaceQueueEntry:
    classified = classify_family(family)
    closure_lane = _closure_lane(family, classified)
    closure_status, closure_evidence = _closure_overlay(family, closure_lane)
    return {
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


def _closure_overlay(family: TextConstructionFamily, closure_lane: ClosureLane) -> tuple[ClosureStatus, list[str]]:
    family_id = family["family_id"]
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

    if overlay is None:
        if _is_conversation_choice_tag_family_id(family_id):
            return "covered_by_owner_route", list(CONVERSATION_CHOICE_TAG_EVIDENCE)
        return "action_required", []
    return overlay["closure_status"], list(overlay["closure_evidence"])


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
    return (
        family_id.startswith("XRL.World.Conversations.Parts/")
        and family_id.endswith(".HandleEvent(GetChoiceTagEvent)")
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
