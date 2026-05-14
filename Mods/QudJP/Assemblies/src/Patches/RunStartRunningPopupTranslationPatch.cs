using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RunStartRunningPopupTranslationPatch
{
    private const string Context = nameof(RunStartRunningPopupTranslationPatch);

    private static readonly Regex WorldMapRunningPattern = new(
        "^You cannot (?<verb>.+?) on the world map\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Run");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "StartRunning", Type.EmptyTypes);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.StartRunning target not found.", Context);
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

        if (TryTranslateCore(source, out translated))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + ".WorldMapMovementMode",
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var match = WorldMapRunningPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (!TryTranslateActiveVerb(match.Groups["verb"].Value, out var activeVerb))
        {
            translated = source;
            return false;
        }

        translated = $"ワールドマップでは{activeVerb}できない。";
        return true;
    }

    private static bool TryTranslateActiveVerb(string activeVerb, out string translated)
    {
        translated = activeVerb switch
        {
            "run" => "走ることは",
            "sprint" => "全力疾走することは",
            "power skate" => "パワースケートすることは",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }
}
