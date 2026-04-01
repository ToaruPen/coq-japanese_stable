using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

internal static class LegacyGamepadPromptTranspilerHelpers
{
    public static IEnumerable<CodeInstruction> Apply(
        IEnumerable<CodeInstruction> instructions,
        ISet<string> translatableLiterals,
        MethodInfo translateLiteralMethod,
        MethodInfo translateRenderedMethod,
        string context)
    {
        try
        {
            var rewritten = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string literal
                    && translatableLiterals.Contains(literal))
                {
                    rewritten.Add(instruction);
                    rewritten.Add(new CodeInstruction(OpCodes.Call, translateLiteralMethod));
                    continue;
                }

                if (IsTranslatableStringSink(instruction))
                {
                    var translateInstruction = new CodeInstruction(OpCodes.Call, translateRenderedMethod)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks,
                    };

                    instruction.labels = new List<Label>();
                    instruction.blocks = new List<ExceptionBlock>();

                    rewritten.Add(translateInstruction);
                    rewritten.Add(instruction);
                    continue;
                }

                rewritten.Add(instruction);
            }

            return rewritten;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler helper failed: {1}", context, ex);
            return instructions;
        }
    }

    private static bool IsTranslatableStringSink(CodeInstruction instruction)
    {
        if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        var parameters = method.GetParameters();
        return (method.ReturnType == typeof(void)
                && ((string.Equals(method.Name, "Write", StringComparison.Ordinal)
                     && parameters.Length == 1
                     && parameters[0].ParameterType == typeof(string))
                    || (string.Equals(method.Name, "WriteAt", StringComparison.Ordinal)
                        && parameters.Length == 3
                        && parameters[0].ParameterType == typeof(int)
                        && parameters[1].ParameterType == typeof(int)
                        && parameters[2].ParameterType == typeof(string))))
               || (method.ReturnType == typeof(string)
                   && string.Equals(method.Name, "StripFormatting", StringComparison.Ordinal)
                   && parameters.Length == 1
                   && parameters[0].ParameterType == typeof(string));
    }
}
