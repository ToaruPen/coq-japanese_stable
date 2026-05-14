using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PrecognitionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
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
        nameof(DummyPrecognitionProducerTarget.FireEvent),
        "You peer into the future.",
        "未来を覗き込んだ。",
        "PeerIntoFuture")]
    [TestCase(
        nameof(DummyPrecognitionProducerTarget.FireEvent),
        "You sense a subtle psychic disturbance.",
        "かすかな精神的乱れを感じる。",
        "PsychicDisturbance")]
    [TestCase(
        nameof(DummyPrecognitionProducerTarget.FireEvent),
        "Your focus returns to the present.",
        "意識が現在に引き戻された。",
        "FocusReturns")]
    [TestCase(
        nameof(DummyPrecognitionProducerTarget.OnBeforeDie),
        "Your focus returns to the present.",
        "意識が現在に引き戻された。",
        "FocusReturns")]
    public void Precognition_TranslatesOwnerQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerQueuedMessage(methodName, source, expected, detail);
    }

    [Test]
    public void Precognition_PreservesQueuedMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummyPrecognitionProducerTarget.FireEvent),
            "You peer into the future.",
            "未来を覗き込んだ。",
            "PeerIntoFuture",
            color: "white");
    }

    [Test]
    public void Precognition_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You peer into the future.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("white"));
                Assert.That(HitCount("PeerIntoFuture"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Precognition_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "You peer into the future.";

        AssertOwnerQueuedMessage(
            nameof(DummyPrecognitionProducerTarget.FireEvent),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "PeerIntoFuture",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You are already within a precognitive vision.")]
    [TestCase("You cannot access someone else's precognitive vision.")]
    [TestCase("Your precognition is about to run out. Would you like to return to the start of your vision?")]
    [TestCase("You sense your imminent demise. Would you like to return to the start of your vision?")]
    public void Precognition_LeavesPopupAndUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerQueuedMessage(
            nameof(DummyPrecognitionProducerTarget.FireEvent),
            source,
            source,
            "PeerIntoFuture",
            expectedHits: 0);
    }

    private static void AssertOwnerQueuedMessage(
        string methodName,
        string source,
        string expected,
        string detail,
        string? color = null,
        int expectedHits = 1)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, methodName);

            DummyPrecognitionProducerTarget.MessageToSend = source;
            DummyPrecognitionProducerTarget.ColorToSend = color;
            InvokeOwner(methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            DummyPrecognitionProducerTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void InvokeOwner(string methodName)
    {
        if (methodName == nameof(DummyPrecognitionProducerTarget.OnBeforeDie))
        {
            var turnsLeft = 1;
            var hitpointsAtSave = 100;
            var temperatureAtSave = 25;
            var activatedSegment = 0L;
            _ = DummyPrecognitionProducerTarget.OnBeforeDie(
                new object(),
                Guid.Empty,
                Guid.Empty,
                ref turnsLeft,
                ref hitpointsAtSave,
                ref temperatureAtSave,
                ref activatedSegment,
                wasPlayer: true,
                realityDistortionBased: false,
                mutation: new object());
            return;
        }

        _ = DummyPrecognitionProducerTarget.FireEvent(new object());
    }

    private static void PatchQueue(Harmony harmony)
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
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: RequireOwnerMethod(methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(PrecognitionTranslationPatch), nameof(PrecognitionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PrecognitionTranslationPatch), nameof(PrecognitionTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName == nameof(DummyPrecognitionProducerTarget.OnBeforeDie)
            ? RequireMethod(
                typeof(DummyPrecognitionProducerTarget),
                methodName,
                typeof(object),
                typeof(Guid),
                typeof(Guid),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(long).MakeByRefType(),
                typeof(bool),
                typeof(bool),
                typeof(object))
            : RequireMethod(typeof(DummyPrecognitionProducerTarget), methodName, typeof(object));
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

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(PrecognitionTranslationPatch) + "." + detail);
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyPrecognitionProducerTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FireEvent(object e)
        {
            _ = e;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool OnBeforeDie(
            object gameObject,
            Guid revertAbilityId,
            Guid glimpseId,
            ref int turnsLeft,
            ref int hitpointsAtSave,
            ref int temperatureAtSave,
            ref long activatedSegment,
            bool wasPlayer,
            bool realityDistortionBased,
            object mutation)
        {
            _ = gameObject;
            _ = revertAbilityId;
            _ = glimpseId;
            _ = turnsLeft;
            _ = hitpointsAtSave;
            _ = temperatureAtSave;
            _ = activatedSegment;
            _ = wasPlayer;
            _ = realityDistortionBased;
            _ = mutation;
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
