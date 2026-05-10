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

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return ResolveTargetMethods()
            .Where(static target => target is not null)
            .Cast<MethodBase>();
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

        yield return TryResolveShowMethod("XRL.UI.InventoryScreen", "InventoryScreen", new[] { gameObjectType });
        yield return TryResolveShowMethod("XRL.UI.StatusScreen", "StatusScreen", new[] { gameObjectType });
        yield return TryResolveShowMethod("XRL.UI.JournalScreen", "JournalScreen", new[] { gameObjectType });
        yield return TryResolveShowMethod("XRL.UI.TinkeringScreen", "TinkeringScreen", new[] { gameObjectType, gameObjectType, eventType });
        yield return TryResolveShowMethod("XRL.UI.QuestLog", "QuestLog", new[] { gameObjectType });
        yield return TryResolveShowMethod("XRL.UI.FactionsScreen", "FactionsScreen", new[] { gameObjectType });
        yield return TryResolveShowMethod("XRL.UI.SkillsAndPowersScreen", "SkillsAndPowersScreen", new[] { gameObjectType });
        yield return TryResolveShowMethod("XRL.UI.EquipmentScreen", "EquipmentScreen", new[] { gameObjectType });
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
        return originalMethod.DeclaringType?.FullName switch
        {
            "XRL.UI.InventoryScreen" => InventoryScreenTranslationPatch.Transpiler(instructions),
            "XRL.UI.StatusScreen" => StatusScreenTranslationPatch.Transpiler(instructions),
            "XRL.UI.JournalScreen" => JournalScreenTranslationPatch.Transpiler(instructions),
            "XRL.UI.TinkeringScreen" => TinkeringScreenTranslationPatch.Transpiler(instructions),
            "XRL.UI.QuestLog" => QuestLogGamepadPromptTranslationPatch.Transpiler(instructions),
            "XRL.UI.FactionsScreen" => FactionsScreenGamepadPromptTranslationPatch.Transpiler(instructions),
            "XRL.UI.SkillsAndPowersScreen" => SkillsAndPowersScreenTranslationPatch.Transpiler(instructions),
            "XRL.UI.EquipmentScreen" => EquipmentScreenTranslationPatch.Transpiler(instructions),
            _ => instructions,
        };
    }
}
