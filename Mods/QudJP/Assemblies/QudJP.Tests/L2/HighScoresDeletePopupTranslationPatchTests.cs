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

    private static void WithPatchedHandleDelete(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNoAsync(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyHighScoresDeleteTarget), nameof(DummyHighScoresDeleteTarget.HandleDelete)),
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void HandleDelete()
        {
            _ = DummyPopupShow.ShowYesNoAsync(PopupMessageToShow).GetAwaiter().GetResult();
        }
    }
}
