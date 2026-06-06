using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class ExaminerResultUnderstandingInventoryRefreshPatch
{
    private const string Context = nameof(ExaminerResultUnderstandingInventoryRefreshPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var examinerType = AccessTools.TypeByName("XRL.World.Parts.Examiner");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (examinerType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Examiner or GameObject.", Context);
            yield break;
        }

        var resultSuccess = AccessTools.Method(examinerType, "ResultSuccess", [gameObjectType]);
        if (resultSuccess is not null)
        {
            yield return resultSuccess;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ResultSuccess(GameObject) target not found.", Context);
        }

        var resultPartialSuccess = AccessTools.Method(examinerType, "ResultPartialSuccess", [gameObjectType, typeof(int)]);
        if (resultPartialSuccess is not null)
        {
            yield return resultPartialSuccess;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ResultPartialSuccess(GameObject, int) target not found.", Context);
        }
    }

    public static void Postfix()
    {
        try
        {
            _ = InventoryScreenRefreshAfterIdentify.TryRefresh();
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
