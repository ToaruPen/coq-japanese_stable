using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FabricateFromSelfTranslationPatchTests
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
        "You fabricate a lead slug from the substance of your body.",
        "あなたはあなたの体の物質からa lead slugを作製した。")]
    [TestCase(
        "The chromeling fabricates 20 HE Missiles from debris and scraps.",
        "The chromelingはdebris and scrapsから20 HE Missilesを作製した。")]
    [TestCase(
        "{{Y|The pyramid}} excavates a large boulder from the substance of its body.",
        "{{Y|The pyramid}}はその体の物質からa large boulderを掘り出した。")]
    public void Patch_TranslatesFabricationQueuedMessage_WhenOwnerPatched(string source, string expected)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyFabricateFromSelfProducer
            {
                QueuedMessageToSend = source,
                ColorToSend = "green",
            }.Activate(automatic: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("green"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You fabricate a lead slug from the substance of your body.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "green", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "You fabricate a lead slug from the substance of your body.";

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyFabricateFromSelfProducer
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(source),
            }.Activate(automatic: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("Nothing happens.")]
    [TestCase("Your health is too weak to do that.")]
    public void Patch_DoesNotClaimFixedOrEmptyMessage_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyFabricateFromSelfProducer
            {
                QueuedMessageToSend = source,
            }.Activate(automatic: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(HitCount(), Is.Zero);
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
                typeof(FabricateFromSelfTranslationPatch),
                nameof(FabricateFromSelfTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireOwnerMethod(),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(FabricateFromSelfTranslationPatch),
                nameof(FabricateFromSelfTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyFabricateFromSelfProducer),
            nameof(DummyFabricateFromSelfProducer.Activate),
            typeof(bool));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.fabricatefromself." + Guid.NewGuid().ToString("N");
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(FabricateFromSelfTranslationPatch) + ".FabricateFromSelfActivate");
    }

    private sealed class DummyFabricateFromSelfProducer
    {
        public string QueuedMessageToSend { get; set; } = string.Empty;
        public string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Activate(bool automatic = false)
        {
            _ = automatic;
            DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
            return true;
        }
    }
}
