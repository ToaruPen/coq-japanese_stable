using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SpindleNegotiationTranslationPatchTests
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

    [TestCase(
        "The delegate for {{C|the Fellowship of Wardens}} says, 'Live and drink, wanderer. We won't forget this.'",
        "{{C|the Fellowship of Wardens}}の代表は言う。「生きて水を飲め、wanderer。私たちはこのことを忘れない。」",
        "DelegateGratitude")]
    [TestCase(
        "The delegate for {{C|the Fellowship of Wardens}} gives you {{Y|an etched handbone}}!",
        "{{C|the Fellowship of Wardens}}の代表はあなたに{{Y|an etched handbone}}をくれた！",
        "DelegateGivesHeirloom")]
    [TestCase(
        "The delegate for {{C|the Consortium of Phyta}} says, 'Betrayer! May you choke on your own spittle! We won't forget this.'",
        "{{C|the Consortium of Phyta}}の代表は言う。「裏切り者め！自分の唾で窒息するがいい！私たちはこのことを忘れない。」",
        "DelegateBetrayed")]
    [TestCase(
        "You yell, 'I cannot believe {{C|the Fellowship of Wardens}} don't despise {{C|the Consortium of Phyta}} for stealing their heirlooms.'",
        "あなたは叫ぶ。「{{C|the Fellowship of Wardens}}がstealing their heirloomsのことで{{C|the Consortium of Phyta}}を軽蔑していないなんて信じられない。」",
        "ChaosSpielAccusation")]
    [TestCase(
        "Due to your revelation, {{C|the Fellowship of Wardens}} change their opinion of {{C|the Consortium of Phyta}}.",
        "あなたの暴露により、{{C|the Fellowship of Wardens}}は{{C|the Consortium of Phyta}}への評価を変えた。",
        "ChaosSpielOpinionChanged")]
    [TestCase(
        "The council will be convened! Come back in 1 day.",
        "評議会は招集される！1日後に戻ってこい。",
        "CouncilConvenes")]
    [TestCase(
        "The council will be convened! Come back in 3 days.",
        "評議会は招集される！3日後に戻ってこい。",
        "CouncilConvenes")]
    public void Patch_TranslatesSpindleNegotiationPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SpindleNegotiationTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySpindleNegotiationTarget
                {
                    PopupMessageToShow = source,
                }.FireEvent(new DummyEvent { Id = "BeginSpindleNegotiation" });

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(RouteHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The council will be convened! Come back in 3 days.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount("CouncilConvenes"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "The council will be convened! Come back in 3 days.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SpindleNegotiationTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySpindleNegotiationTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.FireEvent(new DummyEvent { Id = "BeginSpindleNegotiation" });

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("CouncilConvenes"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SpindleNegotiationTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySpindleNegotiationTarget().FireEvent(new DummyEvent { Id = "BeginSpindleNegotiation" });

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
                    Assert.That(RouteHitCount("CouncilConvenes"), Is.Zero);
                });
            });
    }

    [TestCase("The pact is struck. The Barathrumites may lease control of the Spindle, and all the attending factions owe a debt to Asphodel.")]
    [TestCase("The pact is struck. The Barathrumites may lease control the Spindle.")]
    [TestCase("You ponder how best to sow chaos with your words.")]
    [TestCase("Asphodel yells, '{{R|You ruined the First Council of Omonporch, you barbaric lout!}}'")]
    [TestCase("You don't have enough allied factions. Come back when you're favored by {{C|4}} or more factions.")]
    public void Patch_DoesNotClaimFixedSpindlePopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SpindleNegotiationTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySpindleNegotiationTarget
                {
                    PopupMessageToShow = source,
                }.FireEvent(new DummyEvent { Id = "BeginSpindleNegotiation" });

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("CouncilConvenes"), Is.Zero);
                });
            });
    }

    private static System.Reflection.MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummySpindleNegotiationTarget),
            nameof(DummySpindleNegotiationTarget.FireEvent),
            typeof(DummyEvent));
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(SpindleNegotiationTranslationPatch), detail);
    }
}
