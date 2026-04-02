using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TradeScreenUiTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-trade-ui-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyTradeScreenUiTarget.ResetDefaults();
        DummyPopupAskNumberTarget.Reset();
        DummyLegacyTradeUiTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyTradeScreenUiTarget.ResetDefaults();
        DummyPopupAskNumberTarget.Reset();
        DummyLegacyTradeUiTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesModernMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("Filter", "フィルター"),
            ("toggle sort", "並び替え切替"),
            ("vendor actions", "取引"),
            ("add one", "1つ追加"),
            ("remove one", "1つ減らす"),
            ("toggle all", "すべて切り替え"),
            ("navigate", "移動"),
            ("Close Menu", "メニューを閉じる"),
            ("offer", "提示"),
            ("sort: ", "ソート: "),
            ("by class", "クラス別"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeScreenUiTarget), nameof(DummyTradeScreenUiTarget.UpdateMenuBars)),
                prefix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUiTranslationPatch), nameof(TradeScreenUiTranslationPatch.Prefix))));

            var target = new DummyTradeScreenUiTarget();
            target.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(target.renderedDefaultMenuOptions[0].Description, Is.EqualTo("メニューを閉じる"));
                Assert.That(target.renderedDefaultMenuOptions[1].Description, Is.EqualTo("移動"));
                Assert.That(target.renderedDefaultMenuOptions[2].Description, Is.EqualTo("ソート: a-z/クラス別"));
                Assert.That(target.renderedDefaultMenuOptions[2].KeyDescription, Is.EqualTo("並び替え切替"));
                Assert.That(target.renderedGetItemMenuOptions[4].Description, Is.EqualTo("取引"));
                Assert.That(target.renderedGetItemMenuOptions[5].KeyDescription, Is.EqualTo("1つ追加"));
                Assert.That(target.renderedGetItemMenuOptions[8].Description, Is.EqualTo("提示"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesSortModeDescription_PreservesColorTags_WhenPatched()
    {
        WriteDictionary(
            ("sort: ", "ソート: "),
            ("by class", "クラス別"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeScreenUiTarget), nameof(DummyTradeScreenUiTarget.UpdateMenuBars)),
                prefix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUiTranslationPatch), nameof(TradeScreenUiTranslationPatch.Prefix))));

            DummyTradeScreenUiTarget.TOGGLE_SORT.Description = "sort: {{w|a-z}}/{{y|by class}}";

            var target = new DummyTradeScreenUiTarget();
            target.UpdateMenuBars();

            Assert.That(target.renderedDefaultMenuOptions[2].Description, Is.EqualTo("ソート: {{w|a-z}}/{{y|クラス別}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesTradeSomePrompt_WhenPatched()
    {
        WriteDictionary(
            ("Add how many {0} to trade.", "{0}をいくつ取引に追加しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupAskNumberTarget), nameof(DummyPopupAskNumberTarget.AskNumberAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUiTranslationPatch), nameof(TradeScreenUiTranslationPatch.Prefix))));

            _ = DummyPopupAskNumberTarget.AskNumberAsync("Add how many lead slug to trade.", 2, 0, 5).GetAwaiter().GetResult();

            Assert.That(DummyPopupAskNumberTarget.LastMessage, Is.EqualTo("lead slugをいくつ取引に追加しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesTradeSomePrompt_PreservesColorTags_WhenPatched()
    {
        WriteDictionary(
            ("Add how many {0} to trade.", "{0}をいくつ取引に出す？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupAskNumberTarget), nameof(DummyPopupAskNumberTarget.AskNumberAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUiTranslationPatch), nameof(TradeScreenUiTranslationPatch.Prefix))));

            _ = DummyPopupAskNumberTarget.AskNumberAsync("Add how many {{R|lead slug}} to trade.", 2, 0, 5).GetAwaiter().GetResult();

            Assert.That(DummyPopupAskNumberTarget.LastMessage, Is.EqualTo("{{R|lead slug}}をいくつ取引に出す？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TradeSomePrompt_FallsBackToEnglish_WhenKeyMissing()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupAskNumberTarget), nameof(DummyPopupAskNumberTarget.AskNumberAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUiTranslationPatch), nameof(TradeScreenUiTranslationPatch.Prefix))));

            _ = DummyPopupAskNumberTarget.AskNumberAsync("Add how many lead slug to trade.", 2, 0, 5).GetAwaiter().GetResult();

            Assert.That(DummyPopupAskNumberTarget.LastMessage, Is.EqualTo("Add how many lead slug to trade."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesLegacyReadout_WhenPatched()
    {
        WriteDictionary(
            (" drams", " ドラム"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLegacyTradeUiTarget), nameof(DummyLegacyTradeUiTarget.UpdateTotals)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUiTranslationPatch), nameof(TradeScreenUiTranslationPatch.Postfix))));

            DummyLegacyTradeUiTarget.UpdateTotals(
                Array.Empty<double>(),
                Array.Empty<int>(),
                Array.Empty<List<string>>(),
                Array.Empty<int[]>());

            Assert.That(DummyLegacyTradeUiTarget.sReadout, Is.EqualTo(" {{C|42}} ドラム <-> {{C|10}} ドラム ÄÄ {{W|$50}} "));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ExistingPopupTranslation_UsesTradeDictionary_ForLegacyVendorActions()
    {
        WriteDictionary(
            ("select an action", "操作を選択"),
            ("Add to trade", "取引に追加"));

        Assert.Multiple(() =>
        {
            Assert.That(
                PopupTranslationPatch.TranslatePopupTextForProducerRoute("select an action", nameof(TradeScreenUiTranslationPatch)),
                Is.EqualTo("操作を選択"));
            Assert.That(
                PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute("Add to trade", nameof(TradeScreenUiTranslationPatch)),
                Is.EqualTo("取引に追加"));
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
            Path.Combine(tempDirectory, "trade-ui-test.ja.json"),
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
