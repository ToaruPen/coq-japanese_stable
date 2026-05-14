using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SkillsAndPowersSelectNodePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase("You already have that skill.", "そのスキルはすでに習得している。", "AlreadyHave", nameof(DummyPopupShow.Show))]
    [TestCase("You already have that power.", "そのパワーはすでに習得している。", "AlreadyHave", nameof(DummyPopupShow.Show))]
    [TestCase("You must be initiated into this skill in order to learn it.", "このスキルを習得するには入門している必要がある。", "InitiationRequired", nameof(DummyPopupShow.Show))]
    [TestCase("You must be initiated into this power in order to learn it.", "このパワーを習得するには入門している必要がある。", "InitiationRequired", nameof(DummyPopupShow.Show))]
    [TestCase("You don't have enough skill points to buy that skill!", "そのスキルを購入するにはスキルポイントが足りない！", "NotEnoughSkillPoints", nameof(DummyPopupShow.Show))]
    [TestCase("You don't have enough skill points to buy that power!", "そのパワーを購入するにはスキルポイントが足りない！", "NotEnoughSkillPoints", nameof(DummyPopupShow.Show))]
    [TestCase("No implementation for XRL.World.Parts.Skill.LongBlades", "XRL.World.Parts.Skill.LongBladesの実装がない。", "NoImplementation", nameof(DummyPopupShow.Show))]
    [TestCase("Are you sure you want to buy Long Blade for {{C|150}} sp?", "Long Bladeを{{C|150}}SPで購入しますか？", "BuyConfirmation", nameof(DummyPopupShow.ShowYesNo))]
    public void Patch_TranslatesSelectNodePopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail,
        string popupSurface)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = popupSurface,
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(popupSurface), Is.EqualTo(expected));
                    Assert.That(RouteHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_PreservesColorMarkupInBuyConfirmation_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to buy {{G|Long Blade}} for {{C|150}} sp?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = nameof(DummyPopupShow.ShowYesNo),
                }.SelectNode();

                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("{{G|Long Blade}}を{{C|150}}SPで購入しますか？"));
            });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You already have that skill.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount("AlreadyHave"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You already have that skill.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("AlreadyHave"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = string.Empty,
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
                    Assert.That(RouteHitCount("AlreadyHave"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotClaimFixedRequiredSkillPrompt_WhenOwnerPatched()
    {
        const string source = "You do not have the skill associated with that power. Would you like to purchase the required skill?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = nameof(DummyPopupShow.ShowYesNoCancel),
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("BuyConfirmation"), Is.Zero);
                });
            });
    }

    private static string? LastPopupMessage(string popupSurface)
    {
        return string.Equals(popupSurface, nameof(DummyPopupShow.ShowYesNo), StringComparison.Ordinal)
            ? DummyPopupShow.LastShowYesNoMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static System.Reflection.MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummySkillsAndPowersSelectNodeTarget), nameof(DummySkillsAndPowersSelectNodeTarget.SelectNode));
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            detail);
    }
}
