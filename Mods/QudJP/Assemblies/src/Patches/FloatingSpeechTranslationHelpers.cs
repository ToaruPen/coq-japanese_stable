using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class FloatingSpeechTranslationHelpers
{
    private static readonly Regex WhiteQuotedSpeechPattern = new(
        @"^{{W\|'(?<line>.+)'\}}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhiteWrappedSpeechPattern = new(
        @"^{{W\|(?<line>.+)\}}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhiteQuotedFramePattern = new(
        @"{{W\|'(?<line>.*?)'\}}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateWhiteWrappedParticle(
        string source,
        IReadOnlyDictionary<string, string> lines,
        out string translated)
    {
        var match = WhiteWrappedSpeechPattern.Match(source);
        if (!match.Success || !lines.TryGetValue(match.Groups["line"].Value, out var translatedLine))
        {
            translated = source;
            return false;
        }

        translated = "{{W|" + translatedLine + "}}";
        return true;
    }

    internal static bool TryTranslateWhiteQuotedFragment(
        string source,
        IReadOnlyDictionary<string, string> lines,
        out string translated)
    {
        var match = WhiteQuotedSpeechPattern.Match(source);
        if (!match.Success || !lines.TryGetValue(match.Groups["line"].Value, out var translatedLine))
        {
            translated = source;
            return false;
        }

        translated = "{{W|「" + translatedLine + "」}}";
        return true;
    }

    internal static string NormalizeActorForJapaneseFrame(string source)
    {
        var actor = StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true).Trim();
        return string.Equals(actor, "zealot", StringComparison.OrdinalIgnoreCase) ? "狂信者" : actor;
    }

    internal static bool TryNormalizeWhiteQuotedFrame(string source, out string translated)
    {
        translated = WhiteQuotedFramePattern.Replace(
            source,
            static match => "{{W|「" + match.Groups["line"].Value + "」}}",
            1);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }
}
