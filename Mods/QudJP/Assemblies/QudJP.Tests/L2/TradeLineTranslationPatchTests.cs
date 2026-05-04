using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TradeLineTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-trade-line-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesCategoryAndItemText_WhenPatched()
    {
        WriteDictionary(
            ("Weapons", "武器"),
            ("iron sword", "鉄の剣"),
            ("bronze", "青銅"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeLine), nameof(DummyTradeLine.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TradeLineTranslationPatch), nameof(TradeLineTranslationPatch.Postfix))));

            var target = new DummyTradeLine();
            target.setData(new DummyFrameworkDataElement
            {
                Title = "Weapons",
                Description = "iron sword",
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.categoryText.text, Is.EqualTo("[+] 武器"));
                Assert.That(target.text.text, Is.EqualTo("鉄の剣"));
                Assert.That(target.check.text, Is.EqualTo("{{W|1}}"));
                Assert.That(target.rightFloatText.text, Is.EqualTo("[$1.00]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "TradeLine.CategoryText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "DisplayName.ExactLookup"),
                    Is.EqualTo(1));
            });

            target.setData(new DummyFrameworkDataElement
            {
                Title = "Weapons",
                Description = "{{w|bronze}}",
            });

            Assert.That(target.text.text, Is.EqualTo("{{w|青銅}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_FallbackKeepsEnglishCategoryAndItemText()
    {
        WriteDictionary(("Armor", "防具"));

        WithPatchedTradeLine(target =>
        {
            target.setData(new DummyFrameworkDataElement
            {
                Title = "Weapons",
                Description = "iron sword",
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.categoryText.text, Is.EqualTo("[+] Weapons"));
                Assert.That(target.text.text, Is.EqualTo("iron sword"));
                Assert.That(target.check.text, Is.EqualTo("{{W|1}}"));
                Assert.That(target.rightFloatText.text, Is.EqualTo("[$1.00]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "TradeLine.CategoryText"),
                    Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "DisplayName.ExactLookup"),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_EmptyInputLeavesTradeLineFieldsStable()
    {
        WriteDictionary(("Weapons", "武器"), ("iron sword", "鉄の剣"));

        WithPatchedTradeLine(target =>
        {
            target.setData(new DummyFrameworkDataElement
            {
                Title = string.Empty,
                Description = string.Empty,
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.categoryText.text, Is.EqualTo("[+] "));
                Assert.That(target.text.text, Is.EqualTo(string.Empty));
                Assert.That(target.check.text, Is.EqualTo("{{W|1}}"));
                Assert.That(target.rightFloatText.text, Is.EqualTo("[$1.00]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "TradeLine.CategoryText"),
                    Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "DisplayName.ExactLookup"),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesColorTagsAroundTranslatedCategoryAndItemText()
    {
        WriteDictionary(
            ("Weapons", "武器"),
            ("iron sword", "鉄の剣"));

        WithPatchedTradeLine(target =>
        {
            target.setData(new DummyFrameworkDataElement
            {
                Title = "{{W|Weapons}}",
                Description = "{{G|iron sword}}",
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.categoryText.text, Is.EqualTo("[+] {{W|武器}}"));
                Assert.That(target.text.text, Is.EqualTo("{{G|鉄の剣}}"));
                Assert.That(target.check.text, Is.EqualTo("{{W|1}}"));
                Assert.That(target.rightFloatText.text, Is.EqualTo("[$1.00]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "TradeLine.CategoryText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "DisplayName.ExactLookup"),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Postfix_PreservesDirectTranslationMarkerWithoutReapplyingTradeLineTranslations()
    {
        WriteDictionary(
            ("Weapons", "武器"),
            ("iron sword", "鉄の剣"));

        WithPatchedTradeLine(target =>
        {
            target.setData(new DummyFrameworkDataElement
            {
                Title = "\x01Weapons",
                Description = "\x01iron sword",
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.categoryText.text, Is.EqualTo("[+] \x01Weapons"));
                Assert.That(target.text.text, Is.EqualTo("\x01iron sword"));
                Assert.That(target.check.text, Is.EqualTo("{{W|1}}"));
                Assert.That(target.rightFloatText.text, Is.EqualTo("[$1.00]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "TradeLine.CategoryText"),
                    Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TradeLineTranslationPatch), "DisplayName.ExactLookup"),
                    Is.EqualTo(0));
            });
        });
    }

    private static void WithPatchedTradeLine(Action<DummyTradeLine> action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeLine), nameof(DummyTradeLine.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TradeLineTranslationPatch), nameof(TradeLineTranslationPatch.Postfix))));

            action(new DummyTradeLine());
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
        File.WriteAllText(
            Path.Combine(tempDirectory, "trade-line-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
