using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupTranslationPatch
{
    private const string TargetTypeName = "XRL.UI.Popup";
    private const string UntilPrefix = "Until ";
    private const string QudMenuItemContext = "QudMenuItem";
    private const string QudMenuItemDictionaryFile = "Scoped/ui-popup-qud-menu-item.ja.json";
    private const string CommonMenuActionDictionaryFile = "Scoped/ui-menu-actions.ja.json";
    private const string PresetMealNameDictionaryFile = "Scoped/ui-popup-campfire-preset-meals.ja.json";
    private const string InventoryActionMenuPopupIdPrefix = "InventoryActionMenu:";
    private const string InventoryActionContext = "XRL.World.IInventoryActionsEvent";
    private const string InventoryActionDictionaryFile = "ui-inventory-actions.ja.json";
    private static readonly HashSet<string> CampfireCookingActionLabels = new(StringComparer.Ordinal)
    {
        "Whip up a meal.",
        "Choose ingredients to cook with.",
        "Cook from a recipe.",
        "Preserve your fresh foods.",
        "Preserve your exotic foods.",
        "Stop bleeding.",
        "Treat poison.",
        "Treat illness.",
        "Treat disease onset.",
    };
    private static readonly Regex AsciiLetterPattern =
        new Regex("[A-Za-z]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EmbeddedHotkeyLabelPattern =
        new Regex("^[A-Za-z][A-Za-z .]*[A-Za-z.]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnergyCellSocketPickerTitlePattern =
        new Regex("^Choose a cell for (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnergyCellSocketRemoveCellPattern =
        new Regex("^remove cell: (?<cell>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex InventoryActionRechargeCellPattern =
        new Regex("^Recharge (?<cell>.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InventoryActionCleanAllItemsPattern =
        new Regex("^clean all your items \\[(?<amount>\\d+) drams?\\]$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RenameItemTitlePattern =
        new Regex("^Rename (?<target>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RandomNameCultureOptionPattern =
        new Regex("^Choose a random name from (?<culture>.+?) culture\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlainHotkeyLabelPattern =
        new Regex("^(?<hotkey>Enter|Esc|Tab|Space|space)\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NumberedConversationChoicePattern =
        new Regex("^\\[(?<index>\\d+)\\]\\s+(?<text>.+?)(?:\\s+\\[[^\\]]+\\])?\\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DeleteSavePromptPattern =
        new Regex("^Are you sure you want to delete the save game for (?<value>.+?)\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DeleteTitlePattern =
        new Regex("^Delete (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DuplicateBuildCodePattern =
        new Regex("^That code is already in your library\\. It's named (?<value>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ManageBuildTitlePattern =
        new Regex("^Manage Build: (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GenderCustomizeNamePromptPattern =
        new Regex("^What name should be used for your (?<value>.+?)\\? \\(Male, female, etc\\.\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SifrahChosenCorrectPattern =
        new Regex("^You have already chosen the correct option for (?<value>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SifrahUseWhichPattern =
        new Regex("^Use which option for (?<value>.+)\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SifrahEliminatedPattern =
        new Regex("^You have already eliminated (?<value>.+) as a possibility\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SifrahDisabledPattern =
        new Regex("^Choosing (?<value>.+) is disabled for this turn\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SifrahInsightPattern =
        new Regex("^You have gained insight into (?<value>.+)\\. In a future Sifrah task of this kind, you can use this insight to determine which of your game options are not correct for any requirement\\. This will expend your insight, unless there are no such options\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreHPWarningPattern =
        new Regex("^\\{\\{R\\|Your health has dropped below \\{\\{C\\|(?<value>\\d+)%\\}\\}!\\}\\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreOutOfRangePattern =
        new Regex("^That is out of range! \\((?<value>\\d+) squares?\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreTargetOutOfRangePattern =
        new Regex("^That target is out of range! \\((?<value>\\d+) squares?\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DiscoverLocationPattern =
        new Regex("^You discover (?<value>.+)!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DiscoveredLocationPattern =
        new Regex("^You discovered (?<value>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DiscoverHiddenExaminerPattern =
        new Regex("^You discover something about (?<value>.+?) that was hidden!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TemporalFugueDuplicateImpossiblePattern =
        new Regex("^It is impossible to duplicate (?<value>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuestReceivedPattern =
        new Regex("^You have received a new quest, (?<value>.+)!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EndGameConfirmPattern =
        new Regex("^End game\\?\\n\\nType (?<value>.+?) to confirm\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhysicsAttackConfirmPattern =
        new Regex("^Do you really want to attack (?<value>(?:the |a |an )?.+?)\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ConversationRefusalPattern =
        new Regex("^(?<value>(?:The |the |[Aa]n? )?.+?) refuses? to speak to you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreFleePattern =
        new Regex("^You can't find a way to flee from (?<value>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreReachPattern =
        new Regex("^You can't find a way to reach (?<value>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreAutoattackPattern =
        new Regex("^You do not autoattack (?<value>.+?) because .+ not hostile to you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreReloadPattern =
        new Regex("^You need to reload! \\((?<value>.+)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreOldSavePattern =
        new Regex("^That save file looks like it's from an older save format revision \\((?<value>.+?)\\)\\. Sorry!\\n\\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex XRLCoreGameInfoPattern =
        new Regex("^\\s+(?<mode>.+?) mode\\.\\s+Turn (?<turn>\\d+)\\s+World seed: (?<seed>.+?)\\s+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireStaunchPassThroughPattern =
        new Regex("^You try to staunch the wounds of (?<value>.+?), but your limbs pass through .+\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireStaunchCannotAffectPattern =
        new Regex("^You try to staunch the wounds of (?<value>.+?), but cannot affect .+\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireStaunchPartialPattern =
        new Regex("^You staunch the wounds of (?<value>.+?), though some are too deep to treat\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireStaunchFullPattern =
        new Regex("^You staunch the wounds of (?<value>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireWoundsTooDeepPattern =
        new Regex("^(?<value>.+?) are too deep to treat\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireNeitherBleedingPattern =
        new Regex("^Neither you nor (?<value>.+) are bleeding\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireNoMedicinalPattern =
        new Regex("^You have no medicinal ingredients with which to treat the poison coursing through (?<value>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfirePoisonPassThroughPattern =
        new Regex("^You try to cure the poison coursing through (?<value>.+?), but your limbs pass through .+\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfirePoisonCannotAffectPattern =
        new Regex("^You try to cure the poison coursing through (?<value>.+?), but cannot affect .+\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfirePoisonIneffectivePattern =
        new Regex("^You try to cure the poison coursing through (?<value>.+?), but your cures are ineffective\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireCurePoisonPattern =
        new Regex("^You cure the (?<poison>poison|poisons) coursing through (?<target>.+?) with a balm made from (?<ingredient>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireNewRecipePattern =
        new Regex("^You create a new recipe for \\{\\{\\|(?<value>.+?)\\}\\}!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfireMetabolizePattern =
        new Regex("^You start to metabolize the meal, gaining the following effect for the rest of the day:\\n\\n\\{\\{W\\|(?<value>.+?)\\}\\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CampfirePreservePattern =
        new Regex("^(?<item>.+): how many do you want to preserve\\? \\(max = (?<max>.+)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WaterRitualLowReputationPattern =
        new Regex("^You don't have a high enough reputation with (?<value>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // ShowBlock parameter count for game version 1.0.4
    private const int ShowBlockParameterCount = 8;

    // ShowOptionList parameter count for game version 1.0.4
    private const int ShowOptionListParameterCount = 19;

    private const int ShowConversationParameterCount = 7;

    // ShowOptionList argument indices (game version 1.0.4)
    private const int ShowOptionListIntroIndex = 4;
    private const int ShowOptionListSpacingTextIndex = 9;
    private const int ShowOptionListButtonsIndex = 14;

    private const int ShowConversationTitleIndex = 0;
    private const int ShowConversationIntroIndex = 2;
    private const int ShowConversationOptionsIndex = 3;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var showBlock = FindMethod(methodName: "ShowBlock", parameterCount: ShowBlockParameterCount);
        if (showBlock is not null)
        {
            yield return showBlock;
        }

        var showOptionList = FindMethod(methodName: "ShowOptionList", parameterCount: ShowOptionListParameterCount);
        if (showOptionList is not null)
        {
            yield return showOptionList;
        }

        var showConversation = FindMethod(methodName: "ShowConversation", parameterCount: ShowConversationParameterCount);
        if (showConversation is not null)
        {
            yield return showConversation;
        }
    }

    public static void Prefix(MethodBase __originalMethod, object[] __args)
    {
        try
        {
            if (__originalMethod is null || __args is null)
            {
                Trace.TraceError("QudJP: PopupTranslationPatch.Prefix received null originalMethod or args.");
                return;
            }

            if (__originalMethod.Name == "ShowBlock")
            {
                TranslateShowBlockArgs(__args);
                return;
            }

            if (__originalMethod.Name == "ShowOptionList")
            {
                TranslateShowOptionListArgs(__args);
                return;
            }

            if (__originalMethod.Name == "ShowConversation")
            {
                TranslateShowConversationArgs(__args);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void TranslateShowBlockArgs(object[] args)
    {
        TranslateStringArg(args, index: 0);
        TranslateStringArg(args, index: 1);
    }

    private static void TranslateShowOptionListArgs(object[] args)
    {
        TranslateStringArg(args, index: 0);
        TranslateStringListArg(args, index: 1);
        TranslateStringArg(args, index: ShowOptionListIntroIndex);
        TranslateStringArg(args, index: ShowOptionListSpacingTextIndex);

        if (args.Length > ShowOptionListButtonsIndex)
        {
            TranslatePopupMenuItemTextCollection(args[ShowOptionListButtonsIndex]);
        }
    }

    private static void TranslateShowConversationArgs(object[] args)
    {
        TranslateStringArg(args, index: ShowConversationTitleIndex);
        TranslateStringArg(args, index: ShowConversationIntroIndex);
        TranslateStringListArg(args, index: ShowConversationOptionsIndex);
    }

    private static void TranslateStringArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            return;
        }

        if (args[index] is string text)
        {
            args[index] = TranslatePopupText(text);
        }
    }

    private static void TranslateStringListArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            return;
        }

        if (args[index] is null || args[index] is string || args[index] is not IEnumerable enumerable)
        {
            return;
        }

        var translated = new List<string>();
        var anyChanged = false;
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                translated.Add(string.Empty);
                continue;
            }

            if (item is not string text)
            {
                return;
            }

            var result = TranslatePopupText(text);
            if (!anyChanged && !string.Equals(text, result, StringComparison.Ordinal))
            {
                anyChanged = true;
            }

            translated.Add(result);
        }

        if (anyChanged)
        {
            args[index] = translated;
        }
    }

    internal static string TranslatePopupTextForRoute(string source, string route)
    {
        if (TryStripPopupDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (!IsAlreadyLocalizedPopupTextCore(stripped))
        {
            SinkObservation.LogUnclaimed(
                nameof(PopupTranslationPatch),
                route,
                SinkObservation.ObservationOnlyDetail,
                source,
                stripped);
        }

        return source;
    }

    internal static string TranslatePopupMenuItemText(string source)
    {
        return TranslatePopupMenuItemTextForProducerRoute(source, nameof(PopupTranslationPatch));
    }

    internal static string TranslatePopupTextForProducerRoute(string source, string route)
    {
        return TranslatePopupProducerText(source, route, "Popup.ProducerText", popupId: null);
    }

    internal static string TranslatePopupMenuItemTextForProducerRoute(string source, string route)
    {
        return TranslatePopupMenuItemTextForProducerRoute(source, route, popupId: null);
    }

    internal static string TranslatePopupMenuItemTextForProducerRoute(string source, string route, string? popupId)
    {
        return TranslatePopupProducerText(source, route, "Popup.ProducerMenuItem", popupId);
    }

    private static string TranslatePopupText(string source)
    {
        return TranslatePopupTextForProducerRoute(source, nameof(PopupTranslationPatch));
    }

    private static string TranslatePopupProducerText(string source, string route, string family, string? popupId)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        if (ObjectFinderConfigFiltersTranslationPatch.TryTranslateFixedPopupText(source, out var objectFinderFixedTranslated))
        {
            return NormalizeProducerText(objectFinderFixedTranslated);
        }

        if (ItemNamingTranslationPatch.TryTranslatePopupMessage(source, route, family, out var itemNamingOwnerTranslated))
        {
            return NormalizeProducerText(itemNamingOwnerTranslated);
        }

        if (TinkeringHelpersMakersMarkTranslationPatch.TryTranslatePopupMessage(source, route, family, out var makersMarkTranslated))
        {
            return NormalizeProducerText(makersMarkTranslated);
        }

        if (SavesApiFatalSaveErrorTranslationPatch.TryTranslatePopupMessage(source, route, family, out var saveErrorTranslated))
        {
            return NormalizeProducerText(saveErrorTranslated);
        }

        if (EquipmentScreenBodypartEquipPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            route,
            family,
            out var equipmentScreenTranslated))
        {
            return NormalizeProducerText(equipmentScreenTranslated);
        }

        if (ModDisguiseBeingAppliedPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            route,
            family,
            out var disguiseTranslated))
        {
            return NormalizeProducerText(disguiseTranslated);
        }

        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out var doesVerbTranslated))
        {
            DynamicTextObservability.RecordTransform(route, family + ".DoesVerb", source, doesVerbTranslated);
            return NormalizeProducerText(doesVerbTranslated);
        }

        if (!string.Equals(doesVerbTranslated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family + ".DoesVerb", source, doesVerbTranslated);
            return NormalizeProducerText(doesVerbTranslated);
        }

        if (TryTranslatePopupProducerText(source, route, family, popupId, out var translated))
        {
            return NormalizeProducerText(translated);
        }

        var unmarkedSource = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        if (!string.Equals(unmarkedSource, source, StringComparison.Ordinal))
        {
            return unmarkedSource;
        }

        return source;
    }

    private static string NormalizeProducerText(string translated)
    {
        if (translated is null)
        {
            return string.Empty;
        }

        if (translated.Length == 0)
        {
            return string.Empty;
        }

        return translated.Replace("{{hotkey|}}", string.Empty);
    }

    private static bool TryStripPopupDirectTranslationMarker(string source, out string stripped)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out stripped))
        {
            return true;
        }

        var markerIndex = source.IndexOf(MessageFrameTranslator.DirectTranslationMarker);
        if (markerIndex < 0)
        {
            stripped = source;
            return false;
        }

        stripped = source.Remove(markerIndex, 1);
        return true;
    }

    private static bool TryTranslatePopupProducerText(
        string source,
        string route,
        string family,
        string? popupId,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        if (EmbarkBuilderValidationPopupTranslationPatch.TryTranslatePopupMessage(
                source,
                route,
                family,
                out var embarkBuilderValidationTranslated))
        {
            translated = embarkBuilderValidationTranslated;
            return true;
        }

        if (TradeUiVendorPopupTranslationPatch.TryTranslatePopupMessage(source, route, family, out var tradeUiVendorTranslated))
        {
            translated = tradeUiVendorTranslated;
            return true;
        }

        if (ObjectFinderConfigFiltersTranslationPatch.TryTranslatePopupMessage(source, route, family, out var objectFinderTranslated))
        {
            translated = objectFinderTranslated;
            return true;
        }

        if (ObjectFinderConfigFiltersTranslationPatch.ShouldClaimPopupMessagePassthrough())
        {
            translated = source;
            return true;
        }

        if (GameObjectPopupTranslationPatch.TryTranslatePopupMessage(source, route, family, out var gameObjectPopupTranslated))
        {
            translated = gameObjectPopupTranslated;
            return true;
        }

        if (VehicleFollowerPopupTranslationPatch.TryTranslatePopupProducerText(
                source,
                route,
                family,
                out var vehicleFollowerTranslated))
        {
            translated = vehicleFollowerTranslated;
            return true;
        }

        if (QuestLifecyclePopupTranslationPatch.TryTranslatePopupMessage(source, route, family, out var questLifecycleTranslated))
        {
            translated = questLifecycleTranslated;
            return true;
        }

        if (MetricsManagerLogErrorTranslationPatch.TryTranslatePopupMessage(source, route, family, out var metricsManagerLogErrorTranslated))
        {
            translated = metricsManagerLogErrorTranslated;
            return true;
        }

        if (DeathWrapperFamilyTranslator.TryTranslatePopup(stripped, spans, route, out var deathTranslated))
        {
            translated = deathTranslated;
            return true;
        }

        if (BedTranslationPatch.TryTranslatePopupMessage(source, route, family, out var bedTranslated))
        {
            translated = bedTranslated;
            return true;
        }

        if (ChairTranslationPatch.TryTranslatePopupMessage(source, route, family, out var chairTranslated))
        {
            translated = chairTranslated;
            return true;
        }

        if (LiquidVolumeTranslationPatch.TryTranslatePopupMessage(source, route, family, out var liquidVolumeTranslated))
        {
            translated = liquidVolumeTranslated;
            return true;
        }

        if (DesalinationPelletTranslationPatch.TryTranslatePopupMessage(source, route, family, out var desalinationTranslated))
        {
            translated = desalinationTranslated;
            return true;
        }

        if (FungalSporeInfectionTranslationPatch.TryTranslatePopupMessage(source, route, family, out var fungalSporeTranslated))
        {
            translated = fungalSporeTranslated;
            return true;
        }

        if (ClonelingVehicleTranslationPatch.TryTranslatePopupMessage(source, route, family, out var clonelingVehicleTranslated))
        {
            translated = clonelingVehicleTranslated;
            return true;
        }

        if (EnclosingTranslationPatch.TryTranslatePopupMessage(source, route, family, out var enclosingTranslated))
        {
            translated = enclosingTranslated;
            return true;
        }

        if (RepairTranslationPatch.TryTranslatePopupMessage(source, route, family, out var repairTranslated))
        {
            translated = repairTranslated;
            return true;
        }

        if (TinkeringMinePopupTranslationPatch.TryTranslatePopupMessage(source, route, family, out var tinkeringMineTranslated))
        {
            translated = tinkeringMineTranslated;
            return true;
        }

        if (PlayerDanceRitualTranslationPatch.TryTranslatePopupMessage(source, route, family, out var playerDanceRitualTranslated))
        {
            translated = playerDanceRitualTranslated;
            return true;
        }

        if (BeguilingSifrahTranslationPatch.TryTranslatePopupMessage(source, route, family, out var beguilingSifrahTranslated))
        {
            translated = beguilingSifrahTranslated;
            return true;
        }

        if (ProselytizationSifrahTranslationPatch.TryTranslatePopupMessage(source, route, family, out var proselytizationSifrahTranslated))
        {
            translated = proselytizationSifrahTranslated;
            return true;
        }

        if (RebukingSifrahTranslationPatch.TryTranslatePopupMessage(source, route, family, out var rebukingSifrahTranslated))
        {
            translated = rebukingSifrahTranslated;
            return true;
        }

        if (ExaminerTranslationPatch.TryTranslatePopupMessage(source, route, family, out var examinerTranslated))
        {
            translated = examinerTranslated;
            return true;
        }

        if (MutationsApiTranslationPatch.TryTranslatePopupMessage(source, route, family, out var mutationTranslated))
        {
            translated = mutationTranslated;
            return true;
        }

        if (QudMutationsModuleWindowVariantPopupTranslationPatch.TryTranslatePopupMessage(
                source,
                route,
                family,
                out var mutationVariantTranslated))
        {
            translated = mutationVariantTranslated;
            return true;
        }

        if (BaseMutationSelectVariantPopupTranslationPatch.TryTranslatePopupMessage(
                source,
                route,
                family,
                out var baseMutationVariantTranslated))
        {
            translated = baseMutationVariantTranslated;
            return true;
        }

        if (QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.TryTranslatePopupMessage(
                source,
                route,
                family,
                out var mutationMenuOptionTranslated))
        {
            translated = mutationMenuOptionTranslated;
            return true;
        }

        if (LevelerTranslationPatch.TryTranslatePopupMessage(source, route, family, out var levelerTranslated))
        {
            translated = levelerTranslated;
            return true;
        }

        if (StatusScreenPopupTranslationPatch.TryTranslatePopupMessage(source, route, family, out var statusScreenTranslated))
        {
            translated = statusScreenTranslated;
            return true;
        }

        if (WaterRitualTextTranslator.TryTranslateReputationMessage(
                source,
                route,
                family + ".WaterRitualReputation",
                out var waterRitualReputationTranslated))
        {
            translated = waterRitualReputationTranslated;
            return true;
        }

        if (WaterRitualTextTranslator.TryTranslateMessage(
                source,
                route,
                family + ".WaterRitual",
                out var waterRitualTranslated))
        {
            translated = waterRitualTranslated;
            return true;
        }

        if (CampfireCookFromIngredientsTranslationPatch.TryTranslatePopupProducerText(
                source,
                route,
                family,
                out var campfireCookFromIngredientsTranslated))
        {
            translated = campfireCookFromIngredientsTranslated;
            return true;
        }

        if (IsAlreadyLocalizedPopupTextCore(stripped))
        {
            translated = source;
            return true;
        }

        if (CampfireCookFromRecipeTranslationPatch.TryTranslatePopupProducerText(
                source,
                route,
                family,
                out var campfireCookFromRecipeTranslated))
        {
            translated = campfireCookFromRecipeTranslated;
            return true;
        }

        if (CampfireNostrumsTranslationPatch.TryTranslatePopupProducerText(
                source,
                route,
                family,
                out var campfireNostrumsTranslated))
        {
            translated = campfireNostrumsTranslated;
            return true;
        }

        if (TryTranslatePopupPickOptionHotkeyLabel(
                source,
                stripped,
                spans,
                route,
                family,
                popupId,
                out var hotkeyLabelTranslated))
        {
            translated = hotkeyLabelTranslated;
            return true;
        }

        if (TryTranslatePlainPopupMenuItemText(
                source,
                stripped,
                spans,
                route,
                family,
                popupId,
                out var plainPopupMenuItemTranslated))
        {
            translated = plainPopupMenuItemTranslated;
            return true;
        }

        if (TryTranslateRenameItemTitle(source, stripped, spans, route, family, out var renameItemTitleTranslated))
        {
            translated = renameItemTitleTranslated;
            return true;
        }

        if (TryTranslateEnergyCellSocketPickerText(source, stripped, spans, route, family, out var cellSocketTranslated))
        {
            translated = cellSocketTranslated;
            return true;
        }

        if (TryTranslateUntilCalendarTimeOfDay(source, stripped, spans, route, family, out var untilTranslated))
        {
            translated = untilTranslated;
            return true;
        }

        if (TryTranslateStatusOptionToggle(source, route, family, out var statusOptionTranslated))
        {
            translated = statusOptionTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                source,
                route,
                family + ".XRLCoreHPWarning",
                XRLCoreHPWarningPattern,
                "{{R|Your health has dropped below {{C|{0}%}}!}}",
                Array.Empty<ColorSpan>(),
                out var xrlCoreHpWarningTranslated))
        {
            translated = xrlCoreHpWarningTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreOutOfRange",
                XRLCoreOutOfRangePattern,
                "That is out of range! ({0} squares)",
                spans,
                out var xrlCoreOutOfRangeTranslated))
        {
            translated = xrlCoreOutOfRangeTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreTargetOutOfRange",
                XRLCoreTargetOutOfRangePattern,
                "That target is out of range! ({0} squares)",
                spans,
                out var xrlCoreTargetOutOfRangeTranslated))
        {
            translated = xrlCoreTargetOutOfRangeTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".DiscoverLocation",
                DiscoverLocationPattern,
                "You discover {0}!",
                spans,
                out var discoverLocationTranslated))
        {
            translated = discoverLocationTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".DiscoveredLocation",
                DiscoveredLocationPattern,
                "You discovered {0}.",
                spans,
                out var discoveredLocationTranslated))
        {
            translated = discoveredLocationTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".DiscoverHiddenExaminer",
                DiscoverHiddenExaminerPattern,
                "You discover something about {0} that was hidden!",
                spans,
                out var discoverHiddenExaminerTranslated))
        {
            translated = discoverHiddenExaminerTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".TemporalFugueDuplicateImpossible",
                TemporalFugueDuplicateImpossiblePattern,
                "It is impossible to duplicate {0}.",
                spans,
                translateValueAsDisplayName: true,
                out var temporalFugueDuplicateTranslated))
        {
            translated = temporalFugueDuplicateTranslated;
            return true;
        }

        if (TryTranslateQuestReceived(
                stripped,
                route,
                family + ".QuestReceived",
                spans,
                out var questReceivedTranslated))
        {
            translated = questReceivedTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".EndGameConfirm",
                EndGameConfirmPattern,
                "End game?\n\nType {0} to confirm.",
                spans,
                out var endGameConfirmTranslated))
        {
            translated = endGameConfirmTranslated;
            return true;
        }

        if (string.Equals(route, nameof(PopupShowTranslationPatch), StringComparison.Ordinal))
        {
            if (TryTranslatePhysicsAttackConfirm(
                    stripped,
                    route,
                    family + ".PhysicsAttackConfirm",
                    spans,
                    out var physicsAttackConfirmTranslated))
            {
                translated = physicsAttackConfirmTranslated;
                return true;
            }

            if (TryTranslateConversationRefusal(
                    stripped,
                    route,
                    family + ".ConversationRefusal",
                    spans,
                    out var conversationRefusalTranslated))
            {
                translated = conversationRefusalTranslated;
                return true;
            }
        }

        if (!string.Equals(source, stripped, StringComparison.Ordinal)
            && StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exactSource)
            && !string.Equals(exactSource, source, StringComparison.Ordinal))
        {
            translated = exactSource;
            DynamicTextObservability.RecordTransform(route, family + ".ExactSource", source, translated);
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(stripped, out var exact)
            && !string.Equals(exact, stripped, StringComparison.Ordinal))
        {
            translated = spans.Count == 0
                ? exact
                : ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
                    exact,
                    spans,
                    stripped);
            DynamicTextObservability.RecordTransform(route, family + ".Exact", source, translated);
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".DeleteSavePrompt",
                DeleteSavePromptPattern,
                "Are you sure you want to delete the save game for {0}?",
                spans,
                out var promptTranslated))
        {
            translated = promptTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".DeleteTitle",
                DeleteTitlePattern,
                "Delete {0}",
                spans,
                out var titleTranslated))
        {
            translated = titleTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".DuplicateBuildCode",
                DuplicateBuildCodePattern,
                "That code is already in your library. It's named {0}.",
                spans,
                out var duplicateBuildCodeTranslated))
        {
            translated = duplicateBuildCodeTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".ManageBuildTitle",
                ManageBuildTitlePattern,
                "Manage Build: {0}",
                spans,
                out var manageBuildTitleTranslated))
        {
            translated = manageBuildTitleTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".GenderCustomizeNamePrompt",
                GenderCustomizeNamePromptPattern,
                "What name should be used for your {0}? (Male, female, etc.)",
                spans,
                TranslateGenderCustomizeNamePromptValue,
                out var genderCustomizePromptTranslated))
        {
            translated = genderCustomizePromptTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".SifrahChosenCorrect",
                SifrahChosenCorrectPattern,
                "You have already chosen the correct option for {0}.",
                spans,
                out var sifrahChosenCorrectTranslated))
        {
            translated = sifrahChosenCorrectTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".SifrahUseWhich",
                SifrahUseWhichPattern,
                "Use which option for {0}?",
                spans,
                out var sifrahUseWhichTranslated))
        {
            translated = sifrahUseWhichTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".SifrahEliminated",
                SifrahEliminatedPattern,
                "You have already eliminated {0} as a possibility.",
                spans,
                out var sifrahEliminatedTranslated))
        {
            translated = sifrahEliminatedTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".SifrahDisabled",
                SifrahDisabledPattern,
                "Choosing {0} is disabled for this turn.",
                spans,
                out var sifrahDisabledTranslated))
        {
            translated = sifrahDisabledTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".SifrahInsight",
                SifrahInsightPattern,
                "You have gained insight into {0}. In a future Sifrah task of this kind, you can use this insight to determine which of your game options are not correct for any requirement. This will expend your insight, unless there are no such options.",
                spans,
                out var sifrahInsightTranslated))
        {
            translated = sifrahInsightTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreFlee",
                XRLCoreFleePattern,
                "You can't find a way to flee from {0}.",
                spans,
                out var xrlCoreFleeTranslated))
        {
            translated = xrlCoreFleeTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreReach",
                XRLCoreReachPattern,
                "You can't find a way to reach {0}.",
                spans,
                out var xrlCoreReachTranslated))
        {
            translated = xrlCoreReachTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreAutoattack",
                XRLCoreAutoattackPattern,
                "You do not autoattack {0} because it is not hostile to you.",
                spans,
                out var xrlCoreAutoattackTranslated))
        {
            translated = xrlCoreAutoattackTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreReload",
                XRLCoreReloadPattern,
                "You need to reload! ({0})",
                spans,
                out var xrlCoreReloadTranslated))
        {
            translated = xrlCoreReloadTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".XRLCoreOldSave",
                XRLCoreOldSavePattern,
                "That save file looks like it's from an older save format revision ({0}). Sorry!\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.",
                spans,
                out var xrlCoreOldSaveTranslated))
        {
            translated = xrlCoreOldSaveTranslated;
            return true;
        }

        if (TryTranslateGameInfoBlock(
                stripped,
                route,
                family + ".XRLCoreGameInfo",
                spans,
                out var xrlCoreGameInfoTranslated))
        {
            translated = xrlCoreGameInfoTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireStaunchPassThrough",
                CampfireStaunchPassThroughPattern,
                "You try to staunch the wounds of {0}, but your limbs pass through them.",
                spans,
                out var campfireStaunchPassThroughTranslated))
        {
            translated = campfireStaunchPassThroughTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireStaunchCannotAffect",
                CampfireStaunchCannotAffectPattern,
                "You try to staunch the wounds of {0}, but cannot affect them.",
                spans,
                out var campfireStaunchCannotAffectTranslated))
        {
            translated = campfireStaunchCannotAffectTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireStaunchPartial",
                CampfireStaunchPartialPattern,
                "You staunch the wounds of {0}, though some are too deep to treat.",
                spans,
                out var campfireStaunchPartialTranslated))
        {
            translated = campfireStaunchPartialTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireStaunchFull",
                CampfireStaunchFullPattern,
                "You staunch the wounds of {0}.",
                spans,
                out var campfireStaunchFullTranslated))
        {
            translated = campfireStaunchFullTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireWoundsTooDeep",
                CampfireWoundsTooDeepPattern,
                "{0} are too deep to treat.",
                spans,
                out var campfireWoundsTooDeepTranslated))
        {
            translated = campfireWoundsTooDeepTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireNeitherBleeding",
                CampfireNeitherBleedingPattern,
                "Neither you nor {0} are bleeding.",
                spans,
                out var campfireNeitherBleedingTranslated))
        {
            translated = campfireNeitherBleedingTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfireNoMedicinal",
                CampfireNoMedicinalPattern,
                "You have no medicinal ingredients with which to treat the poison coursing through {0}.",
                spans,
                out var campfireNoMedicinalTranslated))
        {
            translated = campfireNoMedicinalTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfirePoisonPassThrough",
                CampfirePoisonPassThroughPattern,
                "You try to cure the poison coursing through {0}, but your limbs pass through them.",
                spans,
                out var campfirePoisonPassThroughTranslated))
        {
            translated = campfirePoisonPassThroughTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfirePoisonCannotAffect",
                CampfirePoisonCannotAffectPattern,
                "You try to cure the poison coursing through {0}, but cannot affect them.",
                spans,
                out var campfirePoisonCannotAffectTranslated))
        {
            translated = campfirePoisonCannotAffectTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".CampfirePoisonIneffective",
                CampfirePoisonIneffectivePattern,
                "You try to cure the poison coursing through {0}, but your cures are ineffective.",
                spans,
                out var campfirePoisonIneffectiveTranslated))
        {
            translated = campfirePoisonIneffectiveTranslated;
            return true;
        }

        if (TryTranslateCampfireCurePoison(
                stripped,
                route,
                family + ".CampfireCurePoison",
                spans,
                out var campfireCurePoisonTranslated))
        {
            translated = campfireCurePoisonTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                source,
                route,
                family + ".CampfireNewRecipe",
                CampfireNewRecipePattern,
                "You create a new recipe for {{|{0}}}!",
                Array.Empty<ColorSpan>(),
                out var campfireNewRecipeTranslated))
        {
            translated = campfireNewRecipeTranslated;
            return true;
        }

        if (TryTranslateSinglePlaceholderTemplate(
                source,
                route,
                family + ".CampfireMetabolize",
                CampfireMetabolizePattern,
                "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|{0}}}",
                Array.Empty<ColorSpan>(),
                out var campfireMetabolizeTranslated))
        {
            translated = campfireMetabolizeTranslated;
            return true;
        }

        if (TryTranslateCampfirePreserve(
                stripped,
                route,
                family + ".CampfirePreserve",
                spans,
                out var campfirePreserveTranslated))
        {
            translated = campfirePreserveTranslated;
            return true;
        }

        if (TryTranslateWaterRitualLowReputation(
                stripped,
                route,
                family + ".WaterRitualLowReputation",
                spans,
                out var waterRitualLowReputationTranslated))
        {
            translated = waterRitualLowReputationTranslated;
            return true;
        }

        if (JournalNotificationTranslator.TryTranslate(
                source,
                route,
                family + ".JournalNotification",
                out var journalNotificationTranslated))
        {
            translated = journalNotificationTranslated;
            return true;
        }

        if (ShouldTryMessagePatternFallback(route))
        {
            var patternTranslated = MessagePatternTranslator.TranslateIfPatternMatches(source, route);
            if (!string.Equals(patternTranslated, source, StringComparison.Ordinal))
            {
                translated = patternTranslated;
                DynamicTextObservability.RecordTransform(route, family + ".Pattern", source, translated);
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePlainPopupMenuItemText(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string? popupId,
        out string translated)
    {
        translated = source;
        if (!string.Equals(family, "Popup.ProducerMenuItem", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(stripped)
            || source.Contains("{{hotkey|"))
        {
            return false;
        }

        var translatedLabel = TranslatePopupMenuItemLabel(stripped, popupId, spans, null, 0);
        if (translatedLabel is null)
        {
            return TryAcceptInventoryActionMenuOwnerMiss(source, route, family, popupId, out translated);
        }

        if (string.Equals(translatedLabel, stripped, StringComparison.Ordinal))
        {
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedLabel,
            spans,
            stripped.Length);
        DynamicTextObservability.RecordTransform(route, family + ".PlainMenuItem", source, translated);
        return true;
    }

    private static bool TryTranslateRenameItemTitle(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        translated = source;
        var match = RenameItemTitlePattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var targetGroup = match.Groups["target"];
        var targetPrefixLength = targetGroup.Value.StartsWith("your ", StringComparison.Ordinal)
            ? "your ".Length
            : 0;
        var target = RestoreNestedVisibleSlice(
            targetGroup,
            targetPrefixLength,
            targetGroup.Length - targetPrefixLength,
            spans);
        var translatedTarget = ColorAwareTranslationComposer.TranslatePreservingColors(
            target,
            visible => GetDisplayNameRouteTranslator.TranslatePreservingColors(
                visible,
                nameof(PopupTranslationPatch)));
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedTarget + "の名前を変更する。",
            spans,
            stripped.Length);
        DynamicTextObservability.RecordTransform(route, family + ".RenameItemTitle", source, translated);
        return true;
    }

    private static bool TryTranslatePopupPickOptionHotkeyLabel(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string? popupId,
        out string translated)
    {
        translated = source;
        var isBottomContextRoute = string.Equals(route, nameof(QudMenuBottomContextTranslationPatch), StringComparison.Ordinal);
        if (!string.Equals(route, nameof(PopupPickOptionTranslationPatch), StringComparison.Ordinal)
            && !isBottomContextRoute
            && !string.Equals(route, nameof(SelectableTextMenuItemTranslationPatch), StringComparison.Ordinal)
            && !string.Equals(route, nameof(PopupMessageTranslationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        var hotkeyMatch = HotkeyLabelPattern.Match(stripped);
        if (hotkeyMatch.Success && !int.TryParse(hotkeyMatch.Groups["hotkey"].Value, out _))
        {
            var labelGroup = hotkeyMatch.Groups["label"];
            var label = labelGroup.Value;
            var translatedLabel = TranslatePopupMenuItemLabel(label, popupId, spans, labelGroup);
            if (translatedLabel is null)
            {
                return TryAcceptInventoryActionMenuOwnerMiss(source, route, family, popupId, out translated);
            }

            if (string.Equals(translatedLabel, label, StringComparison.Ordinal))
            {
                return false;
            }

            var hotkeySourceLength = hotkeyMatch.Groups["hotkey"].Length + 2;
            var hotkey = ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
                "[" + hotkeyMatch.Groups["hotkey"].Value + "]",
                spans,
                hotkeyMatch.Index,
                hotkeySourceLength);
            var labelSpans = WithoutLegacyDisabledInventoryActionColor(spans, hotkeyMatch.Groups["label"].Index);
            var labelWithWrappers = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                translatedLabel,
                labelSpans,
                hotkeyMatch.Groups["label"]);
            labelWithWrappers = RestoreLegacyDisabledInventoryActionColor(
                labelWithWrappers,
                spans,
                hotkeyMatch.Groups["label"]);
            var visibleTranslation = hotkey + " " + labelWithWrappers;
            if (isBottomContextRoute)
            {
                translated = visibleTranslation;
                DynamicTextObservability.RecordTransform(route, family + ".HotkeyLabel", source, translated);
                return true;
            }

            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                visibleTranslation,
                spans,
                stripped.Length);
            DynamicTextObservability.RecordTransform(route, family + ".HotkeyLabel", source, translated);
            return true;
        }

        // TryTranslatePlainInventoryActionMenuLabel owns InventoryActionMenu labels
        // without inline {{hotkey|...}} markup; embedded hotkey rows continue below.
        if (TryTranslatePlainInventoryActionMenuLabel(
                source,
                stripped,
                spans,
                route,
                family,
                popupId,
                out var plainInventoryActionTranslated))
        {
            translated = plainInventoryActionTranslated;
            return true;
        }

        if (source.IndexOf("{{hotkey|", StringComparison.Ordinal) < 0
            || !EmbeddedHotkeyLabelPattern.IsMatch(stripped))
        {
            return false;
        }

        var embeddedTranslated = TranslatePopupMenuItemLabel(stripped, popupId, spans, null, 0);
        if (embeddedTranslated is null)
        {
            return TryAcceptInventoryActionMenuOwnerMiss(source, route, family, popupId, out translated);
        }

        if (string.Equals(embeddedTranslated, stripped, StringComparison.Ordinal))
        {
            return false;
        }

        if (!embeddedTranslated.StartsWith("{{hotkey|", StringComparison.Ordinal) && TryGetEmbeddedHotkeyMarker(stripped, spans, labelGroup: null, labelStart: 0, out var embeddedHotkeyMarker))
        {
            embeddedTranslated = embeddedHotkeyMarker + embeddedTranslated;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            embeddedTranslated,
            spans,
            stripped.Length);
        DynamicTextObservability.RecordTransform(route, family + ".EmbeddedHotkeyLabel", source, translated);
        return true;
    }

    private static bool TryTranslatePlainInventoryActionMenuLabel(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string? popupId,
        out string translated)
    {
        translated = source;
        if (!IsInventoryActionMenuPopup(popupId) || source.Contains("{{hotkey|"))
        {
            return false;
        }

        var labelStart = 0;
        while (labelStart < stripped.Length && stripped[labelStart] == ' ')
        {
            labelStart++;
        }

        if (labelStart >= stripped.Length)
        {
            return false;
        }

        var label = stripped.Substring(labelStart);
        var translatedLabel = TranslatePopupMenuItemLabel(label, popupId, spans, null, labelStart);
        if (translatedLabel is null)
        {
            return TryAcceptInventoryActionMenuOwnerMiss(source, route, family, popupId, out translated);
        }

        if (string.Equals(translatedLabel, label, StringComparison.Ordinal))
        {
            return false;
        }

        var hasLegacyDisabledColor = HasLegacyDisabledInventoryActionColor(spans, labelStart);
        var visibleTranslation = stripped.Substring(0, labelStart) + translatedLabel;
        translated = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            visibleTranslation,
            hasLegacyDisabledColor ? WithoutLegacyDisabledInventoryActionColor(spans, labelStart) : spans,
            stripped);
        if (hasLegacyDisabledColor
            && !ColorAwareTranslationComposer.StartsWithQudTokenAtVisibleIndex(translated, labelStart, "&K"))
        {
            translated = ColorAwareTranslationComposer.InsertQudColorAtVisibleIndex(translated, labelStart, "&K");
        }

        DynamicTextObservability.RecordTransform(route, family + ".PlainInventoryActionLabel", source, translated);
        return true;
    }

    private static string RestoreLegacyDisabledInventoryActionColor(
        string translatedLabel,
        IReadOnlyList<ColorSpan> spans,
        Group labelGroup)
    {
        return RestoreLegacyDisabledInventoryActionColor(translatedLabel, spans, labelGroup.Index);
    }

    private static string RestoreLegacyDisabledInventoryActionColor(
        string translatedLabel,
        IReadOnlyList<ColorSpan> spans,
        int labelStart)
    {
        if (ColorAwareTranslationComposer.StartsWithQudTokenAtVisibleIndex(translatedLabel, 0, "&K")
            || !HasLegacyDisabledInventoryActionColor(spans, labelStart))
        {
            return translatedLabel;
        }

        return ColorAwareTranslationComposer.InsertQudColorAfterOpeningBoundaryWrappers(translatedLabel, "&K");
    }

    private static bool HasLegacyDisabledInventoryActionColor(IReadOnlyList<ColorSpan> spans, int labelStart)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == labelStart
                && string.Equals(span.Token, "&K", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<ColorSpan> WithoutLegacyDisabledInventoryActionColor(
        IReadOnlyList<ColorSpan> spans,
        int labelStart)
    {
        List<ColorSpan>? filtered = null;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == labelStart
                && string.Equals(span.Token, "&K", StringComparison.Ordinal))
            {
                if (filtered is null)
                {
                    filtered = new List<ColorSpan>(spans.Count - 1);
                    for (var earlierIndex = 0; earlierIndex < index; earlierIndex++)
                    {
                        filtered.Add(spans[earlierIndex]);
                    }
                }

                continue;
            }

            filtered?.Add(span);
        }

        return filtered ?? spans;
    }

    private static bool TryAcceptInventoryActionMenuOwnerMiss(
        string source,
        string route,
        string family,
        string? popupId,
        out string translated)
    {
        translated = source;
        if (!IsInventoryActionMenuPopup(popupId))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            family + ".InventoryActionOwnerMiss",
            source,
            translated,
            logWhenUnchanged: true);
        return true;
    }

    private static string? TranslatePopupMenuItemLabel(
        string label,
        string? popupId,
        IReadOnlyList<ColorSpan>? spans,
        Group? labelGroup,
        int? labelStart = null)
    {
        if (IsInventoryActionMenuPopup(popupId))
        {
            var inventoryActionTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                label,
                InventoryActionContext,
                InventoryActionDictionaryFile);
            if (inventoryActionTranslation is not null)
            {
                return inventoryActionTranslation;
            }

            if (TryTranslateInventoryActionMenuLabelPattern(
                    label,
                    spans,
                    labelGroup,
                    labelStart,
                    out var inventoryActionPatternTranslation))
            {
                return inventoryActionPatternTranslation;
            }

            return ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, CommonMenuActionDictionaryFile);
        }

        var qudMenuItemTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            label,
            QudMenuItemContext,
            QudMenuItemDictionaryFile);
        if (qudMenuItemTranslation is not null)
        {
            return qudMenuItemTranslation;
        }

        if (TryTranslateRandomNameCultureOption(label, spans, labelGroup, labelStart, out var randomCultureTranslated))
        {
            return randomCultureTranslated;
        }

        if (CampfireCookingActionLabels.Contains(label))
        {
            var campfireActionTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                label,
                InventoryActionContext,
                InventoryActionDictionaryFile);
            if (campfireActionTranslation is not null)
            {
                return campfireActionTranslation;
            }
        }

        return ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, CommonMenuActionDictionaryFile);
    }

    private static bool TryTranslateRandomNameCultureOption(
        string label,
        IReadOnlyList<ColorSpan>? spans,
        Group? labelGroup,
        int? labelStart,
        out string translated)
    {
        translated = label;
        var match = RandomNameCultureOptionPattern.Match(label);
        if (!match.Success)
        {
            return false;
        }

        var culture = NormalizeCulturePossessive(match.Groups["culture"].Value);
        if (spans is not null && labelGroup is not null)
        {
            culture = RestoreNestedVisibleSlice(labelGroup, match.Groups["culture"], spans);
            culture = NormalizeCulturePossessive(culture);
        }
        else if (spans is not null && labelStart.HasValue)
        {
            culture = RestoreNestedVisibleSlice(
                label,
                match.Groups["culture"].Index,
                match.Groups["culture"].Length,
                labelStart.Value,
                spans);
            culture = NormalizeCulturePossessive(culture);
        }

        translated = culture + "文化からランダムな名前を選ぶ。";
        return true;
    }

    private static string NormalizeCulturePossessive(string culture)
    {
        if (culture.EndsWith("'s", StringComparison.Ordinal))
        {
            return culture.Substring(0, culture.Length - 2) + "の";
        }

        if (culture.EndsWith("'", StringComparison.Ordinal))
        {
            return culture.Substring(0, culture.Length - 1) + "の";
        }

        return culture;
    }

    private static bool TryTranslateInventoryActionMenuLabelPattern(
        string label,
        IReadOnlyList<ColorSpan>? spans,
        Group? labelGroup,
        int? labelStart,
        out string translated)
    {
        translated = label;
        var cleanAllItemsMatch = InventoryActionCleanAllItemsPattern.Match(label);
        if (cleanAllItemsMatch.Success)
        {
            translated = "手持ちのアイテムをすべて洗う ["
                + cleanAllItemsMatch.Groups["amount"].Value
                + "ドラム]";
            return true;
        }

        var rechargeMatch = InventoryActionRechargeCellPattern.Match(label);
        if (rechargeMatch.Success)
        {
            var cell = rechargeMatch.Groups["cell"].Value;
            if (spans is not null && labelGroup is not null)
            {
                cell = RestoreNestedVisibleSlice(labelGroup, rechargeMatch.Groups["cell"], spans);
            }
            else if (spans is not null && labelStart.HasValue)
            {
                cell = RestoreNestedVisibleSlice(
                    label,
                    rechargeMatch.Groups["cell"].Index,
                    rechargeMatch.Groups["cell"].Length,
                    labelStart.Value,
                    spans);
            }

            cell = RemoveUnmatchedQudClosings(cell);
            var translatedCell = ColorAwareTranslationComposer.TranslatePreservingColors(
                cell,
                visible => GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    visible,
                    nameof(PopupTranslationPatch)));
            translated = AppendDefaultColorAfterInlineColor(translatedCell) + "を充電する";
            if (TryGetLeadingEmbeddedHotkeyMarker(label, spans, labelGroup, labelStart, out var hotkeyMarker))
            {
                translated = hotkeyMarker + translated;
            }

            return true;
        }

        const string eatPrefix = "Eat ";
        if (!label.StartsWith(eatPrefix, StringComparison.Ordinal) || label.Length == eatPrefix.Length)
        {
            return false;
        }

        var mealStart = eatPrefix.Length;
        var mealEnd = label.Length;
        while (mealStart < mealEnd && char.IsWhiteSpace(label[mealStart]))
        {
            mealStart++;
        }

        while (mealEnd > mealStart && char.IsWhiteSpace(label[mealEnd - 1]))
        {
            mealEnd--;
        }

        if (mealEnd == mealStart)
        {
            return false;
        }

        if (label[mealEnd - 1] == '.')
        {
            mealEnd--;
            while (mealEnd > mealStart && char.IsWhiteSpace(label[mealEnd - 1]))
            {
                mealEnd--;
            }
        }

        var meal = label.Substring(mealStart, mealEnd - mealStart);
        if (spans is not null && labelGroup is not null)
        {
            meal = RestoreNestedVisibleSlice(labelGroup, mealStart, mealEnd - mealStart, spans);
        }
        else if (spans is not null && labelStart.HasValue)
        {
            meal = RestoreNestedVisibleSlice(label, mealStart, mealEnd - mealStart, labelStart.Value, spans);
        }

        var translatedMeal = TranslateCookingRecipeNameForInventoryActionMenu(meal);
        if (translatedMeal.Length == 0
            || (string.Equals(translatedMeal, meal, StringComparison.Ordinal) && ContainsVisibleAsciiLetters(meal)))
        {
            return false;
        }

        translated = translatedMeal + "を食べる";
        return true;
    }

    private static bool TryGetLeadingEmbeddedHotkeyMarker(
        string label,
        IReadOnlyList<ColorSpan>? spans,
        Group? labelGroup,
        int? labelStart,
        out string marker)
    {
        marker = string.Empty;
        if (label.Length == 0 || spans is null || spans.Count == 0)
        {
            return false;
        }

        var visibleStart = GetVisibleStart(labelGroup, labelStart);
        if (!HasColorSpanTokenAt(spans, visibleStart, "{{hotkey|")
            || !HasColorSpanTokenAt(spans, visibleStart + 1, "}}"))
        {
            return false;
        }

        marker = "{{hotkey|" + label[0] + "}}";
        return true;
    }

    private static bool TryGetEmbeddedHotkeyMarker(
        string label,
        IReadOnlyList<ColorSpan>? spans,
        Group? labelGroup,
        int? labelStart,
        out string marker)
    {
        marker = string.Empty;
        if (label.Length == 0 || spans is null || spans.Count == 0)
        {
            return false;
        }

        var visibleStart = GetVisibleStart(labelGroup, labelStart);
        var visibleEnd = visibleStart + label.Length;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < visibleStart
                || span.Index >= visibleEnd
                || !string.Equals(span.Token, "{{hotkey|", StringComparison.Ordinal))
            {
                continue;
            }

            var relativeIndex = span.Index - visibleStart;
            if (!HasColorSpanTokenAt(spans, span.Index + 1, "}}"))
            {
                continue;
            }

            marker = "{{hotkey|" + label[relativeIndex] + "}}";
            return true;
        }

        return false;
    }

    private static int GetVisibleStart(Group? labelGroup, int? labelStart)
    {
        return labelGroup is { Success: true }
            ? labelGroup.Index
            : labelStart ?? 0;
    }

    private static bool HasColorSpanTokenAt(IReadOnlyList<ColorSpan> spans, int index, string token)
    {
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            if (span.Index == index
                && string.Equals(span.Token, token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string RestoreNestedVisibleSlice(Group parentGroup, Group childGroup, IReadOnlyList<ColorSpan> spans)
    {
        if (!parentGroup.Success || !childGroup.Success || spans.Count == 0)
        {
            return childGroup.Value;
        }

        return RestoreNestedVisibleSlice(parentGroup, childGroup.Index, childGroup.Length, spans);
    }

    private static string RestoreNestedVisibleSlice(
        Group parentGroup,
        int childStartIndex,
        int childLength,
        IReadOnlyList<ColorSpan> spans)
    {
        if (!parentGroup.Success || childLength < 0 || spans.Count == 0)
        {
            return parentGroup.Success && childLength >= 0
                ? parentGroup.Value.Substring(childStartIndex, childLength)
                : string.Empty;
        }

        return RestoreNestedVisibleSlice(parentGroup.Value, childStartIndex, childLength, parentGroup.Index, spans);
    }

    private static string RestoreNestedVisibleSlice(
        string parentValue,
        int childStartIndex,
        int childLength,
        int parentStartIndex,
        IReadOnlyList<ColorSpan> spans)
    {
        if (childLength < 0 || spans.Count == 0)
        {
            return childLength >= 0
                ? parentValue.Substring(childStartIndex, childLength)
                : string.Empty;
        }

        var startIndex = parentStartIndex + childStartIndex;
        var captureSpans = WithoutUnmatchedBoundaryClosings(
            ColorCodePreserver.SliceSpans(spans, startIndex, childLength));
        return ColorAwareTranslationComposer.Restore(
            parentValue.Substring(childStartIndex, childLength),
            captureSpans);
    }

    private static List<ColorSpan> WithoutUnmatchedBoundaryClosings(List<ColorSpan> spans)
    {
        if (spans.Count == 0)
        {
            return spans;
        }

        List<ColorSpan>? filtered = null;
        var openBoundaryCount = 0;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                openBoundaryCount++;
            }
            else if (ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                if (openBoundaryCount == 0)
                {
                    if (filtered is null)
                    {
                        filtered = new List<ColorSpan>(spans.Count - 1);
                        for (var earlierIndex = 0; earlierIndex < index; earlierIndex++)
                        {
                            filtered.Add(spans[earlierIndex]);
                        }
                    }

                    continue;
                }

                openBoundaryCount--;
            }

            filtered?.Add(span);
        }

        return filtered ?? spans;
    }

    private static string TranslateCookingRecipeNameForInventoryActionMenu(string meal)
    {
        var (strippedMeal, mealSpans) = ColorAwareTranslationComposer.Strip(meal);
        var sourceDisplayName = "{{W|" + meal + "}}";
        if (CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
                sourceDisplayName,
                out var translatedDisplayName,
                out _)
            && !string.Equals(translatedDisplayName, sourceDisplayName, StringComparison.Ordinal))
        {
            var (translatedMeal, _) = ColorAwareTranslationComposer.Strip(translatedDisplayName);
            if (!string.IsNullOrEmpty(translatedMeal))
            {
                return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                    translatedMeal,
                    mealSpans,
                    strippedMeal.Length);
            }
        }

        var presetMeal = ScopedDictionaryLookup.TranslateExactOrLowerAscii(strippedMeal, PresetMealNameDictionaryFile);
        if (presetMeal is not null)
        {
            return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                presetMeal,
                mealSpans,
                strippedMeal.Length);
        }

        return meal;
    }

    private static bool TryTranslateEnergyCellSocketPickerText(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        translated = source;
        if (!string.Equals(route, nameof(PopupPickOptionTranslationPatch), StringComparison.Ordinal)
            && !string.Equals(route, nameof(SelectableTextMenuItemTranslationPatch), StringComparison.Ordinal)
            && !string.Equals(route, nameof(PopupMessageTranslationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        var titleMatch = EnergyCellSocketPickerTitlePattern.Match(stripped);
        if (titleMatch.Success)
        {
            var targetGroup = titleMatch.Groups["target"];
            var targetPrefixLength = targetGroup.Value.StartsWith("your ", StringComparison.Ordinal)
                ? "your ".Length
                : 0;
            var target = RestoreNestedVisibleSlice(
                targetGroup,
                targetPrefixLength,
                targetGroup.Length - targetPrefixLength,
                spans);
            target = RemoveUnmatchedQudClosings(target);

            var visibleTranslated = AppendDefaultColorAfterInlineColor(target) + "用のセルを選ぶ";
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                visibleTranslated,
                spans,
                stripped.Length);
            DynamicTextObservability.RecordTransform(route, family + ".EnergyCellSocketPickerTitle", source, translated);
            return true;
        }

        var removeCellMatch = EnergyCellSocketRemoveCellPattern.Match(stripped);
        if (removeCellMatch.Success)
        {
            const string rawRemovePrefix = "remove cell: ";
            if (source.StartsWith(rawRemovePrefix, StringComparison.Ordinal))
            {
                translated = "セルを外す: " + source.Substring(rawRemovePrefix.Length);
            }
            else
            {
                var visibleTranslated = "セルを外す: " + removeCellMatch.Groups["cell"].Value;
                translated = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
                    visibleTranslated,
                    spans,
                    stripped);
            }

            DynamicTextObservability.RecordTransform(route, family + ".EnergyCellSocketRemoveCell", source, translated);
            return true;
        }

        if (string.Equals(stripped, "disassemble cell", StringComparison.Ordinal))
        {
            var disassembleCell = ScopedDictionaryLookup.TranslateExactOrLowerAscii(stripped, CommonMenuActionDictionaryFile);
            if (disassembleCell is { Length: > 0 } && !string.Equals(disassembleCell, stripped, StringComparison.Ordinal))
            {
                translated = spans.Count == 0
                    ? disassembleCell
                    : ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
                        disassembleCell,
                        spans,
                        stripped);
                DynamicTextObservability.RecordTransform(route, family + ".EnergyCellSocketDisassembleCell", source, translated);
                return true;
            }
        }

        return false;
    }

    private static string AppendDefaultColorAfterInlineColor(string source)
    {
        return source.IndexOf('&') < 0
            ? source
            : source + "&y";
    }

    private static string RemoveUnmatchedQudClosings(string source)
    {
        if (source.IndexOf("}}", StringComparison.Ordinal) < 0)
        {
            return source;
        }

        var builder = new StringBuilder(source.Length);
        var openCount = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (index + 1 < source.Length
                && source[index] == '{'
                && source[index + 1] == '{')
            {
                openCount++;
                builder.Append("{{");
                index++;
                continue;
            }

            if (index + 1 < source.Length
                && source[index] == '}'
                && source[index + 1] == '}')
            {
                if (openCount > 0)
                {
                    openCount--;
                    builder.Append("}}");
                }

                index++;
                continue;
            }

            builder.Append(source[index]);
        }

        return builder.ToString();
    }

    private static bool ContainsAsciiLetters(string source)
    {
        return AsciiLetterPattern.IsMatch(source);
    }

    private static bool ContainsVisibleAsciiLetters(string source)
    {
        var (visible, _) = ColorAwareTranslationComposer.Strip(source);
        return ContainsAsciiLetters(visible);
    }

    internal static bool IsInventoryActionMenuPopup(string? popupId)
    {
        return popupId is not null
            && popupId.StartsWith(InventoryActionMenuPopupIdPrefix, StringComparison.Ordinal);
    }

    private static bool TryTranslateUntilCalendarTimeOfDay(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        if (!stripped.StartsWith(UntilPrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var timeOfDay = stripped.Substring(UntilPrefix.Length);
        if (string.IsNullOrEmpty(timeOfDay)
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(timeOfDay, out var translatedTimeOfDay))
        {
            translated = source;
            return false;
        }

        var visibleTranslated = "次の" + translatedTimeOfDay + "まで";
        translated = spans.Count == 0 ? visibleTranslated : ColorAwareTranslationComposer.Restore(visibleTranslated, spans);
        DynamicTextObservability.RecordTransform(route, family + ".UntilTimeOfDay", source, translated);
        return true;
    }

    private static bool ShouldTryMessagePatternFallback(string route)
    {
        return string.Equals(route, nameof(PopupShowTranslationPatch), StringComparison.Ordinal);
    }

    private static bool TryTranslateWaterRitualLowReputation(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = WaterRitualLowReputationPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "You don't have a high enough reputation with {0}.";
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var factionGroup = match.Groups["value"];
        var faction = WaterRitualTextTranslator.TranslateFactionVisible(factionGroup.Value);
        if (spans.Count > 0)
        {
            faction = ColorAwareTranslationComposer.RestoreCapture(faction, spans, factionGroup);
        }

        translated = translatedTemplate.Replace("{0}", faction);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateStatusOptionToggle(
        string source,
        string route,
        string family,
        out string translated)
    {
        if (TryTranslateBinaryOption(
                source,
                "Equipment View: ",
                "Paperdoll",
                "List",
                "装備表示：",
                "紙人形",
                "リスト",
                out translated)
            || TryTranslateBinaryOption(
                source,
                "Sort Mode: ",
                "Category",
                "A-Z",
                "ソート方式：",
                "カテゴリー",
                "A-Z",
                out translated)
            || TryTranslateBinaryOption(
                source,
                "Search Mode: ",
                "Strict",
                "Fuzzy",
                "検索方式：",
                "厳密",
                "あいまい",
                out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + ".StatusOptionToggle", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateBinaryOption(
        string source,
        string prefix,
        string left,
        string right,
        string translatedPrefix,
        string translatedLeft,
        string translatedRight,
        out string translated)
    {
        var selectedLeft = "{{W|" + left + "}}";
        var selectedRight = "{{W|" + right + "}}";
        var leftSelectedSource = prefix + selectedLeft + "/" + right;
        if (string.Equals(source, leftSelectedSource, StringComparison.Ordinal))
        {
            translated = translatedPrefix + "{{W|" + translatedLeft + "}}/" + translatedRight;
            return true;
        }

        var rightSelectedSource = prefix + left + "/" + selectedRight;
        if (string.Equals(source, rightSelectedSource, StringComparison.Ordinal))
        {
            translated = translatedPrefix + translatedLeft + "/{{W|" + translatedRight + "}}";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSinglePlaceholderTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        IReadOnlyList<ColorSpan> spans,
        bool translateValueAsDisplayName,
        Func<string, string>? translateValue,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var value = match.Groups["value"].Value;
        if (spans.Count > 0)
        {
            value = ColorAwareTranslationComposer.RestoreCapture(value, spans, match.Groups["value"]);
        }

        if (translateValueAsDisplayName)
        {
            value = DisplayNameCaptureTranslator.TranslatePreservingColors(value, nameof(PopupTranslationPatch));
        }

        if (translateValue is not null)
        {
            value = translateValue(value);
        }

        translated = translatedTemplate.Replace("{0}", value);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateSinglePlaceholderTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        IReadOnlyList<ColorSpan> spans,
        bool translateValueAsDisplayName,
        out string translated)
    {
        return TryTranslateSinglePlaceholderTemplate(
            source,
            route,
            family,
            pattern,
            templateKey,
            spans,
            translateValueAsDisplayName,
            translateValue: null,
            out translated);
    }

    private static bool TryTranslateSinglePlaceholderTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        return TryTranslateSinglePlaceholderTemplate(
            source,
            route,
            family,
            pattern,
            templateKey,
            spans,
            translateValueAsDisplayName: false,
            translateValue: null,
            out translated);
    }

    private static bool TryTranslateSinglePlaceholderTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        IReadOnlyList<ColorSpan> spans,
        Func<string, string> translateValue,
        out string translated)
    {
        return TryTranslateSinglePlaceholderTemplate(
            source,
            route,
            family,
            pattern,
            templateKey,
            spans,
            translateValueAsDisplayName: false,
            translateValue,
            out translated);
    }

    private static string TranslateGenderCustomizeNamePromptValue(string value)
    {
        return string.Equals(value.Trim(), "gender", StringComparison.OrdinalIgnoreCase)
            ? "ジェンダー"
            : value;
    }

    private static bool TryTranslateQuestReceived(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = QuestReceivedPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "You have received a new quest, {0}!";
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var quest = match.Groups["value"].Value;
        if (spans.Count > 0)
        {
            quest = ColorAwareTranslationComposer.RestoreCapture(quest, spans, match.Groups["value"]);
        }

        if (GeneratedQuestTitleTranslator.TryTranslatePreservingColors(quest, route, out var translatedQuest))
        {
            quest = translatedQuest;
        }

        translated = translatedTemplate.Replace("{0}", quest);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslatePhysicsAttackConfirm(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        if (!TryTranslatePhysicsAttackConfirmText(source, spans, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    internal static bool TryTranslatePhysicsAttackConfirmText(
        string source,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = PhysicsAttackConfirmPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = StringHelpers.StripLeadingEnglishArticle(
            match.Groups["value"].Value,
            includeCapitalizedDefiniteArticle: true);
        if (spans.Count > 0)
        {
            target = ColorAwareTranslationComposer.RestoreCapture(target, spans, match.Groups["value"]);
        }

        if (!TryTranslatePhysicsAttackConfirmTarget(target, out var translatedTarget))
        {
            translated = source;
            return false;
        }

        translated = "本当に" + translatedTarget + "を攻撃しますか？";
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        return true;
    }

    private static bool TryTranslatePhysicsAttackConfirmTarget(string target, out string translated)
    {
        try
        {
            translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(target, nameof(PopupTranslationPatch));
            return true;
        }
        catch (System.IO.DirectoryNotFoundException ex)
        {
            Trace.TraceError(
                "QudJP: PopupTranslationPatch.GetDisplayNameRouteTranslator.TranslatePreservingColors target translation failed: {0}",
                ex);
            if (ColorAwareTranslationComposer.HasColorMarkup(target))
            {
                translated = target;
                return true;
            }

            translated = target;
            return false;
        }
    }

    private static bool TryTranslateConversationRefusal(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = ConversationRefusalPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = StringHelpers.StripLeadingEnglishArticle(
            match.Groups["value"].Value,
            includeCapitalizedDefiniteArticle: true);
        if (spans.Count > 0)
        {
            target = ColorAwareTranslationComposer.RestoreCapture(target, spans, match.Groups["value"]);
        }

        translated = target + "はあなたと話そうとしない。";
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateCampfireCurePoison(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = CampfireCurePoisonPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "You cure the {0} coursing through {1} with a balm made from {2}.";
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var poison = TranslateCampfirePoisonToken(match.Groups["poison"].Value);
        var target = match.Groups["target"].Value;
        var ingredient = match.Groups["ingredient"].Value;
        if (spans.Count > 0)
        {
            poison = ColorAwareTranslationComposer.RestoreCapture(poison, spans, match.Groups["poison"]);
            target = ColorAwareTranslationComposer.RestoreCapture(target, spans, match.Groups["target"]);
            ingredient = ColorAwareTranslationComposer.RestoreCapture(ingredient, spans, match.Groups["ingredient"]);
        }

        translated = translatedTemplate
            .Replace("{0}", poison)
            .Replace("{1}", target)
            .Replace("{2}", ingredient);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateCampfirePreserve(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = CampfirePreservePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "{0}: how many do you want to preserve? (max = {1})";
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var item = match.Groups["item"].Value;
        var max = match.Groups["max"].Value;
        if (spans.Count > 0)
        {
            item = ColorAwareTranslationComposer.RestoreCapture(item, spans, match.Groups["item"]);
            max = ColorAwareTranslationComposer.RestoreCapture(max, spans, match.Groups["max"]);
        }

        translated = translatedTemplate
            .Replace("{0}", item)
            .Replace("{1}", max);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateGameInfoBlock(
        string source,
        string route,
        string family,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = XRLCoreGameInfoPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "\n\n           {0} mode.\n\n           Turn {1}\n\n          World seed: {2}     \n\n\n   ";
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var mode = match.Groups["mode"].Value;
        var turn = match.Groups["turn"].Value;
        var seed = match.Groups["seed"].Value;
        if (spans.Count > 0)
        {
            mode = ColorAwareTranslationComposer.RestoreCapture(mode, spans, match.Groups["mode"]);
            turn = ColorAwareTranslationComposer.RestoreCapture(turn, spans, match.Groups["turn"]);
            seed = ColorAwareTranslationComposer.RestoreCapture(seed, spans, match.Groups["seed"]);
        }

        translated = translatedTemplate
            .Replace("{0}", mode)
            .Replace("{1}", turn)
            .Replace("{2}", seed);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    internal static bool IsAlreadyLocalizedPopupText(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        return IsAlreadyLocalizedPopupTextCore(stripped);
    }

    private static bool IsAlreadyLocalizedPopupTextCore(string stripped)
    {
        if (stripped.Length == 0)
        {
            return true;
        }

        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                stripped,
                nameof(PopupTranslationPatch)))
        {
            return true;
        }

        var numberedChoice = NumberedConversationChoicePattern.Match(stripped);
        if (numberedChoice.Success)
        {
            return UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                numberedChoice.Groups["text"].Value.TrimEnd(),
                nameof(PopupTranslationPatch));
        }

        var hotkeyMatch = HotkeyLabelPattern.Match(stripped);
        if (hotkeyMatch.Success && !int.TryParse(hotkeyMatch.Groups["hotkey"].Value, out _))
        {
            return UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                hotkeyMatch.Groups["label"].Value,
                nameof(PopupTranslationPatch));
        }

        var plainHotkeyMatch = PlainHotkeyLabelPattern.Match(stripped);
        if (!plainHotkeyMatch.Success)
        {
            return false;
        }

        return UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
            plainHotkeyMatch.Groups["label"].Value,
            nameof(PopupTranslationPatch));
    }

    private static MethodBase? FindMethod(string methodName, int parameterCount)
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: PopupTranslationPatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];
            if (method.Name != methodName || method.GetParameters().Length != parameterCount)
            {
                continue;
            }

            if (!method.IsDefined(typeof(ObsoleteAttribute), inherit: false))
            {
                return method;
            }
        }

        Trace.TraceError(
            $"QudJP: PopupTranslationPatch method '{methodName}' with {parameterCount} params not found (or only obsolete overloads) on '{TargetTypeName}'.");
        return null;
    }

    private static void TranslatePopupMenuItemTextCollection(object? maybeCollection)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IList list)
        {
            return;
        }

        for (var index = 0; index < list.Count; index++)
        {
            var item = list[index];
            _ = PopupTextFieldTranslator.TryTranslateTextField(
                item,
                TranslatePopupMenuItemText,
                translateNullAsEmpty: true);
        }
    }

    private static string TranslateCampfirePoisonToken(string capture)
    {
        return capture switch
        {
            "poison" or "poisons" => "毒",
            _ => capture,
        };
    }
}
