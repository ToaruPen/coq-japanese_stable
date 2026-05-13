using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsWishImplantPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
        DummyCyberneticsWishImplantTarget.PopupMessageToShow = string.Empty;
        DummyCyberneticsWishImplantTarget.UseFailPopup = false;
    }

    [TestCase(
        "No blueprint by the name 'cybertorso' could be found.",
        "'cybertorso'というブループリントは見つからない。",
        "MissingBlueprint",
        true)]
    [TestCase(
        "The blueprint 'Steel Boots' is not a cybernetic.",
        "ブループリント'Steel Boots'はサイバネではない。",
        "NotCybernetic",
        true)]
    [TestCase(
        "No body part by the name 'left arm' could be found.",
        "'left arm'という身体部位は見つからない。",
        "MissingBodyPart",
        true)]
    [TestCase(
        "Your {{Y|left arm}} is implanted with {{G|night vision}}!",
        "{{Y|left arm}}に{{G|night vision}}を埋め込んだ！",
        "Implanted",
        false)]
    [TestCase(
        "Your {{Y|feet}} are implanted with {{G|motorized treads}}!",
        "{{Y|feet}}に{{G|motorized treads}}を埋め込んだ！",
        "Implanted",
        false)]
    public void WishImplant_TranslatesPopupMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail,
        bool useFailPopup)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsWishImplantPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyCyberneticsWishImplantTarget.PopupMessageToShow = source;
                DummyCyberneticsWishImplantTarget.UseFailPopup = useFailPopup;

                DummyCyberneticsWishImplantTarget.WishImplant("implant");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void WishImplant_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "No blueprint by the name 'cybertorso' could be found.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("MissingBlueprint"), Is.Zero);
        });
    }

    [Test]
    public void WishImplant_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string unmarked = "Your left arm is implanted with night vision!";
        var marked = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsWishImplantPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyCyberneticsWishImplantTarget.PopupMessageToShow = marked;

                DummyCyberneticsWishImplantTarget.WishImplant("implant");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(HitCount("Implanted"), Is.Zero);
                });
            });
    }

    [Test]
    public void WishImplant_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsWishImplantPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyCyberneticsWishImplantTarget.PopupMessageToShow = string.Empty;

                DummyCyberneticsWishImplantTarget.WishImplant("implant");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("Implanted"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyCyberneticsWishImplantTarget),
            nameof(DummyCyberneticsWishImplantTarget.WishImplant),
            typeof(string));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(CyberneticsWishImplantPopupTranslationPatch), detail);
    }

    private static class DummyCyberneticsWishImplantTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        public static bool UseFailPopup { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WishImplant(string argument)
        {
            _ = argument;
            if (UseFailPopup)
            {
                DummyPopupShow.ShowFail(PopupMessageToShow);
                return;
            }

            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
