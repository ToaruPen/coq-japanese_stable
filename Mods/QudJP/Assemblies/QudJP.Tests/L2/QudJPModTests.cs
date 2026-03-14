using System.Reflection;
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

        try
        {
            var output = TestTraceHelper.CaptureTrace(() => Assert.That(() => QudJPMod.InvokePatchAll(harmony), Throws.Nothing));

            Assert.That(output, Does.Contain("[QudJP]"),
                "InvokePatchAll should log when patches fail to apply, " +
                "proving PatchAll(Assembly) scanned the correct assembly");
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GetHarmonyPatchTypes_ReturnsHarmonyPatchClassesOnly()
    {
        var patchTypes = QudJPMod.GetHarmonyPatchTypes(typeof(QudJPModTests).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(patchTypes, Does.Contain(typeof(PatchAllTestPatch)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(QudJPModTests)));
            Assert.That(patchTypes, Does.Not.Contain(typeof(PatchAllDummyTarget)));
        });
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

            var output = TestTraceHelper.CaptureTrace(() => QudJPMod.LogPatchResults(harmony));
            Assert.That(output, Does.Contain("method(s) patched"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TryPreparePatchType_ReturnsFalse_WhenTargetMethodReturnsNull()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(NullTargetPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.False);
            Assert.That(reason, Does.Contain("returned null"));
        });
    }

    [Test]
    public void TryPreparePatchType_ReturnsFalse_WhenTargetMethodsReturnsEmpty()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(EmptyTargetsPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.False);
            Assert.That(reason, Does.Contain("returned no target methods"));
        });
    }

    [Test]
    public void TryPreparePatchType_ReturnsTrue_ForSimpleHarmonyPatchWithoutCustomTargetResolver()
    {
        var prepared = QudJPMod.TryPreparePatchType(typeof(PatchAllTestPatch), out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.True);
            Assert.That(reason, Is.Empty);
        });
    }

    [Test]
    public void LogPatchResults_HandlesGetPatchedMethodsFailure_WithoutThrowing()
    {
        var output = TestTraceHelper.CaptureTrace(() => Assert.That(() => QudJPMod.LogPatchResults(ThrowingPatchedMethodsProbe.Create()), Throws.Nothing));
        Assert.That(output, Does.Contain("Failed to enumerate patched methods"));
    }

    [Test]
    public void LogToUnity_WritesToTrace_InTestEnvironment()
    {
        var output = TestTraceHelper.CaptureTrace(() => QudJPMod.LogToUnity("[QudJP] test message"));
        Assert.That(output, Does.Contain("[QudJP] test message"));
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

    [HarmonyPatch]
    internal static class NullTargetPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase? TargetMethod()
        {
            return null;
        }
    }

    [HarmonyPatch]
    internal static class EmptyTargetsPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield break;
        }
    }

    internal sealed class ThrowingPatchedMethodsProbe
    {
        private readonly int sentinel = 1;

        private ThrowingPatchedMethodsProbe()
        {
        }

        public static ThrowingPatchedMethodsProbe Create()
        {
            return new ThrowingPatchedMethodsProbe();
        }

        public System.Collections.IEnumerable GetPatchedMethods()
        {
            _ = sentinel;
            throw new InvalidOperationException("simulated patched-method enumeration failure");
        }
    }
}
