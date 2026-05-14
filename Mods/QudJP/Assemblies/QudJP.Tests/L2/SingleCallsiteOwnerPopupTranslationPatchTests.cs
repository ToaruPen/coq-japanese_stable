using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SingleCallsiteOwnerPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));
        DummyPopupShow.Reset();
        DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms),
        "That is out of range (3 squares)",
        "範囲外だ（3マス）。",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms),
        "That is out of range (1 square)",
        "範囲外だ（1マス）。",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish),
        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
        "{{C|oil}}の報酬として{{Y|folded carbide axe}}を生成した。",
        "BaetylRewardWish",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess),
        "Are you sure you want to dismember yourself?",
        "yourselfを切断してもよいか？",
        "AxeDismemberSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "Are you sure you want to slam yourself?",
        "yourselfを叩きつけてもよいか？",
        "CudgelSlamSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization),
        "Argyve is already your follower. Do you want to proselytize him anyway?",
        "Argyveはすでにあなたの仲間だ。それでも勧誘するか？",
        "ProselytizeFollowerConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe),
        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
        "ひらめきを得て{{Y|laser pistol schematic}}を記した。",
        "TinkeringLearnRecipe",
        PopupMethod.Show)]
    public void Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                InvokeOwnerMethod(methodName, source);

                Assert.Multiple(() =>
                {
                    Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "That is out of range (3 squares)",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
        "BaetylRewardWish",
        PopupMethod.Show)]
    [TestCase(
        "Are you sure you want to dismember yourself?",
        "AxeDismemberSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "Are you sure you want to slam yourself?",
        "CudgelSlamSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "Argyve is already your follower. Do you want to proselytize him anyway?",
        "ProselytizeFollowerConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
        "TinkeringLearnRecipe",
        PopupMethod.Show)]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent(
        string source,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => ShowPopup(source, popupMethod));

        Assert.Multiple(() =>
        {
            Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish), marked);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("BaetylRewardWish"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), string.Empty);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("DecoyHologramOutOfRange"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyAxeDismember),
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.Cast) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyCudgelSlam),
                    typeof(string),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(int),
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(int),
                    typeof(int)),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method."),
        };
    }

    private static void InvokeOwnerMethod(string methodName, string message)
    {
        switch (methodName)
        {
            case nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.CreateHolograms(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish("@Melee Weapons {tier}R");
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.CastForceSuccess(
                    new DummyGameObject(),
                    new DummyAxeDismember(),
                    new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.Cast):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.Cast(
                    new DummyGameObject(),
                    new DummyCudgelSlam(),
                    null,
                    new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.AttemptProselytization();
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe(new DummyGameObject(), 1, 4);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method.");
        }
    }

    private static void ShowPopup(string source, PopupMethod popupMethod)
    {
        if (popupMethod == PopupMethod.ShowYesNo)
        {
            _ = DummyPopupShow.ShowYesNo(source);
            return;
        }

        DummyPopupShow.Show(source);
    }

    private static string? GetLastPopupMessage(PopupMethod popupMethod)
    {
        return popupMethod == PopupMethod.ShowYesNo
            ? DummyPopupShow.LastShowYesNoMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(SingleCallsiteOwnerPopupTranslationPatch), detail);
    }

    public enum PopupMethod
    {
        Show,
        ShowYesNo,
    }
}
