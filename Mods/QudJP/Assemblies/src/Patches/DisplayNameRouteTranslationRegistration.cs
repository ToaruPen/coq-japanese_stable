namespace QudJP.Patches;

internal static class DisplayNameRouteTranslationRegistration
{
    internal static void Register()
    {
        DisplayNameRouteTranslation.RegisterTranslator(GetDisplayNameRouteTranslator.TranslatePreservingColors);
    }
}
