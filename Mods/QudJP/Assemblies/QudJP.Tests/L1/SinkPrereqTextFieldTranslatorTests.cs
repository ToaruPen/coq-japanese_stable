using QudJP;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class SinkPrereqTextFieldTranslatorTests
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "qudjp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        SinkPrereqTextFieldTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkPrereqTextFieldTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void TranslateField_ObservationOnly_LeavesTextSkinFieldUnchanged()
    {
        WriteDictionary(("Keybinds", "キーバインド"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var target = new DummyLeftSideCategory();
        target.text.SetText("Keybinds");

        SinkPrereqTextFieldTranslator.TranslateField(target, "text", "Test");

        Assert.Multiple(() =>
        {
            Assert.That(target.text.text, Is.EqualTo("Keybinds"));
            Assert.That(SinkObservation.GetHitCountForTests(
                nameof(UITextSkinTranslationPatch),
                "Test",
                SinkObservation.ObservationOnlyDetail,
                "Keybinds",
                "Keybinds"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateField_ObservationOnly_LeavesPropertyBackedTextSkinUnchanged()
    {
        WriteDictionary(("Keybinds", "キーバインド"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var target = new DummyPropertyBackedLeftSideCategory();
        target.text.SetText("Keybinds");

        SinkPrereqTextFieldTranslator.TranslateField(target, "text", "Test");

        Assert.Multiple(() =>
        {
            Assert.That(target.text.text, Is.EqualTo("Keybinds"));
            Assert.That(SinkObservation.GetHitCountForTests(
                nameof(UITextSkinTranslationPatch),
                "Test",
                SinkObservation.ObservationOnlyDetail,
                "Keybinds",
                "Keybinds"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateField_SkipsNullInstance()
    {
        Assert.DoesNotThrow(() =>
            SinkPrereqTextFieldTranslator.TranslateField(null, "text", "Test"));
    }

    [Test]
    public void TranslateField_SkipsUnknownFieldName()
    {
        var target = new DummyLeftSideCategory();
        target.text.SetText("something");

        Assert.DoesNotThrow(() =>
            SinkPrereqTextFieldTranslator.TranslateField(target, "nonExistent", "Test"));
    }

    [Test]
    public void TranslateField_SkipsEmptyText()
    {
        var target = new DummyLeftSideCategory();
        target.text.SetText("");

        SinkPrereqTextFieldTranslator.TranslateField(target, "text", "Test");

        Assert.That(target.text.text, Is.EqualTo(""));
    }

    [Test]
    public void TranslateField_PreservesAlreadyTranslatedText()
    {
        var target = new DummyLeftSideCategory();
        target.text.SetText("既に日本語");

        SinkPrereqTextFieldTranslator.TranslateField(target, "text", "Test");

        Assert.That(target.text.text, Is.EqualTo("既に日本語"));
    }

    [Test]
    public void TranslateTextSkin_ObservationOnly_LeavesTextUnchanged()
    {
        WriteDictionary(("Options", "設定"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var skin = new DummyUITextSkinField();
        skin.SetText("Options");

        SinkPrereqTextFieldTranslator.TranslateTextSkin(skin, "Test");

        Assert.Multiple(() =>
        {
            Assert.That(skin.text, Is.EqualTo("Options"));
            Assert.That(SinkObservation.GetHitCountForTests(
                nameof(UITextSkinTranslationPatch),
                "Test",
                SinkObservation.ObservationOnlyDetail,
                "Options",
                "Options"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateTextSkin_SkipsNull()
    {
        Assert.DoesNotThrow(() =>
            SinkPrereqTextFieldTranslator.TranslateTextSkin(null, "Test"));
    }

    [Test]
    public void TranslateChainedField_ObservationOnly_LeavesNestedFieldUnchanged()
    {
        WriteDictionary(("Build Mode", "ビルドモード"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var parent = new DummyParentWithChild();
        parent.child.text.SetText("Build Mode");

        SinkPrereqTextFieldTranslator.TranslateChainedField(
            parent, "child", "text", "Test");

        Assert.Multiple(() =>
        {
            Assert.That(parent.child.text.text, Is.EqualTo("Build Mode"));
            Assert.That(SinkObservation.GetHitCountForTests(
                nameof(UITextSkinTranslationPatch),
                "Test",
                SinkObservation.ObservationOnlyDetail,
                "Build Mode",
                "Build Mode"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateChainedField_SkipsNullParent()
    {
        Assert.DoesNotThrow(() =>
            SinkPrereqTextFieldTranslator.TranslateChainedField(null, "child", "text", "Test"));
    }

    [Test]
    public void TranslateChainedField_SkipsNullChild()
    {
        var parent = new DummyParentWithNullChild();
        Assert.DoesNotThrow(() =>
            SinkPrereqTextFieldTranslator.TranslateChainedField(parent, "child", "text", "Test"));
    }

    [Test]
    public void TranslateTextSkin_PreservesDirectTranslationMarker()
    {
        WriteDictionary(("Options", "設定"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var skin = new DummyUITextSkinField();
        skin.SetText("\x01Options");

        SinkPrereqTextFieldTranslator.TranslateTextSkin(skin, "Test");

        Assert.That(skin.text, Is.Not.EqualTo("設定"),
            "Text prefixed with \\x01 DirectTranslationMarker must not be dictionary-translated.");
        Assert.That(skin.text, Does.Not.Contain("設定"),
            "Marker-prefixed text must bypass translation entirely.");
        Assert.That(skin.text, Is.EqualTo("Options"),
            "Text prefixed with \\x01 DirectTranslationMarker should be returned without the marker.");
    }

    [Test]
    public void TranslateField_PreservesDirectTranslationMarker()
    {
        WriteDictionary(("Keybinds", "キーバインド"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var target = new DummyLeftSideCategory();
        target.text.SetText("\x01Keybinds");

        SinkPrereqTextFieldTranslator.TranslateField(target, "text", "Test");

        Assert.That(target.text.text, Does.Not.Contain("キーバインド"),
            "Field text prefixed with \\x01 DirectTranslationMarker must not be dictionary-translated.");
        Assert.That(target.text.text, Is.EqualTo("Keybinds"),
            "Field text prefixed with \\x01 DirectTranslationMarker should be returned without the marker.");
    }

    [Test]
    public void TranslateChainedField_PreservesDirectTranslationMarker()
    {
        WriteDictionary(("Build Mode", "ビルドモード"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var parent = new DummyParentWithChild();
        parent.child.text.SetText("\u0001Build Mode");

        SinkPrereqTextFieldTranslator.TranslateChainedField(
            parent, "child", "text", "Test");

        Assert.That(parent.child.text.text, Does.Not.Contain("ビルドモード"),
            "Chained field text prefixed with \\x01 DirectTranslationMarker must not be dictionary-translated.");
        Assert.That(parent.child.text.text, Is.EqualTo("Build Mode"),
            "Chained field text prefixed with \\x01 DirectTranslationMarker should be returned without the marker.");
    }

    private void WriteDictionary(params (string Key, string Text)[] entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"entries\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"key\":\"");
            sb.Append(entries[i].Key.Replace("\"", "\\\""));
            sb.Append("\",\"text\":\"");
            sb.Append(entries[i].Text.Replace("\"", "\\\""));
            sb.Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(Path.Combine(tempDir, "test.ja.json"), sb.ToString());
    }

    internal sealed class DummyParentWithChild
    {
        public DummyLeftSideCategory child = new DummyLeftSideCategory();
    }

    internal sealed class DummyParentWithNullChild
    {
#pragma warning disable CS0649
        public DummyLeftSideCategory? child;
#pragma warning restore CS0649
    }

    internal sealed class DummyPropertyBackedLeftSideCategory
    {
        public DummyUITextSkinField text { get; } = new DummyUITextSkinField();
    }
}
