using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfirePreserveTranslationPatch
{
    private const string Context = nameof(CampfirePreserveTranslationPatch);

    private static readonly Regex PreservedLinePattern = new(
        "^(?<source>.+?) into (?<count>\\d+) (?<serving>.+?) of (?<result>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SomeSourcePattern = new(
        "^some (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ArticleSourcePattern = new(
        "^(?:a|an) (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex JapaneseCharacterPattern = new(
        "[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var campfireType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        if (campfireType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, campfireType, "Preserve", Type.EmptyTypes);
        AddTarget(targets, campfireType, "PreserveExotic", Type.EmptyTypes);
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

    internal static void ResetForTests()
    {
        activeDepth = 0;
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

        if (!TryTranslatePreservedResult(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    internal static bool TryTranslateMessageLogMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslatePreservedResult(source, out translated))
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

    private static bool TryTranslatePreservedResult(string source, out string translated)
    {
        const string header = "You preserved:\n\n";
        if (TryStripLeadingQudColor(source, out var colorPrefix, out var uncolored)
            && TryTranslatePreservedResult(uncolored, out var uncoloredTranslated))
        {
            translated = colorPrefix + uncoloredTranslated;
            return true;
        }

        if (!source.StartsWith(header, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var body = source.Substring(header.Length);
        if (body.Length == 0)
        {
            translated = source;
            return false;
        }

        var lines = body.Split('\n');
        var builder = new StringBuilder("保存した:\n\n");
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append(TranslatePreservedLine(lines[index]));
        }

        translated = builder.ToString();
        return true;
    }

    private static bool TryStripLeadingQudColor(string source, out string colorPrefix, out string uncolored)
    {
        if (source.Length < 2
            || (source[0] != '&' && source[0] != '^')
            || !IsQudColorCode(source[1]))
        {
            colorPrefix = string.Empty;
            uncolored = source;
            return false;
        }

        colorPrefix = source.Substring(0, 2);
        uncolored = source.Substring(2);
        return true;
    }

    private static string TranslatePreservedLine(string source)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PreservedLinePattern.Match(stripped);
        if (!match.Success)
        {
            return source;
        }

        var sourceItem = TranslatePreservedSourceWithTrailingColor(match, spans);
        var count = match.Groups["count"].Value;
        var serving = TranslateServingUnit(Restore(match, spans, "serving"));
        var result = TranslateDisplayNameOrSame(Restore(match, spans, "result"));
        return $"{sourceItem}を{count}{serving}の{result}に保存した。";
    }

    private static string TranslatePreservedSourceWithTrailingColor(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var sourceGroup = match.Groups["source"];
        var restored = Restore(match, spans, "source");
        if (!TryGetTrailingInlineColorTokens(spans, sourceGroup, out var trailingColor))
        {
            return TranslatePreservedSource(restored);
        }

        return TranslatePreservedSource(restored) + trailingColor;
    }

    private static bool TryGetTrailingInlineColorTokens(
        IReadOnlyList<ColorSpan> spans,
        Group group,
        out string trailingColor)
    {
        var scanIndex = group.Index + group.Length;
        var builder = new StringBuilder();
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            // ColorSpan.Index is a stripped visible-text index; adjacent zero-width inline tokens share it.
            if (span.Index == scanIndex
                && span.Token.Length == 2
                && (span.Token[0] == '&' || span.Token[0] == '^'))
            {
                builder.Append(span.Token);
            }
        }

        trailingColor = builder.ToString();
        return trailingColor.Length > 0;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslatePreservedSource(string source)
    {
        if (CookingIngredientFragmentTranslator.TryTranslate(source, out var ingredient))
        {
            return ingredient;
        }

        var match = SomeSourcePattern.Match(source);
        if (match.Success
            && TryTranslateDisplayNameOrAlreadyLocalized(match.Groups["name"].Value, out var someName))
        {
            return someName + "少々";
        }

        match = ArticleSourcePattern.Match(source);
        if (match.Success
            && TryTranslateDisplayNameOrAlreadyLocalized(match.Groups["name"].Value, out var articleName))
        {
            return articleName;
        }

        return TranslateDisplayNameOrSame(source);
    }

    private static bool TryTranslateDisplayNameOrAlreadyLocalized(string source, out string translated)
    {
        if (ContainsJapaneseCharacters(source))
        {
            translated = source;
            return true;
        }

        translated = TranslateDisplayNameOrSame(source);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static string TranslateDisplayNameOrSame(string source)
    {
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(source, Context);
    }

    private static string TranslateServingUnit(string source)
    {
        if (string.Equals(source, "serving", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "servings", StringComparison.OrdinalIgnoreCase))
        {
            return "食分";
        }

        if (string.Equals(source, "dram", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "drams", StringComparison.OrdinalIgnoreCase))
        {
            return "ドラム分";
        }

        return source;
    }

    private static bool ContainsJapaneseCharacters(string source)
    {
        return JapaneseCharacterPattern.IsMatch(source);
    }

    private static bool IsQudColorCode(char value)
    {
        return (value >= 'a' && value <= 'z')
            || (value >= 'A' && value <= 'Z');
    }
}
