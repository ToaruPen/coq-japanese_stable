using System;

namespace QudJP.Patches;

internal static class DisplayNameCaptureTranslator
{
    public static string TranslatePreservingColors(string source, string context)
    {
        var withoutArticle = StripLeadingEnglishArticlePreservingColors(source);
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(withoutArticle, context);
    }

    public static string StripLeadingEnglishArticlePreservingColors(string source)
    {
        var trimmed = source.Trim();
        var direct = StringHelpers.StripLeadingEnglishArticle(
            trimmed,
            includeCapitalizedDefiniteArticle: true);
        if (!string.Equals(direct, trimmed, StringComparison.Ordinal))
        {
            return direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(trimmed);
        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(
            visible,
            includeCapitalizedDefiniteArticle: true);
        return string.Equals(withoutArticle, visible, StringComparison.Ordinal)
            ? trimmed
            : ColorAwareTranslationComposer.TranslatePreservingColors(trimmed, _ => withoutArticle);
    }
}
