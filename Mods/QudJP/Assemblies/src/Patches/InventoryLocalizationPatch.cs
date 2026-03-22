using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryLocalizationPatch
{
    private static readonly (string typeName, string methodName)[] KnownTargets =
    {
        ("Qud.UI.InventoryLineData", "get_displayName"),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var seen = new HashSet<MethodBase>();

        for (var index = 0; index < KnownTargets.Length; index++)
        {
            var resolvedType = AccessTools.TypeByName(KnownTargets[index].typeName);
            if (resolvedType is null)
            {
                continue;
            }

            var method = AccessTools.Method(resolvedType, KnownTargets[index].methodName);
            if (method is not null && seen.Add(method))
            {
                targets.Add(method);
            }
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: Failed to resolve inventory text methods. Patch will not apply.");
        }

        return targets;
    }

    public static void Postfix(MethodBase __originalMethod, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            var methodContext = __originalMethod is null
                ? nameof(InventoryLocalizationPatch)
                : ObservabilityHelpers.ComposeContext(
                    nameof(InventoryLocalizationPatch),
                    $"method={__originalMethod.DeclaringType?.Name ?? "<unknown>"}.{__originalMethod.Name}");
            __result = GetDisplayNameRouteTranslator.TranslatePreservingColors(__result, methodContext);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryLocalizationPatch.Postfix failed: {0}", ex);
        }
    }

}
