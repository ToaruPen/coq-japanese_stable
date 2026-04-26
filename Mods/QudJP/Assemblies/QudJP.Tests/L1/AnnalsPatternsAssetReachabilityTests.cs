using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsAssetReachabilityTests
{
    private const string AssetRelativePath = "Dictionaries/annals-patterns.ja.json";

    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    private static readonly Regex JapaneseCharRe = new(@"[぀-ヿ一-鿿]", RegexOptions.Compiled);

    [Test]
    public void ResolveAssetPath_ReturnsExistingFile()
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(AssetRelativePath);
        Assert.That(File.Exists(path), Is.True,
            $"annals-patterns.ja.json must exist at the resolved path: {path}");

        var contents = File.ReadAllText(path, System.Text.Encoding.UTF8);
        Assert.That(JapaneseCharRe.IsMatch(contents), Is.True,
            "annals-patterns.ja.json must contain at least one Japanese character (Hiragana/Katakana/Kanji)");
    }

    [Test]
    public void AnnalsPatternsFile_HasEntriesArrayAndPatternsArray()
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(AssetRelativePath);
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocumentDto));
        var document = serializer.ReadObject(stream) as JournalPatternDocumentDto;
        Assert.That(document, Is.Not.Null);
        Assert.That(document!.Entries, Is.Not.Null,
            "annals-patterns.ja.json must declare an 'entries' array");
        Assert.That(document!.Patterns, Is.Not.Null,
            "annals-patterns.ja.json must declare a 'patterns' array (may be empty if zero accepted candidates)");
        Assert.That(document!.Patterns, Has.Some.Matches<JournalPatternEntryDto>(p =>
            p.Template != null && JapaneseCharRe.IsMatch(p.Template)),
            "at least one pattern template must contain a Japanese character");
    }
}
