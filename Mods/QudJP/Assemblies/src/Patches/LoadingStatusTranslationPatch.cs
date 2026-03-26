using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LoadingStatusTranslationPatch
{
    private const string Context = nameof(LoadingStatusTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("XRL.UI.Loading");
        if (type is null)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch target type 'XRL.UI.Loading' not found.");
            return null;
        }

        var method = AccessTools.Method(type, "SetLoadingStatus", new[] { typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch method 'SetLoadingStatus' not found on 'XRL.UI.Loading'.");
        }

        return method;
    }

    public static void Prefix(ref string description)
    {
        try
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(description, out var markedText))
            {
                description = markedText;
                return;
            }

            var translated = ColorAwareTranslationComposer.TranslatePreservingColors(description);
            if (!string.Equals(translated, description, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "LoadingStatus.Description", description, translated);
                description = MessageFrameTranslator.MarkDirectTranslation(translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
