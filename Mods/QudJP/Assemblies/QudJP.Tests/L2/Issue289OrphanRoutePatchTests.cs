using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class Issue289OrphanRoutePatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-issue289-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyMessageLogLineTarget.ResetMenuOptions();
        DummyTutorialManagerTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyMessageLogLineTarget.ResetMenuOptions();
        DummyTutorialManagerTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void HelpScreenPostfix_TranslatesMenuOptionsAndRenderedHotkeyBar_WhenPatched()
    {
        WriteDictionary(
            ("navigate", "移動"),
            ("Toggle Visibility", "表示を切り替え"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHelpScreenTarget), nameof(DummyHelpScreenTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HelpScreenTranslationPatch), nameof(HelpScreenTranslationPatch.Postfix))));

            var target = new DummyHelpScreenTarget();
            target.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(target.keyMenuOptions[0].Description, Is.EqualTo("移動"));
                Assert.That(target.keyMenuOptions[1].Description, Is.EqualTo("表示を切り替え"));
                Assert.That(target.hotkeyBar.choices[0].Description, Is.EqualTo("移動"));
                Assert.That(target.hotkeyBar.choices[1].Description, Is.EqualTo("表示を切り替え"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(HelpScreenTranslationPatch), "HelpScreen.MenuOption"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessageLogStatusScreenTabPostfix_TranslatesLongAndShortLabels_WhenPatched()
    {
        WriteDictionary(
            ("Message Log", "メッセージログ"),
            ("Log", "ログ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageLogStatusScreenTarget), nameof(DummyMessageLogStatusScreenTarget.GetTabString)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MessageLogStatusScreenTranslationPatch), nameof(MessageLogStatusScreenTranslationPatch.Postfix))));

            var longTarget = new DummyMessageLogStatusScreenTarget();
            var shortTarget = new DummyMessageLogStatusScreenTarget { CompactMode = true };

            Assert.Multiple(() =>
            {
                Assert.That(longTarget.GetTabString(), Is.EqualTo("メッセージログ"));
                Assert.That(shortTarget.GetTabString(), Is.EqualTo("ログ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(MessageLogStatusScreenTranslationPatch), "MessageLogStatusScreen.TabString"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessageLogLinePostfix_TranslatesExpandCollapseOptionsWithoutTouchingMessageText_WhenPatched()
    {
        WriteDictionary(
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageLogLineTarget), nameof(DummyMessageLogLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MessageLogLineTranslationPatch), nameof(MessageLogLineTranslationPatch.Postfix))));

            var target = new DummyMessageLogLineTarget();
            target.setData(new DummyMessageLogLineDataTarget { text = "You hit snapjaw for 7 damage." });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("You hit snapjaw for 7 damage."));
                Assert.That(DummyMessageLogLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyMessageLogLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(MessageLogLineTranslationPatch), "MessageLogLine.MenuOption"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TutorialManagerPrefix_TranslatesPopupTextAndContinueButton_WhenPatched()
    {
        WriteDictionary(
            ("Use ~Accept to continue.", "~Accept で続行せよ。"),
            ("[~Accept] Continue", "[~Accept] 続行"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTutorialManagerTarget),
                    nameof(DummyTutorialManagerTarget.ShowCIDPopupAsync),
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(Action),
                    }),
                prefix: new HarmonyMethod(RequireMethod(typeof(TutorialManagerTranslationPatch), nameof(TutorialManagerTranslationPatch.Prefix))));

            DummyTutorialManagerTarget.ShowCIDPopupAsync(
                    "RootCanvas",
                    "Use ~Accept to continue.",
                    "s",
                    "[~Accept] Continue")
                .GetAwaiter()
                .GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(DummyTutorialManagerTarget.LastPopupText, Is.EqualTo("~Accept で続行せよ。"));
                Assert.That(DummyTutorialManagerTarget.LastButtonText, Is.EqualTo("[~Accept] 続行"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerTranslationPatch), "TutorialManager.PopupText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TutorialManagerTranslationPatch), "TutorialManager.ButtonText"),
                    Is.EqualTo(1));
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

        File.WriteAllText(
            Path.Combine(tempDirectory, "issue289.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
