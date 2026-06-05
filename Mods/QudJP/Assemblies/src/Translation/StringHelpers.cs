using System;
using System.Diagnostics;

namespace QudJP;

internal static class StringHelpers
{
    internal static string StripLeadingDefiniteArticle(string source, StringComparison comparison = StringComparison.Ordinal)
    {
        return source.StartsWith("the ", comparison)
            ? source.Substring(4)
            : source;
    }

    internal static string StripLeadingEnglishArticle(
        string source,
        bool includeCapitalizedDefiniteArticle = false,
        bool includeCapitalizedIndefiniteArticle = false,
        bool includeQuantifierArticle = false)
    {
        if (source.StartsWith("a ", StringComparison.Ordinal))
        {
            return source.Substring(2);
        }

        if (source.StartsWith("an ", StringComparison.Ordinal))
        {
            return source.Substring(3);
        }

        if (source.StartsWith("the ", StringComparison.Ordinal))
        {
            return source.Substring(4);
        }

        if (includeQuantifierArticle && source.StartsWith("some ", StringComparison.Ordinal))
        {
            return source.Substring(5);
        }

        if (includeCapitalizedDefiniteArticle && source.StartsWith("The ", StringComparison.Ordinal))
        {
            return source.Substring(4);
        }

        if (includeCapitalizedIndefiniteArticle && source.StartsWith("A ", StringComparison.Ordinal))
        {
            return source.Substring(2);
        }

        if (includeCapitalizedIndefiniteArticle && source.StartsWith("An ", StringComparison.Ordinal))
        {
            return source.Substring(3);
        }

        if (includeQuantifierArticle && includeCapitalizedIndefiniteArticle && source.StartsWith("Some ", StringComparison.Ordinal))
        {
            return source.Substring(5);
        }

        return source;
    }

    internal static string LowerAscii(string source)
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

    internal static string? TranslateExactOrLowerAscii(string source)
    {
        if (Translator.TryGetTranslation(source, out var direct)
            && !string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var lowered = LowerAscii(source);
        if (!string.Equals(lowered, source, StringComparison.Ordinal)
            && Translator.TryGetTranslation(lowered, out var loweredTranslation)
            && !string.Equals(loweredTranslation, lowered, StringComparison.Ordinal))
        {
            return loweredTranslation;
        }

        var translated = Translator.Translate(source);
        return string.Equals(translated, source, StringComparison.Ordinal)
            ? null
            : translated;
    }

    internal static string? TranslateExactOrLowerAscii(string source, string? context)
    {
        using var _ = Translator.PushLogContext(context);
        return TranslateExactOrLowerAscii(source);
    }

    internal static bool TryGetTranslationExactOrLowerAscii(string source, out string translated)
    {
        if (Translator.TryGetTranslation(source, out translated)
            && !string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        var lowered = LowerAscii(source);
        if (!string.Equals(lowered, source, StringComparison.Ordinal)
            && Translator.TryGetTranslation(lowered, out translated)
            && !string.Equals(translated, lowered, StringComparison.Ordinal))
        {
            return true;
        }

        translated = source;
        return false;
    }

    internal static string TranslateExactOrLowerAsciiFallback(string source)
    {
        var result = TranslateExactOrLowerAscii(source);
        if (result is not null)
        {
            return result;
        }

        Trace.TraceWarning("QudJP: TranslateExactOrLowerAscii miss for '{0}', falling back to source.", source);
        return source;
    }

    internal static bool ContainsOrdinal(string source, string value)
    {
#if NET48
        return source.IndexOf(value, StringComparison.Ordinal) >= 0;
#else
        return source.Contains(value, StringComparison.Ordinal);
#endif
    }

    internal static bool ContainsOrdinalIgnoreCase(string source, string value)
    {
#if NET48
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
#else
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
#endif
    }

}
