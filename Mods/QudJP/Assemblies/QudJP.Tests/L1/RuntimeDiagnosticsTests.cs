namespace QudJP.Tests.L1;

using System.Diagnostics;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class RuntimeDiagnosticsTests
{
    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
    }

    [Test]
    public void VerboseProbesEnabled_DefaultsToDevBuildSetting()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);

        Assert.Multiple(() =>
        {
            Assert.That(RuntimeDiagnostics.BuildFlavor, Is.EqualTo("dev"));
            Assert.That(RuntimeDiagnostics.VerboseProbesEnabled, Is.True);
        });
    }

    [Test]
    public void VerboseProbeOverride_DisablesDynamicAndFinalOutputProbeLogs()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(false);
        DynamicTextObservability.ResetForTests();
        FinalOutputObservability.ResetForTests();
        var finalOutputObservation = new FinalOutputObservation(
            "Sink",
            "Route",
            "Detail",
            FinalOutputObservability.PhaseBeforeSink,
            FinalOutputObservability.TranslationStatusSinkUnclaimed,
            FinalOutputObservability.NotEvaluatedStatus,
            FinalOutputObservability.NotEvaluatedStatus,
            "source",
            "source",
            string.Empty,
            "source");

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            DynamicTextObservability.RecordTransform("Route", "Family", "source", "translated");
            FinalOutputObservability.Record(finalOutputObservation);
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Not.Contain("DynamicTextProbe/v1"));
            Assert.That(output, Does.Not.Contain("FinalOutputProbe/v1"));
            Assert.That(DynamicTextObservability.GetRouteFamilyHitCountForTests("Route", "Family"), Is.Zero);
            Assert.That(FinalOutputObservability.GetHitCountForTests(finalOutputObservation), Is.Zero);
        });
    }

    [Test]
    public void LogImportant_WritesRegardlessOfVerboseProbeOverride()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(false);

        var output = TestTraceHelper.CaptureTrace(() =>
            RuntimeDiagnostics.LogImportant("[QudJP] Build marker test"));

        Assert.That(output, Does.Contain("[QudJP] Build marker test"));
    }

    [Test]
    public void StartupTiming_WritesStructuredElapsedMarker()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            RuntimeStartupTiming.LogElapsed(
                "harmony.prepare_patch_types",
                TimeSpan.FromMilliseconds(12.3456),
                "patch_types=140;prepared=139"));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("[QudJP] StartupTiming/v1:"));
            Assert.That(output, Does.Contain("phase=harmony.prepare_patch_types"));
            Assert.That(output, Does.Contain("elapsed_ms=12.346"));
            Assert.That(output, Does.Contain("detail=patch_types\\=140\\;prepared\\=139"));
        });
    }

    [Test]
    public void LogVerboseProbe_DoesNotInvokeMessageFactory_WhenVerboseProbesAreDisabled()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(false);
        var factoryCalls = 0;

        var output = TestTraceHelper.CaptureTrace(() =>
            RuntimeDiagnostics.LogVerboseProbe(() =>
            {
                factoryCalls++;
                return "[QudJP] DynamicTextProbe/v1: should-not-build";
            }));

        Assert.Multiple(() =>
        {
            Assert.That(factoryCalls, Is.Zero);
            Assert.That(output, Does.Not.Contain("DynamicTextProbe/v1"));
        });
    }

    [Test]
    public void LogVerboseProbe_EmitsMessage_WhenVerboseProbesAreEnabled()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);

        var output = TestTraceHelper.CaptureTrace(() =>
            RuntimeDiagnostics.LogVerboseProbe(() => "[QudJP] DynamicTextProbe/v1: route='test'"));

        Assert.That(output, Does.Contain("DynamicTextProbe/v1"));
    }

    [Test]
    public void LogVerboseProbe_SkipsNullOrEmptyMessages()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            RuntimeDiagnostics.LogVerboseProbe(() => null);
            RuntimeDiagnostics.LogVerboseProbe(() => string.Empty);
        });

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void LogWarning_EmitsWarningTraceEvent()
    {
        using var listener = new EventTypeTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            RuntimeDiagnostics.LogWarning("[QudJP] warning event");
            Trace.Flush();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.That(listener.EventTypes, Does.Contain(TraceEventType.Warning));
    }

    [Test]
    public void LogError_EmitsErrorTraceEvent()
    {
        using var listener = new EventTypeTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            RuntimeDiagnostics.LogError("[QudJP] error event");
            Trace.Flush();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.That(listener.EventTypes, Does.Contain(TraceEventType.Error));
    }

    private sealed class EventTypeTraceListener : TraceListener
    {
        internal List<TraceEventType> EventTypes { get; } = new();

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? message)
        {
            EventTypes.Add(eventType);
            base.TraceEvent(eventCache, source, eventType, id, message);
        }

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? format,
            params object?[]? args)
        {
            EventTypes.Add(eventType);
            base.TraceEvent(eventCache, source, eventType, id, format, args);
        }
    }
}
