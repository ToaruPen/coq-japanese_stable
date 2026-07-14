using System.Diagnostics;
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
public sealed class SavesApiReadSaveJsonTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-savesapi-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        AsyncDummySavesApiTarget.ResultTask = null;

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Postfix_DoesNotBlockPendingRead_AndTranslatesAfterSuccessfulCompletion()
    {
        WriteDictionary(("Total size: {0}", "合計サイズ：{0}"));

        var completion = new TaskCompletionSource<Qud.API.SaveGameInfo?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsyncDummySavesApiTarget.ResultTask = completion.Task;

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(AsyncDummySavesApiTarget), nameof(AsyncDummySavesApiTarget.ReadSaveJson)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SavesApiReadSaveJsonTranslationPatch), nameof(SavesApiReadSaveJsonTranslationPatch.Postfix))));

            var adapted = AsyncDummySavesApiTarget.ReadSaveJson("dir", "Primary.json");

            Assert.Multiple(() =>
            {
                Assert.That(adapted, Is.Not.SameAs(completion.Task));
                Assert.That(adapted.IsCompleted, Is.False);
            });

            var saveInfo = new Qud.API.SaveGameInfo { Size = "Total size: 12mb" };
            completion.SetResult(saveInfo);
            var result = await adapted.ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.SameAs(saveInfo));
                Assert.That(result!.Size, Is.EqualTo("合計サイズ：12mb"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SavesApiReadSaveJsonTranslationPatch),
                        "Total size: {0}"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static class AsyncDummySavesApiTarget
    {
        internal static Task<Qud.API.SaveGameInfo?>? ResultTask { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Task<Qud.API.SaveGameInfo?> ReadSaveJson(string dir, string file)
        {
            _ = dir;
            _ = file;
            return ResultTask
                ?? throw new InvalidOperationException("Configure the dummy result task before invoking the target.");
        }
    }

    [Test]
    public void Postfix_UsesExactGameTaskByRefContract()
    {
        var postfix = RequireMethod(
            typeof(SavesApiReadSaveJsonTranslationPatch),
            nameof(SavesApiReadSaveJsonTranslationPatch.Postfix));

        Assert.Multiple(() =>
        {
            Assert.That(postfix.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(postfix.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(
                postfix.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(Task<Qud.API.SaveGameInfo>).MakeByRefType()));
        });
    }

    [Test]
    public async Task AdaptCompletion_PreservesSuccessfulNullResult()
    {
        var adapted = SavesApiReadSaveJsonTranslationPatch.AdaptCompletion(
            Task.FromResult<DummySaveGameInfo?>(null),
            SavesApiReadSaveJsonTranslationPatch.TranslateResult);

        var result = await adapted.ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(adapted.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(SavesApiReadSaveJsonTranslationPatch),
                    "Total size: {0}"),
                Is.Zero);
        });
    }

    [Test]
    public async Task AdaptCompletion_LeavesHealthyResultUnchanged_WhenTemplateIsMissing()
    {
        var original = new DummySaveGameInfo();
        var adapted = SavesApiReadSaveJsonTranslationPatch.AdaptCompletion(
            Task.FromResult(original),
            SavesApiReadSaveJsonTranslationPatch.TranslateResult);

        var result = await adapted.ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(original));
            Assert.That(result.Size, Is.EqualTo("Total size: 12mb"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(SavesApiReadSaveJsonTranslationPatch),
                    "Total size: {0}"),
                Is.Zero);
        });
    }

    [Test]
    public async Task AdaptCompletion_PreservesHealthyResult_WhenTransformAndFailureLoggingThrow()
    {
        var original = new DummySaveGameInfo();
        var listener = new ThrowingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var adapted = SavesApiReadSaveJsonTranslationPatch.AdaptCompletion(
                Task.FromResult(original),
                (DummySaveGameInfo _) => throw new InvalidOperationException("transform failed"));

            var result = await adapted.ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.SameAs(original));
                Assert.That(adapted.Status, Is.EqualTo(TaskStatus.RanToCompletion));
                Assert.That(listener.TraceEventCalls, Is.GreaterThan(0));
            });
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Test]
    public async Task AdaptCompletion_PreservesAllFaultExceptions_WithoutCallingTransform()
    {
        var first = new InvalidOperationException("first failure");
        var second = new ArgumentException("second failure");
        var completion = new TaskCompletionSource<DummySaveGameInfo?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transformCalled = false;
        var adapted = SavesApiReadSaveJsonTranslationPatch.AdaptCompletion(
            completion.Task,
            _ => transformCalled = true);

        completion.SetException(new Exception[] { first, second });
        await Task.WhenAny(adapted).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(transformCalled, Is.False);
            Assert.That(adapted.IsFaulted, Is.True);
            Assert.That(adapted.Exception!.InnerExceptions, Is.EqualTo(new Exception[] { first, second }));
        });
    }

    [Test]
    public async Task AdaptCompletion_PreservesCancellationStateAndToken_WithoutCallingTransform()
    {
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<DummySaveGameInfo?>(
            new object(),
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transformCalled = false;
        var adapted = SavesApiReadSaveJsonTranslationPatch.AdaptCompletion(
            completion.Task,
            _ => transformCalled = true);

        await cancellation.CancelAsync().ConfigureAwait(false);
        completion.SetCanceled(cancellation.Token);
        await Task.WhenAny(adapted).ConfigureAwait(false);
        var exception = Assert.ThrowsAsync<TaskCanceledException>(
            async () => await adapted.ConfigureAwait(false));

        Assert.Multiple(() =>
        {
            Assert.That(transformCalled, Is.False);
            Assert.That(adapted.IsCanceled, Is.True);
            Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
        });
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class ThrowingTraceListener : TraceListener
    {
        internal int TraceEventCalls { get; private set; }

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? format,
            params object?[]? args)
        {
            TraceEventCalls++;
            throw new InvalidOperationException("trace failed");
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
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

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(tempDirectory, "saves-api-l2.ja.json"),
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
}
