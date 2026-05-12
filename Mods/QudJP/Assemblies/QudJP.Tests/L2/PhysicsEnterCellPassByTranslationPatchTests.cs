using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PhysicsEnterCellPassByTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-passby-producer-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesPassByMessageBeforeMessageLogSink_WhenPatched()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PhysicsEnterCellPassByTranslationPatch), nameof(PhysicsEnterCellPassByTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));

            DummyMessageQueue.AddPlayerMessage("You pass by ウォーターヴァイン.", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ウォーターヴァインのそばを通り過ぎた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AggregateMessageQueuePatch_PreservesPrefixOrder_WhenPatched()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            var original = RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixPhysicsEnterCellPassBy),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixZoneManagerSetActiveZone),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixCombatAndLog),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixMessageLog),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));

            DummyMessageQueue.AddPlayerMessage("You pass by ウォーターヴァイン.", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ウォーターヴァインのそばを通り過ぎた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AggregateMessageQueuePatch_TranslatesEnterCellPassByUsingRepositoryPattern_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            var original = RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixPhysicsEnterCellPassBy),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixMessageLog),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));

            var target = new DummyPhysicsEnterCellTarget();
            target.PassingBy.Add("ウォーターヴァイン");

            target.EnterCell();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ウォーターヴァインのそばを通り過ぎた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AggregateMessageQueuePatch_PassesThroughEmptyAndDirectMarkedMessages_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            var original = RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixPhysicsEnterCellPassBy),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(MessageQueueTranslationPatch),
                    nameof(MessageQueueTranslationPatch.PrefixMessageLog),
                    typeof(string).MakeByRefType(),
                    typeof(string),
                    typeof(bool))));

            DummyMessageQueue.AddPlayerMessage(string.Empty, "&W", Capitalize: false);
            var empty = DummyMessageQueue.LastMessage;

            const string unmarked = "You pass by ウォーターヴァイン.";
            DummyMessageQueue.AddPlayerMessage(MessageFrameTranslator.MarkDirectTranslation(unmarked), "&W", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(empty, Is.EqualTo(string.Empty));
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(unmarked));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PhysicsEnterCellPassByTranslationPatch),
                        "PassBy"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PassesThroughEnglishWhenPatternDoesNotMatch_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PhysicsEnterCellPassByTranslationPatch), nameof(PhysicsEnterCellPassByTranslationPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));

            DummyMessageQueue.AddPlayerMessage("You pass by ウォーターヴァイン.", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You pass by ウォーターヴァイン."));
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

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WritePatternDictionary(params (string pattern, string template)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(entries[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(entries[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void UseRepositoryPatternDictionary()
    {
        var localizationRoot = Path.GetFullPath(
            Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        MessagePatternTranslator.SetPatternFileForTests(null);
    }
}
