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

    [TestCase("snapjaw was vaporized.", "スナップジョーは蒸発した。")]
    [TestCase("snapjaws were vaporized.", "snapjawsは蒸発した。")]
    [TestCase("snapjaw was vaporized", "snapjaw was vaporized")]
    [TestCase("snapjaw was vaporized!", "snapjaw was vaporized!")]
    [TestCase("", "")]
    public void TranslateThirdPersonDeathReason_HandlesWasWerePunctuationAndEmptyCases(
        string source,
        string expected)
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests(source),
            Is.EqualTo(expected));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_PreservesColorTaggedSubject()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("{{R|snapjaw}} was vaporized."),
            Is.EqualTo("{{R|スナップジョー}}は蒸発した。"));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_StripsDirectMarkerWithoutRetranslating()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests(
                MessageFrameTranslator.MarkDirectTranslation("snapjaw was vaporized.")),
            Is.EqualTo("snapjaw was vaporized."));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_UsesSingleJapaneseSentenceEnd()
    {
        var result = GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("snapjaw was vaporized.");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("スナップジョーは蒸発した。"));
            Assert.That(result.Count(character => character == '。'), Is.EqualTo(1));
        });
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
