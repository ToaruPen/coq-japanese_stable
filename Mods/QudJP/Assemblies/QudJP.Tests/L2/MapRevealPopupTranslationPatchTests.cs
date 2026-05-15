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
    private const string FactionDeedAnnalsEntrySource =
        "You add the following entry into the {{K|Annals of Qud}}.\n\n\"On the 1st of Ubu Ut, {{Y|Ereshkigal}} became admired by {{M|the Barathrumites}} for saving their village.\"";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "{{Y|ancient map}} is not owned by you, and using {{Y|it}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{Y|ancient map}}はあなたのものではなく、{{Y|it}}を使うと{{Y|it}}は消費される。本当に行いますか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        "{{C|satchel}} are not owned by you, and using {{Y|the map}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{C|satchel}}はあなたのものではなく、{{Y|the map}}を使うと{{Y|it}}は消費される。本当に行いますか？",
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
        "{{Y|ancient deed}}はあなたのものではなく、{{Y|it}}を使うと{{Y|it}}は消費される。本当に行いますか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        "{{C|satchel}} are not owned by you, and using {{Y|the deed}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{C|satchel}}はあなたのものではなく、{{Y|the deed}}を使うと{{Y|it}}は消費される。本当に行いますか？",
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

    [TestCase(
        "You add the following entry into the {{K|Annals of Qud}}.\n\n\"On the 1st of Ubu Ut, {{Y|Ereshkigal}} became admired by {{M|the Barathrumites}} for saving their village.\"",
        "{{K|クッド年代記}}に次の項目を追加した。\n\n「Ubu Utの1st、{{Y|Ereshkigal}}はsaving their villageにより{{M|the Barathrumites}}から敬愛されるようになった。」")]
    [TestCase(
        "You add the following entry into the {{K|Annals of Qud}}.\n\n\"On the 2nd of Tuum Ut, {{Y|Ereshkigal}} became despised by {{M|the Barathrumites}} for betraying their village.\"",
        "{{K|クッド年代記}}に次の項目を追加した。\n\n「Tuum Utの2nd、{{Y|Ereshkigal}}はbetraying their villageにより{{M|the Barathrumites}}から嫌悪されるようになった。」")]
    public void FactionDeedHandleEvent_TranslatesAnnalsEntryPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerTranslation(FactionDeedOwner, source, expected, "FactionDeedAnnalsEntry");
    }

    [Test]
    public void FactionDeedHandleEvent_DoesNotTranslateAnnalsEntry_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.Show(FactionDeedAnnalsEntrySource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(FactionDeedAnnalsEntrySource));
            Assert.That(RouteHitCount("FactionDeedAnnalsEntry"), Is.Zero);
        });
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

        AssertOwnerPopup(MapRevealOwner, marked, source);
        Assert.Multiple(() =>
        {
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(MapRevealOwner, string.Empty, string.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
            Assert.That(RouteHitCount("MapOfSurroundings"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The map remains inscrutable.";

        AssertOwnerPopup(MapRevealOwner, source, source);
        Assert.Multiple(() =>
        {
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
            Assert.That(RouteHitCount("MapOfSurroundings"), Is.Zero);
        });
    }

    [TestCase("The operation fails.")]
    [TestCase("It's a map of your surroundings!")]
    public void FactionDeedHandleEvent_DoesNotClaimFixedNarrativeOrMapSpecificPopups_WhenOwnerPatched(
        string source)
    {
        AssertOwnerPopup(FactionDeedOwner, source, source);
        Assert.Multiple(() =>
        {
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
            Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
            Assert.That(RouteHitCount("MapOfSurroundings"), Is.Zero);
        });
    }

    private static void AssertOwnerTranslation(string ownerKey, string source, string expected, string expectedDetail)
    {
        AssertOwnerPopup(ownerKey, source, expected);
        Assert.That(RouteHitCount(expectedDetail), Is.EqualTo(1));
    }

    private static void AssertOwnerPopup(string ownerKey, string source, string expected)
    {
        var ownerRoute = CreateOwnerRouteFromKey(ownerKey);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(MapRevealPopupTranslationPatch),
            ownerRoute.Method,
            () =>
            {
                ownerRoute.Invoke(() => DummyPopupShow.Show(source));

                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            });
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(MapRevealPopupTranslationPatch), detail);
    }

    private static DynamicOwnerRouteMethod CreateOwnerRouteFromKey(string ownerKey)
    {
        var separator = ownerKey.LastIndexOf('|');
        return DynamicOwnerRouteMethod.Create(ownerKey[..separator], ownerKey[(separator + 1)..]);
    }
}
