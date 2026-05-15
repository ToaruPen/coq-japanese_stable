using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActionManagerRunSegmentTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "You cannot find a path to {{Y|the snapjaw}}.",
        "{{Y|snapjaw}}への経路が見つからない。",
        nameof(DummyPopupShow.Show),
        "PathToTarget")]
    [TestCase(
        "You cannot find a path toward the northeast.",
        "北東への経路が見つからない。",
        nameof(DummyPopupShow.Show),
        "PathTowardDirection")]
    [TestCase(
        "There are no stairways leading upward nearby.",
        "近くに上り階段はない。",
        nameof(DummyPopupShow.ShowFail),
        "NoNearby")]
    public void RunSegment_TranslatesOwnerPopups_WhenOwnerPatched(
        string source,
        string expected,
        string popupMethod,
        string detail)
    {
        WithPatchedOwner(() =>
        {
            new DummyActionManagerRunSegmentTarget
            {
                PopupMessageToShow = source,
                PopupMethod = popupMethod,
            }.RunSegment();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(PopupHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "You can't figure out how to safely reach the stairs from here.",
        "ここから階段へ安全に辿る経路が見つからない。",
        "SafeReachStairs")]
    [TestCase(
        "You will not auto-attack {{Y|the snapjaw}} because it is not hostile to you.",
        "{{Y|snapjaw}}は敵対していないので自動攻撃しない。",
        "AutoAttackNotHostile")]
    [TestCase(
        "You cannot see your target.",
        "目標が見えない。",
        "CannotSeeTarget")]
    [TestCase(
        "You can't find a way to navigate to {{Y|the snapjaw}}.",
        "{{Y|snapjaw}}への移動経路が見つからない。",
        "NavigateToTarget")]
    [TestCase(
        "You are unable to attack {{Y|the snapjaw}}.",
        "{{Y|snapjaw}}を攻撃できない。",
        "UnableToAttack")]
    [TestCase(
        "You can't seem to find a way to reach {{Y|the snapjaw}}.",
        "{{Y|snapjaw}}へ到達する経路が見つからない。",
        "ReachTarget")]
    public void RunSegment_TranslatesOwnerQueuedMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(() =>
        {
            new DummyActionManagerRunSegmentTarget
            {
                QueuedMessageToSend = source,
            }.RunSegment();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(expected)));
                Assert.That(QueueHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void RunSegment_DoesNotClaimOwnerMessages_WhenOwnerAbsent()
    {
        WithPatchedSinksOnly(() =>
        {
            var target = new DummyActionManagerRunSegmentTarget
            {
                PopupMessageToShow = "There are no stairways nearby.",
                PopupMethod = nameof(DummyPopupShow.ShowFail),
                QueuedMessageToSend = "You cannot see your target.",
            };

            target.RunSegment();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("There are no stairways nearby."));
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You cannot see your target."));
                Assert.That(PopupHitCount("NoNearby"), Is.Zero);
                Assert.That(QueueHitCount("CannotSeeTarget"), Is.Zero);
            });
        });
    }

    [Test]
    public void RunSegment_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        const string popupSource = "There are no stairways nearby.";
        const string queueSource = "You cannot see your target.";

        WithPatchedOwner(() =>
        {
            new DummyActionManagerRunSegmentTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(popupSource),
                PopupMethod = nameof(DummyPopupShow.ShowFail),
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(queueSource),
            }.RunSegment();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(popupSource));
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(queueSource));
                Assert.That(PopupHitCount("NoNearby"), Is.Zero);
                Assert.That(QueueHitCount("CannotSeeTarget"), Is.Zero);
            });
        });
    }

    [Test]
    public void RunSegment_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyActionManagerRunSegmentTarget
            {
                PopupMessageToShow = string.Empty,
                QueuedMessageToSend = string.Empty,
            }.RunSegment();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
                Assert.That(PopupHitCount("PathToTarget"), Is.Zero);
                Assert.That(QueueHitCount("CannotSeeTarget"), Is.Zero);
            });
        });
    }

    [TestCase("You cannot find a path to your destination.")]
    [TestCase("There doesn't seem to be anywhere else to explore.")]
    [TestCase("There is only darkness from an unusual source left to explore.")]
    [TestCase("There doesn't seem to be anywhere else to explore from here.")]
    public void RunSegment_DoesNotClaimDeferredFixedPopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyActionManagerRunSegmentTarget
            {
                PopupMessageToShow = source,
                PopupMethod = nameof(DummyPopupShow.Show),
            }.RunSegment();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(PopupHitCount("PathToTarget"), Is.Zero);
                Assert.That(PopupHitCount("PathTowardDirection"), Is.Zero);
                Assert.That(PopupHitCount("NoNearby"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchSinks(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedSinksOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchSinks(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchSinks(Harmony harmony)
    {
        var popupPrefix = new HarmonyMethod(RequireMethod(
            typeof(PopupShowTranslationPatch),
            nameof(PopupShowTranslationPatch.Prefix),
            typeof(string).MakeByRefType(),
            typeof(MethodBase)));

        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: popupPrefix);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowFail),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: popupPrefix);
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
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyActionManagerRunSegmentTarget), nameof(DummyActionManagerRunSegmentTarget.RunSegment)),
            prefix: new HarmonyMethod(RequireMethod(typeof(ActionManagerRunSegmentTranslationPatch), nameof(ActionManagerRunSegmentTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(ActionManagerRunSegmentTranslationPatch), nameof(ActionManagerRunSegmentTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static int PopupHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(ActionManagerRunSegmentTranslationPatch) + "." + detail);
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(ActionManagerRunSegmentTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        return AccessTools.Method(type, name, parameterTypes)
               ?? throw new InvalidOperationException($"{type.FullName}.{name} not found.");
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.action-manager-run-segment." + Guid.NewGuid().ToString("N");
    }

    private sealed class DummyActionManagerRunSegmentTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

        public string QueuedMessageToSend { get; set; } = string.Empty;

        public void RunSegment()
        {
            if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowFail), StringComparison.Ordinal))
            {
                DummyPopupShow.ShowFail(PopupMessageToShow);
            }
            else
            {
                DummyPopupShow.Show(PopupMessageToShow);
            }

            DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        }
    }
}
