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

    // ShowBlock parameter count for game version 2.0.4
    private const int ShowBlockParameterCount = 8;

    // ShowOptionList parameter count for game version 2.0.4
    private const int ShowOptionListParameterCount = 19;

    private const int ShowConversationParameterCount = 7;

    // ShowOptionList argument indices (game version 2.0.4)
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
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
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
        return TranslatePopupProducerText(source, route, "Popup.ProducerText");
    }

    internal static string TranslatePopupMenuItemTextForProducerRoute(string source, string route)
    {
        return TranslatePopupProducerText(source, route, "Popup.ProducerMenuItem");
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
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

    private static string TranslatePopupProducerText(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        if (TryTranslatePopupProducerText(source, route, family, out var translated))
        {
            return translated;
        }

        return source;
    }

    private static bool TryTranslatePopupProducerText(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

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

        if (MutationsApiTranslationPatch.TryTranslatePopupMessage(source, route, family, out var mutationTranslated))
        {
            translated = mutationTranslated;
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(stripped, out var exact)
            && !string.Equals(exact, stripped, StringComparison.Ordinal))
        {
            translated = spans.Count == 0 ? exact : ColorAwareTranslationComposer.Restore(exact, spans);
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

        var poison = match.Groups["poison"].Value;
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
            if (item is null)
            {
                continue;
            }

            var field = AccessTools.Field(item.GetType(), "text");
            if (field is null || field.FieldType != typeof(string))
            {
                continue;
            }

            var current = field.GetValue(item) as string;
            var translated = TranslatePopupMenuItemText(current ?? string.Empty);
            if (string.Equals(current, translated, StringComparison.Ordinal))
            {
                continue;
            }

            field.SetValue(item, translated);
            list[index] = item;
        }
    }
}
