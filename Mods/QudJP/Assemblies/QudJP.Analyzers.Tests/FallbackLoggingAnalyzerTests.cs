using System.Threading.Tasks;
using NUnit.Framework;
using QudJP.Analyzers;

namespace QudJP.Analyzers.Tests;

using VerifyCS = AnalyzerVerifier<FallbackLoggingAnalyzer>;

[TestFixture]
public sealed class FallbackLoggingAnalyzerTests
{
    [Test]
    public async Task NoDiagnostic_WhenMethodCallFallbackHasPrecedingTraceWarningAsync()
    {
        const string source = """
using System.Diagnostics;

public static class Sample
{
    public static string Resolve()
    {
        Trace.TraceWarning("QudJP: Resolve fallback is used.");
        return GetValue() ?? "fallback";
    }

    private static string? GetValue() => null;
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenMethodCallFallbackHasNoPrecedingLogAsync()
    {
        const string source = """
public static class Sample
{
    public static string Resolve()
    {
        return {|#0:GetValue() ?? "fallback"|};
    }

    private static string? GetValue() => null;
}
""";

        var expected = VerifyCS.Diagnostic(FallbackLoggingAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("GetValue()");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_ForSimpleParameterFallbackAsync()
    {
        const string source = """
public static class Sample
{
    public static string Resolve(string? value)
    {
        return value ?? "fallback";
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }
}
