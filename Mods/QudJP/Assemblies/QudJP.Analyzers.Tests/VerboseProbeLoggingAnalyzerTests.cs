using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing.NUnit;
using NUnit.Framework;
using QudJP.Analyzers;

namespace QudJP.Analyzers.Tests;

using VerifyCS = AnalyzerVerifier<VerboseProbeLoggingAnalyzer>;

[TestFixture]
public sealed class VerboseProbeLoggingAnalyzerTests
{
    [Test]
    public async Task NoDiagnostic_WhenVerboseProbeUsesRuntimeDiagnosticsAsync()
    {
        const string source = """
using System;

namespace QudJP;

internal static class RuntimeDiagnostics
{
    public static void LogVerboseProbe(Func<string> messageFactory) { }
}

public static class Sample
{
    public static void Log()
    {
        RuntimeDiagnostics.LogVerboseProbe(() => "[QudJP] DynamicTextProbe/v1: route='Sample'");
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenProbeMarkerIsLoggedDirectlyToQudJPModAsync()
    {
        const string source = """
namespace QudJP;

internal static class QudJPMod
{
    public static void LogToUnity(string message) { }
}

public static class Sample
{
    public static void Log()
    {
        QudJPMod.LogToUnity({|#0:"[QudJP] FinalOutputProbe/v1: sink='Sample'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(VerboseProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("FinalOutputProbe/v1");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenProbeMarkerIsLoggedDirectlyToTraceInformationAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static void Log()
    {
        Trace.TraceInformation({|#0:"[QudJP] SinkObserve/v1: sink='Sample'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(VerboseProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("SinkObserve/v1");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenInterpolatedProbeMarkerIsLoggedDirectlyAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static void Log(string route)
    {
        Trace.TraceWarning($"[QudJP] DynamicTextProbe/v1: route='{route}'");
    }
}
""";

        var expected = VerifyCS.Diagnostic(VerboseProbeLoggingAnalyzer.DiagnosticId)
            .WithSpan(7, 30, 7, 66)
            .WithArguments("DynamicTextProbe/v1");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenProbeMarkerIsLoggedDirectlyToTraceErrorAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static void Log()
    {
        Trace.TraceError({|#0:"[QudJP] SinkObserve/v1: sink='Sample'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(VerboseProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("SinkObserve/v1");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenProbeMarkerIsLoggedThroughNonVerboseRuntimeDiagnosticsAsync()
    {
        const string source = """
namespace QudJP;

internal static class RuntimeDiagnostics
{
    public static void LogStatus(string message) { }
}

public static class Sample
{
    public static void Log()
    {
        RuntimeDiagnostics.LogStatus({|#0:"[QudJP] DynamicTextProbe/v1: route='Sample'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(VerboseProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("DynamicTextProbe/v1");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenConstProbeMarkerIsLoggedDirectlyAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    private const string Marker = "[QudJP] FinalOutputProbe/v1: sink='Sample'";

    public static void Log()
    {
        Trace.TraceInformation({|#0:Marker|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(VerboseProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("FinalOutputProbe/v1");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }
}
