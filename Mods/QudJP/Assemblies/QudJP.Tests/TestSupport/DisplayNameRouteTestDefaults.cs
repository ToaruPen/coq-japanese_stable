using System.Runtime.CompilerServices;
using QudJP.Patches;

namespace QudJP.Tests;

internal static class DisplayNameRouteTestDefaults
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        UseRegisteredDisplayNameRouteDefault();
    }

    internal static void UseRegisteredDisplayNameRouteDefault()
    {
        DisplayNameRouteTranslation.RegisterDefaultTranslatorForTests(
            GetDisplayNameRouteTranslator.TranslatePreservingColors);
    }

    internal static void UsePassThroughDefault()
    {
        DisplayNameRouteTranslation.ResetDefaultTranslatorForTests();
    }
}
