using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HighScoresDeletePopupTranslationPatchTests
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
    public void HandleDelete_TranslatesDeleteConfirmationPopup_WhenOwnerPatched()
    {
        WithPatchedHandleDelete(() =>
        {
            var target = new DummyHighScoresDeleteTarget
            {
                PopupMessageToShow = "Are you sure you want to delete this?\n\n{{W|Marizah}} died in 12,345 turns",
            };

            target.HandleDelete();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("本当にこれを削除しますか？\n\n{{W|Marizah}} died in 12,345 turns"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void ScoresShow_TranslatesDeleteConfirmationPopup_WhenOwnerPatched()
    {
        WithPatchedScoresShow(() =>
        {
            var target = new DummyHighScoresDeleteTarget
            {
                PopupMessageToShow = "Are you sure you want to delete this?\n\n{{W|Marizah}} died in 12,345 turns",
            };

            target.ShowScores();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("本当にこれを削除しますか？\n\n{{W|Marizah}} died in 12,345 turns"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HandleDelete_DoesNotTranslateDeleteConfirmationPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNoAsync(harmony);

            _ = DummyPopupShow.ShowYesNoAsync("Are you sure you want to delete this?\n\nMarizah died in 12,345 turns")
                .GetAwaiter()
                .GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("Are you sure you want to delete this?\n\nMarizah died in 12,345 turns"));
                Assert.That(HitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ScoresShow_DoesNotTranslateDeleteConfirmationPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNo(harmony);

            _ = DummyPopupShow.ShowYesNo("Are you sure you want to delete this?\n\nMarizah died in 12,345 turns");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("Are you sure you want to delete this?\n\nMarizah died in 12,345 turns"));
                Assert.That(HitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HandleDelete_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        WithPatchedHandleDelete(() =>
        {
            var target = new DummyHighScoresDeleteTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(
                    "Are you sure you want to delete this?\n\nMarizah died in 12,345 turns"),
            };

            target.HandleDelete();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("Are you sure you want to delete this?\n\nMarizah died in 12,345 turns"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleDelete_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedHandleDelete(() =>
        {
            var target = new DummyHighScoresDeleteTarget
            {
                PopupMessageToShow = string.Empty,
            };

            target.HandleDelete();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleDelete_LeavesUnknownNonEmptyPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "Delete the selected high score entry?";

        WithPatchedHandleDelete(() =>
        {
            var target = new DummyHighScoresDeleteTarget
            {
                PopupMessageToShow = source,
            };

            target.HandleDelete();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo(source));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleDelete_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        const string innerSource = "Are you sure you want to delete this?\n\n{{W|Inner}} died in 1 turn";
        const string outerSource = "Are you sure you want to delete this?\n\n{{W|Outer}} died in 2 turns";
        WithPatchedHandleDelete(
            [
                nameof(DummyHighScoresDeleteTarget.HandleDelete),
                nameof(DummyHighScoresDeleteTarget.HandleNestedDelete),
            ],
            () =>
            {
                var innerTarget = new DummyHighScoresDeleteTarget
                {
                    PopupMessageToShow = innerSource,
                };
                var outerTarget = new DummyHighScoresDeleteTarget
                {
                    PopupMessageToShow = outerSource,
                    BeforePopup = () =>
                    {
                        innerTarget.HandleNestedDelete();

                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("本当にこれを削除しますか？\n\n{{W|Inner}} died in 1 turn"));
                            Assert.That(HitCount(), Is.EqualTo(1));
                        });
                    },
                };

                outerTarget.HandleDelete();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("本当にこれを削除しますか？\n\n{{W|Outer}} died in 2 turns"));
                    Assert.That(HitCount(), Is.EqualTo(2));
                });
            });
    }

    private static void WithPatchedHandleDelete(Action assertion)
    {
        WithPatchedHandleDelete([nameof(DummyHighScoresDeleteTarget.HandleDelete)], assertion);
    }

    private static void WithPatchedHandleDelete(string[] ownerMethodNames, Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNoAsync(harmony);
            foreach (var ownerMethodName in ownerMethodNames)
            {
                harmony.Patch(
                    original: RequireMethod(typeof(DummyHighScoresDeleteTarget), ownerMethodName),
                    prefix: new HarmonyMethod(RequireMethod(typeof(HighScoresDeletePopupTranslationPatch), nameof(HighScoresDeletePopupTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(HighScoresDeletePopupTranslationPatch), nameof(HighScoresDeletePopupTranslationPatch.Finalizer), typeof(Exception))));
            }

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedScoresShow(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNo(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyHighScoresDeleteTarget), nameof(DummyHighScoresDeleteTarget.ShowScores)),
                prefix: new HarmonyMethod(RequireMethod(typeof(HighScoresDeletePopupTranslationPatch), nameof(HighScoresDeletePopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(HighScoresDeletePopupTranslationPatch), nameof(HighScoresDeletePopupTranslationPatch.Finalizer), typeof(Exception))));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowYesNoAsync(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoAsync)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
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

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(HighScoresDeletePopupTranslationPatch) + ".DeleteConfirmation");
    }

    private sealed class DummyHighScoresDeleteTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;
        public Action? BeforePopup { get; init; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleDelete()
        {
            BeforePopup?.Invoke();
            _ = DummyPopupShow.ShowYesNoAsync(PopupMessageToShow).GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleNestedDelete()
        {
            _ = DummyPopupShow.ShowYesNoAsync(PopupMessageToShow).GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowScores()
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
    }
}
