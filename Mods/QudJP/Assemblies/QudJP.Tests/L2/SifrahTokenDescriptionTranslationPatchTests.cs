using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SifrahTokenDescriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildLiquid),
        "use water",
        "waterを使う",
        "UseNamedLiquid")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildAttributeSacrifice),
        "sacrifice a point of Agility",
        "Agilityを1ポイント捧げる",
        "SacrificeNamedAttribute")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.SetBeing),
        "invoke Shekhinah, in the manner of {{W|Mechanimists}}",
        "{{W|Mechanimists}}流にShekhinahを呼び出す",
        "InvokeBeingManner")]
    [TestCase(
        nameof(DummySifrahTokenDescriptionTarget.BuildFixedNoArgumentToken),
        "display a Barathrumite token",
        "バラサルム派のしるしを見せる",
        "Exact.DisplayBarathrumiteToken")]
    public void OwnerPatch_TranslatesDescriptionAssignment_WhenPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(ownerMethodName, () =>
        {
            var target = new DummySifrahTokenDescriptionTarget(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerPatch_LeavesUnsupportedDescriptionUnchanged_WhenPatched()
    {
        WithPatchedOwner(nameof(DummySifrahTokenDescriptionTarget.BuildLiquid), () =>
        {
            var target = new DummySifrahTokenDescriptionTarget("use a strange resource");

            target.BuildLiquid();

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo("use a strange resource"));
                Assert.That(HitCount("UseNamedLiquid"), Is.Zero);
            });
        });
    }

    [Test]
    public void OwnerPatch_StripsDirectMarkedDescriptionWithoutRetranslating_WhenPatched()
    {
        WithPatchedOwner(nameof(DummySifrahTokenDescriptionTarget.BuildLiquid), () =>
        {
            var target = new DummySifrahTokenDescriptionTarget("\u0001use liquid");

            target.BuildLiquid();

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo("use liquid"));
                Assert.That(HitCount("UseNamedLiquid"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(string ownerMethodName, Action action)
    {
        var harmonyId = "qudjp.tests.sifrah-token-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySifrahTokenDescriptionTarget), ownerMethodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SifrahTokenDescriptionTranslationPatch),
                    nameof(SifrahTokenDescriptionTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void InvokeOwner(DummySifrahTokenDescriptionTarget target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummySifrahTokenDescriptionTarget), ownerMethodName).Invoke(target, null);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            SifrahTokenDescriptionTranslationPatch.Context,
            SifrahTokenDescriptionTranslationPatch.Family + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummySifrahTokenDescriptionTarget
{
    private readonly string sourceDescription;

    public DummySifrahTokenDescriptionTarget(string sourceDescription)
    {
        this.sourceDescription = sourceDescription;
    }

    public string Description { get; private set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BuildLiquid()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BuildAttributeSacrifice()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetBeing()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BuildFixedNoArgumentToken()
    {
        Description = sourceDescription;
    }
}
