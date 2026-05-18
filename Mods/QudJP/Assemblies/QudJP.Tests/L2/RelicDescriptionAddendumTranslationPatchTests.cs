using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RelicDescriptionAddendumTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        DummyRelicDescriptionAddendumTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void GenerateRelic_TranslatesGeneratedDescriptionAddenda_WhenPatched()
    {
        WithPatchedGenerateRelic(() =>
        {
            DummyRelicDescriptionAddendumTarget.NextShort =
                "A relic. It is stamped with tiny images of salt. There's an engraving of {{C|the Farmers' Guild}} being thrown off a cliff.";

            var result = DummyRelicDescriptionAddendumTarget.GenerateRelic(
                "LongBlade",
                5,
                snapshot: null,
                adjectives: [],
                listProperties: null,
                name: null,
                article: null,
                likedFactionDescriptionAddendum: null);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.Short,
                    Is.EqualTo("A relic. それには塩の小さな図像が刻まれている。 {{C|the Farmers' Guild}}が崖から投げ落とされている様子を描いた彫刻がある。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateRelic_LeavesUnsupportedDescriptionUnchanged_WhenPatched()
    {
        WithPatchedGenerateRelic(() =>
        {
            DummyRelicDescriptionAddendumTarget.NextShort = "A relic with no generated addendum.";

            var result = DummyRelicDescriptionAddendumTarget.GenerateRelic(
                "LongBlade",
                5,
                snapshot: null,
                adjectives: [],
                listProperties: null,
                name: null,
                article: null,
                likedFactionDescriptionAddendum: null);

            Assert.Multiple(() =>
            {
                Assert.That(result.Short, Is.EqualTo("A relic with no generated addendum."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGenerateRelic(Action action)
    {
        var harmonyId = "qudjp.tests.relic-description-addendum." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyRelicDescriptionAddendumTarget),
                    nameof(DummyRelicDescriptionAddendumTarget.GenerateRelic),
                    typeof(string),
                    typeof(int),
                    typeof(object),
                    typeof(List<string>),
                    typeof(Dictionary<string, List<string>>),
                    typeof(string),
                    typeof(string),
                    typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(RelicDescriptionAddendumTranslationPatch),
                    nameof(RelicDescriptionAddendumTranslationPatch.Postfix),
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
            RelicDescriptionAddendumTranslationPatch.Context,
            RelicDescriptionAddendumTranslationPatch.Family);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
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
}

internal static class DummyRelicDescriptionAddendumTarget
{
    public static string NextShort { get; set; } = "A relic.";

    public static void Reset()
    {
        NextShort = "A relic.";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DummyRelicDescriptionObject GenerateRelic(
        string type,
        int tier,
        object? snapshot,
        List<string>? adjectives,
        Dictionary<string, List<string>>? listProperties,
        string? name,
        string? article,
        string? likedFactionDescriptionAddendum)
    {
        _ = type;
        _ = tier;
        _ = snapshot;
        _ = adjectives;
        _ = listProperties;
        _ = name;
        _ = article;
        _ = likedFactionDescriptionAddendum;
        return new DummyRelicDescriptionObject { Short = NextShort };
    }
}

internal sealed class DummyRelicDescriptionObject
{
    public string Short { get; set; } = string.Empty;
}
