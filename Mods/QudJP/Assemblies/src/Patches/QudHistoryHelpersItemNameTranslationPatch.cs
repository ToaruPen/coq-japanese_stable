using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudHistoryHelpersItemNameTranslationPatch
{
    internal const string Context = nameof(QudHistoryHelpersItemNameTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.Annals.QudHistoryHelpers");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var historyType = AccessTools.TypeByName("HistoryKit.History");
        var entityType = AccessTools.TypeByName("HistoryKit.HistoricEntity");
        if (historyType is null || entityType is null)
        {
            Trace.TraceError("QudJP: {0} History or HistoricEntity target type not found.", Context);
            yield break;
        }

        var parameters = new[] { typeof(string), historyType, entityType };
        foreach (var methodName in new[] { "NameItem", "NameItemNounRoot", "NameItemAdjRoot" })
        {
            var method = AccessTools.Method(targetType, methodName, parameters);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.{1}(...) target not found.", Context, methodName);
                continue;
            }

            yield return method;
        }
    }

    public static void Postfix(MethodBase __originalMethod, ref string __result)
    {
        try
        {
            var source = __result;
            if (!HistoricSpiceGeneratedNameTranslator.TryTranslateHistoricItemName(source, out var translated))
            {
                return;
            }

            __result = translated;
            DynamicTextObservability.RecordTransform(Context, FamilyFor(__originalMethod.Name), source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string FamilyFor(string methodName) => Context + "." + methodName;
}
