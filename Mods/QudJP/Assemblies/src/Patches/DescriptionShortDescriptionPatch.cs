using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DescriptionShortDescriptionPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.World.Parts.Description", "Description");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: DescriptionShortDescriptionPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GetShortDescription", new[] { typeof(bool), typeof(bool), typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: DescriptionShortDescriptionPatch.GetShortDescription not found.");
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

            var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
                __result,
                nameof(DescriptionShortDescriptionPatch));
            if (string.Equals(translated, __result, StringComparison.Ordinal))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: DescriptionShortDescriptionPatch.Postfix failed: {0}", ex);
        }
    }
}
