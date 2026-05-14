using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class JournalScreenPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "journal-patterns.ja.json"));

        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        JournalPatternTranslator.ResetForTests();
    }

    [Test]
    public void HandleDelete_TranslatesRecipeDeleteConfirmation_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(JournalScreenPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyJournalScreenPopupProducerTarget.HandleDelete)),
            () =>
            {
                var target = new DummyJournalScreenPopupProducerTarget
                {
                    PopupMessageToShow = "Are you sure you want to delete {{y|ワタワイン粥}}?",
                };

                target.HandleDelete();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("本当に{{y|ワタワイン粥}}を削除しますか？"));
                    Assert.That(HitCount("RecipeDeleteConfirmation"), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "You stop calling this location '{{Y|古い名前}}' and start calling it '{{G|新しい名前}}'.",
        "場所の呼称を「{{Y|古い名前}}」から「{{G|新しい名前}}」に変更した。",
        "RenameLocation")]
    [TestCase(
        "You start calling this location '{{G|新しい名前}}'.",
        "場所を「{{G|新しい名前}}」と呼ぶことにした。",
        "NameLocation")]
    public void Show_TranslatesLocationRenamePopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(JournalScreenPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyJournalScreenPopupProducerTarget.Show)),
            () =>
            {
                var target = new DummyJournalScreenPopupProducerTarget { PopupMessageToShow = source };

                target.Show();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "Are you sure you want to delete {{y|ワタワイン粥}}?",
        "RecipeDeleteConfirmation")]
    [TestCase(
        "You start calling this location '{{G|新しい名前}}'.",
        "NameLocation")]
    public void TryTranslatePopupMessage_DoesNotClaimJournalScreenPopup_WhenOwnerAbsent(
        string source,
        string detail)
    {
        var claimed = JournalScreenPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            nameof(JournalScreenPopupTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [TestCase(
        nameof(DummyJournalScreenPopupProducerTarget.HandleDelete),
        "Are you sure you want to delete {{y|ワタワイン粥}}?",
        "RecipeDeleteConfirmation")]
    [TestCase(
        nameof(DummyJournalScreenPopupProducerTarget.Show),
        "You start calling this location '{{G|新しい名前}}'.",
        "NameLocation")]
    public void JournalScreenPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched(
        string methodName,
        string source,
        string detail)
    {
        AssertOwnerPopup(methodName, MessageFrameTranslator.MarkDirectTranslation(source), source, detail, expectedHits: 0);
    }

    [TestCase(nameof(DummyJournalScreenPopupProducerTarget.HandleDelete), "RecipeDeleteConfirmation")]
    [TestCase(nameof(DummyJournalScreenPopupProducerTarget.Show), "NameLocation")]
    public void JournalScreenPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched(string methodName, string detail)
    {
        AssertOwnerPopup(methodName, string.Empty, string.Empty, detail, expectedHits: 0);
    }

    [Test]
    public void Show_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            nameof(DummyJournalScreenPopupProducerTarget.Show),
            "This place already has a name.",
            "This place already has a name.",
            "NameLocation",
            expectedHits: 0);
    }

    [Test]
    public void HandleDelete_LeavesFixedDeleteEntryConfirmationUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(JournalScreenPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyJournalScreenPopupProducerTarget.HandleDelete)),
            () =>
            {
                var target = new DummyJournalScreenPopupProducerTarget
                {
                    PopupMessageToShow = "Are you sure you want to delete this entry?",
                };

                target.HandleDelete();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("Are you sure you want to delete this entry?"));
                    Assert.That(HitCount("RecipeDeleteConfirmation"), Is.Zero);
                });
            });
    }

    private static void AssertOwnerPopup(
        string methodName,
        string source,
        string expected,
        string detail,
        int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(JournalScreenPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummyJournalScreenPopupProducerTarget { PopupMessageToShow = source };

                _ = RequireOwnerMethod(methodName).Invoke(target, []);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage ?? DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyJournalScreenPopupProducerTarget), methodName);
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(JournalScreenPopupTranslationPatch), detail);
    }
}
