using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageDynamicQuestRewardDisplayNameTranslationPatch
{
    private const string Context = nameof(VillageDynamicQuestRewardDisplayNameTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    internal static bool IsActive => activeDepth > 0;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.VillageDynamicQuestContext");
        var method = targetType is null ? null : AccessTools.Method(targetType, "getQuestReward", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }
}
