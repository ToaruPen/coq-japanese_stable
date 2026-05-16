using System.Reflection;
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
        "No blueprint by ID '{{W|missing-body}}' found.",
        "ID '{{W|missing-body}}' のブループリントが見つからない。",
        "MissingBlueprint")]
    [TestCase(
        "You have nothing that meets the requirement of the {{Y|armament}}.",
        "{{Y|armament}}の要件を満たすものを持っていない。",
        "MissingRequirement")]
    public void TryTranslatePopupMessage_TranslatesKnownGolemSelectionPopup_WithoutOwnerScope(
        string source,
        string expected,
        string detail)
    {
        var translated = GolemQuestSelectionPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            "Popup.Show",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(HitCount(detail), Is.EqualTo(1));
        });
    }

    [TestCase(
        "No blueprint by ID '{{W|missing-body}}' found.",
        "ID '{{W|missing-body}}' のブループリントが見つからない。",
        "MissingBlueprint")]
    [TestCase(
        "You have nothing that meets the requirement of the {{Y|armament}}.",
        "{{Y|armament}}の要件を満たすものを持っていない。",
        "MissingRequirement")]
    public void Patch_TranslatesKnownGolemSelectionPopup_WhenOwnerAbsent(
        string source,
        string expected,
        string detail)
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation("No blueprint by ID '{{W|missing-body}}' found.");

        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("No blueprint by ID '{{W|missing-body}}' found."));
                Assert.That(HitCount("MissingBlueprint"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged()
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail(string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("MissingBlueprint"), Is.Zero);
                Assert.That(HitCount("MissingRequirement"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged()
    {
        const string source = "There is no suitable golem material here.";

        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("MissingBlueprint"), Is.Zero);
                Assert.That(HitCount("MissingRequirement"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_TranslatesRepeatedKnownGolemPopups()
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail("No blueprint by ID '{{W|missing-body}}' found.");
            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ID '{{W|missing-body}}' のブループリントが見つからない。"));
                Assert.That(HitCount("MissingBlueprint"), Is.EqualTo(1));
                Assert.That(HitCount("MissingRequirement"), Is.Zero);
            });

            DummyPopupShow.ShowFail("You have nothing that meets the requirement of the {{Y|armament}}.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|armament}}の要件を満たすものを持っていない。"));
                Assert.That(HitCount("MissingBlueprint"), Is.EqualTo(1));
                Assert.That(HitCount("MissingRequirement"), Is.EqualTo(1));
            });
        });
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

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(GolemQuestSelectionPopupTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
