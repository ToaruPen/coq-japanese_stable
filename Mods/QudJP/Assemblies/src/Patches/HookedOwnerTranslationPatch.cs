using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HookedOwnerTranslationPatch
{
    internal const string Family = "HookedOwner";

    private const string Context = nameof(HookedOwnerTranslationPatch);

    private static readonly Regex BreakFreePattern = new(
        "^(?<subject>.+?) breaks? free from (?<holder>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var commandTakeActionEventType = AccessTools.TypeByName("XRL.World.CommandTakeActionEvent");
        AddTarget(targets, "XRL.World.Effects.Hooked", "HandleEvent", commandTakeActionEventType);
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

        if (!TryTranslateBreakFree(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Family + ".BreakFree",
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateBreakFree(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = BreakFreePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = TranslateSubject(Restore(match, spans, "subject"));
        var holder = TranslateHolder(Restore(match, spans, "holder"));
        translated = RestoreWholeSourceBoundary(subject + holder + "から抜け出した！", source, stripped, spans);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateSubject(string source)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source).Trim();
        if (string.Equals(visible, "You", StringComparison.Ordinal)
            || string.Equals(visible, "you", StringComparison.Ordinal))
        {
            return "あなたは";
        }

        return DisplayNameCaptureTranslator.StripLeadingEnglishArticlePreservingColors(source.Trim()) + "は";
    }

    private static string TranslateHolder(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(source.Trim(), TranslateHolderVisible).Trim();
    }

    private static string TranslateHolderVisible(string visible)
    {
        var trimmed = visible.Trim();
        if (string.Equals(trimmed, "the hook maneuver", StringComparison.Ordinal))
        {
            return "フック技";
        }

        if (trimmed.StartsWith("your ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(5).TrimStart();
        }

        var possessiveIndex = trimmed.IndexOf("'s ", StringComparison.Ordinal);
        if (possessiveIndex > 0)
        {
            return StringHelpers.StripLeadingEnglishArticle(
                    trimmed.Substring(0, possessiveIndex),
                    includeCapitalizedDefiniteArticle: true,
                    includeCapitalizedIndefiniteArticle: true)
                + "の"
                + trimmed.Substring(possessiveIndex + 3);
        }

        return StringHelpers.StripLeadingEnglishArticle(
            trimmed,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, params Type?[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var resolvedParameterTypes = new Type[parameterTypes.Length];
        for (var index = 0; index < parameterTypes.Length; index++)
        {
            if (parameterTypes[index] is null)
            {
                Trace.TraceError("QudJP: {0}.{1}.{2} parameter type {3} not found.", Context, typeName, methodName, index);
                return;
            }

            resolvedParameterTypes[index] = parameterTypes[index]!;
        }

        var method = AccessTools.Method(targetType, methodName, resolvedParameterTypes);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
