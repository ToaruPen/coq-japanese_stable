using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameTextDeathReasonTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        DynamicTextObservability.ResetForTests();
        DummyGameTextTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyGameTextTarget.Reset();
    }

    [Test]
    public void Postfix_TranslatesConvertedThirdPersonDeathReason_WhenPatched()
    {
        AssertPatchedDeathReason("snapjaw was vaporized.", "スナップジョーは蒸発した。", 1);
    }

    [Test]
    public void Postfix_LeavesUnsupportedDeathReasonUnchanged_WhenPatched()
    {
        AssertPatchedDeathReason("snapjaw was frobnicated.", "snapjaw was frobnicated.", 0);
    }

    [Test]
    public void Postfix_LeavesEmptyDeathReasonUnchanged_WhenPatched()
    {
        AssertPatchedDeathReason(string.Empty, string.Empty, 0);
    }

    [Test]
    public void Postfix_PreservesColorTagsInDeathReason_WhenPatched()
    {
        AssertPatchedDeathReason("{{C|snapjaw}} was vaporized.", "{{C|スナップジョー}}は蒸発した。", 1);
    }

    [Test]
    public void Postfix_StripsDirectMarkerFromDeathReason_WhenPatched()
    {
        AssertPatchedDeathReason(MessageFrameTranslator.MarkDirectTranslation("スナップジョーは蒸発した。"), "スナップジョーは蒸発した。", 1);
    }

    private static void AssertPatchedDeathReason(string source, string expected, int expectedHitCount)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            DummyGameTextTarget.ResultOverride = source;
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyGameTextTarget),
                    nameof(DummyGameTextTarget.RoughConvertSecondPersonToThirdPerson)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(GameTextDeathReasonTranslationPatch),
                    nameof(GameTextDeathReasonTranslationPatch.Postfix))));

            var result = DummyGameTextTarget.RoughConvertSecondPersonToThirdPerson("You were vaporized.", new object());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(GameTextDeathReasonTranslationPatch),
                        "DeathReason.ThirdPersonConverted"),
                    Is.EqualTo(expectedHitCount));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void RoughConvertSecondPersonToThirdPerson_TargetMethodExistsInL2Harness()
    {
        Assert.That(
            RequireMethod(
                typeof(DummyGameTextTarget),
                nameof(DummyGameTextTarget.RoughConvertSecondPersonToThirdPerson)),
            Is.Not.Null);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
