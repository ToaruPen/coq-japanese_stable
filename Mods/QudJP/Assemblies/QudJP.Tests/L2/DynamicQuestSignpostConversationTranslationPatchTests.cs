using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DynamicQuestSignpostConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyDynamicQuestSignpostConversationTarget.Reset();
    }

    [Test]
    public void HandleEvent_TranslatesSignpostIntroAndTargetPrefix_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyDynamicQuestSignpostConversationTarget.Choices, Does.Contain("仕事を探している。"));
                Assert.That(DummyDynamicQuestSignpostConversationTarget.Nodes, Does.Contain("北にいる{{Y|Mehmet}}と話す。"));
                Assert.That(HitCount(), Is.EqualTo(3));
            });
        });

        var outsideOwner = DummyHistoricStringExpander.ExpandString("Speak to ");
        Assert.That(outsideOwner, Is.EqualTo("Speak to "));
    }

    [Test]
    public void HandleEvent_TranslatesMultipleSignpostTargets_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.TargetList =
                "{{Y|Mehmet}}, to the north, {{Y|Ashe}}, also to the north, or {{Y|Elder}}, to the southeast.";

            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.That(
                DummyDynamicQuestSignpostConversationTarget.Nodes,
                Does.Contain("北にいる{{Y|Mehmet}}、または北にいる{{Y|Ashe}}、または南東にいる{{Y|Elder}}と話す。"));
        });
    }

    [Test]
    public void HandleEvent_TranslatesFindSignpostTargets_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.SpeakToPrefix = "Find ";
            DummyDynamicQuestSignpostConversationTarget.TargetList =
                "{{Y|Mehmet}}, to the north, or {{Y|Ashe}}, to the south.";

            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.That(
                DummyDynamicQuestSignpostConversationTarget.Nodes,
                Does.Contain("北にいる{{Y|Mehmet}}、または南にいる{{Y|Ashe}}を探す。"));
        });
    }

    [Test]
    public void HandleEvent_TranslatesHereSignpostTargets_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.TargetList =
                "{{Y|Mehmet}}, here, or {{Y|Ashe}}, also here.";

            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.That(
                DummyDynamicQuestSignpostConversationTarget.Nodes,
                Does.Contain("ここにいる{{Y|Mehmet}}、またはここにいる{{Y|Ashe}}と話す。"));
        });
    }

    [Test]
    public void HandleEvent_TranslatesBareSignpostTargets_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.TargetList =
                "{{Y|Mehmet}}, {{Y|Ashe}}, to the south.";

            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.That(
                DummyDynamicQuestSignpostConversationTarget.Nodes,
                Does.Contain("{{Y|Mehmet}}、または南にいる{{Y|Ashe}}と話す。"));
        });
    }

    [Test]
    public void HandleEvent_TranslatesBareFindSignpostTarget_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.SpeakToPrefix = "Find ";
            DummyDynamicQuestSignpostConversationTarget.TargetList = "{{Y|Mehmet}}.";

            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.That(
                DummyDynamicQuestSignpostConversationTarget.Nodes,
                Does.Contain("{{Y|Mehmet}}を探す。"));
        });
    }

    [Test]
    public void HandleEvent_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyDynamicQuestSignpostConversationTarget.ChoiceIntro =
                MessageFrameTranslator.DirectTranslationMarker + "I'm looking for work.";
            DummyDynamicQuestSignpostConversationTarget.SpeakToPrefix =
                MessageFrameTranslator.DirectTranslationMarker + "Speak to ";

            DummyDynamicQuestSignpostConversationTarget.HandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyDynamicQuestSignpostConversationTarget.Choices, Does.Contain("I'm looking for work."));
                Assert.That(DummyDynamicQuestSignpostConversationTarget.Nodes, Does.Contain("Speak to {{Y|Mehmet}}, to the north."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedHandleEvent(Action action)
    {
        var harmonyId = "qudjp.tests.dynamic-quest-signpost-conversation." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyDynamicQuestSignpostConversationTarget),
                    nameof(DummyDynamicQuestSignpostConversationTarget.HandleEvent)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(DynamicQuestSignpostConversationTranslationPatch),
                    nameof(DynamicQuestSignpostConversationTranslationPatch.Transpiler),
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
            nameof(DynamicQuestSignpostConversationTranslationPatch),
            nameof(DynamicQuestSignpostConversationTranslationPatch) + ".ExpandString");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyDynamicQuestSignpostConversationTarget
{
    public static string ChoiceIntro { get; set; } = "I'm looking for work.";

    public static string SpeakToPrefix { get; set; } = "Speak to ";

    public static string TargetList { get; set; } = "{{Y|Mehmet}}, to the north.";

    public static List<string> Choices { get; } = [];

    public static List<string> Nodes { get; } = [];

    public static void Reset()
    {
        ChoiceIntro = "I'm looking for work.";
        SpeakToPrefix = "Speak to ";
        TargetList = "{{Y|Mehmet}}, to the north.";
        Choices.Clear();
        Nodes.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void HandleEvent()
    {
        Choices.Add(DummyHistoricStringExpander.ExpandString(ChoiceIntro));
        Nodes.Add(DummyHistoricStringExpander.ExpandString(SpeakToPrefix) + TargetList);
    }
}
