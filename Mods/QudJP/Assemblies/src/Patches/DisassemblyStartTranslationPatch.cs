using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DisassemblyStartTranslationPatch
{
    private const string Context = nameof(DisassemblyStartTranslationPatch);
    private static readonly Regex ReverseEngineerPromptPattern = new(
        "^Do you want to try to reverse engineer (?<item>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StartDisassemblingPattern = new(
        "^You start disassembling (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisassembleEurekaBuildReceiptPattern = new(
        "^(?:You disassemble\\s+(?:(?:the|your)\\s+)?)?(?<item>.+?)\\.\\s+Eureka! You may now build\\s+(?<build>.+?)\\.\\s+You receive tinkering bits <(?<bits>.+?)>\\.*!?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DisassembleBitsReceiptPattern = new(
        "^You disassemble\\s+(?:(?:the|your)\\s+)?(?<item>.+?)\\.\\s+You receive tinkering bits <(?<bits>.+?)>\\.*!?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DisassembleEurekaBuildAndModReceiptPattern = new(
        "^(?:You disassemble\\s+(?:(?:the|your)\\s+)?)?(?<item>.+?)\\.\\s+Eureka! You may now build\\s+(?<build>.+?)\\s+and\\s+mod items with the\\s+(?<mods>.+?)\\s+(?:mod|mods)\\.\\s+You receive tinkering bits <(?<bits>.+?)>\\.*!?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DisassembleEurekaModReceiptPattern = new(
        "^(?:You disassemble\\s+(?:(?:the|your)\\s+)?)?(?<item>.+?)\\.\\s+Eureka! You may now mod items with the\\s+(?<mods>.+?)\\s+(?:mod|mods)\\.\\s+You receive tinkering bits <(?<bits>.+?)>\\.*!?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DisplayNameListSeparators = new(
        "(, and | and |, )",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingColoredDisplayNamePrefix = new(
        "^(?<prefix>(?:\\{\\{[^{}|]+\\|[^{}]*\\}\\}\\s*)+)(?<rest>\\S.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] LocationSuffixes =
    {
        " here",
        " to the north",
        " to the south",
        " to the east",
        " to the west",
        " to the northeast",
        " to the northwest",
        " to the southeast",
        " to the southwest",
    };
    private static readonly string[] LeadingItemPrefixes =
    {
        "the ",
        "your ",
    };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Tinkering.Disassembly");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        foreach (var methodName in new[] { "Continue", "End" })
        {
            var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
            if (method is not null)
            {
                targets.Add(method);
                continue;
            }

            Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
        }

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

        if (!TryTranslateStartDisassemblingMessage(message, out var translated)
            && !TryTranslateDisassembleReceiptMessage(message, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateReverseEngineerPrompt(source, out translated)
            || TryTranslateDisassembleReceiptMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateReverseEngineerPrompt(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ReverseEngineerPromptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{TranslateDisplayNameCapture(NormalizeItemCapture(RestoreCapture(match, spans, "item")))}をリバースエンジニアリングしてみる？";
        return true;
    }

    private static bool TryTranslateStartDisassemblingMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StartDisassemblingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{TranslateDisplayNameCapture(NormalizeItemCapture(RestoreCapture(match, spans, "item")))}の分解を始めた。";
        return true;
    }

    private static bool TryTranslateDisassembleReceiptMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var buildAndModMatch = DisassembleEurekaBuildAndModReceiptPattern.Match(stripped);
        if (buildAndModMatch.Success)
        {
            translated = BuildEurekaReceiptTranslation(
                buildAndModMatch,
                spans,
                RestoreCapture(buildAndModMatch, spans, "build"),
                RestoreCapture(buildAndModMatch, spans, "mods"));
            return true;
        }

        var match = DisassembleEurekaBuildReceiptPattern.Match(stripped);
        if (match.Success)
        {
            translated = BuildEurekaReceiptTranslation(
                match,
                spans,
                RestoreCapture(match, spans, "build"),
                mods: null);
            return true;
        }

        var modMatch = DisassembleEurekaModReceiptPattern.Match(stripped);
        if (modMatch.Success)
        {
            translated = BuildEurekaReceiptTranslation(
                modMatch,
                spans,
                build: null,
                RestoreCapture(modMatch, spans, "mods"));
            return true;
        }

        var bitsOnlyMatch = DisassembleBitsReceiptPattern.Match(stripped);
        if (bitsOnlyMatch.Success)
        {
            var item = TranslateDisplayNameCapture(RestoreDisassemblyItemCapture(bitsOnlyMatch, spans, "item"));
            var bits = RestoreCapture(bitsOnlyMatch, spans, "bits");
            translated = $"{item}を分解し、修理ビット<{bits}>を受け取った。";
            return true;
        }

        translated = source;
        return false;
    }

    private static string BuildEurekaReceiptTranslation(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string? build,
        string? mods)
    {
        var item = TranslateDisplayNameCapture(RestoreDisassemblyItemCapture(match, spans, "item"));
        var bits = RestoreCapture(match, spans, "bits");
        var suffixes = new List<string>();
        if (!string.IsNullOrWhiteSpace(build))
        {
            suffixes.Add($"{TranslateDisplayNameCapture(build!)}を作れるようになった。");
        }

        if (!string.IsNullOrWhiteSpace(mods))
        {
            suffixes.Add($"{TranslateDisplayNameList(mods!)} modでアイテムを改造できるようになった。");
        }

        return $"{item}を分解し、修理ビット<{bits}>を受け取った。ひらめいた！ {string.Concat(suffixes)}";
    }

    private static string StripTrailingLocationSuffix(string source)
    {
        var trimmed = source.Trim();
        for (var index = 0; index < LocationSuffixes.Length; index++)
        {
            var suffix = LocationSuffixes[index];
            if (trimmed.EndsWith(suffix, StringComparison.Ordinal))
            {
                return trimmed.Substring(0, trimmed.Length - suffix.Length).TrimEnd();
            }
        }

        return trimmed;
    }

    private static string NormalizeItemCapture(string source)
    {
        var trimmed = StripTrailingLocationSuffix(source);
        for (var index = 0; index < LeadingItemPrefixes.Length; index++)
        {
            var prefix = LeadingItemPrefixes[index];
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed.Substring(prefix.Length).TrimStart();
            }
        }

        return trimmed;
    }

    private static string RestoreDisassemblyItemCapture(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName)
    {
        return NormalizeItemCapture(RestoreCapture(match, spans, groupName));
    }

    private static string TranslateDisplayNameCapture(string source)
    {
        var leadingColoredPrefix = LeadingColoredDisplayNamePrefix.Match(source.Trim());
        if (leadingColoredPrefix.Success)
        {
            return TranslateColoredPrefix(leadingColoredPrefix.Groups["prefix"].Value)
                + TranslateDisplayNameCapture(leadingColoredPrefix.Groups["rest"].Value);
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => GetDisplayNameRouteTranslator.TranslatePreservingColors(visible, Context));
    }

    private static string TranslateColoredPrefix(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => GetDisplayNameRouteTranslator.TranslatePreservingColors(visible, Context));
    }

    private static string TranslateDisplayNameList(string source)
    {
        var segments = DisplayNameListSeparators.Split(source);
        for (var index = 0; index < segments.Length; index++)
        {
            segments[index] = segments[index] switch
            {
                ", and " => "、",
                " and " => "と",
                ", " => "、",
                _ => TranslateDisplayNameCapture(segments[index]),
            };
        }

        return string.Concat(segments);
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
