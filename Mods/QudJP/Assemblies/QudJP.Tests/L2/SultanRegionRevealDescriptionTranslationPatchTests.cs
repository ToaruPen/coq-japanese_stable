using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SultanRegionRevealDescriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummySultanRegionTarget.Reset();
    }

    [Test]
    public void SultanReveal_TranslatesDescriptionShort_WhenPatched()
    {
        WithPatchedFireEvent(() =>
        {
            var target = new DummySultanRegionTarget();

            target.FireEvent(new SultanDummyEvent("SultanReveal"));

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.ParentObject.Description.Short,
                    Is.EqualTo("失われた州ではイーターたちが奇妙な植物群を愛でていた。その遺跡は平原の上に横たわっている。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OtherEvent_DoesNotTranslate_WhenPatched()
    {
        WithPatchedFireEvent(() =>
        {
            var target = new DummySultanRegionTarget();

            target.FireEvent(new SultanDummyEvent("OtherEvent"));

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.ParentObject.Description.Short,
                    Is.EqualTo("The Eaters admired their strange flora in the lost province whose ruins lie over the flats."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedFireEvent(Action action)
    {
        var harmonyId = "qudjp.tests.sultan-region-reveal-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummySultanRegionTarget),
                    nameof(DummySultanRegionTarget.FireEvent),
                    typeof(SultanDummyEvent)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SultanRegionRevealDescriptionTranslationPatch),
                    nameof(SultanRegionRevealDescriptionTranslationPatch.Postfix),
                    typeof(object),
                    typeof(object),
                    typeof(bool))));
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
            nameof(SultanRegionRevealDescriptionTranslationPatch),
            nameof(SultanRegionRevealDescriptionTranslationPatch) + ".DescriptionShort");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummySultanRegionTarget
{
    public DummySultanRegionGameObject ParentObject { get; } = new();

    public static void Reset()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool FireEvent(SultanDummyEvent E)
    {
        if (E.ID == "SultanReveal")
        {
            ParentObject.Description.Short =
                "The Eaters admired their strange flora in the lost province whose ruins lie over the flats.";
        }

        return true;
    }
}

internal sealed class DummySultanRegionGameObject
{
    public DummySultanRegionDescription Description { get; } = new();
}

internal sealed class DummySultanRegionDescription
{
    public string Short { get; set; } =
        "The Eaters admired their strange flora in the lost province whose ruins lie over the flats.";
}

internal sealed class SultanDummyEvent(string id)
{
    public string ID { get; } = id;
}
