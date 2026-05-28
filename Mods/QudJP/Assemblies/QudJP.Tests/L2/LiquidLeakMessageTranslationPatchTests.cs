using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LiquidLeakMessageTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-liquid-leak-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        WriteLiquidDictionaries();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        nameof(DummyLiquidLeakTarget.LeakWhenBrokenDistributeLiquid),
        "The {{Y|broken canteen}} leaks 1 dram of {{B|water}}.",
        "{{Y|broken canteen}}から{{B|水}} 1ドラムが漏れ出た。")]
    [TestCase(
        nameof(DummyLiquidLeakTarget.LeaksFluidDistributeLiquid),
        "The {{Y|oozing vase}} leaks 2 drams of {{C|slime}}.",
        "{{Y|oozing vase}}から{{C|粘液}} 2ドラムが漏れ出た。")]
    [TestCase(
        nameof(DummyLiquidLeakTarget.LeaksFluidDistributeLiquid),
        "{{G|leaking pipes}} leak 2 drams of {{B|water}}.",
        "{{G|leaking pipes}}から{{B|水}} 2ドラムが漏れ出た。")]
    [TestCase(
        nameof(DummyLiquidLeakTarget.LeakWhenBrokenDistributeLiquid),
        "The 異様な装置 leaks 43 drams of {{g|algal}} {{B|water}}.",
        "異様な装置から{{g|藻質の}} {{B|水}} 43ドラムが漏れ出た。")]
    public void LiquidLeak_TranslatesQueuedMessage_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void LiquidLeak_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|broken canteen}} leaks 1 dram of {{B|water}}.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidLeak_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyLiquidLeakTarget.LeakWhenBrokenDistributeLiquid),
            MessageFrameTranslator.MarkDirectTranslation("翻訳済みの漏出メッセージ"),
            "翻訳済みの漏出メッセージ");
    }

    [TestCase("")]
    [TestCase("The canteen is sealed.")]
    [TestCase("{{G|leaking pipes}} drip 2 drams of {{B|water}}.")]
    [TestCase("2 drams of water pours out all over you.")]
    public void LiquidLeak_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(nameof(DummyLiquidLeakTarget.LeakWhenBrokenDistributeLiquid), source, source);
        AssertQueuedMessage(nameof(DummyLiquidLeakTarget.LeaksFluidDistributeLiquid), source, source);
    }

    private static void AssertQueuedMessage(string methodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyLiquidLeakTarget.MessageToSend = source;
            InvokeOwnerMethod(methodName);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyLiquidLeakTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyLiquidLeakTarget.LeakWhenBrokenDistributeLiquid),
            nameof(DummyLiquidLeakTarget.LeaksFluidDistributeLiquid),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLiquidLeakTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(LiquidLeakMessageTranslationPatch), nameof(LiquidLeakMessageTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(LiquidLeakMessageTranslationPatch), nameof(LiquidLeakMessageTranslationPatch.Finalizer), typeof(Exception))));
        }
    }

    private void WriteLiquidDictionaries()
    {
        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-liquids.ja.json"),
            "{\"entries\":[{\"key\":\"water\",\"context\":\"XRL.Liquids\",\"text\":\"水\"},{\"key\":\"slime\",\"context\":\"XRL.Liquids\",\"text\":\"粘液\"}]}\n");
        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-liquid-adjectives.ja.json"),
            "{\"entries\":[{\"key\":\"algal\",\"context\":\"XRL.Liquids.Adjective\",\"text\":\"藻質の\"}]}\n");
    }

    private static void InvokeOwnerMethod(string methodName)
    {
        _ = RequireMethod(typeof(DummyLiquidLeakTarget), methodName).Invoke(null, null);
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyLiquidLeakTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LeakWhenBrokenDistributeLiquid()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, null, Capitalize: false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool LeaksFluidDistributeLiquid()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, null, Capitalize: false);
            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
        }
    }
}
