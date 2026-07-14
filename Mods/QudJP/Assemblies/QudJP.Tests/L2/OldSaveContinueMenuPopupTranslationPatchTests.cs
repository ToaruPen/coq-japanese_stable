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

    private static TaskCompletionSource<bool> moveNextFinalized = CreateFinalizerSignal();

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
        ResetFinalizerSignal();

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
    [TestCase(nameof(DummyOldSaveContinueMenuProducer.XrlCoreSaveManagement))]
    [TestCase(nameof(DummyOldSaveContinueMenuProducer.XrlGameLoadGame))]
    public async Task Patch_TranslatesOldSavePopup_InAsyncOwnerContinuation(string methodName)
    {
        await WithPatchedOwnerAndPopupAsync(
            methodName,
            async () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = OldSaveMessage,
                };

                AssertScopeInactive(OldSaveMessage);
                var invocation = InvokeOwnerMethod(target, methodName);
                AssertScopeInactive(OldSaveMessage);

                ResetFinalizerSignal();
                target.ContinueOwner();
                await invocation.ConfigureAwait(false);
                await WaitForMoveNextToReturn().ConfigureAwait(false);

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(methodName), Is.EqualTo(TranslatedOldSaveMessage));
                    Assert.That(HitCount(), Is.EqualTo(1));
                });
                AssertScopeInactive(OldSaveMessage);
            }).ConfigureAwait(false);
    }

    [Test]
    public void Patch_DoesNotRecordOwnerRoute_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() =>
        {
            _ = DummyPopupShow.ShowAsync(OldSaveMessage);
            DummyPopupShow.Show(OldSaveMessage);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(TranslatedOldSaveMessage));
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(TranslatedOldSaveMessage));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [TestCase(nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu))]
    [TestCase(nameof(DummyOldSaveContinueMenuProducer.XrlCoreSaveManagement))]
    public async Task Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched(string methodName)
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(OldSaveMessage);

        await WithPatchedOwnerAndPopupAsync(
            methodName,
            async () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = source,
                };

                var invocation = InvokeOwnerMethod(target, methodName);
                ResetFinalizerSignal();
                target.ContinueOwner();
                await invocation.ConfigureAwait(false);
                await WaitForMoveNextToReturn().ConfigureAwait(false);

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(methodName), Is.EqualTo(OldSaveMessage));
                    Assert.That(HitCount(), Is.Zero);
                });
            }).ConfigureAwait(false);
    }

    [TestCase(nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu))]
    [TestCase(nameof(DummyOldSaveContinueMenuProducer.XrlCoreSaveManagement))]
    public async Task Patch_LeavesUnknownOldSavePopupUnchanged_WhenOwnerPatched(string methodName)
    {
        const string source = "That save file comes from an unknown future save format revision.";

        await WithPatchedOwnerAndPopupAsync(
            methodName,
            async () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = source,
                };

                var invocation = InvokeOwnerMethod(target, methodName);
                ResetFinalizerSignal();
                target.ContinueOwner();
                await invocation.ConfigureAwait(false);
                await WaitForMoveNextToReturn().ConfigureAwait(false);

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(methodName), Is.EqualTo(source));
                    Assert.That(HitCount(), Is.Zero);
                });
            }).ConfigureAwait(false);
    }

    [TestCase(nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu))]
    [TestCase(nameof(DummyOldSaveContinueMenuProducer.XrlCoreSaveManagement))]
    public async Task Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched(string methodName)
    {
        await WithPatchedOwnerAndPopupAsync(
            methodName,
            async () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    PopupMessageToShow = string.Empty,
                };

                var invocation = InvokeOwnerMethod(target, methodName);
                ResetFinalizerSignal();
                target.ContinueOwner();
                await invocation.ConfigureAwait(false);
                await WaitForMoveNextToReturn().ConfigureAwait(false);

                Assert.That(LastPopupMessage(methodName), Is.EqualTo(string.Empty));
            }).ConfigureAwait(false);
    }

    [Test]
    public async Task Patch_CleansUpScope_WhenAsyncOwnerContinuationThrows()
    {
        await WithPatchedOwnerAndPopupAsync(
            nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu),
            async () =>
            {
                var target = new DummyOldSaveContinueMenuProducer
                {
                    ThrowAfterAwait = true,
                };

                var invocation = target.MainMenuContinueMenu();
                AssertScopeInactive(OldSaveMessage);

                ResetFinalizerSignal();
                target.ContinueOwner();
                await Assert.ThatAsync(
                    async () => await invocation.ConfigureAwait(false),
                    Throws.TypeOf<InvalidOperationException>()).ConfigureAwait(false);
                await WaitForMoveNextToReturn().ConfigureAwait(false);
                AssertScopeInactive(OldSaveMessage);
            }).ConfigureAwait(false);
    }

    private static async Task WithPatchedOwnerAndPopupAsync(string methodName, Func<Task> action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowMethods(harmony);
            harmony.Patch(
                original: RequireMoveNextMethod(RequireOwnerMethod(methodName)),
                prefix: new HarmonyMethod(RequireMethod(typeof(OldSaveContinueMenuPopupTranslationPatch), nameof(OldSaveContinueMenuPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(OldSaveContinueMenuPopupTranslationPatchTests), nameof(ObserveOldSaveFinalizer), typeof(Exception))));

            await action().ConfigureAwait(false);
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
            PatchPopupShowMethods(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowMethods(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(MethodBase))));

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
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(MethodBase))));
    }

    private static Task InvokeOwnerMethod(DummyOldSaveContinueMenuProducer target, string methodName)
    {
        if (string.Equals(methodName, nameof(DummyOldSaveContinueMenuProducer.XrlCoreSaveManagement), StringComparison.Ordinal))
        {
            return target.XrlCoreSaveManagement();
        }

        if (string.Equals(methodName, nameof(DummyOldSaveContinueMenuProducer.SaveManagementContinueMenu), StringComparison.Ordinal))
        {
            return target.SaveManagementContinueMenu();
        }

        return string.Equals(methodName, nameof(DummyOldSaveContinueMenuProducer.XrlGameLoadGame), StringComparison.Ordinal)
            ? target.XrlGameLoadGame()
            : target.MainMenuContinueMenu();
    }

    private static string? LastPopupMessage(string methodName)
    {
        return string.Equals(methodName, nameof(DummyOldSaveContinueMenuProducer.MainMenuContinueMenu), StringComparison.Ordinal)
               || string.Equals(methodName, nameof(DummyOldSaveContinueMenuProducer.SaveManagementContinueMenu), StringComparison.Ordinal)
            ? DummyPopupShow.LastShowAsyncMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static void AssertScopeInactive(string source)
    {
        Assert.That(
            OldSaveContinueMenuPopupTranslationPatch.TryTranslatePopupMessage(
                source,
                nameof(PopupShowTranslationPatch),
                "OldSaveContinueMenuPopup",
                out var translated),
            Is.False);
        Assert.That(translated, Is.EqualTo(source));
    }

    private static Task WaitForMoveNextToReturn()
    {
        // AsyncTaskMethodBuilder may resume the Task awaiter inline before Harmony's Finalizer runs.
        // This signal is completed only after the production Finalizer has unwound that invocation.
        return moveNextFinalized.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static Exception? ObserveOldSaveFinalizer(Exception? __exception)
    {
        var result = OldSaveContinueMenuPopupTranslationPatch.Finalizer(__exception);
        _ = moveNextFinalized.TrySetResult(true);
        return result;
    }

    private static void ResetFinalizerSignal()
    {
        moveNextFinalized = CreateFinalizerSignal();
    }

    private static TaskCompletionSource<bool> CreateFinalizerSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private static MethodInfo RequireMoveNextMethod(MethodInfo logicalMethod)
    {
        var stateMachineType = logicalMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                               ?? throw new InvalidOperationException($"Async state machine not found: {logicalMethod.Name}");
        return RequireMethod(stateMachineType, nameof(IAsyncStateMachine.MoveNext));
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
        private readonly TaskCompletionSource<bool> ownerGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string PopupMessageToShow { get; set; } = string.Empty;

        public bool ThrowAfterAwait { get; set; }

        public void ContinueOwner()
        {
            ownerGate.SetResult(true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task MainMenuContinueMenu()
        {
            await ownerGate.Task.ConfigureAwait(false);
            ThrowIfRequested();
            await DummyPopupShow.ShowAsync(PopupMessageToShow).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task SaveManagementContinueMenu()
        {
            await ownerGate.Task.ConfigureAwait(false);
            ThrowIfRequested();
            _ = nameof(SaveManagementContinueMenu);
            await DummyPopupShow.ShowAsync(PopupMessageToShow).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task XrlCoreSaveManagement()
        {
            await ownerGate.Task.ConfigureAwait(false);
            ThrowIfRequested();
            ShowPopupSynchronously();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task XrlGameLoadGame()
        {
            await ownerGate.Task.ConfigureAwait(false);
            ThrowIfRequested();
            _ = nameof(XrlGameLoadGame);
            ShowPopupSynchronously();
        }

        private void ShowPopupSynchronously()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        private void ThrowIfRequested()
        {
            if (ThrowAfterAwait)
            {
                throw new InvalidOperationException("Dummy async owner failed after suspension.");
            }
        }
    }
}
