using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DynamicQuestItemNameMutationTranslator
{
    private static readonly string[] SacredAdjectives =
    {
        "sacred",
        "holy",
        "divine",
        "hallowed",
        "angelic",
        "consecrated",
        "godly",
        "pure",
        "sanctified",
        "venerable",
    };

    private static readonly string SacredAdjectivePattern = BuildAlternation(SacredAdjectives);

    private static readonly string[] ItemCaptureDictionaryFiles =
    {
        "ui-displayname-atomic.ja.json",
    };

    private static readonly Regex PrefixPattern = new(
        "^(?<adjective>" + SacredAdjectivePattern + ") (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex OfThePattern = new(
        "^(?<item>.+?) of the (?<adjective>[A-Za-z'-]+) (?<noun>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (!TryTranslateCore(stripped, spans, out var translatedCore))
        {
            translated = original;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }

    private static bool TryTranslateCore(string source, IReadOnlyList<ColorSpan> spans, out string translated)
    {
        var match = OfThePattern.Match(source);
        if (match.Success
            && HistorySpiceComponentLookup.TryTranslateWord(match.Groups["adjective"].Value, out var adjective)
            && HistorySpiceComponentLookup.TryTranslateTitlePhrase(match.Groups["noun"].Value, out var noun)
            && TryTranslateItemCapture(match, spans, source, "item", out var item))
        {
            translated = adjective + noun + "の" + item;
            return true;
        }

        match = PrefixPattern.Match(source);
        if (match.Success
            && HistorySpiceComponentLookup.TryTranslateWord(match.Groups["adjective"].Value, out adjective)
            && TryTranslateItemCapture(match, spans, source, "item", out item))
        {
            translated = adjective + item;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateItemCapture(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string source,
        string groupName,
        out string translated)
    {
        var group = match.Groups[groupName];
        if (!TryTranslateItemCaptureVisible(group.Value, out var visible))
        {
            translated = group.Value;
            return false;
        }

        if (HasOnlyWholeSourceBoundaryWrapper(spans, source.Length))
        {
            translated = visible;
            return true;
        }

        translated = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(visible, spans, group).Trim();
        return true;
    }

    private static bool TryTranslateItemCaptureVisible(string source, out string translated)
    {
        var trimmed = source.Trim();
        var articleless = StringHelpers.StripLeadingEnglishArticle(trimmed);
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(articleless, ItemCaptureDictionaryFiles);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(articleless, out translated))
        {
            return true;
        }

        translated = trimmed;
        return false;
    }

    private static bool HasOnlyWholeSourceBoundaryWrapper(IReadOnlyList<ColorSpan> spans, int sourceLength)
    {
        return spans.Count == 2
            && spans[0].Index == 0
            && spans[1].Index == sourceLength;
    }

    private static string BuildAlternation(IEnumerable<string> values)
    {
        return string.Join("|", values.Select(Regex.Escape));
    }
}
