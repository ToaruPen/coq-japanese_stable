using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class GameSummaryTextTranslator
{
    private const string Context = nameof(GameSummaryTextTranslator);

    private static readonly Regex TitlePattern = new Regex(
        @"^\{\{C\|(?<mark>.)\}\} (?<kind>Game summary|Chronology|Final messages) for \{\{W\|(?<name>.+?)\}\} \{\{C\|\k<mark>\}\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EndedPattern = new Regex(
        @"^This game ended on (?<date>.+?) at (?<time>.+?)\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LevelPattern = new Regex(
        @"^You were level \{\{C\|(?<level>\d+)\}\}\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScorePattern = new Regex(
        @"^You scored \{\{C\|(?<score>-?\d+)\}\} points?\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TurnsPattern = new Regex(
        @"^You survived for \{\{C\|(?<turns>\d+)\}\} turns?\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FoundLairsPattern = new Regex(
        @"^You found \{\{C\|(?<count>\d+)\}\} lairs?\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NamedItemsPattern = new Regex(
        @"^You named \{\{C\|(?<count>\d+)\}\} items?\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GeneratedItemsPattern = new Regex(
        @"^You generated \{\{C\|(?<count>\d+)\}\} storied items?\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AdvancedArtifactPattern = new Regex(
        @"^The most advanced artifact in your possession was (?<artifact>.+)\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GameModePattern = new Regex(
        @"^This game was played in (?<mode>.+?) mode\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string TranslateCause(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                return string.Empty;
            }

            return source;
        }

        return TranslateLine(source!, "GameSummary.Cause");
    }

    internal static string TranslateDetails(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                return string.Empty;
            }

            return source;
        }

        var lines = source!.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = TranslateLine(lines[index], "GameSummary.Details");
        }

        return string.Join("\n", lines);
    }

    private static string TranslateLine(string source, string family)
    {
        var translated = TranslateLineCore(source);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(Context, family, source, translated);
        }

        return translated;
    }

    private static string TranslateLineCore(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact))
        {
            return exact;
        }

        var titleMatch = TitlePattern.Match(source);
        if (titleMatch.Success)
        {
            var templateKey = titleMatch.Groups["kind"].Value + " for {0}";
            var template = Translator.Translate(templateKey);
            if (!string.Equals(template, templateKey, StringComparison.Ordinal))
            {
                return "{{C|" + titleMatch.Groups["mark"].Value + "}} "
                    + string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        template,
                        "{{W|" + titleMatch.Groups["name"].Value + "}}")
                    + " {{C|" + titleMatch.Groups["mark"].Value + "}}";
            }
        }

        var endedMatch = EndedPattern.Match(source);
        if (endedMatch.Success)
        {
            return FormatTemplate(
                "This game ended on {0} at {1}.",
                source,
                endedMatch.Groups["date"].Value,
                endedMatch.Groups["time"].Value);
        }

        var levelMatch = LevelPattern.Match(source);
        if (levelMatch.Success)
        {
            return FormatTemplate("You were level {0}.", source, "{{C|" + levelMatch.Groups["level"].Value + "}}");
        }

        var scoreMatch = ScorePattern.Match(source);
        if (scoreMatch.Success)
        {
            return FormatTemplate("You scored {0} points.", source, "{{C|" + scoreMatch.Groups["score"].Value + "}}");
        }

        var turnsMatch = TurnsPattern.Match(source);
        if (turnsMatch.Success)
        {
            return FormatTemplate("You survived for {0} turns.", source, "{{C|" + turnsMatch.Groups["turns"].Value + "}}");
        }

        var foundLairsMatch = FoundLairsPattern.Match(source);
        if (foundLairsMatch.Success)
        {
            return FormatTemplate("You found {0} lairs.", source, "{{C|" + foundLairsMatch.Groups["count"].Value + "}}");
        }

        var namedItemsMatch = NamedItemsPattern.Match(source);
        if (namedItemsMatch.Success)
        {
            return FormatTemplate("You named {0} items.", source, "{{C|" + namedItemsMatch.Groups["count"].Value + "}}");
        }

        var generatedItemsMatch = GeneratedItemsPattern.Match(source);
        if (generatedItemsMatch.Success)
        {
            return FormatTemplate("You generated {0} storied items.", source, "{{C|" + generatedItemsMatch.Groups["count"].Value + "}}");
        }

        var artifactMatch = AdvancedArtifactPattern.Match(source);
        if (artifactMatch.Success)
        {
            var artifact = ColorAwareTranslationComposer.TranslatePreservingColors(
                artifactMatch.Groups["artifact"].Value,
                static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var artifactExact)
                    ? artifactExact
                    : visible);
            return FormatTemplate("The most advanced artifact in your possession was {0}.", source, artifact);
        }

        var gameModeMatch = GameModePattern.Match(source);
        if (gameModeMatch.Success)
        {
            var sourceMode = gameModeMatch.Groups["mode"].Value;
            var mode = StringHelpers.TryGetTranslationExactOrLowerAscii(sourceMode, out var translatedMode)
                ? translatedMode
                : sourceMode;
            return FormatTemplate("This game was played in {0} mode.", source, mode);
        }

        return source;
    }

    private static string FormatTemplate(string key, string fallback, params object[] args)
    {
        var template = Translator.Translate(key);
        return string.Equals(template, key, StringComparison.Ordinal)
            ? fallback
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, template, args);
    }
}
