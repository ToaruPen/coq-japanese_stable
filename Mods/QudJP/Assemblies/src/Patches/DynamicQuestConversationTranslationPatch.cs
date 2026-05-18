using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DynamicQuestConversationTranslationPatch
{
    private const string Context = nameof(DynamicQuestConversationTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.DynamicQuestConversationHelper");
        var conversationType = AccessTools.TypeByName("XRL.World.Conversations.ConversationXMLBlueprint");
        var questType = GameTypeResolver.FindType("Qud.API.Quest", "Quest");
        if (targetType is null || conversationType is null || questType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = ResolveTargetMethod(targetType, conversationType, questType);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.appendQuestCompletionSequence target not found.", Context);
        }

        return method;
    }

    private static MethodInfo? ResolveTargetMethod(Type targetType, Type conversationType, Type questType)
    {
        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];
            if (!string.Equals(method.Name, "appendQuestCompletionSequence", StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 10
                && parameters[0].ParameterType == conversationType
                && parameters[1].ParameterType == questType
                && parameters[2].ParameterType == conversationType
                && parameters[3].ParameterType == typeof(string)
                && parameters[4].ParameterType == typeof(string))
            {
                return method;
            }
        }

        return null;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var transformed = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (IsHistoricStringExpanderCall(instruction))
            {
                transformed++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(DynamicQuestConversationTranslationPatch), nameof(TranslateExpandedText)));
            }
        }

        if (transformed == 0)
        {
            Trace.TraceWarning("QudJP: {0}.Transpiler found no HistoricStringExpander.ExpandString calls.", Context);
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

    public static void Prefix(ref string completeText, ref string incompleteText)
    {
        try
        {
            TranslateExplicitChoiceArgument(ref completeText, "CompletionChoice");
            TranslateExplicitChoiceArgument(ref incompleteText, "IncompleteChoice");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static void TranslateExplicitChoiceArgument(ref string text, string route)
    {
        try
        {
            var source = text;
            if (!DynamicQuestExplicitConversationTextTranslator.TryTranslate(source, out var translated))
            {
                text = translated;
                return;
            }

            text = translated;
            DynamicTextObservability.RecordTransform(Context, Context + "." + route, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateExplicitChoiceArgument failed: {1}", Context, ex);
        }
    }

    private static bool IsHistoricStringExpanderCall(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Call || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        return method.ReturnType == typeof(string)
            && string.Equals(method.Name, "ExpandString", StringComparison.Ordinal);
    }
}
