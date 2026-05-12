using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing.NUnit;
using NUnit.Framework;
using QudJP.Analyzers;

namespace QudJP.Analyzers.Tests;

using VerifyCS = AnalyzerVerifier<HarmonyPatchTryCatchAnalyzer>;

[TestFixture]
public sealed class HarmonyPatchTryCatchAnalyzerTests
{
    private const string HarmonyStubs = """
namespace HarmonyLib
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class HarmonyPatch : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyTargetMethod : Attribute { }
}
""";

    [Test]
    public async Task NoDiagnostic_WhenPatchMethodBodyIsFullyWrappedByTryCatchAsync()
    {
        var source = """
using System;
using System.Diagnostics;
using HarmonyLib;
""" + HarmonyStubs + """
[HarmonyPatch]
public static class SamplePatch
{
    public static bool Prefix(ref string __result)
    {
        try
        {
            __result = "ok";
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SamplePatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_WhenPatchMethodBodyUsesBareCatchAsync()
    {
        var source = """
using System.Diagnostics;
using HarmonyLib;
""" + HarmonyStubs + """
[HarmonyPatch]
public static class SamplePatch
{
    public static bool Prefix(ref string __result)
    {
        try
        {
            __result = "ok";
            return false;
        }
        catch
        {
            Trace.TraceError("QudJP: SamplePatch.Prefix failed");
            return true;
        }
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }

    [Test]
    public async Task Diagnostic_WhenPatchMethodMissingTopLevelTryCatchAsync()
    {
        var source = """
using HarmonyLib;
""" + HarmonyStubs + """
[HarmonyPatch]
public static class SamplePatch
{
    public static bool {|#0:Prefix|}(ref string __result)
    {
        __result = "ok";
        return false;
    }
}
""";

        var expected = VerifyCS.Diagnostic(HarmonyPatchTryCatchAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Prefix", "SamplePatch");

        await VerifyCS.VerifyAnalyzerAsync(source, expected).ConfigureAwait(false);
    }

    [Test]
    public async Task NoDiagnostic_ForTargetMethodEvenWithoutTryCatchAsync()
    {
        var source = """
using System.Reflection;
using HarmonyLib;
""" + HarmonyStubs + """
[HarmonyPatch]
public static class SamplePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return null;
    }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source).ConfigureAwait(false);
    }
}
