using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DecoyHologramEmitterActivateTranslationPatch
{
    private const string Context = nameof(DecoyHologramEmitterActivateTranslationPatch);

    private static readonly Regex StillStartingPattern = new(
        "^(?:The |the |A |a |An |an )?(?<object>.+?) (?:is|are) still starting up\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InsufficientChargePattern = new(
        "^(?:The |the |A |a |An |an )?(?<object>.+?) (?:does|do) not have enough charge to sustain the hologram\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UnresponsivePattern = new(
        "^(?:The |the |A |a |An |an )?(?<object>.+?) (?:is|are) unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var emitterType = AccessTools.TypeByName("XRL.World.Parts.DecoyHologramEmitter");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (emitterType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var activate = AccessTools.Method(emitterType, "ActivateHologramBracelet", [gameObjectType, eventType]);
        if (activate is not null)
        {
            yield return activate;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ActivateHologramBracelet(GameObject, IEvent) not found.", Context);
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

        if (!TryTranslate(source, out translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
        return true;
    }

    private static bool TryTranslate(string source, out string translated, out string detail)
    {
        if (TryTranslatePattern(
                StillStartingPattern,
                source,
                "DecoyHologramStillStarting",
                static subject => $"{subject}はまだ起動中だ。",
                out translated,
                out detail))
        {
            return true;
        }

        if (TryTranslatePattern(
                InsufficientChargePattern,
                source,
                "DecoyHologramInsufficientCharge",
                static subject => $"{subject}にはホログラムを維持するのに十分な充電がない。",
                out translated,
                out detail))
        {
            return true;
        }

        return TryTranslatePattern(
            UnresponsivePattern,
            source,
            "DecoyHologramUnresponsive",
            static subject => $"{subject}は反応しない。",
            out translated,
            out detail);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        string patternDetail,
        Func<string, string> build,
        out string translated,
        out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            build(RestoreObject(match, spans)),
            spans,
            stripped.Length,
            source);
        detail = patternDetail;
        return true;
    }

    private static string RestoreObject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["object"];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
