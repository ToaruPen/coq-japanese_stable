using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationDisplayTextPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-conversation-display-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesKnownDisplayTextUnchanged_WhenPatched()
    {
        WriteDictionary(("Hello, traveler.", "旅人さん、こんにちは。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("Hello, traveler.");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("Hello, traveler."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_LogsUnclaimedDisplayText_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            const string source = "Hello, traveler.";
            var element = new DummyConversationElement(source);
            var result = element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(ConversationDisplayTextPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughUnknownDisplayText_WhenPatched()
    {
        WriteDictionary(("Known line", "既知の文"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("Unknown runtime line");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("Unknown runtime line"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Farewell", "さらば"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("Farewell");
            var result = element.GetDisplayText(withColor: true);

            Assert.That(result, Is.EqualTo("{{W|Farewell}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesEmptyResultUnchanged_WhenPatched()
    {
        WriteDictionary(("Placeholder", "プレースホルダー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(string.Empty);
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesNullResultUnchanged_WhenPatched()
    {
        WriteDictionary(("Placeholder", "プレースホルダー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(null!);
            string? result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.Null);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughAlreadyJapaneseText_WhenPatched()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("すでに日本語です");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("すでに日本語です"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("生きて飲め。 [End]", "生きて飲め。")]
    [TestCase("取引しよう。 [begin trade]", "取引しよう。")]
    [TestCase("お前の渇きは私の渇き、私の水はお前のものだ。 [begin water ritual; 1 dram of water]", "お前の渇きは私の渇き、私の水はお前のものだ。")]
    public void Postfix_StripsTrailingActionMarkers_WhenPatched(string source, string expected)
    {
        WriteDictionary(("Dummy", "ダミー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(source);
            var result = element.GetDisplayText(withColor: false);
            Assert.That(result, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughAlreadyJapaneseChoice_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("スティルトとは？");
            var result = element.GetDisplayText(withColor: false);
            Assert.That(result, Is.EqualTo("スティルトとは？"));
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

        var path = Path.Combine(tempDirectory, "conversation-display-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
