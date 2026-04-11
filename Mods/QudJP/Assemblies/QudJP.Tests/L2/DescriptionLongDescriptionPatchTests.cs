using System;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using NUnit.Framework;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionLongDescriptionPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-long-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesAppendedDescription_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("It crackles with static.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("それは静電気を散らしている。"));
        });
    }

    [Test]
    public void Postfix_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "It crackles with static.";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(builder.ToString(), Is.EqualTo("それは静電気を散らしている。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesExistingPrefix_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("It crackles with static.");
            var builder = new StringBuilder("prefix: ");
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("prefix: それは静電気を散らしている。"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Charged item", "帯電したアイテム"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("{{C|Charged item}}");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("{{C|帯電したアイテム}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownDescription_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Unknown long description");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("Unknown long description"));
        });
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesStatAbbreviationsUnchanged_WhenPatched()
    {
        WriteDictionary(("STR", "筋力"), ("+1 STR", "+1 筋力"));

        RunWithDescriptionPatch(() =>
        {
            var abbreviationTarget = new DummyDescriptionTarget("STR");
            var abbreviationBuilder = new StringBuilder();
            abbreviationTarget.GetLongDescription(abbreviationBuilder);

            var signedTarget = new DummyDescriptionTarget("+1 STR");
            var signedBuilder = new StringBuilder();
            signedTarget.GetLongDescription(signedBuilder);

            Assert.Multiple(() =>
            {
                Assert.That(abbreviationBuilder.ToString(), Is.EqualTo("STR"));
                Assert.That(signedBuilder.ToString(), Is.EqualTo("+1 STR"));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesCompareStatusLines_WhenPatched()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        RunWithDescriptionPatch(() =>
        {
            var capTarget = new DummyDescriptionTarget("Strength Bonus Cap: no limit");
            var capBuilder = new StringBuilder();
            capTarget.GetLongDescription(capBuilder);

            var egoCapTarget = new DummyDescriptionTarget("Ego Bonus Cap: 2");
            var egoCapBuilder = new StringBuilder();
            egoCapTarget.GetLongDescription(egoCapBuilder);

            var weaponClassTarget =
                new DummyDescriptionTarget("Weapon Class: Long Blades (increased penetration on critical hit)");
            var weaponClassBuilder = new StringBuilder();
            weaponClassTarget.GetLongDescription(weaponClassBuilder);

            Assert.Multiple(() =>
            {
                Assert.That(capBuilder.ToString(), Is.EqualTo("筋力ボーナス上限: なし"));
                Assert.That(egoCapBuilder.ToString(), Is.EqualTo("Ego ボーナス上限: 2"));
                Assert.That(weaponClassBuilder.ToString(), Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesVillageDescriptionPattern_WhenPatched()
    {
        WriteDictionary(
            ("conclave", "会合"),
            ("kin", "血縁"),
            ("some organization", "ある組織"));
        WriteMessagePatternDictionary((
            "^(.+?), there's a ((?i:gathering|conclave|congregation|settlement|band|flock|society)) of (.+?) and their ((?i:folk|communities|kindred|families|kin|kind|kinsfolk|tribe|clan))\\.$",
            "{0}、{t2}とその{t3}の{t1}がある。"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("sun-baked ruins, there's a conclave of some organization and their kin.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(builder.ToString(), Is.EqualTo("sun-baked ruins、ある組織とその血縁の会合がある。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.Pattern"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesPhysicalFeaturesAndEquippedLines_WhenPatched()
    {
        WriteDictionary(
            ("stinger", "毒針"),
            ("black robe", "黒のローブ"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Physical features: stinger\nEquipped: black robe");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("身体的特徴: 毒針\n装備: 黒のローブ"));
        });
    }

    [Test]
    public void Postfix_TranslatesReasonBearingDispositionLine_WithColoredFactionTarget_WhenPatched()
    {
        WriteDictionary(("giving alms to pilgrims", "巡礼者に施しをしたため"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Admired by {{C|the Mechanimists}} for giving alms to pilgrims.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("{{C|the Mechanimists}}に敬愛されている。理由: 巡礼者に施しをしたため。"));
        });
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
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(builder.ToString());
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void AppendEntries(StringBuilder builder, IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private static MethodInfo DescriptionMethod => RequireMethod(typeof(DummyDescriptionTarget), nameof(DummyDescriptionTarget.GetLongDescription));

    private static HarmonyMethod DescriptionPrefix =>
        new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Prefix)));

    private static HarmonyMethod DescriptionPostfix =>
        new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Postfix)));

    private static void RunWithDescriptionPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: DescriptionMethod,
                prefix: DescriptionPrefix,
                postfix: DescriptionPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "description-long-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
    }

    private void WriteMessagePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");
        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private sealed class DummyDescriptionTarget
    {
        private readonly string appendedText;

        public DummyDescriptionTarget(string appendedText)
        {
            this.appendedText = appendedText;
        }

        public void GetLongDescription(StringBuilder SB)
        {
            SB.Append(appendedText);
        }
    }
}
