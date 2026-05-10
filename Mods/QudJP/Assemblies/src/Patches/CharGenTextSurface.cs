using System.Collections;

namespace QudJP.Patches;

internal static class CharGenTextSurface
{
    internal static IEnumerable? MaterializeTranslatedBreadcrumbs(IEnumerable? values, string context)
    {
        return values is null
            ? values
            : CharGenProducerTranslationHelpers.MaterializeTranslatedEnumerable(values, "Title", context);
    }

    internal static IEnumerable MaterializeTranslatedFrameworkData(IEnumerable values, string context)
    {
        return CharGenProducerTranslationHelpers.MaterializeTranslatedFrameworkDataEnumerable(values, context);
    }

    internal static void TranslateWindowTitle(object target, string memberName, string context)
    {
        CharGenProducerTranslationHelpers.TranslateStringMember(target, memberName, context);
    }
}
