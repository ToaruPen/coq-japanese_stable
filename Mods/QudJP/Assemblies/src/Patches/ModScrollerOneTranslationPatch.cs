using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModScrollerOneTranslationPatch
{
    private const string Context = nameof(ModScrollerOneTranslationPatch);
    private const string TargetTypeName = "Qud.UI.ModScrollerOne";
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, "OnActivate");
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.OnActivate(...) not found on '{1}'.", Context, TargetTypeName);
            yield break;
        }

        yield return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            var translated = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string literal)
                {
                    instruction.operand = ModManagementSemanticPipeline.TranslateDisabledScriptsSuffix(literal);
                }

                translated.Add(instruction);
            }

            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    internal static string TranslateLiteralForTests(string source)
    {
        return ModManagementSemanticPipeline.TranslateDisabledScriptsSuffix(source);
    }
}
