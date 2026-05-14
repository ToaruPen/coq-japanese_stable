using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PointOfInterestNavigationPopupTranslationPatch
{
    private const string Context = nameof(PointOfInterestNavigationPopupTranslationPatch);

    private static readonly Regex AlreadyAtPattern = new(
        "^You are already (?<preposition>\\S+) (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLocationPattern = new(
        "^Somehow there seems to be no location for (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.PointOfInterest");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "NavigateTo", [gameObjectType]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.NavigateTo target not found.", Context);
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
            if (!OwnerTranslationScope.IsActive(activeDepth))
            {
                directMarkerPassThroughText = null;
            }
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

        if (PopupShowTranslationPatch.TryConsumeDirectMarkerPassThrough(source, ref directMarkerPassThroughText))
        {
            translated = source;
            return true;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            directMarkerPassThroughText = markedText;
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

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        var match = AlreadyAtPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateAlreadyAt(
                match.Groups["preposition"].Value,
                match.Groups["target"].Value);
            detail = "AlreadyAtPointOfInterest";
            return true;
        }

        match = NoLocationPattern.Match(source);
        if (match.Success)
        {
            translated = $"どういうわけか{match.Groups["target"].Value}の場所が見つからない。";
            detail = "NoPointOfInterestLocation";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TranslateAlreadyAt(string preposition, string target)
    {
        return preposition switch
        {
            "near" or "by" => $"{target}の近くにすでにいる。",
            _ => $"{target}にすでにいる。",
        };
    }
}
