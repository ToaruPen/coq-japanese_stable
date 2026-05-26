using System.Text;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CharGenMenuOptionOwnerTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-chargen-menu-option-owner-l2", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void SummaryMenuBarPostfix_TranslatesOwnerYieldedDescriptions()
    {
        WriteDictionary(
            ("Re-Randomize Selections", "選択を再ランダム化"),
            ("Export Code to Clipboard", "コードをクリップボードにコピー"),
            ("Save Build To Library", "ビルドをライブラリに保存"));
        var options = new List<DummyCharGenMenuOption>
        {
            new() { Description = "Re-Randomize Selections", InputCommand = "CmdChargenRandom", KeyDescription = "R" },
            new() { Description = "Export Code to Clipboard", InputCommand = "CmdExportCode", KeyDescription = "None" },
            new() { Description = "Save Build To Library", InputCommand = "CmdSaveBuildToLibrary", KeyDescription = "None" },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(options).Cast<DummyCharGenMenuOption>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Description, Is.EqualTo("選択を再ランダム化"));
            Assert.That(translated[1].Description, Is.EqualTo("コードをクリップボードにコピー"));
            Assert.That(translated[2].Description, Is.EqualTo("ビルドをライブラリに保存"));
            Assert.That(translated[0].InputCommand, Is.EqualTo("CmdChargenRandom"));
            Assert.That(translated[0].KeyDescription, Is.EqualTo("R"));
        });
    }

    [Test]
    public void MutationsMenuBarPostfix_TranslatesStructuredPointsAndVariantAction()
    {
        WriteDictionary(
            ("Points Remaining:", "残りポイント:"),
            ("Choose Variant", "変種を選択"));
        var options = new List<DummyCharGenMenuOption>
        {
            new() { Description = "{{R|Points Remaining: -2}}", InputCommand = string.Empty },
            new() { Description = "Choose Variant", InputCommand = "CmdChargenMutationVariant" },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(options).Cast<DummyCharGenMenuOption>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Description, Is.EqualTo("{{R|残りポイント: -2}}"));
            Assert.That(translated[1].Description, Is.EqualTo("変種を選択"));
            Assert.That(translated[1].InputCommand, Is.EqualTo("CmdChargenMutationVariant"));
        });
    }

    [Test]
    public void AttributesMenuBarPostfix_TranslatesStructuredPointsRemainingDescription()
    {
        WriteDictionary(("Points Remaining:", "残りポイント:"));
        var options = new List<DummyCharGenMenuOption>
        {
            new() { Description = "{{y|Points Remaining: 4}}", InputCommand = string.Empty, KeyDescription = null },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(options).Cast<DummyCharGenMenuOption>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Description, Is.EqualTo("{{y|残りポイント: 4}}"));
            Assert.That(translated[0].InputCommand, Is.EqualTo(string.Empty));
            Assert.That(translated[0].KeyDescription, Is.Null);
        });
    }

    [Test]
    public void BuildLibrarySelectionsPostfix_TranslatesStaticAddBuildTitle()
    {
        WriteDictionary(("Add a new build code", "新しいビルドコードを追加"));
        var selections = new List<DummyChoiceWithColorIcon>
        {
            new() { Title = "Existing User Build", Description = string.Empty },
            new() { Title = "Add a new build code", Description = string.Empty },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateChoiceTitlesForTests(selections).Cast<DummyChoiceWithColorIcon>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Title, Is.EqualTo("Existing User Build"));
            Assert.That(translated[0].Description, Is.EqualTo(string.Empty));
            Assert.That(translated[1].Title, Is.EqualTo("新しいビルドコードを追加"));
            Assert.That(translated[1].Description, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void GamemodeSelectionsPostfix_TranslatesTitleAndDescription()
    {
        WriteDictionary(
            ("Classic", "クラシック"),
            ("Explore Qud in the year 1000.", "西暦1000年のクドを探索する。"));
        var selections = new List<DummyChoiceWithColorIcon>
        {
            new()
            {
                Title = "Classic",
                Description = "Explore Qud in the year 1000.",
            },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateChoiceTitlesAndDescriptionsForTests(selections).Cast<DummyChoiceWithColorIcon>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Title, Is.EqualTo("クラシック"));
            Assert.That(translated[0].Description, Is.EqualTo("西暦1000年のクドを探索する。"));
        });
    }

    [Test]
    public void CustomizePetsPostfix_TranslatesPetDescriptions()
    {
        WriteDictionary(("Albino ape", "白化した類人猿"));
        var pets = new List<DummyChoiceWithColorIcon>
        {
            new() { Title = "unused", Description = "Albino ape" },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(pets).Cast<DummyChoiceWithColorIcon>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Title, Is.EqualTo("unused"));
            Assert.That(translated[0].Description, Is.EqualTo("白化した類人猿"));
        });
    }

    [Test]
    public void OwnerEnumerablePostfix_PreservesFallbacksEmptyStringsAndMarkers()
    {
        WriteDictionary(("Export Code to Clipboard", "コードをクリップボードにコピー"));
        var options = new List<DummyCharGenMenuOption>
        {
            new() { Description = "Unknown Owner Text" },
            new() { Description = string.Empty },
            new() { Description = "\u0001Export Code to Clipboard" },
            new() { Description = "{{y|Export Code to Clipboard}}" },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(options).Cast<DummyCharGenMenuOption>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Description, Is.EqualTo("Unknown Owner Text"));
            Assert.That(translated[1].Description, Is.EqualTo(string.Empty));
            Assert.That(translated[2].Description, Is.EqualTo("\u0001Export Code to Clipboard"));
            Assert.That(translated[3].Description, Is.EqualTo("{{y|コードをクリップボードにコピー}}"));
        });
    }

    [Test]
    public void BuildLibraryMenuBarPostfix_TranslatesOptionsDescription()
    {
        WriteDictionary(("Options", "オプション"));
        var options = new List<DummyCharGenMenuOption>
        {
            new() { Description = "Options", InputCommand = "CmdChargenItemOptions", KeyDescription = "O" },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(options).Cast<DummyCharGenMenuOption>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Description, Is.EqualTo("オプション"));
            Assert.That(translated[0].InputCommand, Is.EqualTo("CmdChargenItemOptions"));
            Assert.That(translated[0].KeyDescription, Is.EqualTo("O"));
        });
    }

    [Test]
    public void GamemodeMenuBarPostfix_TranslatesDebugQuickstartDescription()
    {
        WriteDictionary(("[Debug] Quickstart", "[デバッグ] クイックスタート"));
        var options = new List<DummyCharGenMenuOption>
        {
            new() { Description = "[Debug] Quickstart", InputCommand = "CmdDebugQuickstart", KeyDescription = "Q" },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(options).Cast<DummyCharGenMenuOption>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(translated[0].Description, Is.EqualTo("[デバッグ] クイックスタート"));
            Assert.That(translated[0].InputCommand, Is.EqualTo("CmdDebugQuickstart"));
            Assert.That(translated[0].KeyDescription, Is.EqualTo("Q"));
        });
    }

    [Test]
    public void TranslateMenuOptions_HandlesEmptyList()
    {
        var empty = new List<DummyCharGenMenuOption>();

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateMenuOptionsForTests(empty)
            .Cast<DummyCharGenMenuOption>()
            .ToList();

        Assert.That(translated, Is.Empty);
    }

    [Test]
    public void TranslateChoiceTitles_PreservesMarkerPrefixedTitle()
    {
        WriteDictionary(("Add a new build code", "新しいビルドコードを追加"));
        var selections = new List<DummyChoiceWithColorIcon>
        {
            new() { Title = "\u0001Add a new build code", Description = string.Empty },
        };

        var translated = CharGenMenuOptionOwnerTranslationPatch.TranslateChoiceTitlesForTests(selections)
            .Cast<DummyChoiceWithColorIcon>()
            .ToList();

        Assert.That(translated[0].Title, Is.EqualTo("\u0001Add a new build code"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var path = Path.Combine(dictionariesDirectory, "chargen-menu-option-owner-l2.ja.json");
        using var writer = new StreamWriter(path, append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
