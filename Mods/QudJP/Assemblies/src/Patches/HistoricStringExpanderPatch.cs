using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HistoricStringExpanderPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Trace.TraceWarning("QudJP: HistoricStringExpanderPatch is temporarily disabled to avoid corrupting HistorySpice/world generation output.");
        yield break;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (MessageFrameTranslator.TryStripDirectTranslationMarker(__result, out var markedText))
            {
                source = markedText;
            }

            __result = UITextSkinTranslationPatch.TranslatePreservingColors(
                source,
                nameof(HistoricStringExpanderPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HistoricStringExpanderPatch.Postfix failed: {0}", ex);
        }
    }
}
