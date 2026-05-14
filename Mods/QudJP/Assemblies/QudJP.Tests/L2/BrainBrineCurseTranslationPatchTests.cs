using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BrainBrineCurseTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        DynamicTextObservability.ResetForTests();
        StatusScreenPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        StatusScreenPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TestCase(
        "You learn the skill {{C|Long Blade Proficiency}}!",
        "{{C|長剣の習熟}}を習得した！")]
    [TestCase(
        "You gained the mutation {{G|Light Manipulation}}!",
        "変異{{G|光操作}}を得た！")]
    [TestCase(
        "You gained the defect {{R|Albino}}!",
        "欠陥{{R|アルビノ}}を得た！")]
    public void BrainBrineCurseGainChoice_TranslatesRewardPopups_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerPopup(source, expected, expectedHits: 1);
    }

    [Test]
    public void BrainBrineCurseGainChoice_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You gained the mutation {{G|Light Manipulation}}!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void BrainBrineCurseGainChoice_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You gained the mutation {{G|Light Manipulation}}!";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You gained the mutation.")]
    public void BrainBrineCurseGainChoice_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(BrainBrineCurseTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyBrainBrineCurseProducer
                {
                    PopupMessageToShow = source,
                }.GainChoice("test");

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
            typeof(DummyBrainBrineCurseProducer),
            nameof(DummyBrainBrineCurseProducer.GainChoice),
            typeof(string));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(BrainBrineCurseTranslationPatch), "RewardPopup");
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }

    private sealed class DummyBrainBrineCurseProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void GainChoice(string choice)
        {
            _ = choice;
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
