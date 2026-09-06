using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameSummaryTombstonePopupTranslationPatch
{
    private const string Context = nameof(GameSummaryTombstonePopupTranslationPatch);

    private static readonly Regex SavedPattern = new(
        "^Your tombstone file was saved:\\n\\n(?<path>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ClassicErrorPattern = new(
        "^There was an error saving: (?<path>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ModernErrorPattern = new(
        "^There was an error (?<result>.+?) saving: (?<path>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();

        var gameSummaryScreenType = GameTypeResolver.FindType("Qud.UI.GameSummaryScreen", "GameSummaryScreen");
        if (gameSummaryScreenType is null)
        {
            Trace.TraceError("QudJP: {0} GameSummaryScreen target type not found.", Context);
        }
        else
        {
            AddTarget(targets, gameSummaryScreenType, "SaveTombstone", Type.EmptyTypes);
        }

        var gameSummaryUiType = GameTypeResolver.FindType("XRL.UI.GameSummaryUI", "GameSummaryUI");
        if (gameSummaryUiType is null)
        {
            Trace.TraceError("QudJP: {0} GameSummaryUI target type not found.", Context);
        }
        else
        {
            AddTarget(
                targets,
                gameSummaryUiType,
                "Show",
                new[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool) });
        }

        return targets;
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

        if (TryTranslateTemplate(
                source,
                route,
                family + ".GameSummaryTombstoneSaved",
                SavedPattern,
                "Your tombstone file was saved:\n\n{0}",
                out translated,
                "path"))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                route,
                family + ".GameSummaryTombstoneError",
                ModernErrorPattern,
                "There was an error {0} saving: {1}",
                out translated,
                "result",
                "path"))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                route,
                family + ".GameSummaryTombstoneError",
                ClassicErrorPattern,
                "There was an error saving: {0}",
                out translated,
                "path"))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string key,
        out string translated,
        params string[] captureGroups)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate(key);
        if (string.Equals(template, key, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        try
        {
            var arguments = new object[captureGroups.Length];
            for (var index = 0; index < captureGroups.Length; index++)
            {
                arguments[index] = match.Groups[captureGroups[index]].Value;
            }

            translated = string.Format(
                CultureInfo.InvariantCulture,
                template,
                arguments);
        }
        catch (FormatException ex)
        {
            Trace.TraceError("QudJP: {0}.{1} template format failed: {2}", Context, family, ex);
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is null)
        {
            Trace.TraceError(
                "QudJP: {0}.{1}.{2} target not found.",
                Context,
                targetType.FullName,
                methodName);
            return;
        }

        targets.Add(method);
    }
}
