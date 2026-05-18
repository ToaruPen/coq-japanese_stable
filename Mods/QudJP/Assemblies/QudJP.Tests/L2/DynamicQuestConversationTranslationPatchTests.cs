using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DynamicQuestConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyDynamicQuestConversationTarget.Reset();
    }

    [Test]
    public void AppendQuestCompletionSequence_TranslatesOnlyOwnerExpandedText_WhenPatched()
    {
        WithPatchedAppendQuestCompletionSequence(() =>
        {
            DummyDynamicQuestConversationTarget.AppendQuestCompletionSequence();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyDynamicQuestConversationTarget.Nodes,
                    Does.Contain("冒険者よ、感謝する。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。"));
                Assert.That(
                    DummyDynamicQuestConversationTarget.Nodes,
                    Does.Contain("旅人よ、感謝する。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。"));
                Assert.That(DummyDynamicQuestConversationTarget.Choices, Does.Contain("この辺りに仕事はあるか？"));
                Assert.That(HitCount(), Is.EqualTo(3));
            });
        });

        var outsideOwner = DummyHistoricStringExpander.ExpandString("Is there work around here?");
        Assert.That(outsideOwner, Is.EqualTo("Is there work around here?"));
    }

    [Test]
    public void AppendQuestCompletionSequence_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedAppendQuestCompletionSequence(() =>
        {
            DummyDynamicQuestConversationTarget.IntroTemplate =
                MessageFrameTranslator.DirectTranslationMarker + "I'm looking for work.";

            DummyDynamicQuestConversationTarget.AppendQuestCompletionSequence();

            Assert.Multiple(() =>
            {
                Assert.That(DummyDynamicQuestConversationTarget.Choices, Does.Contain("I'm looking for work."));
                Assert.That(HitCount(), Is.EqualTo(2));
            });
        });
    }

    private static void WithPatchedAppendQuestCompletionSequence(Action action)
    {
        var harmonyId = "qudjp.tests.dynamic-quest-conversation." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestConversationTarget),
                    nameof(DummyDynamicQuestConversationTarget.AppendQuestCompletionSequence)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(DynamicQuestConversationTranslationPatch),
                    nameof(DynamicQuestConversationTranslationPatch.Transpiler),
                    typeof(IEnumerable<CodeInstruction>))));
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
            nameof(DynamicQuestConversationTranslationPatch),
            nameof(DynamicQuestConversationTranslationPatch) + ".ExpandString");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyDynamicQuestConversationTarget
{
    public static string IntroTemplate { get; set; } = "Is there work around here?";

    public static List<string> Nodes { get; } = [];

    public static List<string> Choices { get; } = [];

    public static void Reset()
    {
        IntroTemplate = "Is there work around here?";
        Nodes.Clear();
        Choices.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AppendQuestCompletionSequence()
    {
        AddNode(DummyHistoricStringExpander.ExpandString("Our thanks, adventurer. Our village owes you a debt. For now, please choose a reward from our stockpile as payment for your service."));
        AddNode(DummyHistoricStringExpander.ExpandString("Our thanks, traveler. You've proven =player.reflexive= a friend to our village. Take this recoiler and return whenever your throat is dry."));
        DistributeChoice(DummyHistoricStringExpander.ExpandString(IntroTemplate));
    }

    private static void AddNode(string text)
    {
        Nodes.Add(text);
    }

    private static void DistributeChoice(string text)
    {
        Choices.Add(text);
    }
}
