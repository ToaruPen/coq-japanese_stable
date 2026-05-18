#if HAS_TMP
using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LegacyTextMeshFontPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.PropertySetter(typeof(TextMesh), nameof(TextMesh.text));
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve UnityEngine.TextMesh.text setter. Patch will not apply.");
        }

        return method;
    }

    public static void Postfix(TextMesh __instance)
    {
        try
        {
            FontManager.ApplyToTextMesh(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LegacyTextMeshFontPatch.Postfix failed: {0}", ex);
        }
    }
}
#endif
