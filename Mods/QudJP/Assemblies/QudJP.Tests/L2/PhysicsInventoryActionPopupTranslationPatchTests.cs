using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PhysicsInventoryActionPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "The {{Y|canteen}} is not owned by you. Are you sure you want to pour from {{Y|it}}?",
        "{{Y|canteen}}はあなたの所有物ではない。本当にそこから注ぎますか？",
        nameof(DummyPopupShow.ShowYesNoCancel),
        "OwnershipPour")]
    [TestCase(
        "You don't have any {{C|water}} to clean {{Y|the bronze dagger}} with.",
        "{{Y|bronze dagger}}を清掃するための{{C|water}}がない。",
        nameof(DummyPopupShow.ShowFail),
        "NoCleaningLiquid")]
    [TestCase(
        "Do you really want to attack {{Y|the snapjaw}}?",
        "本当に{{Y|snapjaw}}を攻撃しますか？",
        nameof(DummyPopupShow.ShowYesNo),
        "PhysicsAttackConfirm")]
    public void HandleEvent_TranslatesPhysicsInventoryActionPopups_WhenOwnerPatched(
        string source,
        string expected,
        string popupMethod,
        string detail)
    {
        WithPatchedOwner(() =>
        {
            new DummyPhysicsInventoryActionPopupTarget
            {
                PopupMessageToShow = source,
                PopupMethod = popupMethod,
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail("You don't have any {{C|water}} to clean {{Y|the bronze dagger}} with.");

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("You don't have any {{C|water}} to clean {{Y|the bronze dagger}} with."));
                Assert.That(HitCount("NoCleaningLiquid"), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Do you really want to attack {{Y|the snapjaw}}?";

        WithPatchedOwner(() =>
        {
            new DummyPhysicsInventoryActionPopupTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                PopupMethod = nameof(DummyPopupShow.ShowYesNo),
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                Assert.That(HitCount("PhysicsAttackConfirm"), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyPhysicsInventoryActionPopupTarget
            {
                PopupMessageToShow = string.Empty,
                PopupMethod = nameof(DummyPopupShow.ShowFail),
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("NoCleaningLiquid"), Is.Zero);
            });
        });
    }

    [TestCase("Notes added.")]
    [TestCase("Notes removed.")]
    [TestCase("No notes found.")]
    [TestCase("{{Y|debug internals}}")]
    public void HandleEvent_DoesNotClaimDeferredFixedOrRuntimePopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyPhysicsInventoryActionPopupTarget
            {
                PopupMessageToShow = source,
                PopupMethod = nameof(DummyPopupShow.ShowFail),
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("OwnershipPour"), Is.Zero);
                Assert.That(HitCount("NoCleaningLiquid"), Is.Zero);
                Assert.That(HitCount("PhysicsAttackConfirm"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);
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
        var prefix = new HarmonyMethod(RequireMethod(
            typeof(PopupShowTranslationPatch),
            nameof(PopupShowTranslationPatch.Prefix),
            typeof(string).MakeByRefType(),
            typeof(MethodBase)));

        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowFail),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowYesNo),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int)),
            prefix: prefix);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowYesNoCancel),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int)),
            prefix: prefix);
    }

    private static void PatchOwner(Harmony harmony)
    {
        var prefix = new HarmonyMethod(RequireMethod(
            typeof(PhysicsInventoryActionPopupTranslationPatch),
            nameof(PhysicsInventoryActionPopupTranslationPatch.Prefix)));
        var finalizer = new HarmonyMethod(RequireMethod(
            typeof(PhysicsInventoryActionPopupTranslationPatch),
            nameof(PhysicsInventoryActionPopupTranslationPatch.Finalizer),
            typeof(Exception)));

        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPhysicsInventoryActionPopupTarget),
                nameof(DummyPhysicsInventoryActionPopupTarget.HandleEvent),
                typeof(DummyInventoryActionEvent)),
            prefix: prefix,
            finalizer: finalizer);
    }

    private static string? LastPopupMessage(string popupMethod)
    {
        return popupMethod switch
        {
            nameof(DummyPopupShow.ShowFail) => DummyPopupShow.LastShowMessage,
            nameof(DummyPopupShow.ShowYesNo) => DummyPopupShow.LastShowYesNoMessage,
            nameof(DummyPopupShow.ShowYesNoCancel) => DummyPopupShow.LastShowYesNoCancelMessage,
            _ => DummyPopupShow.LastShowMessage,
        };
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(PhysicsInventoryActionPopupTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.physics-inventory-action-popup." + Guid.NewGuid().ToString("N");
    }
}

internal sealed class DummyPhysicsInventoryActionPopupTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string PopupMethod { get; set; } = nameof(DummyPopupShow.ShowFail);

    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowYesNoCancel), StringComparison.Ordinal))
        {
            _ = DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
        }
        else if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowYesNo), StringComparison.Ordinal))
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
        else if (string.Equals(PopupMethod, nameof(DummyPopupShow.Show), StringComparison.Ordinal))
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
        else
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }

        return true;
    }
}
