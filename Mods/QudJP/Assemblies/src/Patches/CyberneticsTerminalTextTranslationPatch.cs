using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsTerminalTextTranslationPatch
{
    private const string Context = nameof(CyberneticsTerminalTextTranslationPatch);

    private static readonly MethodInfo? TranslateScreenMethod =
        AccessTools.Method(typeof(CyberneticsTerminalTextTranslator), nameof(CyberneticsTerminalTextTranslator.TranslateScreen));

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        if (TranslateScreenMethod is null)
        {
            Trace.TraceWarning("QudJP: {0}.TranslateScreen() not found; patch will be skipped.", Context);
            return null;
        }

        var targetType = GameTypeResolver.FindType("XRL.UI.TerminalScreen", "TerminalScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Update", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Update() not found.", Context);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (TranslateScreenMethod is null)
        {
            Trace.TraceWarning("QudJP: {0}.TranslateScreen() not found during transpile; leaving instructions unchanged.", Context);
            foreach (var instruction in instructions)
            {
                yield return instruction;
            }

            yield break;
        }

        var injected = false;
        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (!injected && IsOnUpdateCall(instruction))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, TranslateScreenMethod);
                injected = true;
            }
        }

        if (!injected)
        {
            Trace.TraceWarning("QudJP: {0} failed to locate OnUpdate() call.", Context);
        }
    }

    private static bool IsOnUpdateCall(CodeInstruction instruction)
    {
        if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        return string.Equals(method.Name, "OnUpdate", StringComparison.Ordinal)
            && method.ReturnType == typeof(void)
            && method.GetParameters().Length == 0;
    }
}
