using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PseudoRelicGeneratedNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void Send_TranslatesFactionHeirloomStyleNameAndClearsArticle_WhenPatched()
    {
        WithPatchedSend(() =>
        {
            var relic = new DummyPseudoRelicObject
            {
                DisplayName = "Edge of the Dominant Sword",
                IndefiniteArticle = "the",
                DefiniteArticle = "the",
            };
            relic.SetCachedDisplayNameForSort("Edge of the Dominant Sword");

            DummyAfterPseudoRelicGeneratedEventTarget.Send(relic, "might", "LongBlade", "sword", 5);

            Assert.Multiple(() =>
            {
                Assert.That(relic.DisplayName, Is.EqualTo("支配的な剣の刃"));
                Assert.That(relic.IndefiniteArticle, Is.Empty);
                Assert.That(relic.DefiniteArticle, Is.Empty);
                Assert.That(relic.GetCachedDisplayNameForSort(), Is.Null);
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Send_TranslatesRuinsStyleNameAndPreservesPlaceName_WhenPatched()
    {
        WithPatchedSend(() =>
        {
            var relic = new DummyPseudoRelicObject
            {
                DisplayName = "Dominant Sword of some forgotten ruins",
                IndefiniteArticle = "the",
                DefiniteArticle = "the",
            };

            DummyAfterPseudoRelicGeneratedEventTarget.Send(relic, "might", "LongBlade", "sword", 5);

            Assert.Multiple(() =>
            {
                Assert.That(relic.DisplayName, Is.EqualTo("忘れられた遺跡の支配的な剣"));
                Assert.That(relic.IndefiniteArticle, Is.Empty);
                Assert.That(relic.DefiniteArticle, Is.Empty);
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Send_LeavesUnknownNameUnchanged_WhenPatched()
    {
        WithPatchedSend(() =>
        {
            var relic = new DummyPseudoRelicObject
            {
                DisplayName = "Edge of the Qwern Sword",
                IndefiniteArticle = "the",
                DefiniteArticle = "the",
            };

            DummyAfterPseudoRelicGeneratedEventTarget.Send(relic, "might", "LongBlade", "sword", 5);

            Assert.Multiple(() =>
            {
                Assert.That(relic.DisplayName, Is.EqualTo("Edge of the Qwern Sword"));
                Assert.That(relic.IndefiniteArticle, Is.EqualTo("the"));
                Assert.That(relic.DefiniteArticle, Is.EqualTo("the"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Send_StripsDirectMarkerAndClearsArticleAndCache_WhenPatched()
    {
        WithPatchedSend(() =>
        {
            var relic = new DummyPseudoRelicObject
            {
                DisplayName = MessageFrameTranslator.DirectTranslationMarker + "Edge of the Dominant Sword",
                IndefiniteArticle = "the",
                DefiniteArticle = "the",
            };
            relic.SetCachedDisplayNameForSort("Edge of the Dominant Sword");

            DummyAfterPseudoRelicGeneratedEventTarget.Send(relic, "might", "LongBlade", "sword", 5);

            Assert.Multiple(() =>
            {
                Assert.That(relic.DisplayName, Is.EqualTo("Edge of the Dominant Sword"));
                Assert.That(relic.IndefiniteArticle, Is.Empty);
                Assert.That(relic.DefiniteArticle, Is.Empty);
                Assert.That(relic.GetCachedDisplayNameForSort(), Is.Null);
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedSend(Action action)
    {
        var harmonyId = "qudjp.tests.pseudo-relic-generated-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyAfterPseudoRelicGeneratedEventTarget),
                    nameof(DummyAfterPseudoRelicGeneratedEventTarget.Send),
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(PseudoRelicGeneratedNameTranslationPatch),
                    nameof(PseudoRelicGeneratedNameTranslationPatch.Postfix),
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
            PseudoRelicGeneratedNameTranslationPatch.Context,
            PseudoRelicGeneratedNameTranslationPatch.Family);
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

internal static class DummyAfterPseudoRelicGeneratedEventTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Send(object item, string element, string type, string subtype, int tier)
    {
        _ = item;
        _ = element;
        _ = type;
        _ = subtype;
        _ = tier;
    }
}

internal sealed class DummyPseudoRelicObject
{
    public string DisplayName { get; set; } = string.Empty;

    public string IndefiniteArticle { get; set; } = string.Empty;

    public string DefiniteArticle { get; set; } = string.Empty;

    private string? _CachedDisplayNameForSort;

    public string? GetCachedDisplayNameForSort() => _CachedDisplayNameForSort;

    public void SetCachedDisplayNameForSort(string? value)
    {
        _CachedDisplayNameForSort = value;
    }

    public void SetStringProperty(string name, string value, bool removeIfNull = false)
    {
        _ = removeIfNull;
        if (string.Equals(name, "IndefiniteArticle", StringComparison.Ordinal))
        {
            IndefiniteArticle = value;
        }
        else if (string.Equals(name, "DefiniteArticle", StringComparison.Ordinal))
        {
            DefiniteArticle = value;
        }
    }
}
