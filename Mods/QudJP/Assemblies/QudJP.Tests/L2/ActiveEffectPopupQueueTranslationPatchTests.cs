using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActiveEffectPopupQueueTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        ResetState();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.IrisdualCallowApply),
        "your rind softens while you recrystallize!",
        "あなたの外皮が柔らかくなり、あなたは再結晶化した！",
        "IrisdualCallowRindSoftens")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.IrisdualCallowApply),
        "the snapjaw's rind softens while it recrystallizes!",
        "snapjawの外皮が柔らかくなり、それは再結晶化した！",
        "IrisdualCallowRindSoftens")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ThreeTonguesApply),
        "A trio of tongues vegetate from your face!",
        "3本の舌があなたの顔から生え出た！",
        "ThreeTonguesVegetate")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ThreeTonguesApply),
        "A trio of tongues vegetate from the snapjaw's face!",
        "3本の舌がsnapjawの顔から生え出た！",
        "ThreeTonguesVegetate")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.HobbledApply),
        "you are hobbled!",
        "あなたは足を引きずっている！",
        "HobbledApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.TerrifiedApply),
        "you are overwhelmed with terror!",
        "あなたは恐怖に圧倒された！",
        "TerrifiedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.GeometricHealApply),
        "you begin healing.",
        "あなたは回復を始めた。",
        "GeometricHealApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.TranceApply),
        "you enter a trance!",
        "あなたはトランス状態に入った！",
        "TranceApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.StingerPoisonedApply),
        "you have been poisoned!",
        "あなたは毒を受けた！",
        "StingerPoisonedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.FuriouslyConfusedApply),
        "you become confused!",
        "あなたは混乱した！",
        "FuriouslyConfusedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ConfusedApply),
        "you become confused!",
        "あなたは混乱した！",
        "FuriouslyConfusedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.PoisonedApply),
        "you have been poisoned!",
        "あなたは毒を受けた！",
        "StingerPoisonedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.PhasePoisonedApply),
        "you have been poisoned!",
        "あなたは毒を受けた！",
        "StingerPoisonedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.HealingApply),
        "you begin healing.",
        "あなたは回復を始めた。",
        "GeometricHealApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.DazedApply),
        "you are dazed.",
        "あなたは朦朧としている。",
        "DazedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ParalyzedApply),
        "you are paralyzed!",
        "あなたは麻痺している！",
        "ParalyzedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.PoisonedFireEvent),
        "you are no longer poisoned!",
        "あなたはもう毒を受けていない！",
        "NoLongerPoisonedFireEvent")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.PhasePoisonedFireEvent),
        "The snapjaw is no longer poisoned!",
        "snapjawはもう毒を受けていない！",
        "NoLongerPoisonedFireEvent")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.BasiliskPoisonFireEvent),
        "you feel less stiff.",
        "あなたは体の硬さがほぐれた。",
        "BasiliskPoisonLessStiff")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.CrippleFireEvent),
        "you are no longer crippled!",
        "あなたは損傷から回復した！",
        "NoLongerCrippledFireEvent")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.PoisonGasPoisonFireEvent),
        "you are no longer poisoned!",
        "あなたはもう毒を受けていない！",
        "NoLongerPoisonedFireEvent")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.AshPoisonFireEvent),
        "you are no longer choking!",
        "あなたは窒息から回復した！",
        "NoLongerChokingFireEvent")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.LuminousApply),
        "you start to glow.",
        "あなたは輝き始めた。",
        "LuminousApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.MeditatingApply),
        "you begin meditating.",
        "あなたは瞑想を始めた。",
        "MeditatingApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ScintillatingApply),
        "you start scintillating in {{rainbow|prismatic hues}}!",
        "あなたは{{rainbow|虹色の色彩}}できらめき始めた！",
        "ScintillatingApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.SuppressedApply),
        "you are suppressed!",
        "あなたは制圧された！",
        "SuppressedApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ShadeOilApply),
        "you begin to flicker in and out of corporeality.",
        "あなたは実体と非実体の間で揺らぎ始めた。",
        "ShadeOilApply")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.AsleepRemove),
        "you wake up.",
        "あなたは目を覚ました。",
        "AsleepWakeUp")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.AsleepRemove),
        "you exit sleep mode.",
        "あなたはスリープモードを終了した。",
        "AsleepExitSleepMode")]
    public void OwnerPatch_TranslatesQueuedMessages_WhenActiveEffectOwnerIsPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedQueueOwner(ownerMethodName, () =>
        {
            var target = new DummyActiveEffectPopupQueueOwner(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("\u0001" + expected));
                Assert.That(HitCount("Queue", detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.ShadeOilWorldMapFireEvent),
        "You cannot do that on the world map.",
        "ワールドマップではそれはできない。",
        "ShadeOilWorldMap")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.BrainBrineCurseFireEvent),
        "You shake the water from your addled brain, but someone else's thoughts have already taken root.",
        "混乱した脳から水を振り払ったが、すでに誰か別の思考が根を下ろしている。",
        "BrainBrineCurseRootedThoughts")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.SphynxSaltTonicApply),
        "The clouds part in your mind and a ray of clarity strikes through.",
        "心の中で雲が割れ、明晰さの光が差し込む。",
        "SphynxSaltClarity")]
    [TestCase(
        nameof(DummyActiveEffectPopupQueueOwner.PoisonedPopupApply),
        "you have been poisoned!",
        "あなたは毒を受けた！",
        "StingerPoisonedApply")]
    public void OwnerPatch_TranslatesShowPopupMessages_WhenActiveEffectOwnerIsPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedShowOwner(ownerMethodName, () =>
        {
            var target = new DummyActiveEffectPopupQueueOwner(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("Popup", detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "Shade oil has been applied. Do you wish to phase out immediately?",
        "シェードオイルが適用された。すぐに位相をずらす？")]
    [TestCase(
        "Shade oil has been applied by your injector. Do you wish to phase out immediately?",
        "シェードオイルがあなたのinjectorによって適用された。すぐに位相をずらす？")]
    public void OwnerPatch_TranslatesShadeOilShowYesNoPrompt_WhenActiveEffectOwnerIsPatched(
        string source,
        string expected)
    {
        WithPatchedShowYesNoOwner(nameof(DummyActiveEffectPopupQueueOwner.ShadeOilPromptFireEvent), () =>
        {
            var target = new DummyActiveEffectPopupQueueOwner(source);

            target.ShadeOilPromptFireEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                Assert.That(HitCount("Popup", "ShadeOilPhasePrompt"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerPatch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = "qudjp.tests.active-effect-popup-queue.absent." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("your rind softens while you recrystallize!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("your rind softens while you recrystallize!"));
                Assert.That(HitCount("Queue", "IrisdualCallowRindSoftens"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedQueueOwner(string ownerMethodName, Action action)
    {
        WithPatchedOwner(ownerMethodName, PatchQueue, action);
    }

    private static void WithPatchedShowOwner(string ownerMethodName, Action action)
    {
        WithPatchedOwner(ownerMethodName, PatchPopupShow, action);
    }

    private static void WithPatchedShowYesNoOwner(string ownerMethodName, Action action)
    {
        WithPatchedOwner(ownerMethodName, PatchPopupShowYesNo, action);
    }

    private static void WithPatchedOwner(string ownerMethodName, Action<Harmony> patchSink, Action action)
    {
        var harmonyId = "qudjp.tests.active-effect-popup-queue." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            patchSink(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyActiveEffectPopupQueueOwner), ownerMethodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(ActiveEffectPopupQueueTranslationPatch), nameof(ActiveEffectPopupQueueTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(ActiveEffectPopupQueueTranslationPatch), nameof(ActiveEffectPopupQueueTranslationPatch.Finalizer), typeof(Exception))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequirePopupShowPrefix()));
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowYesNo),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int)),
            prefix: new HarmonyMethod(RequirePopupShowPrefix()));
    }

    private static void InvokeOwner(DummyActiveEffectPopupQueueOwner target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummyActiveEffectPopupQueueOwner), ownerMethodName).Invoke(target, null);
    }

    private static int HitCount(string sink, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            sink == "Queue" ? "MessageQueue.AddPlayerMessage" : nameof(PopupShowTranslationPatch),
            ActiveEffectPopupQueueTranslationPatch.Family + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static MethodInfo RequirePopupShowPrefix()
    {
        return RequireMethod(
            typeof(PopupShowTranslationPatch),
            nameof(PopupShowTranslationPatch.Prefix),
            typeof(string).MakeByRefType(),
            typeof(MethodBase));
    }

    private static void ResetState()
    {
        DynamicTextObservability.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.LastShowMessage = null;
        DummyPopupShow.LastShowYesNoMessage = null;
    }
}

internal sealed class DummyActiveEffectPopupQueueOwner
{
    private readonly string source;

    public DummyActiveEffectPopupQueueOwner(string source)
    {
        this.source = source;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool IrisdualCallowApply()
    {
        DummyMessageQueue.AddPlayerMessage(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ThreeTonguesApply()
    {
        DummyMessageQueue.AddPlayerMessage(source);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HobbledApply()
    {
        return SendQueuedMessage(nameof(HobbledApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TerrifiedApply()
    {
        return SendQueuedMessage(nameof(TerrifiedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool GeometricHealApply()
    {
        return SendQueuedMessage(nameof(GeometricHealApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TranceApply()
    {
        return SendQueuedMessage(nameof(TranceApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool StingerPoisonedApply()
    {
        return SendQueuedMessage(nameof(StingerPoisonedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool FuriouslyConfusedApply()
    {
        return SendQueuedMessage(nameof(FuriouslyConfusedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ConfusedApply()
    {
        return SendQueuedMessage(nameof(ConfusedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PoisonedApply()
    {
        return SendQueuedMessage(nameof(PoisonedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PhasePoisonedApply()
    {
        return SendQueuedMessage(nameof(PhasePoisonedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HealingApply()
    {
        return SendQueuedMessage(nameof(HealingApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool DazedApply()
    {
        return SendQueuedMessage(nameof(DazedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ParalyzedApply()
    {
        return SendQueuedMessage(nameof(ParalyzedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PoisonedFireEvent()
    {
        return SendQueuedMessage(nameof(PoisonedFireEvent));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PhasePoisonedFireEvent()
    {
        return SendQueuedMessage(nameof(PhasePoisonedFireEvent));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool BasiliskPoisonFireEvent()
    {
        return SendQueuedMessage(nameof(BasiliskPoisonFireEvent));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool CrippleFireEvent()
    {
        return SendQueuedMessage(nameof(CrippleFireEvent));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PoisonGasPoisonFireEvent()
    {
        return SendQueuedMessage(nameof(PoisonGasPoisonFireEvent));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool AshPoisonFireEvent()
    {
        return SendQueuedMessage(nameof(AshPoisonFireEvent));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool LuminousApply()
    {
        return SendQueuedMessage(nameof(LuminousApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool MeditatingApply()
    {
        return SendQueuedMessage(nameof(MeditatingApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ScintillatingApply()
    {
        return SendQueuedMessage(nameof(ScintillatingApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool SuppressedApply()
    {
        return SendQueuedMessage(nameof(SuppressedApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ShadeOilApply()
    {
        return SendQueuedMessage(nameof(ShadeOilApply));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AsleepRemove()
    {
        DummyMessageQueue.AddPlayerMessage(source);
    }

    private bool SendQueuedMessage(string owner)
    {
        _ = owner;
        DummyMessageQueue.AddPlayerMessage(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ShadeOilWorldMapFireEvent()
    {
        ShowPopup(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ShadeOilPromptFireEvent()
    {
        _ = DummyPopupShow.ShowYesNo(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool BrainBrineCurseFireEvent()
    {
        var message = source;
        DummyPopupShow.Show(message);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool SphynxSaltTonicApply()
    {
        ShowOwnPopup();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PoisonedPopupApply()
    {
        _ = nameof(PoisonedPopupApply);
        ShowOwnPopup();
        return true;
    }

    private static void ShowPopup(string message)
    {
        DummyPopupShow.Show(message);
    }

    private void ShowOwnPopup()
    {
        DummyPopupShow.Show(source);
    }
}
