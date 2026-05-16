using System;

namespace QudJP;

internal static class CirculatoryLossTermTranslator
{
    internal static bool TryTranslateTermPhrase(string source, out string translated)
    {
        var normalized = source.Trim();
        if (TryTranslateTerm(normalized, out translated))
        {
            return true;
        }

        if (normalized.StartsWith("is ", StringComparison.Ordinal))
        {
            return TryTranslateTerm(normalized.Substring(3), out translated);
        }

        if (normalized.StartsWith("are ", StringComparison.Ordinal))
        {
            return TryTranslateTerm(normalized.Substring(4), out translated);
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryTranslateTerm(string source, out string translated)
    {
        translated = source switch
        {
            "bleeding" => "出血",
            "leaking" => "液漏れ",
            "oozing" => "滲出",
            "fluxing" => "フラックス漏れ",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }
}
