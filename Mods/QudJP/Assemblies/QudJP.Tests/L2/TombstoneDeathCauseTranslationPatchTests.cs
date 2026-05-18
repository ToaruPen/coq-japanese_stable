using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TombstoneDeathCauseTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyTombstoneDeathCauseTarget.Reset();
    }

    [Test]
    public void GenerateTombstone_TranslatesGeneratedDeathCause_WhenPatched()
    {
        WithPatchedGenerateTombstone(() =>
        {
            var target = new DummyTombstoneDeathCauseTarget();
            target.GenerateTombstone();

            Assert.Multiple(() =>
            {
                Assert.That(target.Lines, Does.Contain("snapjawに刺殺された"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateTombstone_LeavesCustomInscription_WhenPatched()
    {
        WithPatchedGenerateTombstone(() =>
        {
            var target = new DummyTombstoneDeathCauseTarget
            {
                Inscription = "Died of old age",
            };

            target.GenerateTombstone();

            Assert.Multiple(() =>
            {
                Assert.That(target.Lines, Does.Contain("Died of old age"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void GenerateRachelTombstone_TranslatesFixedGlotrotCause_WhenPatched()
    {
        WithPatchedGenerateRachelTombstone(() =>
        {
            var target = new DummyTombstoneDeathCauseTarget();
            target.GenerateRachelTombstone();

            Assert.Multiple(() =>
            {
                Assert.That(target.Lines, Does.Contain("グロットロットに倒れた。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    private static void WithPatchedGenerateTombstone(Action action)
    {
        WithPatchedMethod(nameof(DummyTombstoneDeathCauseTarget.GenerateTombstone), action);
    }

    private static void WithPatchedGenerateRachelTombstone(Action action)
    {
        WithPatchedMethod(nameof(DummyTombstoneDeathCauseTarget.GenerateRachelTombstone), action);
    }

    private static void WithPatchedMethod(string methodName, Action action)
    {
        var harmonyId = "qudjp.tests.tombstone-death-cause." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTombstoneDeathCauseTarget), methodName),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(TombstoneDeathCauseTranslationPatch),
                    nameof(TombstoneDeathCauseTranslationPatch.Transpiler),
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
            nameof(TombstoneDeathCauseTranslationPatch),
            nameof(TombstoneDeathCauseTranslationPatch) + ".DeathCause");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyTombstoneDeathCauseTarget
{
    public string? Inscription { get; set; }

    public List<string> Lines { get; } = [];

    public static void Reset()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GenerateTombstone()
    {
        Lines.Add(DummyStringFormat.ClipText("Here lies", 80));
        Lines.Add(DummyStringFormat.ClipText(Inscription ?? "Stabbed to death by a snapjaw", 80));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GenerateRachelTombstone()
    {
        Lines.Add(DummyStringFormat.ClipText("Here lies", 80));
        Lines.Add(DummyStringFormat.ClipText("Succumbed to glotrot.", 80));
    }
}

internal static class DummyStringFormat
{
    public static string ClipText(string source, int maxWidth)
    {
        return source.Length <= maxWidth ? source : source.Substring(0, maxWidth);
    }
}
