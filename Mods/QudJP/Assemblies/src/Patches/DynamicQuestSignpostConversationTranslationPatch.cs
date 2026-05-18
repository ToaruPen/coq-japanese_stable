using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DynamicQuestSignpostConversationTranslationPatch
{
    private const string Context = nameof(DynamicQuestSignpostConversationTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.World.Parts.DynamicQuestSignpostConversation",
            "DynamicQuestSignpostConversation");
        var eventType = GameTypeResolver.FindType(
            "XRL.World.BeforeConversationEvent",
            "BeforeConversationEvent");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(BeforeConversationEvent) target not found.", Context);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var transformed = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (IsTranslatableStringProducerCall(instruction))
            {
                transformed++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(DynamicQuestSignpostConversationTranslationPatch), nameof(TranslateExpandedText)));
            }
        }

        if (transformed == 0)
        {
            Trace.TraceWarning("QudJP: {0}.Transpiler found no translatable string producer calls.", Context);
        }
    }

    public static string TranslateExpandedText(string source)
    {
        try
        {
            if (!DynamicQuestConversationTextTranslator.TryTranslate(source, out var translated))
            {
                return translated;
            }

            DynamicTextObservability.RecordTransform(Context, Context + ".ExpandString", source, translated);
            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateExpandedText failed: {1}", Context, ex);
            return source;
        }
    }

    private static bool IsTranslatableStringProducerCall(CodeInstruction instruction)
    {
        return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
            && instruction.operand is MethodInfo { ReturnType: var returnType } method
            && returnType == typeof(string)
            && (method.Name == "ExpandString"
                || method.DeclaringType == typeof(string) && method.Name == nameof(string.Concat));
    }
}
