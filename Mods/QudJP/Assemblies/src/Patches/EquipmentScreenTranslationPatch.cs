using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EquipmentScreenTranslationPatch
{
    private const string Context = nameof(EquipmentScreenTranslationPatch);

    private static readonly HashSet<string> TranslatableLiterals = new(StringComparer.Ordinal)
    {
        "< {{W|7}} Inventory | Factions {{W|9}} >",
        " Inventory | Factions ",
    };

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(EquipmentScreenTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly MethodInfo TranslateRenderedPromptMethod =
        AccessTools.Method(typeof(EquipmentScreenTranslationPatch), nameof(TranslateRenderedPrompt))
        ?? throw new InvalidOperationException("TranslateRenderedPrompt method not found.");

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.EquipmentScreen", "EquipmentScreen");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type or GameObject type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Show", new[] { gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Show(GameObject) not found.", Context);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return LegacyGamepadPromptTranspilerHelpers.Apply(
                instructions,
                TranslatableLiterals,
                TranslateLiteralMethod,
                TranslateRenderedPromptMethod,
                Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed: {1}", Context, ex);
            return instructions;
        }
    }

    public static string TranslateLiteral(string source)
    {
        try
        {
            return LegacyGamepadPromptTranslationHelpers.TranslateEquipmentLiteral(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateLiteral failed: {1}", Context, ex);
            return source;
        }
    }

    public static string TranslateRenderedPrompt(string source)
    {
        try
        {
            return LegacyGamepadPromptTranslationHelpers.TranslateEquipmentRendered(source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateRenderedPrompt failed: {1}", Context, ex);
            return source;
        }
    }
}
