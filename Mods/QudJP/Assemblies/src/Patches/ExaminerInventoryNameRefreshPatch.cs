using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ExaminerInventoryNameRefreshPatch
{
    private const string Context = nameof(ExaminerInventoryNameRefreshPatch);

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

        foreach (var methodName in new[]
                 {
                     "ResultSuccess",
                     "ResultExceptionalSuccess",
                 })
        {
            var method = AccessTools.Method(examinerType, methodName, [gameObjectType]);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}(GameObject) not found.", Context, methodName);
            }
        }

        var partialSuccess = AccessTools.Method(examinerType, "ResultPartialSuccess", [gameObjectType, typeof(int)]);
        if (partialSuccess is not null)
        {
            yield return partialSuccess;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ResultPartialSuccess(GameObject, int) not found.", Context);
        }

        foreach (var methodName in new[]
                 {
                     "MakeUnderstood",
                     "MakePartiallyUnderstood",
                 })
        {
            var boolMethod = AccessTools.Method(examinerType, methodName, [typeof(bool)]);
            if (boolMethod is not null)
            {
                yield return boolMethod;
            }
            else
            {
                Trace.TraceWarning("QudJP: {0}.{1}(bool) not found.", Context, methodName);
            }

            var outStringMethod = AccessTools.Method(examinerType, methodName, [typeof(string).MakeByRefType()]);
            if (outStringMethod is not null)
            {
                yield return outStringMethod;
            }
            else
            {
                Trace.TraceWarning("QudJP: {0}.{1}(string&) not found.", Context, methodName);
            }
        }
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            var parentObject = ReflectionUtils.GetPropertyOrFieldValue(__instance, "ParentObject");
            InventoryNameRefreshCoordinator.MarkInventoryNameStateChanged(parentObject);
            _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItem(parentObject);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
