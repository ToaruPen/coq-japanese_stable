using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
    private const string InventoryActionMenuPopupIdPrefix = "InventoryActionMenu:";
    private const string InventoryActionContext = "XRL.World.IInventoryActionsEvent";
    private const string InventoryActionDictionaryFile = "ui-inventory-actions.ja.json";
    private static readonly Regex HotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
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
    private static readonly Regex QuestReceivedPattern =
        new Regex("^You have received a new quest, (?<value>.+)!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
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

    internal static string TranslatePopupMenuItemTextForRoute(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

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

    private static string TranslatePopupProducerText(string source, string route, string family, string? popupId)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (TryStripPopupDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        if (ItemNamingTranslationPatch.TryTranslatePopupMessage(source, route, family, out var itemNamingOwnerTranslated))
        {
            return NormalizeProducerText(itemNamingOwnerTranslated);
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

        if (TradeUiVendorPopupTranslationPatch.TryTranslatePopupMessage(source, route, family, out var tradeUiVendorTranslated))
        {
            translated = tradeUiVendorTranslated;
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

        if (IsAlreadyLocalizedPopupTextCore(stripped))
        {
            translated = source;
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

        if (TryTranslateUntilCalendarTimeOfDay(source, stripped, spans, route, family, out var untilTranslated))
        {
            translated = untilTranslated;
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

        if (TryTranslateSinglePlaceholderTemplate(
                stripped,
                route,
                family + ".WaterRitualLowReputation",
                WaterRitualLowReputationPattern,
                "You don't have a high enough reputation with {0}.",
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
            var patternTranslated = MessagePatternTranslator.Translate(source, route);
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
            && !string.Equals(route, nameof(SelectableTextMenuItemTranslationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        var hotkeyMatch = HotkeyLabelPattern.Match(stripped);
        if (hotkeyMatch.Success && !int.TryParse(hotkeyMatch.Groups["hotkey"].Value, out _))
        {
            var label = hotkeyMatch.Groups["label"].Value;
            var translatedLabel = TranslatePopupMenuItemLabel(label, popupId);
            if (translatedLabel is null)
            {
                return TryAcceptInventoryActionMenuOwnerMiss(source, popupId, out translated);
            }

            if (string.Equals(translatedLabel, label, StringComparison.Ordinal))
            {
                return false;
            }

            var hotkeySourceLength = hotkeyMatch.Groups["hotkey"].Length + 2;
            var hotkey = isBottomContextRoute
                ? ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
                    "[" + hotkeyMatch.Groups["hotkey"].Value + "]",
                    spans,
                    hotkeyMatch.Index,
                    hotkeySourceLength)
                : ColorAwareTranslationComposer.RestoreSlice(
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
            || !Regex.IsMatch(stripped, "^[A-Za-z][A-Za-z ]*[A-Za-z]$", RegexOptions.CultureInvariant))
        {
            return false;
        }

        var embeddedTranslated = TranslatePopupMenuItemLabel(stripped, popupId);
        if (embeddedTranslated is null)
        {
            return TryAcceptInventoryActionMenuOwnerMiss(source, popupId, out translated);
        }

        if (string.Equals(embeddedTranslated, stripped, StringComparison.Ordinal))
        {
            return false;
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
        var translatedLabel = TranslatePopupMenuItemLabel(label, popupId);
        if (translatedLabel is null)
        {
            translated = source;
            return true;
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
        if (hasLegacyDisabledColor && !translated.StartsWith("&", StringComparison.Ordinal))
        {
            translated = InsertAmpersandColorAtVisibleIndex(translated, labelStart, "&K");
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
        if (translatedLabel.StartsWith("&", StringComparison.Ordinal)
            || !HasLegacyDisabledInventoryActionColor(spans, labelStart))
        {
            return translatedLabel;
        }

        return InsertAmpersandColorAfterOpeningBoundaryWrappers(translatedLabel, "&K");
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

    private static string InsertAmpersandColorAfterOpeningBoundaryWrappers(string source, string color)
    {
        var index = 0;
        while (index < source.Length)
        {
            if (source[index] == '{'
                && index + 1 < source.Length
                && source[index + 1] == '{')
            {
                var pipeIndex = source.IndexOf('|', index + 2);
                if (pipeIndex >= 0)
                {
                    index = pipeIndex + 1;
                    continue;
                }
            }

            if (source[index] == '<')
            {
                var closeIndex = source.IndexOf('>', index + 1);
                if (closeIndex >= 0
                    && source.IndexOf("<color=", index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    index = closeIndex + 1;
                    continue;
                }
            }

            break;
        }

        return source.Substring(0, index) + color + source.Substring(index);
    }

    private static string InsertAmpersandColorAtVisibleIndex(string source, int visibleIndex, string color)
    {
        var index = 0;
        var visible = 0;
        while (index < source.Length && visible < visibleIndex)
        {
            if (TryAdvanceMarkupToken(source, ref index))
            {
                continue;
            }

            index++;
            visible++;
        }

        return source.Substring(0, index)
            + InsertAmpersandColorAfterOpeningBoundaryWrappers(source.Substring(index), color);
    }

    private static bool TryAdvanceMarkupToken(string source, ref int index)
    {
        if (index + 1 < source.Length
            && source[index] == '{'
            && source[index + 1] == '{')
        {
            var openPipeIndex = source.IndexOf('|', index + 2);
            if (openPipeIndex >= 0)
            {
                index = openPipeIndex + 1;
                return true;
            }
        }

        if (index + 1 < source.Length
            && source[index] == '}'
            && source[index + 1] == '}')
        {
            index += 2;
            return true;
        }

        if (index + 1 < source.Length
            && (source[index] == '&' || source[index] == '^'))
        {
            index += 2;
            return true;
        }

        if (source[index] == '<')
        {
            var closeIndex = source.IndexOf('>', index + 1);
            if (closeIndex >= 0
                && (source.IndexOf("<color=", index, StringComparison.OrdinalIgnoreCase) == index
                    || source.IndexOf("</color", index, StringComparison.OrdinalIgnoreCase) == index))
            {
                index = closeIndex + 1;
                return true;
            }
        }

        return false;
    }

    private static bool TryAcceptInventoryActionMenuOwnerMiss(string source, string? popupId, out string translated)
    {
        translated = source;
        return IsInventoryActionMenuPopup(popupId);
    }

    private static string? TranslatePopupMenuItemLabel(string label, string? popupId)
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

            if (TryTranslateInventoryActionMenuLabelPattern(label, out var inventoryActionPatternTranslation))
            {
                return inventoryActionPatternTranslation;
            }

            return ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, CommonMenuActionDictionaryFile);
        }

        var qudMenuItemTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            label,
            QudMenuItemContext,
            QudMenuItemDictionaryFile);
        return qudMenuItemTranslation is not null
            ? qudMenuItemTranslation
            : ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, CommonMenuActionDictionaryFile);
    }

    private static bool TryTranslateInventoryActionMenuLabelPattern(string label, out string translated)
    {
        translated = label;
        const string eatPrefix = "Eat ";
        if (!label.StartsWith(eatPrefix, StringComparison.Ordinal) || label.Length == eatPrefix.Length)
        {
            return false;
        }

        var meal = label.Substring(eatPrefix.Length).Trim();
        if (meal.Length == 0)
        {
            return false;
        }

        if (meal.EndsWith(".", StringComparison.Ordinal))
        {
            meal = meal.Substring(0, meal.Length - 1).TrimEnd();
        }

        translated = TranslateCookingRecipeNameForInventoryActionMenu(meal) + "を食べる";
        return true;
    }

    private static string TranslateCookingRecipeNameForInventoryActionMenu(string meal)
    {
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
                return translatedMeal;
            }
        }

        return meal switch
        {
            "Apple Matz" => "アップルマッツァ",
            "Mulled Mushroom Cider" => "温めたマッシュルームサイダー",
            "Goat in Sweet Leaf" => "甘葉包みのヤギ肉",
            "Tongue and Cheek" => "タングアンドチーク",
            "Bone Babka" => "ボーンバブカ",
            "Hot and Spiny" => "ホットアンドスパイニー",
            "Mah Lah Soup" => "マーラースープ",
            "The Porridge" => "粥",
            _ => meal,
        };
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

    private static bool TryTranslateSinglePlaceholderTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        IReadOnlyList<ColorSpan> spans,
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

        translated = translatedTemplate.Replace("{0}", value);
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
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

        translated = "本当に" + target + "を攻撃しますか？";
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, source.Length, translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        return true;
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
