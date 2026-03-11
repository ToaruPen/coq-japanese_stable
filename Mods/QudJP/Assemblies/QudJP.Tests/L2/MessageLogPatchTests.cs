using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MessageLogPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-messagelog-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesHitMessage_WhenPatched()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You hit snapjaw for 7 damage.", "&w", Capitalize: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjawに7ダメージを与えた"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesColorCodes_WhenTranslating()
    {
        WritePatternDictionary(("^You miss (.+?)[.!]?$", "{0}への攻撃をはずした"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("{{G|You miss snapjaw.}}", "&y", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{G|snapjawへの攻撃をはずした}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PassesThroughUnmatchedMessage()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You begin moving.", "&c", Capitalize: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You begin moving."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesMultipleMessagesInSequence()
    {
        WritePatternDictionary(
            ("^You pick up (.+?)[.!]?$", "{0}を拾った"),
            ("^You drop (.+?)[.!]?$", "{0}を落とした"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You pick up copper nugget.", "&W", Capitalize: false);
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("copper nuggetを拾った"));

            DummyMessageQueue.AddPlayerMessage("You drop copper nugget.", "&W", Capitalize: false);
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("copper nuggetを落とした"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesIncomingDamageAndPreservesOtherArguments()
    {
        WritePatternDictionary(("^(.+) hits you for (\\d+) damage[.!]?$", "{0}の攻撃で{1}ダメージを受けた"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("snapjaw hits you for 5 damage!", "&R", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjawの攻撃で5ダメージを受けた"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("&R"));
                Assert.That(DummyMessageQueue.LastCapitalize, Is.False);
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

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
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
}
