using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DimensionManagerExtraDimensionNameTranslationPatch
{
    private const string Context = nameof(DimensionManagerExtraDimensionNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Encounters.DimensionManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateMoreDimensions", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateMoreDimensions() target not found.", Context);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return DimensionManagerGeneratedNameTranslationPatch.Transpiler(instructions);
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            var extraDimensions = DimensionManagerGeneratedNameTranslationPatch.GetMemberValue(__instance, "ExtraDimensions") as IEnumerable;
            if (extraDimensions is null)
            {
                return;
            }

            foreach (var dimension in extraDimensions)
            {
                DimensionManagerGeneratedNameTranslationPatch.TranslateStringMember(
                    dimension,
                    "Name",
                    "ExtraDimensionName");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
