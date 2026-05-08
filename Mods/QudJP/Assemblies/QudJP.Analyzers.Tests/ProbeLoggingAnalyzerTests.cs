using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.NUnit;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using NUnit.Framework;
using QudJP.Analyzers;

namespace QudJP.Analyzers.Tests;

using VerifyCS = AnalyzerVerifier<ProbeLoggingAnalyzer>;

[TestFixture]
public sealed class ProbeLoggingAnalyzerTests
{
    private const string QudJPStubs = """
namespace QudJP
{
    public static class QudJPMod
    {
        public static void LogToUnity(string message) { }
    }

    public static class RuntimeDiagnostics
    {
        public static void LogVerboseProbe(System.Func<string> buildMessage) { }

        public static void LogImportant(string message) { }

        public static void LogStatus(string message) { }

        public static void LogWarning(string message) { }

        public static void LogError(string message) { }
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(string message) { }
    }
}
""";

    [Test]
    public async Task Diagnostic_WhenDirectQudJPModLogToUnityEmitsProbeMarkerAsync()
    {
        var source = QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        QudJP.QudJPMod.LogToUnity({|#0:"[QudJP] NewProbe/v1: leaked"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("QudJPMod.LogToUnity");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenUnityDebugLogEmitsSinkObserveMarkerAsync()
    {
        var source = QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        UnityEngine.Debug.Log({|#0:"[QudJP] SinkObserve/v1: leaked"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Debug.Log");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTraceInformationEmitsTranslatorVerboseMarkerAsync()
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        Trace.TraceInformation({|#0:"[QudJP] Translator: missing key 'abc'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Trace.TraceInformation");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTraceInformationEmitsNoPatternForMarkerAsync()
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        Trace.TraceInformation({|#0:$"[QudJP] MessagePatternTranslator: no pattern for '{nameof(Sample)}'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Trace.TraceInformation");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTraceInformationEmitsNoPatternForMarkerWithoutQudJPPrefixAsync()
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        Trace.TraceInformation({|#0:"MessagePatternTranslator: no pattern for 'abc'"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Trace.TraceInformation");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTraceInformationEmitsProbeMarkerFromLocalAsync()
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        var logLine = "[QudJP] SinkObserve/v1: leaked";
        Trace.TraceInformation({|#0:logLine|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Trace.TraceInformation");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenRuntimeDiagnosticsStatusLogEmitsProbeMarkerAsync()
    {
        var source = QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        QudJP.RuntimeDiagnostics.LogStatus({|#0:"[QudJP] NewProbe/v1: leaked"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("RuntimeDiagnostics.LogStatus");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTraceInformationEmitsProbeMarkerFromFormatArgumentAsync()
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        Trace.TraceInformation("{0}", {|#0:"[QudJP] NewProbe/v1: leaked"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Trace.TraceInformation");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [TestCase("TraceWarning")]
    [TestCase("TraceError")]
    [TestCase("WriteLine")]
    public async Task Diagnostic_WhenTraceTextEmitterEmitsProbeMarkerAsync(string methodName)
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + $$"""
public static class Sample
{
    public static void Log()
    {
        Trace.{{methodName}}({|#0:"[QudJP] NewProbe/v1: leaked"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(ProbeLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Trace." + methodName);

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_ForApprovedRuntimeDiagnosticsApisAndTraceErrorAsync()
    {
        var source = "using System.Diagnostics;\n" + QudJPStubs + """
public static class Sample
{
    public static void Log()
    {
        QudJP.RuntimeDiagnostics.LogVerboseProbe(() => "[QudJP] NewProbe/v1: gated");
        QudJP.RuntimeDiagnostics.LogImportant("[QudJP] Build marker: marker");
        Trace.TraceError("QudJP: failure");
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_InsideRuntimeDiagnosticsSourceFileAsync()
    {
        const string source = """
namespace QudJP
{
    public static class QudJPMod
    {
        public static void LogToUnity(string message) { }
    }

    internal static class RuntimeDiagnostics
    {
        public static void LogVerboseProbe()
        {
            QudJPMod.LogToUnity("[QudJP] NewProbe/v1: allowed here");
        }
    }
}
""";

#pragma warning disable CS0618 // Existing analyzer test package exposes NUnitVerifier as obsolete.
        var test = new CSharpAnalyzerTest<ProbeLoggingAnalyzer, NUnitVerifier>();
#pragma warning restore CS0618
        test.TestState.Sources.Add(("RuntimeDiagnostics.cs", source));

        await test.RunAsync().ConfigureAwait(false);
    }
}
