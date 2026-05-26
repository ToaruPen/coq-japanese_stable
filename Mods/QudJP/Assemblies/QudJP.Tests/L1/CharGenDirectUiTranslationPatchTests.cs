using System.Text;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CharGenDirectUiTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-chargen-direct-ui-l2", Guid.NewGuid().ToString("N"));
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
    public void AttributeUpdatedPostfix_TranslatesBonusTooltipAndPointCost()
    {
        WriteDictionary(("Calling", "職能"));
        var target = new DummyCharGenAttributeSelectionControlTarget
        {
            data =
            {
                BonusSource = "+2 from Calling\n",
                APToRaise = 2,
            },
        };

        target.Updated();
        CharGenDirectUiTranslationPatch.TranslateAttributeSelectionControlForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(target.tooltip.LastKey, Is.EqualTo("BodyText"));
            Assert.That(target.tooltip.LastText, Is.EqualTo("職能による +2\n"));
            Assert.That(target.TitleButton.Title, Is.EqualTo("[2点]"));
        });
    }

    [Test]
    public void AttributeUpdatedPostfix_LeavesUnknownBonusSourceUnchanged()
    {
        var target = new DummyCharGenAttributeSelectionControlTarget
        {
            data =
            {
                BonusSource = "+1 from Unmapped Source\n",
                APToRaise = 1,
            },
        };

        target.Updated();
        CharGenDirectUiTranslationPatch.TranslateAttributeSelectionControlForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(target.tooltip.LastText, Is.EqualTo("+1 from Unmapped Source\n"));
            Assert.That(target.TitleButton.Title, Is.EqualTo("[1点]"));
        });
    }

    [TestCase("", "")]
    [TestCase("\u0001+2 from Calling", "+2 from Calling")]
    public void AttributeUpdatedPostfix_PreservesEmptyAndDirectMarkedBonusSource(string bonusSource, string expected)
    {
        WriteDictionary(("Calling", "職能"));
        var target = new DummyCharGenAttributeSelectionControlTarget
        {
            data =
            {
                BonusSource = bonusSource,
                APToRaise = 1,
            },
        };

        target.Updated();
        CharGenDirectUiTranslationPatch.TranslateAttributeSelectionControlForTests(target);

        Assert.That(target.tooltip.LastText, Is.EqualTo(expected));
    }

    [Test]
    public void AttributeUpdatedPostfix_TranslatesColorTaggedBonusSource()
    {
        WriteDictionary(("Calling", "職能"));
        var target = new DummyCharGenAttributeSelectionControlTarget
        {
            data =
            {
                BonusSource = "+2 from {{C|Calling}}",
                APToRaise = 1,
            },
        };

        target.Updated();
        CharGenDirectUiTranslationPatch.TranslateAttributeSelectionControlForTests(target);

        Assert.That(target.tooltip.LastText, Is.EqualTo("{{C|職能}}による +2"));
    }

    [Test]
    public void QudSubtypeBeforeShowPostfix_TranslatesColonWrappedSubtypeTitle()
    {
        WriteDictionary(("choose subtype", "職能を選択"));
        var target = new DummyQudSubtypeModuleWindowTarget();

        target.BeforeShow(new DummyEmbarkBuilderModuleWindowDescriptor());
        CharGenDirectUiTranslationPatch.TranslateQudSubtypeModuleWindowForTests(target);

        Assert.That(target.prefabComponent.titleText.text, Is.EqualTo("：職能を選択："));
    }

    [Test]
    public void QudSubtypeBeforeShowPostfix_LeavesUnknownSubtypeTitleUnchanged()
    {
        var target = new DummyQudSubtypeModuleWindowTarget { SubtypeTitle = "unknown subtype" };

        target.BeforeShow(new DummyEmbarkBuilderModuleWindowDescriptor());
        CharGenDirectUiTranslationPatch.TranslateQudSubtypeModuleWindowForTests(target);

        Assert.That(target.prefabComponent.titleText.text, Is.EqualTo(":unknown subtype:"));
    }

    [TestCase("", "::")]
    [TestCase("\u0001choose subtype", ":choose subtype:")]
    public void QudSubtypeBeforeShowPostfix_PreservesEmptyAndDirectMarkedSubtypeTitle(string subtypeTitle, string expected)
    {
        WriteDictionary(("choose subtype", "職能を選択"));
        var target = new DummyQudSubtypeModuleWindowTarget { SubtypeTitle = subtypeTitle };

        target.BeforeShow(new DummyEmbarkBuilderModuleWindowDescriptor());
        CharGenDirectUiTranslationPatch.TranslateQudSubtypeModuleWindowForTests(target);

        Assert.That(target.prefabComponent.titleText.text, Is.EqualTo(expected));
    }

    [Test]
    public void QudSubtypeBeforeShowPostfix_TranslatesColorTaggedSubtypeTitle()
    {
        WriteDictionary(("choose subtype", "職能を選択"));
        var target = new DummyQudSubtypeModuleWindowTarget { SubtypeTitle = "{{C|choose subtype}}" };

        target.BeforeShow(new DummyEmbarkBuilderModuleWindowDescriptor());
        CharGenDirectUiTranslationPatch.TranslateQudSubtypeModuleWindowForTests(target);

        Assert.That(target.prefabComponent.titleText.text, Is.EqualTo("：{{C|職能を選択}}："));
    }

    [Test]
    public void AttributeUpdatedPostfix_TranslatesZeroApToRaiseAsZeroPts()
    {
        WriteDictionary(("Calling", "職能"));
        var target = new DummyCharGenAttributeSelectionControlTarget
        {
            data =
            {
                BonusSource = "+2 from Calling\n",
                APToRaise = 0,
            },
        };

        target.Updated();
        CharGenDirectUiTranslationPatch.TranslateAttributeSelectionControlForTests(target);

        Assert.That(target.TitleButton.Title, Is.EqualTo("[0点]"));
    }

    [Test]
    public void AttributeUpdatedPostfix_TooltipKeyRemainsBodyText()
    {
        WriteDictionary(("Calling", "職能"));
        var target = new DummyCharGenAttributeSelectionControlTarget
        {
            data =
            {
                BonusSource = "+1 from Calling\n",
                APToRaise = 1,
            },
        };

        target.Updated();
        CharGenDirectUiTranslationPatch.TranslateAttributeSelectionControlForTests(target);

        Assert.That(target.tooltip.LastKey, Is.EqualTo("BodyText"));
    }

    [Test]
    public void QudSubtypeBeforeShowPostfix_GetSubtypeTitleIsCalledBeforeTranslation()
    {
        WriteDictionary(("choose subtype", "職能を選択"));
        var target = new DummyQudSubtypeModuleWindowTarget { SubtypeTitle = "choose subtype" };

        target.BeforeShow(new DummyEmbarkBuilderModuleWindowDescriptor());
        CharGenDirectUiTranslationPatch.TranslateQudSubtypeModuleWindowForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(target.getSubtypeTitle(), Is.EqualTo("choose subtype"));
            Assert.That(target.prefabComponent.titleText.text, Is.EqualTo("：職能を選択："));
        });
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var path = Path.Combine(dictionariesDirectory, "chargen-direct-ui-l2.ja.json");
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
