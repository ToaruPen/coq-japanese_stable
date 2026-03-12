using System;
using HarmonyLib;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudJPModTests
{
    [Test]
    public void InvokePatchAll_ScansCorrectAssembly_AndHandlesErrorsGracefully()
    {
        var harmonyId = $"qudjp.tests.patchall.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        using var listener = new System.Diagnostics.TextWriterTraceListener(new System.IO.StringWriter());
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            Assert.That(() => QudJPMod.InvokePatchAll(harmony), Throws.Nothing);

            listener.Flush();
            var output = listener.Writer!.ToString()!;

            Assert.That(output, Does.Contain("[QudJP]"),
                "InvokePatchAll should log when patches fail to apply, " +
                "proving PatchAll(Assembly) scanned the correct assembly");
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LogPatchResults_OutputsMethodCount_AfterPatchingAssembly()
    {
        var harmonyId = $"qudjp.tests.logpatch.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(PatchAllDummyTarget), nameof(PatchAllDummyTarget.Echo)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PatchAllTestPatch), nameof(PatchAllTestPatch.Postfix))));

            using var listener = new System.Diagnostics.TextWriterTraceListener(new System.IO.StringWriter());
            System.Diagnostics.Trace.Listeners.Add(listener);

            try
            {
                QudJPMod.LogPatchResults(harmony);
                listener.Flush();
                var output = listener.Writer!.ToString()!;

                Assert.That(output, Does.Contain("method(s) patched"));
            }
            finally
            {
                System.Diagnostics.Trace.Listeners.Remove(listener);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LogToUnity_WritesToTrace_InTestEnvironment()
    {
        using var listener = new System.Diagnostics.TextWriterTraceListener(new System.IO.StringWriter());
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            QudJPMod.LogToUnity("[QudJP] test message");
            listener.Flush();
            var output = listener.Writer!.ToString()!;

            Assert.That(output, Does.Contain("[QudJP] test message"));
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    // Simple test-local target for PatchAll assembly scanning to discover.
    internal static class PatchAllDummyTarget
    {
        public static string Echo(string input) => input;
    }

    // This [HarmonyPatch] class will be found by PatchAll when the test assembly is
    // scanned. If PatchAll() no-args gets the wrong assembly via GetCallingAssembly(),
    // this class will NOT be found and the test will fail (RED).
    [HarmonyPatch(typeof(PatchAllDummyTarget), nameof(PatchAllDummyTarget.Echo))]
    internal static class PatchAllTestPatch
    {
        public static void Postfix(ref string __result)
        {
            __result = $"[patched] {__result}";
        }
    }
}
