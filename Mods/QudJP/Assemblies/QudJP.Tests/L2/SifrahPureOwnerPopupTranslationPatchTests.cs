using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SifrahPureOwnerPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.BaetylOfferingSifrah),
        "You have no usable options to employ for performing an offering to {{Y|神聖な石碑}}, giving you no chance of doing so well. You can remedy this situation by improving your Intelligence or by obtaining items useful in such a ritual.",
        "{{Y|神聖な石碑}}に捧げ物をするために使用できる選択肢がなく、うまく行う見込みがない。知性を高めるか、そのような儀式に役立つアイテムを入手すれば、この状況を改善できる。",
        "BaetylOffering")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.FormalWaterRitualSifrah),
        "You have no usable options to employ for performing the formal water ritual with {{C|水商人}}, giving you no chance of doing so well. You can remedy this situation by improving your Ego or by obtaining items useful in such a ritual.",
        "{{C|水商人}}との正式な水の儀式に使用できる選択肢がなく、うまく行う見込みがない。エゴを高めるか、そのような儀式に役立つアイテムを入手すれば、この状況を改善できる。",
        "FormalWaterRitual")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.HagglingSifrah),
        "You have no usable options to employ for haggling with {{G|商人}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations.",
        "{{G|商人}}と値段交渉するために使用できる選択肢がなく、成功する見込みがない。エゴや社交スキルを高めるか、社交的な状況に役立つアイテムを入手すれば、この状況を改善できる。",
        "Haggling")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ItemModdingSifrah),
        "You have no usable options to employ for modding {{W|古びたライフル}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "{{W|古びたライフル}}を改造するために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
        "ItemModding")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ItemNamingSifrah),
        "You have no usable options to employ for ritually naming {{M|折れた剣}}, giving you no chance of performing well. You can remedy this situation by improving your Ego, Willpower, and esoteric skills, or by obtaining items useful in ritual.",
        "{{M|折れた剣}}に儀式的に名付けるために使用できる選択肢がなく、うまく行う見込みがない。エゴ、意志力、秘教系スキルを高めるか、儀式に役立つアイテムを入手すれば、この状況を改善できる。",
        "ItemNaming")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ReverseEngineeringSifrah),
        "You have no usable options to employ for reverse engineering {{Y|奇妙な小物}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "{{Y|奇妙な小物}}をリバースエンジニアリングするために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
        "ReverseEngineering")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ReverseEngineeringCheckEarlyExit),
        "Exiting will still disassemble {{Y|奇妙な小物}}, and will result in an attempt at reverse engineering as matters stand. Do you still want to exit?",
        "終了しても{{Y|奇妙な小物}}は分解され、現状のままリバースエンジニアリングを試みることになる。それでも終了する？",
        "ReverseEngineeringEarlyExit")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RitualAttributeSacrificeCheckTokenUse),
        "Your Strength is too depleted to do that.",
        "Strengthが消耗しすぎているため、それはできない。",
        "AttributeSacrifice")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RitualInvokeHigherBeingCheckTokenUse),
        "You have blasphemed against {{M|Oboroqoru}}.",
        "{{M|Oboroqoru}}に冒涜を働いた。",
        "InvokeHigherBeing")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.SocialSecretCheckTokenUse),
        "You do not have any more secrets {{G|Argyve}} is interested in.",
        "{{G|Argyve}}が興味を持つ秘密を持っていない。",
        "SocialSecret")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.TinkeringBitCheckTokenUse),
        "You do not have any more {{R|scrap bits}}.",
        "{{R|scrap bits}}を持っていない。",
        "TinkeringBit")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.TinkeringChargeCheckTokenUse),
        "You do not have any energy cells with 500 charge available, and your electrical generation capacity is unable to meet the demand.",
        "500チャージのあるエネルギーセルを持っておらず、発電能力でも需要を満たせない。",
        "TinkeringChargeWithGeneration")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.TinkeringChargeCheckTokenUse),
        "You do not have any energy cells with 500 charge available.",
        "500チャージのあるエネルギーセルを持っていない。",
        "TinkeringCharge")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.TinkeringComputePowerCheckTokenUse),
        "You do not have 1 unit of compute power available on the local lattice.",
        "ローカル格子上に利用可能な計算能力が1ユニットない。",
        "TinkeringComputePower")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.TinkeringLiquidCheckTokenUse),
        "You do not have any {{B|brain brine}}.",
        "{{B|brain brine}}を持っていない。",
        "TinkeringLiquid")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.SifrahGameMakeMoveForSlot),
        "You have already chosen the correct option for {{C|glyph sequence}}.",
        "{{C|glyph sequence}}にはすでに正しい選択肢を選んでいる。",
        "MakeMoveForSlotChosenCorrect")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.SifrahGameMakeMoveForSlot),
        "You have already eliminated {{R|rusted key}} as a possibility.",
        "{{R|rusted key}}はすでに可能性から除外している。",
        "MakeMoveForSlotEliminated")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.SifrahGameMakeMoveForSlot),
        "Choosing {{Y|phase prism}} is disabled for this turn.",
        "{{Y|phase prism}}を選ぶことはこのターン無効化されている。",
        "MakeMoveForSlotDisabled")]
    public void Patch_TranslatesSifrahPureOwnerPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahPureOwnerPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummySifrahPureOwnerPopupProducerTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslateSifrahPureOwnerPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string source = "You have already chosen the correct option for {{C|glyph sequence}}.";
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("MakeMoveForSlotChosenCorrect"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_StripsDirectMarkerFromSifrahPureOwnerPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string unmarked = "You have already chosen the correct option for {{C|glyph sequence}}.";
                var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(HitCount("MakeMoveForSlotChosenCorrect"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahPureOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySifrahPureOwnerPopupProducerTarget.HagglingSifrah)),
            () =>
            {
                var source = MessageFrameTranslator.MarkDirectTranslation("You have no usable options to employ for haggling with {{G|商人}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations.");
                var target = new DummySifrahPureOwnerPopupProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.HagglingSifrah(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You have no usable options to employ for haggling with {{G|商人}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations."));
                    Assert.That(HitCount("Haggling"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_RestoresOuterOwnerContext_AfterNestedOwnerPopup()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwners(
            typeof(SifrahPureOwnerPopupTranslationPatch),
            [
                RequireOwnerMethod(nameof(DummySifrahPureOwnerPopupProducerTarget.HagglingSifrah)),
                RequireOwnerMethod(nameof(DummySifrahPureOwnerPopupProducerTarget.SifrahGameMakeMoveForSlot)),
            ],
            () =>
            {
                var target = new DummySifrahPureOwnerPopupProducerTarget
                {
                    InvokeMakeMoveBeforeHagglingPopup = true,
                    NestedPopupMessageToShow = "You have already chosen the correct option for {{C|glyph sequence}}.",
                    PopupMessageToShow = "You have no usable options to employ for haggling with {{G|商人}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations.",
                };

                target.HagglingSifrah(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{G|商人}}と値段交渉するために使用できる選択肢がなく、成功する見込みがない。エゴや社交スキルを高めるか、社交的な状況に役立つアイテムを入手すれば、この状況を改善できる。"));
                    Assert.That(HitCount("MakeMoveForSlotChosenCorrect"), Is.EqualTo(1));
                    Assert.That(HitCount("Haggling"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahPureOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySifrahPureOwnerPopupProducerTarget.HagglingSifrah)),
            () =>
            {
                var target = new DummySifrahPureOwnerPopupProducerTarget();

                target.HagglingSifrah(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("Haggling"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummySifrahPureOwnerPopupProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummySifrahPureOwnerPopupProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, [new DummyGameObject()]);
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(SifrahPureOwnerPopupTranslationPatch), detail);
    }
}
