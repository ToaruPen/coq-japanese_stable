using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SingleCallsiteOwnerQueueTranslationPatchTests
{
    private const string ModMorphogeneticOwner = "XRL.World.Parts.ModMorphogenetic|ApplyMorphicShock";
    private const string WeirdwireConduitOwner = "XRL.World.Quests.WeirdwireConduitSystem|HandleEvent";

    [SetUp]
    public void SetUp()
    {
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
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
        "A weird, painful shock reverberates through you.",
        "「奇妙で痛い電撃」が全身を駆け抜けた。",
        "ModMorphogeneticPainfulShock")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
        "A weird shock reverberates through you.",
        "「奇妙な電撃」が全身を駆け抜けた。",
        "ModMorphogeneticPainlessShock")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
        "You now have 37 feet of copper wire.",
        "銅線を37フィート持っている。",
        "WeirdwireCopperWireTotal")]
    public void SingleCallsiteOwnerQueue_TranslatesOwnerMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerQueuedMessage(methodName, source, expected, detail);
    }

    [Test]
    public void SingleCallsiteOwnerQueue_PreservesQueuedMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "You now have 37 feet of copper wire.",
            "銅線を37フィート持っている。",
            "WeirdwireCopperWireTotal",
            color: "c");
    }

    [Test]
    public void SingleCallsiteOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You now have 37 feet of copper wire.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "c", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("c"));
                Assert.That(HitCount("WeirdwireCopperWireTotal"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SingleCallsiteOwnerQueue_DoesNotTranslateWrongOwnerMessage_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
            "You now have 37 feet of copper wire.",
            "You now have 37 feet of copper wire.",
            "WeirdwireCopperWireTotal",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "A weird, painful shock reverberates through you.",
            "A weird, painful shock reverberates through you.",
            "ModMorphogeneticPainfulShock",
            expectedHits: 0);
    }

    [Test]
    public void SingleCallsiteOwnerQueue_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "You now have 37 feet of copper wire.";

        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "WeirdwireCopperWireTotal",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You now have copper wire.")]
    [TestCase("A weird shock reverberates nearby.")]
    public void SingleCallsiteOwnerQueue_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            source,
            source,
            "WeirdwireCopperWireTotal",
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
        var ownerRoute = CreateOwnerRoute(methodName);
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, ownerRoute.Method);

            ownerRoute.Invoke(() => DummyMessageQueue.AddPlayerMessage(source, color, Capitalize: false));

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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

    private static void PatchOwner(Harmony harmony, MethodInfo ownerMethod)
    {
        harmony.Patch(
            original: ownerMethod,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(SingleCallsiteOwnerQueueTranslationPatch),
                nameof(SingleCallsiteOwnerQueueTranslationPatch.Prefix),
                typeof(MethodBase))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(SingleCallsiteOwnerQueueTranslationPatch),
                nameof(SingleCallsiteOwnerQueueTranslationPatch.Finalizer),
                typeof(Exception))));
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
            nameof(SingleCallsiteOwnerQueueTranslationPatch) + "." + detail);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static DynamicOwnerRouteMethod CreateOwnerRoute(string methodName)
    {
        return methodName switch
        {
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock) => CreateOwnerRouteFromKey(ModMorphogeneticOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent) => CreateOwnerRouteFromKey(WeirdwireConduitOwner),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method."),
        };
    }

    private static DynamicOwnerRouteMethod CreateOwnerRouteFromKey(string ownerKey)
    {
        var separator = ownerKey.LastIndexOf('|');
        return DynamicOwnerRouteMethod.Create(ownerKey[..separator], ownerKey[(separator + 1)..]);
    }

    private static class DummySingleCallsiteOwnerQueueTarget
    {
        public static bool ApplyMorphicShock() => true;

        public static bool HandleWeirdwireTookEvent() => true;
    }
}
