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
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-messagelog-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
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

    [Test]
    public void Prefix_TranslatesWeaponCombatMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You hit \\((x\\d+)\\) for (\\d+) damage with your (.+?)[.!] \\[(.+?)\\]$", "{2}で{1}ダメージを与えた。({0}) [{3}]"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You hit (x1) for 2 damage with your 青銅の短剣! [11]", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("青銅の短剣で2ダメージを与えた。(x1) [11]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPassByMessageWithoutArticle_WhenPatched()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You pass by ウォーターヴァイン.", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ウォーターヴァインのそばを通り過ぎた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesFreezingWeaponDamage_WhenPatched()
    {
        WritePatternDictionary(("^(.+?) takes (\\d+) damage from your freezing weapon![.!]?$", "{0}はあなたの凍てつく武器で{1}ダメージを受けた！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("タム takes 1 damage from your freezing weapon!", "&C", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("タムはあなたの凍てつく武器で1ダメージを受けた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDirectionalSeeAndStop_WhenPatched()
    {
        WritePatternDictionary((
            "^You see (.+?) to the (north|south|east|west|northeast|northwest|southeast|southwest) and stop moving[.!]?$",
            "{t1}に{0}が見えたので移動をやめた。"));
        WriteExactDictionary(("east", "東"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You see タム、ドロマド商人 to the east and stop moving.", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("東にタム、ドロマド商人が見えたので移動をやめた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathMessage_WhenPatched()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(("QudJP.DeathWrapper.KilledBy.Wrapped", "あなたは死んだ。\n\n{killer}に殺された。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You died.\n\nYou were killed by メフメット.", "&R", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたは死んだ。\n\nメフメットに殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesBittenToDeathMessage_WhenPatched()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(("QudJP.DeathWrapper.BittenToDeathBy.Wrapped", "あなたは死んだ。\n\n{killer}に噛み殺された。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You died.\n\nYou were bitten to death by the ウォーターヴァイン農家.", "&R", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたは死んだ。\n\nウォーターヴァイン農家に噛み殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarkerWithoutReapplyingPatterns()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("\u0001熊は防いだ。", "&W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("熊は防いだ。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesBareAccidentalDeathMessage_WhenPatched()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(("QudJP.DeathWrapper.AccidentallyKilledBy.Bare", "{killer}にうっかり殺された。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("You were accidentally killed by a ウォーターヴァイン農家.", "&R", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ウォーターヴァイン農家にうっかり殺された。"));
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

    private void WriteExactDictionary(params (string key, string text)[] entries)
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

        File.WriteAllText(Path.Combine(dictionaryDirectory, "ui-test.ja.json"), builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
