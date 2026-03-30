using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationPronounExchangeTranslationPatchTests
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

    [Test]
    public void Postfix_TranslatesMutualExchange_WhenPatched()
    {
        var translated = InvokePatched(new DummyConversationSpeaker(), speakerGivePronouns: true, speakerGetPronouns: true, speakerGetNewPronouns: false);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("Mehmetと代名詞を交換した。Mehmetの代名詞はhe/him/his。"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(ConversationPronounExchangeTranslationPatch),
                    "Conversation.PronounExchange"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Postfix_TranslatesSpeakerToPlayerExchange_WhenPatched()
    {
        var translated = InvokePatched(new DummyConversationSpeaker(), speakerGivePronouns: true, speakerGetPronouns: false, speakerGetNewPronouns: false);

        Assert.That(translated, Is.EqualTo("Mehmetが代名詞を教えてくれた。he/him/his。"));
    }

    [Test]
    public void Postfix_TranslatesNewPronouns_WhenPatched()
    {
        var translated = InvokePatched(new DummyConversationSpeaker(), speakerGivePronouns: false, speakerGetPronouns: false, speakerGetNewPronouns: true);

        Assert.That(translated, Is.EqualTo("Mehmetに新しい代名詞を伝えた。"));
    }

    [Test]
    public void Postfix_TranslatesCurrentPronouns_WhenPatched()
    {
        var translated = InvokePatched(new DummyConversationSpeaker(), speakerGivePronouns: false, speakerGetPronouns: true, speakerGetNewPronouns: false);

        Assert.That(translated, Is.EqualTo("Mehmetに代名詞を伝えた。"));
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForUnknownText()
    {
        var changed = ConversationPronounExchangeTranslationPatch.TryTranslate("unknown text", out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo("unknown text"));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForEmptyString()
    {
        var changed = ConversationPronounExchangeTranslationPatch.TryTranslate(string.Empty, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Postfix_IsNoOp_ForNullResult()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationPronounExchangeTarget), nameof(DummyConversationPronounExchangeTarget.PronounExchangeDescriptionNull)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationPronounExchangeTranslationPatch), nameof(ConversationPronounExchangeTranslationPatch.Postfix))));

            var result = DummyConversationPronounExchangeTarget.PronounExchangeDescriptionNull(new object(), new DummyConversationSpeaker());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationPronounExchangeTranslationPatch),
                        "Conversation.PronounExchange"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_IsNoOp_ForUnmatchedText()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationPronounExchangeTarget), nameof(DummyConversationPronounExchangeTarget.PronounExchangeDescriptionFixed)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationPronounExchangeTranslationPatch), nameof(ConversationPronounExchangeTranslationPatch.Postfix))));

            var result = DummyConversationPronounExchangeTarget.PronounExchangeDescriptionFixed(new object(), new DummyConversationSpeaker());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("unmatched pronoun text"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationPronounExchangeTranslationPatch),
                        "Conversation.PronounExchange"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string InvokePatched(DummyConversationSpeaker speaker, bool speakerGivePronouns, bool speakerGetPronouns, bool speakerGetNewPronouns)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationPronounExchangeTarget), nameof(DummyConversationPronounExchangeTarget.PronounExchangeDescription)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationPronounExchangeTranslationPatch), nameof(ConversationPronounExchangeTranslationPatch.Postfix))));

            return DummyConversationPronounExchangeTarget.PronounExchangeDescription(
                new object(),
                speaker,
                speakerGivePronouns,
                speakerGetPronouns,
                speakerGetNewPronouns);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
