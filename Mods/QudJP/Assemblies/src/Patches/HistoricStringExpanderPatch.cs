using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HistoricStringExpanderPatch
{
    private const string TargetTypeName = "HistoryKit.HistoricStringExpander";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("HistoryKit.HistoricStringExpander:ExpandString");
        if (method is not null)
        {
            return method;
        }

        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is not null)
        {
            var methods = AccessTools.GetDeclaredMethods(targetType);
            for (var index = 0; index < methods.Count; index++)
            {
                var candidate = methods[index];
                if (candidate.Name != "ExpandString")
                {
                    continue;
                }

                var parameters = candidate.GetParameters();
                if (parameters.Length == 5 && parameters[0].ParameterType == typeof(string))
                {
                    return candidate;
                }
            }
        }

        Trace.TraceError("QudJP: Failed to resolve HistoricStringExpander.ExpandString(...). Patch will not apply.");
        return null;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            __result = UITextSkinTranslationPatch.TranslatePreservingColors(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HistoricStringExpanderPatch.Postfix failed: {0}", ex);
        }
    }
}
