using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LiquidLeakMessageTranslationPatch
{
    private const string Context = nameof(LiquidLeakMessageTranslationPatch);

    private static readonly Regex LeakPattern = new(
        "^(?<owner>.+?) leaks? (?<amount>\\d+) drams? of (?<liquid>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.LeakWhenBroken",
                     "DistributeLiquid",
                     ["XRL.World.Parts.LiquidVolume"]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.LeaksFluid",
                     "DistributeLiquid",
                     Type.EmptyTypes))
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

        if (!TryTranslate(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
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

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, string[] parameterTypeNames)
    {
        var parameters = new Type[parameterTypeNames.Length];
        for (var index = 0; index < parameterTypeNames.Length; index++)
        {
            var parameterType = AccessTools.TypeByName(parameterTypeNames[index]);
            if (parameterType is null)
            {
                Trace.TraceError("QudJP: {0} parameter type not found: {1}", Context, parameterTypeNames[index]);
                yield break;
            }

            parameters[index] = parameterType;
        }

        foreach (var method in ResolveTarget(typeName, methodName, parameters))
        {
            yield return method;
        }
    }

    private static bool TryTranslate(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = LeakPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var owner = RestoreOwner(match, spans);
        var liquid = Restore(match, spans, "liquid");
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            string.Concat(owner, "から", liquid, ' ', match.Groups["amount"].Value, "ドラムが漏れ出た。"),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        if (!string.Equals(groupName, "liquid", StringComparison.Ordinal))
        {
            return restored;
        }

        var translated = LiquidVolumeFragmentTranslator.TranslateLiquidPhrasePreservingColors(
            ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim());
        return translated is null
            ? restored
            : translated;
    }

    private static string RestoreOwner(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["owner"];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        return StripLeadingEnglishArticlePreservingColors(restored);
    }

    private static string StripLeadingEnglishArticlePreservingColors(string source)
    {
        var direct = StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        var normalized = StringHelpers.StripLeadingEnglishArticle(visible, includeCapitalizedDefiniteArticle: true);
        return string.Equals(normalized, visible, StringComparison.Ordinal)
            ? source
            : ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => normalized).Trim();
    }
}
