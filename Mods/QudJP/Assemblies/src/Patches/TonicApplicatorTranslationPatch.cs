using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TonicApplicatorTranslationPatch
{
    private const string Context = nameof(TonicApplicatorTranslationPatch);

    private static readonly Regex LoveNoEffectPattern = new(
        "^(?<subject>.+?) looks? you over and metabolizes? the love tonic with no effect\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SphynxSaltApplyPattern = new(
        "^(?<owner>.+?) appl(?:y|ies) (?<tonic>.+?sphynx salt.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} event type not found.", Context);
            yield break;
        }

        foreach (var method in ResolveTarget("XRL.World.Parts.LoveTonicApplicator", eventType))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Parts.SphynxSalt_Tonic_Applicator", eventType))
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

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context + "." + detail, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, Type eventType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.FireEvent target not found.", Context, targetType.FullName);
    }

    private static bool TryTranslate(string source, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = LoveNoEffectPattern.Match(stripped);
        if (match.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                string.Concat(RestoreActor(match, spans, "subject"), "はあなたをじろじろ見てからラブトニックを代謝したが、効果はなかった。"),
                spans,
                stripped.Length,
                source);
            detail = "LoveNoEffect";
            return true;
        }

        match = SphynxSaltApplyPattern.Match(stripped);
        if (match.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                string.Concat(RestoreActor(match, spans, "owner"), "は", Restore(match, spans, "tonic"), "を使った。"),
                spans,
                stripped.Length,
                source);
            detail = "SphynxSaltApply";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreActor(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return StripLeadingEnglishArticlePreservingColors(Restore(match, spans, groupName));
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
