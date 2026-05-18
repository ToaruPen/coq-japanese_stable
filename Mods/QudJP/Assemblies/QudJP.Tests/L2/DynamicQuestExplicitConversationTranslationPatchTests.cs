using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DynamicQuestExplicitConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyDynamicQuestExplicitConversationTarget.Reset();
    }

    [Test]
    public void IntroChoicePrefix_TranslatesAndRecordsIntroChoice()
    {
        var text = "Yes. I will find the rusted relic as you ask.";

        WithPatchedIntroChoice(() => DummyDynamicQuestExplicitConversationTarget.IntroChoice(text));

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyDynamicQuestExplicitConversationTarget.LastIntroChoice,
                Is.EqualTo("はい。頼まれたとおり錆びた遺物を探す。"));
            Assert.That(IntroHitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void IntroChoicePrefix_StripsDirectMarkerWithoutObservabilityHit()
    {
        var text = MessageFrameTranslator.DirectTranslationMarker + "No, I will not.";

        WithPatchedIntroChoice(() => DummyDynamicQuestExplicitConversationTarget.IntroChoice(text));

        Assert.Multiple(() =>
        {
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastIntroChoice, Is.EqualTo("No, I will not."));
            Assert.That(IntroHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CompletionPrefix_TranslatesCompletionAndIncompleteChoices()
    {
        var complete = "I've found the rusted relic.";
        var incomplete = "I don't have the rusted relic yet.";

        WithPatchedCompletionChoice(
            () => DummyDynamicQuestExplicitConversationTarget.CompletionChoice(complete, incomplete));

        Assert.Multiple(() =>
        {
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastCompleteChoice, Is.EqualTo("錆びた遺物を見つけた。"));
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastIncompleteChoice, Is.EqualTo("まだ錆びた遺物を持っていない。"));
            Assert.That(ConversationHitCount("CompletionChoice"), Is.EqualTo(1));
            Assert.That(ConversationHitCount("IncompleteChoice"), Is.EqualTo(1));
        });
    }

    [Test]
    public void CompletionPrefix_LeavesUnknownCompletionChoicesUnchanged()
    {
        var complete = "Something else happened.";
        var incomplete = "Nothing else happened yet.";

        WithPatchedCompletionChoice(
            () => DummyDynamicQuestExplicitConversationTarget.CompletionChoice(complete, incomplete));

        Assert.Multiple(() =>
        {
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastCompleteChoice, Is.EqualTo("Something else happened."));
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastIncompleteChoice, Is.EqualTo("Nothing else happened yet."));
            Assert.That(ConversationHitCount("CompletionChoice"), Is.Zero);
            Assert.That(ConversationHitCount("IncompleteChoice"), Is.Zero);
        });
    }

    [Test]
    public void CompletionPrefix_StripsDirectMarkersWithoutObservabilityHit()
    {
        var complete = MessageFrameTranslator.DirectTranslationMarker + "I've found the rusted relic.";
        var incomplete = MessageFrameTranslator.DirectTranslationMarker + "I don't have the rusted relic yet.";

        WithPatchedCompletionChoice(
            () => DummyDynamicQuestExplicitConversationTarget.CompletionChoice(complete, incomplete));

        Assert.Multiple(() =>
        {
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastCompleteChoice, Is.EqualTo("I've found the rusted relic."));
            Assert.That(DummyDynamicQuestExplicitConversationTarget.LastIncompleteChoice, Is.EqualTo("I don't have the rusted relic yet."));
            Assert.That(ConversationHitCount("CompletionChoice"), Is.Zero);
            Assert.That(ConversationHitCount("IncompleteChoice"), Is.Zero);
        });
    }

    private static void WithPatchedIntroChoice(Action action)
    {
        var harmonyId = "qudjp.tests.dynamic-quest-explicit-intro." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestExplicitConversationTarget),
                    nameof(DummyDynamicQuestExplicitConversationTarget.IntroChoice),
                    typeof(string)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(DynamicQuestIntroChoiceTranslationPatch),
                    nameof(DynamicQuestIntroChoiceTranslationPatch.Prefix),
                    typeof(string).MakeByRefType())));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedCompletionChoice(Action action)
    {
        var harmonyId = "qudjp.tests.dynamic-quest-explicit-completion." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestExplicitConversationTarget),
                    nameof(DummyDynamicQuestExplicitConversationTarget.CompletionChoice),
                    typeof(string),
                    typeof(string)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(DynamicQuestConversationTranslationPatch),
                    nameof(DynamicQuestConversationTranslationPatch.Prefix),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType())));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int IntroHitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DynamicQuestIntroChoiceTranslationPatch),
            nameof(DynamicQuestIntroChoiceTranslationPatch) + ".IntroChoice");

    private static int ConversationHitCount(string route) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DynamicQuestConversationTranslationPatch),
            nameof(DynamicQuestConversationTranslationPatch) + "." + route);

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyDynamicQuestExplicitConversationTarget
{
    public static string LastIntroChoice { get; private set; } = string.Empty;

    public static string LastCompleteChoice { get; private set; } = string.Empty;

    public static string LastIncompleteChoice { get; private set; } = string.Empty;

    public static void Reset()
    {
        LastIntroChoice = string.Empty;
        LastCompleteChoice = string.Empty;
        LastIncompleteChoice = string.Empty;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void IntroChoice(string text)
    {
        LastIntroChoice = text;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CompletionChoice(string completeText, string incompleteText)
    {
        LastCompleteChoice = completeText;
        LastIncompleteChoice = incompleteText;
    }
}
