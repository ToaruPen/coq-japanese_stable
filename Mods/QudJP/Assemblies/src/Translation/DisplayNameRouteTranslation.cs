using System;

namespace QudJP;

internal static class DisplayNameRouteTranslation
{
    private static readonly object SyncRoot = new object();
    private static DisplayNameTranslator translator = PassThrough;

    internal delegate string DisplayNameTranslator(string? source, string? context);

    internal static void RegisterTranslator(DisplayNameTranslator routeTranslator)
    {
        if (routeTranslator is null)
        {
            throw new ArgumentNullException(nameof(routeTranslator));
        }

        lock (SyncRoot)
        {
            translator = routeTranslator;
        }
    }

    internal static void RegisterTranslatorForTests(DisplayNameTranslator routeTranslator)
    {
        RegisterTranslator(routeTranslator);
    }

    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            translator = PassThrough;
        }
    }

    internal static string TranslatePreservingColors(string? source, string? context = null)
    {
        DisplayNameTranslator currentTranslator;
        lock (SyncRoot)
        {
            currentTranslator = translator;
        }

        return currentTranslator(source, context);
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

    private static string PassThrough(string? source, string? context)
    {
        _ = context;
        return source ?? string.Empty;
    }
}
