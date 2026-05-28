using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HagglingSifrahResultDescriptionTranslationPatchTests
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
        nameof(DummyHagglingSifrahResultTarget.ResultCriticalFailure),
        "Your haggling was an abysmal failure.",
        "交渉は壊滅的な失敗だった。",
        "CriticalFailure")]
    [TestCase(
        nameof(DummyHagglingSifrahResultTarget.ResultFailure),
        "Your haggling went poorly.",
        "交渉はうまくいかなかった。",
        "Failure")]
    [TestCase(
        nameof(DummyHagglingSifrahResultTarget.ResultPartialSuccess),
        "Your haggling was mediocre.",
        "交渉はそこそこの結果だった。",
        "PartialSuccess")]
    [TestCase(
        nameof(DummyHagglingSifrahResultTarget.ResultSuccess),
        "Your haggling went well.",
        "交渉はうまくいった。",
        "Success")]
    [TestCase(
        nameof(DummyHagglingSifrahResultTarget.ResultExceptionalSuccess),
        "Your haggling was spectacular.",
        "交渉は見事な成功だった。",
        "ExceptionalSuccess")]
    public void ResultMethod_TranslatesOutcomeDescription_WhenPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(ownerMethodName, () =>
        {
            var target = new DummyHagglingSifrahResultTarget(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void ResultMethod_LeavesUnsupportedDescriptionUnchanged_WhenPatched()
    {
        WithPatchedOwner(nameof(DummyHagglingSifrahResultTarget.ResultSuccess), () =>
        {
            var target = new DummyHagglingSifrahResultTarget("A different haggling outcome.");

            target.ResultSuccess();

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo("A different haggling outcome."));
                Assert.That(HitCount("Success"), Is.Zero);
            });
        });
    }

    [Test]
    public void ResultMethod_StripsDirectMarkedDescriptionWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedOwner(nameof(DummyHagglingSifrahResultTarget.ResultSuccess), () =>
        {
            var target = new DummyHagglingSifrahResultTarget(
                MessageFrameTranslator.MarkDirectTranslation("A different haggling outcome."));

            target.ResultSuccess();

            Assert.Multiple(() =>
            {
                Assert.That(target.Description, Is.EqualTo("A different haggling outcome."));
                Assert.That(HitCount("Success"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(string ownerMethodName, Action action)
    {
        var harmonyId = "qudjp.tests.haggling-sifrah-result-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHagglingSifrahResultTarget), ownerMethodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(HagglingSifrahResultDescriptionTranslationPatch),
                    nameof(HagglingSifrahResultDescriptionTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void InvokeOwner(DummyHagglingSifrahResultTarget target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummyHagglingSifrahResultTarget), ownerMethodName).Invoke(target, null);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            HagglingSifrahResultDescriptionTranslationPatch.Context,
            HagglingSifrahResultDescriptionTranslationPatch.Family + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyHagglingSifrahResultTarget
{
    private readonly string sourceDescription;

    public DummyHagglingSifrahResultTarget(string sourceDescription)
    {
        this.sourceDescription = sourceDescription;
    }

    public string Description { get; private set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultCriticalFailure()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultFailure()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultPartialSuccess()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultSuccess()
    {
        Description = sourceDescription;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultExceptionalSuccess()
    {
        Description = sourceDescription;
    }
}
