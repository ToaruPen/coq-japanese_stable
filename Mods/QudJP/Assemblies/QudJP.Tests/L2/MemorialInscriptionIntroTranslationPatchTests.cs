using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MemorialInscriptionIntroTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyMemorialIntroTarget.Intro = "Here Lies";
    }

    [Test]
    public void GenerateMemorial_TranslatesExpandedIntro_WhenPatched()
    {
        WithPatchedGenerateMemorial(() =>
        {
            DummyMemorialIntroTarget.Intro = "Dream in the Light of Gjaus";

            var result = DummyMemorialIntroTarget.GenerateMemorial();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ジャウスの光の中に夢見よ"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateMemorial_LeavesUnknownIntro_WhenPatched()
    {
        WithPatchedGenerateMemorial(() =>
        {
            DummyMemorialIntroTarget.Intro = "Unknown epitaph";

            var result = DummyMemorialIntroTarget.GenerateMemorial();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Unknown epitaph"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGenerateMemorial(Action action)
    {
        var harmonyId = "qudjp.tests.memorial-intro." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMemorialIntroTarget), nameof(DummyMemorialIntroTarget.GenerateMemorial)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(MemorialInscriptionIntroTranslationPatch),
                    nameof(MemorialInscriptionIntroTranslationPatch.Transpiler),
                    typeof(IEnumerable<CodeInstruction>))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(MemorialInscriptionIntroTranslationPatch),
            nameof(MemorialInscriptionIntroTranslationPatch) + ".Intro");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyMemorialIntroTarget
{
    public static string Intro { get; set; } = "Here Lies";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GenerateMemorial()
    {
        return DummyHistoricStringExpander.ExpandString(Intro);
    }
}
