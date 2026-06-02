using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ModDisguiseBeingAppliedPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
        PopupPickOptionTranslationPatch.ClearPopupOptionMenuDataPreservationForTests();
        ModDisguiseBeingAppliedPopupTranslationPatch.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
        PopupPickOptionTranslationPatch.ClearPopupOptionMenuDataPreservationForTests();
        ModDisguiseBeingAppliedPopupTranslationPatch.ResetForTests();
    }

    [Test]
    public void Patch_TranslatesNoFamiliarCreaturesPopup_WhenOwnerPatched()
    {
        var target = new DummyModDisguiseBeingAppliedTarget
        {
            PopupMessageToShow = "You aren't familiar enough with any creatures to make a disguise.",
        };

        WithPatchedOwner(() => target.BeingAppliedBy(new DummyGameObject(), new DummyGameObject()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("変装に使えるほど見知った生き物がいない。"));
            Assert.That(HitCount(nameof(PopupShowTranslationPatch), "NoFamiliarCreatures"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_TranslatesNoFamiliarCreaturesPopup_PreservesColorTags_WhenOwnerPatched()
    {
        var target = new DummyModDisguiseBeingAppliedTarget
        {
            PopupMessageToShow = "{{Y|You aren't familiar enough with any creatures to make a disguise.}}",
        };

        WithPatchedOwner(() => target.BeingAppliedBy(new DummyGameObject(), new DummyGameObject()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|変装に使えるほど見知った生き物がいない。}}"));
            Assert.That(HitCount(nameof(PopupShowTranslationPatch), "NoFamiliarCreatures"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedPopupWithoutRecordingTransform_WhenOwnerPatched()
    {
        const string translated = "変装に使えるほど見知った生き物がいない。";
        var target = new DummyModDisguiseBeingAppliedTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(translated),
        };

        WithPatchedOwner(() => target.BeingAppliedBy(new DummyGameObject(), new DummyGameObject()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
            Assert.That(HitCount(nameof(PopupShowTranslationPatch), "NoFamiliarCreatures"), Is.Zero);
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "Unmapped disguise popup.";
        var target = new DummyModDisguiseBeingAppliedTarget
        {
            PopupMessageToShow = source,
        };

        WithPatchedOwner(() => target.BeingAppliedBy(new DummyGameObject(), new DummyGameObject()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(nameof(PopupShowTranslationPatch), "NoFamiliarCreatures"), Is.Zero);
        });
    }

    [Test]
    public void Patch_TranslatesPickerTitle_WhenOwnerPatched()
    {
        var target = new DummyModDisguiseBeingAppliedTarget
        {
            PickerOptions = new[] { "{{Y|snapjaw}}", "creature of some kind" },
        };

        WithPatchedOwner(() => target.BeingAppliedBy(new DummyGameObject(), new DummyGameObject()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("作る変装を選ぶ。"));
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[] { "{{Y|snapjaw}}", "creature of some kind" }));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "PickerTitle"), Is.EqualTo(1));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "FallbackCreatureOption"), Is.Zero);
        });
    }

    [Test]
    public void Patch_HandlesEmptyPickerOptions_WhenOwnerPatched()
    {
        var target = new DummyModDisguiseBeingAppliedTarget
        {
            PickerOptions = Array.Empty<string>(),
        };

        WithPatchedOwner(() => target.BeingAppliedBy(new DummyGameObject(), new DummyGameObject()));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("作る変装を選ぶ。"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Empty);
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "PickerTitle"), Is.EqualTo(1));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "FallbackCreatureOption"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotClaimDisguisePopup_WhenOwnerAbsent()
    {
        const string source = "creature of some kind";

        WithPatchedPopupOnly(() =>
            DummyPopupGenericTarget.PickOption(Title: "Choose a disguise to make.", Options: new[] { source }));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("Choose a disguise to make."));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { source }));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "PickerTitle"), Is.Zero);
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "FallbackCreatureOption"), Is.Zero);
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupRoutes(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyModDisguiseBeingAppliedTarget), nameof(DummyModDisguiseBeingAppliedTarget.BeingAppliedBy)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(ModDisguiseBeingAppliedPopupTranslationPatch),
                    nameof(ModDisguiseBeingAppliedPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(ModDisguiseBeingAppliedPopupTranslationPatch),
                    nameof(ModDisguiseBeingAppliedPopupTranslationPatch.Finalizer))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupRoutes(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupRoutes(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
    }

    private static int HitCount(string route, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            route,
            "Popup.ProducerText." + nameof(ModDisguiseBeingAppliedPopupTranslationPatch) + "." + detail);
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
