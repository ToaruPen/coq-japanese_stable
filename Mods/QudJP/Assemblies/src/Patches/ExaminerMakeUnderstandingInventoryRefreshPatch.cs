using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class ExaminerMakeUnderstandingInventoryRefreshPatch
{
    private const string Context = nameof(ExaminerMakeUnderstandingInventoryRefreshPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var examinerType = AccessTools.TypeByName("XRL.World.Parts.Examiner");
        if (examinerType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        foreach (var methodName in new[] { "MakeUnderstood", "MakePartiallyUnderstood" })
        {
            var method = AccessTools.Method(examinerType, methodName, new[] { typeof(bool) });
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}(bool) target not found.", Context, methodName);
            }
        }
    }

    public static void Postfix(bool __result)
    {
        try
        {
            if (__result)
            {
                _ = InventoryScreenRefreshAfterIdentify.TryRefresh();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
