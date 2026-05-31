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
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Postfix_TranslatesConvertedThirdPersonDeathReason_WhenPatched()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyGameTextTarget),
                    nameof(DummyGameTextTarget.RoughConvertSecondPersonToThirdPerson)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(GameTextDeathReasonTranslationPatch),
                    nameof(GameTextDeathReasonTranslationPatch.Postfix))));

            var result = DummyGameTextTarget.RoughConvertSecondPersonToThirdPerson("You were vaporized.", new object());

            Assert.That(result, Is.EqualTo("スナップジョーは蒸発した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslateThirdPersonDeathReason_LeavesUnknownReasonUnchanged()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("snapjaw was splatted."),
            Is.EqualTo("snapjaw was splatted."));
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
