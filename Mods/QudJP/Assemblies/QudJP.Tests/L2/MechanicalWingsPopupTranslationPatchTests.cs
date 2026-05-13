using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MechanicalWingsPopupTranslationPatchTests
{
    private const string StartupSource = "The {{Y|mechanical wings}} are still starting up.";
    private const string UnresponsiveSource = "The {{Y|mechanical wings}} are unresponsive.";
    private const string SingularStartupSource = "The {{Y|gyrocopter backpack}} is still starting up.";
    private const string SingularUnresponsiveSource = "The {{Y|gyrocopter backpack}} is unresponsive.";
    private const string StartupTranslated = "{{Y|mechanical wings}}はまだ起動中だ";
    private const string UnresponsiveTranslated = "{{Y|mechanical wings}}は反応しなくなった。";
    private const string SingularStartupTranslated = "{{Y|gyrocopter backpack}}はまだ起動中だ";
    private const string SingularUnresponsiveTranslated = "{{Y|gyrocopter backpack}}は反応しなくなった。";

    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(RepositoryDictionaryDirectory());
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(StartupSource, StartupTranslated, "MechanicalWingsStartup")]
    [TestCase(UnresponsiveSource, UnresponsiveTranslated, "MechanicalWingsUnresponsive")]
    [TestCase(SingularStartupSource, SingularStartupTranslated, "MechanicalWingsStartup")]
    [TestCase(SingularUnresponsiveSource, SingularUnresponsiveTranslated, "MechanicalWingsUnresponsive")]
    public void Patch_TranslatesMechanicalWingsStartupPopup_WhenOwnerPatched(
        string source,
        string expected,
        string expectedFamilySuffix)
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMechanicalWingsProducer
            {
                PopupMessageToShow = source,
            };

            target.TryStartup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.Show." + expectedFamilySuffix),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        RunWithPopupPatchOnly(() => DummyPopupShow.Show(StartupSource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(StartupSource));
            Assert.That(GetStartupHitCount(), Is.EqualTo(0));
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(StartupSource);

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMechanicalWingsProducer
            {
                PopupMessageToShow = source,
            };

            target.TryStartup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(StartupSource));
                Assert.That(GetStartupHitCount(), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMechanicalWingsProducer
            {
                PopupMessageToShow = string.Empty,
            };

            target.TryStartup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(GetStartupHitCount(), Is.Zero);
                Assert.That(GetUnresponsiveHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The {{Y|mechanical wings}} are humming.";

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMechanicalWingsProducer
            {
                PopupMessageToShow = source,
            };

            target.TryStartup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(GetStartupHitCount(), Is.Zero);
                Assert.That(GetUnresponsiveHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        RunWithOwnerAndPopupPatches(
            [
                nameof(DummyMechanicalWingsProducer.TryStartup),
                nameof(DummyMechanicalWingsProducer.TryNestedStartup),
            ],
            () =>
            {
                var innerTarget = new DummyMechanicalWingsProducer
                {
                    PopupMessageToShow = UnresponsiveSource,
                };
                var outerTarget = new DummyMechanicalWingsProducer
                {
                    PopupMessageToShow = StartupSource,
                    BeforePopup = () =>
                    {
                        innerTarget.TryNestedStartup();

                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(UnresponsiveTranslated));
                            Assert.That(GetUnresponsiveHitCount(), Is.EqualTo(1));
                            Assert.That(GetStartupHitCount(), Is.Zero);
                        });
                    },
                };

                outerTarget.TryStartup();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(StartupTranslated));
                    Assert.That(GetUnresponsiveHitCount(), Is.EqualTo(1));
                    Assert.That(GetStartupHitCount(), Is.EqualTo(1));
                });

                DummyPopupShow.ShowFail(StartupSource);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(StartupSource));
                    Assert.That(GetUnresponsiveHitCount(), Is.EqualTo(1));
                    Assert.That(GetStartupHitCount(), Is.EqualTo(1));
                });
            });
    }

    private static string RepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }

    private static string RepositoryMessageFramePath()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return (parameters.Length == 0
                ? AccessTools.Method(type, methodName)
                : AccessTools.Method(type, methodName, parameters))
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void RunWithPopupPatchOnly(Action action)
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

    private static void RunWithOwnerAndPopupPatches(Action action)
    {
        RunWithOwnerAndPopupPatches([nameof(DummyMechanicalWingsProducer.TryStartup)], action);
    }

    private static void RunWithOwnerAndPopupPatches(string[] ownerMethodNames, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            foreach (var ownerMethodName in ownerMethodNames)
            {
                harmony.Patch(
                    original: RequireMethod(typeof(DummyMechanicalWingsProducer), ownerMethodName),
                    prefix: new HarmonyMethod(RequireMethod(typeof(MechanicalWingsPopupTranslationPatch), nameof(MechanicalWingsPopupTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(MechanicalWingsPopupTranslationPatch), nameof(MechanicalWingsPopupTranslationPatch.Finalizer), typeof(Exception))));
            }

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

    private static int GetStartupHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.MechanicalWingsStartup");
    }

    private static int GetUnresponsiveHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.MechanicalWingsUnresponsive");
    }

    private sealed class DummyMechanicalWingsProducer
    {
        public string PopupMessageToShow = string.Empty;
        public Action? BeforePopup;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryStartup()
        {
            BeforePopup?.Invoke();
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryNestedStartup()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return false;
        }
    }
}
