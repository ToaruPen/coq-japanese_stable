using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TabulaRasaeTranslationPatch
{
    private const string Context = nameof(TabulaRasaeTranslationPatch);
    private static readonly Regex NoEffectPattern = new(
        "^Your attack does not affect (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AdaptPattern = new(
        "^The Tabula Rasae adapt to (?<attribute>.+?) damage\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.TabulaRasae");
        var confusionType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Confusion");
        var beforeApplyDamageEventType = AccessTools.TypeByName("XRL.World.BeforeApplyDamageEvent");
        var tookDamageEventType = AccessTools.TypeByName("XRL.World.TookDamageEvent");
        var mentalAttackEventType = AccessTools.TypeByName("XRL.World.MentalAttackEvent");
        if (targetType is null || confusionType is null || beforeApplyDamageEventType is null || tookDamageEventType is null || mentalAttackEventType is null)
        {
            Trace.TraceError("QudJP: {0} target or event type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { beforeApplyDamageEventType });
        AddTarget(targets, targetType, "HandleEvent", new[] { tookDamageEventType });
        AddTarget(
            targets,
            confusionType,
            "Confuse",
            new[] { mentalAttackEventType, typeof(bool), typeof(int), typeof(int), typeof(bool) });
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

        if (!TryTranslateTabulaRasaeMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateTabulaRasaeMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = NoEffectPattern.Match(stripped);
        if (match.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"攻撃は{RestoreCapture(match, spans, "target")}に影響を与えない。",
                spans,
                stripped.Length,
                source);
            return true;
        }

        match = AdaptPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"タブラ・ラサは{TranslateDamageAttribute(match, spans)}ダメージに適応した。";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateDamageAttribute(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["attribute"];
        var translated = group.Value.Trim() switch
        {
            "acid" => "酸",
            "bleeding" => "出血",
            "cold" => "冷気",
            "electric" or "electrical" => "電撃",
            "heat" => "熱",
            "mental" => "精神",
            "poison" => "毒",
            "sonic" => "音波",
            _ => null,
        };

        return translated is null
            ? ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim()
            : ColorAwareTranslationComposer.RestoreCapture(translated, spans, group).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
