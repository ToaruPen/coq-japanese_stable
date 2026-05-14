using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TerrainTravelTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummyTerrainTravelProducerTarget.HandleEvent),
        "Base encounter chance: 12%",
        "基本遭遇率: 12%",
        "BaseEncounterChance")]
    [TestCase(
        nameof(DummyTerrainTravelProducerTarget.HandleEvent),
        "Modified encounter chance: 35%",
        "修正後遭遇率: 35%",
        "ModifiedEncounterChance")]
    [TestCase(
        nameof(DummyTerrainTravelProducerTarget.HandleEvent),
        "Triggered encounter chance: 7%",
        "発生した遭遇率: 7%",
        "TriggeredEncounterChance")]
    [TestCase(
        nameof(DummyTerrainTravelProducerTarget.HandleLeavingCell),
        "Get lost chance: 40%",
        "迷子になる確率: 40%",
        "GetLostChance")]
    [TestCase(
        nameof(DummyTerrainTravelProducerTarget.HandleLeavingCell),
        "Travel speed: 1250 segments/parasang",
        "移動速度: 1250 セグメント/パラサング",
        "TravelSpeed")]
    public void TerrainTravel_TranslatesDebugQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerQueuedMessage(methodName, source, expected, detail);
    }

    [Test]
    public void TerrainTravel_PreservesQueuedMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummyTerrainTravelProducerTarget.HandleEvent),
            "Base encounter chance: 12%",
            "基本遭遇率: 12%",
            "BaseEncounterChance",
            color: "white");
    }

    [Test]
    public void TerrainTravel_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Base encounter chance: 12%";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("white"));
                Assert.That(QueueHitCount("BaseEncounterChance"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TerrainTravel_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "Base encounter chance: 12%";

        AssertOwnerQueuedMessage(
            nameof(DummyTerrainTravelProducerTarget.HandleEvent),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "BaseEncounterChance",
            expectedHits: 0);
    }

    [Test]
    public void TerrainTravel_LeavesUnsupportedQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummyTerrainTravelProducerTarget.HandleEvent),
            "Encounter chance pending.",
            "Encounter chance pending.",
            "BaseEncounterChance",
            expectedHits: 0);
    }

    [Test]
    public void TerrainTravel_TranslatesHpWarningPopup_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            "{{R|Your health has dropped below {{C|40%}}!}} Do you want to stop travelling?",
            "{{R|HPが{{C|40%}}を下回った！}} 移動をやめるか？",
            "HpWarningStopTravel");
    }

    [Test]
    public void TerrainTravel_DoesNotClaimEncounterRuntimePopup_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            "You discover a lair. Would you like to investigate?",
            "You discover a lair. Would you like to investigate?",
            "HpWarningStopTravel",
            expectedHits: 0);
    }

    [Test]
    public void TerrainTravel_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "{{R|Your health has dropped below {{C|40%}}!}} Do you want to stop travelling?";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "HpWarningStopTravel",
            expectedHits: 0);
    }

    [Test]
    public void TerrainTravel_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, "HpWarningStopTravel", expectedHits: 0);
    }

    private static void AssertOwnerQueuedMessage(
        string methodName,
        string source,
        string expected,
        string detail,
        string? color = null,
        int expectedHits = 1)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony, methodName);

            var target = new DummyTerrainTravelProducerTarget
            {
                QueuedMessageToSend = source,
                ColorToSend = color,
            };

            InvokeOwner(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(QueueHitCount(detail), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(TerrainTravelTranslationPatch),
            RequireOwnerMethod(nameof(DummyTerrainTravelProducerTarget.HandleLeavingCell)),
            () =>
            {
                var target = new DummyTerrainTravelProducerTarget { PopupMessageToShow = source };

                InvokeOwner(target, nameof(DummyTerrainTravelProducerTarget.HandleLeavingCell));

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                    Assert.That(PopupHitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static void InvokeOwner(DummyTerrainTravelProducerTarget target, string methodName)
    {
        if (methodName == nameof(DummyTerrainTravelProducerTarget.HandleLeavingCell))
        {
            var totalSegments = 0;
            _ = target.HandleLeavingCell(new object(), ref totalSegments);
            return;
        }

        _ = target.HandleEvent(new DummyInventoryActionEvent());
    }

    private static void PatchMessageQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: RequireOwnerMethod(methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(TerrainTravelTranslationPatch), nameof(TerrainTravelTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(TerrainTravelTranslationPatch), nameof(TerrainTravelTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName == nameof(DummyTerrainTravelProducerTarget.HandleLeavingCell)
            ? RequireMethod(typeof(DummyTerrainTravelProducerTarget), methodName, typeof(object), typeof(int).MakeByRefType())
            : RequireMethod(typeof(DummyTerrainTravelProducerTarget), methodName, typeof(DummyInventoryActionEvent));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            "TerrainTravelTranslationPatch." + detail);
    }

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(TerrainTravelTranslationPatch), detail);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
