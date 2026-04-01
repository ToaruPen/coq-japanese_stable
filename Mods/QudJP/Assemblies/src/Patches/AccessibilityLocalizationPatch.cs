using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AccessibilityLocalizationPatch
{
    private const string Context = nameof(AccessibilityLocalizationPatch);

    private static readonly Dictionary<string, string> Translations = new(StringComparer.Ordinal)
    {
        ["ElementDisabled"] = "無効",
        ["Element_Button"] = "ボタン",
        ["Element_Slider"] = "スライダー",
        ["Element_Toggle"] = "トグル",
        ["Element_TextEdit"] = "テキスト入力",
        ["Element_Dropdown"] = "ドロップダウン",
        ["Desktop_HintButton"] = "Enterキーで選択",
        ["Desktop_HintTextEdit"] = "Enterキーで編集",
        ["Desktop_HintToggle"] = "Enterキーで切り替え",
        ["Desktop_HintDropdown"] = "Enterキーで開く",
        ["Desktop_HintSlider"] = "上下キーで調整",
        ["Desktop_General"] = "矢印キーで移動、Enterキーで選択",
        ["Mobile_HintButton"] = "ダブルタップで選択",
        ["Mobile_HintTextEdit"] = "ダブルタップで編集",
        ["Mobile_HintToggle"] = "ダブルタップで切り替え",
        ["Mobile_HintDropdown"] = "ダブルタップで開く",
        ["Mobile_HintSlider"] = "スワイプで調整",
        ["Mobile_General"] = "スワイプで移動、ダブルタップで選択",
        ["EnabledAccessibility"] = "アクセシビリティが有効になりました",
        ["DisabledAccessibility"] = "アクセシビリティが無効になりました",
        ["Checkbox_Checked"] = "チェック済み",
        ["Checkbox_NotChecked"] = "未チェック",
        ["DropdownItemSelected"] = "選択済み",
        ["DropdownItemIndex"] = "項目",
        ["Keyboard_PasswordHidden"] = "パスワードは非表示です",
        ["Keyboard_CapitalLetter"] = "大文字 X",
        ["Keyboard_ShiftOn"] = "シフトオン",
        ["Keyboard_ShiftOff"] = "シフトオフ",
        ["Keyboard_ShowingLanguage"] = "表示中の言語: ",
        ["Keyboard_ShowingLetters"] = "文字を表示中",
        ["Keyboard_ShiftKey"] = "シフト",
        ["Keyboard_NumbersAndSymbols"] = "数字と記号",
        ["Keyboard_ShowingNumbers"] = "数字を表示中",
        ["Keyboard_Symbols"] = "記号",
        ["Keyboard_Letters"] = "文字",
        ["Keyboard_ShowingSymbols"] = "記号を表示中",
        ["Keyboard_Numbers"] = "数字",
        ["Keyboard_Return"] = "改行",
        ["Keyboard_Done"] = "完了",
        ["Keyboard_Showing"] = "キーボードを表示中",
        ["Keyboard_Hidden"] = "キーボードを非表示にしました",
    };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("UAP_AccessibilityManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Localize_Internal", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Localize_Internal(string) not found.", Context);
        }

        return method;
    }

    public static void Postfix(string key, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(key) || !Translations.TryGetValue(key, out var translated) || string.IsNullOrEmpty(translated))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
