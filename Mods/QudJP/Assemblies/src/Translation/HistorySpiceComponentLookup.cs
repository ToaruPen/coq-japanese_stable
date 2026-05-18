namespace QudJP;

internal static class HistorySpiceComponentLookup
{
    private const string DictionaryFile = "Scoped/historyspice-common.ja.json";
    private const string EraSuffix = " Era";

    internal static string? TranslateExactOrLowerAscii(string source) =>
        ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DictionaryFile);

    internal static bool TryTranslateWord(string source, out string translated)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var lower = LowerAscii(source);
        var scoped = TranslateExactOrLowerAscii(lower);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateTitlePhrase(string source, out string translated)
    {
        var words = source.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            translated = source;
            return false;
        }

        var translatedWords = new string[words.Length];
        for (var index = 0; index < words.Length; index++)
        {
            if (!TryTranslateWord(words[index], out translatedWords[index]))
            {
                translated = source;
                return false;
            }
        }

        translated = string.Concat(translatedWords);
        return true;
    }

    internal static bool TryTranslateEraName(string source, out string translated)
    {
        if (!source.EndsWith(EraSuffix, System.StringComparison.Ordinal)
            || !TryTranslateTitlePhrase(source.Substring(0, source.Length - EraSuffix.Length), out var eraName))
        {
            translated = source;
            return false;
        }

        translated = eraName + "の時代";
        return true;
    }

    private static string LowerAscii(string source)
    {
        var buffer = source.ToCharArray();
        var changed = false;
        for (var index = 0; index < buffer.Length; index++)
        {
            var character = buffer[index];
            if (character < 'A' || character > 'Z')
            {
                continue;
            }

            buffer[index] = (char)(character + ('a' - 'A'));
            changed = true;
        }

        return changed ? new string(buffer) : source;
    }

}
