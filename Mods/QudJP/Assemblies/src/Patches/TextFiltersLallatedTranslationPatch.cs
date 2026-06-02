using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TextFiltersLallatedTranslationPatch
{
    internal const string Context = nameof(TextFiltersLallatedTranslationPatch);
    private const string Family = "TextFilters.Lallated";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return TextFilterSpeechStatusPatchHelpers.GetTextFiltersMethod(
            Context,
            "Lallated",
            [typeof(string), typeof(string)]);
    }

    public static void Postfix(string Text, ref string __result)
    {
        try
        {
            var source = __result;
            var translated = TextFilterSpeechStatusTranslator.TranslateLallated(source, Text);
            var recordTransform =
                !MessageFrameTranslator.TryStripDirectTranslationMarker(Text, out _)
                && !MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _);
            TextFilterSpeechStatusPatchHelpers.RecordIfChanged(
                Context,
                Family,
                source,
                translated,
                ref __result,
                recordTransform);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
