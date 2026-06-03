using System;

namespace QudJP;

internal static class DisplayNameRouteTranslation
{
    private static readonly object SyncRoot = new object();
    private static DisplayNameTranslator defaultTranslator = PassThrough;
    private static DisplayNameTranslator translator = defaultTranslator;

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

    internal static void RegisterDefaultTranslatorForTests(DisplayNameTranslator routeTranslator)
    {
        if (routeTranslator is null)
        {
            throw new ArgumentNullException(nameof(routeTranslator));
        }

        lock (SyncRoot)
        {
            defaultTranslator = routeTranslator;
            translator = defaultTranslator;
        }
    }

    internal static void ResetDefaultTranslatorForTests()
    {
        lock (SyncRoot)
        {
            defaultTranslator = PassThrough;
            translator = defaultTranslator;
        }
    }

    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            translator = defaultTranslator;
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
        return TranslatePreservingColors(
            NormalizeDisplayNameCaptureForRoute(source),
            context);
    }

    internal static string StripLeadingEnglishArticlePreservingColors(string source)
    {
        var trimmed = source.Trim();
        if (TryStripLeadingEnglishArticle(trimmed, out var direct))
        {
            return direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(trimmed);
        if (!TryStripLeadingEnglishArticle(visible, out var withoutArticle))
        {
            return trimmed;
        }

        return RestoreArticleStrippedVisibleText(trimmed, withoutArticle);
    }

    private static string NormalizeDisplayNameCaptureForRoute(string source)
    {
        var withoutDirectMarkers = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        return StripLeadingEnglishArticlePreservingColors(withoutDirectMarkers);
    }

    private static bool TryStripLeadingEnglishArticle(string source, out string stripped)
    {
        stripped = StringHelpers.StripLeadingEnglishArticle(
            source,
            includeCapitalizedDefiniteArticle: true);
        return !string.Equals(stripped, source, StringComparison.Ordinal);
    }

    private static string RestoreArticleStrippedVisibleText(string source, string visibleWithoutArticle)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var boundaryRestored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            visibleWithoutArticle,
            spans,
            stripped.Length);
        return string.Equals(boundaryRestored, visibleWithoutArticle, StringComparison.Ordinal)
            ? ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => visibleWithoutArticle)
            : boundaryRestored;
    }

    private static string PassThrough(string? source, string? context)
    {
        _ = context;
        return source ?? string.Empty;
    }
}
