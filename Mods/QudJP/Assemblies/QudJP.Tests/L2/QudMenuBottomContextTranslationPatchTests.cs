using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudMenuBottomContextTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-bottom-context-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        ResetTestState();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        ResetTestState();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesMenuItemText()
    {
        WriteDictionary(("Inspect", "調べる"));

        var context = new DummyQudMenuBottomContext("Inspect");
        RunRefreshButtonsWithPatch(context);

        Assert.Multiple(() =>
        {
            Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("調べる"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(QudMenuBottomContextTranslationPatch),
                    "Popup.ProducerMenuItem.Exact"),
                Is.GreaterThan(0));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(PopupTranslationPatch),
                    nameof(QudMenuBottomContextTranslationPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Inspect",
                    "Inspect"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarker_FromMenuItemText()
    {
        var context = new DummyQudMenuBottomContext("調べる");

        RunRefreshButtonsWithPatch(context);

        Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("調べる"));
    }

    [Test]
    public void Prefix_TranslatesAndFlattensNestedHotkeyLabel()
    {
        WriteScopedMenuActionDictionary(("back", "戻る"));

        var context = new DummyQudMenuBottomContext("{{y|{{W|[Esc]}} Back}}");

        RunRefreshButtonsWithPatch(context);

        Assert.Multiple(() =>
        {
            Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("{{W|[Esc]}} {{y|戻る}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(QudMenuBottomContextTranslationPatch),
                    "Popup.ProducerMenuItem.HotkeyLabel"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Prefix_FlattensNestedHotkeyLabel_WhenLabelIsUntranslated()
    {
        var context = new DummyQudMenuBottomContext("{{y|{{W|[Esc]}} Back}}");

        RunRefreshButtonsWithPatch(context);

        Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("{{W|[Esc]}} {{y|Back}}"));
    }

    [TestCase("{{y|{{W|[~Accept]}} Continue}}", "{{W|[~Accept]}} {{y|続ける}}")]
    [TestCase("{{y|{{W|[space]}} Continue}}", "{{W|[space]}} {{y|続ける}}")]
    public void Prefix_PreservesNestedHotkeyTokenAndBrackets(string source, string expected)
    {
        WriteScopedMenuActionDictionary(("continue", "続ける"));

        var context = new DummyQudMenuBottomContext(source);

        RunRefreshButtonsWithPatch(context);

        Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo(expected));
    }

    [Test]
    public void Prefix_PreservesMalformedNestedHotkeyLabelUnchanged()
    {
        var source = "{{y|{{W|Esc}} Back}}";
        var context = new DummyQudMenuBottomContext(source);

        Assert.DoesNotThrow(() => RunRefreshButtonsWithPatch(context));

        Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo(source));
    }

    [Test]
    public void Prefix_PreservesPopupMessageButtonColorTags()
    {
        WriteDictionary(
            ("{{W|[y]}} {{y|Yes}}", "{{W|[y]}} {{y|はい}}"),
            ("{{W|[n]}} {{y|No}}", "{{W|[n]}} {{y|いいえ}}"));

        var context = new DummyQudMenuBottomContext(
            "{{y|{{W|[y]}} Yes}}",
            "{{y|{{W|[n]}} No}}");

        RunRefreshButtonsWithPatch(context);

        Assert.Multiple(() =>
        {
            Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("{{W|[y]}} {{y|はい}}"));
            Assert.That(((DummyMenuItem)context.items[1]!).text, Is.EqualTo("{{W|[n]}} {{y|いいえ}}"));
        });
    }

    private static void RunRefreshButtonsWithPatch(DummyQudMenuBottomContext context)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQudMenuBottomContext), nameof(DummyQudMenuBottomContext.RefreshButtons)),
                prefix: new HarmonyMethod(RequireMethod(typeof(QudMenuBottomContextTranslationPatch), nameof(QudMenuBottomContextTranslationPatch.Prefix))));

            context.RefreshButtons();
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

    private static void ResetTestState()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile(Path.Combine(tempDirectory, "bottom-context.ja.json"), entries);
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private void WriteScopedMenuActionDictionary(params (string key, string text)[] entries)
    {
        var scopedDirectory = Path.Combine(tempDirectory, "Scoped");
        Directory.CreateDirectory(scopedDirectory);
        WriteDictionaryFile(Path.Combine(scopedDirectory, "ui-menu-actions.ja.json"), entries);
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private static void WriteDictionaryFile(string path, params (string key, string text)[] entries)
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

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class DummyQudMenuBottomContext
    {
        public IList items;

        public DummyQudMenuBottomContext(params string[] texts)
        {
            items = new ArrayList(texts.Select(static text => new DummyMenuItem(text)).ToArray());
        }

        public void RefreshButtons()
        {
        }
    }

    private sealed class DummyMenuItem
    {
        public string text;

        public DummyMenuItem(string text)
        {
            this.text = text;
        }
    }
}
