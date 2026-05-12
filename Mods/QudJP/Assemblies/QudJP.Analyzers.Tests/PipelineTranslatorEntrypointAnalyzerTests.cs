using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing.NUnit;
using NUnit.Framework;
using QudJP.Analyzers;

namespace QudJP.Analyzers.Tests;

using VerifyCS = AnalyzerVerifier<PipelineTranslatorEntrypointAnalyzer>;

[TestFixture]
public sealed class PipelineTranslatorEntrypointAnalyzerTests
{
    [Test]
    public async Task Diagnostic_WhenMessagePipelineCallsTranslatorDirectlyAsync()
    {
        const string source = """
namespace QudJP.Patches;

internal static class MessageQueueSemanticPipeline
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        return {|#0:SamplePatch.TryTranslateQueuedMessage(ref message, color)|};
    }
}

internal static class SamplePatch
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color) => false;
}
""";

        var expected = VerifyCS.Diagnostic(PipelineTranslatorEntrypointAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments(
                "TryTranslateQueuedMessage",
                "QudJP.Patches.SamplePatch.TryTranslateQueuedMessage(ref string, string?)");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_WhenPipelineCallsTranslatorInsideExceptionCatchTryAsync()
    {
        const string source = """
using System;

namespace QudJP.Patches;

internal static class MessageQueueSemanticPipeline
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        try
        {
            return SamplePatch.TryTranslateQueuedMessage(ref message, color);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal static class SamplePatch
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color) => false;
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTranslatorCallIsInsideCatchBlockAsync()
    {
        const string source = """
using System;

namespace QudJP.Patches;

internal static class MessageQueueSemanticPipeline
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        try
        {
            return false;
        }
        catch (Exception)
        {
            return {|#0:SamplePatch.TryTranslateQueuedMessage(ref message, color)|};
        }
    }
}

internal static class SamplePatch
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color) => false;
}
""";

        var expected = VerifyCS.Diagnostic(PipelineTranslatorEntrypointAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments(
                "TryTranslateQueuedMessage",
                "QudJP.Patches.SamplePatch.TryTranslateQueuedMessage(ref string, string?)");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenTranslatorCallIsInsideFinallyBlockAsync()
    {
        const string source = """
using System;

namespace QudJP.Patches;

internal static class MessageQueueSemanticPipeline
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        try
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _ = {|#0:SamplePatch.TryTranslateQueuedMessage(ref message, color)|};
        }
    }
}

internal static class SamplePatch
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color) => false;
}
""";

        var expected = VerifyCS.Diagnostic(PipelineTranslatorEntrypointAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments(
                "TryTranslateQueuedMessage",
                "QudJP.Patches.SamplePatch.TryTranslateQueuedMessage(ref string, string?)");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_WhenPopupPipelineUsesLocalCrashSafeHelperAsync()
    {
        const string source = """
using System;

namespace QudJP.Patches;

internal static class PopupShowSemanticPipeline
{
    internal static string TranslateMessage(string source, string route)
    {
        if (TryTranslatePopupMessageWithFallback(SamplePatch.TryTranslatePopupMessage, source, route, out var translated))
        {
            return translated;
        }

        return source;
    }

    private static bool TryTranslatePopupMessageWithFallback(
        PopupMessageTranslator translator,
        string source,
        string route,
        out string translated)
    {
        try
        {
            return translator(source, route, "Popup.Show", out translated);
        }
        catch (Exception)
        {
            translated = source;
            return false;
        }
    }

    private delegate bool PopupMessageTranslator(string source, string route, string family, out string translated);
}

internal static class SamplePatch
{
    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        translated = source;
        return false;
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_OutsideSemanticPipelineAsync()
    {
        const string source = """
namespace QudJP.Patches;

internal static class OtherTranslator
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        return SamplePatch.TryTranslateQueuedMessage(ref message, color);
    }
}

internal static class SamplePatch
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color) => false;
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }
}
