namespace QudJP.Tests.L1;

public sealed class ColorTranslationCase
{
    internal ColorTranslationCase(string source, string expected, string context, params (string key, string text)[] entries)
    {
        Source = source;
        Expected = expected;
        Context = context;
        Entries = entries;
    }

    internal string Source { get; }

    internal string Expected { get; }

    internal string Context { get; }

    internal IReadOnlyList<(string key, string text)> Entries { get; }
}

internal static class ColorRouteInvariantCases
{
    internal static IEnumerable<TestCaseData> UiTextSkinCases()
    {
        yield return new TestCaseData(
                new ColorTranslationCase(
                    "{{W|Hello}}",
                    "{{W|こんにちは}}",
                    nameof(QudJP.Patches.UITextSkinTranslationPatch),
                    ("Hello", "こんにちは")))
            .SetName("UITextSkin_QudWrapper");

        yield return new TestCaseData(
                new ColorTranslationCase(
                    "<color=#44ff88>Hello</color>",
                    "<color=#44ff88>こんにちは</color>",
                    nameof(QudJP.Patches.UITextSkinTranslationPatch),
                    ("Hello", "こんにちは")))
            .SetName("UITextSkin_TmpWrapper");
    }

    internal static IEnumerable<TestCaseData> PopupMenuItemCases()
    {
        yield return new TestCaseData(
                new ColorTranslationCase(
                    "{{W|[Esc]}} {{y|Cancel}}",
                    "{{W|[Esc]}} {{y|キャンセル}}",
                    nameof(QudJP.Patches.PopupTranslationPatch),
                    ("Cancel", "キャンセル")))
            .SetName("Popup_HotkeyLabelMarkup");
    }
}
