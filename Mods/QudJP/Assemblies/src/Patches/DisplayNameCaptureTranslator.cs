namespace QudJP.Patches;

internal static class DisplayNameCaptureTranslator
{
    public static string TranslatePreservingColors(string source, string context)
    {
        return DisplayNameRouteTranslation.TranslateCapturePreservingColors(source, context);
    }

    public static bool TryTranslatePlaceholderValue(string source, string context, out string translated)
    {
        return DisplayNamePlaceholderTranslator.TryTranslatePlaceholderValue(
            source,
            candidate => TranslatePreservingColors(candidate, context),
            out translated);
    }

    public static string StripLeadingEnglishArticlePreservingColors(string source)
    {
        return DisplayNameRouteTranslation.StripLeadingEnglishArticlePreservingColors(source);
    }
}
