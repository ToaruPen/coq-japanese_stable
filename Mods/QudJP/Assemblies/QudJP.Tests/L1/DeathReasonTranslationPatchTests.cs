using QudJP;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DeathReasonTranslationPatchTests
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "qudjp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        DynamicTextObservability.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void TranslateDeathReason_TranslatesKnownReason()
    {
        WriteDictionary(("You were vaporized.", "蒸発した。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were vaporized.");

        Assert.That(result, Does.Contain("蒸発した。"));
    }

    [Test]
    public void TranslateDeathReason_PreservesUnknownReason()
    {
        var result = DeathReasonTranslationPatch.TranslateDeathReason("Some unknown death reason.");

        Assert.That(result, Is.EqualTo("Some unknown death reason."));
    }

    [Test]
    public void TranslateDeathReason_PreservesDirectTranslationMarker()
    {
        WriteDictionary(("You were vaporized.", "蒸発した。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("\u0001Already translated");

        Assert.That(result, Is.EqualTo("Already translated"),
            "DirectTranslationMarker should be stripped and text returned as-is.");
    }

    [Test]
    public void TranslateDeathReason_TranslatesSteppedOn()
    {
        WriteDictionary(("You were stepped on.", "踏みつぶされた。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were stepped on.");

        Assert.That(result, Does.Contain("踏みつぶされた。"));
    }

    [Test]
    public void TranslateDeathReason_TranslatesCrushedByFallingRocks()
    {
        WriteDictionary(("You were crushed by falling rocks.", "落石に押しつぶされた。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were crushed by falling rocks.");

        Assert.That(result, Does.Contain("落石に押しつぶされた。"));
    }

    [Test]
    public void TranslateDeathReason_TranslatesResorbedIntoMassMind()
    {
        WriteDictionary(("You were resorbed into the Mass Mind.", "集合精神に再吸収された。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were resorbed into the Mass Mind.");

        Assert.That(result, Does.Contain("集合精神に再吸収された。"));
    }

    [Test]
    public void TranslateDeathReason_MarksTranslatedResult()
    {
        WriteDictionary(("You were vaporized.", "蒸発した。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were vaporized.");

        Assert.That(result[0], Is.EqualTo('\x01'),
            "Translated death reason should be marked with DirectTranslationMarker to prevent double-translation.");
    }

    [Test]
    public void TranslateDeathReason_EmptyStringReturnsEmpty()
    {
        var result = DeathReasonTranslationPatch.TranslateDeathReason("");

        Assert.That(result, Is.EqualTo(""));
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
}
