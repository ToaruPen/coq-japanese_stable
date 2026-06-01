using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DescriptionLookPopupTranslationPatch
{
    private const string Context = nameof(DescriptionLookPopupTranslationPatch);
    private const string TargetTypeName = "XRL.World.Parts.Description";

    private static readonly IReadOnlyDictionary<string, string> LiteralTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Recall {{W|S}}tory"] = "{{W|S}} ストーリーを思い出す",
            ["{{C|Recall Story}}"] = "{{C|ストーリーを思い出す}}",
            ["[press {{W|space}} or recall {{W|s}}tory]"] = "[{{W|space}}で続行 / {{W|s}}でストーリー]",
            ["[press {{W|space}}]"] = "[{{W|space}}で続行]",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || inventoryActionEventType is null)
        {
            Trace.TraceError(
                "QudJP: {0} failed to resolve target type '{1}' or InventoryActionEvent.",
                Context,
                TargetTypeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [inventoryActionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) not found.", Context);
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

    public static string TranslateLiteralForTests(string source)
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
