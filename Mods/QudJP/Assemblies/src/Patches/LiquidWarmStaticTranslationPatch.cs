using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LiquidWarmStaticTranslationPatch
{
    private const string Context = nameof(LiquidWarmStaticTranslationPatch);

    private static readonly Regex MindFluctuatesPattern = new(
        "^(?:(?<owner>.+?)'s|Your) mind starts to fluctuate in and out of coherence\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex KnowledgeDistortsPattern = new(
        "^(?:(?<owner>.+?)'s|Your) knowledge of (?<oldSkill>.+?) distorts into knowledge of (?<newSkill>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GenomeFluctuatesPattern = new(
        "^(?:(?<owner>.+?)'s|Your) genome fluctuates and genes start turning on and off at random\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MutationTransmutesPattern = new(
        "^(?:(?<owner>.+?)'s|Your) mutation (?<oldMutation>.+?) transmutes into the mutation (?<newMutation>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.Liquids.LiquidWarmStatic");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "GlitchSkills", new[] { gameObjectType });
        AddTarget(targets, targetType, "GlitchMutations", new[] { gameObjectType });
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

        DynamicTextObservability.RecordTransform(Context, "LiquidWarmStatic.Queue", message, translated);
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
        if (TryTranslateLine(source, out translated))
        {
            return true;
        }

#pragma warning disable CA2249
        if (source.IndexOf('\n') >= 0 && TryTranslateLines(source, out translated))
#pragma warning restore CA2249
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateLine(string source, out string translated)
    {
        if (TryUnwrapWholeQudColor(source, "W", out var inner)
            && TryTranslateUnwrappedCore(inner, out var innerTranslated))
        {
            translated = "{{W|" + innerTranslated + "}}";
            return true;
        }

        return TryTranslateUnwrappedCore(source, out translated);
    }

    private static bool TryTranslateLines(string source, out string translated)
    {
        var lines = source.Split('\n');
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var suffix = lines[index].EndsWith("\r", StringComparison.Ordinal) ? "\r" : string.Empty;
            var line = suffix.Length == 0 ? lines[index] : lines[index].Substring(0, lines[index].Length - suffix.Length);
            if (TryTranslateLine(line, out var translatedLine))
            {
                lines[index] = translatedLine + suffix;
                changed = true;
            }
        }

        translated = changed ? string.Join("\n", lines) : source;
        return changed;
    }

    private static bool TryTranslateUnwrappedCore(string source, out string translated)
    {
        return TryTranslatePattern(
            MindFluctuatesPattern,
            source,
            (match, spans) => $"{RestorePossessiveOwner(match, spans)}の精神が一貫性を失って揺らぎ始めた。",
            out translated)
            || TryTranslatePattern(
                KnowledgeDistortsPattern,
                source,
                (match, spans) =>
                    $"{RestorePossessiveOwner(match, spans)}の{Restore(match, spans, "oldSkill")}の知識が"
                    + $"{Restore(match, spans, "newSkill")}の知識へ歪んだ。",
                out translated)
            || TryTranslatePattern(
                GenomeFluctuatesPattern,
                source,
                (match, spans) => $"{RestorePossessiveOwner(match, spans)}のゲノムが揺らぎ、遺伝子が無作為にオンオフし始めた。",
                out translated)
            || TryTranslatePattern(
                MutationTransmutesPattern,
                source,
                (match, spans) =>
                    $"{RestorePossessiveOwner(match, spans)}の変異{Restore(match, spans, "oldMutation")}が"
                    + $"変異{Restore(match, spans, "newMutation")}へ変質した。",
                out translated);
    }

    private static bool TryUnwrapWholeQudColor(string source, string color, out string inner)
    {
        var prefix = "{{" + color + "|";
        if (source.StartsWith(prefix, StringComparison.Ordinal)
            && source.EndsWith("}}", StringComparison.Ordinal))
        {
            inner = source.Substring(prefix.Length, source.Length - prefix.Length - 2);
            return true;
        }

        inner = source;
        return false;
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

    private static string RestorePossessiveOwner(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return match.Groups["owner"].Success ? Restore(match, spans, "owner") : "あなた";
    }
}
