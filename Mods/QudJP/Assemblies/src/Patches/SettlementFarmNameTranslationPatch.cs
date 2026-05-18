using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SettlementFarmNameTranslationPatch
{
    private const string Context = nameof(SettlementFarmNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.Names.SettlementNames");
        var historyType = AccessTools.TypeByName("HistoryKit.History");
        if (targetType is null || historyType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateFarmName", [historyType, typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateFarmName(History,string) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!SettlementFarmNameTranslator.TryTranslate(source, out var translated))
            {
                __result = translated;
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Context + ".GenerateFarmName", source, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
