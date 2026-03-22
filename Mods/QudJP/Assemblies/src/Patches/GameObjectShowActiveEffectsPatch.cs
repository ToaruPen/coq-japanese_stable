using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectShowActiveEffectsPatch
{
    private const string NoActiveEffectsText = "No active effects.";
    private const string ActiveEffectsTitlePrefix = "&WActive Effects&Y - ";

    private static readonly MethodInfo TranslateNoActiveEffectsTextMethod =
        AccessTools.Method(typeof(GameObjectShowActiveEffectsPatch), nameof(TranslateNoActiveEffectsText))
        ?? throw new InvalidOperationException("TranslateNoActiveEffectsText method not found.");

    private static readonly MethodInfo TranslateActiveEffectsTitlePrefixMethod =
        AccessTools.Method(typeof(GameObjectShowActiveEffectsPatch), nameof(TranslateActiveEffectsTitlePrefix))
        ?? throw new InvalidOperationException("TranslateActiveEffectsTitlePrefix method not found.");

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: GameObjectShowActiveEffectsPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ShowActiveEffects", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectShowActiveEffectsPatch.ShowActiveEffects not found.");
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal)
            {
                if (string.Equals(literal, NoActiveEffectsText, StringComparison.Ordinal))
                {
                    yield return new CodeInstruction(OpCodes.Call, TranslateNoActiveEffectsTextMethod);
                    continue;
                }

                if (string.Equals(literal, ActiveEffectsTitlePrefix, StringComparison.Ordinal))
                {
                    yield return new CodeInstruction(OpCodes.Call, TranslateActiveEffectsTitlePrefixMethod);
                    continue;
                }
            }

            yield return instruction;
        }
    }

    public static string TranslateNoActiveEffectsText()
    {
        var translated = Translator.Translate(NoActiveEffectsText);
        return string.Equals(translated, NoActiveEffectsText, StringComparison.Ordinal)
            ? NoActiveEffectsText
            : translated;
    }

    public static string TranslateActiveEffectsTitlePrefix()
    {
        var template = Translator.Translate("Active Effects - {0}");
        if (string.Equals(template, "Active Effects - {0}", StringComparison.Ordinal))
        {
            return ActiveEffectsTitlePrefix;
        }

        var visiblePrefix = template.Replace("{0}", string.Empty);
        const string delimiter = " - ";
        if (visiblePrefix.EndsWith(delimiter, StringComparison.Ordinal))
        {
            visiblePrefix = visiblePrefix.Substring(0, visiblePrefix.Length - delimiter.Length);
        }

        return "&W" + visiblePrefix + "&Y - ";
    }
}
