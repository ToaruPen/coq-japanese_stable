using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DataDiskLearnPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [Test]
    public void Patch_TranslatesItemModificationLearnPopup_WhenOwnerPatched()
    {
        WithPatchedDataDiskHandleEvent(() =>
        {
            var target = new DummyDataDiskProducerTarget
            {
                PopupMessageToShow = "You learn the item modification {{W|counterweighted}}.",
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("アイテム改造{{W|counterweighted}}を習得した。"));
                Assert.That(HitCount("ItemModification"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_TranslatesBuildRecipeLearnPopup_WhenOwnerPatched()
    {
        WithPatchedDataDiskHandleEvent(() =>
        {
            var target = new DummyDataDiskProducerTarget
            {
                PopupMessageToShow = "You learn to build {{C|laser pistols}}.",
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{C|laser pistols}}を作成する方法を習得した。"));
                Assert.That(HitCount("BuildRecipe"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslateDataDiskLearnPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You learn to build laser pistols.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You learn to build laser pistols."));
                Assert.That(HitCount("BuildRecipe"), Is.Zero);
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
        WithPatchedDataDiskHandleEvent(() =>
        {
            var target = new DummyDataDiskProducerTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("You learn to build laser pistols."),
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You learn to build laser pistols."));
                Assert.That(HitCount("BuildRecipe"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedDataDiskHandleEvent(() =>
        {
            var target = new DummyDataDiskProducerTarget
            {
                PopupMessageToShow = "You study the data disk but learn nothing new.",
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You study the data disk but learn nothing new."));
                Assert.That(HitCount("ItemModification"), Is.Zero);
                Assert.That(HitCount("BuildRecipe"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedDataDiskHandleEvent(() =>
        {
            var target = new DummyDataDiskProducerTarget
            {
                PopupMessageToShow = string.Empty,
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("BuildRecipe"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_KeepsOuterOwnerScopeActive_WhenNestedScopeExits()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            DataDiskLearnPopupTranslationPatch.Prefix();
            try
            {
                DataDiskLearnPopupTranslationPatch.Prefix();
                DataDiskLearnPopupTranslationPatch.Finalizer(null);

                DummyPopupShow.Show("You learn to build {{C|laser pistols}}.");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{C|laser pistols}}を作成する方法を習得した。"));
                    Assert.That(HitCount("BuildRecipe"), Is.EqualTo(1));
                });
            }
            finally
            {
                DataDiskLearnPopupTranslationPatch.Finalizer(null);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedDataDiskHandleEvent(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            assertion();
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

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyDataDiskProducerTarget), nameof(DummyDataDiskProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DataDiskLearnPopupTranslationPatch), nameof(DataDiskLearnPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(DataDiskLearnPopupTranslationPatch), nameof(DataDiskLearnPopupTranslationPatch.Finalizer), typeof(Exception))));
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

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(DataDiskLearnPopupTranslationPatch) + "." + detail);
    }

    private sealed class DummyDataDiskProducerTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HandleEvent(DummyInventoryActionEvent e)
        {
            _ = e;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }
}
