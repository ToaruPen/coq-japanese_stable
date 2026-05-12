using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GolemQuestSelectionPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        Translator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyGolemQuestSelectionProducer.WishSpec),
        "No blueprint by ID '{{W|missing-body}}' found.",
        "ID '{{W|missing-body}}' のブループリントが見つからない。",
        "MissingBlueprint")]
    [TestCase(
        nameof(DummyGolemQuestSelectionProducer.Pick),
        "You have nothing that meets the requirement of the {{Y|armament}}.",
        "{{Y|armament}}の要件を満たすものを持っていない。",
        "MissingRequirement")]
    public void Patch_TranslatesGolemSelectionPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndPopup(methodName, () =>
        {
            var target = new DummyGolemQuestSelectionProducer
            {
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslateGolemSelectionPopup_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail("No blueprint by ID '{{W|missing-body}}' found.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("No blueprint by ID '{{W|missing-body}}' found."));
                Assert.That(HitCount("MissingBlueprint"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation("No blueprint by ID '{{W|missing-body}}' found.");

        WithPatchedOwnerAndPopup(
            nameof(DummyGolemQuestSelectionProducer.WishSpec),
            () =>
            {
                var target = new DummyGolemQuestSelectionProducer
                {
                    PopupMessageToShow = source,
                };

                target.WishSpec();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("No blueprint by ID '{{W|missing-body}}' found."));
                    Assert.That(HitCount("MissingBlueprint"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwnerAndPopup(
            nameof(DummyGolemQuestSelectionProducer.WishSpec),
            () =>
            {
                var target = new DummyGolemQuestSelectionProducer
                {
                    PopupMessageToShow = string.Empty,
                };

                target.WishSpec();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("MissingBlueprint"), Is.Zero);
                    Assert.That(HitCount("MissingRequirement"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "There is no suitable golem material here.";

        WithPatchedOwnerAndPopup(
            nameof(DummyGolemQuestSelectionProducer.WishSpec),
            () =>
            {
                var target = new DummyGolemQuestSelectionProducer
                {
                    PopupMessageToShow = source,
                };

                target.WishSpec();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("MissingBlueprint"), Is.Zero);
                    Assert.That(HitCount("MissingRequirement"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        const string outerMethodName = nameof(DummyGolemQuestSelectionProducer.Pick);
        const string innerMethodName = nameof(DummyGolemQuestSelectionProducer.NestedWishSpec);

        WithPatchedOwnerAndPopup(
            [outerMethodName, innerMethodName],
            () =>
            {
                var innerTarget = new DummyGolemQuestSelectionProducer
                {
                    PopupMessageToShow = "No blueprint by ID '{{W|missing-body}}' found.",
                };
                var outerTarget = new DummyGolemQuestSelectionProducer
                {
                    PopupMessageToShow = "You have nothing that meets the requirement of the {{Y|armament}}.",
                    BeforePopup = () =>
                    {
                        innerTarget.NestedWishSpec();

                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ID '{{W|missing-body}}' のブループリントが見つからない。"));
                            Assert.That(HitCount("MissingBlueprint"), Is.EqualTo(1));
                            Assert.That(HitCount("MissingRequirement"), Is.Zero);
                        });
                    },
                };

                InvokeOwnerMethod(outerTarget, outerMethodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|armament}}の要件を満たすものを持っていない。"));
                    Assert.That(HitCount("MissingBlueprint"), Is.EqualTo(1));
                    Assert.That(HitCount("MissingRequirement"), Is.EqualTo(1));
                });

                DummyPopupShow.ShowFail("You have nothing that meets the requirement of the {{Y|armament}}.");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You have nothing that meets the requirement of the {{Y|armament}}."));
                    Assert.That(HitCount("MissingBlueprint"), Is.EqualTo(1));
                    Assert.That(HitCount("MissingRequirement"), Is.EqualTo(1));
                });
            });
    }

    private static void WithPatchedOwnerAndPopup(string methodName, Action action)
    {
        WithPatchedOwnerAndPopup([methodName], action);
    }

    private static void WithPatchedOwnerAndPopup(string[] methodNames, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony);
            foreach (var methodName in methodNames)
            {
                harmony.Patch(
                    original: RequireOwnerMethod(methodName),
                    prefix: new HarmonyMethod(RequireMethod(typeof(GolemQuestSelectionPopupTranslationPatch), nameof(GolemQuestSelectionPopupTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(GolemQuestSelectionPopupTranslationPatch), nameof(GolemQuestSelectionPopupTranslationPatch.Finalizer))));
            }

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopup(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void InvokeOwnerMethod(DummyGolemQuestSelectionProducer target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, null);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(GolemQuestSelectionPopupTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyGolemQuestSelectionProducer), methodName);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyGolemQuestSelectionProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;
        public Action? BeforePopup { get; init; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WishSpec()
        {
            ShowPopup();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void NestedWishSpec()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Pick()
        {
            BeforePopup?.Invoke();
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }

        private void ShowPopup()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
    }
}
