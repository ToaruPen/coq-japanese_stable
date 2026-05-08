namespace QudJP.Tests.L1;

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
}
