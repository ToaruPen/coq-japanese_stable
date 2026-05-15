using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EelSpawnTranslationPatch
{
    private const string Context = nameof(EelSpawnTranslationPatch);

    private static readonly Regex CannotReachPattern = new(
        "^A sewage eel tries to wrap itself(?<target> around .+?), but cannot reach!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PassesThroughPattern = new(
        "^A sewage eel tries to wrap itself(?<target> around .+?), but passes through you!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WrapPopupPattern = new(
        "^A sewage eel wraps itself(?<target> around .+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var eelSpawnType = AccessTools.TypeByName("XRL.World.Parts.EelSpawn");
        var objectEnteredCellEventType = AccessTools.TypeByName("XRL.World.ObjectEnteredCellEvent");
        if (eelSpawnType is null || objectEnteredCellEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var handleEvent = AccessTools.Method(eelSpawnType, "HandleEvent", [objectEnteredCellEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(ObjectEnteredCellEvent) not found.", Context);
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateQueuedMessage(message, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

        var match = WrapPopupPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "下水ウナギが" + TranslateTarget(match.Groups["target"].Value) + "に巻きついた！";
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".EelSpawnWrap",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateQueuedMessage(string source, out string translated, out string detail)
    {
        var match = CannotReachPattern.Match(source);
        if (match.Success)
        {
            detail = "EelSpawnCannotReach";
            translated = "下水ウナギが" + TranslateTarget(match.Groups["target"].Value) + "に巻きつこうとしたが、届かなかった！";
            return true;
        }

        match = PassesThroughPattern.Match(source);
        if (match.Success)
        {
            detail = "EelSpawnPassesThrough";
            translated = "下水ウナギが" + TranslateTarget(match.Groups["target"].Value) + "に巻きつこうとしたが、あなたをすり抜けた！";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TranslateTarget(string source)
    {
        const string AroundYour = " around your ";

        if (source == " around you")
        {
            return "あなた";
        }

        if (source.StartsWith(AroundYour, StringComparison.Ordinal))
        {
            return "あなたの" + source.Substring(AroundYour.Length);
        }

        return source.TrimStart();
    }
}
