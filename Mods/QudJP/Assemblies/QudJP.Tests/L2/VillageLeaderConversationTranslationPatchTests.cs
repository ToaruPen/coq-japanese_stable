using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VillageLeaderConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Prefix_TranslatesLeaderIntro()
    {
        var text = "Live and drink, friend.";

        VillageLeaderConversationTranslationPatch.Prefix(ref text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("生きて飲め、友。"));
            Assert.That(HitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void Prefix_DoesNotRecordHit_ForDirectMarker()
    {
        var text = MessageFrameTranslator.DirectTranslationMarker + "Live and drink, friend.";

        VillageLeaderConversationTranslationPatch.Prefix(ref text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("Live and drink, friend."));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void HarmonyPrefix_BindsLeaderMessageByArgumentIndex()
    {
        using var patch = PatchDummyAddVillagerConversation();

        DummyAddVillagerConversationTarget.Reset();
        DummyAddVillagerConversationTarget.AddVillagerConversation(
            obj: new object(),
            message: "Live and drink, friend.",
            response: "Live and drink.",
            Q1: null,
            A1: null,
            AppendConversation: false,
            ClearLost: true);

        Assert.Multiple(() =>
        {
            Assert.That(DummyAddVillagerConversationTarget.LastMessage, Is.EqualTo("生きて飲め、友。"));
            Assert.That(HitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void HarmonyPrefix_StripsDirectMarkerByArgumentIndex_WithoutRecordingHit()
    {
        using var patch = PatchDummyAddVillagerConversation();

        DummyAddVillagerConversationTarget.Reset();
        DummyAddVillagerConversationTarget.AddVillagerConversation(
            obj: new object(),
            message: MessageFrameTranslator.DirectTranslationMarker + "Live and drink, friend.",
            response: "Live and drink.",
            Q1: null,
            A1: null,
            AppendConversation: false,
            ClearLost: true);

        Assert.Multiple(() =>
        {
            Assert.That(DummyAddVillagerConversationTarget.LastMessage, Is.EqualTo("Live and drink, friend."));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    private static int HitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(VillageLeaderConversationTranslationPatch),
            nameof(VillageLeaderConversationTranslationPatch) + ".leaderIntro");

    private static HarmonyPatchScope PatchDummyAddVillagerConversation()
    {
        var harmonyId = "qudjp.tests.village-leader-conversation." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: AccessTools.Method(
                typeof(DummyAddVillagerConversationTarget),
                nameof(DummyAddVillagerConversationTarget.AddVillagerConversation)),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(VillageLeaderConversationTranslationPatch),
                    nameof(VillageLeaderConversationTranslationPatch.Prefix))));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static class DummyAddVillagerConversationTarget
    {
        public static string? LastMessage { get; private set; }

        public static void Reset()
        {
            LastMessage = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddVillagerConversation(
            object obj,
            string message,
            string response,
            string? Q1,
            string? A1,
            bool AppendConversation,
            bool ClearLost)
        {
            _ = obj;
            _ = response;
            _ = Q1;
            _ = A1;
            _ = AppendConversation;
            _ = ClearLost;
            LastMessage = message;
        }
    }

    private sealed class HarmonyPatchScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyPatchScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
