using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModInfoTranslationPatch
{
    private const string Context = nameof(ModInfoTranslationPatch);
    private const string TargetTypeName = "XRL.ModInfo";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            yield break;
        }

        foreach (var methodName in new[]
                 {
                     "ConfirmDependencies",
                     "ConfirmUpdate",
                     "DownloadUpdate",
                     "AppendDependencyConfirmation",
                 })
        {
            var method = AccessTools.Method(targetType, methodName);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.{1}(...) not found on '{2}'.", Context, methodName, TargetTypeName);
                continue;
            }

            yield return method;
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(MethodBase? __originalMethod, IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            var translated = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string literal
                    && __originalMethod is not null)
                {
                    instruction.operand = TranslateLiteral(__originalMethod.Name, literal);
                }

                translated.Add(instruction);
            }

            return translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Transpiler failed for {1}: {2}", Context, __originalMethod?.Name ?? "(unknown)", ex);
            return instructions;
        }
    }

    internal static string TranslateLiteral(string methodName, string source)
    {
        return methodName switch
        {
            "ConfirmDependencies" => TranslateConfirmDependenciesLiteral(source),
            "ConfirmUpdate" => TranslateConfirmUpdateLiteral(source),
            "DownloadUpdate" => TranslateDownloadUpdateLiteral(source),
            "AppendDependencyConfirmation" => TranslateAppendDependencyConfirmationLiteral(source),
            _ => source,
        };
    }

    internal static string TranslateLiteralForTests(string methodName, string source) => TranslateLiteral(methodName, source);

    private static string TranslateConfirmDependenciesLiteral(string source)
    {
        return string.Equals(source, "{{W|Dependencies}}", StringComparison.Ordinal)
            ? "{{W|依存関係}}"
            : source;
    }

    private static string TranslateConfirmUpdateLiteral(string source)
    {
        return source switch
        {
            " has a new version available: " => "の新しいバージョンが利用可能です: ",
            "\n\nDo you want to download it?" => "\n\nダウンロードしますか？",
            ".\n\nDo you want to download it?" => ".\n\nダウンロードしますか？",
            "{{W|Update Available}}" => "{{W|更新あり}}",
            _ => source,
        };
    }

    private static string TranslateDownloadUpdateLiteral(string source)
    {
        return source switch
        {
            "Updating " => string.Empty,
            "..." => "を更新中…",
            _ => source,
        };
    }

    private static string TranslateAppendDependencyConfirmationLiteral(string source)
    {
        return source switch
        {
            "Invalid" => "無効",
            "Version mismatch" => "バージョン不一致",
            "Missing" => "未検出",
            _ => source,
        };
    }
}
