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

    private static string PassThrough(string? source, string? context)
    {
        _ = context;
        return source ?? string.Empty;
    }
}
