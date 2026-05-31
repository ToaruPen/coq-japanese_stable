using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GeneratedDisplayNameOwnerTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-generated-display-name-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        StatusScreenPopupTranslationPatch.ResetForTests();
        WriteDictionaryFile(
            "ui-displayname.ja.json",
            ("Joppa", "ジョッパ"),
            ("Resheph", "レシェフ"),
            ("snapjaw", "スナップジョー"),
            ("a snapjaw", "スナップジョー"),
            ("space inverter", "スペースインバーター"));
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("honeyed", "ハチミツ風味の"),
            ("bread", "パン"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));
        WriteMutationsXml(("Temporal Fugue", "時間遁走"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        StatusScreenPopupTranslationPatch.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void VillageFaction_TranslatesGeneratedDisplayNameWithoutChangingKeys()
    {
        var faction = new DummyFaction
        {
            Name = "villagers of Joppa",
            DisplayName = "Cult of the Honeyed Bread",
            FormatWithArticle = true,
        };

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateVillageFactionDisplayName(faction);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(faction.DisplayName, Is.EqualTo("ハチミツ風味のパンの教団"));
            Assert.That(faction.Name, Is.EqualTo("villagers of Joppa"));
            Assert.That(faction.FormatWithArticle, Is.True);
        });
    }

    [Test]
    public void VillageFaction_TranslatesFallbackVillagersOfDisplayName()
    {
        var faction = new DummyFaction
        {
            Name = "villagers of Joppa",
            DisplayName = "villagers of Joppa",
        };

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateVillageFactionDisplayName(faction);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(faction.DisplayName, Is.EqualTo("ジョッパの村人"));
            Assert.That(faction.Name, Is.EqualTo("villagers of Joppa"));
        });
    }

    [Test]
    public void TemporalFugueCopy_TranslatesDisplayNameAndPlayerCopyDescription()
    {
        var copy = new DummyGameObject
        {
            Render = { DisplayName = "clone of a snapjaw" },
        };
        copy.SetStringProperty("PlayerCopyDescription", "one of your Temporal Fugue clones");

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateTemporalFugueCopy(copy);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(copy.Render.DisplayName, Is.EqualTo("スナップジョーのクローン"));
            Assert.That(copy.GetStringProperty("PlayerCopyDescription"), Is.EqualTo("あなたの時間遁走のクローンの一人"));
        });
    }

    [Test]
    public void TemporalFugueCopy_TranslatesNonMutationPlayerCopyDescriptionContext()
    {
        var copy = new DummyGameObject
        {
            Render = { DisplayName = "clone of a snapjaw" },
        };
        copy.SetStringProperty("PlayerCopyDescription", "one of your space inverter clones");

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateTemporalFugueCopy(copy);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(copy.Render.DisplayName, Is.EqualTo("スナップジョーのクローン"));
            Assert.That(copy.GetStringProperty("PlayerCopyDescription"), Is.EqualTo("あなたのスペースインバーターのクローンの一人"));
        });
    }

    [Test]
    public void TemporalFugueCopy_LeavesUnknownPlayerCopyDescriptionContextUnchanged()
    {
        var copy = new DummyGameObject();
        copy.SetStringProperty("PlayerCopyDescription", "one of your unknown source clones");

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateTemporalFugueCopy(copy);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(copy.GetStringProperty("PlayerCopyDescription"), Is.EqualTo("one of your unknown source clones"));
        });
    }

    [Test]
    public void SultanMuralCells_TranslateHistoricAndRuinedMuralNames()
    {
        var historic = new DummyGameObject { DisplayName = "mural of Resheph" };
        var ruined = new DummyGameObject { DisplayName = "ruined mural of Resheph" };
        var cells = new[] { new DummyCell(historic), new DummyCell(ruined) };

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateMuralCells(cells);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(historic.DisplayName, Is.EqualTo("レシェフの壁画"));
            Assert.That(ruined.DisplayName, Is.EqualTo("レシェフの崩れた壁画"));
        });
    }

    [Test]
    public void PlayerMuralPanel_TranslatesSelectedPanelOnly()
    {
        var first = new DummyGameObject { DisplayName = "mural of Joppa" };
        var second = new DummyGameObject { DisplayName = "mural of Resheph" };
        var controller = new DummyPlayerMuralController();
        var cells = new[]
        {
            new DummyLocation2D(new DummyCell(first)),
            new DummyLocation2D(new DummyCell(second)),
        };

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslatePlayerMuralPanel(controller, cells, 1);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(first.DisplayName, Is.EqualTo("mural of Joppa"));
            Assert.That(second.DisplayName, Is.EqualTo("レシェフの壁画"));
        });
    }

    [Test]
    public void VillageDynamicQuestRewardGameObject_TranslatesRecoilerDisplayNameOnly()
    {
        var recoiler = new DummyGameObject
        {
            Render = { DisplayName = "Joppa recoiler" },
        };

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateRewardGameObject(recoiler);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(recoiler.Render.DisplayName, Is.EqualTo("ジョッパのリコイラー"));
        });
    }

    [Test]
    public void GeneratedDisplayNameHelpers_LeaveUnknownsUnchanged()
    {
        var faction = new DummyFaction { DisplayName = "unknown generated label" };

        var changed = GeneratedDisplayNameOwnerTranslationHelpers.TranslateVillageFactionDisplayName(faction);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(faction.DisplayName, Is.EqualTo("unknown generated label"));
        });
    }

    private void WriteDictionaryFile(string relativePath, params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[i].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[i].text));
            builder.Append("\"}");
        }

        builder.Append("]}\n");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ScopedDictionaryLookup.ResetForTests();
    }

    private void WriteMutationsXml(params (string name, string displayName)[] entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<mutations>");
        foreach (var (name, displayName) in entries)
        {
            builder.Append("  <mutation Name=\"");
            builder.Append(EscapeXml(name));
            builder.Append("\" DisplayName=\"");
            builder.Append(EscapeXml(displayName));
            builder.AppendLine("\" />");
        }

        builder.AppendLine("</mutations>");
        File.WriteAllText(
            Path.Combine(tempDirectory, "Mutations.jp.xml"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private sealed class DummyFaction
    {
        public string Name { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool FormatWithArticle { get; set; }
    }

    private sealed class DummyGameObject
    {
        private readonly Dictionary<string, string> stringProperties = new(StringComparer.Ordinal);

        public string DisplayName { get; set; } = string.Empty;

        public DummyRender Render { get; } = new();

        public string GetStringProperty(string name)
        {
            return stringProperties.TryGetValue(name, out var value) ? value : string.Empty;
        }

        public void SetStringProperty(string name, string value, bool silent = false)
        {
            _ = silent;
            stringProperties[name] = value;
        }
    }

    private sealed class DummyRender
    {
        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class DummyCell
    {
        private readonly object? mural;

        public DummyCell(object? mural)
        {
            this.mural = mural;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Invoked by GeneratedDisplayNameOwnerTranslationHelpers through reflection.")]
        public object? GetFirstObjectWithPart(string partName)
        {
            return string.Equals(partName, "SultanMural", StringComparison.Ordinal) ? mural : null;
        }
    }

    private sealed class DummyLocation2D
    {
        public DummyLocation2D(DummyCell cell)
        {
            Cell = cell;
        }

        public DummyCell Cell { get; }
    }

    private sealed class DummyPlayerMuralController
    {
        public DummyPlayerMuralParent ParentObject { get; } = new();
    }

    private sealed class DummyPlayerMuralParent
    {
        public DummyZone CurrentZone { get; } = new();
    }

    private sealed class DummyZone
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members",
            Justification = "Invoked by GeneratedDisplayNameOwnerTranslationHelpers through reflection.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S2325:Methods and properties that don't access instance data should be static",
            Justification = "The game API method is an instance method and the test mirrors that reflective shape.")]
        public DummyCell GetCell(DummyLocation2D location)
        {
            return location.Cell;
        }
    }
}
