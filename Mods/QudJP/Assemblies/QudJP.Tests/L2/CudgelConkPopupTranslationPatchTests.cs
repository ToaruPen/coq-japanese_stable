using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CudgelConkPopupTranslationPatchTests
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

    [TestCase("snapjaw doesn't have anything like a head to conk.", "snapjawには殴る頭のようなものがない。")]
    [TestCase("gelatinous wedges don't have anything like a head to conk.", "gelatinous wedgesには殴る頭のようなものがない。")]
    public void Patch_TranslatesNoHeadPopup_WhenOwnerPatched(string source, string expected)
    {
        WithPatchedCudgelConk(nameof(DummyCudgelConkProducerTarget.ShowNoHeadPopup), () =>
        {
            var target = new DummyCudgelConkProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.ShowNoHeadPopup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("NoHead"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_TranslatesConfirmSelfConkPopup_WhenOwnerPatched()
    {
        WithPatchedCudgelConk(nameof(DummyCudgelConkProducerTarget.ShowConfirmSelfConkPopup), () =>
        {
            var target = new DummyCudgelConkProducerTarget
            {
                PopupMessageToShow = "Are you sure you want to conk yourself on {{C|the head}}?",
            };

            target.ShowConfirmSelfConkPopup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("本当に自分自身を{{C|頭}}にこん棒で殴りますか？"));
                Assert.That(HitCount("ConfirmSelfConk"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslateCudgelConkPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail("snapjaw doesn't have anything like a head to conk.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("snapjaw doesn't have anything like a head to conk."));
                Assert.That(HitCount("NoHead"), Is.Zero);
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
        WithPatchedCudgelConk(nameof(DummyCudgelConkProducerTarget.ShowNoHeadPopup), () =>
        {
            var target = new DummyCudgelConkProducerTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("snapjaw doesn't have anything like a head to conk."),
            };

            target.ShowNoHeadPopup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("snapjaw doesn't have anything like a head to conk."));
                Assert.That(HitCount("NoHead"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedCudgelConk(nameof(DummyCudgelConkProducerTarget.ShowNoHeadPopup), () =>
        {
            var target = new DummyCudgelConkProducerTarget
            {
                PopupMessageToShow = "snapjaw is already unconscious.",
            };

            target.ShowNoHeadPopup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("snapjaw is already unconscious."));
                Assert.That(HitCount("NoHead"), Is.Zero);
                Assert.That(HitCount("ConfirmSelfConk"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedCudgelConk(nameof(DummyCudgelConkProducerTarget.ShowNoHeadPopup), () =>
        {
            var target = new DummyCudgelConkProducerTarget
            {
                PopupMessageToShow = string.Empty,
            };

            target.ShowNoHeadPopup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("NoHead"), Is.Zero);
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
            PatchPopupShowFail(harmony);

            CudgelConkPopupTranslationPatch.Prefix();
            try
            {
                CudgelConkPopupTranslationPatch.Prefix();
                CudgelConkPopupTranslationPatch.Finalizer(null);

                DummyPopupShow.ShowFail("snapjaw doesn't have anything like a head to conk.");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("snapjawには殴る頭のようなものがない。"));
                    Assert.That(HitCount("NoHead"), Is.EqualTo(1));
                });
            }
            finally
            {
                CudgelConkPopupTranslationPatch.Finalizer(null);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedCudgelConk(string methodName, Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowFail(harmony);
            PatchPopupShowYesNo(harmony);
            PatchOwner(harmony, RequireMethod(typeof(DummyCudgelConkProducerTarget), methodName));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(CudgelConkPopupTranslationPatch), nameof(CudgelConkPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(CudgelConkPopupTranslationPatch), nameof(CudgelConkPopupTranslationPatch.Finalizer), typeof(Exception))));
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
            "Popup.Show." + nameof(CudgelConkPopupTranslationPatch) + "." + detail);
    }

    private sealed class DummyCudgelConkProducerTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowNoHeadPopup()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowConfirmSelfConkPopup()
        {
            DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
    }
}
