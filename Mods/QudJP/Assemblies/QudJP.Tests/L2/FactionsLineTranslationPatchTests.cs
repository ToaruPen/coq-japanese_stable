using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FactionsLineTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-factionsline-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesKnownFactionLineTexts_WhenPatched()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("Reputation: {0}", "評判: {0}"),
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("You aren't welcome in their holy places.", "あなたは彼らの聖地では歓迎されていない。"),
            ("The {0} is interested in trading secrets about {1}. They're also interested in hearing gossip that's about them.", "{0}は{1}に関する秘密の取引に関心があり、自分たちに関するうわさ話にも興味を示す。"),
            ("the sultan they worship", "彼らが崇拝するスルタン"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(DummyFactionsLine), nameof(DummyFactionsLine.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FactionsLineTranslationPatch), nameof(FactionsLineTranslationPatch.Postfix))));

            var line = new DummyFactionsLine();
            line.setData(new DummyFactionsLineData("The villagers of Abal"));

            Assert.Multiple(() =>
            {
                Assert.That(line.barText.text, Is.EqualTo("Abalの村人たち"));
                Assert.That(line.barReputationText.text, Is.EqualTo("評判: 0"));
                Assert.That(line.detailsText.text, Is.EqualTo("Abalの村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"));
                Assert.That(line.detailsText2.text, Is.EqualTo("あなたは彼らの聖地では歓迎されていない。"));
                Assert.That(line.detailsText3.text, Is.EqualTo("Arbitrarilyborn Cultは彼らが崇拝するスルタンに関する秘密の取引に関心があり、自分たちに関するうわさ話にも興味を示す。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsDelegatedOwnerRouteTransform_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("The villagers of {0}", "{0}の村人たち"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(DummyFactionsLine), nameof(DummyFactionsLine.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FactionsLineTranslationPatch), nameof(FactionsLineTranslationPatch.Postfix))));

            var line = new DummyFactionsLine();
            const string source = "The villagers of Abal";
            line.setData(new DummyFactionsLineData(source));

            Assert.Multiple(() =>
            {
                Assert.That(line.barText.text, Is.EqualTo("Abalの村人たち"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(FactionsStatusScreenTranslationPatch),
                        "The villagers of {0}"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(FactionsStatusScreenTranslationPatch),
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
    public void Postfix_DoesNotLogMissingKeys_ForAlreadyLocalizedFactionOutputs()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(DummyFactionsLine), nameof(DummyFactionsLine.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FactionsLineTranslationPatch), nameof(FactionsLineTranslationPatch.Postfix))));

            var output = TestTraceHelper.CaptureTrace(() =>
            {
                var line = new DummyFactionsLine();
                line.barText.SetText("Abalの村人たち");
                line.barReputationText.SetText("評判: 0");
                line.detailsText.SetText("猫はたいてい撫でさせてくれない。");
                FactionsLineTranslationPatch.Postfix(line, data: null);
            });

            Assert.Multiple(() =>
            {
                Assert.That(output, Does.Not.Contain("missing key 'Abalの村人たち'"));
                Assert.That(output, Does.Not.Contain("missing key '評判: 0'"));
                Assert.That(output, Does.Not.Contain("missing key '猫はたいてい撫でさせてくれない。'"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_EnablesWrapping_ForFactionDetailFields()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("You aren't welcome in their holy places.", "あなたは彼らの聖地では歓迎されていない。"),
            ("The {0} is interested in trading secrets about {1}. They're also interested in hearing gossip that's about them.", "{0}は{1}に関する秘密の取引に関心があり、自分たちに関するうわさ話にも興味を示す。"),
            ("the sultan they worship", "彼らが崇拝するスルタン"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(DummyFactionsLine), nameof(DummyFactionsLine.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FactionsLineTranslationPatch), nameof(FactionsLineTranslationPatch.Postfix))));

            var line = new DummyFactionsLine();
            line.setData(new DummyFactionsLineData("The villagers of Abal"));

            Assert.Multiple(() =>
            {
                Assert.That(line.barText.enableWordWrapping, Is.False);
                Assert.That(line.detailsText.enableWordWrapping, Is.True);
                Assert.That(line.detailsText.textWrapping, Is.True);
                Assert.That(line.detailsText.textWrappingMode, Is.EqualTo(DummyTextWrappingMode.Normal));
                Assert.That(line.detailsText2.enableWordWrapping, Is.True);
                Assert.That(line.detailsText3.enableWordWrapping, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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

        var path = Path.Combine(tempDirectory, "factions-line-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
