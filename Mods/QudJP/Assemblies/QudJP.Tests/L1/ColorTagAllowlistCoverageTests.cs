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
            ["Mods/QudJP/Assemblies/src/Observability/FinalOutputObservability.cs:109:RecordDirectMarker"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs:264:HasColorMarkup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs:113:TryTranslatePopupMessage"] = "Audited Strip call: popup target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs:134:TryTranslateQueuedMessage"] = "Audited Strip call: queued target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:131:TryTranslateExact"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:170:TryTranslateTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/AnimateObjectTranslationPatch.cs:109:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BasePronounProviderCustomizePopupTranslationPatch.cs:97:TryTranslateCustomizePopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BedChairFragmentTranslator.cs:120:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs:105:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/BrainBrineCurseTranslationPatch.cs:93:TryTranslatePopupMessage"] = "Audited Strip call: name colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/CampfireNostrumsTranslationPatch.cs:163:TryTranslatePopupMessage"] = "Audited Strip call: generated target/condition captures are restored through the local MarkupAwareRestoreCapture helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs:139:TranslatePreservedLine"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CampfireRemainsAttemptLightTranslationPatch.cs:84:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ClonelingVehicleFragmentTranslator.cs:56:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CloningStartBuddedCloneTranslationPatch.cs:116:TryTranslateDetachMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CombatTextSurfaceTranslationPatch.cs:143:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationRewardPopupTranslationPatch.cs:132:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ConversationScriptPopupTranslationPatch.cs:148:TryTranslateCore"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CookingEffectFragmentTranslator.cs:312:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CookingRuntimeTranslationPatch.cs:330:TryTranslateWellFedPopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/CyberneticsStasisEntanglerTranslationPatch.cs:103:TryTranslateDeployMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DamagePenetrationDebugTranslationPatch.cs:101:TryTranslateDebugMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs:86:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DesalinationPelletFragmentTranslator.cs:21:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:175:TryFindDanglingBoundaryOpening"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:231:HasColorBoundaryOpening"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:266:HasColorBoundaryClosing"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:346:TryTranslateSultanShrineWrapperPreservingColors"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs:118:TryTranslateReverseEngineerPrompt"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs:138:TryTranslateStartDisassemblingMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EmboldenedTranslationPatch.cs:118:TryTranslateCore"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs:143:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs:68:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnergyCellSocketAccessPopupTranslationPatch.cs:87:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EnergyLoaderCannotTakeTranslationPatch.cs:79:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EngraverTranslationPatch.cs:101:TryTranslateEngraveMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/EngulfingTranslationPatch.cs:100:TryTranslateEngulfingMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs:137:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FactionsLineTranslationPatch.cs:70:TranslateTextField"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FactionsStatusScreenTranslationPatch.cs:878:AddLocalizedSearchFragment"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FirefightingTranslationPatch.cs:95:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FlightTranslationPatch.cs:95:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ForceBubbleOwnerTranslationPatch.cs:95:TryTranslateForceBubbleMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/FugueOnStepTranslationPatch.cs:99:TryTranslateStepMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs:111:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedQueueDoesVerbTranslationPatch.cs:145:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/GeneratedSubjectQueueTranslationPatch.cs:144:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/HiddenRenderTranslationPatch.cs:95:TryTranslateRevealMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/IExamineEventProcessIdentifyTranslationPatch.cs:85:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ITeleporterTranslationPatch.cs:125:TryTranslatePopupMessage"] = "Audited Strip call: generated subject/plane colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs:139:TryTranslateGraveyardZoneMessage"] = "Audited Strip call: owner colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/InventoryFireEventTranslationPatch.cs:154:TryTranslateContainerOwnershipPrompt"] = "Audited Strip call: container/item colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/JournalNotificationTranslator.cs:20:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/KeyMappingUiTranslationPatch.cs:112:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/LevelerTranslationPatch.cs:109:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs:204:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs:116:TryTranslatePopupMessage"] = "Audited Strip call: popup target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/LongBladesCoreTranslationPatch.cs:136:TryTranslateQueuedMessage"] = "Audited Strip call: queued actor/target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs:135:TryTranslateCompanionRippedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs:151:TryTranslateRippedFromPlayerMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs:165:TryTranslatePulledTowardMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs:207:TranslateProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MapRevealPopupTranslationPatch.cs:139:TryTranslatePopupMessageForOwnerKey"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MentalShieldTranslationPatch.cs:91:TryTranslateMentalShieldMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:60:Prefix"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MissileWeaponHitTranslationPatch.cs:114:TryTranslateQueuedMessage"] = "Audited Strip call: predicate-only route gate; color ownership is preserved by MessageLogProducerTranslationHelpers.TryPreparePatternMessage.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationActionFailureTranslationPatch.cs:89:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs:127:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationGeneratedTextTranslationPatch.cs:152:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationInfectionTranslationPatch.cs:84:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/MutationsApiTranslationPatch.cs:85:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs:106:TranslateProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetEitherOrExplodeTranslationPatch.cs:131:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:139:TryTranslateAstralTether"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:153:TryTranslateWisdomReveal"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:167:TryTranslateStopGleaming"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs:181:TryTranslateStartGleaming"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PickGameObjectScreenTranslationPatch.cs:115:TranslateProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs:131:TryTranslateMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs:151:TryTranslatePopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationHelpers.cs:101:TryTranslateFoodWaterPart"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PhysicsInventoryActionPopupTranslationPatch.cs:94:TryTranslatePopupMessage"] = "Audited Strip call: ownership-pour colors are restored by LiquidVolumeFragmentTranslator, cleaning-liquid captures use MarkupAwareRestoreCapture, and attack-confirm target colors are restored by PopupTranslationPatch.TryTranslatePhysicsAttackConfirmText.",
            ["Mods/QudJP/Assemblies/src/Patches/PhysicsProcessTakeDamageTranslationPatch.cs:251:TryTranslateDamageFrame"] = "Audited Strip call: damage-frame source and damage-type captures are restored through local MarkupAwareRestoreCapture helpers.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:1618:IsAlreadyLocalizedPopupText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:267:TranslatePopupTextForRoute"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:318:TranslatePopupMenuItemTextForRoute"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:409:TryTranslatePopupProducerText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PowerEntryRequirementPopupTranslationPatch.cs:110:TryTranslatePrerequisitePopup"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs:105:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/PsychicGlimmerTranslationPatch.cs:100:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs:100:NormalizeItemTexts"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/QuestLifecyclePopupTranslationPatch.cs:118:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/RebukingSifrahTranslationPatch.cs:96:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs:156:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/RequiresPowerToEquipCheckEquipPopupTranslationPatch.cs:100:TryTranslatePowerLossUnequip"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SelfTearExplosionTranslationPatch.cs:77:TryTranslateQueuedMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs:343:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs:646:TryGetPureOwnerBatchPopupCandidateText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SifrahTokenItemPopupTranslationPatch.cs:137:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs:1275:TryTranslatePhysicsAttackConfirm"] = "Audited Strip call: attack-confirm target colors are restored by PopupTranslationPatch.TryTranslatePhysicsAttackConfirmText.",
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersLineTranslationPatch.cs:216:TranslateSkillRightText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersSelectNodePopupTranslationPatch.cs:106:TryTranslatePopupMessage"] = "Audited Strip call: popup colors are restored through TryTranslateStripped and RestoreWhole; captured skill names use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/SoundManagerSetChannelTrackTranslationPatch.cs:104:TryTranslateSoundLogTrack"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SpindleNegotiationTranslationPatch.cs:106:TryTranslatePopupMessage"] = "Audited Strip call: popup colors are restored through TryTranslateStripped and RestoreWhole; generated faction/item captures use MarkupAwareRestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs:291:TryTranslateTail"] = "Audited Strip call: mutation popup tail colors are restored by RestoreWhole and RestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs:328:TryTranslateIncreasedRankLine"] = "Audited Strip call: mutation rank-up colors are restored by RestoreWhole and RestoreCapture.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenMutationPopupTranslationPatch.cs:417:TryTranslateRankBoostLine"] = "Audited Strip call: rank-boost line colors are restored by RestoreWhole; whole-line color wrappers are covered by L2 tests.",
            ["Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs:119:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs:107:TryTranslateExistingCampfireNavigation"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TattooGunTranslationPatch.cs:105:TryTranslateTattooMessage"] = "Audited Strip call: target/tattoo colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/TemporaryRealityStabilizeTranslationPatch.cs:96:TryTranslateWorldlineMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TinkerItemTranslationPatch.cs:104:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:1020:ShouldSkipMessagePatternTranslation"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:309:TryTranslatePerformOfferTradeWaterMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:327:TryTranslateHasNothingToTradeMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs:345:TryTranslateTradeUiPopupText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/TutorialManagerTranslationPatch.cs:56:TryTranslateExpandedHotkeyText"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/VehicleSeatTranslationPatch.cs:90:TryTranslatePopupMessage"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:139:TryTranslatePlayerUnresponsive"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:153:TryTranslateObserverUnresponsive"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:167:TryTranslatePlayerWind"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs:181:TryTranslateObserverWind"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs:111:TryTranslatePopupMessage"] = "Audited Strip call: popup target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/XrlCorePlayerTurnTranslationPatch.cs:133:TryTranslateQueuedMessage"] = "Audited Strip call: queued target colors are restored through the local MarkupAwareRestoreCapture helper.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:544:TryTranslateCoProcessorTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:629:TryTranslateActiveLightSourceTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:700:TryTranslateCounterweightedTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:729:TryTranslateDisguiseReputationTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:779:TryTranslateElementalDamageTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:812:TryTranslateTemplate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Patches/ZoneWindChangeTranslationPatch.cs:114:TryTranslate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Translation/ColorAwareTranslationComposer.cs:30:Strip"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs:77:Translate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs:83:Translate"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
            ["Mods/QudJP/Assemblies/src/UI/FontManager.cs:250:TryWarmPrimaryFontCharactersForUi"] = "Audited Strip call: color ownership is restored in a downstream branch/helper or the call is predicate/observation-only.",
        };

    private static readonly SortedDictionary<string, string> NameLikeRestoreCaptureWithoutGuardAllowlist =
        new(StringComparer.Ordinal)
        {
            ["Mods/QudJP/Assemblies/src/Patches/ChargenStructuredTextTranslator.cs:423:TryTranslateCyberneticsSlot:name"] =
                "Cybernetics slot names are exact static labels, not display-name owner captures.",
        };

    private static readonly string[] DisplayNameOwnerRouteFiles =
    {
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/AbilityBarButtonTextTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DisplayNameSemanticPipeline.cs",
        "Mods/QudJP/Assemblies/src/Patches/GameManagerUpdateSelectedAbilityPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TradeLineTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs",
    };

    private static readonly string[] MarkupAwareCaptureOwnerFiles =
    {
        "Mods/QudJP/Assemblies/src/Patches/ActionManagerRunSegmentTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/BasePronounProviderCustomizePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/BrainBrineCurseTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireCookAvailabilityTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireCookFromIngredientsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireNostrumsTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CampfireRemainsAttemptLightTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CloningStartBuddedCloneTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsStasisEntanglerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs",
        "Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EatMemoriesOnHitTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EffectMobilityBlockTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EnergyCellSocketAccessPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EnergyLoaderCannotTakeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EngraverTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/EngulfingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FirefightingTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FlightTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/FugueOnStepTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/GeneratedQueueDoesVerbTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/HiddenRenderTranslationPatch.cs",
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
        "Mods/QudJP/Assemblies/src/Patches/SifrahTokenItemPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersSelectNodePopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SoundManagerSetChannelTrackTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SpindleNegotiationTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/SurvivalCampAttemptCampPopupTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TabulaRasaeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TattooGunTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TemporaryRealityStabilizeTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TenfoldPathInitiatoryTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TinkerItemTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/TonicApplicatorTranslationPatch.cs",
        "Mods/QudJP/Assemblies/src/Patches/VehicleSeatTranslationPatch.cs",
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
