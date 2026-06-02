using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ObjectFinderConfigFiltersTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupGenericTarget.Reset();
        PopupPickOptionTranslationPatch.ClearPopupOptionMenuDataPreservationForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupGenericTarget.Reset();
        PopupPickOptionTranslationPatch.ClearPopupOptionMenuDataPreservationForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void ConfigFilters_TranslatesFilterAndActionPopupPayloads_WhenOwnerPatched()
    {
        using var ownerPatch = PatchOwner();
        using var pickOptionPatch = PatchPickOption();
        var target = new DummyObjectFinderConfigFiltersTarget();

        target.ConfigFilters();

        Assert.Multiple(() =>
        {
            Assert.That(target.FirstPickTitle, Is.EqualTo("変更するフィルターを選択"));
            Assert.That(
                target.FirstPickOptions,
                Is.EqualTo(new[] { "Custom Classifier{{G| [表示]}}" }));
            Assert.That(target.SecondPickTitle, Is.EqualTo("Custom Classifier"));
            Assert.That(
                target.SecondPickOptions,
                Is.EqualTo(new[] { "アイテムを非表示", "ルールを無視", "上へ移動", "下へ移動" }));
            Assert.That(target.LastActionSelection, Is.EqualTo("Hide Items"));
            Assert.That(ObjectFinderHitCount("ObjectFinder.ConfigFilters.Title"), Is.EqualTo(1));
            Assert.That(ObjectFinderHitCount("ObjectFinder.ConfigFilters.State"), Is.EqualTo(1));
            Assert.That(ObjectFinderHitCount("ObjectFinder.ConfigFilters.Action"), Is.EqualTo(4));
        });
    }

    [Test]
    public void ConfigFilters_LeavesUnknownAndStripsDirectMarkedText_WhenOwnerPatched()
    {
        using var ownerPatch = PatchOwner();
        using var pickOptionPatch = PatchPickOption();
        var target = new DummyObjectFinderConfigFiltersTarget
        {
            ActionsToShow =
            [
                "Unknown Action",
                string.Empty,
                MessageFrameTranslator.MarkDirectTranslation("アイテムを表示"),
                "{{R|Show Items}}",
            ],
            FilterRowsToShow =
            [
                "Unknown Classifier",
                string.Empty,
                MessageFrameTranslator.MarkDirectTranslation("表示済みフィルター"),
                "Custom Classifier{{G| [Show]}}",
            ],
        };

        target.ConfigFilters();

        Assert.Multiple(() =>
        {
            Assert.That(
                target.SecondPickOptions,
                Is.EqualTo(new[]
                {
                    "Unknown Action",
                    string.Empty,
                    "アイテムを表示",
                    "{{R|アイテムを表示}}",
                }));
            Assert.That(
                target.FirstPickOptions,
                Is.EqualTo(new[]
                {
                    "Unknown Classifier",
                    string.Empty,
                    "表示済みフィルター",
                    "Custom Classifier{{G| [表示]}}",
                }));
            Assert.That(ObjectFinderHitCount("ObjectFinder.ConfigFilters.Action"), Is.EqualTo(1));
            Assert.That(ObjectFinderHitCount("ObjectFinder.ConfigFilters.State"), Is.EqualTo(1));
        });
    }

    private static IDisposable PatchOwner()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyObjectFinderConfigFiltersTarget), nameof(DummyObjectFinderConfigFiltersTarget.ConfigFilters)),
            prefix: new HarmonyMethod(RequireMethod(typeof(ObjectFinderConfigFiltersTranslationPatch), nameof(ObjectFinderConfigFiltersTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(ObjectFinderConfigFiltersTranslationPatch), nameof(ObjectFinderConfigFiltersTranslationPatch.Finalizer))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static IDisposable PatchPickOption()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static int ObjectFinderHitCount(string family)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(ObjectFinderConfigFiltersTranslationPatch),
            family);
    }

    private sealed class DummyObjectFinderConfigFiltersTarget
    {
        public string FirstPickTitle { get; private set; } = string.Empty;

        public IReadOnlyList<string>? FirstPickOptions { get; private set; }

        public string SecondPickTitle { get; private set; } = string.Empty;

        public IReadOnlyList<string>? SecondPickOptions { get; private set; }

        public string? LastActionSelection { get; private set; }

        public IReadOnlyList<string> ActionsToShow { get; init; } =
        [
            "Hide Items",
            "Ignore Rule",
            "Move Up",
            "Move Down",
        ];

        public IReadOnlyList<string> FilterRowsToShow { get; init; } =
        [
            "Custom Classifier{{G| [Show]}}",
        ];

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ConfigFilters()
        {
            var actions = new List<string>(ActionsToShow);
            var actionIndex = DummyPopupGenericTarget.PickOption(
                Title: "Custom Classifier",
                Options: actions.ToArray(),
                AllowEscape: true);
            SecondPickTitle = DummyPopupGenericTarget.LastPickOptionTitle;
            SecondPickOptions = DummyPopupGenericTarget.LastPickOptionOptions;
            LastActionSelection = actionIndex >= 0 && actionIndex < actions.Count ? actions[actionIndex] : null;

            _ = DummyPopupGenericTarget.PickOption(
                Title: "Pick a filter to change",
                Options: FilterRowsToShow.ToArray(),
                AllowEscape: true);
            FirstPickTitle = DummyPopupGenericTarget.LastPickOptionTitle;
            FirstPickOptions = DummyPopupGenericTarget.LastPickOptionOptions;
        }
    }

    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
