using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ShortBladesShankTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        DummyShortBladesShankProducer.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        DummyShortBladesShankProducer.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You attempt to take advantage of the snapjaw's misfortune and shank them.",
        "あなたはthe snapjawの不運につけ込んで急所を突こうとした。")]
    [TestCase(
        "{{Y|The snapjaw}} attempts to take advantage of your misfortune and shank you.",
        "{{Y|The snapjaw}}はあなたの不運につけ込んで急所を突こうとした。")]
    public void Shank_TranslatesAttemptQueuedMessage_WhenOwnerPatched(string source, string expected)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            DummyShortBladesShankProducer.QueuedMessageToSend = source;
            DummyShortBladesShankProducer.ColorToSend = "red";

            DummyShortBladesShankProducer.Cast(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("red"));
                Assert.That(QueueHitCount("ShortBladesShankAttempt"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Shank_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You attempt to take advantage of the snapjaw's misfortune and shank them.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "red", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount("ShortBladesShankAttempt"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Shank_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "You attempt to take advantage of the snapjaw's misfortune and shank them.";

        WithPatchedOwnerAndQueue(() =>
        {
            DummyShortBladesShankProducer.QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(source);

            DummyShortBladesShankProducer.Cast(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount("ShortBladesShankAttempt"), Is.Zero);
            });
        });
    }

    [TestCase(
        "Are you sure you want to shank yourself?",
        "自分自身の急所を突きますか？")]
    [TestCase(
        "Are you sure you want to shank {{Y|yourself}}?",
        "{{Y|自分自身}}の急所を突きますか？")]
    public void Shank_TranslatesSelfConfirmationPopup_WhenOwnerPatched(string source, string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ShortBladesShankTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyShortBladesShankProducer.PopupMessageToShow = source;

                DummyShortBladesShankProducer.Cast(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                    Assert.That(PopupHitCount("ShortBladesShankSelfConfirmation"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Shank_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Are you sure you want to shank yourself?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowYesNo(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
            Assert.That(PopupHitCount("ShortBladesShankSelfConfirmation"), Is.Zero);
        });
    }

    [Test]
    public void Shank_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to shank yourself?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ShortBladesShankTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyShortBladesShankProducer.PopupMessageToShow =
                    MessageFrameTranslator.MarkDirectTranslation(source);

                DummyShortBladesShankProducer.Cast(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("ShortBladesShankSelfConfirmation"), Is.Zero);
                });
            });
    }

    [TestCase("")]
    [TestCase("There's nothing there you can shank.")]
    [TestCase("There's nothing there to shank.")]
    public void Shank_DoesNotClaimFixedOrEmptyPopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ShortBladesShankTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyShortBladesShankProducer.PopupMessageToShow = source;

                DummyShortBladesShankProducer.Cast(new DummyGameObject());

                Assert.That(PopupHitCount("ShortBladesShankSelfConfirmation"), Is.Zero);
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
                typeof(ShortBladesShankTranslationPatch),
                nameof(ShortBladesShankTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireOwnerMethod(),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(ShortBladesShankTranslationPatch),
                nameof(ShortBladesShankTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyShortBladesShankProducer),
            nameof(DummyShortBladesShankProducer.Cast),
            typeof(DummyGameObject),
            typeof(DummyShortBladesShankProducer),
            typeof(DummyGameObject));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.shortbladesshank." + Guid.NewGuid().ToString("N");
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(ShortBladesShankTranslationPatch) + "." + detail);
    }

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ShortBladesShankTranslationPatch), detail);
    }

    private sealed class DummyShortBladesShankProducer
    {
        public static string QueuedMessageToSend { get; set; } = string.Empty;
        public static string PopupMessageToShow { get; set; } = string.Empty;
        public static string? ColorToSend { get; set; }

        public static void Reset()
        {
            QueuedMessageToSend = string.Empty;
            PopupMessageToShow = string.Empty;
            ColorToSend = null;
        }

        public static void Cast(
            DummyGameObject parentObject,
            DummyShortBladesShankProducer? skill = null,
            DummyGameObject? weapon = null)
        {
            _ = parentObject;
            _ = skill;
            _ = weapon;

            if (!string.IsNullOrEmpty(QueuedMessageToSend))
            {
                DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
            }

            if (!string.IsNullOrEmpty(PopupMessageToShow))
            {
                DummyPopupShow.ShowYesNo(PopupMessageToShow);
            }
        }
    }
}
