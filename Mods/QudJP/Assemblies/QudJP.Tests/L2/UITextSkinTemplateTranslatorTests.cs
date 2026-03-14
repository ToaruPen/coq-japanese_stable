using System.Text;
using System.Text.RegularExpressions;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UITextSkinTemplateTranslatorTests
{
    private static readonly Regex SkillPointsPattern =
        new Regex("^Skill Points \\(SP\\): (?<rest>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-uitextskin-template-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateSinglePlaceholderText_UsesSetText_WhenTemplateMatches()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));
        var skin = new DummyUITextSkin();
        skin.SetText("Skill Points (SP): 7");

        UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            skin,
            SkillPointsPattern,
            "Skill Points (SP): {val}",
            "{val}",
            "TestRoute");

        Assert.That(skin.Text, Is.EqualTo("スキルポイント (SP): 7"));
    }

    [Test]
    public void TranslateSinglePlaceholderText_UpdatesFieldOnlyTarget_WhenTemplateMatches()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));
        var skin = new DummyFieldOnlyUITextSkin { text = "Skill Points (SP): 8" };

        UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            skin,
            SkillPointsPattern,
            "Skill Points (SP): {val}",
            "{val}",
            "TestRoute");

        Assert.That(skin.text, Is.EqualTo("スキルポイント (SP): 8"));
    }

    [Test]
    public void TranslateSinglePlaceholderText_UpdatesPropertyOnlyTarget_WhenTemplateMatches()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));
        var skin = new DummyPropertyOnlyUITextSkin { Text = "Skill Points (SP): 9" };

        UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            skin,
            SkillPointsPattern,
            "Skill Points (SP): {val}",
            "{val}",
            "TestRoute");

        Assert.That(skin.Text, Is.EqualTo("スキルポイント (SP): 9"));
    }

    [Test]
    public void TranslateSinglePlaceholderText_UpdatesLowercasePropertyTarget_WhenTemplateMatches()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));
        var skin = new DummyLowercasePropertyUITextSkin { text = "Skill Points (SP): 10" };

        UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            skin,
            SkillPointsPattern,
            "Skill Points (SP): {val}",
            "{val}",
            "TestRoute");

        Assert.That(skin.text, Is.EqualTo("スキルポイント (SP): 10"));
    }

    [Test]
    public void TranslateSinglePlaceholderText_LeavesTextUnchanged_WhenPatternDoesNotMatch()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));
        var skin = new DummyUITextSkin();
        skin.SetText("Mutation Points: 3");

        UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            skin,
            SkillPointsPattern,
            "Skill Points (SP): {val}",
            "{val}",
            "TestRoute");

        Assert.That(skin.Text, Is.EqualTo("Mutation Points: 3"));
    }

    [Test]
    public void TranslateSinglePlaceholderText_LeavesTextUnchanged_WhenTemplateKeyMissing()
    {
        WriteDictionary(("Unrelated", "無関係"));
        var skin = new DummyUITextSkin();
        skin.SetText("Skill Points (SP): 5");

        UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            skin,
            SkillPointsPattern,
            "Skill Points (SP): {val}",
            "{val}",
            "StatusScreenTemplateTranslationPatchTests");

        Assert.Multiple(() =>
        {
            Assert.That(skin.Text, Is.EqualTo("Skill Points (SP): 5"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Skill Points (SP): {val}"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingRouteHitCountForTests("StatusScreenTemplateTranslationPatchTests"), Is.EqualTo(1));
        });
    }

    [Test]
    public void TranslateSinglePlaceholderText_AllowsNullTarget()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));

        Assert.DoesNotThrow(() => UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
            uiTextSkin: null,
            pattern: SkillPointsPattern,
            templateKey: "Skill Points (SP): {val}",
            placeholderToken: "{val}",
            context: "TestRoute"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

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

        var path = Path.Combine(tempDirectory, "uitextskin-template-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        var serialized = System.Text.Json.JsonSerializer.Serialize(value);
        return serialized.Substring(1, serialized.Length - 2);
    }
}
