using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class JournalScreenPopupTranslationPatch
{
    private const string Context = nameof(JournalScreenPopupTranslationPatch);

    private static readonly Regex RecipeDeletePattern = new(
        "^Are you sure you want to delete (?<recipe>\\{\\{y\\|.+?\\}\\})\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StopCallingLocationPattern = new(
        "^You stop calling this location '(?<oldName>.+?)' and start calling it '(?<newName>.+?)'\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StartCallingLocationPattern = new(
        "^You start calling this location '(?<name>.+?)'\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var journalScreenType = AccessTools.TypeByName("XRL.UI.JournalScreen");
        var journalEntryType = AccessTools.TypeByName("Qud.API.IBaseJournalEntry");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (journalScreenType is null || journalEntryType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var handleDelete = AccessTools.Method(
            journalScreenType,
            "HandleDelete",
            [typeof(string), journalEntryType, gameObjectType]);
        if (handleDelete is not null)
        {
            yield return handleDelete;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleDelete(string, IBaseJournalEntry, GameObject) not found.", Context);
        }

        var show = AccessTools.Method(journalScreenType, "Show", [gameObjectType]);
        if (show is not null)
        {
            yield return show;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Show(GameObject) not found.", Context);
        }
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateRecipeDeleteConfirmation(source, out translated))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + ".RecipeDeleteConfirmation",
                source,
                translated);
            return true;
        }

        if (TryTranslateLocationRename(source, route, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + "." + detail,
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateRecipeDeleteConfirmation(string source, out string translated)
    {
        var match = RecipeDeletePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "本当に" + match.Groups["recipe"].Value + "を削除しますか？";
        return true;
    }

    private static bool TryTranslateLocationRename(string source, string route, out string translated, out string detail)
    {
        var stopCallingMatch = StopCallingLocationPattern.Match(source);
        if (stopCallingMatch.Success)
        {
            detail = "RenameLocation";
            translated = "場所の呼称を「"
                + stopCallingMatch.Groups["oldName"].Value
                + "」から「"
                + stopCallingMatch.Groups["newName"].Value
                + "」に変更した。";
            return true;
        }

        var startCallingMatch = StartCallingLocationPattern.Match(source);
        if (startCallingMatch.Success)
        {
            detail = "NameLocation";
            translated = "場所を「" + startCallingMatch.Groups["name"].Value + "」と呼ぶことにした。";
            return true;
        }

        _ = route;
        translated = source;
        detail = string.Empty;
        return false;
    }
}
