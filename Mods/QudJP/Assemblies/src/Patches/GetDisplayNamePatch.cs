using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GetDisplayNamePatch
{
    private const string TargetTypeName = "XRL.World.GetDisplayNameEvent";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("XRL.World.GetDisplayNameEvent:GetFor");
        if (method is not null)
        {
            return method;
        }

        foreach (var type in AccessTools.AllTypes())
        {
            if (type is null)
            {
                continue;
            }

            var fullName = type.FullName;
            if (string.IsNullOrEmpty(fullName))
            {
                continue;
            }

            if (!string.Equals(fullName, TargetTypeName, StringComparison.Ordinal)
                && !string.Equals(type.Name, "GetDisplayNameEvent", StringComparison.Ordinal))
            {
                continue;
            }

            var methods = AccessTools.GetDeclaredMethods(type);
            for (var index = 0; index < methods.Count; index++)
            {
                var candidate = methods[index];
                if (!string.Equals(candidate.Name, "GetFor", StringComparison.Ordinal)
                    || candidate.ReturnType != typeof(string))
                {
                    continue;
                }

                var parameters = candidate.GetParameters();
                if (parameters.Length >= 2 && parameters[1].ParameterType == typeof(string))
                {
                    return candidate;
                }
            }
        }

        Trace.TraceError("QudJP: Failed to resolve GetDisplayNameEvent.GetFor(...). Patch will not apply.");
        return null;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result = GetDisplayNameRouteTranslator.TranslatePreservingColors(__result, nameof(GetDisplayNamePatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GetDisplayNamePatch.Postfix failed: {0}", ex);
        }
    }
}
