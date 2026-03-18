#if HAS_TMP
using System;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TextMeshProUguiFontPatch
{
    private const string ReplacementObjectName = "QudJPReplacementText";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(typeof(TextMeshProUGUI), "OnEnable");
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve TextMeshProUGUI.OnEnable(). Patch will not apply.");
        }

        return method;
    }

    public static void Postfix(TextMeshProUGUI __instance)
    {
        try
        {
            if (IsReplacement(__instance))
            {
                return;
            }

            FontManager.ApplyToText(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TextMeshProUguiFontPatch.Postfix failed: {0}", ex);
        }
    }

    private static bool IsReplacement(TextMeshProUGUI text)
    {
        return string.Equals(text.gameObject.name, ReplacementObjectName, StringComparison.Ordinal);
    }
}
#endif
