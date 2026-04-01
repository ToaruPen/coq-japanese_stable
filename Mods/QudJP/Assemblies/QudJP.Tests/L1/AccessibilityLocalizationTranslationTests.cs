using System.Reflection;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class AccessibilityLocalizationTranslationTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
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

    [Test]
    public void TranslationCatalog_ContainsAll41Keys()
    {
        var translations = GetTranslations();

        Assert.That(translations.Count, Is.EqualTo(ExpectedTranslations.Count));

        foreach (var expected in ExpectedTranslations)
        {
            Assert.That(translations.TryGetValue(expected.Key, out var actual), Is.True, $"Missing translation key: {expected.Key}");
            Assert.That(actual, Is.EqualTo(expected.Value), $"Unexpected translation for key: {expected.Key}");
        }
    }

    [Test]
    public void TranslationCatalog_HasNoEmptyValues()
    {
        var translations = GetTranslations();

        foreach (var pair in translations)
        {
            Assert.That(pair.Value, Is.Not.Empty, $"Translation value must not be empty: {pair.Key}");
        }
    }

    [Test]
    public void Postfix_ReplacesResult_ForKnownKey()
    {
        var result = "Element_Button";

        AccessibilityLocalizationPatch.Postfix("Element_Button", ref result);

        Assert.That(result, Is.EqualTo("ボタン"));
    }

    [Test]
    public void Postfix_LeavesResultUnchanged_ForUnknownKey()
    {
        var result = "unknown-value";

        AccessibilityLocalizationPatch.Postfix("Unknown_Key", ref result);

        Assert.That(result, Is.EqualTo("unknown-value"));
    }

    private static Dictionary<string, string> GetTranslations()
    {
        var field = typeof(AccessibilityLocalizationPatch).GetField("Translations", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);

        var value = field!.GetValue(null) as Dictionary<string, string>;
        Assert.That(value, Is.Not.Null);
        return value!;
    }
}
