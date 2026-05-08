namespace QudJP.Tests.L1;

using Newtonsoft.Json;

[TestFixture]
[Category("L1")]
public sealed class RuntimeJsonLoaderTests
{
    [Test]
    public void RuntimeJsonAssets_DoNotUseDataContractJsonSerializer()
    {
        var sourceRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Assemblies", "src");
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(Path.Combine("Translation", "JsonAssetLoader.cs"), StringComparison.Ordinal))
            .ToArray();

        var offenders = files
            .Where(static path => File.ReadAllText(path).Contains("DataContractJsonSerializer", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            offenders,
            Is.Empty,
            "Runtime JSON assets must use the Newtonsoft-backed JsonAssetLoader for Linux Unity/Mono compatibility.");
    }

    [Test]
    public void JsonAssetLoader_LoadsRepositoryDictionaryEntries()
    {
        var dictionaryPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "ui-default.ja.json");

        var document = JsonAssetLoader.LoadFromFile<Newtonsoft.Json.Linq.JObject>(dictionaryPath);
        var entries = document["entries"] as Newtonsoft.Json.Linq.JArray;

        Assert.Multiple(() =>
        {
            Assert.That(entries, Is.Not.Null);
            Assert.That(entries, Has.Count.GreaterThan(0));
            Assert.That(
                entries!.Any(static entry => (string?)entry["key"] == "Inventory" && (string?)entry["text"] == "インベントリ"),
                Is.True);
        });
    }

    [Test]
    [NonParallelizable]
    public void JsonAssetLoader_IgnoresGlobalNewtonsoftDefaultSettings()
    {
        var previousDefaultSettings = JsonConvert.DefaultSettings;
        var tempFile = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{TestContext.CurrentContext.Test.ID}.json");
        File.WriteAllText(tempFile, """{"entries":[{"key":"Inventory","text":"インベントリ"}],"unexpected":true}""");

        try
        {
            JsonConvert.DefaultSettings = static () => new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
            };

            var document = JsonAssetLoader.LoadFromFile<TestDictionaryDocument>(tempFile);

            Assert.Multiple(() =>
            {
                Assert.That(document.Entries, Is.Not.Null);
                Assert.That(document.Entries, Has.Count.EqualTo(1));
                Assert.That(document.Entries![0].Key, Is.EqualTo("Inventory"));
                Assert.That(document.Entries[0].Text, Is.EqualTo("インベントリ"));
            });
        }
        finally
        {
            JsonConvert.DefaultSettings = previousDefaultSettings;
            File.Delete(tempFile);
        }
    }

    [System.Runtime.Serialization.DataContract]
    private sealed class TestDictionaryDocument
    {
        [System.Runtime.Serialization.DataMember(Name = "entries")]
        public List<TestDictionaryEntry>? Entries { get; set; }
    }

    [System.Runtime.Serialization.DataContract]
    private sealed class TestDictionaryEntry
    {
        [System.Runtime.Serialization.DataMember(Name = "key")]
        public string? Key { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "text")]
        public string? Text { get; set; }
    }
}
