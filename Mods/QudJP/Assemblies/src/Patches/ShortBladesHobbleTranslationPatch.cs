using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ShortBladesHobbleTranslationPatch
{
    private const string Context = nameof(ShortBladesHobbleTranslationPatch);

    private static readonly Regex PlayerFindsWeaknessPattern = new(
        "^You find a weakness in (?<target>.+?) defenses\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EnemyFindsWeaknessPattern = new(
        "^(?<actor>.+?) (?<verb>find|finds) a weakness in your defenses\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SelfConfirmationPattern = new(
        "^Are you sure you want to hobble (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var hobbleType = AccessTools.TypeByName("XRL.World.Parts.Skill.ShortBlades_Hobble");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (hobbleType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var fireEvent = AccessTools.Method(hobbleType, "FireEvent", [eventType]);
        if (fireEvent is not null)
        {
            yield return fireEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) not found.", Context);
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

        var match = SelfConfirmationPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = TranslateSelfTarget(match.Groups["target"].Value) + "を足止めしてもよいか？";
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".ShortBladesHobbleSelfConfirmation",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateQueuedMessage(string source, out string translated, out string detail)
    {
        var match = PlayerFindsWeaknessPattern.Match(source);
        if (match.Success)
        {
            detail = "ShortBladesHobblePlayerFindsWeakness";
            translated = TrimPossessive(match.Groups["target"].Value) + "の防御に隙を見つけた。";
            return true;
        }

        match = EnemyFindsWeaknessPattern.Match(source);
        if (match.Success)
        {
            detail = "ShortBladesHobbleEnemyFindsWeakness";
            translated = match.Groups["actor"].Value + "があなたの防御に隙を見つけた。";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TrimPossessive(string source)
    {
        if (source.EndsWith("'s", StringComparison.Ordinal))
        {
            return source.Substring(0, source.Length - 2);
        }

        return source.EndsWith("'", StringComparison.Ordinal)
            ? source.Substring(0, source.Length - 1)
            : source;
    }

    private static string TranslateSelfTarget(string source)
    {
        return source == "yourself" ? "自分自身" : source;
    }
}
