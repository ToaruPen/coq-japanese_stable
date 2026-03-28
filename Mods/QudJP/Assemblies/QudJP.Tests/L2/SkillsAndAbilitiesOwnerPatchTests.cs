using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SkillsAndAbilitiesOwnerPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-skills-abilities-owner-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void SkillsAndPowersStatusScreenDetailsPatch_TranslatesOwnerFields_WhenPatched()
    {
        WriteDictionary(
            ("Spit Slime", "粘液吐き"),
            ("You produce a viscous slime that you can spit at things.", "粘つくスライムを生成し、対象へ吐きかけられる。"),
            ("[Learned]", "[習得済み]"),
            ("Required Skills", "前提スキル"),
            ("Tinker I", "工匠 I"),
            ("Tinker II", "工匠 II"),
            ("Melee", "近接"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillsAndPowersDetailScreen), nameof(DummySkillsAndPowersDetailScreen.UpdateDetailsFromNode)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SkillsAndPowersStatusScreenDetailsPatch), nameof(SkillsAndPowersStatusScreenDetailsPatch.Postfix))));

            var screen = new DummySkillsAndPowersDetailScreen();
            screen.UpdateDetailsFromNode(new DummySPNode
            {
                Name = "Spit Slime",
                Description = "You produce a viscous slime that you can spit at things.",
            });

            Assert.Multiple(() =>
            {
                Assert.That(screen.skillNameText.Text, Is.EqualTo("粘液吐き"));
                Assert.That(screen.detailsText.Text, Is.EqualTo("粘つくスライムを生成し、対象へ吐きかけられる。"));
                Assert.That(screen.learnedText.Text, Is.EqualTo("{{G|[習得済み]}}"));
                Assert.That(screen.requirementsText.Text, Is.EqualTo(":: {{C|100}} SP ::\n:: 23 INT ::"));
                Assert.That(screen.requiredSkillsHeader.Text, Is.EqualTo("前提スキル"));
                Assert.That(screen.requiredSkillsText.Text, Is.EqualTo("  :工匠 II [200sp] 23 INT, 工匠 I\n:近接"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SkillsAndPowersStatusScreenDetailsPatch_RecordsOwnerRouteTransform_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("Spit Slime", "粘液吐き"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillsAndPowersDetailScreen), nameof(DummySkillsAndPowersDetailScreen.UpdateDetailsFromNode)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SkillsAndPowersStatusScreenDetailsPatch), nameof(SkillsAndPowersStatusScreenDetailsPatch.Postfix))));

            var screen = new DummySkillsAndPowersDetailScreen();
            screen.UpdateDetailsFromNode(new DummySPNode
            {
                Name = "Spit Slime",
            });

            const string source = "Spit Slime";
            Assert.Multiple(() =>
            {
                Assert.That(screen.skillNameText.Text, Is.EqualTo("粘液吐き"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SkillsAndPowersStatusScreenDetailsPatch),
                        "SkillsAndPowers.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(SkillsAndPowersStatusScreenDetailsPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityBarUpdateAbilitiesTextPatch_TranslatesAbilityTextOwnerSide_WhenPatched()
    {
        WriteDictionary(
            ("ABILITIES", "アビリティ"),
            ("page {0} of {1}", "{0}/{1}ページ"),
            ("Previous Page", "前のページ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBar), nameof(DummyAbilityBar.UpdateAbilitiesText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AbilityBarUpdateAbilitiesTextPatch), nameof(AbilityBarUpdateAbilitiesTextPatch.Postfix))));

            var bar = new DummyAbilityBar();
            bar.UpdateAbilitiesText();

            Assert.Multiple(() =>
            {
                Assert.That(bar.AbilityCommandText.Text, Is.EqualTo("アビリティ\n1/3ページ"));
                Assert.That(bar.CycleCommandText.Text, Is.EqualTo("前のページ"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityBarUpdateAbilitiesTextPatch_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(
            ("ABILITIES", "アビリティ"),
            ("page {0} of {1}", "{0}/{1}ページ"),
            ("Previous Page", "前のページ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBar), nameof(DummyAbilityBar.UpdateAbilitiesText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AbilityBarUpdateAbilitiesTextPatch), nameof(AbilityBarUpdateAbilitiesTextPatch.Postfix))));

            var bar = new DummyAbilityBar();
            bar.UpdateAbilitiesText();

            const string abilitiesSource = "ABILITIES\npage 1 of 3";
            const string cycleSource = "Previous Page";
            Assert.Multiple(() =>
            {
                Assert.That(bar.AbilityCommandText.Text, Is.EqualTo("アビリティ\n1/3ページ"));
                Assert.That(bar.CycleCommandText.Text, Is.EqualTo("前のページ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarUpdateAbilitiesTextPatch),
                        "AbilityBar.AbilitiesCommand"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarUpdateAbilitiesTextPatch),
                        "AbilityBar.CycleCommand"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(AbilityBarUpdateAbilitiesTextPatch),
                        SinkObservation.ObservationOnlyDetail,
                        abilitiesSource,
                        abilitiesSource),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(AbilityBarUpdateAbilitiesTextPatch),
                        SinkObservation.ObservationOnlyDetail,
                        cycleSource,
                        cycleSource),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
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
        File.WriteAllText(
            Path.Combine(tempDirectory, "skills-abilities-owner-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
