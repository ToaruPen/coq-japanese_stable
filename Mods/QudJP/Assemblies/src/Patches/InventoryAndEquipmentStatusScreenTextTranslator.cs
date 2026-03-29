using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class InventoryAndEquipmentStatusScreenTextTranslator
{
    private static readonly Regex HotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CategoryWeightWithCountPattern =
        new Regex("^\\|(?<items>\\d+) items\\|(?<weight>.+) lbs\\.\\|$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CategoryWeightOnlyPattern =
        new Regex("^\\|(?<weight>.+) lbs\\.\\|$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ItemWeightPattern =
        new Regex("^\\[(?<weight>.+) lbs\\.\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PricePattern =
        new Regex("^\\{\\{B\\|\\$(?<value>.+)\\}\\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CarriedWeightPattern =
        new Regex("^\\{\\{C\\|(?<carried>.+)\\{\\{K\\|/(?<capacity>.+)\\}\\} lbs\\. \\}\\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryStripEmbeddedHotkeyLabel(string source, out string stripped)
    {
        var match = HotkeyLabelPattern.Match(source);
        if (!match.Success)
        {
            stripped = source;
            return false;
        }

        stripped = match.Groups["label"].Value;
        return true;
    }

    internal static bool TryTranslateUiText(string source, string route, out string translated)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, "InventoryAndEquipment.ExactLookup", source, translated);
            return true;
        }

        var match = HotkeyLabelPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedLabel = StringHelpers.TranslateExactOrLowerAscii(match.Groups["label"].Value);
        if (translatedLabel is null)
        {
            translated = source;
            return false;
        }

        translated = $"[{match.Groups["hotkey"].Value}] {translatedLabel}";
        DynamicTextObservability.RecordTransform(route, "InventoryAndEquipment.HotkeyLabel", source, translated);
        return true;
    }

    internal static bool TryTranslateCategoryWeightText(string source, string route, out string translated)
    {
        var countedMatch = CategoryWeightWithCountPattern.Match(source);
        if (countedMatch.Success)
        {
            var template = Translator.Translate("|{items} items|{weight} lbs.|");
            if (!string.Equals(template, "|{items} items|{weight} lbs.|", StringComparison.Ordinal))
            {
                translated = template
                    .Replace("{items}", countedMatch.Groups["items"].Value)
                    .Replace("{weight}", countedMatch.Groups["weight"].Value);
                DynamicTextObservability.RecordTransform(route, "|{items} items|{weight} lbs.|", source, translated);
                return true;
            }
        }

        var weightOnlyMatch = CategoryWeightOnlyPattern.Match(source);
        if (!weightOnlyMatch.Success)
        {
            translated = source;
            return false;
        }

        var weightOnlyTemplate = Translator.Translate("|{weight} lbs.|");
        if (string.Equals(weightOnlyTemplate, "|{weight} lbs.|", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = weightOnlyTemplate.Replace("{weight}", weightOnlyMatch.Groups["weight"].Value);
        DynamicTextObservability.RecordTransform(route, "|{weight} lbs.|", source, translated);
        return true;
    }

    internal static bool TryTranslateItemWeightText(string source, string route, out string translated)
    {
        var match = ItemWeightPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("[{weight} lbs.]");
        if (string.Equals(template, "[{weight} lbs.]", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = template.Replace("{weight}", match.Groups["weight"].Value);
        DynamicTextObservability.RecordTransform(route, "[{weight} lbs.]", source, translated);
        return true;
    }

    internal static bool TryTranslatePriceText(string source, string route, out string translated)
    {
        var match = PricePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("{{B|${value}}}");
        if (string.Equals(template, "{{B|${value}}}", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = template.Replace("{value}", match.Groups["value"].Value);
        DynamicTextObservability.RecordTransform(route, "{{B|${value}}}", source, translated);
        return true;
    }

    internal static bool TryTranslateCarriedWeightText(string source, string route, out string translated)
    {
        var match = CarriedWeightPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("{{C|{carried}{{K|/{capacity}} lbs. }}");
        if (string.Equals(template, "{{C|{carried}{{K|/{capacity}} lbs. }}", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = template
            .Replace("{carried}", match.Groups["carried"].Value)
            .Replace("{capacity}", match.Groups["capacity"].Value);
        DynamicTextObservability.RecordTransform(route, "{{C|{carried}{{K|/{capacity}} lbs. }}", source, translated);
        return true;
    }
}
