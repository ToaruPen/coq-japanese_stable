using System.Text.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorTagAllowlistCoverageTests
{
    private static readonly Regex FileScopedNamespacePattern =
        new("^namespace\\s+.+?;$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MethodSignaturePattern =
        new(
            "^\\s*(?:private|internal|public|protected|static|sealed|override|async|unsafe|extern|readonly|virtual|new|partial|\\s)+[^=;]*?\\b(?<name>[A-Za-z_]\\w*)\\s*\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StripDeconstructionPattern =
        new(
            "\\bvar\\s*\\(\\s*(?:[A-Za-z_]\\w*|_)\\s*,\\s*(?<spans>[A-Za-z_]\\w*|_)\\s*\\)\\s*=\\s*(?:ColorAwareTranslationComposer|ColorCodePreserver)\\.Strip\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] RestoreRoundTripSymbols =
    {
        "ColorAwareTranslationComposer.Restore(",
        "ColorAwareTranslationComposer.RestoreRelative(",
        "ColorAwareTranslationComposer.RestoreCapture(",
        "ColorAwareTranslationComposer.MarkupAwareRestoreCapture(",
        "ColorAwareTranslationComposer.RestoreSlice(",
        "ColorAwareTranslationComposer.RestoreMatchBoundaries(",
        "ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(",
        "ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(",
        "ColorAwareTranslationComposer.TranslatePreservingColors(",
        "GetDisplayNameRouteTranslator.TranslatePreservingColors(",
        "UITextSkinTranslationPatch.TranslatePreservingColors(",
        "RestoreBalancedCapture(",
        "RestoreCaptureAtOffset(",
    };

    private static readonly string[] StripRoundTripSymbols =
    {
        "ColorAwareTranslationComposer.Strip(",
        "ColorCodePreserver.Strip(",
    };

    private static readonly string[] NameLikeCaptureGroups =
    {
        "killer",
        "item",
        "name",
        "owner",
        "subject",
        "target",
    };

    private static readonly string NameLikeCaptureAlternation = string.Join("|", NameLikeCaptureGroups);

    private static readonly Regex DirectNameLikeRestoreCapturePattern =
        new(
            "ColorAwareTranslationComposer\\.RestoreCapture\\((?<value>.*?),\\s*[^,]+,\\s*(?<group>[^;]*?\\.Groups\\[\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\])",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AliasAssignmentPattern =
        new(
            "\\b(?:var|Group)\\s+(?<alias>[A-Za-z_]\\w*)\\s*=\\s*[^;]*?\\.Groups\\[\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\]",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AliasedNameLikeRestoreCapturePattern =
        new(
            "ColorAwareTranslationComposer\\.RestoreCapture\\((?<value>.*?),\\s*[^,]+,\\s*(?<alias>[A-Za-z_]\\w*)\\s*\\)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SourceHelperNameLikeRestoreCapturePattern =
        new(
            "\\bRestoreCapture\\(\\s*match\\s*,\\s*spans\\s*,\\s*\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\s*\\)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SourceCaptureValuePattern =
        new(
            "^(?:[^;]*?\\.Groups\\[\"(?<name>"
            + NameLikeCaptureAlternation
            + ")\"\\]\\.Value|(?<alias>[A-Za-z_]\\w*))$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] NameLikeRestoreCaptureGuardSymbols =
    {
        "HasColorMarkup(",
        "IsAlreadyLocalized",
        "MarkupAwareRestoreCapture(",
    };

    private static readonly SortedDictionary<string, string> StripWithoutLocalRestoreAllowlist =
        new(StringComparer.Ordinal)
        {
            // Keys identify the ColorAwareTranslationComposer.Strip call site, not the containing method declaration.
            ["Mods/QudJP/Assemblies/src/Observability/ColorShapeCaptureObservability.cs:144:Capture"] = "Audited Strip call: observation-only shape capture, color ownership is not modified.",
            ["Mods/QudJP/Assemblies/src/Observability/ColorShapeCaptureObservability.cs:145:Capture"] = "Audited Strip call: observation-only shape capture, color ownership is not modified.",
            ["Mods/QudJP/Assemblies/src/Observability/FinalOutputObservability.cs:107:RecordDirectMarker"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs:264:HasColorMarkup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs:113:TryTranslatePopupMessage"] = "Audited Strip call: popup target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs:134:TryTranslateQueuedMessage"] = "Audited Strip call: queued target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityCooldownTranslator.cs:30:TryTranslateRawCooldown"] = "Audited Strip call: cooldown duration and ability colors are restored through RestoreCapture and TranslatePreservingColors.",
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:156:TryTranslateExact"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:195:TryTranslateTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/AnimateObjectTranslationPatch.cs:109:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BasePronounProviderCustomizePopupTranslationPatch.cs:108:TryTranslateCustomizePopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BedChairFragmentTranslator.cs:120:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BeginBeingUnequippedFailureMessageTranslationPatch.cs:65:TryTranslateFailureMessage"] = "Audited Strip call: item capture colors are restored through MarkupAwareRestoreCapture before display-name owner-route translation.",
            ["Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs:105:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BrainBrineCurseTranslationPatch.cs:93:TryTranslatePopupMessage"] = "Audited Strip call: name colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/CampfireNostrumsTranslationPatch.cs:163:TryTranslatePopupMessage"] = "Audited Strip call: generated target/condition captures are restored through the local MarkupAwareRestoreCapture helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs:194:TranslatePreservedLine"] = "Audited Strip call: preserve source, serving, and result captures are restored through the local Restore helper before owner-route translation.",
            ["Mods/QudJP/Assemblies/src/Patches/CampfireRemainsAttemptLightTranslationPatch.cs:84:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs:538:TryTranslateRuntimeMutationRankSection"] = "Audited Strip call: runtime mutation rank captures restore range, damage, cooldown, and temperature values through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/ClonelingVehicleFragmentTranslator.cs:56:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CloningStartBuddedCloneTranslationPatch.cs:116:TryTranslateDetachMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CombatTextSurfaceTranslationPatch.cs:143:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs:179:TryTranslateBasicActionMarker"] = "Audited Strip call: action marker color ownership is restored through RestoreTranslatedActionMarker.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs:249:TryTranslateRequireReputationMarker"] = "Audited Strip call: action marker color ownership is restored through RestoreTranslatedActionMarker and captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs:279:TryTranslateAddSlynthCandidateMarker"] = "Audited Strip call: action marker color ownership is restored through RestoreTranslatedActionMarker and captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs:300:TryTranslateWaterRitualChoiceMarker"] = "Audited Strip call: action marker color ownership is restored through RestoreTranslatedActionMarker and captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationRewardPopupTranslationPatch.cs:132:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationScriptPopupTranslationPatch.cs:160:TryTranslateCore"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CookingEffectFragmentTranslator.cs:447:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CookingMealDescriptionTranslator.cs:46:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after ingredient captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/CookingRuntimeTranslationPatch.cs:330:TryTranslateWellFedPopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CyberneticsPrecisionForceLatheTranslationPatch.cs:143:TryTranslateCore"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CyberneticsStasisEntanglerTranslationPatch.cs:103:TryTranslateDeployMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DamagePenetrationDebugTranslationPatch.cs:101:TryTranslateDebugMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs:125:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs:98:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:262:TryTranslateSplitHistoryHeaderStartLine"] = "Audited Strip call: split history header colors are restored by preserving source boundary tokens across the following line.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:466:TryTranslateSplitHistoryHeaderContinuationLine"] = "Audited Strip call: split history continuation is predicate-only before appending the localized suffix after the source color boundary.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:557:TryFindDanglingBoundaryOpening"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:613:HasColorBoundaryOpening"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:648:HasColorBoundaryClosing"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:775:TryTranslateSultanShrineWrapperPreservingColors"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:883:TryTranslateRegainsChargeWhenWornOrHeldLine"] = "Audited Strip call: whole-line color wrappers are restored through RestoreWholeLineBoundaryWrappers after the fixed description template is reconstructed.",
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs:163:TryTranslateReverseEngineerPrompt"] = "Audited Strip call: color ownership is restored through MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs:183:TryTranslateStartDisassemblingMessage"] = "Audited Strip call: color ownership is restored through MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs:203:TryTranslateDisassembleReceiptMessage"] = "Audited Strip call: disassembled item, build target, mod target, and bit captures are restored before owner-route translation.",
            ["Mods/QudJP/Assemblies/src/Patches/DynamicQuestConversationTextTranslator.cs:74:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after generated quest conversation captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/DynamicQuestExplicitConversationTextTranslator.cs:70:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after explicit dynamic quest conversation captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/DynamicQuestGeneratedQuestTextTranslator.cs:104:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after generated quest/step captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/EaterCryptPlaqueTextTranslator.cs:96:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after finite crypt plaque fragment translation.",
            ["Mods/QudJP/Assemblies/src/Patches/EmboldenedTranslationPatch.cs:118:TryTranslateCore"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs:143:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs:68:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnergyCellSocketAccessPopupTranslationPatch.cs:87:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnergyLoaderCannotTakeTranslationPatch.cs:79:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EngraverTranslationPatch.cs:101:TryTranslateEngraveMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EngulfingTranslationPatch.cs:100:TryTranslateEngulfingMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs:188:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ImportedFoodOrDrinkFactionNameTranslator.cs:43:TryTranslate"] = "Audited Strip call: whole-source boundary wrappers are restored through RestoreWhole for translated generated faction-name frames.",
            ["Mods/QudJP/Assemblies/src/Patches/FactionsLineTranslationPatch.cs:70:TranslateTextField"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FactionsStatusScreenTranslationPatch.cs:878:AddLocalizedSearchFragment"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FirefightingTranslationPatch.cs:95:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FlightTranslationPatch.cs:102:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ForceBubbleOwnerTranslationPatch.cs:95:TryTranslateForceBubbleMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FriendOrFoeReasonTranslator.cs:62:TryTranslate"] = "Audited Strip call: whole-source boundary wrappers are restored through RestoreWhole for every translated friend-or-foe reason frame.",
            ["Mods/QudJP/Assemblies/src/Patches/FugueOnStepTranslationPatch.cs:99:TryTranslateStepMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs:147:TryTranslatePopupMessage"] = "Audited Strip call: companion ability and follow-distance menu captures are restored through MarkupAwareRestoreCapture, and whole-row wrappers are restored after option/state translation.",
            ["Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs:111:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedQueueDoesVerbTranslationPatch.cs:153:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedSubjectQueueTranslationPatch.cs:144:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:3125:TranslateDisplayNameWithClause"] = "Audited Strip call: with-clause colors are restored by source-markup-aware clause helpers after dictionary lookup.",
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:3159:TranslateDisplayNameWithClausePreservingSourceMarkup"] = "Audited Strip call: source-owned with-clause markup is restored through component-aware clause helpers after dictionary lookup.",
            ["Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:3923:TryLoadLocalizedBlueprintDisplayNameMarkup"] = "Audited Strip call: blueprint display-name markup is stripped only to build a visible-name lookup key; restored markup comes from the same DisplayName attribute.",
            ["Mods/QudJP/Assemblies/src/Patches/HiddenRenderTranslationPatch.cs:160:TryTranslateRevealMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/HookedOwnerTranslationPatch.cs:89:TryTranslateBreakFree"] = "Audited Strip call: subject and holder captures are restored through markup-aware helpers before the break-free frame is reconstructed.",
            ["Mods/QudJP/Assemblies/src/Patches/IExamineEventProcessIdentifyTranslationPatch.cs:85:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ITeleporterTranslationPatch.cs:131:TryTranslatePopupMessage"] = "Audited Strip call: generated subject/plane colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs:152:TryTranslateGraveyardZoneMessage"] = "Audited Strip call: owner colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs:167:TryTranslateContainerOwnershipPrompt"] = "Audited Strip call: container/item colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs:182:TryTranslateInventoryFailurePopup"] = "Audited Strip call: inventory popup colors are restored through capture and whole-source wrapper restoration helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs:226:GetPopupFamilyDetail"] = "Audited Strip call: predicate-only popup family classifier; color ownership is not modified.",
            ["Mods/QudJP/Assemblies/src/Patches/JournalNotificationTranslator.cs:20:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/KeyMappingUiTranslationPatch.cs:112:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/LevelerTranslationPatch.cs:113:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs:282:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs:116:TryTranslatePopupMessage"] = "Audited Strip call: popup target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs:136:TryTranslateQueuedMessage"] = "Audited Strip call: queued actor/target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs:135:TryTranslateCompanionRippedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs:151:TryTranslateRippedFromPlayerMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs:165:TryTranslatePulledTowardMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs:207:TranslateProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MapRevealPopupTranslationPatch.cs:139:TryTranslatePopupMessageForOwnerKey"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MentalShieldTranslationPatch.cs:91:TryTranslateMentalShieldMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:84:Prefix"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MissileWeaponHitTranslationPatch.cs:114:TryTranslateQueuedMessage"] = "Audited Strip call: predicate-only route gate; color ownership is preserved by MessageLogProducerTranslationHelpers.TryPreparePatternMessage.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationActionFailureTranslationPatch.cs:105:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs:127:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs:152:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationInfectionTranslationPatch.cs:84:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationsApiTranslationPatch.cs:90:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs:106:TranslateProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetEitherOrExplodeTranslationPatch.cs:131:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:139:TryTranslateAstralTether"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:153:TryTranslateWisdomReveal"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:167:TryTranslateStopGleaming"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:181:TryTranslateStartGleaming"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PickGameObjectScreenTranslationPatch.cs:115:TranslateProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs:148:TryTranslateMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs:169:TryTranslatePopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationHelpers.cs:107:TryTranslateFoodWaterPart"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PhysicsInventoryActionPopupTranslationPatch.cs:94:TryTranslatePopupMessage"] = "Audited Strip call: ownership-pour colors are restored by LiquidVolumeFragmentTranslator, cleaning-liquid captures use MarkupAwareRestoreCapture, and attack-confirm target colors are restored by PopupTranslationPatch.TryTranslatePhysicsAttackConfirmText.",
            ["Mods/QudJP/Assemblies/src/Patches/PhysicAmputateLimbTranslationPatch.cs:125:TryTranslatePopupMessage"] = "Audited Strip call: field-amputation popup captures are restored through RestoreCapture and whole-source boundary wrappers are restored through RestoreWhole.",
            ["Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs:260:TryTranslateDamageFrame"] = "Audited Strip call: damage-frame source and damage-type captures are restored through local MarkupAwareRestoreCapture helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslatedMessageHandoff.cs:215:CreateKey"] = "Audited Strip call: handoff key captures visible text and color shape only; it does not modify display ownership.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:2060:TranslateCookingRecipeNameForInventoryActionMenu"] = "Audited Strip call: recipe-name colors are restored through CookbookDisplayNameTranslator after inventory action menu parsing.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:2216:ContainsVisibleAsciiLetters"] = "Audited Strip call: predicate-only ASCII check over visible recipe text; color ownership is restored before the predicate result is emitted.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:2789:IsAlreadyLocalizedPopupText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:303:TranslatePopupTextForRoute"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:452:TryTranslatePopupProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PowerEntryRequirementPopupTranslationPatch.cs:110:TryTranslatePrerequisitePopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs:114:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PsychicGlimmerTranslationPatch.cs:100:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/QuestLifecyclePopupTranslationPatch.cs:118:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/RandomAltarBaetylTranslationPatch.cs:92:TryTranslatePopupMessage"] = "Audited Strip call: baetyl demand/reward captures restore item, reward, offering, and baetyl colors through MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/RebukingSifrahTranslationPatch.cs:96:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs:172:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/RequiresPowerToEquipCheckEquipPopupTranslationPatch.cs:100:TryTranslatePowerLossUnequip"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SelfTearExplosionTranslationPatch.cs:77:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs:383:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs:706:TryGetPureOwnerBatchPopupCandidateText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SifrahTokenItemPopupTranslationPatch.cs:137:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs:1953:TryTranslatePhysicsAttackConfirm"] = "Audited Strip call: attack-confirm target colors are restored by PopupTranslationPatch.TryTranslatePhysicsAttackConfirmText.",
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersLineTranslationPatch.cs:216:TranslateSkillRightText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersSelectNodePopupTranslationPatch.cs:123:TranslateProducedMessage"] = "Audited Strip call: owner-produced popup colors are restored through TryTranslateStripped and RestoreWhole; captured skill names use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersSelectNodePopupTranslationPatch.cs:159:TryTranslatePopupMessage"] = "Audited Strip call: popup colors are restored through TryTranslateStripped and RestoreWhole; captured skill names use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/SoundManagerSetChannelTrackTranslationPatch.cs:104:TryTranslateSoundLogTrack"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SpindleNegotiationTranslationPatch.cs:106:TryTranslatePopupMessage"] = "Audited Strip call: popup colors are restored through TryTranslateStripped and RestoreWhole; generated faction/item captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/SubmergedBurrowedOwnerTranslationPatch.cs:161:TryTranslateCore"] = "Audited Strip call: whole-source wrappers are restored after subject and target captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs:291:TryTranslateTail"] = "Audited Strip call: mutation popup tail colors are restored by RestoreWhole and RestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs:328:TryTranslateIncreasedRankLine"] = "Audited Strip call: mutation rank-up colors are restored by RestoreWhole and RestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs:417:TryTranslateRankBoostLine"] = "Audited Strip call: rank-boost line colors are restored by RestoreWhole; whole-line color wrappers are covered by L2 tests.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs:127:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs:151:TryTranslateExistingCampfireHere"] = "Audited Strip call: campfire capture colors are restored through the local RestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs:168:TryTranslateExistingCampfireNavigation"] = "Audited Strip call: campfire capture colors are restored through the local RestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs:185:TryTranslateCampfireInPool"] = "Audited Strip call: liquid capture colors are restored through the local RestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/TattooGunTranslationPatch.cs:105:TryTranslateTattooMessage"] = "Audited Strip call: target/tattoo colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/TempleDedicationPlaqueInscriptionTranslator.cs:34:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after finite temple dedication frame translation.",
            ["Mods/QudJP/Assemblies/src/Patches/TemporaryRealityStabilizeTranslationPatch.cs:96:TryTranslateWorldlineMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TinkerItemTranslationPatch.cs:104:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:1112:ShouldSkipMessagePatternTranslation"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:338:TryTranslatePerformOfferTradeWaterMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:356:TryTranslateHasNothingToTradeMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:374:TryTranslateTradeUiPopupText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TutorialManagerTranslationPatch.cs:56:TryTranslateExpandedHotkeyText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/VehicleFollowerPopupTranslationPatch.cs:82:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/VehicleSeatTranslationPatch.cs:90:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/VehicleUnpoweredTranslationPatch.cs:100:TryTranslatePopupMessage"] = "Audited Strip call: vehicle, cell, and slot captures are restored through markup-aware helpers before the popup frame is reconstructed.",
            ["Mods/QudJP/Assemblies/src/Patches/VillageLeaderConversationTranslator.cs:46:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after generated village leader conversation captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/VillagePetConversationTranslator.cs:92:TryTranslateQuestion"] = "Audited Strip call: whole-source wrappers are restored after generated village pet question captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/VillagePetConversationTranslator.cs:128:TryTranslateAnswer"] = "Audited Strip call: whole-source wrappers are restored after generated village pet origin-story captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/VillageWallDescriptionTranslator.cs:78:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after generated wall/canvas captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/VillageWallDescriptionTranslator.cs:158:ContainsLowercaseAsciiWord"] = "Audited Strip call: predicate-only guard used to reject untranslated lowercase capture residue after candidate translation.",
            ["Mods/QudJP/Assemblies/src/Patches/VillageTerrainRevealDescriptionTranslator.cs:172:TryTranslate"] = "Audited Strip call: whole-source wrappers are restored after generated village terrain captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:139:TryTranslatePlayerUnresponsive"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:153:TryTranslateObserverUnresponsive"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:167:TryTranslatePlayerWind"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:181:TryTranslateObserverWind"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs:111:TryTranslatePopupMessage"] = "Audited Strip call: popup target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs:133:TryTranslateQueuedMessage"] = "Audited Strip call: queued target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:764:TryTranslateHeartstopperTemplate"] = "Audited Strip call: color ownership is restored through TryFormatTemplate after translating only template captures.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:799:TryTranslateSmartTrackingScopeTemplate"] = "Audited Strip call: color ownership is restored through TryFormatTemplate after translating only template captures.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:829:TryTranslateMasterworkTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:863:TryTranslateBeamsplitterTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:902:TryTranslateOffhandAttackChanceTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:941:TryTranslateStrengthBonusCapTemplate"] = "Audited Strip call: color ownership is restored through TryFormatTemplate after translating only the cap capture.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:976:TryTranslateWeaponClassTemplate"] = "Audited Strip call: whole-source rules wrappers are restored after translating only the weapon class capture.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:1032:TryTranslateCoProcessorTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:1125:TryTranslateActiveLightSourceTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:1196:TryTranslateCounterweightedTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:1376:TryTranslateDisguiseReputationTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:1426:TryTranslateElementalDamageTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:1460:TryTranslateTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ZoneWindChangeTranslationPatch.cs:114:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:34:Strip"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs:91:Translate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs:110:TranslateCore"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/UI/FontManager.cs:250:TryWarmPrimaryFontCharactersForUi"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
        };

    private static readonly SortedDictionary<string, string> NameLikeRestoreCaptureWithoutGuardAllowlist =
        new(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/ChargenStructuredTextTranslator.cs:438:TryTranslateCyberneticsSlot:name"] =
                "Cybernetics slot names are exact static labels, not display-name owner captures.",
        };

    private static readonly string[] DisplayNameOwnerRouteFiles =
    {
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilityNameTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/BeginBeingUnequippedFailureMessageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CookingIngredientFragmentTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DisplayNameCaptureTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DisplayNameSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FabricateFromSelfAbilityDescriptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GameManagerUpdateSelectedAbilityPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GeneratedDisplayNameOwnerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/LookTooltipInformationWrapPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/RandomAltarBaetylTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SifrahTokenDescriptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TinkeringDetailsLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/VillageSignatureItemTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/WorldPartGeneratedDisplayNameTranslationPatches.cs",
        "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Translation/MessageFrameTranslator.cs",
    };

    private static readonly string[] MarkupAwareCaptureOwnerFiles =
    {
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ActivatedAbilitiesAddAbilityPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ActiveEffectPopupQueueTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/BasePronounProviderCustomizePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/BeginBeingUnequippedFailureMessageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/BrainBrineCurseTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireCookAvailabilityTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireCookFromIngredientsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireCookFromRecipeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CookingMealDescriptionTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireNostrumsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireRemainsAttemptLightTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTextTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/CloningStartBuddedCloneTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsStasisEntanglerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DynamicQuestConversationTextTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DynamicQuestExplicitConversationTextTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DynamicQuestGeneratedQuestTextTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DynamicQuestItemNameMutationTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/EatMemoriesOnHitTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EffectMobilityBlockTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EnergyCellSocketAccessPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EnergyLoaderCannotTakeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EngraverTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EngulfingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EquipmentScreenBodypartEquipPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FirefightingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FlightTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FriendOrFoeReasonTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/FugueOnStepTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GeneratedQueueDoesVerbTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/HiddenRenderTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/HookedOwnerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/IExamineEventProcessIdentifyTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ITeleporterTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ItemModdingSifrahTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/KeyMappingUiTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/LiquidLeakMessageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MentalShieldTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MutationActionFailureTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PhysicsInventoryActionPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PowerEntryRequirementPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/PsychicGlimmerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/RequiresPowerToEquipCheckEquipPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SelfTearExplosionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SifrahTokenDescriptionTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SifrahTokenItemPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersSelectNodePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SoundManagerSetChannelTrackTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SpindleNegotiationTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SubmergedBurrowedOwnerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TabulaRasaeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TattooGunTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TemporaryRealityStabilizeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TenfoldPathInitiatoryTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TinkerItemTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TinkeringMinePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TombstoneDeathCauseTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/TonicApplicatorTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/VehicleSeatTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/VehicleUnpoweredTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/VillageLeaderConversationTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/VillagePetConversationTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/WaterRitualPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/VillageWallDescriptionTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/VillageTerrainRevealDescriptionTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs",
        "Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs",
    };

    [Test]
    public void EveryStripCallSite_HasMatchingRestoreCallSite()
    {
        var actual = FindStripCallSitesWithoutLocalRestore();

        Assert.That(
            actual.Keys,
            Is.EquivalentTo(StripWithoutLocalRestoreAllowlist.Keys),
            "Each Strip call site must restore colors in the same local forward block or be explicitly allowlisted.\n"
            + string.Join("\n", actual.Keys));
    }

    [Test]
    public void DisplayNameTranslate_OnlyCalled_FromOwnerRouteAllowlist()
    {
        var actual = FindFilesContaining("GetDisplayNameRouteTranslator.TranslatePreservingColors(");

        Assert.That(actual, Is.EquivalentTo(DisplayNameOwnerRouteFiles));
    }

    [Test]
    public void RestoreCapture_OnNameLikeCapture_HasMarkupGuard()
    {
        var unguarded = FindNameLikeRestoreCaptureCallSitesWithoutMarkupGuard();
        var markupAwareOwnerFiles = FindFilesContaining("ColorAwareTranslationComposer.MarkupAwareRestoreCapture(");

        Assert.Multiple(() =>
        {
            Assert.That(
                unguarded.Keys,
                Is.EquivalentTo(NameLikeRestoreCaptureWithoutGuardAllowlist.Keys),
                "RestoreCapture on name-like captures must use a markup-aware guard or be a documented non-display-name exception.\n"
                + string.Join("\n", unguarded.Keys));
            Assert.That(markupAwareOwnerFiles, Is.EquivalentTo(MarkupAwareCaptureOwnerFiles));
        });
    }

    [Test]
    public void DictionaryCorpus_HasBalancedMarkupTokens()
    {
        var failures = new List<string>();
        foreach (var value in EnumerateDictionaryTranslatedValues())
        {
            var (stripped, spans) = ColorAwareTranslationComposer.Strip(value.Value);
            var restored = ColorAwareTranslationComposer.Restore(stripped, spans);
            if (!string.Equals(value.Value, restored, StringComparison.Ordinal))
            {
                failures.Add($"{value.RelativePath}:{value.ArrayName}.{value.PropertyName}[{value.Index}]");
            }
        }

        Assert.That(failures, Is.Empty);
    }

    private static SortedDictionary<string, string> FindStripCallSitesWithoutLocalRestore()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in GetSourceFiles())
        {
            var relativePath = ToRepositoryRelativePath(file);
            var lines = File.ReadAllLines(file);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!ContainsAny(lines[lineIndex], StripRoundTripSymbols))
                {
                    continue;
                }

                var methodStart = FindContainingMethodStart(lines, lineIndex);
                var methodName = methodStart < 0 ? "<unknown>" : ExtractMethodName(lines[methodStart]);
                var localText = methodStart < 0
                    ? lines[lineIndex]
                    : string.Join("\n", ExtractLocalForwardBlock(lines, methodStart, lineIndex));

                if (TryGetStripSpansVariable(lines[lineIndex], out var spansVariable)
                    && ContainsRestoreUsingSpansVariable(localText, spansVariable))
                {
                    continue;
                }

                var key = $"{relativePath}:{lineIndex + 1}:{methodName}";
                result[key] = lines[lineIndex].Trim();
            }
        }

        return result;
    }

    private static SortedDictionary<string, string> FindNameLikeRestoreCaptureCallSitesWithoutMarkupGuard()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in GetSourceFiles())
        {
            var relativePath = ToRepositoryRelativePath(file);
            var lines = File.ReadAllLines(file);
            foreach (var invocation in EnumerateRestoreCaptureInvocations(lines))
            {
                var methodStart = FindContainingMethodStart(lines, invocation.StartLineIndex);
                var methodName = methodStart < 0 ? "<unknown>" : ExtractMethodName(lines[methodStart]);
                var methodLines = methodStart < 0
                    ? new[] { invocation.Text }
                    : ExtractMethodBlock(lines, methodStart);
                var aliases = FindNameLikeGroupAliases(methodLines);
                if (!TryGetUnsafeNameLikeRestoreCapture(invocation.Text, aliases, out var groupName))
                {
                    continue;
                }

                var guardText = methodStart < 0
                    ? invocation.Text
                    : string.Join("\n", ExtractLocalGuardBlock(lines, methodStart, invocation.StartLineIndex));
                if (ContainsAny(guardText, NameLikeRestoreCaptureGuardSymbols))
                {
                    continue;
                }

                var key = $"{relativePath}:{invocation.StartLineIndex + 1}:{methodName}:{groupName}";
                result[key] = invocation.Text.Trim();
            }
        }

        return result;
    }

    private static string[] FindFilesContaining(string symbol)
    {
        var matches = new List<string>();

        foreach (var file in GetSourceFiles())
        {
            var text = File.ReadAllText(file);
            if (text.Contains(symbol, StringComparison.Ordinal))
            {
                matches.Add(ToRepositoryRelativePath(file));
            }
        }

        return matches.ToArray();
    }

    private static IEnumerable<DictionaryValue> EnumerateDictionaryTranslatedValues()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var dictionariesRoot = Path.Combine(root, "Mods", "QudJP", "Localization", "Dictionaries");

        foreach (var file in GetSortedFiles(dictionariesRoot, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var rootElement = document.RootElement;
            foreach (var value in EnumerateTranslatedTextArrays(rootElement, file))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<DictionaryValue> EnumerateTranslatedTextArrays(JsonElement rootElement, string file)
    {
        foreach (var property in rootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var value in EnumerateStringPropertyArray(rootElement, file, property.Name, "text"))
            {
                yield return value;
            }

            foreach (var value in EnumerateStringPropertyArray(rootElement, file, property.Name, "template"))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<DictionaryValue> EnumerateStringPropertyArray(
        JsonElement rootElement,
        string file,
        string arrayName,
        string propertyName)
    {
        if (!rootElement.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() is { } value)
            {
                yield return new DictionaryValue(ToRepositoryRelativePath(file), arrayName, propertyName, index, value);
            }

            index++;
        }
    }

    private static int FindContainingMethodStart(string[] lines, int callLineIndex)
    {
        for (var index = callLineIndex; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (line.Length == 0
                || line.StartsWith("[", StringComparison.Ordinal)
                || FileScopedNamespacePattern.IsMatch(line))
            {
                continue;
            }

            if (IsMethodSignature(line))
            {
                return index;
            }
        }

        return -1;
    }

    private static string ExtractMethodName(string line)
    {
        var match = MethodSignaturePattern.Match(line);
        return match.Success ? match.Groups["name"].Value : "<unknown>";
    }

    private static IReadOnlyList<string> ExtractMethodBlock(string[] lines, int methodStart)
    {
        var end = methodStart;
        var depth = 0;
        var sawOpeningBrace = false;
        for (var index = methodStart; index < lines.Length; index++)
        {
            var line = lines[index];
            for (var charIndex = 0; charIndex < line.Length; charIndex++)
            {
                if (line[charIndex] == '{')
                {
                    depth++;
                    sawOpeningBrace = true;
                }
                else if (line[charIndex] == '}')
                {
                    depth--;
                }
            }

            end = index;
            if (sawOpeningBrace && depth <= 0)
            {
                break;
            }
        }

        return lines[methodStart..(end + 1)];
    }

    private static IReadOnlyList<string> ExtractLocalForwardBlock(string[] lines, int methodStart, int callLineIndex)
    {
        var methodBlock = ExtractMethodBlock(lines, methodStart);
        var methodEnd = methodStart + methodBlock.Count - 1;
        var regionEnd = Math.Min(methodEnd, callLineIndex + 80);
        return lines[callLineIndex..(regionEnd + 1)];
    }

    private static IReadOnlyList<string> ExtractLocalGuardBlock(string[] lines, int methodStart, int callLineIndex)
    {
        var methodBlock = ExtractMethodBlock(lines, methodStart);
        var methodEnd = methodStart + methodBlock.Count - 1;
        var regionStart = Math.Max(methodStart, callLineIndex - 80);
        var regionEnd = Math.Min(methodEnd, callLineIndex + 80);
        return lines[regionStart..(regionEnd + 1)];
    }

    private static bool TryGetStripSpansVariable(string line, out string spansVariable)
    {
        var match = StripDeconstructionPattern.Match(line);
        if (match.Success && match.Groups["spans"].Value != "_")
        {
            spansVariable = match.Groups["spans"].Value;
            return true;
        }

        spansVariable = string.Empty;
        return false;
    }

    private static bool ContainsRestoreUsingSpansVariable(string source, string spansVariable)
    {
        for (var symbolIndex = 0; symbolIndex < RestoreRoundTripSymbols.Length; symbolIndex++)
        {
            var symbol = RestoreRoundTripSymbols[symbolIndex];
            var searchStart = 0;
            while (searchStart < source.Length)
            {
                var occurrence = source.IndexOf(symbol, searchStart, StringComparison.Ordinal);
                if (occurrence < 0)
                {
                    break;
                }

                var invocation = TryExtractInvocation(source, occurrence, out var extracted)
                    ? extracted
                    : source[occurrence..];
                if (ContainsIdentifier(invocation, spansVariable))
                {
                    return true;
                }

                searchStart = occurrence + symbol.Length;
            }
        }

        return false;
    }

    private static IEnumerable<InvocationText> EnumerateRestoreCaptureInvocations(string[] lines)
    {
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var searchStart = 0;
            while (searchStart < lines[lineIndex].Length)
            {
                var occurrence = lines[lineIndex].IndexOf("RestoreCapture(", searchStart, StringComparison.Ordinal);
                if (occurrence < 0)
                {
                    break;
                }

                if (TryExtractInvocation(lines, lineIndex, occurrence, out var invocation))
                {
                    yield return invocation;
                }
                else
                {
                    yield return new InvocationText(lines[lineIndex][occurrence..], lineIndex);
                }

                searchStart = occurrence + "RestoreCapture(".Length;
            }
        }
    }

    private static bool TryExtractInvocation(string[] lines, int lineIndex, int invocationNameIndex, out InvocationText invocation)
    {
        var source = string.Join("\n", lines[lineIndex..]);
        var start = GetInvocationTextStart(lines[lineIndex], invocationNameIndex);
        if (!TryExtractInvocation(source, start, out var invocationText))
        {
            invocation = new InvocationText(string.Empty, lineIndex);
            return false;
        }

        invocation = new InvocationText(invocationText, lineIndex);
        return true;
    }

    private static bool TryExtractInvocation(string source, int invocationStart, out string invocationText)
    {
        var openParen = source.IndexOf('(', invocationStart);
        if (openParen < 0)
        {
            invocationText = string.Empty;
            return false;
        }

        var depth = 0;
        for (var index = openParen; index < source.Length; index++)
        {
            if (source[index] == '(')
            {
                depth++;
            }
            else if (source[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    invocationText = source[invocationStart..(index + 1)];
                    return true;
                }
            }
        }

        invocationText = string.Empty;
        return false;
    }

    private static int GetInvocationTextStart(string line, int invocationNameIndex)
    {
        var index = invocationNameIndex;
        while (index > 0 && IsInvocationQualifierCharacter(line[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static bool IsInvocationQualifierCharacter(char value)
    {
        return value == '.' || value == '_' || char.IsAsciiLetterOrDigit(value);
    }

    private static bool ContainsIdentifier(string source, string identifier)
    {
        var searchStart = 0;
        while (searchStart < source.Length)
        {
            var index = source.IndexOf(identifier, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var before = index == 0 ? '\0' : source[index - 1];
            var afterIndex = index + identifier.Length;
            var after = afterIndex >= source.Length ? '\0' : source[afterIndex];
            if (!IsIdentifierCharacter(before) && !IsIdentifierCharacter(after))
            {
                return true;
            }

            searchStart = index + identifier.Length;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return value == '_' || char.IsAsciiLetterOrDigit(value);
    }

    private static Dictionary<string, string> FindNameLikeGroupAliases(IReadOnlyList<string> methodLines)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < methodLines.Count; index++)
        {
            var match = AliasAssignmentPattern.Match(methodLines[index]);
            if (match.Success)
            {
                aliases[match.Groups["alias"].Value] = match.Groups["name"].Value;
            }
        }

        return aliases;
    }

    private static bool TryGetUnsafeNameLikeRestoreCapture(
        string line,
        IReadOnlyDictionary<string, string> aliases,
        out string groupName)
    {
        var directMatch = DirectNameLikeRestoreCapturePattern.Match(line);
        if (directMatch.Success)
        {
            groupName = directMatch.Groups["name"].Value;
            return !IsSourceCaptureRestore(directMatch.Groups["value"].Value, groupName, aliases);
        }

        var aliasMatch = AliasedNameLikeRestoreCapturePattern.Match(line);
        if (aliasMatch.Success && aliases.TryGetValue(aliasMatch.Groups["alias"].Value, out var aliasedGroupName))
        {
            groupName = aliasedGroupName;
            return !IsSourceCaptureRestore(aliasMatch.Groups["value"].Value, groupName, aliases);
        }

        var helperMatch = SourceHelperNameLikeRestoreCapturePattern.Match(line);
        if (helperMatch.Success)
        {
            groupName = helperMatch.Groups["name"].Value;
            return false;
        }

        groupName = string.Empty;
        return false;
    }

    private static bool IsSourceCaptureRestore(string valueExpression, string groupName, IReadOnlyDictionary<string, string> aliases)
    {
        var normalized = valueExpression.Trim();
        var sourceMatch = SourceCaptureValuePattern.Match(normalized);
        if (!sourceMatch.Success)
        {
            return false;
        }

        if (sourceMatch.Groups["name"].Success)
        {
            return string.Equals(sourceMatch.Groups["name"].Value, groupName, StringComparison.Ordinal);
        }

        return aliases.TryGetValue(sourceMatch.Groups["alias"].Value, out var aliasedGroup)
            && string.Equals(aliasedGroup, groupName, StringComparison.Ordinal);
    }

    private static bool IsMethodSignature(string line)
    {
        if (!MethodSignaturePattern.IsMatch(line))
        {
            return false;
        }

        return !line.StartsWith("if ", StringComparison.Ordinal)
            && !line.StartsWith("for ", StringComparison.Ordinal)
            && !line.StartsWith("foreach ", StringComparison.Ordinal)
            && !line.StartsWith("while ", StringComparison.Ordinal)
            && !line.StartsWith("switch ", StringComparison.Ordinal)
            && !line.StartsWith("catch ", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string source, IReadOnlyList<string> symbols)
    {
        for (var index = 0; index < symbols.Count; index++)
        {
            if (source.Contains(symbols[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToRepositoryRelativePath(string path)
    {
        return Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string[] GetSourceFiles()
    {
        var sourceRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Assemblies", "src");
        return GetSortedFiles(sourceRoot, "*.cs");
    }

    private static string[] GetSortedFiles(string root, string searchPattern)
    {
        var files = Directory.GetFiles(root, searchPattern, SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    private sealed record DictionaryValue(string RelativePath, string ArrayName, string PropertyName, int Index, string Value);

    private sealed record InvocationText(string Text, int StartLineIndex);
}
