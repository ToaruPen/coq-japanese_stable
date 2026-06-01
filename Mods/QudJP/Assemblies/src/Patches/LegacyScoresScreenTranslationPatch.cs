using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LegacyScoresScreenTranslationPatch
{
    internal const string Context = nameof(LegacyScoresScreenTranslationPatch);
    internal const string Family = Context + ".BufferText";

    private static readonly MethodInfo TranslateBufferTextMethod =
        AccessTools.Method(typeof(LegacyScoresScreenTranslationPatch), nameof(TranslateBufferText))
        ?? throw new InvalidOperationException("LegacyScoresScreenTranslationPatch.TranslateBufferText not found.");

    private static readonly MethodInfo TranslateBufferBuilderMethod =
        AccessTools.Method(typeof(LegacyScoresScreenTranslationPatch), nameof(TranslateBufferBuilder))
        ?? throw new InvalidOperationException("LegacyScoresScreenTranslationPatch.TranslateBufferBuilder not found.");

    private static readonly IReadOnlyDictionary<string, string> LiteralTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["&Y>Local Scores"] = "&Y>終了した冒険",
            [" &yLocal Scores"] = " &y終了した冒険",
            ["Local Scores"] = "終了した冒険",
            ["&Y>Daily"] = "&Y>デイリー",
            [" &yDaily"] = " &yデイリー",
            ["Daily"] = "デイリー",
            ["&Y>Daily (friends)"] = "&Y>デイリー (フレンド)",
            [" &yDaily (friends)"] = " &yデイリー (フレンド)",
            ["Daily (friends)"] = "デイリー (フレンド)",
            ["No high scores!"] = "ハイスコアはありません！",
            ["<more...>"] = "<続き…>",
            ["loading scores..."] = "スコアを読み込み中…",
            ["<not connected to provider>"] = "<プロバイダーに接続されていません>",
            ["&Y[&WR&y - Revisit Epilogue&Y] &Y[&WD / Del&y - Delete&Y]"] =
                "&Y[&WR&y - エピローグ再訪&Y] &Y[&WD / Del&y - 削除&Y]",
            ["&Y[&WD / Del&y - Delete&Y]"] = "&Y[&WD / Del&y - 削除&Y]",
            ["&WDown&y-next page &WUp&y-previous page"] = "&WDown&y-次のページ &WUp&y-前のページ",
            ["&W7&y-previous board &W9&y-next board"] = "&W7&y-前のボード &W9&y-次のボード",
            ["This game was played in Classic mode."] = "このゲームはクラシックモードでプレイされた。",
            ["Page "] = "ページ ",
            [" of "] = " / ",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.Core.Scores");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "Show", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Show() target not found.", Context);
            yield break;
        }

        yield return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal && ShouldTranslateLiteral(literal))
            {
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, TranslateBufferTextMethod);
                continue;
            }

            if (IsStringWriteCall(instruction))
            {
                yield return new CodeInstruction(OpCodes.Call, TranslateBufferTextMethod);
                yield return instruction;
                continue;
            }

            if (IsStringBuilderWriteCall(instruction))
            {
                yield return new CodeInstruction(OpCodes.Call, TranslateBufferBuilderMethod);
                yield return instruction;
                continue;
            }

            yield return instruction;
        }
    }

    internal static string TranslateBufferText(string source)
    {
        try
        {
            if (string.IsNullOrEmpty(source))
            {
                return source;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
            {
                return markedText;
            }

            var translated = TranslateBufferTextCore(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, Family, source, translated);
            }

            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateBufferText failed: {1}", Context, ex);
            return source;
        }
    }

    internal static StringBuilder TranslateBufferBuilder(StringBuilder source)
    {
        var original = source.ToString();
        var translated = TranslateBufferText(original);
        return string.Equals(translated, original, StringComparison.Ordinal)
            ? source
            : new StringBuilder(translated);
    }

    private static string TranslateBufferTextCore(string source)
    {
        if (LiteralTranslations.TryGetValue(source, out var literal))
        {
            return literal;
        }

        var translatedDetails = GameSummaryTextTranslator.TranslateDetails(source);
        return string.Equals(translatedDetails, source, StringComparison.Ordinal)
            ? source
            : translatedDetails;
    }

    private static bool ShouldTranslateLiteral(string literal)
    {
        return LiteralTranslations.ContainsKey(literal)
            || string.Equals(literal, "This game was played in ", StringComparison.Ordinal)
            || string.Equals(literal, " mode.", StringComparison.Ordinal);
    }

    private static bool IsStringWriteCall(CodeInstruction instruction)
    {
        if (instruction.operand is not MethodInfo method
            || !string.Equals(method.Name, "Write", StringComparison.Ordinal))
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
    }

    private static bool IsStringBuilderWriteCall(CodeInstruction instruction)
    {
        if (instruction.operand is not MethodInfo method
            || !string.Equals(method.Name, "Write", StringComparison.Ordinal))
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length == 1 && parameters[0].ParameterType == typeof(StringBuilder);
    }
}
