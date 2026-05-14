using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RandomAltarBaetylTranslationPatchTests
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
        "I ACCEPT YOUR OFFERING!\n\nThe sparking baetyl gives you {{Y|a carbide dagger}}!",
        "捧げ物を受け取った！\n\nsparking baetylは{{Y|a carbide dagger}}を授けた！")]
    public void Patch_TranslatesRewardPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertOwnerPopup(source, expected, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "I ACCEPT YOUR OFFERING!\n\nThe sparking baetyl gives you a carbide dagger!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "I ACCEPT YOUR OFFERING!\n\nThe sparking baetyl gives you a carbide dagger!";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("I AM SATED, MORTAL. BEGONE.")]
    [TestCase("PETTY MORTAL! BRING ME {{Y|a copper nugget}}, AND I SHALL REWARD YOU WITH a carbide dagger.")]
    [TestCase("PETTY MORTAL! BRING ME {{Y|a copper nugget}}, AND I SHALL REWARD YOU WITH a carbide dagger.\n\nOffer the sparking baetyl the {{Y|copper nugget}} nearby?")]
    public void Patch_DoesNotClaimFixedRuntimeOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(RandomAltarBaetylTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyRandomAltarBaetylProducer
                {
                    PopupMessageToShow = source,
                }.BaetylWantsSacrifice();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyRandomAltarBaetylProducer),
                   nameof(DummyRandomAltarBaetylProducer.BaetylWantsSacrifice),
                   [])
               ?? throw new MissingMethodException(
                   typeof(DummyRandomAltarBaetylProducer).FullName,
                   nameof(DummyRandomAltarBaetylProducer.BaetylWantsSacrifice));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(RandomAltarBaetylTranslationPatch),
            "RandomAltarBaetylRewardPopup");
    }

    private sealed class DummyRandomAltarBaetylProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void BaetylWantsSacrifice()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
