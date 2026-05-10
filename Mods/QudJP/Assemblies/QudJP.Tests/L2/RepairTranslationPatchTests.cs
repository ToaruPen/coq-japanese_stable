using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RepairTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyRepairProducerTarget.HandleEvent),
        "{{Y|青銅の短剣}} is not owned by you, and trying to repair {{Y|青銅の短剣}} risks damaging {{R|青銅の短剣}}. Are you sure you want to do so?",
        "{{Y|青銅の短剣}}はあなたのものではなく、{{Y|青銅の短剣}}を修理しようとすると{{R|青銅の短剣}}を損傷させる危険がある。本当に行うか？",
        "OwnershipRisk")]
    [TestCase(
        nameof(DummyRepairProducerTarget.HandleEvent),
        "{{C|古い箱}} is not owned by you, and trying to repair {{Y|ひび割れたレンズ}} inside {{C|古い箱}} risks causing damage. Are you sure you want to do so?",
        "{{C|古い箱}}はあなたのものではなく、{{C|古い箱}}の中にある{{Y|ひび割れたレンズ}}を修理しようとすると損傷を引き起こす危険がある。本当に行うか？",
        "ContainerOwnershipRisk")]
    public void Patch_TranslatesRepairConfirmationPopup_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNoCancel(harmony);
            PatchOwner(harmony, RequireOwnerMethod(methodName));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_TranslatesRepairSuccessShowBlock_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultSuccess)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = "You repair {{C|壊れたライフル}}.",
            };

            target.RepairResultSuccess(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{C|壊れたライフル}}を修理した。"));
                Assert.That(RepairHitCount(nameof(PopupTranslationPatch), "Success"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_TranslatesTinkeringBitsReward_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultExceptionalSuccess)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = "You receive tinkering bits <{{|ABCD}}>",
            };

            target.RepairResultExceptionalSuccess(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("修理ビット<{{|ABCD}}>を受け取った。"));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "TinkeringBits"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        nameof(DummyRepairProducerTarget.RepairResultPartialSuccess),
        "You make some progress repairing {{Y|位相砲}}.",
        "{{Y|位相砲}}の修理が少し進んだ。",
        "PartialSuccess")]
    [TestCase(
        nameof(DummyRepairProducerTarget.RepairResultFailure),
        "You can't figure out how to fix {{R|壊れたジャイロコプター}}.",
        "{{R|壊れたジャイロコプター}}の修理方法がわからない。",
        "Failure")]
    public void Patch_TranslatesRepairOutcomePopup_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(methodName));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = source,
            };

            InvokeOutcomeMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        "You cannot repair {{Y|未知の装置}} until you understand it.",
        "{{Y|未知の装置}}について理解するまで、修理できない。",
        "CannotRepairUntilUnderstand")]
    [TestCase(
        "You cannot repair {{R|封印された機械}}.",
        "{{R|封印された機械}}は修理できない。",
        "CannotRepair")]
    public void TinkeringRepairPatch_TranslatesHandleEventShowFailMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.HandleEvent)));

            var target = new DummyRepairProducerTarget
            {
                HandleEventPopupMethod = "ShowFail",
                PopupMessageToShow = source,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringRepairPatch_TranslatesMissingBitsPopup_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.HandleEvent)));

            var target = new DummyRepairProducerTarget
            {
                HandleEventPopupMethod = "ShowFail",
                PopupMessageToShow = "You don't have <{{|ABCD}}> to repair {{Y|ひび割れたレンズ}}. You have:\n\n{{|A: 1, B: 0}}",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("{{Y|ひび割れたレンズ}}を修理するための<{{|ABCD}}>がない。所持ビット:\n\n{{|A: 1, B: 0}}"));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "MissingBits"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringRepairPatch_TranslatesSpendBitsConfirmation_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNoCancel(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.HandleEvent)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = "Do you want to spend <{{|ABCD}}> to repair {{Y|ひび割れたレンズ}}? You have:\n\n{{|A: 1, B: 0}}",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowYesNoCancelMessage,
                    Is.EqualTo("{{Y|ひび割れたレンズ}}を修理するために<{{|ABCD}}>を消費するか？所持ビット:\n\n{{|A: 1, B: 0}}"));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "SpendBits"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringRepairPatch_TranslatesSharedRepairOutcomePopups_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultPartialSuccess)));
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultFailure)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = "You make some progress repairing {{Y|修理中の精密機械}}.",
            };

            target.RepairResultPartialSuccess(new DummyGameObject(), new DummyGameObject());
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|修理中の精密機械}}の修理が少し進んだ。"));

            target.PopupMessageToShow = "You can't figure out how to fix {{R|壊れた測定器}}.";
            target.RepairResultFailure(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{R|壊れた測定器}}の修理方法がわからない。"));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "PartialSuccess"), Is.EqualTo(1));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "Failure"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringRepairPatch_TranslatesSharedRepairSuccessAndBitsPopups_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowBlock(harmony);
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultSuccess)));
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultExceptionalSuccess)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = "You repair {{C|壊れた測定器}}.",
            };

            target.RepairResultSuccess(new DummyGameObject(), new DummyGameObject());
            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{C|壊れた測定器}}を修理した。"));

            target.PopupMessageToShow = "You receive tinkering bits <{{|XYZ}}>";
            target.RepairResultExceptionalSuccess(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("修理ビット<{{|XYZ}}>を受け取った。"));
                Assert.That(RepairHitCount(nameof(PopupTranslationPatch), "Success"), Is.EqualTo(1));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "TinkeringBits"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotTranslateRepairPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You make some progress repairing {{Y|位相砲}}.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You make some progress repairing {{Y|位相砲}}."));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "PartialSuccess"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultPartialSuccess)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("You make some progress repairing {{Y|位相砲}}."),
            };

            target.RepairResultPartialSuccess(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You make some progress repairing {{Y|位相砲}}."));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "PartialSuccess"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultFailure)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = string.Empty,
            };

            target.RepairResultFailure(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "Failure"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRepairProducerTarget.RepairResultPartialSuccess)));

            var target = new DummyRepairProducerTarget
            {
                PopupMessageToShow = "{{W|You make some progress repairing {{Y|位相砲}}.}}",
            };

            target.RepairResultPartialSuccess(new DummyGameObject(), new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{W|{{Y|位相砲}}の修理が少し進んだ。}}"));
                Assert.That(RepairHitCount(nameof(PopupShowTranslationPatch), "PartialSuccess"), Is.EqualTo(1));
            });
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

    private static void PatchPopupShowYesNoCancel(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancel)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowBlock(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(RepairTranslationPatch), nameof(RepairTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(RepairTranslationPatch), nameof(RepairTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName == nameof(DummyRepairProducerTarget.HandleEvent)
            ? RequireMethod(typeof(DummyRepairProducerTarget), methodName, typeof(DummyInventoryActionEvent))
            : RequireMethod(typeof(DummyRepairProducerTarget), methodName, typeof(DummyGameObject), typeof(DummyGameObject));
    }

    private static void InvokeOutcomeMethod(DummyRepairProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject(), new DummyGameObject() });
    }

    private static int RepairHitCount(string route, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            route,
            "Popup.ProducerText." + nameof(RepairTranslationPatch) + "." + detail);
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
}
