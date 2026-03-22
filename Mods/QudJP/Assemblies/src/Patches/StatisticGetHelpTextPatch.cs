using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatisticGetHelpTextPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.World.Statistic", "Statistic");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: StatisticGetHelpTextPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GetHelpText", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: StatisticGetHelpTextPatch.GetHelpText not found.");
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (!Translator.TryGetTranslation(__result, out var translated)
                || string.Equals(translated, __result, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(
                nameof(StatisticGetHelpTextPatch),
                "AttributeHelp.Exact",
                __result,
                translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: StatisticGetHelpTextPatch.Postfix failed: {0}", ex);
        }
    }
}
