using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LegacyGamepadPromptTranslationPatch
{
    private const string Context = nameof(LegacyGamepadPromptTranslationPatch);

    private delegate IEnumerable<CodeInstruction> ScreenTranspiler(IEnumerable<CodeInstruction> instructions);

    private static readonly (string QualifiedName, string FallbackName, ScreenTranspiler Transpiler)[] TargetHandlers =
    {
        ("XRL.UI.InventoryScreen", "InventoryScreen", InventoryScreenTranslationPatch.Transpiler),
        ("XRL.UI.StatusScreen", "StatusScreen", StatusScreenTranslationPatch.Transpiler),
        ("XRL.UI.JournalScreen", "JournalScreen", JournalScreenTranslationPatch.Transpiler),
        ("XRL.UI.TinkeringScreen", "TinkeringScreen", TinkeringScreenTranslationPatch.Transpiler),
        ("XRL.UI.QuestLog", "QuestLog", QuestLogGamepadPromptTranslationPatch.Transpiler),
        ("XRL.UI.FactionsScreen", "FactionsScreen", FactionsScreenGamepadPromptTranslationPatch.Transpiler),
        ("XRL.UI.SkillsAndPowersScreen", "SkillsAndPowersScreen", SkillsAndPowersScreenTranslationPatch.Transpiler),
        ("XRL.UI.EquipmentScreen", "EquipmentScreen", EquipmentScreenTranslationPatch.Transpiler),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return ResolveTargetMethods().OfType<MethodBase>();
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        try
        {
            return DispatchTranspiler(instructions, __originalMethod);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    private static IEnumerable<MethodBase?> ResolveTargetMethods()
    {
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        var eventType = GameTypeResolver.FindType("XRL.World.IEvent", "IEvent");

        for (var index = 0; index < TargetHandlers.Length; index++)
        {
            var target = TargetHandlers[index];
            var parameterTypes = string.Equals(target.FallbackName, "TinkeringScreen", StringComparison.Ordinal)
                ? new[] { gameObjectType, gameObjectType, eventType }
                : new[] { gameObjectType };
            yield return TryResolveShowMethod(target.QualifiedName, target.FallbackName, parameterTypes);
        }
    }

    private static MethodBase? TryResolveShowMethod(string fullTypeName, string shortTypeName, Type?[] parameterTypes)
    {
        try
        {
            var targetType = GameTypeResolver.FindType(fullTypeName, shortTypeName);
            if (targetType is null || Array.Exists(parameterTypes, static parameterType => parameterType is null))
            {
                Trace.TraceError("QudJP: {0}.Show parameter types could not be resolved.", fullTypeName);
                return null;
            }

            var resolvedParameterTypes = Array.ConvertAll(parameterTypes, static parameterType => parameterType!);
            var method = AccessTools.Method(targetType, "Show", resolvedParameterTypes);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.Show target method not found.", fullTypeName);
            }

            return method;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Show target resolution failed: {1}", fullTypeName, ex);
            return null;
        }
    }

    private static IEnumerable<CodeInstruction> DispatchTranspiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase originalMethod)
    {
        var declaringType = originalMethod.DeclaringType;
        for (var index = 0; index < TargetHandlers.Length; index++)
        {
            if (MatchesTargetType(declaringType, TargetHandlers[index].QualifiedName, TargetHandlers[index].FallbackName))
            {
                return TargetHandlers[index].Transpiler(instructions);
            }
        }

        return instructions;
    }

    private static bool MatchesTargetType(Type? declaringType, string qualifiedName, string fallbackName)
    {
        return string.Equals(declaringType?.FullName, qualifiedName, StringComparison.Ordinal)
               || string.Equals(declaringType?.Name, fallbackName, StringComparison.Ordinal);
    }
}
