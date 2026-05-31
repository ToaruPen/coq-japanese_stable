using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SavesApiFatalSaveErrorTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupMessageTarget.Reset();
        SavesApiFatalSaveErrorTranslationPatch.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupMessageTarget.Reset();
        SavesApiFatalSaveErrorTranslationPatch.ResetForTests();
    }

    [Test]
    public void Patch_TranslatesPermissionFailurePopup_WhenOwnerPatched()
    {
        const string source =
            "There was a permission error while trying to access your save directory.\n\n" +
            "Access denied: /Users/test/Saves\n\n" +
            "Caves of Qud will exit now since we cannot save games. Please check your directory’s permissions.\n";

        var buttons = new List<DummyPopupMessageItem>
        {
            new("Quit", "Accept,Cancel", "Cancel"),
        };

        WithPatchedOwner(() => new DummyPopupMessageTarget().ShowPopup(source, buttons, title: "Error reading save location."));

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupMessageTarget.LastMessage,
                Is.EqualTo(
                    "セーブディレクトリへのアクセス中に権限エラーが発生した。\n\n" +
                    "Access denied: /Users/test/Saves\n\n" +
                    "ゲームを保存できないため、Caves of Qud を終了する。ディレクトリの権限を確認してください。\n"));
            Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("セーブ場所の読み取りエラー"));
            Assert.That(DummyPopupMessageTarget.LastButtons?.Single().text, Is.EqualTo("終了"));
            Assert.That(HitCount("PermissionBody"), Is.EqualTo(1));
            Assert.That(HitCount("Title"), Is.EqualTo(1));
            Assert.That(HitCount("QuitButton"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_TranslatesGenericFailurePopup_WhenOwnerPatched()
    {
        const string source =
            "There was an error while trying to access your save directory.\n\n" +
            "Directory: /Users/test/Saves\n\n" +
            "Caves of Qud will exit now since we cannot save games. Please check your directory’s permissions.\n";

        var buttons = new List<DummyPopupMessageItem>
        {
            new("Quit", "Accept,Cancel", "Cancel"),
        };

        WithPatchedOwner(() => new DummyPopupMessageTarget().ShowPopup(source, buttons, title: "Error reading save location."));

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupMessageTarget.LastMessage,
                Is.EqualTo(
                    "セーブディレクトリへのアクセス中にエラーが発生した。\n\n" +
                    "ディレクトリ: /Users/test/Saves\n\n" +
                    "ゲームを保存できないため、Caves of Qud を終了する。ディレクトリの権限を確認してください。\n"));
            Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("セーブ場所の読み取りエラー"));
            Assert.That(DummyPopupMessageTarget.LastButtons?.Single().text, Is.EqualTo("終了"));
            Assert.That(HitCount("GenericBody"), Is.EqualTo(1));
            Assert.That(HitCount("Title"), Is.EqualTo(1));
            Assert.That(HitCount("QuitButton"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_DoesNotClaimFatalSaveErrorPopup_WhenOwnerAbsent()
    {
        const string source = "There was an error while trying to access your save directory.";

        WithPatchedPopupMessageOnly(() => new DummyPopupMessageTarget().ShowPopup(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo(source));
            Assert.That(HitCount("GenericBody"), Is.Zero);
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupMessage(harmony);
            SavesApiFatalSaveErrorTranslationPatch.Prefix();
            action();
        }
        finally
        {
            _ = SavesApiFatalSaveErrorTranslationPatch.Finalizer(null);
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupMessageOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupMessage(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupMessage(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupMessageTranslationPatch),
            "Popup.ProducerText." + nameof(SavesApiFatalSaveErrorTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
