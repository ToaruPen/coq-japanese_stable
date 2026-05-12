using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GolemQuestSelectionPopupTranslationPatch
{
    private const string Context = nameof(GolemQuestSelectionPopupTranslationPatch);

    private static readonly Regex MissingBlueprintPattern = new(
        "^No blueprint by ID '(?<id>.+?)' found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MissingRequirementPattern = new(
        "^You have nothing that meets the requirement of the (?<requirement>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in ResolveTarget(
                     "XRL.World.Quests.GolemQuest.GolemBodySelection",
                     "WishSpec",
                     [typeof(string)]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Quests.GolemQuest.GolemMaterialSelection`2",
                     "Pick",
                     Type.EmptyTypes))
        {
            yield return method;
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

        if (TryTranslateCore(source, out translated, out var detail))
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

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        if (TryTranslatePattern(
                MissingBlueprintPattern,
                source,
                (match, spans) => $"ID '{Restore(match, spans, "id")}' のブループリントが見つからない。",
                out translated))
        {
            detail = "MissingBlueprint";
            return true;
        }

        if (TryTranslatePattern(
                MissingRequirementPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "requirement")}の要件を満たすものを持っていない。",
                out translated))
        {
            detail = "MissingRequirement";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
