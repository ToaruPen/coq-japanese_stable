using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class SultanRegionRevealDescriptionTranslator
{
    private static readonly IReadOnlyDictionary<string, string> GovernmentTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["autarchy"] = "独裁国",
            ["republic"] = "共和国",
            ["city-state"] = "都市国家",
            ["monarchy"] = "王国",
            ["aristocracy"] = "貴族政",
            ["oligarchy"] = "寡頭政",
            ["democracy"] = "民主政",
            ["theocracy"] = "神権政",
            ["precinct"] = "管区",
            ["district"] = "地区",
            ["quarter"] = "街区",
            ["province"] = "州",
        };

    private static readonly IReadOnlyDictionary<string, string> LostTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lost"] = "失われた",
            ["vanished"] = "消え失せた",
            ["moldered"] = "朽ちた",
            ["desolate"] = "荒廃した",
            ["extinct"] = "滅びた",
        };

    private static readonly IReadOnlyDictionary<string, string> TerrainPhrases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["over the flats"] = "平原の上",
            ["under the chrome carcasses of giants"] = "巨人たちのクロムの残骸の下",
        };

    private static readonly IReadOnlyDictionary<string, string> ActivityPhrases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["the Eaters dwelled and dreamed"] = "イーターたちが住まい、夢見ていた",
            ["The Eaters admired their strange flora"] = "イーターたちが奇妙な植物群を愛でていた",
        };

    private static readonly string GovernmentPattern = BuildAlternation(GovernmentTerms.Keys);
    private static readonly string LostPattern = BuildAlternation(LostTerms.Keys);

    private static readonly Regex AncientFramePattern = new(
        "^(?<terrain1>.+?) and (?<terrain2>.+?), here stretches the ancient (?<government>" + GovernmentPattern + ") where (?<activity>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex LostFramePattern = new(
        "^(?<activity>.+?) in the (?<lost>" + LostPattern + ") (?<government>" + GovernmentPattern + ") whose ruins lie (?<terrain>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (!TryTranslateCore(stripped, out var translatedCore))
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

    private static bool TryTranslateCore(string source, out string translated)
    {
        var match = AncientFramePattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTerrain(match.Groups["terrain1"].Value)
                + "と"
                + TranslateTerrain(match.Groups["terrain2"].Value)
                + "、ここには古代の"
                + GovernmentTerms[match.Groups["government"].Value]
                + "が広がり、"
                + TranslateActivity(match.Groups["activity"].Value)
                + "。";
            return true;
        }

        match = LostFramePattern.Match(source);
        if (match.Success)
        {
            translated = LostTerms[match.Groups["lost"].Value]
                + GovernmentTerms[match.Groups["government"].Value]
                + "では"
                + TranslateActivity(match.Groups["activity"].Value)
                + "。その遺跡は"
                + TranslateTerrain(match.Groups["terrain"].Value)
                + "に横たわっている。";
            return true;
        }

        translated = source;
        return false;
    }

    private static string BuildAlternation(IEnumerable<string> values)
    {
        return string.Join("|", values.Select(Regex.Escape));
    }

    private static string TranslateTerrain(string source) =>
        TerrainPhrases.TryGetValue(source, out var translated) ? translated : source;

    private static string TranslateActivity(string source) =>
        ActivityPhrases.TryGetValue(source, out var translated) ? translated : source;
}
