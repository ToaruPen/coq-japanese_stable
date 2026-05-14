using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ShortBladesHobbleTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You find a weakness in the snapjaw's defenses.",
        "the snapjawの防御に隙を見つけた。",
        "green",
        "ShortBladesHobblePlayerFindsWeakness")]
    [TestCase(
        "The snapjaw finds a weakness in your defenses.",
        "The snapjawがあなたの防御に隙を見つけた。",
        "red",
        "ShortBladesHobbleEnemyFindsWeakness")]
    public void Hobble_TranslatesWeaknessQueuedMessages_WhenOwnerPatched(
        string source,
        string expected,
        string color,
        string detail)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyShortBladesHobbleProducer
            {
                QueuedMessageToSend = source,
                ColorToSend = color,
            }.FireEvent(new DummyShortBladesEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(QueueHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Hobble_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You find a weakness in the snapjaw's defenses.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "green", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount("ShortBladesHobblePlayerFindsWeakness"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Hobble_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "You find a weakness in the snapjaw's defenses.";

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyShortBladesHobbleProducer
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(source),
            }.FireEvent(new DummyShortBladesEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount("ShortBladesHobblePlayerFindsWeakness"), Is.Zero);
            });
        });
    }

    [TestCase(
        "Are you sure you want to hobble yourself?",
        "自分自身を足止めしてもよいか？")]
    [TestCase(
        "Are you sure you want to hobble {{Y|yourself}}?",
        "{{Y|yourself}}を足止めしてもよいか？")]
    public void Hobble_TranslatesSelfConfirmationPopup_WhenOwnerPatched(string source, string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ShortBladesHobbleTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyShortBladesHobbleProducer
                {
                    PopupMessageToShow = source,
                }.FireEvent(new DummyShortBladesEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                    Assert.That(PopupHitCount("ShortBladesHobbleSelfConfirmation"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Hobble_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Are you sure you want to hobble yourself?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowYesNo(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
            Assert.That(PopupHitCount("ShortBladesHobbleSelfConfirmation"), Is.Zero);
        });
    }

    [Test]
    public void Hobble_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to hobble yourself?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ShortBladesHobbleTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyShortBladesHobbleProducer
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.FireEvent(new DummyShortBladesEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("ShortBladesHobbleSelfConfirmation"), Is.Zero);
                });
            });
    }

    [TestCase("")]
    [TestCase("You must have a short blade equipped in your primary hand to hobble.")]
    [TestCase("There's nothing there to hobble.")]
    public void Hobble_DoesNotClaimFixedOrEmptyPopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ShortBladesHobbleTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyShortBladesHobbleProducer
                {
                    PopupMessageToShow = source,
                }.FireEvent(new DummyShortBladesEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("ShortBladesHobbleSelfConfirmation"), Is.Zero);
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
                typeof(ShortBladesHobbleTranslationPatch),
                nameof(ShortBladesHobbleTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireOwnerMethod(),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(ShortBladesHobbleTranslationPatch),
                nameof(ShortBladesHobbleTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyShortBladesHobbleProducer),
            nameof(DummyShortBladesHobbleProducer.FireEvent),
            typeof(DummyShortBladesEvent));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.shortbladeshobble." + Guid.NewGuid().ToString("N");
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(ShortBladesHobbleTranslationPatch) + "." + detail);
    }

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ShortBladesHobbleTranslationPatch), detail);
    }

    private sealed class DummyShortBladesHobbleProducer
    {
        public string QueuedMessageToSend { get; set; } = string.Empty;
        public string PopupMessageToShow { get; set; } = string.Empty;
        public string? ColorToSend { get; set; }

        public void FireEvent(DummyShortBladesEvent e)
        {
            _ = e;

            if (!string.IsNullOrEmpty(QueuedMessageToSend))
            {
                DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
            }

            DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
    }

    private sealed class DummyShortBladesEvent
    {
    }
}
