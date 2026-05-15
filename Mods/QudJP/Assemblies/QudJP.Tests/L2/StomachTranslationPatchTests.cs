using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class StomachTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "The moisture is sucked out of your body.",
        "体から水分が吸い出された。",
        "StomachMoistureBody")]
    [TestCase(
        "{{W|The moisture is sucked out of your body.}}",
        "{{W|体から水分が吸い出された。}}",
        "StomachMoistureBody")]
    [TestCase(
        "The moisture is sucked out of your throat.",
        "喉から水分が吸い出された。",
        "StomachMoistureThroat")]
    [TestCase(
        "&YThe moisture is sucked out of your throat.",
        "&Y喉から水分が吸い出された。",
        "StomachMoistureThroat")]
    public void AddWater_TranslatesDehydrationQueueMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyStomachProducer
            {
                MessageToSend = source,
                ColorToSend = "red",
            }.FireEvent(new DummyStomachEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("red"));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void AddWater_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The moisture is sucked out of your body.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "red", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount("StomachMoistureBody"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddWater_DoesNotRetranslateDirectMarkedQueueMessage_WhenOwnerPatched()
    {
        const string source = "The moisture is sucked out of your body.";

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyStomachProducer
            {
                MessageToSend = MessageFrameTranslator.MarkDirectTranslation(source),
            }.FireEvent(new DummyStomachEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount("StomachMoistureBody"), Is.Zero);
            });
        });
    }

    [Test]
    public void AddWater_DoesNotRetranslateUnknownDirectMarkedQueueMessage_WhenOwnerPatched()
    {
        const string source = "The stomach gurgles in an unfamiliar way.";

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyStomachProducer
            {
                MessageToSend = MessageFrameTranslator.MarkDirectTranslation(source),
            }.FireEvent(new DummyStomachEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount("StomachMoistureBody"), Is.Zero);
                Assert.That(HitCount("StomachMoistureThroat"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You drank way too much!")]
    [TestCase("Ugh, you feel sick.")]
    public void AddWater_DoesNotClaimFixedRuntimeOrEmptyQueueMessages_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyStomachProducer
            {
                MessageToSend = source,
            }.FireEvent(new DummyStomachEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount("StomachMoistureBody"), Is.Zero);
                Assert.That(HitCount("StomachMoistureThroat"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwnerAndQueue(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireOwnerMethod(),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(StomachTranslationPatch),
                nameof(StomachTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireOwnerMethod(),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(StomachTranslationPatch),
                nameof(StomachTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyStomachProducer),
            nameof(DummyStomachProducer.FireEvent),
            typeof(DummyStomachEvent));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.stomach." + Guid.NewGuid().ToString("N");
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(StomachTranslationPatch) + "." + detail);
    }

    private sealed class DummyStomachProducer
    {
        public string MessageToSend { get; set; } = string.Empty;
        public string? ColorToSend { get; set; }

        public void FireEvent(DummyStomachEvent e)
        {
            _ = e;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }
    }

    private sealed class DummyStomachEvent
    {
    }
}
