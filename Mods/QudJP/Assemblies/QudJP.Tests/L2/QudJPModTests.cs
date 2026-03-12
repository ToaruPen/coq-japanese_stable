using System;
using System.Linq;
using HarmonyLib;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudJPModTests
{
    [Test]
    public void InvokePatchAll_AppliesHarmonyPatches_WhenAssemblyContainsPatchClasses()
    {
        var harmonyId = $"qudjp.tests.patchall.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            // Act: InvokePatchAll resolves and calls PatchAll with the correct assembly.
            // If PatchAll() no-args is used via reflection, Assembly.GetCallingAssembly()
            // may return the wrong assembly (the bug). PatchAll(Assembly) with
            // GetExecutingAssembly() is the correct path.
            QudJPMod.InvokePatchAll(harmony);

            // Assert: PatchAllTestPatch targeting PatchAllDummyTarget.Echo should have
            // been discovered and applied when the test assembly is scanned correctly.
            var patchedMethods = harmony.GetPatchedMethods().ToList();
            Assert.That(patchedMethods, Is.Not.Empty,
                "InvokePatchAll should discover [HarmonyPatch] classes in the current assembly");
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
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
    private static class PatchAllTestPatch
    {
        public static void Postfix(ref string __result)
        {
            __result = $"[patched] {__result}";
        }
    }
}
