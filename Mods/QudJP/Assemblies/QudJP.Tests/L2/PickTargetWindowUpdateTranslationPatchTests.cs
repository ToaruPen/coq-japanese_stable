using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PickTargetWindowUpdateTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-pick-target-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyPickTargetWindow.currentText = string.Empty;
        DummyPickTargetWindow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateCurrentText_TranslatesGeneratedPickTargetCommandBar()
    {
        WriteDictionary(
            ("Dig to where?", "どこまで掘る？"),
            ("select", "決定"),
            ("unlock", "固定解除"));
        DummyPickTargetWindow.currentText = "Dig to where? | {{W|space}}-select | unlock ({{hotkey|F1}}))";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo("どこまで掘る？ | {{W|space}}-決定 | 固定解除 ({{hotkey|F1}}))"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickTargetWindowUpdateTranslationPatch),
                    "PickTarget.CommandBar"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateCurrentText_TranslatesObservedLookCommandBarAtOwnerRoute()
    {
        WriteDictionary(
            ("Look", "見る"),
            ("lock", "ロック"),
            ("interact", "操作する"),
            ("walk", "歩く"));
        DummyPickTargetWindow.currentText = "Look | {{W|ESC}} | {{hotkey|({{hotkey|F1}})}} {{W|l}}ock | {{hotkey|space}} interact | {{hotkey|{{hotkey|W}}}} walk";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(
                DummyPickTargetWindow.currentText,
                Is.EqualTo("見る | {{W|ESC}} | {{hotkey|({{hotkey|F1}})}} {{W|ロ}}ック | {{hotkey|space}} 操作する | {{hotkey|{{hotkey|W}}}} 歩く"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickTargetWindowUpdateTranslationPatch),
                    "PickTarget.CommandBar"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Prefix_TranslatesGeneratedPickTargetCommandBar_WhenPatched()
    {
        WriteDictionary(
            ("Dig to where?", "どこまで掘る？"),
            ("select", "決定"));
        DummyPickTargetWindow.currentText = "Dig to where? | {{W|space}}-select";

        RunWithPickTargetWindowUpdatePatch(() => DummyPickTargetWindow.Update());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo("どこまで掘る？ | {{W|space}}-決定"));
            Assert.That(DummyPickTargetWindow.UpdateCallCount, Is.EqualTo(1));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickTargetWindowUpdateTranslationPatch),
                    "PickTarget.CommandBar"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateCurrentText_PreservesColorMarkupInMarkupWrappedHotkeyLabel()
    {
        WriteDictionary(
            ("Dig to where?", "どこまで掘る？"),
            ("select", "決定"));
        DummyPickTargetWindow.currentText = "Dig to where? | {{W|space}}-{{G|select}}";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo("どこまで掘る？ | {{W|space}}-{{G|決定}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickTargetWindowUpdateTranslationPatch),
                    "PickTarget.CommandBar"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateCurrentText_TranslatesObservedMissilePickerUnlockSegment()
    {
        WriteDictionary(("select", "選択"));
        DummyPickTargetWindow.currentText = "{{W|space}}-select | unlock ({{hotkey|F1}}) {{hotkey|M}} - mark target [{{W|}}] Menu | Fire Missile Weapon";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(
                DummyPickTargetWindow.currentText,
                Is.EqualTo("{{W|space}}-選択 | ロック解除 ({{hotkey|F1}}) {{hotkey|M}} - 対象をマーク [{{W|}}] メニュー | 飛び道具を射撃"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickTargetWindowUpdateTranslationPatch),
                    "PickTarget.CommandBar"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateCurrentText_TranslatesExactPickTargetLabel()
    {
        WriteDictionary(("Dig to where?", "どこまで掘る？"));
        DummyPickTargetWindow.currentText = "Dig to where?";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo("どこまで掘る？"));
        });
    }

    [Test]
    public void TranslateCurrentText_TranslatesTonicApplyDirectionPrompt()
    {
        DummyPickTargetWindow.currentText = "Apply {{Y|salve tonic}}";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo("{{Y|salve tonic}}を使用"));
        });
    }

    [Test]
    public void TranslateCurrentText_FallbackToEnglish_WhenExactLabelMissing()
    {
        WriteDictionary(("select", "決定"));
        DummyPickTargetWindow.currentText = "Dig to where?";

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo("Dig to where?"));
        });
    }

    [Test]
    public void TranslateCurrentText_ReturnsFalse_WhenCurrentTextIsEmpty()
    {
        DummyPickTargetWindow.currentText = string.Empty;

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(DummyPickTargetWindow.currentText, Is.Empty);
        });
    }

    [TestCase("\u0001Dig to where?", "\u0001どこまで掘る？")]
    [TestCase("<color=red>Dig to where?</color>", "<color=red>どこまで掘る？</color>")]
    public void TranslateCurrentText_TranslatesExactLabelPreservingMarkerAndColor(string source, string expected)
    {
        WriteDictionary(("Dig to where?", "どこまで掘る？"));
        DummyPickTargetWindow.currentText = source;

        var changed = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(DummyPickTargetWindow.currentText, Is.EqualTo(expected));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickTargetWindowUpdateTranslationPatch),
                    "PickTarget.ExactLookup"),
                Is.GreaterThan(0));
        });
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"")
                .Append(EscapeJson(entries[index].key))
                .Append("\",\"text\":\"")
                .Append(EscapeJson(entries[index].text))
                .Append("\"}");
        }

        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "ui-pick-target.ja.json"), builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static void RunWithPickTargetWindowUpdatePatch(Action action)
    {
        var harmonyId = $"qudjp.tests.picktarget.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(DummyPickTargetWindow), nameof(DummyPickTargetWindow.Update)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(PickTargetWindowUpdateTranslationPatchTests), nameof(PrefixDummyPickTargetWindowUpdate))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PrefixDummyPickTargetWindowUpdate()
    {
        _ = PickTargetWindowUpdateTranslationPatch.TranslateCurrentTextForTests(typeof(DummyPickTargetWindow));
    }

    private static class DummyPickTargetWindow
    {
        public static string currentText = string.Empty;

        public static int UpdateCallCount { get; private set; }

        public static void Update()
        {
            UpdateCallCount++;
        }

        public static void Reset()
        {
            currentText = string.Empty;
            UpdateCallCount = 0;
        }
    }
}
