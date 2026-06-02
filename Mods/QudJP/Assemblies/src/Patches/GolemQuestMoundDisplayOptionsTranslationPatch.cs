using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GolemQuestMoundDisplayOptionsTranslationPatch
{
    private const string Context = nameof(GolemQuestMoundDisplayOptionsTranslationPatch);
    private const string TargetTypeName = "XRL.World.Parts.GolemQuestMound";

    private static readonly IReadOnlyDictionary<string, string> LiteralTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{W|[Backspace]}} {{y|Build}}"] = "{{W|[Backspace]}} {{y|建造}}",
            ["{{K|Build}}"] = "{{K|建造}}",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target type '{1}' or GameObject.", Context, TargetTypeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, "DisplayOptions", [gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.DisplayOptions(GameObject) not found.", Context);
            yield break;
        }

        yield return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var originalInstructions = instructions.ToList();
        List<CodeInstruction> translatedInstructions;
        try
        {
            translatedInstructions = new List<CodeInstruction>(originalInstructions.Count);
            foreach (var instruction in originalInstructions)
            {
                var translatedInstruction = new CodeInstruction(instruction);
                if (translatedInstruction.opcode == OpCodes.Ldstr && translatedInstruction.operand is string literal)
                {
                    translatedInstruction.operand = TranslateLiteral(literal);
                }

                translatedInstructions.Add(translatedInstruction);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            translatedInstructions = originalInstructions;
        }

        foreach (var instruction in translatedInstructions)
        {
            yield return instruction;
        }
    }

    internal static string TranslateLiteralForTests(string source)
    {
        return TranslateLiteral(source);
    }

    private static string TranslateLiteral(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var unmarked))
        {
            return unmarked;
        }

        return LiteralTranslations.TryGetValue(source, out var translated)
            ? translated
            : source;
    }
}
