using System.IO;
using System.Runtime.Serialization.Json;

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

    [Test]
    public void ResolveAssetPath_ReturnsExistingFile()
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(AssetRelativePath);
        Assert.That(File.Exists(path), Is.True,
            $"annals-patterns.ja.json must exist at the resolved path: {path}");
    }

    [Test]
    public void AnnalsPatternsFile_HasEntriesArrayAndPatternsArray()
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(AssetRelativePath);
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocumentDto));
        var document = serializer.ReadObject(stream) as JournalPatternDocumentDto;
        Assert.That(document, Is.Not.Null);
        Assert.That(document!.Patterns, Is.Not.Null,
            "annals-patterns.ja.json must declare a 'patterns' array (may be empty if zero accepted candidates)");
    }
}
