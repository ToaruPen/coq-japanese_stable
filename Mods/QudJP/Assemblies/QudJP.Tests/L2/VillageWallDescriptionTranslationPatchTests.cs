using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VillageWallDescriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void VillageWallFactory_TranslatesDescriptionShort_WhenPatched()
    {
        WithPatchedFactory(() =>
        {
            DummyVillageWallFactory.NextWall = new DummyVillageWallObject
            {
                Short = "Planks of witchwood have been cut in a layered style and bound together with asphalt and rope.",
            };

            var result = DummyVillageWallFactory.getAVillageWall();

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.Short,
                    Is.EqualTo("ウィッチウッドの板材が層状様式に切り出され、アスファルトと縄で束ねられている。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void VillageWallFactory_LeavesUnknownDescriptionShort_WhenPatched()
    {
        WithPatchedFactory(() =>
        {
            DummyVillageWallFactory.NextWall = new DummyVillageWallObject
            {
                Short = "A wall with no generated history.",
            };

            var result = DummyVillageWallFactory.getAVillageWall();

            Assert.Multiple(() =>
            {
                Assert.That(result.Short, Is.EqualTo("A wall with no generated history."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedFactory(Action action)
    {
        var harmonyId = "qudjp.tests.village-wall-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyVillageWallFactory), nameof(DummyVillageWallFactory.getAVillageWall)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(VillageWallDescriptionTranslationPatch),
                    nameof(VillageWallDescriptionTranslationPatch.Postfix),
                    typeof(object))));
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
            nameof(VillageWallDescriptionTranslationPatch),
            nameof(VillageWallDescriptionTranslationPatch) + ".DescriptionShort");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyVillageWallObject
{
    public string Short { get; set; } = string.Empty;
}

internal static class DummyVillageWallFactory
{
    public static DummyVillageWallObject NextWall { get; set; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DummyVillageWallObject getAVillageWall()
    {
        return NextWall;
    }
}
