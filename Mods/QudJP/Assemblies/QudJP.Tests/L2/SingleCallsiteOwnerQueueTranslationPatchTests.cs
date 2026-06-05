using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SingleCallsiteOwnerQueueTranslationPatchTests
{
    private const string ActivatedAbilityEntryOwner = "XRL.World.Parts.ActivatedAbilityEntry|TrySendCommandEventOnPlayer";
    private const string BiomeSurfaceDistributionOwner = "XRL.World.Biomes.BiomeManager|DisplaySurfaceDistribution";
    private const string ElevatorSwitchOwner = "XRL.World.Parts.ElevatorSwitch|FireEvent";
    private const string FetchesOwner = "XRL.World.Parts.Fetches|HandleEvent";
    private const string ModMorphogeneticOwner = "XRL.World.Parts.ModMorphogenetic|ApplyMorphicShock";
    private const string MonochromeOwner = "XRL.World.Effects.Monochrome|FireEvent";
    private const string PersuasionRebukeRobotAttemptOwner = "XRL.World.Parts.Skill.Persuasion_RebukeRobot|AttemptRebuke";
    private const string PyroZoneStartedOwner = "XRL.World.Parts.PyroZone|Started";
    private const string PyroZoneStoppedOwner = "XRL.World.Parts.PyroZone|Stopped";
    private const string CryoZoneStartedOwner = "XRL.World.Parts.CryoZone|Started";
    private const string CryoZoneStoppedOwner = "XRL.World.Parts.CryoZone|Stopped";
    private const string SnapjawHowlOwner = "XRL.World.Parts.Skill.Snapjaw_Howl|FireEvent";
    private const string SphynxSaltTonicOwner = "XRL.World.Effects.SphynxSalt_Tonic|Apply";
    private const string StairsDownOwner = "XRL.World.Parts.StairsDown|CheckPullDown";
    private const string ThiefBotOwner = "XRL.World.Parts.ThiefBot|FireEvent";
    private const string TonicHandleEventOwner = "XRL.World.Parts.Tonic|HandleEvent";
    private const string WeirdwireConduitOwner = "XRL.World.Quests.WeirdwireConduitSystem|HandleEvent";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.TrySendCommandEventOnPlayer),
        "You cannot do that on the world map.",
        "ワールドマップではそれはできない。",
        "ActivatedAbilityEntryWorldMapBlock")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.TrySendCommandEventOnPlayer),
        "You must wait {{C|7 round}} before using {{C|Phase Blink}}.",
        "{{C|Phase Blink}}を使うには{{C|7ラウンド}}待つ必要がある。",
        "ActivatedAbilityEntryNotUsableDescription")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.DisplaySurfaceDistribution),
        "Fungal biome: 37/4200, 0%.",
        "菌類バイオーム: 37/4200、0%。",
        "BiomeSurfaceDistribution")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.FireElevatorSwitchEvent),
        "Nothing seems to happen when you hit the switch.",
        "スイッチを押しても何も起こらない。",
        "ElevatorSwitchNothingHappens")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
        "A weird, painful shock reverberates through you.",
        "「奇妙で痛い電撃」が全身を駆け抜けた。",
        "ModMorphogeneticPainfulShock")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
        "A weird shock reverberates through you.",
        "「奇妙な電撃」が全身を駆け抜けた。",
        "ModMorphogeneticPainlessShock")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.FireMonochromeEvent),
        "Color starts to seep into the world.",
        "世界に色が染み込んでいく。",
        "MonochromeColorReturns")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.AttemptRebukeRobot),
        "You cannot rebuke without a tongue.",
        "舌がないと叱責できない。",
        "PersuasionRebukeRobotMissingTongue")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.PyroZoneStarted),
        "The air to the southeast starts to shimmer with heat!",
        "南東の空気が熱で揺らめき始めた！",
        "PyroZoneStarted")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.PyroZoneStopped),
        "The air to the southwest ceases shimmering with heat.",
        "南西の空気の熱による揺らめきが収まった。",
        "PyroZoneStopped")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.CryoZoneStarted),
        "The air here bursts into a field of frigid mist!",
        "このあたりの空気が極寒の霧に包まれた！",
        "CryoZoneStarted")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.CryoZoneStopped),
        "The frigid mist to the northwest dissipates.",
        "北西の極寒の霧が消えた。",
        "CryoZoneStopped")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.FireSnapjawHowlEvent),
        "You are frenzied by the howl!",
        "遠吠えに興奮させられた！",
        "SnapjawHowlFrenzy")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.ApplySphynxSaltTonic),
        "You sense a subtle psychic disturbance.",
        "かすかな精神的乱れを感じる。",
        "SphynxSaltPsychicDisturbance")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.CheckPullDown),
        "You fall downward!",
        "下に落ちた！",
        "StairsDownFallDownward")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.FireThiefBotEvent),
        "the chrome idol's pincers pass through you harmlessly.",
        "the chrome idolのハサミはあなたを傷つけることなくすり抜けた。",
        "ThiefBotPincersPassThrough")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.FireThiefBotEvent),
        "You avoid the chrome idol's pincers.",
        "the chrome idolのハサミを避けた。",
        "ThiefBotAvoidPincers")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleFetchesEvent),
        "the dog runs off to fetch {{Y|a salve tonic}}!",
        "the dogは{{Y|a salve tonic}}を取りに走り去った！",
        "FetchesRunsOffToFetch")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "the goat eats {{Y|a salve tonic}}.",
        "the goatは{{Y|a salve tonic}}を食べた。",
        "TonicVisibleConsume")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "the goat applies {{Y|a shade oil injector}}.",
        "the goatは{{Y|a shade oil injector}}を使用した。",
        "TonicVisibleConsume")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "the goat is unable to consume tonics.",
        "the goatはトニックを摂取できない。",
        "TonicUnableConsume")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "You are unable to consume tonics.",
        "あなたはトニックを摂取できない。",
        "TonicUnableConsume")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "{{Y|salve tonic}} is broken...",
        "{{Y|salve tonic}}は壊れている...",
        "TonicBroken")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "{{Y|salve tonic}} is rusted...",
        "{{Y|salve tonic}}は錆びている...",
        "TonicRusted")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "You are out of phase with {{R|snapjaw}}.",
        "{{R|snapjaw}}とは位相がずれている。",
        "TonicOutOfPhase")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "You cannot reach {{R|snapjaw}}.",
        "{{R|snapjaw}}に届かない。",
        "TonicCannotReach")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "There is no one there you can feed {{Y|salve tonic}} to.",
        "そこには{{Y|salve tonic}}を飲ませられる相手がいない。",
        "TonicNoOneThere")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "There is no one there you can apply {{Y|salve tonic}} to.",
        "そこには{{Y|salve tonic}}を使用できる相手がいない。",
        "TonicNoOneThere")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "If you want to eat {{Y|salve tonic}} yourself, you can do so through the eat action.",
        "{{Y|salve tonic}}を自分自身に食べさせたい場合は、食べるアクションから行える。",
        "TonicSelfTarget")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "If you want to apply {{Y|salve tonic}} to yourself, you can do so through the apply action.",
        "{{Y|salve tonic}}を自分自身に使用したい場合は、使用アクションから行える。",
        "TonicSelfTarget")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "{{R|snapjaw}} does not want to consume {{Y|salve tonic}}.",
        "{{R|snapjaw}}は{{Y|salve tonic}}を摂取したがっていない。",
        "TonicUnwillingConsume")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "{{R|snapjaw}} does not want {{Y|salve tonic}} applied to them. You'll need to equip it as a weapon and attack with it.",
        "{{R|snapjaw}}は{{Y|salve tonic}}を使用されたがっていない。武器として装備し、それで攻撃する必要がある。",
        "TonicUnwillingApply")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
        "You now have 37 feet of copper wire.",
        "銅線を37フィート持っている。",
        "WeirdwireCopperWireTotal")]
    public void SingleCallsiteOwnerQueue_TranslatesOwnerMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerQueuedMessage(methodName, source, expected, detail);
    }

    [Test]
    public void SingleCallsiteOwnerQueue_PreservesQueuedMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "You now have 37 feet of copper wire.",
            "銅線を37フィート持っている。",
            "WeirdwireCopperWireTotal",
            color: "c");
    }

    [Test]
    public void SingleCallsiteOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You now have 37 feet of copper wire.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "c", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("c"));
                Assert.That(HitCount("WeirdwireCopperWireTotal"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SingleCallsiteOwnerQueue_DoesNotTranslateWrongOwnerMessage_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
            "Nothing seems to happen when you hit the switch.",
            "Nothing seems to happen when you hit the switch.",
            "ElevatorSwitchNothingHappens",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
            "You cannot do that on the world map.",
            "You cannot do that on the world map.",
            "ActivatedAbilityEntryWorldMapBlock",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "You are frenzied by the howl!",
            "You are frenzied by the howl!",
            "SnapjawHowlFrenzy",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
            "You now have 37 feet of copper wire.",
            "You now have 37 feet of copper wire.",
            "WeirdwireCopperWireTotal",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "A weird, painful shock reverberates through you.",
            "A weird, painful shock reverberates through you.",
            "ModMorphogeneticPainfulShock",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock),
            "Color starts to seep into the world.",
            "Color starts to seep into the world.",
            "MonochromeColorReturns",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.FireMonochromeEvent),
            "You cannot rebuke without a tongue.",
            "You cannot rebuke without a tongue.",
            "PersuasionRebukeRobotMissingTongue",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.AttemptRebukeRobot),
            "You sense a subtle psychic disturbance.",
            "You sense a subtle psychic disturbance.",
            "SphynxSaltPsychicDisturbance",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "You fall downward!",
            "You fall downward!",
            "StairsDownFallDownward",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "the dog runs off to fetch {{Y|a salve tonic}}!",
            "the dog runs off to fetch {{Y|a salve tonic}}!",
            "FetchesRunsOffToFetch",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            "the goat eats {{Y|a salve tonic}}.",
            "the goat eats {{Y|a salve tonic}}.",
            "TonicVisibleConsume",
            expectedHits: 0);
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.CheckPullDown),
            "You avoid the chrome idol's pincers.",
            "You avoid the chrome idol's pincers.",
            "ThiefBotAvoidPincers",
            expectedHits: 0);
    }

    [Test]
    public void SingleCallsiteOwnerQueue_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "You now have 37 feet of copper wire.";

        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "WeirdwireCopperWireTotal",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You now have copper wire.")]
    [TestCase("A weird shock reverberates nearby.")]
    [TestCase("You fall upward!")]
    [TestCase("the chrome idol's force field passes through you harmlessly.")]
    public void SingleCallsiteOwnerQueue_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerQueuedMessage(
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent),
            source,
            source,
            "WeirdwireCopperWireTotal",
            expectedHits: 0);
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleFetchesEvent),
        "the dog sniffs the air.",
        "FetchesRunsOffToFetch")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent),
        "The tonic hums in your hand.",
        "TonicVisibleConsume")]
    public void SingleCallsiteOwnerQueue_DoesNotClaimDeferredRuntimeMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string detail)
    {
        AssertOwnerQueuedMessage(
            methodName,
            source,
            source,
            detail,
            expectedHits: 0);
    }

    private static void AssertOwnerQueuedMessage(
        string methodName,
        string source,
        string expected,
        string detail,
        string? color = null,
        int expectedHits = 1)
    {
        var ownerRoute = CreateOwnerRoute(methodName);
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, ownerRoute.Method);

            ownerRoute.Invoke(() => DummyMessageQueue.AddPlayerMessage(source, color, Capitalize: false));

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        var original = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo ownerMethod)
    {
        harmony.Patch(
            original: ownerMethod,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(SingleCallsiteOwnerQueueTranslationPatch),
                nameof(SingleCallsiteOwnerQueueTranslationPatch.Prefix),
                typeof(MethodBase))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(SingleCallsiteOwnerQueueTranslationPatch),
                nameof(SingleCallsiteOwnerQueueTranslationPatch.Finalizer),
                typeof(Exception))));
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

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(SingleCallsiteOwnerQueueTranslationPatch) + "." + detail);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static DynamicOwnerRouteMethod CreateOwnerRoute(string methodName)
    {
        return methodName switch
        {
            nameof(DummySingleCallsiteOwnerQueueTarget.TrySendCommandEventOnPlayer) => CreateOwnerRouteFromKey(ActivatedAbilityEntryOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.DisplaySurfaceDistribution) => CreateOwnerRouteFromKey(BiomeSurfaceDistributionOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.FireElevatorSwitchEvent) => CreateOwnerRouteFromKey(ElevatorSwitchOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleFetchesEvent) => CreateOwnerRouteFromKey(FetchesOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplyMorphicShock) => CreateOwnerRouteFromKey(ModMorphogeneticOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.FireMonochromeEvent) => CreateOwnerRouteFromKey(MonochromeOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.AttemptRebukeRobot) => CreateOwnerRouteFromKey(PersuasionRebukeRobotAttemptOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.PyroZoneStarted) => CreateOwnerRouteFromKey(PyroZoneStartedOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.PyroZoneStopped) => CreateOwnerRouteFromKey(PyroZoneStoppedOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.CryoZoneStarted) => CreateOwnerRouteFromKey(CryoZoneStartedOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.CryoZoneStopped) => CreateOwnerRouteFromKey(CryoZoneStoppedOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.FireSnapjawHowlEvent) => CreateOwnerRouteFromKey(SnapjawHowlOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.ApplySphynxSaltTonic) => CreateOwnerRouteFromKey(SphynxSaltTonicOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.CheckPullDown) => CreateOwnerRouteFromKey(StairsDownOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.FireThiefBotEvent) => CreateOwnerRouteFromKey(ThiefBotOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleTonicEvent) => CreateOwnerRouteFromKey(TonicHandleEventOwner),
            nameof(DummySingleCallsiteOwnerQueueTarget.HandleWeirdwireTookEvent) => CreateOwnerRouteFromKey(WeirdwireConduitOwner),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method."),
        };
    }

    private static DynamicOwnerRouteMethod CreateOwnerRouteFromKey(string ownerKey)
    {
        var separator = ownerKey.LastIndexOf('|');
        return DynamicOwnerRouteMethod.Create(ownerKey[..separator], ownerKey[(separator + 1)..]);
    }

    private static class DummySingleCallsiteOwnerQueueTarget
    {
        public static void TrySendCommandEventOnPlayer()
        {
        }

        public static void DisplaySurfaceDistribution()
        {
        }

        public static bool FireElevatorSwitchEvent() => true;

        public static bool HandleFetchesEvent() => true;

        public static bool ApplyMorphicShock() => true;

        public static bool FireMonochromeEvent() => true;

        public static bool AttemptRebukeRobot() => true;

        public static void PyroZoneStarted()
        {
        }

        public static void PyroZoneStopped()
        {
        }

        public static void CryoZoneStarted()
        {
        }

        public static void CryoZoneStopped()
        {
        }

        public static bool FireSnapjawHowlEvent() => true;

        public static bool ApplySphynxSaltTonic() => true;

        public static bool CheckPullDown() => true;

        public static bool FireThiefBotEvent() => true;

        public static bool HandleTonicEvent() => true;

        public static bool HandleWeirdwireTookEvent() => true;
    }
}
