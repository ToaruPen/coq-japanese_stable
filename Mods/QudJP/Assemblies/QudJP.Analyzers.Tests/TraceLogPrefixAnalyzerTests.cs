using System.Threading.Tasks;
using NUnit.Framework;
using QudJP.Analyzers;

namespace QudJP.Analyzers.Tests;

using VerifyCS = AnalyzerVerifier<TraceLogPrefixAnalyzer>;

[TestFixture]
public sealed class TraceLogPrefixAnalyzerTests
{
    [Test]
    public async Task NoDiagnostic_WhenTraceWarningStartsWithQudJPPrefixAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static void Log(string message)
    {
        Trace.TraceWarning($"QudJP: {message}");
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTraceMessageDoesNotIncludePrefixAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static void Log()
    {
        Trace.TraceWarning({|#0:"Missing prefix"|});
    }
}
""";

        var expected = VerifyCS.Diagnostic(TraceLogPrefixAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("TraceWarning");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_ForTraceInformationWithoutPrefixAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static void Log()
    {
        Trace.TraceInformation("No prefix required for informational logs.");
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }
}
