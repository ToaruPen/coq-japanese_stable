using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FireSuppressionDischargeTranslationPatch
{
    private const string Context = nameof(FireSuppressionDischargeTranslationPatch);

    private static readonly Regex FireSuppressionSelfPattern = new(
        "^(?<amount>\\d+) drams? of (?<liquid>.+?) discharges all over you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FireSuppressionTargetPattern = new(
        "^(?<amount>\\d+) drams? of (?<liquid>.+?) discharges all over (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CyberneticsSelfPattern = new(
        "^Your (?<device>.+?) discharges (?<amount>\\d+) drams? of (?<liquid>.+?) all over you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CyberneticsTargetPattern = new(
        "^(?<owner>.+(?:'s|s'|の)) (?<device>.+?) discharges (?<amount>\\d+) drams? of (?<liquid>.+?) all over (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.FireSuppressionSystem",
                     "CheckFireSuppression",
                     ["XRL.World.GameObject"]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.CyberneticsFireSuppressionSystem",
                     "TurnTick",
                     [typeof(long), typeof(int)]))
        {
            yield return method;
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

        if (!TryTranslate(message, out var translated, out var detail))
        {
            return false;
        }

        var markedTranslated = MessageFrameTranslator.MarkDirectTranslation(translated);
        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context + "." + detail, message, markedTranslated);
        message = markedTranslated;
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, string[] parameterTypeNames)
    {
        var parameterTypes = new Type[parameterTypeNames.Length];
        for (var index = 0; index < parameterTypeNames.Length; index++)
        {
            var parameterType = AccessTools.TypeByName(parameterTypeNames[index]);
            if (parameterType is null)
            {
                Trace.TraceError("QudJP: {0} parameter type not found: {1}", Context, parameterTypeNames[index]);
                yield break;
            }

            parameterTypes[index] = parameterType;
        }

        foreach (var method in ResolveTarget(typeName, methodName, parameterTypes))
        {
            yield return method;
        }
    }

    private static bool TryTranslate(string source, out string translated, out string detail)
    {
        if (TryBuild(
                FireSuppressionSelfPattern,
                source,
                static (match, spans) => BuildSimpleDischarge(match, spans, "あなた"),
                out translated))
        {
            detail = "FireSuppressionSelf";
            return true;
        }

        if (TryBuild(
                FireSuppressionTargetPattern,
                source,
                static (match, spans) => BuildSimpleDischarge(match, spans, RestoreTarget(match, spans, "target")),
                out translated))
        {
            detail = "FireSuppressionTarget";
            return true;
        }

        if (TryBuild(
                CyberneticsSelfPattern,
                source,
                static (match, spans) => BuildDeviceDischarge(match, spans, "あなたの", "あなた"),
                out translated))
        {
            detail = "CyberneticsSelf";
            return true;
        }

        if (TryBuild(
                CyberneticsTargetPattern,
                source,
                static (match, spans) => BuildDeviceDischarge(match, spans, RestoreOwner(match, spans, "owner"), RestoreTarget(match, spans, "target")),
                out translated))
        {
            detail = "CyberneticsTarget";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryBuild(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> build,
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
            build(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string BuildSimpleDischarge(Match match, IReadOnlyList<ColorSpan> spans, string target)
    {
        return string.Concat(
            Restore(match, spans, "liquid"),
            ' ',
            match.Groups["amount"].Value,
            "ドラムが",
            target,
            "の全身に放出された。");
    }

    private static string BuildDeviceDischarge(Match match, IReadOnlyList<ColorSpan> spans, string owner, string target)
    {
        return string.Concat(
            owner,
            Restore(match, spans, "device"),
            "が",
            Restore(match, spans, "liquid"),
            ' ',
            match.Groups["amount"].Value,
            "ドラムを",
            target,
            "の全身に放出した。");
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreOwner(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var owner = Restore(match, spans, groupName);
        if (owner.EndsWith("の", StringComparison.Ordinal))
        {
            return owner;
        }

        if (owner.EndsWith("'s", StringComparison.Ordinal))
        {
            return owner.Substring(0, owner.Length - 2) + "の";
        }

        if (owner.EndsWith("s'", StringComparison.Ordinal))
        {
            return owner.Substring(0, owner.Length - 1) + "の";
        }

        return owner + "の";
    }

    private static string RestoreTarget(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = Restore(match, spans, groupName);
        if (string.Equals(group.Value.Trim(), "it", StringComparison.OrdinalIgnoreCase))
        {
            return ColorAwareTranslationComposer.RestoreCapture("それ", spans, group).Trim();
        }

        var normalized = StringHelpers.StripLeadingEnglishArticle(group.Value.Trim(), includeCapitalizedDefiniteArticle: true);
        if (string.Equals(normalized, group.Value.Trim(), StringComparison.Ordinal))
        {
            return restored;
        }

        return ColorAwareTranslationComposer.RestoreCapture(normalized, spans, group).Trim();
    }
}
