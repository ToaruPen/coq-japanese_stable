using System.IO;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class BookLineGeometryProbePolicyTests
{
    [Test]
    public void BookLineTranslationPatch_EmitsDevOnlyGeometryProbeAfterSettingText()
    {
        var source = ReadSource("Patches", "BookLineTranslationPatch.cs");

        Assert.That(source, Does.Contain("BookLineGeometryObservability.TryBuildSnapshot("));
        Assert.That(source, Does.Contain("RuntimeDiagnostics.LogVerboseProbe(() => logLine!)"));
        Assert.That(source, Does.Contain("DelayedBookLineGeometryProbeScheduler.ScheduleSnapshot("));
        Assert.That(source, Does.Not.Contain("BookLineTextLayoutAdjustment.Apply("));
        Assert.That(source, Does.Not.Contain("DelayedBookLineTextLayoutAdjustmentScheduler.ScheduleAdjustment("));
    }

    [Test]
    public void BookLineGeometryObservability_ReportsRuntimeLayoutAndMaskFields()
    {
        var source = ReadSource("Observability", "BookLineGeometryObservability.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("BookLineGeometryProbe/v1"));
            Assert.That(source, Does.Contain("rect="));
            Assert.That(source, Does.Contain("preferred="));
            Assert.That(source, Does.Contain("overflow="));
            Assert.That(source, Does.Contain("wrap="));
            Assert.That(source, Does.Contain("maxVisibleLines="));
            Assert.That(source, Does.Contain("textTruncated="));
            Assert.That(source, Does.Contain("maskChain="));
            Assert.That(source, Does.Contain("hierarchy="));
            Assert.That(source, Does.Contain("legacyText["));
        });
    }

    private static string ReadSource(params string[] parts)
    {
        var pathParts = new string[parts.Length + 5];
        pathParts[0] = TestProjectPaths.GetRepositoryRoot();
        pathParts[1] = "Mods";
        pathParts[2] = "QudJP";
        pathParts[3] = "Assemblies";
        pathParts[4] = "src";
        Array.Copy(parts, 0, pathParts, 5, parts.Length);
        return File.ReadAllText(Path.Combine(pathParts));
    }
}
