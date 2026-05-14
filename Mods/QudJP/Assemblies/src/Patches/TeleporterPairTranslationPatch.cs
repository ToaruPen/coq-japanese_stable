using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TeleporterPairTranslationPatch
{
    private const string Context = nameof(TeleporterPairTranslationPatch);

    private static readonly Regex CooldownPattern = new(
        "^You must wait (?<duration>.+?) before using (?<object>this|these|that|those|it|them) again\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TurnDurationPattern = new(
        "^(?<count>\\d+) turns?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var teleporterPairType = AccessTools.TypeByName("XRL.World.Parts.TeleporterPair");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (teleporterPairType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var attemptTeleport = AccessTools.Method(teleporterPairType, "AttemptTeleport", [gameObjectType, eventType]);
        if (attemptTeleport is not null)
        {
            yield return attemptTeleport;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.AttemptTeleport(GameObject, IEvent) not found.", Context);
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

        var match = CooldownPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = TranslateObject(match.Groups["object"].Value)
            + "を再び使うには"
            + TranslateDuration(match.Groups["duration"].Value)
            + "待たなければならない。";
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".TeleporterPairCooldown",
            source,
            translated);
        return true;
    }

    private static string TranslateDuration(string source)
    {
        var match = TurnDurationPattern.Match(source);
        if (match.Success)
        {
            return match.Groups["count"].Value + "ターン";
        }

        return source;
    }

    private static string TranslateObject(string source)
    {
        return source switch
        {
            "these" or "those" or "them" => "これら",
            _ => "これ",
        };
    }
}
