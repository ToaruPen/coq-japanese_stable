using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HistoricEventRegionRevealPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "journal-patterns.ja.json"));

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
        JournalPatternTranslator.ResetForTests();
    }

    [TestCase(
        "You discover the location of {{Y|Omonporch}}.",
        "{{Y|Omonporch}}の場所を発見した。")]
    [TestCase(
        "You discover the location of the salt-stained ruins.",
        "the salt-stained ruinsの場所を発見した。")]
    public void PerformRegionReveal_TranslatesRegionRevealPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerPopup(source, expected);
    }

    [Test]
    public void PerformRegionReveal_DoesNotClaimRegionRevealPopup_WhenOwnerAbsent()
    {
        const string source = "You discover the location of {{Y|Omonporch}}.";

        var claimed = HistoricEventRegionRevealPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            nameof(HistoricEventRegionRevealPopupTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void PerformRegionReveal_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You discover the location of {{Y|Omonporch}}.";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [Test]
    public void PerformRegionReveal_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, expectedHits: 0);
    }

    [Test]
    public void PerformRegionReveal_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup("You discovered the hidden village of {{Y|Kyakukya}}.", "You discovered the hidden village of {{Y|Kyakukya}}.", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(HistoricEventRegionRevealPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                var target = new DummyHistoricEventRegionRevealTarget { PopupMessageToShow = source };

                target.PerformRegionReveal();

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
            typeof(DummyHistoricEventRegionRevealTarget),
            nameof(DummyHistoricEventRegionRevealTarget.PerformRegionReveal));
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText."
            + nameof(HistoricEventRegionRevealPopupTranslationPatch)
            + ".RegionRevealLocation");
    }
}
