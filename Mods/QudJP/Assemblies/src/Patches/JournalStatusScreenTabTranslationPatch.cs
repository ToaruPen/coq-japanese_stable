using System;
using System.Diagnostics;

namespace QudJP.Patches;

public static class JournalStatusScreenTabTranslationPatch
{
    private const string Context = nameof(JournalStatusScreenTabTranslationPatch);

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            var translated = StringHelpers.TranslateExactOrLowerAscii(__result);
            if (translated is null || string.Equals(translated, __result, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(
                ObservabilityHelpers.ComposeContext(Context, "return"),
                "JournalStatusScreen.TabString",
                __result,
                translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalStatusScreenTabTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
