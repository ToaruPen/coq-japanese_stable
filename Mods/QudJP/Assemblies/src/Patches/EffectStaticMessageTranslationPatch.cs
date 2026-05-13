using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EffectStaticMessageTranslationPatch
{
    private const string Context = nameof(EffectStaticMessageTranslationPatch);

    private static readonly Regex BerserkCountdownPattern = new(
        "^(?<turns>.+?) until your berserker rage ends\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DemolishingCountdownPattern = new(
        "^(?<turns>.+?) until you stop demolishing\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExhaustionCollapsePattern = new(
        "^You're going to collapse from exhaustion in (?<rounds>.+?) rounds?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var beginTakeActionEventType = AccessTools.TypeByName("XRL.World.BeginTakeActionEvent");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (beginTakeActionEventType is null || eventType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Effects.AxonsDeflated", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.AxonsInflated", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.BasiliskPoison", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.Berserk", "HandleEvent", new[] { beginTakeActionEventType });
        AddTarget(targets, "XRL.World.Effects.Cudgel_SmashingUp", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.EmptyTheClips", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.Flagging", "HandleEvent", new[] { beginTakeActionEventType });
        AddTarget(targets, "XRL.World.Effects.NocturnalApexed", "Apply", new[] { gameObjectType });
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

        var translated = message switch
        {
            "You start to feel sluggish." => "体がだるくなってきた。",
            "The hurdles that separate the will and the way begin to collapse." => "志と道を隔てていた障害が崩れ始める。",
            "You feel stiff as a stone." => "石のように体がこわばる。",
            "You begin itching for a trigger." => "引き金を求めてうずうずしてきた。",
            "You start to prowl." => "うろつき始めた。",
            _ => null,
        };
        if (translated is null && !TryTranslateCountdownMessage(message, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static bool TryTranslateCountdownMessage(string source, out string translated)
    {
        var match = BerserkCountdownPattern.Match(source);
        if (match.Success && TryTranslateTurnRemainder(match.Groups["turns"].Value, out var turns))
        {
            translated = $"バーサークの怒りが終わるまであと{turns}ターン。";
            return true;
        }

        match = DemolishingCountdownPattern.Match(source);
        if (match.Success && TryTranslateTurnRemainder(match.Groups["turns"].Value, out turns))
        {
            translated = $"解体をやめるまであと{turns}ターン。";
            return true;
        }

        match = ExhaustionCollapsePattern.Match(source);
        if (match.Success && TryTranslateCardinal(match.Groups["rounds"].Value, out var rounds))
        {
            translated = $"疲労で倒れるまであと{rounds}ラウンド。";
            return true;
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryTranslateTurnRemainder(string source, out string translatedCount)
    {
        const string singularSuffix = " turn remains";
        const string pluralSuffix = " turns remain";

        if (source.EndsWith(singularSuffix, StringComparison.Ordinal))
        {
            return TryTranslateCardinal(source[..^singularSuffix.Length], out translatedCount);
        }

        if (source.EndsWith(pluralSuffix, StringComparison.Ordinal))
        {
            return TryTranslateCardinal(source[..^pluralSuffix.Length], out translatedCount);
        }

        translatedCount = string.Empty;
        return false;
    }

    private static bool TryTranslateCardinal(string source, out string translated)
    {
        var normalized = source.Trim();
        if (int.TryParse(normalized, out var numeric))
        {
            translated = numeric.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        translated = normalized switch
        {
            "zero" => "0",
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            "eleven" => "11",
            "twelve" => "12",
            "thirteen" => "13",
            "fourteen" => "14",
            "fifteen" => "15",
            "sixteen" => "16",
            "seventeen" => "17",
            "eighteen" => "18",
            "nineteen" => "19",
            "twenty" => "20",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
