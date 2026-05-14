using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EngulfingTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase(
        "The {{Y|slumberling}} tries to engulf you, but fails.",
        "{{Y|slumberling}}はあなたを飲み込もうとしたが、失敗した。")]
    [TestCase(
        "The {{Y|slumberling}} tries to engulf {{C|snapjaw scavenger}}, but fails.",
        "{{Y|slumberling}}は{{C|snapjaw scavenger}}を飲み込もうとしたが、失敗した。")]
    public void Engulfing_TranslatesEngulfFailureMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void Engulfing_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|slumberling}} tries to engulf you, but fails.";
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
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Engulfing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The slumberling tries to engulf you, but fails."),
            "The slumberling tries to engulf you, but fails.");
    }

    [TestCase("")]
    [TestCase("The slumberling tries to engulf you.")]
    [TestCase("The slumberling engulfs snapjaw scavenger.")]
    public void Engulfing_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(source, source);
    }

    private static void AssertQueuedMessage(
        string source,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyEngulfingTarget.MessageToSend = source;
            DummyEngulfingTarget.ColorToSend = expectedColor;
            DummyEngulfingTarget.Engulf(new object(), null);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyEngulfingTarget.Reset();
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
        harmony.Patch(
            original: RequireMethod(typeof(DummyEngulfingTarget), nameof(DummyEngulfingTarget.Engulf), typeof(object), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(EngulfingTranslationPatch), nameof(EngulfingTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(EngulfingTranslationPatch), nameof(EngulfingTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyEngulfingTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Engulf(object who, object? e)
        {
            _ = who;
            _ = e;
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
