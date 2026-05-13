using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationRewardPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        nameof(DummyConversationRewardProducer.AddSlynthCandidateHandleEvent),
        "{{Y|Grit Gate}} is now a sanctuary option for the slynth.",
        "{{Y|Grit Gate}}がスリンスの聖域候補になった。",
        "SlynthSanctuary")]
    [TestCase(
        nameof(DummyConversationRewardProducer.AddSlynthCandidateHandleEvent),
        "{{Y|the salt dunes}} are now a sanctuary option for the slynth.",
        "{{Y|the salt dunes}}がスリンスの聖域候補になった。",
        "SlynthSanctuary")]
    [TestCase(
        nameof(DummyConversationRewardProducer.PaxInfectLimbInfectLimb),
        "You've contracted {{G|glowcrust}} on your left arm.",
        "left armに{{G|glowcrust}}を発症した。",
        "PaxInfectLimb")]
    [TestCase(
        nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent),
        "You receive {{Y|an electrobow}} and {{C|three lead slugs}}!",
        "{{Y|an electrobow}} and {{C|three lead slugs}}を受け取った！",
        "ReceiveItem")]
    public void Patch_TranslatesConversationRewardPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        RunWithOwnerAndPopupPatches(methodName, () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = source,
            };

            InvokeOwner(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You receive {{Y|an electrobow}}!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("ReceiveItem"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You've contracted {{G|glowcrust}} on your left arm.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.PaxInfectLimbInfectLimb), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = marked,
            };

            target.PaxInfectLimbInfectLimb();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("PaxInfectLimb"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = string.Empty,
            };

            target.ReceiveItemHandleEvent();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The conversation ends.";

        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = source,
            };

            target.ReceiveItemHandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("ReceiveItem"), Is.Zero);
            });
        });
    }

    private static void RunWithOwnerAndPopupPatches(string methodName, Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ConversationRewardPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyConversationRewardProducer), methodName),
            action);
    }

    private static void InvokeOwner(DummyConversationRewardProducer target, string methodName)
    {
        _ = OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyConversationRewardProducer), methodName).Invoke(target, null);
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ConversationRewardPopupTranslationPatch), detail);
    }

    private sealed class DummyConversationRewardProducer
    {
        public string PopupMessageToShow = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AddSlynthCandidateHandleEvent()
        {
            return EmitPopup(nameof(AddSlynthCandidateHandleEvent));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool PaxInfectLimbInfectLimb()
        {
            return EmitPopup(nameof(PaxInfectLimbInfectLimb));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ReceiveItemHandleEvent()
        {
            return EmitPopup(nameof(ReceiveItemHandleEvent));
        }

        private bool EmitPopup(string route)
        {
            _ = route;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }
}
