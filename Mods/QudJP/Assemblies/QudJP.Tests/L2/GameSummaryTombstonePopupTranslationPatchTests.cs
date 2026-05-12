using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameSummaryTombstonePopupTranslationPatchTests
{
    private const string SavedTemplate = "Your tombstone file was saved:\n\n{0}";
    private const string ErrorTemplate = "There was an error saving: {0}";
    private const string TombstonePath = "/tmp/Qudman-5-13-2026-2-00 AM.txt";
    private const string SavedMessage = "Your tombstone file was saved:\n\n" + TombstonePath;
    private const string ErrorMessage = "There was an error saving: " + TombstonePath;
    private const string TranslatedSavedMessage = "墓碑ファイルを保存しました:\n\n" + TombstonePath;
    private const string TranslatedErrorMessage = "保存中にエラーが発生しました: " + TombstonePath;

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-game-summary-tombstone-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();

        WriteDictionary(
            (SavedTemplate, "墓碑ファイルを保存しました:\n\n{0}"),
            (ErrorTemplate, "保存中にエラーが発生しました: {0}"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(nameof(DummyGameSummaryTombstoneProducer.ModernSaveTombstone), SavedMessage, TranslatedSavedMessage, "GameSummaryTombstoneSaved")]
    [TestCase(nameof(DummyGameSummaryTombstoneProducer.ModernSaveTombstone), ErrorMessage, TranslatedErrorMessage, "GameSummaryTombstoneError")]
    [TestCase(nameof(DummyGameSummaryTombstoneProducer.ClassicShow), SavedMessage, TranslatedSavedMessage, "GameSummaryTombstoneSaved")]
    [TestCase(nameof(DummyGameSummaryTombstoneProducer.ClassicShow), ErrorMessage, TranslatedErrorMessage, "GameSummaryTombstoneError")]
    public void Patch_TranslatesTombstonePopup_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string expectedFamilySuffix)
    {
        RunWithOwnerAndPopupPatches(
            methodName,
            () =>
            {
                var target = new DummyGameSummaryTombstoneProducer
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

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
    public void Patch_DoesNotTranslateTombstonePopup_WhenOwnerAbsent()
    {
        RunWithPopupPatchOnly(() => DummyPopupShow.Show(SavedMessage));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(SavedMessage));
            Assert.That(GetSavedHitCount(), Is.EqualTo(0));
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedTombstonePopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(SavedMessage);

        RunWithOwnerAndPopupPatches(
            nameof(DummyGameSummaryTombstoneProducer.ModernSaveTombstone),
            () =>
            {
                var target = new DummyGameSummaryTombstoneProducer
                {
                    PopupMessageToShow = source,
                };

                target.ModernSaveTombstone();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(SavedMessage));
                    Assert.That(GetSavedHitCount(), Is.EqualTo(0));
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(
            nameof(DummyGameSummaryTombstoneProducer.ModernSaveTombstone),
            () =>
            {
                var target = new DummyGameSummaryTombstoneProducer
                {
                    PopupMessageToShow = string.Empty,
                };

                target.ModernSaveTombstone();

                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            });
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

    private static void RunWithOwnerAndPopupPatches(string ownerMethodName, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchOwner(harmony, ownerMethodName);
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchOwner(Harmony harmony, string ownerMethodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyGameSummaryTombstoneProducer), ownerMethodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(GameSummaryTombstonePopupTranslationPatch), nameof(GameSummaryTombstonePopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(GameSummaryTombstonePopupTranslationPatch), nameof(GameSummaryTombstonePopupTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void InvokeOwnerMethod(DummyGameSummaryTombstoneProducer target, string methodName)
    {
        if (string.Equals(methodName, nameof(DummyGameSummaryTombstoneProducer.ClassicShow), StringComparison.Ordinal))
        {
            target.ClassicShow();
            return;
        }

        target.ModernSaveTombstone();
    }

    private static int GetSavedHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.GameSummaryTombstoneSaved");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append('{');
            builder.Append("\"key\":");
            builder.Append(System.Text.Json.JsonSerializer.Serialize(entries[index].key));
            builder.Append(',');
            builder.Append("\"text\":");
            builder.Append(System.Text.Json.JsonSerializer.Serialize(entries[index].text));
            builder.Append('}');
        }

        builder.Append("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "ui-game-summary-tombstone.ja.json"), builder.ToString(), Utf8WithoutBom);
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private sealed class DummyGameSummaryTombstoneProducer
    {
        public string PopupMessageToShow = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ModernSaveTombstone()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ClassicShow()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
