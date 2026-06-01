using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class StatisticStatShiftDisplayNameTranslationPatchTests
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

    [TestCase("camouflage", "迷彩")]
    [TestCase("co-processor", "コプロセッサ")]
    [TestCase("yurtmat's camouflage", "yurtmatの迷彩")]
    [TestCase("implant's co-processor", "implantのコプロセッサ")]
    public void Prefix_TranslatesKnownStatShiftDisplayNames_WhenPatched(string source, string expected)
    {
        var harmonyId = "qudjp.tests.statistic-stat-shift-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyStatisticTarget), nameof(DummyStatisticTarget.AddShift)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(StatisticStatShiftDisplayNameTranslationPatch),
                    nameof(StatisticStatShiftDisplayNameTranslationPatch.Prefix),
                    typeof(string).MakeByRefType())));

            var result = DummyStatisticTarget.AddShift(1, source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        StatisticStatShiftDisplayNameTranslationPatch.Context,
                        StatisticStatShiftDisplayNameTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesUnknownDisplayNameUnchanged()
    {
        var source = InvokePatchedAddShift("unknown source");

        Assert.Multiple(() =>
        {
            Assert.That(source, Is.EqualTo("unknown source"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    StatisticStatShiftDisplayNameTranslationPatch.Context,
                    StatisticStatShiftDisplayNameTranslationPatch.Family),
                Is.Zero);
        });
    }

    [Test]
    public void Prefix_DoesNotTranslateMalformedPossessiveDisplayName()
    {
        var source = InvokePatchedAddShift("owner' camouflage");

        Assert.Multiple(() =>
        {
            Assert.That(source, Is.EqualTo("owner' camouflage"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    StatisticStatShiftDisplayNameTranslationPatch.Context,
                    StatisticStatShiftDisplayNameTranslationPatch.Family),
                Is.Zero);
        });
    }

    private static string InvokePatchedAddShift(string source)
    {
        var harmonyId = "qudjp.tests.statistic-stat-shift-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyStatisticTarget), nameof(DummyStatisticTarget.AddShift)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(StatisticStatShiftDisplayNameTranslationPatch),
                    nameof(StatisticStatShiftDisplayNameTranslationPatch.Prefix),
                    typeof(string).MakeByRefType())));

            return DummyStatisticTarget.AddShift(1, source);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static class DummyStatisticTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string AddShift(int amount, string DisplayName, bool baseValue = false)
        {
            _ = amount;
            _ = baseValue;
            return DisplayName;
        }
    }
}
