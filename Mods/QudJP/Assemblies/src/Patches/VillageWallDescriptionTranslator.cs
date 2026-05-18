using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class VillageWallDescriptionTranslator
{
    private static readonly Regex LowercaseAsciiWordPattern = new(
        @"\b[a-z][A-Za-z'-]*\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CanvasPattern = new(
        "^A leather wrought from the peeled and tanned (?<skin>.+?) of (?<creature>.+?) was hung in a fashion inspired by (?<inspiration>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PlantRopePattern = new(
        "^(?<planks>.+?) of (?<plant>.+?) have been cut in a (?<style>.+?) style and bound together with (?<tar>.+?) and rope\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PlantFiberPattern = new(
        "^(?<planks>.+?) of (?<plant>.+?) have been cut in a (?<style>.+?) style and bound together with (?<tar>.+?) and (?<strips>.+?) of (?<bindingPlant>.+?) (?<fiberMaterial>fibrous bark)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PlantProperCreaturePattern = new(
        "^(?<planks>.+?) of (?<plant>.+?) have been cut in a (?<style>.+?) style and bound together with (?<tar>.+?) and the (?<skin>.+?) of (?<creature>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PlantCreaturePattern = new(
        "^(?<planks>.+?) of (?<plant>.+?) have been cut in a (?<style>.+?) style and bound together with (?<tar>.+?) and (?<creature>.+?) (?<skin>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex BoneProperCreaturePattern = new(
        "^Crack-stuck (?<tar>.+?) binds together the stiff and (?<style>.+?) (?<bones>.+?) of (?<creature>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex BoneCreaturePattern = new(
        "^Crack-stuck (?<tar>.+?) binds together the stiff and (?<style>.+?) (?<bones>.+?) of several slaughtered (?<creatures>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly IReadOnlyDictionary<string, string> CaptureTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["glowfish"] = "グロウフィッシュ",
            ["hide"] = "皮",
            ["spirals"] = "螺旋",
            ["Planks"] = "板材",
            ["planks"] = "板材",
            ["witchwood"] = "ウィッチウッド",
            ["layered"] = "層状",
            ["asphalt"] = "アスファルト",
            ["strips"] = "細片",
            ["livid creeper"] = "リヴィドクリーパー",
            ["fibrous bark"] = "繊維質の樹皮",
            ["bones"] = "骨",
        };

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (TryTranslatePattern(CanvasPattern, stripped, spans, original, match =>
                TranslateCapture(match, spans, "creature") + "の剥がしてなめした" + TranslateCapture(match, spans, "skin")
                + "から作られた革が、" + TranslateCapture(match, spans, "inspiration") + "に着想を得た様式で掛けられている。",
                out translated)
            || TryTranslatePattern(PlantRopePattern, stripped, spans, original, match =>
                TranslateCapture(match, spans, "plant") + "の" + TranslateCapture(match, spans, "planks") + "が" + TranslateCapture(match, spans, "style")
                + "様式に切り出され、" + TranslateCapture(match, spans, "tar") + "と縄で束ねられている。",
                out translated)
            || TryTranslatePattern(PlantProperCreaturePattern, stripped, spans, original, match =>
                TranslateCapture(match, spans, "plant") + "の" + TranslateCapture(match, spans, "planks") + "が" + TranslateCapture(match, spans, "style")
                + "様式に切り出され、" + TranslateCapture(match, spans, "tar") + "と" + TranslateCapture(match, spans, "creature") + "の"
                + TranslateCapture(match, spans, "skin") + "で束ねられている。",
                out translated)
            || TryTranslatePattern(PlantFiberPattern, stripped, spans, original, match =>
                TranslateCapture(match, spans, "plant") + "の" + TranslateCapture(match, spans, "planks") + "が" + TranslateCapture(match, spans, "style")
                + "様式に切り出され、" + TranslateCapture(match, spans, "tar") + "と" + TranslateCapture(match, spans, "bindingPlant") + "の"
                + TranslateCapture(match, spans, "fiberMaterial") + "の" + TranslateCapture(match, spans, "strips") + "で束ねられている。",
                out translated)
            || TryTranslatePattern(PlantCreaturePattern, stripped, spans, original, match =>
                TranslateCapture(match, spans, "plant") + "の" + TranslateCapture(match, spans, "planks") + "が" + TranslateCapture(match, spans, "style")
                + "様式に切り出され、" + TranslateCapture(match, spans, "tar") + "と" + TranslateCapture(match, spans, "creature") + "の"
                + TranslateCapture(match, spans, "skin") + "で束ねられている。",
                out translated)
            || TryTranslatePattern(BoneCreaturePattern, stripped, spans, original, match =>
                "ひび割れに詰まった" + TranslateCapture(match, spans, "tar") + "が、屠られたいくつかの"
                + TranslateCapture(match, spans, "creatures") + "の硬く" + TranslateCapture(match, spans, "style") + "な"
                + TranslateCapture(match, spans, "bones") + "をつなぎ留めている。",
                out translated)
            || TryTranslatePattern(BoneProperCreaturePattern, stripped, spans, original, match =>
                "ひび割れに詰まった" + TranslateCapture(match, spans, "tar") + "が、" + TranslateCapture(match, spans, "creature")
                + "の硬く" + TranslateCapture(match, spans, "style") + "な" + TranslateCapture(match, spans, "bones") + "をつなぎ留めている。",
                out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        Func<Match, string> build,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var built = build(match);
        if (ContainsLowercaseAsciiWord(built))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            built,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string TranslateCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var translated = CaptureTerms.TryGetValue(group.Value, out var term) ? term : group.Value;
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static bool ContainsLowercaseAsciiWord(string source)
    {
        var (visible, _) = ColorAwareTranslationComposer.Strip(source);
        return LowercaseAsciiWordPattern.IsMatch(visible);
    }
}
