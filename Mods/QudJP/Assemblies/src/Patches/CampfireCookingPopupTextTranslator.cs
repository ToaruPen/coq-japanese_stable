using System;

namespace QudJP.Patches;

internal static class CampfireCookingPopupTextTranslator
{
    internal static bool TryTranslateAteMealPopup(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!string.Equals(stripped, "You eat the meal.", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "食事をとった。",
            spans,
            stripped.Length,
            source);
        return true;
    }

    internal static bool TryTranslateMealDescriptionPopup(string source, out string translated)
    {
        return CookingMealDescriptionTranslator.TryTranslate(source, out translated);
    }
}
