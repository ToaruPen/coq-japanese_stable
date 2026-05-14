using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TabulaRasaeTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DynamicTextObservability.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase(
        nameof(DummyTabulaRasaeTarget.HandleBeforeApplyDamageEvent),
        "Your attack does not affect {{Y|Tabula Rasae}}.",
        "攻撃は{{Y|Tabula Rasae}}に影響を与えない。")]
    [TestCase(
        nameof(DummyTabulaRasaeTarget.HandleTookDamageEvent),
        "The Tabula Rasae adapt to heat damage.",
        "タブラ・ラサは熱ダメージに適応した。")]
    [TestCase(
        nameof(DummyTabulaRasaeTarget.ConfusionConfuse),
        "{{R|Your attack does not affect snapjaw.}}",
        "{{R|攻撃はsnapjawに影響を与えない。}}")]
    public void TabulaRasae_TranslatesOwnerMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertQueuedMessage(methodName, source, expected, expectedColor: "white");
    }

    [Test]
    public void TabulaRasae_TranslatesUnknownDamageAttribute_WithCapturedText()
    {
        AssertQueuedMessage(
            nameof(DummyTabulaRasaeTarget.HandleTookDamageEvent),
            "The Tabula Rasae adapt to {{C|cosmic}} damage.",
            "タブラ・ラサは{{C|cosmic}}ダメージに適応した。");
    }

    [Test]
    public void TabulaRasae_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "Your attack does not affect {{Y|Tabula Rasae}}.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TabulaRasae_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyTabulaRasaeTarget.HandleBeforeApplyDamageEvent),
            MessageFrameTranslator.MarkDirectTranslation("Your attack does not affect the Tabula Rasae."),
            "Your attack does not affect the Tabula Rasae.");
    }

    [TestCase("")]
    [TestCase("Your mental attack does not affect {{Y|Tabula Rasae}}.")]
    [TestCase("The Tabula Rasae adapt.")]
    public void TabulaRasae_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(
            nameof(DummyTabulaRasaeTarget.HandleBeforeApplyDamageEvent),
            source,
            source);
    }

    private static void AssertQueuedMessage(
        string ownerMethodName,
        string source,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, ownerMethodName);

            DummyTabulaRasaeTarget.MessageToSend = source;
            DummyTabulaRasaeTarget.ColorToSend = expectedColor;
            InvokeOwner(ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyTabulaRasaeTarget.Reset();
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

    private static void PatchOwner(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyTabulaRasaeTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(TabulaRasaeTranslationPatch), nameof(TabulaRasaeTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(TabulaRasaeTranslationPatch), nameof(TabulaRasaeTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void InvokeOwner(string methodName)
    {
        _ = methodName switch
        {
            nameof(DummyTabulaRasaeTarget.HandleBeforeApplyDamageEvent) => DummyTabulaRasaeTarget.HandleBeforeApplyDamageEvent(),
            nameof(DummyTabulaRasaeTarget.HandleTookDamageEvent) => DummyTabulaRasaeTarget.HandleTookDamageEvent(),
            nameof(DummyTabulaRasaeTarget.ConfusionConfuse) => DummyTabulaRasaeTarget.ConfusionConfuse(),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown owner method."),
        };
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

    private static class DummyTabulaRasaeTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HandleBeforeApplyDamageEvent()
        {
            return SendMessage();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HandleTookDamageEvent()
        {
            _ = nameof(HandleTookDamageEvent);
            return SendMessage();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ConfusionConfuse()
        {
            _ = nameof(ConfusionConfuse);
            return SendMessage();
        }

        private static bool SendMessage()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return false;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
