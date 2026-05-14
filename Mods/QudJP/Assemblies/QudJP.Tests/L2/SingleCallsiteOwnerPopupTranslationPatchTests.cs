using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SingleCallsiteOwnerPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));
        DummyPopupShow.Reset();
        DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms),
        "That is out of range (3 squares)",
        "範囲外だ（3マス）。",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms),
        "That is out of range (1 square)",
        "範囲外だ（1マス）。",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish),
        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
        "{{C|oil}}の報酬として{{Y|folded carbide axe}}を生成した。",
        "BaetylRewardWish",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess),
        "Are you sure you want to dismember yourself?",
        "yourselfを切断してもよいか？",
        "AxeDismemberSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "Are you sure you want to slam yourself?",
        "yourselfを叩きつけてもよいか？",
        "CudgelSlamSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization),
        "Argyve is already your follower. Do you want to proselytize him anyway?",
        "Argyveはすでにあなたの仲間だ。それでも勧誘するか？",
        "ProselytizeFollowerConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe),
        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
        "ひらめきを得て{{Y|laser pistol schematic}}を記した。",
        "TinkeringLearnRecipe",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated),
        "Argyve (NPC_Argyve) is considered unique, are you sure you want to create another?",
        "Argyve（NPC_Argyve）は一意とみなされています。もう1つ作成しますか？",
        "GameUniqueWishConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio),
        "You activate {{Y|the chrome idol}} and toss it into the air.",
        "{{Y|the chrome idol}}を起動して空中に放り投げた。",
        "GenocideCurioActivation",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal),
        "The mainframe is unresponsive.",
        "The mainframeは反応しない。",
        "GritGateMainframeUnresponsive",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc),
        "The death of Kesehind means that the investigation can go no further.",
        "Kesehindの死により、調査はこれ以上進められなくなった。",
        "HindrenMysteryCriticalNpcDeath",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant),
        "Your flamethrower has consumed all of its oil.",
        "あなたのflamethrowerはits oilをすべて消費した。",
        "LiquidFueledPowerPlantEmpty",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss),
        "You have found {{Y|Kindrish}}!",
        "{{Y|Kindrish}}を見つけた！",
        "MakeFussOnTaken",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat),
        "Your genome destabilizes and you gain 1 mutation point.",
        "ゲノムが不安定化し、変異ポイントを1得た。",
        "MutationPointsOnEat",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat),
        "Your genome destabilizes and you gain 3 mutation points.",
        "ゲノムが不安定化し、変異ポイントを3得た。",
        "MutationPointsOnEat",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant),
        "Your fuel cells have consumed all of their blood.",
        "あなたのfuel cellsはtheir bloodをすべて消費した。",
        "LiquidFueledPowerPlantEmpty",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath),
        "Just before your demise, you are transported to safety! {{Y|recoiler}} disintegrates.",
        "死の直前、あなたは安全な場所へ転送された！ {{Y|recoiler}}は崩壊した。",
        "RecoilOnDeathTransport",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath),
        "Just before your demise, you are transported to safety! {{Y|recoilers}} disintegrate.",
        "死の直前、あなたは安全な場所へ転送された！ {{Y|recoilers}}は崩壊した。",
        "RecoilOnDeathTransport",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle),
        "you are covered in slime!",
        "youはslimeに覆われた！",
        "SpraybottleCovered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle),
        "{{Y|the snapjaw}} is covered in oil!",
        "{{Y|the snapjaw}}はoilに覆われた！",
        "SpraybottleCovered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord),
        "You bothered {{G|Yurl}} again.",
        "{{G|Yurl}}にまた迷惑をかけた。",
        "WaterRitualRecordBothered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax),
        "The infected crust of skin on your third arm loosens and breaks away.",
        "あなたのthird armの感染した皮殻が緩み、剥がれ落ちた。",
        "SpreadPaxCure",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook),
        "Your Strength is increased by {{G|1}}!",
        "あなたのStrengthが{{G|1}}上昇した！",
        "TrainingBookAttributeIncrease",
        PopupMethod.Show)]
    public void Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                InvokeOwnerMethod(methodName, source);

                Assert.Multiple(() =>
                {
                    Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "That is out of range (3 squares)",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
        "BaetylRewardWish",
        PopupMethod.Show)]
    [TestCase(
        "Are you sure you want to dismember yourself?",
        "AxeDismemberSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "Are you sure you want to slam yourself?",
        "CudgelSlamSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "Argyve is already your follower. Do you want to proselytize him anyway?",
        "ProselytizeFollowerConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
        "TinkeringLearnRecipe",
        PopupMethod.Show)]
    [TestCase(
        "Argyve (NPC_Argyve) is considered unique, are you sure you want to create another?",
        "GameUniqueWishConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "You activate {{Y|the chrome idol}} and toss it into the air.",
        "GenocideCurioActivation",
        PopupMethod.Show)]
    [TestCase(
        "The mainframe is unresponsive.",
        "GritGateMainframeUnresponsive",
        PopupMethod.Show)]
    [TestCase(
        "The death of Kesehind means that the investigation can go no further.",
        "HindrenMysteryCriticalNpcDeath",
        PopupMethod.Show)]
    [TestCase(
        "Your flamethrower has consumed all of its oil.",
        "LiquidFueledPowerPlantEmpty",
        PopupMethod.Show)]
    [TestCase(
        "You have found {{Y|Kindrish}}!",
        "MakeFussOnTaken",
        PopupMethod.Show)]
    [TestCase(
        "Your genome destabilizes and you gain 3 mutation points.",
        "MutationPointsOnEat",
        PopupMethod.Show)]
    [TestCase(
        "Just before your demise, you are transported to safety! {{Y|recoiler}} disintegrates.",
        "RecoilOnDeathTransport",
        PopupMethod.Show)]
    [TestCase(
        "you are covered in slime!",
        "SpraybottleCovered",
        PopupMethod.Show)]
    [TestCase(
        "You bothered {{G|Yurl}} again.",
        "WaterRitualRecordBothered",
        PopupMethod.Show)]
    [TestCase(
        "The infected crust of skin on your third arm loosens and breaks away.",
        "SpreadPaxCure",
        PopupMethod.Show)]
    [TestCase(
        "Your Strength is increased by {{G|1}}!",
        "TrainingBookAttributeIncrease",
        PopupMethod.Show)]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent(
        string source,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => ShowPopup(source, popupMethod));

        Assert.Multiple(() =>
        {
            Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [TestCase(
        "Argyve (NPC_Argyve) is considered unique, are you sure you want to create another?",
        "GameUniqueWishConfirmation")]
    [TestCase(
        "You activate {{Y|the chrome idol}} and toss it into the air.",
        "GenocideCurioActivation")]
    [TestCase(
        "The mainframe is unresponsive.",
        "GritGateMainframeUnresponsive")]
    [TestCase(
        "The death of Kesehind means that the investigation can go no further.",
        "HindrenMysteryCriticalNpcDeath")]
    [TestCase(
        "Your flamethrower has consumed all of its oil.",
        "LiquidFueledPowerPlantEmpty")]
    [TestCase(
        "You have found {{Y|Kindrish}}!",
        "MakeFussOnTaken")]
    [TestCase(
        "Your genome destabilizes and you gain 3 mutation points.",
        "MutationPointsOnEat")]
    [TestCase(
        "Just before your demise, you are transported to safety! {{Y|recoiler}} disintegrates.",
        "RecoilOnDeathTransport")]
    [TestCase(
        "you are covered in slime!",
        "SpraybottleCovered")]
    [TestCase(
        "You bothered {{G|Yurl}} again.",
        "WaterRitualRecordBothered")]
    [TestCase(
        "The infected crust of skin on your third arm loosens and breaks away.",
        "SpreadPaxCure")]
    [TestCase(
        "Your Strength is increased by {{G|1}}!",
        "TrainingBookAttributeIncrease")]
    public void Patch_DoesNotTranslatePopupUnderWrongSingleCallsiteOwner(
        string source,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount(detail), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish), marked);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("BaetylRewardWish"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), string.Empty);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("DecoyHologramOutOfRange"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyAxeDismember),
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.Cast) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyCudgelSlam),
                    typeof(string),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(int),
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(int),
                    typeof(int)),
            nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyInventoryActionEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyBeforeDeathRemovalEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyEndTurnEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyBeforeDieEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyBeginConversationEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method."),
        };
    }

    private static void InvokeOwnerMethod(string methodName, string message)
    {
        switch (methodName)
        {
            case nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.CreateHolograms(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish("@Melee Weapons {tier}R");
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.CastForceSuccess(
                    new DummyGameObject(),
                    new DummyAxeDismember(),
                    new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.Cast):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.Cast(
                    new DummyGameObject(),
                    new DummyCudgelSlam(),
                    null,
                    new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.AttemptProselytization();
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe(new DummyGameObject(), 1, 4);
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.OnCreated("Wish");
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleGenocideCurio(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleGritGateMainframeTerminal(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleHindrenMysteryCriticalNpc(new DummyBeforeDeathRemovalEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleLiquidFueledPowerPlant(new DummyEndTurnEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.MakeFuss(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.FireMutationPointsOnEat(new DummyEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleRecoilOnDeath(new DummyBeforeDieEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleSpraybottle(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleTrainingBook(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleWaterRitualRecord(new DummyBeginConversationEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.FinishSpreadPax();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method.");
        }
    }

    private static void ShowPopup(string source, PopupMethod popupMethod)
    {
        if (popupMethod == PopupMethod.ShowYesNo)
        {
            _ = DummyPopupShow.ShowYesNo(source);
            return;
        }

        DummyPopupShow.Show(source);
    }

    private static string? GetLastPopupMessage(PopupMethod popupMethod)
    {
        return popupMethod == PopupMethod.ShowYesNo
            ? DummyPopupShow.LastShowYesNoMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(SingleCallsiteOwnerPopupTranslationPatch), detail);
    }

    public enum PopupMethod
    {
        Show,
        ShowYesNo,
    }
}
