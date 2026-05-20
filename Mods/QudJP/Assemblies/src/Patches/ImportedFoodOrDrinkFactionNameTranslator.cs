using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ImportedFoodOrDrinkFactionNameTranslator
{
    private const string WorldGospelsDictionaryFile = "world-gospels.ja.json";

    private static readonly Regex OfTheRootPattern = new(
        "^(?<kind>[A-Z][A-Za-z]+) of the (?<root>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingKindPattern = new(
        "^(?<root>.+) (?<kind>[A-Z][A-Za-z]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(string? source, out string translated)
    {
        var sourceValue = source ?? string.Empty;
        if (sourceValue.Length == 0)
        {
            translated = sourceValue;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(sourceValue, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(sourceValue);
        if (TryTranslateCore(stripped, out var translatedCore))
        {
            translated = RestoreWhole(translatedCore, stripped, spans, sourceValue);
            return true;
        }

        translated = sourceValue;
        return false;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var ofTheMatch = OfTheRootPattern.Match(source);
        if (ofTheMatch.Success
            && TryTranslateKind(ofTheMatch.Groups["kind"].Value, out var kind))
        {
            translated = TranslateRoot(ofTheMatch.Groups["root"].Value) + "の" + kind;
            return true;
        }

        var trailingMatch = TrailingKindPattern.Match(source);
        if (trailingMatch.Success
            && TryTranslateKind(trailingMatch.Groups["kind"].Value, out kind))
        {
            translated = TranslateRoot(trailingMatch.Groups["root"].Value) + "の" + kind;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateKind(string source, out string translated)
    {
        if (HistorySpiceComponentLookup.TryTranslateWord(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return true;
        }

        var worldGospel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldGospelsDictionaryFile);
        if (worldGospel is not null)
        {
            translated = worldGospel;
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateRoot(string source)
    {
        if (HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source) is { } exact)
        {
            return exact;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out var titlePhrase)
            ? titlePhrase
            : source;
    }

    private static string RestoreWhole(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }
}
