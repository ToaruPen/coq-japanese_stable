using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class SultanShrineWrapperTranslator
{
    private const string TemplateKeyWithQuality = "QudJP.ShrineWrapper.AncientSultan";
    private const string TemplateKeyWithoutQuality = "QudJP.ShrineWrapper.AncientSultan.NoQuality";
    private const string FamilyTagWithQuality = "ShrineWrapper.AncientSultan";
    private const string FamilyTagWithoutQuality = "ShrineWrapper.AncientSultan.NoQuality";

    // The trailing "\n\n{quality}" segment is appended only on the tooltip / popup path. Routes
    // that surface the bare Description.Short (set by SultanShrine.ShrineInitialize) see only
    // the prefix + gospel, so the quality block must be optional or those routes pass through
    // English.
    private static readonly Regex CompositePattern =
        new Regex(
            "^The shrine depicts a significant event from the life of the ancient sultan (?<sultan>.+?):\\n\\n(?<gospel>.+?)(?:\\n\\n(?<quality>.+?))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    // Quality strings come from XRL.Rules.Strings.WoundLevel. The set is finite and shipped in
    // the world-shrine.ja.json dictionary; entries missing from the dictionary fall through to
    // the original English so a future game-side addition does not silently break the wrapper.
    private static readonly Dictionary<string, string> QualityKeys = new(StringComparer.Ordinal)
    {
        ["Perfect"] = "QudJP.ShrineWrapper.Quality.Perfect",
        ["Fine"] = "QudJP.ShrineWrapper.Quality.Fine",
        ["Lightly Damaged"] = "QudJP.ShrineWrapper.Quality.LightlyDamaged",
        ["Damaged"] = "QudJP.ShrineWrapper.Quality.Damaged",
        ["Badly Damaged"] = "QudJP.ShrineWrapper.Quality.BadlyDamaged",
        ["Undamaged"] = "QudJP.ShrineWrapper.Quality.Undamaged",
        ["Badly Wounded"] = "QudJP.ShrineWrapper.Quality.BadlyWounded",
        ["Wounded"] = "QudJP.ShrineWrapper.Quality.Wounded",
        ["Injured"] = "QudJP.ShrineWrapper.Quality.Injured",
    };

    internal static bool TryTranslateMessage(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        string route,
        out string translated)
    {
        var match = CompositePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var qualityGroup = match.Groups["quality"];
        var hasQuality = qualityGroup.Success;
        var templateKey = hasQuality ? TemplateKeyWithQuality : TemplateKeyWithoutQuality;
        if (!Translator.TryGetTranslation(templateKey, out var template))
        {
            translated = source;
            return false;
        }

        var sultan = TranslateSultanName(match.Groups["sultan"].Value);
        var gospel = TranslateGospel(match.Groups["gospel"].Value, route);
        var quality = hasQuality ? TranslateQuality(qualityGroup.Value) : null;

        if (!TryCompose(template, sultan, gospel, quality, out var composed, out var qualityStart))
        {
            translated = source;
            return false;
        }

        if (hasQuality && quality is not null && spans is not null && spans.Count > 0 && qualityStart >= 0)
        {
            var prefix = composed.Substring(0, qualityStart);
            var middle = composed.Substring(qualityStart, quality.Length);
            var suffix = composed.Substring(qualityStart + quality.Length);
            composed = prefix
                + ColorAwareTranslationComposer.RestoreCapture(middle, spans, qualityGroup)
                + suffix;
        }

        DynamicTextObservability.RecordTransform(
            nameof(MessagePatternTranslator),
            hasQuality ? FamilyTagWithQuality : FamilyTagWithoutQuality,
            source,
            composed);

        translated = composed;
        return true;
    }

    private static string TranslateSultanName(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var direct = Translator.Translate(source);
        return string.Equals(direct, source, StringComparison.Ordinal) ? source : direct;
    }

    private static string TranslateGospel(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return JournalPatternTranslator.Translate(source, route);
    }

    private static string TranslateQuality(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!QualityKeys.TryGetValue(source, out var key))
        {
            return source;
        }

        if (!Translator.TryGetTranslation(key, out var translation))
        {
            return source;
        }

        return translation;
    }

    private static bool TryCompose(
        string template,
        string sultan,
        string gospel,
        string? quality,
        out string composed,
        out int qualityStart)
    {
        const string sultanPlaceholder = "{sultan}";
        const string gospelPlaceholder = "{gospel}";
        const string qualityPlaceholder = "{quality}";

        var sultanIndex = template.IndexOf(sultanPlaceholder, StringComparison.Ordinal);
        var gospelIndex = template.IndexOf(gospelPlaceholder, StringComparison.Ordinal);
        if (sultanIndex < 0 || gospelIndex < 0 || sultanIndex >= gospelIndex)
        {
            composed = string.Empty;
            qualityStart = -1;
            return false;
        }

        if (quality is null)
        {
            composed = template
                .Replace(sultanPlaceholder, sultan)
                .Replace(gospelPlaceholder, gospel);
            qualityStart = -1;
            return true;
        }

        var qualityIndex = template.IndexOf(qualityPlaceholder, StringComparison.Ordinal);
        if (qualityIndex < 0 || gospelIndex >= qualityIndex)
        {
            composed = string.Empty;
            qualityStart = -1;
            return false;
        }

        composed = template
            .Replace(sultanPlaceholder, sultan)
            .Replace(gospelPlaceholder, gospel)
            .Replace(qualityPlaceholder, quality);

        var sultanDelta = sultan.Length - sultanPlaceholder.Length;
        var gospelDelta = gospel.Length - gospelPlaceholder.Length;
        qualityStart = qualityIndex + sultanDelta + gospelDelta;
        return true;
    }
}
