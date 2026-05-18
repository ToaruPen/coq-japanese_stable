using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TempleDedicationPlaqueInscriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        DummyTempleDedicationPlaqueTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void GenerateInscription_TranslatesDedicationFrame_WhenPatched()
    {
        WithPatchedGenerateInscription(() =>
        {
            var inscription = DummyTempleDedicationPlaqueTarget.GenerateInscription();

            Assert.Multiple(() =>
            {
                Assert.That(
                    inscription,
                    Is.EqualTo("この寺院は638,01qyにthe Exhaustiers' Guildによって建てられた。彼らはクロムの時代に、エグレゴア「四角車輪」から分離した。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateInscription_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedGenerateInscription(() =>
        {
            DummyTempleDedicationPlaqueTarget.Inscription =
                MessageFrameTranslator.DirectTranslationMarker + DummyTempleDedicationPlaqueTarget.Inscription;

            var inscription = DummyTempleDedicationPlaqueTarget.GenerateInscription();

            Assert.Multiple(() =>
            {
                Assert.That(
                    inscription,
                    Is.EqualTo("This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore Square Wheel in the Chrome Era."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGenerateInscription(Action action)
    {
        var harmonyId = "qudjp.tests.temple-dedication-plaque-inscription." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTempleDedicationPlaqueTarget),
                    nameof(DummyTempleDedicationPlaqueTarget.GenerateInscription)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(TempleDedicationPlaqueInscriptionTranslationPatch),
                    nameof(TempleDedicationPlaqueInscriptionTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));
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
            nameof(TempleDedicationPlaqueInscriptionTranslationPatch),
            nameof(TempleDedicationPlaqueInscriptionTranslationPatch) + ".GenerateInscription");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
}

internal static class DummyTempleDedicationPlaqueTarget
{
    public static string Inscription { get; set; } =
        "This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore Square Wheel in the Chrome Era.";

    public static void Reset()
    {
        Inscription =
            "This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore Square Wheel in the Chrome Era.";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GenerateInscription()
    {
        return Inscription;
    }
}
