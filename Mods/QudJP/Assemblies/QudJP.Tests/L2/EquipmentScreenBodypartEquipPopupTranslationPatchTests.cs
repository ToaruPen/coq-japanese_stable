using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EquipmentScreenBodypartEquipPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        EquipmentScreenBodypartEquipPopupTranslationPatch.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        EquipmentScreenBodypartEquipPopupTranslationPatch.ResetForTests();
    }

    [TestCase("You don't have anything to use in that slot.", "そのスロットで使えるものがない。", "NoSlotItem")]
    [TestCase("You have no inventory!", "持ち物がない！", "NoInventory")]
    public void Patch_TranslatesBodypartEquipPopup_WhenOwnerPatched(string source, string expected, string detail)
    {
        var target = new DummyEquipmentScreenBodypartEquipTarget
        {
            PopupMessageToShow = source,
        };

        WithPatchedOwner(() => target.ShowBodypartEquipUI(new DummyGameObject(), new DummyBodyPart()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(HitCount(detail), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_DoesNotClaimBodypartEquipPopup_WhenOwnerAbsent()
    {
        const string source = "You have no inventory!";

        WithPatchedPopupShowOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("NoInventory"), Is.Zero);
        });
    }

    [Test]
    public void Patch_LeavesFallbackEmptyAndDirectMarkedBodypartEquipPopupSafe_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            DummyPopupShow.Show("Unknown equip popup.");
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Unknown equip popup."));

            DummyPopupShow.Show(string.Empty);
            Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);

            DummyPopupShow.Show(MessageFrameTranslator.MarkDirectTranslation("翻訳済み"));
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("翻訳済み"));
        });

        Assert.That(HitCount("NoInventory"), Is.Zero);
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyEquipmentScreenBodypartEquipTarget),
                    nameof(DummyEquipmentScreenBodypartEquipTarget.ShowBodypartEquipUI)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(EquipmentScreenBodypartEquipPopupTranslationPatch),
                    nameof(EquipmentScreenBodypartEquipPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(EquipmentScreenBodypartEquipPopupTranslationPatch),
                    nameof(EquipmentScreenBodypartEquipPopupTranslationPatch.Finalizer))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupShowOnly(Action action)
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

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(EquipmentScreenBodypartEquipPopupTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
