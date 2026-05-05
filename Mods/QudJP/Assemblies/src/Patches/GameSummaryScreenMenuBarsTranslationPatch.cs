using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameSummaryScreenMenuBarsTranslationPatch
{
    private const string Context = nameof(GameSummaryScreenMenuBarsTranslationPatch);
    private const string SaveTombstoneFileText = "Save Tombstone File";
    private const string ExitText = "Exit";

    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(GameSummaryScreenMenuBarsTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("GameSummaryScreenMenuBarsTranslationPatch.TranslateLiteral method not found.");

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.GameSummaryScreen", "GameSummaryScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: GameSummaryScreenMenuBarsTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateMenuBars", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: GameSummaryScreenMenuBarsTranslationPatch.UpdateMenuBars() not found.");
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr
                && instruction.operand is string literal
                && IsTargetLiteral(literal))
            {
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, TranslateLiteralMethod);
                continue;
            }

            yield return instruction;
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
#if HAS_TMP
            var repaired = TmpTextRepairer.TryRepairInvisibleTexts(__instance);
            if (repaired > 0)
            {
                QudJPMod.LogToUnity(TmpTextRepairer.BuildRepairLog("GameSummaryTextRepair/v1", repaired));
            }
#else
            _ = __instance;
#endif
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateLiteral(string source)
    {
        try
        {
            var translated = Translator.Translate(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "GameSummaryScreen.MenuOption", source, translated);
            }

            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateLiteral failed: {1}", Context, ex);
            return source;
        }
    }

    private static bool IsTargetLiteral(string literal)
    {
        return string.Equals(literal, SaveTombstoneFileText, StringComparison.Ordinal)
            || string.Equals(literal, ExitText, StringComparison.Ordinal);
    }
}
