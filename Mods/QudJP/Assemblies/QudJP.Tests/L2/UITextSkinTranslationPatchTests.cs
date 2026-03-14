using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UITextSkinTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-uitextskin-l2", Guid.NewGuid().ToString("N"));
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
    public void Prefix_TranslatesKnownKey()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Hello";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("こんにちは"));
    }

    [Test]
    public void Prefix_PassesThroughUnknownText()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Unknown text";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("Unknown text"));
    }

    [Test]
    public void Prefix_PreservesColorCodes()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "{{W|Hello}}";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("{{W|こんにちは}}"));
    }

    [TestCase("[Esc]")]
    [TestCase("[Space]")]
    [TestCase("[]")]
    [TestCase("[Esc] Cancel")]
    [TestCase("SP: 99")]
    [TestCase("1.0.4\nbuild 2.0.210.24")]
    [TestCase("quit")]
    public void Prefix_SkipsKnownObservabilityNoiseTokens(string text)
    {
        WriteDictionary(("quit", "終了"), ("[Esc]", "[Esc-JP]"));

        var original = text;
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo(original));
            Assert.That(Translator.GetMissingKeyHitCountForTests(original), Is.EqualTo(0));
        });
    }

    [TestCase("クラシック")]
    [TestCase("チュートリアル\n[A]")]
    [TestCase("：ゲームモードを選択：")]
    [TestCase(" >{{K| . . . . . . . ■ .  . . . . . . . ■")]
    [TestCase("   ")]
    public void TranslatePreservingColors_SkipsAlreadyLocalizedUITextSinkText(string text)
    {
        WriteDictionary(("クラシック", "CLASSIC-JP"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotSuppressJapaneseTextForNonSinkContexts()
    {
        var text = "クラシック";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(CharGenLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [TestCase("新しいゲーム", nameof(MainMenuLocalizationPatch))]
    [TestCase("：プレイ方式を選択：", nameof(CharGenLocalizationPatch))]
    [TestCase("チュートリアル\n[A]", nameof(CharGenLocalizationPatch))]
    [TestCase("Caves of Qud の基礎を学ぶ。", nameof(CharGenLocalizationPatch))]
    [TestCase("有機生命体の史料庫と照合する微小なグラフェンアレイ。\n\n生物クリーチャーの正確なHP・AV・DVを参照できる。", nameof(CharGenLocalizationPatch))]
    public void TranslatePreservingColors_SkipsAlreadyLocalizedDirectRouteText(string text, string context)
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, context);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [TestCase("光学バイオスキャナ (Face)")]
    [TestCase("+1 Toughness")]
    [TestCase("Stinger (Confusing Venom)")]
    public void TranslatePreservingColors_KeepsObservingMixedLanguageDirectRouteText(string text)
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(CharGenLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(1));
        });
    }

    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharGenLocalizationPatch), "QudMutationsModule", "QudCyberneticsModule")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharGenLocalizationPatch), "EmbarkBuilder")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(MainMenuLocalizationPatch), "Qud.UI.MainMenu")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(OptionsLocalizationPatch), "Qud.UI.OptionsScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(PopupTranslationPatch), "Qud.UI.Popup")]
    public void ResolveObservabilityContext_ReclassifiesKnownSinkStacks(
        string originalContext,
        string expectedContext,
        params string[] stackTypeNames)
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(originalContext, stackTypeNames);

        Assert.That(resolvedContext, Is.EqualTo(expectedContext));
    }

    [TestCase("Points Remaining: 12")]
    [TestCase("Your Strength score determines how effectively you penetrate armor.")]
    [TestCase("ù +2 Ego\nù Proselytize\n")]
    public void ResolveObservabilityContext_ReclassifiesKnownCharGenTextPatterns(string source)
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(
            nameof(UITextSkinTranslationPatch),
            source,
            "Some.Unrelated.Widget");

        Assert.That(resolvedContext, Is.EqualTo(nameof(CharGenLocalizationPatch)));
    }

    [Test]
    public void ResolveObservabilityContext_LeavesUnknownSinkStackUntouched()
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(
            nameof(UITextSkinTranslationPatch),
            "Some.Unrelated.Widget");

        Assert.That(resolvedContext, Is.EqualTo(nameof(UITextSkinTranslationPatch)));
    }

    [Test]
    public void Prefix_HandlesNullOrEmpty()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var emptyText = string.Empty;
        UITextSkinTranslationPatch.Prefix(ref emptyText);

        Assert.That(emptyText, Is.EqualTo(string.Empty));
    }

    [Test]
    public void HarmonyPatch_AppliesPrefix_ToDummyUITextSkin()
    {
        WriteDictionary(("World", "世界"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyUITextSkin), nameof(DummyUITextSkin.SetText)),
                prefix: new HarmonyMethod(RequireMethod(typeof(UITextSkinTranslationPatch), nameof(UITextSkinTranslationPatch.Prefix))));

            var dummy = new DummyUITextSkin();
            dummy.SetText("World");

            Assert.That(dummy.Text, Is.EqualTo("世界"));
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

        var path = Path.Combine(tempDirectory, "ui-textskin.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
