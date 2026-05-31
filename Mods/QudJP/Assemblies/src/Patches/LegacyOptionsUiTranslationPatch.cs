using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LegacyOptionsUiTranslationPatch
{
    internal const string Context = nameof(LegacyOptionsUiTranslationPatch);
    internal const string Family = Context + ".BufferText";

    private static readonly MethodInfo TranslateBufferTextMethod =
        AccessTools.Method(typeof(LegacyOptionsUiTranslationPatch), nameof(TranslateBufferText))
        ?? throw new InvalidOperationException("LegacyOptionsUiTranslationPatch.TranslateBufferText not found.");

    private static readonly Regex ColorPrefixPattern =
        new Regex("^(?<prefix>&[A-Za-z])(?<visible>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RestartOptionLinePattern =
        new Regex(
            "\\{\\{g\\|\\* (?<label>.+?)\\}\\}",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly IReadOnlyDictionary<string, string> LiteralTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[ &wGame Options&y ]"] = "[ &wゲームオプション&y ]",
            [" &WESC&y - Exit "] = " &WESC&y - 終了 ",
            [" [&WSpace&y-change option] "] = " [&WSpace&y-オプション変更] ",
            ["&W<More...>"] = "&W<続き…>",
            ["&W<< &K[more]  "] = "&W<< &K[続き]  ",
            ["  &K[more] &W>>"] = "  &K[続き] &W>>",
            ["[more]"] = "[続き]",
            ["Would you like to save your changes?"] = "変更を保存しますか？",
        };

    private static readonly IReadOnlyDictionary<string, string> LegacyOptionTermTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Gameplay"] = "ゲームプレイ",
            ["General"] = "一般",
            ["Use Tiles"] = "タイルを使用",
            ["VSync"] = "垂直同期",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.OptionsUI");
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
            if (instruction.opcode == OpCodes.Ldstr
                && instruction.operand is string literal
                && LiteralTranslations.ContainsKey(literal))
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

            if (IsParameterlessStringToStringCall(instruction))
            {
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, TranslateBufferTextMethod);
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

    private static string TranslateBufferTextCore(string source)
    {
        if (LiteralTranslations.TryGetValue(source, out var literal))
        {
            return literal;
        }

        if (TryTranslateRestartPrompt(source, out var restartPrompt))
        {
            return restartPrompt;
        }

        return TranslateWithOuterWhitespace(source);
    }

    private static bool TryTranslateRestartPrompt(string source, out string translated)
    {
        const string header = "These options require a game restart to take effect:\n";
        const string footer = "\n\nDo you want to do so now?";
        if (!source.StartsWith(header, StringComparison.Ordinal) || !source.EndsWith(footer, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var body = source.Substring(header.Length, source.Length - header.Length - footer.Length);
        var translatedBody = RestartOptionLinePattern.Replace(
            body,
            match => "{{g|* " + TranslateVisibleToken(match.Groups["label"].Value) + "}}");
        translated = "これらのオプションを有効にするにはゲームの再起動が必要です:\n"
            + translatedBody
            + "\n\n今すぐ再起動しますか？";
        return true;
    }

    private static string TranslateWithOuterWhitespace(string source)
    {
        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        var leading = source.Substring(0, leadingLength);
        var visibleEnd = source.Length - trailingLength;
        var visible = source.Substring(leadingLength, visibleEnd - leadingLength);
        var trailing = source.Substring(visibleEnd);
        return leading + TranslateVisibleToken(visible) + trailing;
    }

    private static string TranslateVisibleToken(string source)
    {
        if (LiteralTranslations.TryGetValue(source, out var literal))
        {
            return literal;
        }

        var colorMatch = ColorPrefixPattern.Match(source);
        if (colorMatch.Success)
        {
            var visible = colorMatch.Groups["visible"].Value;
            if (visible.EndsWith("&y", StringComparison.Ordinal))
            {
                return colorMatch.Groups["prefix"].Value
                    + TranslateVisibleToken(visible.Substring(0, visible.Length - 2))
                    + "&y";
            }

            return colorMatch.Groups["prefix"].Value + TranslateVisibleToken(visible);
        }

        if (LegacyOptionTermTranslations.TryGetValue(source, out var legacyTerm))
        {
            return legacyTerm;
        }

        return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated) ? translated : source;
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

    private static bool IsParameterlessStringToStringCall(CodeInstruction instruction)
    {
        return instruction.operand is MethodInfo method
            && string.Equals(method.Name, "ToString", StringComparison.Ordinal)
            && method.ReturnType == typeof(string)
            && method.GetParameters().Length == 0;
    }
}
