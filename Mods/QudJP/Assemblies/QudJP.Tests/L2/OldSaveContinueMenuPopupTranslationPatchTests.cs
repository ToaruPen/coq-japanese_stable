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
public sealed class OldSaveContinueMenuPopupTranslationPatchTests
{
    private const string OldSaveTemplate =
        "That save file looks like it's from an older save format revision ({0}). Sorry!\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.";

    private const string OldSaveMessage =
        "That save file looks like it's from an older save format revision (2.0.3). Sorry!\n\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.";

    private const string TranslatedOldSaveMessage =
        "このセーブデータは古いフォーマット（2.0.3）のようです。\nゲームクライアントで以前のブランチに切り替えれば読み込める可能性があります。";

    private const string TranslatedOldSaveTemplate =
        "このセーブデータは古いフォーマット（{0}）のようです。\nゲームクライアントで以前のブランチに切り替えれば読み込める可能性があります。";

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-old-save-popup-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();

        WriteDictionary((OldSaveTemplate, TranslatedOldSaveTemplate));
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu))]
    [TestCase(nameof(DummyOldSaveContinueMenuProducer.SaveManagementContinueMenu))]
    public void Patch_TranslatesOldSavePopup_WhenOwnerPatched(string methodName)
    {
        WithPatchedOwnerAndPopup(methodName, () =>
        {
            var target = new DummyOldSaveContinueMenuProducer
            {
                PopupMessageToShow = OldSaveMessage,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(TranslatedOldSaveMessage));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotRecordOwnerRoute_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() =>
        {
            _ = DummyPopupShow.ShowAsync(OldSaveMessage);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(TranslatedOldSaveMessage));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(OldSaveMessage);

        WithPatchedOwnerAndPopup(
            nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu),
            () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = source,
                };

                target.MainMenuContinueMenu();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(OldSaveMessage));
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesUnknownOldSavePopupUnchanged_WhenOwnerPatched()
    {
        const string source = "That save file comes from an unknown future save format revision.";

        WithPatchedOwnerAndPopup(
            nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu),
            () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = source,
                };

                target.MainMenuContinueMenu();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
                    Assert.That(HitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwnerAndPopup(
            nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu),
            () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = string.Empty,
                };

                target.MainMenuContinueMenu();

                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(string.Empty));
            });
    }

    private static void WithPatchedOwnerAndPopup(string methodName, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowAsync(harmony);
            harmony.Patch(
                original: RequireOwnerMethod(methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(OldSaveContinueMenuPopupTranslationPatch), nameof(OldSaveContinueMenuPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(OldSaveContinueMenuPopupTranslationPatch), nameof(OldSaveContinueMenuPopupTranslationPatch.Finalizer), typeof(Exception))));

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
            PatchPopupShowAsync(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowAsync(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowAsync),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix), typeof(string).MakeByRefType())));
    }

    private static void InvokeOwnerMethod(DummyOldSaveContinueMenuProducer target, string methodName)
    {
        if (string.Equals(methodName, nameof(DummyOldSaveContinueMenuProducer.SaveManagementContinueMenu), StringComparison.Ordinal))
        {
            target.SaveManagementContinueMenu();
            return;
        }

        target.MainMenuContinueMenu();
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "Popup.Show." + nameof(OldSaveContinueMenuPopupTranslationPatch),
            "Popup.ProducerText.XRLCoreOldSave");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(tempDirectory, "old-save-popup-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyOldSaveContinueMenuProducer), methodName);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                   null,
                   parameters,
                   null)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyOldSaveContinueMenuProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MainMenuContinueMenu()
        {
            _ = DummyPopupShow.ShowAsync(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SaveManagementContinueMenu()
        {
            _ = DummyPopupShow.ShowAsync(PopupMessageToShow);
        }
    }
}
