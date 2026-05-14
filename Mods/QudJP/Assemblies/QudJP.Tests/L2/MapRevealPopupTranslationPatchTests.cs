using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MapRevealPopupTranslationPatchTests
{
    private const string MapRevealOwner = "XRL.World.Parts.MapReveal|HandleEvent";
    private const string FactionDeedOwner = "XRL.World.Parts.FactionDeed|HandleEvent";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "{{Y|ancient map}} is not owned by you, and using {{Y|it}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{Y|ancient map}}はあなたのものではなく、{{Y|it}}を使うと{{Y|it}}は消費される。本当に行うか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        "{{C|satchel}} are not owned by you, and using {{Y|the map}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{C|satchel}}はあなたのものではなく、{{Y|the map}}を使うと{{Y|it}}は消費される。本当に行うか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        "{{Y|ancient map}} seems to be behaving as nothing more than an ordinary piece of paper.",
        "{{Y|ancient map}}は普通の紙切れとしてしか振る舞っていないようだ。",
        "OrdinaryPaper")]
    [TestCase(
        "It's a map of your surroundings!",
        "周囲の地図だ！",
        "MapOfSurroundings")]
    [TestCase(
        "They're a map of your surroundings!",
        "周囲の地図だ！",
        "MapOfSurroundings")]
    public void HandleEvent_TranslatesInventoriedPopupMessages_WhenOwnerPatched(
        string source,
        string expected,
        string expectedDetail)
    {
        AssertOwnerTranslation(MapRevealOwner, source, expected, expectedDetail);
    }

    [TestCase(
        "{{Y|ancient deed}} is not owned by you, and using {{Y|it}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{Y|ancient deed}}はあなたのものではなく、{{Y|it}}を使うと{{Y|it}}は消費される。本当に行うか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        "{{C|satchel}} are not owned by you, and using {{Y|the deed}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{C|satchel}}はあなたのものではなく、{{Y|the deed}}を使うと{{Y|it}}は消費される。本当に行うか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        "{{Y|ancient deed}} seems to be behaving as nothing more than an ordinary piece of paper.",
        "{{Y|ancient deed}}は普通の紙切れとしてしか振る舞っていないようだ。",
        "OrdinaryPaper")]
    public void FactionDeedHandleEvent_TranslatesSharedDocumentPopupMessages_WhenOwnerPatched(
        string source,
        string expected,
        string expectedDetail)
    {
        AssertOwnerTranslation(FactionDeedOwner, source, expected, expectedDetail);
    }

    [Test]
    public void HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "{{Y|ancient map}} is not owned by you, and using {{Y|it}} will consume {{Y|it}}. Are you sure you want to do so?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowYesNoCancel(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "{{Y|ancient map}} seems to be behaving as nothing more than an ordinary piece of paper.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        Assert.That(
            MapRevealPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                marked,
                MapRevealOwner,
                nameof(PopupShowTranslationPatch),
                "MapRevealPopup",
                out var translated),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        Assert.That(
            MapRevealPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                string.Empty,
                MapRevealOwner,
                nameof(PopupShowTranslationPatch),
                "MapRevealPopup",
                out var translated),
            Is.False);
        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void HandleEvent_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The map remains inscrutable.";

        Assert.That(
            MapRevealPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                source,
                MapRevealOwner,
                nameof(PopupShowTranslationPatch),
                "MapRevealPopup",
                out var translated),
            Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
            Assert.That(RouteHitCount("MapOfSurroundings"), Is.Zero);
        });
    }

    [TestCase("The operation fails.")]
    [TestCase("You add the following entry into the {{K|Annals of Qud}}.\n\n\"On the 1st of Ubu Ut, {{Y|Ereshkigal}} became admired by {{M|the Barathrumites}} for saving their village.\"")]
    [TestCase("It's a map of your surroundings!")]
    public void FactionDeedHandleEvent_DoesNotClaimFixedNarrativeOrMapSpecificPopups_WhenOwnerPatched(
        string source)
    {
        Assert.That(
            MapRevealPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                source,
                FactionDeedOwner,
                nameof(PopupShowTranslationPatch),
                "MapRevealPopup",
                out var translated),
            Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
            Assert.That(RouteHitCount("MapOfSurroundings"), Is.Zero);
        });
    }

    private static void AssertOwnerTranslation(string ownerKey, string source, string expected, string expectedDetail)
    {
        Assert.That(
            MapRevealPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                source,
                ownerKey,
                nameof(PopupShowTranslationPatch),
                "MapRevealPopup",
                out var translated),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(RouteHitCount(expectedDetail), Is.EqualTo(1));
        });
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(MapRevealPopupTranslationPatch), detail);
    }
}
