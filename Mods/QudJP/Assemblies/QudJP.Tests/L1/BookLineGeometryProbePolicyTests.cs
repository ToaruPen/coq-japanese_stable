using System;
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

        AssertContainsInOrder(
            source,
            "#if QUDJP_DEV_BUILD",
            "BookLineGeometryObservability.TryBuildSnapshot(",
            "RuntimeDiagnostics.LogVerboseProbe(() => logLine!)",
            "DelayedBookLineGeometryProbeScheduler.ScheduleSnapshot(",
            "#endif");
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

    [Test]
    public void BookLineGeometryObservability_IsDevOnlyAndReleaseBuildIsNoOp()
    {
        var source = ReadSource("Observability", "BookLineGeometryObservability.cs");

        AssertContainsInOrder(
            source,
            "#if HAS_TMP && QUDJP_DEV_BUILD",
            "internal static bool TryBuildSnapshot(",
            "try",
            "BookLineGeometryProbe/v1",
            "catch (Exception ex)",
            "Trace.TraceError(",
            "logLine = null;",
            "return false;",
            "#else",
            "internal static bool TryBuildSnapshot(",
            "logLine = null;",
            "return false;",
            "#endif");
    }

    [Test]
    public void DelayedBookLineGeometryProbeScheduler_IsDevOnlyAndReleaseBuildIsNoOp()
    {
        var source = ReadSource("Observability", "DelayedBookLineGeometryProbeScheduler.cs");

        AssertContainsInOrder(
            source,
            "#if HAS_TMP && QUDJP_DEV_BUILD",
            "internal static void ScheduleSnapshot(",
            "runner == null",
            "host != null",
            "component == null",
            "#else",
            "internal static void ScheduleSnapshot(",
            "_ = lineInstance;",
            "_ = source;",
            "_ = rendered;",
            "#endif");
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

    private static void AssertContainsInOrder(string source, params string[] snippets)
    {
        var searchStart = 0;
        foreach (var snippet in snippets)
        {
            var index = source.IndexOf(snippet, searchStart, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Missing snippet after index {searchStart}: {snippet}");
            searchStart = index + snippet.Length;
        }
    }
}
