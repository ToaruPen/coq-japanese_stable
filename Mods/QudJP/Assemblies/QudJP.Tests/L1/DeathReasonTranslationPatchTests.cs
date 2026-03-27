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
        SinkObservation.ResetForTests();
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void TranslateDeathReason_ObservationOnly_LeavesKnownReasonUnchanged()
    {
        WriteDictionary(("You were vaporized.", "蒸発した。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were vaporized.");

        Assert.That(result, Is.EqualTo("You were vaporized."));
    }

    [Test]
    public void TranslateDeathReason_ObservationOnly_LogsUnclaimedReason()
    {
        const string source = "You were vaporized.";

        var result = DeathReasonTranslationPatch.TranslateDeathReason(source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(source));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(DeathReasonTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    source,
                    source),
                Is.GreaterThan(0));
        });
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
    public void TranslateDeathReason_ObservationOnly_LeavesSteppedOnUnchanged()
    {
        WriteDictionary(("You were stepped on.", "踏みつぶされた。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were stepped on.");

        Assert.That(result, Is.EqualTo("You were stepped on."));
    }

    [Test]
    public void TranslateDeathReason_ObservationOnly_LeavesCrushedByFallingRocksUnchanged()
    {
        WriteDictionary(("You were crushed by falling rocks.", "落石に押しつぶされた。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were crushed by falling rocks.");

        Assert.That(result, Is.EqualTo("You were crushed by falling rocks."));
    }

    [Test]
    public void TranslateDeathReason_ObservationOnly_LeavesResorbedIntoMassMindUnchanged()
    {
        WriteDictionary(("You were resorbed into the Mass Mind.", "集合精神に再吸収された。"));
        Translator.SetDictionaryDirectoryForTests(tempDir);

        var result = DeathReasonTranslationPatch.TranslateDeathReason("You were resorbed into the Mass Mind.");

        Assert.That(result, Is.EqualTo("You were resorbed into the Mass Mind."));
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
