using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AsleepOwnerTranslationPatch
{
    private const string Context = nameof(AsleepOwnerTranslationPatch);

    private static readonly Regex EnterSleepModePattern = new(
        "^You enter (?<mode>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GoIntoSleepModePattern = new(
        "^(?<actor>.+?) goes? into (?<mode>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FallAsleepPattern = new(
        "^(?<actor>.+?) falls? (?<state>.+?)[.!]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PressActivationPanelPattern = new(
        "^(?<actor>.+?) presses? (?<panel>.+?) activation panel\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShakeAwakePattern = new(
        "^(?<actor>.+?) gently\\s*shakes? (?<target>.+?) awake\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouPressActivationPanelPattern = new(
        "^You press (?<panel>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouShakeAwakePattern = new(
        "^You gently shake (?<target>.+?) awake\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CantWakePattern = new(
        "^You can't figure out how to wake (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Asleep");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var beginTakeActionEventType = AccessTools.TypeByName("XRL.World.BeginTakeActionEvent");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || gameObjectType is null || beginTakeActionEventType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Apply", new[] { gameObjectType });
        AddTarget(targets, targetType, "HandleEvent", new[] { beginTakeActionEventType });
        AddTarget(targets, targetType, "HandleEvent", new[] { inventoryActionEventType });
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

        if (!TryTranslateCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "Asleep.Queue", message, translated);
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

        if (!TryTranslateCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
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

    private static bool TryTranslateCore(string source, out string translated)
    {
        if (source == "You are asleep.")
        {
            translated = "眠っている。";
            return true;
        }

        return TryTranslatePattern(
            EnterSleepModePattern,
            source,
            (match, spans) => $"あなたは{TranslateSleepTerm(Restore(match, spans, "mode"))}に入った。",
            out translated)
            || TryTranslatePattern(
                GoIntoSleepModePattern,
                source,
                (match, spans) => $"{NormalizeSubject(Restore(match, spans, "actor"))}は{TranslateSleepTerm(Restore(match, spans, "mode"))}に入った。",
                out translated)
            || TryTranslatePattern(
                FallAsleepPattern,
                source,
                (match, spans) => $"{NormalizeSubject(Restore(match, spans, "actor"))}は{TranslateSleepTerm(Restore(match, spans, "state"))}に落ちた。",
                out translated)
            || TryTranslatePattern(
                PressActivationPanelPattern,
                source,
                (match, spans) => $"{NormalizeSubject(Restore(match, spans, "actor"))}は{NormalizePanelOwner(Restore(match, spans, "panel"))}起動パネルを押した。",
                out translated)
            || TryTranslatePattern(
                ShakeAwakePattern,
                source,
                (match, spans) => $"{NormalizeSubject(Restore(match, spans, "actor"))}は{Restore(match, spans, "target")}をやさしく揺り起こした。",
                out translated)
            || TryTranslatePattern(
                YouPressActivationPanelPattern,
                source,
                (match, spans) => $"あなたは{Restore(match, spans, "panel")}を押した。",
                out translated)
            || TryTranslatePattern(
                YouShakeAwakePattern,
                source,
                (match, spans) => $"あなたは{Restore(match, spans, "target")}をやさしく揺り起こした。",
                out translated)
            || TryTranslatePattern(
                CantWakePattern,
                source,
                (match, spans) => $"あなたには{Restore(match, spans, "target")}を起こす方法がわからない。",
                out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string NormalizePanelOwner(string owner)
    {
        return string.Equals(owner, "your", StringComparison.OrdinalIgnoreCase)
            ? "あなたの"
            : owner + " ";
    }

    private static string NormalizeSubject(string subject)
    {
        if (string.Equals(subject, "You", StringComparison.Ordinal))
        {
            return "あなた";
        }

        return StripLeadingSubjectArticle(subject);
    }

    private static string StripLeadingSubjectArticle(string subject)
    {
        var markerEnd = subject.LastIndexOf('\u0003');
        if (markerEnd >= 0 && markerEnd + 1 < subject.Length)
        {
            return subject.Substring(0, markerEnd + 1)
                + StripLeadingSubjectArticle(subject.Substring(markerEnd + 1));
        }

        return StringHelpers.StripLeadingEnglishArticle(
            subject,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
    }

    private static string TranslateSleepTerm(string term)
    {
        return term
            .Replace("sleep mode", "スリープモード")
            .Replace("asleep", "眠り");
    }
}
