using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AutoActTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-autoact-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
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
    public void InterruptWithReason_TranslatesStopBecauseMessage_WhenPatched()
    {
        WriteDictionary(
            ("moving", "移動"));
        WritePatterns(
            ("^You stop (moving|resting|digging|gathering|attacking|acting|auto-exploring) because you can go no further[.!]?$", "これ以上進めないので{t0}をやめた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAutoActWithReasonTarget(harmony);
            PatchMessageQueue(harmony);

            var target = new DummyAutoActInterruptBecauseTarget
            {
                MessageToSend = "{{y|You stop moving because you can go no further.}}",
                ColorToSend = "y",
            };

            target.Interrupt("you can go no further");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{y|これ以上進めないので移動をやめた。}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InterruptWithObject_TranslatesSpotMessage_WhenPatched()
    {
        WriteDictionary(
            ("north", "北"),
            ("auto-exploring", "自動探索"));
        WritePatterns(
            ("^You see (?:a |an |the )?(.+?) to the (north|south|east|west|northeast|northwest|southeast|southwest) and stop (moving|resting|digging|gathering|attacking|acting|auto-exploring)[.!]?$", "{t1}に{0}が見えたので{t2}をやめた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAutoActWithObjectTarget(harmony);
            PatchMessageQueue(harmony);

            var target = new DummyAutoActInterruptObjectTarget
            {
                MessageToSend = "You see a snapjaw to the north and stop auto-exploring.",
            };

            target.Interrupt(new DummyGameObject { DisplayName = "snapjaw" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("北にsnapjawが見えたので自動探索をやめた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ResetAutoexploreProperties_TranslatesResetStatus_WhenPatched()
    {
        WritePatterns(
            ("^Resetting (.+?) on (.+?)$", "{1}上の{0}をリセットした。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAutoActResetTarget(harmony);
            PatchMessageQueue(harmony);

            var target = new DummyAutoActResetTarget
            {
                MessageToSend = "Resetting AutoexploreAction_, AutoexploreSuppression on snapjaw",
            };

            _ = target.ResetAutoexploreProperties();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjaw上のAutoexploreAction_, AutoexploreSuppressionをリセットした。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.autoact.{Guid.NewGuid():N}";
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

    private static void PatchMessageQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchAutoActWithReasonTarget(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyAutoActInterruptBecauseTarget), nameof(DummyAutoActInterruptBecauseTarget.Interrupt), typeof(string), typeof(DummyCell), typeof(DummyGameObject), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(AutoActTranslationPatch), nameof(AutoActTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(AutoActTranslationPatch), nameof(AutoActTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchAutoActWithObjectTarget(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyAutoActInterruptObjectTarget), nameof(DummyAutoActInterruptObjectTarget.Interrupt), typeof(DummyGameObject), typeof(bool), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(AutoActTranslationPatch), nameof(AutoActTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(AutoActTranslationPatch), nameof(AutoActTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchAutoActResetTarget(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyAutoActResetTarget), nameof(DummyAutoActResetTarget.ResetAutoexploreProperties)),
            prefix: new HarmonyMethod(RequireMethod(typeof(AutoActTranslationPatch), nameof(AutoActTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(AutoActTranslationPatch), nameof(AutoActTranslationPatch.Finalizer), typeof(Exception))));
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
            Path.Combine(tempDirectory, "ui-autoact-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WritePatterns(params (string pattern, string template)[] patterns)
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

        File.WriteAllText(
            patternFilePath,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
