using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DynamicQuestConstructorConversationTranslationPatch
{
    private const string Context = nameof(DynamicQuestConstructorConversationTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 3);
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        var questType = GameTypeResolver.FindType("Qud.API.Quest", "Quest");
        if (gameObjectType is null || questType is null)
        {
            Trace.TraceError("QudJP: {0} shared target parameter type not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver",
            "addQuestConversationToGiver",
            gameObjectType,
            questType,
            gameObjectType);
        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver",
            "addQuestConversationToGiver",
            gameObjectType,
            questType);
        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver",
            "addQuestConversationToGiver",
            gameObjectType,
            questType,
            gameObjectType);
        return targets;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var transformed = 0;
        foreach (var instruction in instructions)
        {
            if (IsQuestIntroConsumerCall(instruction))
            {
                transformed++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(DynamicQuestConstructorConversationTranslationPatch), nameof(TranslateExpandedText)));
            }

            yield return instruction;
            if (IsTranslatableStringProducerCall(instruction))
            {
                transformed++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(DynamicQuestConstructorConversationTranslationPatch), nameof(TranslateExpandedText)));
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
            if (!DynamicQuestConstructorConversationTextTranslator.TryTranslate(source, out var translated))
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

    private static void AddTarget(ICollection<MethodBase> targets, string typeName, string methodName, params Type[] parameterTypes)
    {
        var type = AccessTools.TypeByName(typeName);
        var method = type is null ? null : AccessTools.Method(type, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}({3}).", Context, typeName, methodName, string.Join(", ", Array.ConvertAll(parameterTypes, static type => type.FullName)));
            return;
        }

        targets.Add(method);
    }

    private static bool IsTranslatableStringProducerCall(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Call
            && instruction.opcode != OpCodes.Callvirt
            || instruction.operand is not MethodInfo { ReturnType: var returnType } method
            || returnType != typeof(string))
        {
            return false;
        }

        if (method.Name == "ExpandString")
        {
            return true;
        }

        return IsStringReplaceCall(method) || IsStringConcatCall(method);
    }

    private static bool IsQuestIntroConsumerCall(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Call
            && instruction.opcode != OpCodes.Callvirt
            || instruction.operand is not MethodInfo method
            || method.Name != "AddNode")
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length > 0
            && parameters[parameters.Length - 1].ParameterType == typeof(string);
    }

    private static bool IsStringReplaceCall(MethodInfo method)
    {
        return method.DeclaringType == typeof(string)
            && method.Name == nameof(string.Replace)
            && method.GetParameters() is { Length: 2 } parameters
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].ParameterType == typeof(string);
    }

    private static bool IsStringConcatCall(MethodInfo method)
    {
        return method.DeclaringType == typeof(string)
            && method.Name == nameof(string.Concat);
    }
}
