using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EffectDescriptionPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.World.Effect", "Effect");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: EffectDescriptionPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GetDescription", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: EffectDescriptionPatch.GetDescription not found.");
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

            if (!ActiveEffectTextTranslator.TryTranslateText(
                    __result,
                    nameof(EffectDescriptionPatch),
                    "ActiveEffects.Description",
                    out var translated))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EffectDescriptionPatch.Postfix failed: {0}", ex);
        }
    }
}
