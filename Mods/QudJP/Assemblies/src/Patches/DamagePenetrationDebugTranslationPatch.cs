using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DamagePenetrationDebugTranslationPatch
{
    private const string Context = nameof(DamagePenetrationDebugTranslationPatch);
    private static readonly Regex PenetratedPattern = new(
        "^Penned with Roll:(?<roll>-?\\d+) Final:(?<final>-?\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FailedPattern = new(
        "^Didn't pen with (?<roll>-?\\d+) Final:(?<final>-?\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BonusPattern = new(
        "^Penning Bonus: (?<bonus>-?\\d+) Max: (?<max>-?\\d+) Used: (?<used>-?\\d+) Target: (?<target>-?\\d+)\\(Penned (?<count>-?\\d+) times\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.Rules.Stat");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "RollDamagePenetrations", new[] { typeof(int), typeof(int), typeof(int) });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.RollDamagePenetrations(int,int,int) not found.", Context);
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

        if (!TryTranslateDebugMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateDebugMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PenetratedPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreWholeSource(
                $"貫通成功 ロール:{match.Groups["roll"].Value} 最終:{match.Groups["final"].Value}",
                spans,
                stripped.Length,
                source);
            return true;
        }

        match = FailedPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreWholeSource(
                $"貫通失敗 ロール:{match.Groups["roll"].Value} 最終:{match.Groups["final"].Value}",
                spans,
                stripped.Length,
                source);
            return true;
        }

        match = BonusPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreWholeSource(
                $"貫通ボーナス: {match.Groups["bonus"].Value} 最大: {match.Groups["max"].Value} 使用: {match.Groups["used"].Value} 目標: {match.Groups["target"].Value}(貫通 {match.Groups["count"].Value} 回)",
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    private static string RestoreWholeSource(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int strippedLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            strippedLength,
            source);
    }
}
