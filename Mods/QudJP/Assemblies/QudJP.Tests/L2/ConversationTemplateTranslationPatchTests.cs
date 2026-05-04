using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationTemplateTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-conversation-template-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyConversationApiTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyConversationApiTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesGeneratedVillageTinkerTemplate_BeforePlayerVariablesAreReplaced()
    {
        const string source =
            "Need a gadget repaired or identified, =player.formalAddressTerm=? Or if you're a tinker =player.reflexive=, perhaps you'd like to peruse my schematics?";
        const string translated =
            "修理や鑑定が必要なガジェットはあるかい？ それとも君自身が工匠なら、設計図を見ていくかい？";

        WriteDictionary((source, translated + " =player.formalAddressTerm= =player.reflexive="), ("Live and drink, tinker.", "生きて飲め、工匠。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyConversationApiTarget),
                    nameof(DummyConversationApiTarget.AddSimpleConversationToObject),
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(ConversationSimpleTemplateTranslationPatch), nameof(ConversationSimpleTemplateTranslationPatch.Prefix))));

            DummyConversationApiTarget.AddSimpleConversationToObject(
                new object(),
                source,
                "Live and drink, tinker.",
                Filter: null,
                FilterExtras: null,
                Append: null,
                ClearLost: false,
                ClearOriginal: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyConversationApiTarget.LastText, Is.EqualTo(translated));
                Assert.That(DummyConversationApiTarget.LastGoodbye, Is.EqualTo("生きて飲め、工匠。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationTemplateTranslator),
                        "ConversationTemplate.Exact"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesQuestionOverloadFields_WhenPatched()
    {
        WriteDictionary(
            ("Hello, =player.formalAddressTerm=.", "こんにちは。 =player.formalAddressTerm="),
            ("Live and drink.", "生きて飲め。"),
            ("Why?", "なぜ？"),
            ("Because.", "そういうものだ。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyConversationApiTarget),
                    nameof(DummyConversationApiTarget.AddSimpleConversationToObject),
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(ConversationQuestionTemplateTranslationPatch), nameof(ConversationQuestionTemplateTranslationPatch.Prefix))));

            DummyConversationApiTarget.AddSimpleConversationToObject(
                new object(),
                "Hello, =player.formalAddressTerm=.",
                "Live and drink.",
                Question: "Why?",
                Answer: "Because.",
                Filter: null,
                FilterExtras: null,
                Append: null,
                ClearLost: false,
                ClearOriginal: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyConversationApiTarget.LastText, Is.EqualTo("こんにちは。"));
                Assert.That(DummyConversationApiTarget.LastGoodbye, Is.EqualTo("生きて飲め。"));
                Assert.That(DummyConversationApiTarget.LastQuestion, Is.EqualTo("なぜ？"));
                Assert.That(DummyConversationApiTarget.LastAnswer, Is.EqualTo("そういうものだ。"));
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

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] argumentTypes)
    {
        return (argumentTypes.Length == 0
                ? AccessTools.Method(type, methodName)
                : AccessTools.Method(type, methodName, argumentTypes))
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

        var path = Path.Combine(tempDirectory, "conversation-template-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
