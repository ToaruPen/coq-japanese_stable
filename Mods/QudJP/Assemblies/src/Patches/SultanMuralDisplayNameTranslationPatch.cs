using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SultanMuralDisplayNameTranslationPatch
{
    private const string Context = nameof(SultanMuralDisplayNameTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 2);
        var targetType = AccessTools.TypeByName("XRL.World.Parts.SultanMuralController");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var eventType = AccessTools.TypeByName("HistoryKit.HistoricEvent");
        if (targetType is null || cellType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        var listType = typeof(List<>).MakeGenericType(cellType);
        AddTarget(targets, targetType, "updateHistoricMural", listType, eventType);
        AddTarget(targets, targetType, "ruinMural", listType, eventType);
        return targets;
    }

    public static void Postfix(object? __0)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateMuralCells(__0);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, Type targetType, string methodName, params Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0} target not found: {1}.", Context, methodName);
    }
}
