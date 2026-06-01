using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class XrlCoreRestoreModsLoadedTranslationPatch
{
    private const string Context = nameof(XrlCoreRestoreModsLoadedTranslationPatch);

    private static readonly IReadOnlyDictionary<string, string> LiteralTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["One or more mods enabled in this save are {{red|not available}}:{{red|"] =
                "このセーブで有効な1つ以上のModが{{red|利用できません}}:{{red|",
            ["}}Do you still wish to try to load this save?"] =
                "}}このセーブを読み込んでみますか？",
            ["Incomplete Mod Configuration"] =
                "不完全なMod構成",
            ["These mods are {{red|disabled}} in the save:{{red|"] =
                "このセーブでは{{red|無効}}になっているMod:{{red|",
            ["These mods are {{green|enabled}} in the save:{{green|"] =
                "このセーブでは{{green|有効}}になっているMod:{{green|",
            ["Restart using save game's mod configuration"] =
                "セーブのMod構成で再起動",
            ["Load keeping current mod configuration"] =
                "現在のMod構成のまま読み込む",
            ["Mod Configuration Differs"] =
                "Mod構成が異なります",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var coreType = GameTypeResolver.FindType("XRL.Core.XRLCore", "XRLCore");
        if (coreType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRL.Core.XRLCore.", Context);
            yield break;
        }

        var sourceMethod = AccessTools.Method(coreType, "RestoreModsLoadedAsync", [typeof(List<string>)]);
        if (sourceMethod is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRLCore.RestoreModsLoadedAsync(List<string>).", Context);
            yield break;
        }

        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        var moveNext = asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
        if (moveNext is not null)
        {
            yield return moveNext;
            yield break;
        }

        Trace.TraceError("QudJP: {0} failed to resolve RestoreModsLoadedAsync state machine MoveNext.", Context);
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal)
            {
                instruction.operand = TranslateLiteral(literal);
            }

            yield return instruction;
        }
    }

    public static string TranslateLiteralForTests(string source) => TranslateLiteral(source);

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

        if (LiteralTranslations.TryGetValue(source, out var translated))
        {
            return translated;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (spans.Count > 0 && LiteralTranslations.TryGetValue(stripped, out translated))
        {
            return ColorAwareTranslationComposer.Restore(translated, spans);
        }

        return source;
    }
}
