using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed partial class Issue201OtherUiBindingPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-issue201-other-ui-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        DummyBookScreenTarget.ResetStaticMenuOptions();
        BookUI.Reset();
        DummyAbilityManagerLineTarget.ResetStaticMenuOptions();
        DummyPickGameObjectLineTarget.ResetStaticMenuOptions();
        DummyHighScoresScreenTarget.ResetStaticMenuOptions();
        PickGameObjectScreen.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        ChargenStructuredTextTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyBookScreenTarget.ResetStaticMenuOptions();
        BookUI.Reset();
        DummyAbilityManagerLineTarget.ResetStaticMenuOptions();
        DummyPickGameObjectLineTarget.ResetStaticMenuOptions();
        DummyHighScoresScreenTarget.ResetStaticMenuOptions();
        PickGameObjectScreen.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void BookScreenPrefix_TranslatesTitleAndMenuOptions_ForBookObjectOverload()
    {
        WriteDictionary(
            ("Codex of Leaves", "葉の宝典"),
            ("Previous Page", "前のページ"),
            ("Next Page", "次のページ"),
            ("Close book", "本を閉じる"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBookScreenTarget), nameof(DummyBookScreenTarget.showScreen), new[] { typeof(DummyBookTarget), typeof(string), typeof(Action<int>), typeof(Action<int>) }),
                prefix: new HarmonyMethod(RequireMethod(typeof(BookScreenTranslationPatch), nameof(BookScreenTranslationPatch.Prefix))));

            var target = new DummyBookScreenTarget();
            target.showScreen(new DummyBookTarget { Title = "Codex of Leaves" });

            Assert.Multiple(() =>
            {
                Assert.That(target.titleText.Text, Is.EqualTo("葉の宝典"));
                Assert.That(DummyBookScreenTarget.PREV_PAGE.Description, Is.EqualTo("前のページ"));
                Assert.That(DummyBookScreenTarget.PREV_PAGE.KeyDescription, Is.EqualTo("前のページ"));
                Assert.That(DummyBookScreenTarget.NEXT_PAGE.Description, Is.EqualTo("次のページ"));
                Assert.That(DummyBookScreenTarget.getItemMenuOptions[2].Description, Is.EqualTo("本を閉じる"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(BookScreenTranslationPatch), "BookScreen.TitleText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(BookScreenTranslationPatch), "BookScreen.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void BookScreenPrefix_TranslatesTitle_ForBookIdOverload()
    {
        WriteDictionary(("Leaf Journal", "葉の日誌"));
        BookUI.Books["leaf-journal"] = new DummyBookTarget { Title = "Leaf Journal" };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBookScreenTarget), nameof(DummyBookScreenTarget.showScreen), new[] { typeof(string), typeof(string), typeof(Action<int>), typeof(Action<int>) }),
                prefix: new HarmonyMethod(RequireMethod(typeof(BookScreenTranslationPatch), nameof(BookScreenTranslationPatch.Prefix))));

            var target = new DummyBookScreenTarget();
            target.showScreen("leaf-journal");

            Assert.That(target.titleText.Text, Is.EqualTo("葉の日誌"));
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

    private static MethodInfo RequireMethod(Type type, string methodName, Type[]? parameterTypes = null)
    {
        var method = parameterTypes is null
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
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

        var path = Path.Combine(tempDirectory, "issue201-other-ui.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteMutationsXml(params (string name, string displayName)[] entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<mutations>");
        foreach (var entry in entries)
        {
            builder.Append("  <mutation Name=\"");
            builder.Append(EscapeXml(entry.name));
            builder.Append("\" DisplayName=\"");
            builder.Append(EscapeXml(entry.displayName));
            builder.AppendLine("\" />");
        }

        builder.AppendLine("</mutations>");
        File.WriteAllText(
            Path.Combine(tempDirectory, "Mutations.jp.xml"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
