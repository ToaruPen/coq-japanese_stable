using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class InventoryAndEquipmentStatusScreenTextTranslator
{
    private static readonly Regex HotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
}
