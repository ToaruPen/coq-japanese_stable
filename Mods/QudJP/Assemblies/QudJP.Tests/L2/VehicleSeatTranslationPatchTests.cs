using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VehicleSeatTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "Accessing the pilot console requires the permanent insertion of {{Y|a cybernetic credit wedge}}.\n\nAre you sure you want to proceed?",
        "操縦コンソールへアクセスするには{{Y|サイバネティック・クレジットウェッジ}}を恒久的に挿入する必要がある。\n\n続行しますか？",
        "VehicleSeatPilotConsoleConfirmation")]
    [TestCase(
        "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.",
        "操縦コンソールへアクセスするにはサイバネティック・クレジットウェッジを恒久的に挿入する必要がある。",
        "VehicleSeatPilotConsoleRequirement")]
    public void Patch_TranslatesPilotConsoleRequirementPopup_WhenOwnerPatched(
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
            "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("VehicleSeatPilotConsoleRequirement"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source =
            "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "VehicleSeatPilotConsoleRequirement",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("Unrelated pilot console sentence.")]
    public void Patch_DoesNotClaimFixedOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, "VehicleSeatPilotConsoleRequirement", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits)
    {
        VehicleSeatTranslationPatch.Prefix();
        try
        {
            OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
                () =>
                {
                    new DummyVehicleSeatProducer
                    {
                        PopupMessageToShow = source,
                    }.AttemptPilot(new DummyGameObject());

                    Assert.Multiple(() =>
                    {
                        Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                        Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                    });
                });
        }
        finally
        {
            VehicleSeatTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void Patch_TargetMethodFixture_TranslatesWhenOwnerMethodIsPatched()
    {
        const string source =
            "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(VehicleSeatTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyVehicleSeatProducer
                {
                    PopupMessageToShow = source,
                }.AttemptPilot(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowMessage,
                        Is.EqualTo("操縦コンソールへアクセスするにはサイバネティック・クレジットウェッジを恒久的に挿入する必要がある。"));
                    Assert.That(HitCount("VehicleSeatPilotConsoleRequirement"), Is.EqualTo(1));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyVehicleSeatProducer),
                   nameof(DummyVehicleSeatProducer.AttemptPilot),
                   [typeof(DummyGameObject)])
               ?? throw new MissingMethodException(
                   typeof(DummyVehicleSeatProducer).FullName,
                   nameof(DummyVehicleSeatProducer.AttemptPilot));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(VehicleSeatTranslationPatch), detail);
    }

    private sealed class DummyVehicleSeatProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AttemptPilot(DummyGameObject obj)
        {
            _ = obj;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }
}
