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
public sealed class VehicleUnpoweredTranslationPatchTests
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
        "{{Y|chem cell}} is drained or nearly drained.\n\nRecharge or replace it to power {{C|phase cannon}}.",
        "{{Y|ケムセル}}は消耗しているか、ほとんど空だ。\n\n{{C|phase cannon}}に電力を供給するには再充電するか交換する必要がある。",
        "CellDrained")]
    [TestCase(
        "The {{Y|chem cell}} is drained or nearly drained.\n\nRecharge or replace it to power the {{C|phase cannon}}.",
        "{{Y|ケムセル}}は消耗しているか、ほとんど空だ。\n\n{{C|phase cannon}}に電力を供給するには再充電するか交換する必要がある。",
        "CellDrained")]
    [TestCase(
        "Insert a chem cell to power {{C|phase cannon}}.",
        "{{C|phase cannon}}に電力を供給するにはケムセルを挿入する必要がある。",
        "InsertCell")]
    [TestCase(
        "Insert a chem cell to power the {{C|phase cannon}}.",
        "{{C|phase cannon}}に電力を供給するにはケムセルを挿入する必要がある。",
        "InsertCell")]
    [TestCase(
        "{{C|phase cannon}} lacks the power to act.",
        "{{C|phase cannon}}は行動する力がない。",
        "LacksPower")]
    [TestCase(
        "The {{C|phase cannon}} lacks the power to act.",
        "{{C|phase cannon}}は行動する力がない。",
        "LacksPower")]
    public void Patch_TranslatesUnpoweredVehiclePopup_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "{{C|phase cannon}} lacks the power to act.";

        WithPatchedVehicleUnpoweredPopupTranslator(() => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("LacksPower"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "{{C|phase cannon}} lacks the power to act.";

        WithProductionPopupPatches(() =>
        {
            WithOwnerPatch(() =>
            {
                new DummyVehicleUnpoweredProducer
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.PreventActionMessage(new DummyGameObject());
            });

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("LacksPower"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_RestoresDirectMarkerPassThroughText_ForNestedOwnerScopes()
    {
        const string outerSource = "{{C|phase cannon}} lacks the power to act.";
        const string innerSource = "Insert a chem cell to power {{C|phase cannon}}.";

        VehicleUnpoweredTranslationPatch.Prefix(out var outerState);
        try
        {
            _ = VehicleUnpoweredTranslationPatch.TryTranslatePopupMessage(
                MessageFrameTranslator.MarkDirectTranslation(outerSource),
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out _);

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo(outerSource));

            VehicleUnpoweredTranslationPatch.Prefix(out var innerState);
            try
            {
                _ = VehicleUnpoweredTranslationPatch.TryTranslatePopupMessage(
                    MessageFrameTranslator.MarkDirectTranslation(innerSource),
                    nameof(PopupShowTranslationPatch),
                    "Popup.Show",
                    out _);

                Assert.That(DirectMarkerPassThroughText(), Is.EqualTo(innerSource));
            }
            finally
            {
                VehicleUnpoweredTranslationPatch.Finalizer(null, innerState);
            }

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo(outerSource));
        }
        finally
        {
            VehicleUnpoweredTranslationPatch.Finalizer(null, outerState);
        }

        Assert.That(DirectMarkerPassThroughText(), Is.Null);
    }

    [TestCase("")]
    [TestCase("The vehicle hums quietly.")]
    public void Patch_DoesNotClaimFixedOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, "LacksPower", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits)
    {
        WithPatchedOwnerAndVehicleUnpoweredPopupTranslator(() =>
        {
            new DummyVehicleUnpoweredProducer
            {
                PopupMessageToShow = source,
            }.PreventActionMessage(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
            });
        });
    }

    private static void WithPatchedOwnerAndVehicleUnpoweredPopupTranslator(Action action)
    {
        WithPatchedVehicleUnpoweredPopupTranslator(harmony =>
        {
            PatchOwner(harmony);

            action();
        });
    }

    private static void WithPatchedVehicleUnpoweredPopupTranslator(Action action)
    {
        WithPatchedVehicleUnpoweredPopupTranslator(_ => action());
    }

    private static void WithPatchedVehicleUnpoweredPopupTranslator(Action<Harmony> action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(VehicleUnpoweredTranslationPatchTests),
                    nameof(TranslateVehicleUnpoweredPopup))));
            harmony.Patch(
                original: OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(VehicleUnpoweredTranslationPatchTests),
                    nameof(TranslateVehicleUnpoweredPopup))));

            action(harmony);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithProductionPopupPatches(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchProductionPopup(harmony, nameof(DummyPopupShow.Show));
            PatchProductionPopup(harmony, nameof(DummyPopupShow.ShowFail));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithOwnerPatch(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireOwnerMethod(),
            prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                typeof(VehicleUnpoweredTranslationPatch),
                "Prefix",
                typeof(string).MakeByRefType())),
            finalizer: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                typeof(VehicleUnpoweredTranslationPatch),
                "Finalizer",
                typeof(Exception),
                typeof(string))));
    }

    private static void PatchProductionPopup(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyPopupShow), methodName),
            prefix: new HarmonyMethod(
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(PopupShowTranslationPatch),
                    nameof(PopupShowTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(PopupShowTranslationPatch),
                    nameof(PopupShowTranslationPatch.Finalizer))));
    }

    private static void TranslateVehicleUnpoweredPopup(ref string Message)
    {
        if (VehicleUnpoweredTranslationPatch.TryTranslatePopupMessage(
            Message,
            nameof(PopupShowTranslationPatch),
            "Popup.Show",
            out var translated))
        {
            Message = translated;
        }
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyVehicleUnpoweredProducer),
                   nameof(DummyVehicleUnpoweredProducer.PreventActionMessage),
                   [typeof(DummyGameObject)])
               ?? throw new MissingMethodException(
                   typeof(DummyVehicleUnpoweredProducer).FullName,
                   nameof(DummyVehicleUnpoweredProducer.PreventActionMessage));
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(VehicleUnpoweredTranslationPatch) + "." + detail);
    }

    private static string? DirectMarkerPassThroughText()
    {
        var field = typeof(VehicleUnpoweredTranslationPatch).GetField(
            "directMarkerPassThroughText",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);
        return field!.GetValue(null) as string;
    }

    private sealed class DummyVehicleUnpoweredProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PreventActionMessage(DummyGameObject actor)
        {
            _ = actor;
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
    }
}
