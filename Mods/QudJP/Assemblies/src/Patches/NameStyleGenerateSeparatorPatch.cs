using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class NameStyleGenerateSeparatorPatch
{
    private const string Context = nameof(NameStyleGenerateSeparatorPatch);

    private static readonly MethodInfo? StringBuilderAppendCharMethod =
        AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new[] { typeof(char) });

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return ResolveTargetMethod(
            () => GameTypeResolver.FindType("XRL.Names.NameStyle", "NameStyle"),
            targetType => AccessTools.Method(targetType, "Generate"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (StringBuilderAppendCharMethod is null)
        {
            Trace.TraceWarning("QudJP: {0} StringBuilder.Append(char) not found; leaving instructions unchanged.", Context);
            return instructions;
        }

        try
        {
            var originalInstructions = instructions.ToList();
            var rewritten = originalInstructions
                .Select(instruction => new CodeInstruction(instruction))
                .ToList();
            var replacements = 0;

            for (var index = 0; index < rewritten.Count - 1; index++)
            {
                if (!CallsStringBuilderAppendChar(rewritten[index + 1])
                    || !TryGetLoadedChar(rewritten[index], out var separator))
                {
                    continue;
                }

                var translated = NameStyleGenerateSeparatorHelpers.TranslateSeparator(separator);
                if (translated == separator)
                {
                    continue;
                }

                ReplaceLoadedChar(rewritten[index], translated);
                replacements++;
            }

            if (replacements != 4)
            {
                Trace.TraceWarning(
                    "QudJP: {0} replaced {1} separator site(s); expected 4; leaving instructions unchanged.",
                    Context,
                    replacements);
                return originalInstructions;
            }

            return rewritten;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    private static bool CallsStringBuilderAppendChar(CodeInstruction instruction)
    {
        if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        return method == StringBuilderAppendCharMethod;
    }

    private static bool TryGetLoadedChar(CodeInstruction instruction, out char value)
    {
        if (instruction.opcode == OpCodes.Ldc_I4_M1)
        {
            value = unchecked((char)(-1));
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_0)
        {
            value = (char)0;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_1)
        {
            value = (char)1;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_2)
        {
            value = (char)2;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_3)
        {
            value = (char)3;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_4)
        {
            value = (char)4;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_5)
        {
            value = (char)5;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_6)
        {
            value = (char)6;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_7)
        {
            value = (char)7;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_8)
        {
            value = (char)8;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte signedByte)
        {
            value = (char)signedByte;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is byte unsignedByte)
        {
            value = (char)unsignedByte;
            return true;
        }

        if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int intValue)
        {
            value = (char)intValue;
            return true;
        }

        value = default;
        return false;
    }

    private static void ReplaceLoadedChar(CodeInstruction instruction, char value)
    {
        instruction.opcode = OpCodes.Ldc_I4;
        instruction.operand = (int)value;
    }

    private static MethodBase? ResolveTargetMethod(
        Func<Type?> targetTypeResolver,
        Func<Type, MethodInfo?> methodResolver)
    {
        try
        {
            var targetType = targetTypeResolver();
            if (targetType is null)
            {
                Trace.TraceError("QudJP: {0} target type not found.", Context);
                return null;
            }

            var method = methodResolver(targetType);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.Generate() not found.", Context);
            }

            return method;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TargetMethod failed: {1}", Context, ex);
            return null;
        }
    }
}
