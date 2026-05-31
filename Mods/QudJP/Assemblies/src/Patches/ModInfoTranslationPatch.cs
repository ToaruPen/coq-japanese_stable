using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModInfoTranslationPatch
{
    private const string Context = nameof(ModInfoTranslationPatch);
    private const string TargetTypeName = "XRL.ModInfo";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            yield break;
        }

        foreach (var methodName in new[]
	                 {
	                     "ConfirmDependencies",
	                     "ConfirmUpdate",
	                     "ConfirmFailure",
	                     "DownloadUpdate",
	                     "AppendDependencyConfirmation",
	                 })
        {
            var method = AccessTools.Method(targetType, methodName);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.{1}(...) not found on '{2}'.", Context, methodName, TargetTypeName);
                continue;
            }

            yield return method;
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(MethodBase? __originalMethod, IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            var translated = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string literal
                    && __originalMethod is not null)
                {
                    instruction.operand = ModManagementSemanticPipeline.TranslateLiteral(__originalMethod.Name, literal);
                }

                translated.Add(instruction);
            }

            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed for {1}: {2}", Context, __originalMethod?.Name ?? "(unknown)", ex);
            return instructions;
        }
    }

    internal static string TranslateLiteralForTests(string methodName, string source) =>
        ModManagementSemanticPipeline.TranslateLiteral(methodName, source);
}
