using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class XDidYTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryPath = null!;
    private string? lastMessage;
    private bool lastUsePopup;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-xdidy-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryPath = Path.Combine(tempDirectory, "verbs.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "ui-test.ja.json"), "{\"entries\":[]}\n", Utf8WithoutBom);
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(dictionaryPath);
        XDidYTranslationPatch.SetMessageDispatcherForTests((_, message, _, usePopup) =>
        {
            lastMessage = message;
            lastUsePopup = usePopup;
        });
        DummyXDidYTarget.Reset();
        lastMessage = null;
        lastUsePopup = false;
    }

    [TearDown]
    public void TearDown()
    {
        XDidYTranslationPatch.SetMessageDispatcherForTests(null);
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidYAndSkipsOriginalEnglishAssembly()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "block",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊は防いだ。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PromotesUsePopupFromDialogWhenHeldByPlayer()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var playerHolder = new DummyVisibilityTarget(isPlayer: true);
        var actor = new DummyVisibilityTarget(holder: playerHolder);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: actor,
                Verb: "block",
                SubjectOverride: "熊",
                AlwaysVisible: true,
                FromDialog: true,
                UsePopup: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたの熊は防いだ。"));
                Assert.That(lastUsePopup, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_SuppressesInvisibleMessageBeforeEnglishAssembly()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var hiddenSource = new DummyVisibilityTarget(isVisible: false);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "block",
                SubjectOverride: "熊",
                Source: hiddenSource,
                AlwaysVisible: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidYToZWithObjectTemplate()
    {
        WriteDictionary(tier2: new[] { ("stare", "at {0} menacingly", "{0}を睨みつけた") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "stare",
                Preposition: "at",
                Object: "タム",
                Extra: "menacingly",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊はタムを睨みつけた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXDidYToZWadeThroughLiquid()
    {
        WriteDictionary(tier3: new[] { ("wade", "through {0}", "{0}の中をかき分けて進んだ") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidYToZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYToZForTests))));

            DummyXDidYTarget.XDidYToZ(
                Actor: null,
                Verb: "wade",
                Preposition: "through",
                Object: "塩気のある水たまり",
                SubjectOverride: "あなた",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001あなたは塩気のある水たまりの中をかき分けて進んだ。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesWDidXToYWithZWithTemplate()
    {
        WriteDictionary(tier3: new[] { ("strike", "{0} with {1} for {2} damage", "{1}で{0}に{2}ダメージを与えた") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.WDidXToYWithZ)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixWDidXToYWithZForTests))));

            DummyXDidYTarget.WDidXToYWithZ(
                Actor: null,
                Verb: "strike",
                DirectPreposition: null,
                DirectObject: "スナップジョー",
                IndirectPreposition: "with",
                IndirectObject: "青銅の短剣",
                Extra: "for 5 damage",
                SubjectOverride: "熊",
                AlwaysVisible: true,
                EndMark: "!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.False);
                Assert.That(lastMessage, Is.EqualTo("\u0001熊は青銅の短剣でスナップジョーに5ダメージを与えた！"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_FallsBackToOriginalWhenVerbLookupIsMissing()
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyXDidYTarget), nameof(DummyXDidYTarget.XDidY)),
                prefix: new HarmonyMethod(RequireMethod(typeof(XDidYTranslationPatch), nameof(XDidYTranslationPatch.PrefixXDidYForTests))));

            DummyXDidYTarget.XDidY(
                Actor: null,
                Verb: "teleport",
                SubjectOverride: "熊",
                AlwaysVisible: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyXDidYTarget.OriginalExecuted, Is.True);
                Assert.That(lastMessage, Is.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionary(
        IEnumerable<(string verb, string text)>? tier1 = null,
        IEnumerable<(string verb, string extra, string text)>? tier2 = null,
        IEnumerable<(string verb, string extra, string text)>? tier3 = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [],");
        builder.AppendLine("  \"tier1\": [");
        WriteTier1(builder, tier1);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier2\": [");
        WriteTier2(builder, tier2);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier3\": [");
        WriteTier2(builder, tier3);
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        File.WriteAllText(dictionaryPath, builder.ToString(), Utf8WithoutBom);
    }

    private static void WriteTier1(StringBuilder builder, IEnumerable<(string verb, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static void WriteTier2(StringBuilder builder, IEnumerable<(string verb, string extra, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"extra\": \"")
                .Append(EscapeJson(entry.extra))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.xdidy.{Guid.NewGuid():N}";
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
