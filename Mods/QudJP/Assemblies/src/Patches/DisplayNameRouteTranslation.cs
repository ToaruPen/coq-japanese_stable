using System;

namespace QudJP;

internal static class DisplayNameRouteTranslation
{
    internal static string TranslatePreservingColors(string? source, string? context = null)
    {
        return Patches.GetDisplayNameRouteTranslator.TranslatePreservingColors(source, context);
    }

    internal static string TranslateCapturePreservingColors(string source, string context)
    {
        var withoutArticle = StripLeadingEnglishArticlePreservingColors(source);
        var withoutDirectMarkers = MessageFrameTranslator.StripAllDirectTranslationMarkers(withoutArticle);
        return TranslatePreservingColors(withoutDirectMarkers, context);
    }

    internal static string StripLeadingEnglishArticlePreservingColors(string source)
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
