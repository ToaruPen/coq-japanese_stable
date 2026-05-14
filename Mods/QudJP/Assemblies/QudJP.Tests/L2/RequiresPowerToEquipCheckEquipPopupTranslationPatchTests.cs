using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RequiresPowerToEquipCheckEquipPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "Your {{Y|floating glowsphere}} stops operating; you unequip it.",
        "{{Y|floating glowsphere}}は動作を停止した。あなたはそれを外した。")]
    [TestCase(
        "Your {{Y|rocket skates}} stop operating; you unequip them.",
        "{{Y|rocket skates}}は動作を停止した。あなたはそれらを外した。")]
    public void CheckEquip_TranslatesPowerLossUnequipPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerPopup(source, expected);
    }

    [Test]
    public void CheckEquip_DoesNotClaimPowerLossUnequipPopup_WhenOwnerAbsent()
    {
        const string source = "Your {{Y|floating glowsphere}} stops operating; you unequip it.";

        var claimed = RequiresPowerToEquipCheckEquipPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            nameof(RequiresPowerToEquipCheckEquipPopupTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void CheckEquip_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Your {{Y|floating glowsphere}} stops operating; you unequip it.";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [Test]
    public void CheckEquip_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, expectedHits: 0);
    }

    [Test]
    public void CheckEquip_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup("Your {{Y|floating glowsphere}} stops operating.", "Your {{Y|floating glowsphere}} stops operating.", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(RequiresPowerToEquipCheckEquipPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                var target = new DummyRequiresPowerToEquipTarget { PopupMessageToShow = source };

                target.CheckEquip();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyRequiresPowerToEquipTarget),
            nameof(DummyRequiresPowerToEquipTarget.CheckEquip));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(RequiresPowerToEquipCheckEquipPopupTranslationPatch),
            "PowerLossUnequip");
    }
}
