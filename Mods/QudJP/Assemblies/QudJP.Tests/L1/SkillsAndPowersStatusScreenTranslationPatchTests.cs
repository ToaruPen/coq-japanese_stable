using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class SkillsAndPowersStatusScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-skills-status-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
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
    public void TryTranslateExactLeafPreservingColors_PrefersScopedSkillDictionaryOverGlobalCollision()
    {
        WriteDictionaryFile("ui-chargen-supplement.ja.json", ("Persuasion", "説得術"));
        WriteDictionaryFile(
            Path.Combine("Scoped", "ui-skillsandpowers-skill-names.ja.json"),
            ("Persuasion", "説得"));

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
            "{{G|Persuasion}}",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(result.translated, Is.EqualTo("{{G|説得}}"));
        });
    }

    [Test]
    public void TryTranslateText_TranslatesStructuredSkillLineUsingScopedSkillNames()
    {
        WriteDictionaryFile(
            "ui-chargen-supplement.ja.json",
            ("Persuasion", "説得術"),
            ("Wayfaring", "サバイバル"));
        WriteDictionaryFile(
            Path.Combine("Scoped", "ui-skillsandpowers-skill-names.ja.json"),
            ("Persuasion", "説得"),
            ("Wayfaring", "辺境行"));

        var translated = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateText(
            "  :Persuasion [100sp] 19 Ego, Wayfaring",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("  :説得 [100sp] 19 EGO, 辺境行"));
        });
    }

    private void WriteDictionaryFile(string relativePath, params (string key, string text)[] entries)
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

        var path = Path.Combine(tempDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
