using System;
using System.Diagnostics;

namespace QudJP.Patches;

public static class MessageLogStatusScreenTranslationPatch
{
    private const string Context = nameof(MessageLogStatusScreenTranslationPatch);

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
                "MessageLogStatusScreen.TabString",
                __result,
                translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageLogStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
