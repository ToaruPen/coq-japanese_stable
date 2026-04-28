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
public sealed class LookTooltipContentPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-look-tooltip-l2", Guid.NewGuid().ToString("N"));
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
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesKnownTooltip_WhenPatched()
    {
        WriteDictionary(("This relic hums softly.", "この遺物はかすかに唸っている。"));

        RunWithTooltipPatch(() =>
        {
            const string source = "This relic hums softly.";
            var result = DummyLookTooltipTarget.GenerateTooltipContent(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("この遺物はかすかに唸っている。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(LookTooltipContentPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(LookTooltipContentPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void TranslateTooltipContent_UsesPopupFixedLeafDictionary_ForPlayerLookPopup()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        const string source = "It's you.";
        var result = LookTooltipContentPatch.TranslateTooltipContent(source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("あなた自身だ。"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(LookTooltipContentPatch),
                    "Description.ExactLeaf"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Ancient ruin", "古代の廃墟"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("{{Y|Ancient ruin}}");

            Assert.That(result, Is.EqualTo("{{Y|古代の廃墟}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownTooltip_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("Unknown tooltip text");

            Assert.That(result, Is.EqualTo("Unknown tooltip text"));
        });
    }

    [Test]
    public void Postfix_LeavesStatAbbreviationsUnchanged_WhenPatched()
    {
        WriteDictionary(("STR", "筋力"), ("+1 STR", "+1 筋力"));

        RunWithTooltipPatch(() =>
        {
            var abbreviation = DummyLookTooltipTarget.GenerateTooltipContent("STR");
            var signed = DummyLookTooltipTarget.GenerateTooltipContent("+1 STR");

            Assert.Multiple(() =>
            {
                Assert.That(abbreviation, Is.EqualTo("STR"));
                Assert.That(signed, Is.EqualTo("+1 STR"));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesCompareStatusLines_WhenPatched()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Ego", "自我"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        RunWithTooltipPatch(() =>
        {
            var cap = DummyLookTooltipTarget.GenerateTooltipContent("Strength Bonus Cap: no limit");
            var egoCap = DummyLookTooltipTarget.GenerateTooltipContent("Ego Bonus Cap: 2");
            var weaponClass = DummyLookTooltipTarget.GenerateTooltipContent(
                "Weapon Class: Long Blades (increased penetration on critical hit)");

            Assert.Multiple(() =>
            {
                Assert.That(cap, Is.EqualTo("筋力ボーナス上限: なし"));
                Assert.That(egoCap, Is.EqualTo("自我ボーナス上限: 2"));
                Assert.That(weaponClass, Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
            });
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

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
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

    private static HarmonyMethod TooltipPostfix =>
        new HarmonyMethod(RequireMethod(typeof(LookTooltipContentPatch), nameof(LookTooltipContentPatch.Postfix)));

    private static void RunWithTooltipPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLookTooltipTarget), nameof(DummyLookTooltipTarget.GenerateTooltipContent)),
                postfix: TooltipPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "look-tooltip-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
    }

    private static class DummyLookTooltipTarget
    {
        public static string GenerateTooltipContent(string content)
        {
            return content;
        }
    }
}
