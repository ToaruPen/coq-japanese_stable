namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessageQueueSemanticPipelineColorContractTests
{
    [Test]
    public void RepresentativeQueuedPatternTranslation_PreservesColorTagsInJapaneseOutput()
    {
        var dictionaryPath = Path.Combine(
            FindRepositoryRoot().FullName,
            "Mods/QudJP/Localization/Dictionaries/messages.ja.json");
        MessagePatternTranslator.SetPatternFileForTests(dictionaryPath);

        try
        {
            var translated = MessagePatternTranslator.Translate("You gain {{C|75}} XP!");

            Assert.That(translated, Is.EqualTo("あなたは経験値を{{C|75}}獲得した"));
        }
        finally
        {
            MessagePatternTranslator.ResetForTests();
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
