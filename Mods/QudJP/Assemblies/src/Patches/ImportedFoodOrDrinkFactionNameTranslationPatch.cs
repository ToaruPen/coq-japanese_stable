using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ImportedFoodOrDrinkFactionNameTranslationPatch
{
    internal const string Context = nameof(ImportedFoodOrDrinkFactionNameTranslationPatch);
    internal const string Family = Context + ".generateFactionName";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.Annals.ImportedFoodorDrink");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "generateFactionName", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.generateFactionName(string) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!ImportedFoodOrDrinkFactionNameTranslator.TryTranslate(source, out var translated))
            {
                return;
            }

            __result = translated;
            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
