using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HighScoresDeletePopupTranslationPatch
{
    private const string Context = nameof(HighScoresDeletePopupTranslationPatch);

    private static readonly Regex DeleteConfirmationPattern =
        new Regex(
            "^Are you sure you want to delete this\\?\\n\\n(?<detail>[\\s\\S]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        AddTarget(targets, GameTypeResolver.FindType("Qud.UI.HighScoresScreen", "HighScoresScreen"), "HandleDelete");
        AddTarget(targets, AccessTools.TypeByName("XRL.Core.Scores"), "Show");
        return targets;
    }

    private static void AddTarget(List<MethodBase> targets, Type? targetType, string methodName)
    {
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0}.{1} target type not found.", Context, methodName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} not found.", Context, targetType.FullName, methodName);
            return;
        }

        targets.Add(method);
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

        var match = DeleteConfirmationPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = string.Concat(
            "本当にこれを削除しますか？\n\n",
            match.Groups["detail"].Value);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + ".DeleteConfirmation", source, translated);
        return true;
    }
}
