using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FirefightingTranslationPatch
{
    private const string Context = nameof(FirefightingTranslationPatch);

    private static readonly Regex CannotReachPattern = new(
        "^You cannot reach (?<target>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var firefightingType = AccessTools.TypeByName("XRL.World.Capabilities.Firefighting");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (firefightingType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var attemptFirefightingCore = AccessTools.Method(
            firefightingType,
            "AttemptFirefightingCore",
            [gameObjectType, gameObjectType, typeof(int), typeof(bool), typeof(bool)]);
        if (attemptFirefightingCore is not null)
        {
            yield return attemptFirefightingCore;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.AttemptFirefightingCore target not found.", Context);
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

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = CannotReachPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = TranslateTarget(match, spans) + "に手が届かない。";
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".CannotReachSubject",
            source,
            translated);
        return true;
    }

    private static string TranslateTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = match.Groups["target"];
        var cleaned = StringHelpers.StripLeadingEnglishArticle(
            target.Value.Trim(),
            includeCapitalizedDefiniteArticle: true);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(cleaned, spans, target).Trim();
    }
}
