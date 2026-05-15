using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RealityStabilizedEventTranslationPatch
{
    private const string Context = nameof(RealityStabilizedEventTranslationPatch);

    private static readonly Regex PsychicWhiffPattern = new(
        "^You feel a psychic whiff as (?<actor>.+?) pushes? past resistance in the structure of spacetime\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PsychicThudPattern = new(
        "^You feel a psychic thud as (?<actor>.+?) pushes? against the structure of spacetime and fails? to break through\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WincePattern = new(
        "^(?<actor>.+?) winces?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShowerSparksPattern = new(
        "^(?<device>.+?) showers? sparks everywhere\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmitSparksPattern = new(
        "^(?<device>.+?) emits? a shower of sparks!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string NormalityLatticePopup =
        "You try to push through the normality lattice, but it snaps back into place.";

    private const string NormalityLatticeWinceSuffix = " You wince in pain.";

    private const string NormalityLatticeTranslation =
        "あなたは通常性格子を押し通ろうとしたが、それは跳ね返って元に戻った。";

    private const string NormalityLatticeWinceTranslation = "あなたは痛みに顔をしかめた。";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.RealityStabilized");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "TryContest", new[] { gameObjectType, typeof(int), typeof(int) });
        AddTarget(targets, targetType, "FailedToContest", new[] { gameObjectType });
        AddTarget(targets, targetType, "ShortCircuitDevice", new[] { gameObjectType, gameObjectType, eventType });
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
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

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateQueuedCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "RealityStabilized.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateNormalityLatticePopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        if (!TryTranslatePattern(EmitSparksPattern, source, device => $"{device}が火花の雨を放った！", out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateNormalityLatticePopup(string source, out string translated)
    {
        if (string.Equals(source, NormalityLatticePopup, StringComparison.Ordinal))
        {
            translated = NormalityLatticeTranslation;
            return true;
        }

        if (string.Equals(source, NormalityLatticePopup + NormalityLatticeWinceSuffix, StringComparison.Ordinal))
        {
            translated = NormalityLatticeTranslation + NormalityLatticeWinceTranslation;
            return true;
        }

        translated = source;
        return false;
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

    private static bool TryTranslateQueuedCore(string source, out string translated)
    {
        return TryTranslatePattern(
            PsychicWhiffPattern,
            source,
            actor => $"{actor}が時空構造の抵抗を押し通る、精神的なかすかな感触を覚えた。",
            out translated)
            || TryTranslatePattern(
                PsychicThudPattern,
                source,
                actor => $"{TranslateFailedContestActor(actor)}が時空構造を押して突破に失敗した、精神的な鈍い衝撃を感じた。",
                out translated)
            || TryTranslatePattern(
                WincePattern,
                source,
                actor => $"{actor}が顔をしかめた。",
                out translated)
            || TryTranslatePattern(
                ShowerSparksPattern,
                source,
                device => $"{device}があたり一面に火花を散らした。",
                out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var groupName = match.Groups["actor"].Success ? "actor" : "device";
        var group = match.Groups[groupName];
        var value = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(value),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string TranslateFailedContestActor(string actor)
    {
        return string.Equals(actor, "someone", StringComparison.Ordinal) ? "誰か" : actor;
    }
}
