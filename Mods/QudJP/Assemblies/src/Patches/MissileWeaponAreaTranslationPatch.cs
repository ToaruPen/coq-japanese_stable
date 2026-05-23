using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MissileWeaponAreaTranslationPatch
{
    private const string Context = nameof(MissileWeaponAreaTranslationPatch);
    private const string DictionaryFile = "ui-missile-weapon-area.ja.json";
    private static readonly MethodInfo TranslateLiteralMethod =
        AccessTools.Method(typeof(MissileWeaponAreaTranslationPatch), nameof(TranslateLiteral))
        ?? throw new InvalidOperationException("TranslateLiteral method not found.");

    private static readonly Regex HotkeyLabelPattern = new(
        "^(?<hotkey>\\{\\{W\\|\\[[^\\]]+\\]\\}\\}\\s+)(?<label>fire|reload)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.MissileWeaponArea", "MissileWeaponArea");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "AfterRender");
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.AfterRender not found.", Context);
        }

        return method;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (instruction.opcode == OpCodes.Ldstr
                && instruction.operand is string literal
                && IsHotkeyLabelSuffixLiteral(literal))
            {
                yield return new CodeInstruction(OpCodes.Call, TranslateLiteralMethod);
            }
        }
    }

    public static void Postfix(object? ___fireHotkeyText, object? ___reloadHotkeyText)
    {
        try
        {
            TranslateHotkeyText(___fireHotkeyText);
            TranslateHotkeyText(___reloadHotkeyText);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    public static string TranslateLiteral(string source)
    {
        if (string.IsNullOrEmpty(source) || !IsHotkeyLabelSuffixLiteral(source))
        {
            return source;
        }

        if (source.EndsWith(" fire", StringComparison.Ordinal))
        {
            return ReplaceLabelSuffix(source, "fire");
        }

        if (source.EndsWith(" reload", StringComparison.Ordinal))
        {
            return ReplaceLabelSuffix(source, "reload");
        }

        return source;
    }

    private static void TranslateHotkeyText(object? uiTextSkin)
    {
        var source = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        var match = HotkeyLabelPattern.Match(source);
        if (!match.Success)
        {
            return;
        }

        var label = match.Groups["label"].Value;
        var translatedLabel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, DictionaryFile);
        if (translatedLabel is null)
        {
            return;
        }

        var translated = match.Groups["hotkey"].Value + translatedLabel;
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, "MissileWeaponArea.HotkeyLabel", source, translated);
        _ = UITextSkinReflectionAccessor.SetCurrentTextField(uiTextSkin, translated);
    }

    private static bool IsHotkeyLabelSuffixLiteral(string source)
    {
        return source.EndsWith(" fire", StringComparison.Ordinal)
            || source.EndsWith(" reload", StringComparison.Ordinal);
    }

    private static string ReplaceLabelSuffix(string source, string label)
    {
        var translatedLabel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, DictionaryFile);
        if (translatedLabel is null)
        {
            return source;
        }

        return source.Substring(0, source.Length - label.Length) + translatedLabel;
    }
}
