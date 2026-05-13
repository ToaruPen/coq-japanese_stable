using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MissileWeaponAreaTranslationPatch
{
    private const string Context = nameof(MissileWeaponAreaTranslationPatch);
    private const string DictionaryFile = "ui-missile-weapon-area.ja.json";

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
}
