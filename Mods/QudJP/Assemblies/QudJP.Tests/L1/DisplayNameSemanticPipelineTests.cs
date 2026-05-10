using System.Reflection;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DisplayNameSemanticPipelineTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-display-name-pipeline-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
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
    public void TryTranslateResult_UpdatesTranslatedDisplayName()
    {
        WriteDictionary(("phase cannon", "フェーズキャノン"));
        var result = "phase cannon";

        var changed = DisplayNameSemanticPipeline.TryTranslateResult(ref result, nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(result, Is.EqualTo("フェーズキャノン"));
        });
    }

    [Test]
    public void TryTranslateResult_LeavesUnknownDisplayNameUnchanged()
    {
        var result = "unknown display name";

        var changed = DisplayNameSemanticPipeline.TryTranslateResult(ref result, nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(result, Is.EqualTo("unknown display name"));
        });
    }

    [Test]
    public void TryTranslateResult_LeavesEmptyDisplayNameUnchanged()
    {
        var result = string.Empty;

        var changed = DisplayNameSemanticPipeline.TryTranslateResult(ref result, nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslateResult_PreservesColorTags()
    {
        WriteDictionary(("phase cannon", "フェーズキャノン"));
        var result = "{{W|phase cannon}}";

        var changed = DisplayNameSemanticPipeline.TryTranslateResult(ref result, nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(result, Is.EqualTo("{{W|フェーズキャノン}}"));
        });
    }

    [Test]
    public void TryTranslateResult_UsesExactLookupForControlMarkerDisplayName()
    {
        WriteDictionary(("phase \u0001 cannon", "制御マーカー付きフェーズキャノン"));
        var result = "phase \u0001 cannon";

        var changed = DisplayNameSemanticPipeline.TryTranslateResult(ref result, nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(result, Is.EqualTo("制御マーカー付きフェーズキャノン"));
        });
    }

    [Test]
    public void TryTranslateResult_ComposesMethodContext()
    {
        WriteDictionary(("phase cannon", "フェーズキャノン"));
        var result = "phase cannon";
        var method = typeof(DisplayNameSemanticPipelineTests).GetMethod(
            nameof(TryTranslateResult_ComposesMethodContext),
            BindingFlags.Instance | BindingFlags.Public)!;
        var context = DisplayNameSemanticPipeline.ComposeMethodContext(method, nameof(InventoryLocalizationPatch));

        var changed = DisplayNameSemanticPipeline.TryTranslateResult(ref result, method, nameof(InventoryLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(result, Is.EqualTo("フェーズキャノン"));
            Assert.That(
                context,
                Is.EqualTo(
                    "InventoryLocalizationPatch > method=DisplayNameSemanticPipelineTests.TryTranslateResult_ComposesMethodContext"));
        });
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(entries[index].key);
            builder.Append("\",\"text\":\"");
            builder.Append(entries[index].text);
            builder.Append("\"}");
        }

        builder.Append("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "display-name.ja.json"), builder.ToString());
    }
}
