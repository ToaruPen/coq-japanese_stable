using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class HotkeyLabelFamilyTranslator
{
    private static readonly Regex BracketedHotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateBracketedLabel(
        string source,
        string route,
        string family,
        bool rejectNumericHotkeys,
        out string translated)
    {
        var match = BracketedHotkeyLabelPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var hotkey = match.Groups["hotkey"].Value;
        if (rejectNumericHotkeys && int.TryParse(hotkey, out _))
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

        translated = $"[{hotkey}] {translatedLabel}";
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }
}
