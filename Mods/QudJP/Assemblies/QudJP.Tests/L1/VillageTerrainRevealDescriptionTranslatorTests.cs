using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class VillageTerrainRevealDescriptionTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-village-terrain-reveal-l1", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(Path.Combine(dictionaryDirectory, "Scoped"));

        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        WriteHistorySpiceDictionary(
            ("people", "人々"),
            ("folk", "民"),
            ("kin", "血縁"),
            ("gather", "集う"),
            ("come together", "集まる"),
            ("reverence", "崇敬"),
            ("flock", "群れ"),
            ("the spindle", "スピンドル"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "Over the flats, people gather in reverence of the spindle.",
        "平地の上で、人々がスピンドルを崇敬して集う。")]
    [TestCase(
        "People gather over the flats in reverence of {{Y|the spindle}}.",
        "人々が{{Y|スピンドル}}を崇敬して平地の上で集う。")]
    [TestCase(
        "Buried under the crescent dunes, folk come together to mock {{R|the Glow}}.",
        "三日月砂丘の下に埋もれて、民が{{R|輝き}}を嘲るために集まる。")]
    [TestCase(
        "Folk come together buried under the crescent dunes to mock the Glow.",
        "民が輝きを嘲るために三日月砂丘の下に埋もれて集まる。")]
    [TestCase(
        "Shrouded in motes of fireflies, there's a flock of {{W|the Mechanimists}} and their kin.",
        "蛍の微光に包まれて、{{W|メカニマス教団}}とその同胞の一団がいる。")]
    [TestCase(
        "There's a flock of the Mechanimists and their kin shrouded in motes of fireflies.",
        "蛍の微光に包まれて、メカニマス教団とその同胞の一団がいる。")]
    public void TryTranslate_TranslatesVillageRevealDescriptionFrame(string source, string expected)
    {
        var ok = VillageTerrainRevealDescriptionTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_TranslatesCombinedTerrainFragments()
    {
        var ok = VillageTerrainRevealDescriptionTranslator.TryTranslate(
            "Over the flats and under a chrome arch, people gather in reverence of the spindle.",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("平地の上とクロムのアーチの下で、人々がスピンドルを崇敬して集う。"));
        });
    }

    [Test]
    public void TryTranslate_RestoresInlineMarkupOnCombinedTerrainFragments()
    {
        var ok = VillageTerrainRevealDescriptionTranslator.TryTranslate(
            "{{g|over the flats}} and {{Y|under a chrome arch}}, people gather in reverence of the spindle.",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{g|平地の上}}と{{Y|クロムのアーチの下で}}、人々がスピンドルを崇敬して集う。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorBoundary()
    {
        var ok = VillageTerrainRevealDescriptionTranslator.TryTranslate(
            "{{C|Over the flats, people gather in reverence of the spindle.}}",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{C|平地の上で、人々がスピンドルを崇敬して集う。}}"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("A village location with no generated terrain description.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var ok = VillageTerrainRevealDescriptionTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = VillageTerrainRevealDescriptionTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "Over the flats, people gather in reverence of the spindle.",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("Over the flats, people gather in reverence of the spindle."));
        });
    }

    private void WriteHistorySpiceDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "Scoped", "historyspice-common.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
