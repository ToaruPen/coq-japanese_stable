using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SupplyableIntegratedHostPopupTranslationPatchTests
{
    private const string NoNeededSuppliesSource = "The {{Y|phase cannon}} needs no supplies.";
    private const string NoHeldSuppliesSource = "You have no supplies that the {{Y|phase cannon}} needs.";
    private const string PluralNoNeededSuppliesSource = "The {{Y|phase cannons}} need no supplies.";
    private const string PluralNoHeldSuppliesSource = "You have no supplies that the {{Y|phase cannons}} need.";
    private const string NoNeededSuppliesTranslated = "{{Y|phase cannon}}は補給品を必要としていない。";
    private const string NoHeldSuppliesTranslated = "{{Y|phase cannon}}が必要とする補給品を持っていない。";
    private const string PluralNoNeededSuppliesTranslated = "{{Y|phase cannons}}は補給品を必要としていない。";
    private const string PluralNoHeldSuppliesTranslated = "{{Y|phase cannons}}が必要とする補給品を持っていない。";

    [SetUp]
    public void SetUp()
    {
        ResetState();
    }

    [TearDown]
    public void TearDown()
    {
        ResetState();
    }

    private static void ResetState()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(NoNeededSuppliesSource, NoNeededSuppliesTranslated, "SupplyableIntegratedHostNoNeededSupplies")]
    [TestCase(NoHeldSuppliesSource, NoHeldSuppliesTranslated, "SupplyableIntegratedHostNoHeldSupplies")]
    [TestCase(PluralNoNeededSuppliesSource, PluralNoNeededSuppliesTranslated, "SupplyableIntegratedHostNoNeededSupplies")]
    [TestCase(PluralNoHeldSuppliesSource, PluralNoHeldSuppliesTranslated, "SupplyableIntegratedHostNoHeldSupplies")]
    public void Patch_TranslatesSupplyableIntegratedHostPopup_WhenOwnerPatched(
        string source,
        string expected,
        string expectedFamilySuffix)
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummySupplyableIntegratedHostProducer
            {
                PopupMessageToShow = source,
            };

            target.AttemptSupply();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.Show." + expectedFamilySuffix),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        RunWithPopupPatchOnly(() => DummyPopupShow.Show(NoNeededSuppliesSource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(NoNeededSuppliesSource));
            Assert.That(GetNoNeededSuppliesHitCount(), Is.EqualTo(0));
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(NoNeededSuppliesSource);

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummySupplyableIntegratedHostProducer
            {
                PopupMessageToShow = source,
            };

            target.AttemptSupply();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(NoNeededSuppliesSource));
                Assert.That(GetNoNeededSuppliesHitCount(), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummySupplyableIntegratedHostProducer
            {
                PopupMessageToShow = string.Empty,
            };

            target.AttemptSupply();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
        });
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return (parameters.Length == 0
                ? AccessTools.Method(type, methodName)
                : AccessTools.Method(type, methodName, parameters))
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void RunWithPopupPatchOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithOwnerAndPopupPatches(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySupplyableIntegratedHostProducer), nameof(DummySupplyableIntegratedHostProducer.AttemptSupply)),
                prefix: new HarmonyMethod(RequireMethod(typeof(SupplyableIntegratedHostPopupTranslationPatch), nameof(SupplyableIntegratedHostPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(SupplyableIntegratedHostPopupTranslationPatch), nameof(SupplyableIntegratedHostPopupTranslationPatch.Finalizer), typeof(Exception))));
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static int GetNoNeededSuppliesHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.SupplyableIntegratedHostNoNeededSupplies");
    }

    private sealed class DummySupplyableIntegratedHostProducer
    {
        public string PopupMessageToShow = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AttemptSupply()
        {
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }
}
