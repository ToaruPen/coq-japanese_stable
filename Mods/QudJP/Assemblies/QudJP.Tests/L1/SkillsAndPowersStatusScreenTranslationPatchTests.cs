using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
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
    public void TryTranslateExactLeafPreservingColors_TranslatesUppercaseHeaderAndBracketedSkillNames()
    {
        WriteDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Required Skills", "前提スキル"),
            ("Tinker I", "工匠 I"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
                    "[REQUIRED SKILLS]",
                    nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
                    recordTransform: false).translated,
                Is.EqualTo("[前提スキル]"));
            Assert.That(
                SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
                    "{{R|[Tinker I]}}",
                    nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
                    recordTransform: false).translated,
                Is.EqualTo("{{R|[工匠 I]}}"));
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

    [Test]
    public void TryTranslateStructuredLinePreservingColors_RebuildsPowerEntryLineWithoutOffsettingCaptures()
    {
        WriteDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Tinker I", "工匠 I"),
            ("Tinker II", "工匠 II"));

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateStructuredLinePreservingColors(
            "    {{K|:}}{{K|Tinker II}} [{{K|200}}sp] {{C|23}} {{R|Intelligence}}, {{R|Tinker I}}",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(
                result.translated,
                Is.EqualTo("    {{K|:}}{{K|工匠 II}} [{{K|200}}sp] {{C|23}} {{R|INT}}, {{R|工匠 I}}"));
        });
    }

    [Test]
    public void TryTranslateStructuredLinePreservingColors_LeavesUnknownLeafTextUnchanged()
    {
        WriteDictionaryFile("ui-skillsandpowers.ja.json", ("Tinker I", "工匠 I"));
        const string source = "{{K|:Unknown Power}}";

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateStructuredLinePreservingColors(
            source,
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.False);
            Assert.That(result.translated, Is.EqualTo(source));
        });
    }

    [TestCase("")]
    [TestCase("{{K|}}")]
    [TestCase("\u0001{{K|:Tinker II}}")]
    public void TryTranslateStructuredLinePreservingColors_PassesThroughMarkerAndEmptyEdgeCases(string source)
    {
        WriteDictionaryFile("ui-skillsandpowers.ja.json", ("Tinker II", "工匠 II"));

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateStructuredLinePreservingColors(
            source,
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.False);
            Assert.That(result.translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateStructuredLinePreservingColors_RestoresWholeLineBoundaryWrapper()
    {
        WriteDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Tinker I", "工匠 I"),
            ("Tinker II", "工匠 II"));

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateStructuredLinePreservingColors(
            "{{K|Tinker II [200sp] 23 Intelligence, Tinker I}}",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(result.translated, Is.EqualTo("{{K|工匠 II [200sp] 23 INT, 工匠 I}}"));
        });
    }

    [Test]
    public void TryTranslateDetailText_TranslatesGeneratedAbilityStatLinesAndCooldownAdjustment()
    {
        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            "Duration: 6d6 rounds\nRange: 8\nArea: 7x7\nCooldown: {{G|43}} rounds\nCooldown reduced by 7 due to high Willpower.",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(
                result.translated,
                Is.EqualTo("持続時間: 6d6 ラウンド\n射程: 8\n効果範囲: 7x7\nクールダウン: {{G|43}} ラウンド\nクールダウンが7短縮（高い意志力による）。"));
        });
    }

    [Test]
    public void TryTranslateDetailText_PreservesColorsInsideGeneratedAbilityStatValues()
    {
        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            "Duration: {{G|6 rounds}}",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(result.translated, Is.EqualTo("持続時間: {{G|6 ラウンド}}"));
        });
    }

    [Test]
    public void TryTranslateDetailText_PreservesLabelAndWholeLineColorsInGeneratedAbilityStatLines()
    {
        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            "{{K|{{G|Duration}}: 6 rounds}}",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(result.translated, Is.EqualTo("{{K|{{G|持続時間}}: 6 ラウンド}}"));
        });
    }

    [Test]
    public void TryTranslateDetailText_TranslatesCooldownFloorLine()
    {
        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            "Cooldown cannot be reduced below {{G|5}} rounds.",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(result.translated, Is.EqualTo("クールダウンは{{G|5}}ラウンド未満には短縮されない。"));
        });
    }

    [Test]
    public void TryTranslateDetailText_LeavesNonMatchingEnglishLineUnchanged()
    {
        var source = "Some unrelated line.";

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            source,
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.False);
            Assert.That(result.translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateDetailText_LeavesEmptyInputUnchanged()
    {
        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            string.Empty,
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.False);
            Assert.That(result.translated, Is.Empty);
        });
    }

    [Test]
    public void TryTranslateDetailText_LeavesDirectMarkedInputUnchanged()
    {
        var source = "\x01{{G|Some unrelated line.}}";

        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            source,
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.False);
            Assert.That(result.translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateDetailText_PreservesCooldownReasonColors()
    {
        var result = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText(
            "Cooldown reduced by 7 due to {{G|high Willpower}}.",
            nameof(SkillsAndPowersStatusScreenTranslationPatchTests),
            recordTransform: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.changed, Is.True);
            Assert.That(result.translated, Is.EqualTo("クールダウンが7短縮（{{G|高い意志力}}による）。"));
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
