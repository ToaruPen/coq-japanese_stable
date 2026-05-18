using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VillageTerrainRevealDescriptionTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-village-terrain-reveal-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(Path.Combine(dictionaryDirectory, "Scoped"));

        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        WriteHistorySpiceDictionary(
            ("people", "人々"),
            ("gather", "集う"),
            ("reverence", "崇敬"),
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

    [Test]
    public void TryTranslateRevealedDescription_TranslatesDescriptionShort_WhenOwnerMatches()
    {
        var target = new DummyVillageTerrainPart
        {
            ParentObject = new DummyVillageTerrainObject
            {
                DescriptionPart = new DummyVillageTerrainDescription
                {
                    Short = "Over the flats, people gather in reverence of the spindle.",
                },
            },
        };

        var ok = VillageTerrainRevealDescriptionTranslationPatch.TryTranslateRevealedDescriptionForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(target.ParentObject.DescriptionPart.Short, Is.EqualTo("平地の上で、人々がスピンドルを崇敬して集う。"));
            Assert.That(HitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void TryTranslateRevealedDescription_LeavesUnknownDescriptionShort()
    {
        var target = new DummyVillageTerrainPart
        {
            ParentObject = new DummyVillageTerrainObject
            {
                DescriptionPart = new DummyVillageTerrainDescription
                {
                    Short = "A village location with no generated terrain description.",
                },
            },
        };

        var ok = VillageTerrainRevealDescriptionTranslationPatch.TryTranslateRevealedDescriptionForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(target.ParentObject.DescriptionPart.Short, Is.EqualTo("A village location with no generated terrain description."));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void Postfix_TranslatesOnlySuccessfulVillageRevealEvent()
    {
        var target = new DummyVillageTerrainPart
        {
            ParentObject = new DummyVillageTerrainObject
            {
                DescriptionPart = new DummyVillageTerrainDescription
                {
                    Short = "Over the flats, people gather in reverence of the spindle.",
                },
            },
        };

        VillageTerrainRevealDescriptionTranslationPatch.Postfix(target, new DummyVillageTerrainEvent { ID = "Inspect" }, true);
        Assert.That(target.ParentObject.DescriptionPart.Short, Is.EqualTo("Over the flats, people gather in reverence of the spindle."));

        VillageTerrainRevealDescriptionTranslationPatch.Postfix(target, new DummyVillageTerrainEvent { ID = "VillageReveal" }, false);
        Assert.That(target.ParentObject.DescriptionPart.Short, Is.EqualTo("Over the flats, people gather in reverence of the spindle."));

        VillageTerrainRevealDescriptionTranslationPatch.Postfix(target, new DummyVillageTerrainEvent { ID = "VillageReveal" }, true);

        Assert.Multiple(() =>
        {
            Assert.That(target.ParentObject.DescriptionPart.Short, Is.EqualTo("平地の上で、人々がスピンドルを崇敬して集う。"));
            Assert.That(HitCount(), Is.EqualTo(1));
        });
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(VillageTerrainRevealDescriptionTranslationPatch),
            nameof(VillageTerrainRevealDescriptionTranslationPatch) + ".DescriptionShort");
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

internal sealed class DummyVillageTerrainPart
{
    public DummyVillageTerrainObject ParentObject { get; set; } = new();
}

internal sealed class DummyVillageTerrainObject
{
    public DummyVillageTerrainDescription DescriptionPart { get; set; } = new();
}

internal sealed class DummyVillageTerrainDescription
{
    public string Short { get; set; } = string.Empty;
}

internal sealed class DummyVillageTerrainEvent
{
    public string ID { get; set; } = string.Empty;
}
