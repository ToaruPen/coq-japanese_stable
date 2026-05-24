using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LevelerTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "Your genome enters an excited state! Would you like to spend {{rules|4}} mutation points to buy a mutation before rapidly mutating?",
        "ゲノムが励起状態に入った！急速変異する前に{{rules|4}}変異ポイントを消費して変異を購入しますか？",
        "LevelerBuyMutationPrompt")]
    [TestCase(
        "Your genome enters an excited state! Would you like to spend {{rules|4}} mutation points to buy {{Y|an esper mutation}} before rapidly mutating?",
        "ゲノムが励起状態に入った！急速変異する前に{{rules|4}}変異ポイントを消費して{{Y|超能力変異}}を購入しますか？",
        "LevelerBuyMutationPrompt")]
    [TestCase(
        "Your genome enters an excited state! Would you like to spend {{rules|4}} mutation points to buy {{Y|a physical mutation}} before rapidly mutating?",
        "ゲノムが励起状態に入った！急速変異する前に{{rules|4}}変異ポイントを消費して{{Y|身体的変異}}を購入しますか？",
        "LevelerBuyMutationPrompt")]
    [TestCase(
        "You have rapidly advanced {{Y|Teleportation}} by 2 ranks to rank {{C|6}}!",
        "{{Y|Teleportation}}を2ランク急速に成長させ、ランク{{C|6}}に到達した！",
        "LevelerRapidAdvancement")]
    [TestCase(
        "You have no physical mutations to rapidly advance!",
        "急速に成長させられる身体的変異がない！",
        "LevelerNoPhysicalMutations")]
    public void Patch_TranslatesRapidAdvancementPopup_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source =
            "You have rapidly advanced {{Y|Teleportation}} by 2 ranks to rank {{C|6}}!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("LevelerRapidAdvancement"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You have no physical mutations to rapidly advance!";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "LevelerNoPhysicalMutations",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("The genome hums quietly.")]
    public void Patch_DoesNotClaimFixedOrEmptyPopup_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(LevelerTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyLevelerProducer
                {
                    PopupMessageToShow = source,
                }.RapidAdvancement(3, new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("LevelerBuyMutationPrompt"), Is.Zero);
                });
            });
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(LevelerTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyLevelerProducer
                {
                    PopupMessageToShow = source,
                }.RapidAdvancement(3, new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyLevelerProducer),
                   nameof(DummyLevelerProducer.RapidAdvancement),
                   [typeof(int), typeof(DummyGameObject)])
               ?? throw new MissingMethodException(
                   typeof(DummyLevelerProducer).FullName,
                   nameof(DummyLevelerProducer.RapidAdvancement));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(LevelerTranslationPatch), detail);
    }

    private sealed class DummyLevelerProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RapidAdvancement(int amount, DummyGameObject parentObject)
        {
            _ = amount;
            _ = parentObject;
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
