using QudJP;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DeathReasonTranslationPatchTests
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "qudjp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void TranslateDeathReason_TranslatesKnownReason()
    {
        const string source = "You were vaporized.";
        WriteDictionary((source, "蒸発した。"));

        var result = DeathReasonTranslationPatch.TranslateDeathReason(source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("蒸発した。"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(DeathReasonTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    source,
                    source),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateDeathReason_PreservesColorCodes()
    {
        WriteDictionary(("You were stepped on.", "踏みつぶされた。"));

        var result = DeathReasonTranslationPatch.TranslateDeathReason("{{R|You were stepped on.}}");

        Assert.That(result, Is.EqualTo("{{R|踏みつぶされた。}}"));
    }

    [Test]
    public void TranslateDeathReason_UsesLowerAsciiFallback()
    {
        WriteDictionary(("you were stepped on.", "踏みつぶされた。"));

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were stepped on.");

        Assert.That(result, Is.EqualTo("踏みつぶされた。"));
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
        var result = DeathReasonTranslationPatch.TranslateDeathReason("Already translated");

        Assert.That(result, Is.EqualTo("Already translated"),
            "DirectTranslationMarker should be stripped and text returned as-is.");
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
