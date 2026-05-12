using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationSelfTargetPopupTranslationPatchTests
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

    [TestCase(nameof(DummyMutationSelfTargetProducer.BreatherBaseCast))]
    [TestCase(nameof(DummyMutationSelfTargetProducer.FlamingRayCast))]
    [TestCase(nameof(DummyMutationSelfTargetProducer.FreezeBreathFireEvent))]
    [TestCase(nameof(DummyMutationSelfTargetProducer.FreezingRayCast))]
    public void Patch_TranslatesSelfTargetPopup_WhenOwnerPatched(string methodName)
    {
        UseRepositoryPatternDictionary();

        WithPatchedOwnerAndPopup(methodName, () =>
        {
            var target = new DummyMutationSelfTargetProducer
            {
                PopupMessageToShow = "Are you sure you want to target yourself?",
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("yourselfを標的にしてもよいか？"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotClaimSelfTargetPopup_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        WithPatchedPopupOnly(() =>
        {
            _ = DummyPopupShow.ShowYesNoCancel("Are you sure you want to target yourself?");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("yourselfを標的にしてもよいか？"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        var source = MessageFrameTranslator.MarkDirectTranslation("Are you sure you want to target yourself?");

        WithPatchedOwnerAndPopup(
            nameof(DummyMutationSelfTargetProducer.BreatherBaseCast),
            () =>
            {
                var target = new DummyMutationSelfTargetProducer
                {
                    PopupMessageToShow = source,
                };

                target.BreatherBaseCast();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("Are you sure you want to target yourself?"));
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesUnknownSelfTargetPopupUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        const string source = "Are you sure you want to target yourself now.";

        WithPatchedOwnerAndPopup(
            nameof(DummyMutationSelfTargetProducer.BreatherBaseCast),
            () =>
            {
                var target = new DummyMutationSelfTargetProducer
                {
                    PopupMessageToShow = source,
                };

                target.BreatherBaseCast();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_PreservesColorTagsInSelfTargetPopup_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        WithPatchedOwnerAndPopup(
            nameof(DummyMutationSelfTargetProducer.BreatherBaseCast),
            () =>
            {
                var target = new DummyMutationSelfTargetProducer
                {
                    PopupMessageToShow = "Are you sure you want to target {{Y|your clone}}?",
                };

                target.BreatherBaseCast();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("{{Y|your clone}}を標的にしてもよいか？"));
                    Assert.That(HitCount(), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        UseRepositoryPatternDictionary();

        WithPatchedOwnerAndPopup(
            [
                nameof(DummyMutationSelfTargetProducer.BreatherBaseCast),
                nameof(DummyMutationSelfTargetProducer.FlamingRayCast),
            ],
            () =>
            {
                var innerTarget = new DummyMutationSelfTargetProducer
                {
                    PopupMessageToShow = "Are you sure you want to target yourself?",
                };
                var outerTarget = new DummyMutationSelfTargetProducer
                {
                    PopupMessageToShow = "Are you sure you want to target {{Y|your clone}}?",
                    BeforePopup = () =>
                    {
                        innerTarget.FlamingRayCast();
                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("yourselfを標的にしてもよいか？"));
                            Assert.That(HitCount(), Is.EqualTo(1));
                        });
                    },
                };

                outerTarget.BreatherBaseCast();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("{{Y|your clone}}を標的にしてもよいか？"));
                    Assert.That(HitCount(), Is.EqualTo(2));
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        WithPatchedOwnerAndPopup(
            nameof(DummyMutationSelfTargetProducer.BreatherBaseCast),
            () =>
            {
                var target = new DummyMutationSelfTargetProducer
                {
                    PopupMessageToShow = string.Empty,
                };

                target.BreatherBaseCast();

                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(string.Empty));
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
                    prefix: new HarmonyMethod(RequireMethod(typeof(MutationSelfTargetPopupTranslationPatch), nameof(MutationSelfTargetPopupTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(MutationSelfTargetPopupTranslationPatch), nameof(MutationSelfTargetPopupTranslationPatch.Finalizer))));
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
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancel)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void InvokeOwnerMethod(DummyMutationSelfTargetProducer target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, null);
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(MutationSelfTargetPopupTranslationPatch) + ".SelfTargetConfirmation");
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyMutationSelfTargetProducer), methodName);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void UseRepositoryPatternDictionary()
    {
        var localizationRoot = Path.GetFullPath(
            Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        MessagePatternTranslator.SetPatternFileForTests(null);
    }

    private sealed class DummyMutationSelfTargetProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public Action? BeforePopup { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool BreatherBaseCast()
        {
            return EmitPopup();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool FlamingRayCast()
        {
            return EmitPopup();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool FreezeBreathFireEvent()
        {
            return EmitPopup();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool FreezingRayCast()
        {
            return EmitPopup();
        }

        private bool EmitPopup()
        {
            BeforePopup?.Invoke();
            _ = DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
            return true;
        }
    }
}
