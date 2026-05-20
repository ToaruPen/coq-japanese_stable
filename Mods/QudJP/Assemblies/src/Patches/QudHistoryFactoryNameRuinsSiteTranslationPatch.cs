using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudHistoryFactoryNameRuinsSiteTranslationPatch
{
    internal const string Context = nameof(QudHistoryFactoryNameRuinsSiteTranslationPatch);
    internal const string Family = Context + ".NameRuinsSite";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.Annals.QudHistoryFactory");
        var historyType = AccessTools.TypeByName("HistoryKit.History");
        if (targetType is null || historyType is null)
        {
            Trace.TraceError("QudJP: {0} target type or History type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "NameRuinsSite",
            new[] { historyType, typeof(bool).MakeByRefType(), typeof(string).MakeByRefType() });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.NameRuinsSite(...) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!HistoricSpiceGeneratedNameTranslator.TryTranslateRuinsSiteName(source, out var translated))
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
