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
    private const string DisabledScriptsSuffix =
        " contains scripts and has been permanently disabled in the options.\n{{K|(Options->Modding->Allow scripting mods)}}";
    private const string DisabledScriptsSuffixJa =
        " にはスクリプトが含まれていますが、オプションで永続的に無効化されています。\n{{K|(オプション->Mod->スクリプトModを許可)}}";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, "OnActivate");
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.OnActivate(...) not found on '{1}'.", Context, TargetTypeName);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            var translated = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string literal
                    && string.Equals(literal, DisabledScriptsSuffix, StringComparison.Ordinal))
                {
                    instruction.operand = DisabledScriptsSuffixJa;
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
        return string.Equals(source, DisabledScriptsSuffix, StringComparison.Ordinal)
            ? DisabledScriptsSuffixJa
            : source;
    }
}
