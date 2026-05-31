using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BaseMutationSelectVariantPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupGenericTarget.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupGenericTarget.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void SelectVariant_TranslatesVariantPickerTitle_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPickOption(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseMutationTarget), nameof(DummyBaseMutationTarget.SelectVariant)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(BaseMutationSelectVariantPopupTranslationPatch),
                    nameof(BaseMutationSelectVariantPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(BaseMutationSelectVariantPopupTranslationPatch),
                    nameof(BaseMutationSelectVariantPopupTranslationPatch.Finalizer),
                    typeof(Exception))));

            _ = DummyBaseMutationTarget.SelectVariant();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("変種を選択"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupPickOptionTranslationPatch),
                        "Popup.ProducerText.BaseMutationSelectVariantPopupTranslationPatch"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SelectVariant_DoesNotTranslateVariantPickerTitle_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPickOption(harmony);

            _ = DummyBaseMutationTarget.SelectVariant();

            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("Choose variant"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SelectVariant_DoesNotRetranslateDirectMarkedTitle_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPickOption(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseMutationTarget), nameof(DummyBaseMutationTarget.SelectVariant)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(BaseMutationSelectVariantPopupTranslationPatch),
                    nameof(BaseMutationSelectVariantPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(BaseMutationSelectVariantPopupTranslationPatch),
                    nameof(BaseMutationSelectVariantPopupTranslationPatch.Finalizer),
                    typeof(Exception))));

            DummyBaseMutationTarget.TitleToShow = MessageFrameTranslator.MarkDirectTranslation("Choose variant");

            _ = DummyBaseMutationTarget.SelectVariant();

            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("Choose variant"));
        }
        finally
        {
            DummyBaseMutationTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPickOption(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyBaseMutationTarget
    {
        public static string TitleToShow { get; set; } = "Choose variant";

        public static bool SelectVariant()
        {
            _ = DummyPopupGenericTarget.PickOption(Title: TitleToShow);
            return true;
        }

        public static void Reset()
        {
            TitleToShow = "Choose variant";
        }
    }
}
