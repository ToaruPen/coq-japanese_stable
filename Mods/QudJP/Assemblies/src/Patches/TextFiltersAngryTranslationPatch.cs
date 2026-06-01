using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TextFiltersAngryTranslationPatch
{
    internal const string Context = nameof(TextFiltersAngryTranslationPatch);
    private const string Family = "TextFilters.Angry";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return TextFilterSpeechStatusPatchHelpers.GetTextFiltersMethod(Context, "Angry", [typeof(string)]);
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            var translated = TextFilterSpeechStatusTranslator.TranslateAngry(source);
            TextFilterSpeechStatusPatchHelpers.RecordIfChanged(Context, Family, source, translated, ref __result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
