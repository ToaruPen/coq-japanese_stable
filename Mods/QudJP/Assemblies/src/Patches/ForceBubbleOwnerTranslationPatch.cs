using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ForceBubbleOwnerTranslationPatch
{
    private const string Context = nameof(ForceBubbleOwnerTranslationPatch);

    private static readonly Regex SnapOffPattern = new(
        "^The (?<bubble>force bubble)(?: (?<position>around|in front of) (?<subject>.+?))? snaps off\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PopsIntoBeingPattern = new(
        "^A (?<bubble>force bubble) pops into being (?<position>around|in front of) (?<subject>.+?)(?<end>[.!])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var iEventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (iEventType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter type not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Parts.ForceEmitter", "ActivateForceEmitter", new[] { iEventType });
        AddTarget(targets, "XRL.World.Parts.Stopsvaalinn", "ActivateStopsvalinn", new[] { iEventType });
        AddTarget(targets, "XRL.World.Parts.Mutation.ForceBubble", "DestroyBubble", new[] { typeof(bool) });
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

        if (!TryTranslateForceBubbleMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static bool TryTranslateForceBubbleMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SnapOffPattern.Match(stripped);
        if (match.Success)
        {
            var bubble = Restore(match, spans, "bubble", "フォースバブル");
            var position = match.Groups["position"].Value;
            if (string.Equals(position, "around", StringComparison.Ordinal))
            {
                translated = RestoreWhole($"{RestoreSubject(match, spans)}の周りの{bubble}が消えた。", spans, stripped, source);
                return true;
            }

            if (string.Equals(position, "in front of", StringComparison.Ordinal))
            {
                translated = RestoreWhole($"{RestoreSubject(match, spans)}の前の{bubble}が消えた。", spans, stripped, source);
                return true;
            }

            translated = RestoreWhole($"{bubble}が消えた。", spans, stripped, source);
            return true;
        }

        match = PopsIntoBeingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = RestoreSubject(match, spans);
        var forceBubble = Restore(match, spans, "bubble", "フォースバブル");
        var particle = string.Equals(match.Groups["position"].Value, "around", StringComparison.Ordinal)
            ? "の周りに"
            : "の前に";
        var endMark = match.Groups["end"].Value == "!" ? "！" : "。";

        translated = RestoreWhole($"{subject}{particle}{forceBubble}が出現した{endMark}", spans, stripped, source);
        return true;
    }

    private static string RestoreSubject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var subject = match.Groups["subject"].Value;
        return string.Equals(subject, "you", StringComparison.Ordinal)
            ? "あなた"
            : Restore(match, spans, "subject", subject);
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName, string translatedValue)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(translatedValue, spans, group).Trim();
    }

    private static string RestoreWhole(string translated, IReadOnlyList<ColorSpan> spans, string stripped, string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
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
