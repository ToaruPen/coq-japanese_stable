using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PsychometryTranslationPatch
{
    private const string Context = nameof(PsychometryTranslationPatch);

    private static readonly Regex TooComplexPattern = new(
        "^(?<subject>This artifact|These artifacts) (?:is|are) too complex for you to decipher (?<pronoun>its|their) (?<topic>function|method of construction)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UnderstandingPattern = new(
        "^You flush with understanding of the (?<possessive>artifact's|artifacts') past and determine (?<pronoun>it|them) to be (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MustDisassemblePattern = new(
        "^You must disassemble (?<item>.+?) in order to unlock (?<pronoun>its|their) secrets\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MustLearnReverseEngineerPattern = new(
        "^You must learn the way of the Reverse Engineer and disassemble (?<item>.+?) in order to unlock (?<pronoun>its|their) secrets\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LearnBlueprintPattern = new(
        "^You abide the memory of (?<source>.+?) creation\\. You learn to build (?<blueprint>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Psychometry");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", new[] { inventoryActionEventType });
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) target not found.", Context);
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslateCore(stripped, spans, out translated))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string stripped, IReadOnlyList<ColorSpan> spans, out string translated)
    {
        var match = TooComplexPattern.Match(stripped);
        if (match.Success)
        {
            var subject = string.Equals(match.Groups["subject"].Value, "These artifacts", StringComparison.Ordinal)
                ? "これらのアーティファクト"
                : "このアーティファクト";
            var topic = string.Equals(match.Groups["topic"].Value, "function", StringComparison.Ordinal)
                ? "機能"
                : "製法";
            translated = string.Concat(subject, "は複雑すぎてあなたにはその", topic, "を解読できない。");
            return true;
        }

        match = UnderstandingPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(
                "あなたは",
                TranslateArtifactPossessive(match.Groups["possessive"].Value),
                "の過去を理解し、それが",
                RestoreCapture(match, spans, "item"),
                "だと判明した。");
            return true;
        }

        match = MustDisassemblePattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(
                "秘密を解き明かすには",
                RestoreCapture(match, spans, "item"),
                "を分解しなければならない。");
            return true;
        }

        match = MustLearnReverseEngineerPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(
                "秘密を解き明かすにはリバースエンジニアの道を学び、",
                RestoreCapture(match, spans, "item"),
                "を分解しなければならない。");
            return true;
        }

        match = LearnBlueprintPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(
                TrimPossessive(RestoreCapture(match, spans, "source")),
                "の創造の記憶に身を委ねた。",
                RestoreCapture(match, spans, "blueprint"),
                "を作れるようになった。");
            return true;
        }

        translated = stripped;
        return false;
    }

    private static string TranslateArtifactPossessive(string value)
    {
        return string.Equals(value, "artifacts'", StringComparison.Ordinal)
            ? "それらのアーティファクト"
            : "そのアーティファクト";
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TrimPossessive(string value)
    {
        var trimmed = value.EndsWith("'s", StringComparison.Ordinal)
            ? value.Substring(0, value.Length - 2)
            : value.TrimEnd('\'');
        return StripLeadingArticle(trimmed);
    }

    private static string StripLeadingArticle(string value)
    {
        if (value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(4);
        }

        if (value.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(3);
        }

        return value.StartsWith("a ", StringComparison.OrdinalIgnoreCase)
            ? value.Substring(2)
            : value;
    }
}
