using System;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class WorldModsTextTranslator
{
    private const string WorldModsDictionaryFile = "world-mods.ja.json";

    private static readonly Regex MasterworkPattern = new Regex(
        "^Masterwork: This weapon scores critical hits (?<value>.+) of the time instead of 5%\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(string source, string route, string family, out string translated)
    {
        if (TryTranslateScopedExact(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateMasterworkTemplate(source, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateScopedExact(string source, string route, string family, out string translated)
    {
        var direct = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldModsDictionaryFile);
        if (!string.IsNullOrEmpty(direct) && !string.Equals(direct, source, StringComparison.Ordinal))
        {
            translated = direct!;
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (string.Equals(stripped, source, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var strippedTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAscii(stripped, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(strippedTranslation) || string.Equals(strippedTranslation, stripped, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.Restore(strippedTranslation, spans);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateMasterworkTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = MasterworkPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("Masterwork: This weapon scores critical hits {0} of the time instead of 5%.");
        if (string.Equals(template, "Masterwork: This weapon scores critical hits {0} of the time instead of 5%.", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var visible = template.Replace("{0}", match.Groups["value"].Value);
        translated = ColorAwareTranslationComposer.Restore(visible, spans);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }
}
