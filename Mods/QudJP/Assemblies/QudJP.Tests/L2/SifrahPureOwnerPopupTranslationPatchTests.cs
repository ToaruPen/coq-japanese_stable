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
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(GetLocalizationRoot(), "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        MessagePatternTranslator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
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
        nameof(DummySifrahPureOwnerPopupProducerTarget.DisarmingSifrah),
        "You have no usable options to employ for disarming {{R|罠}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "{{R|罠}}を解除するために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
        "Disarming")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ExamineSifrah),
        "You have no usable options to employ for examining {{Y|謎めいた装置}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "{{Y|謎めいた装置}}を調査するために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
        "Examine")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.HackingSifrah),
        "You have no usable options to employ for hacking {{C|古い端末}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "{{C|古い端末}}をハッキングするために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
        "Hacking")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ProselytizationSifrah),
        "You have no usable options to employ for proselytizing {{Y|砂漠の隠者}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations.",
        "{{Y|砂漠の隠者}}を布教するために使用できる選択肢がなく、成功する見込みがない。エゴや社交スキルを高めるか、社交的な状況に役立つアイテムを入手すれば、この状況を改善できる。",
        "Proselytization")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RebukingSifrah),
        "You have no usable options to employ for rebuking {{C|眠れる機械}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, by implanting appropriate cybernetics, or by obtaining items useful in social situations.",
        "{{C|眠れる機械}}を叱責するために使用できる選択肢がなく、成功する見込みがない。エゴや社交スキルを高めるか、適切なサイバネティクスを埋め込むか、社交的な状況に役立つアイテムを入手すれば、この状況を改善できる。",
        "Rebuking")]
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
        nameof(DummySifrahPureOwnerPopupProducerTarget.RepairSifrah),
        "You have no usable options to employ for repairing {{W|壊れたタレット}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "{{W|壊れたタレット}}を修理するために使用できる選択肢がなく、成功する見込みがない。知性や工作スキルを高めるか、工作に役立つアイテムを入手すれば、この状況を改善できる。",
        "Repair")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.PsychicCombatSifrah),
        "You have no usable options to employ for {{M|精神戦}}, giving you no chance of performing well. You can remedy this situation by improving your Ego, Willpower, Intelligence, and esoteric skills.",
        "{{M|精神戦}}に使用できる選択肢がなく、うまく行う見込みがない。エゴ、意志力、知性、秘教系スキルを高めれば、この状況を改善できる。",
        "PsychicCombat")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RealityDistortionSifrah),
        "You have no usable options to employ for {{Y|現実の歪曲}}, giving you no chance of performing well. You can remedy this situation by improving your Ego, Willpower, Intelligence, and esoteric skills.",
        "{{Y|現実の歪曲}}に使用できる選択肢がなく、うまく行う見込みがない。エゴ、意志力、知性、秘教系スキルを高めれば、この状況を改善できる。",
        "RealityDistortion")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ReverseEngineeringCheckEarlyExit),
        "Exiting will still disassemble {{Y|奇妙な小物}}, and will result in an attempt at reverse engineering as matters stand. Do you still want to exit?",
        "終了しても{{Y|奇妙な小物}}は分解され、現状のままリバースエンジニアリングを試みることになる。それでも終了する？",
        "ReverseEngineeringEarlyExit")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ReverseEngineeringFinish),
        "You fail to reverse engineer {{Y|奇妙な小物}}.",
        "{{Y|奇妙な小物}}のリバースエンジニアリングに失敗した。",
        "ReverseEngineeringFinish")]
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
                    PopupMethod = PopupMethodForPureOwnerPopup(methodName),
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

    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.DisarmingSifrah),
        "You have mastered disarming operations of this complexity. Do you want to perform detailed disarming anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically succeed.",
        "この難度での解除作業は熟達済みだ。それでも詳細に解除を試みるか？『いいえ』なら、自動で成功する。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ExamineSifrah),
        "You have mastered examining artifacts of this complexity. Do you want to perform detailed examination anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically succeed at the examination.",
        "この難度での遺物調査は熟達済みだ。それでも詳細に遺物調査を試みるか？『いいえ』なら、可能なら自動で成功する。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.HackingSifrah),
        "You have mastered hacks of this complexity. Do you want to perform a detailed hack anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically succeed at the hack.",
        "この難度でのハッキングは熟達済みだ。それでも詳細なハッキングを試みるか？『いいえ』なら、可能なら自動で成功する。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.ProselytizationSifrah),
        "You have mastered proselytization at this level of discourse. Do you want to perform detailed proselytization anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically succeed at proselytization if that is possible.",
        "このレベルの布教は熟達済みだ。それでも詳細な布教を試みるか？『いいえ』なら、可能なら布教が自動で成功する。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RebukingSifrah),
        "You have mastered rebuking robots of this grade. Do you want to perform a detailed rebuke anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically succeed t the rebuke if that is possible.",
        "この難度でのロボット叱責は熟達済みだ。それでも詳細な叱責を試みるか？『いいえ』なら、可能なら叱責が自動で成功する。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RepairSifrah),
        "You have mastered repairs of this complexity. Do you want to perform detailed repairs anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically succeed at the repairs.",
        "この難度での修理は熟達済みだ。それでも詳細な修理を試みるか？『いいえ』なら、可能なら修理が自動で成功する。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.PsychicCombatSifrah),
        "You have mastered psychic combat at this level of difficulty. Do you want to guide the process in detail anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically receive the results of strong but unexceptional performance.",
        "この難度での精神戦闘は習熟済みだ。卓越した成功率を狙って詳細な手順を自ら指揮する？「いいえ」を選ぶと、平凡でも堅実な結果が自動的に得られる。")]
    [TestCase(
        nameof(DummySifrahPureOwnerPopupProducerTarget.RealityDistortionSifrah),
        "You have mastered reality distortion at this level of difficulty. Do you want to guide the process in detail anyway, with an enhanced chance of exceptional success? If you answer 'No', you will automatically receive the results of strong but unexceptional performance.",
        "この難度での現実歪曲は熟達済みだ。それでも詳細に制御するか？『いいえ』なら、強いが平凡な結果を自動で得る。")]
    public void Patch_TranslatesMasteredPrompt_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahPureOwnerPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummySifrahPureOwnerPopupProducerTarget
                {
                    PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
            });
    }

    [Test]
    public void Patch_DoesNotTranslateSifrahPureOwnerPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string source = "You have already chosen the correct option for {{C|glyph sequence}}.";
                DummyPopupShow.ShowFail(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("MakeMoveForSlotChosenCorrect"), Is.Zero);
                });
            });
    }

    [TestCase(
        "You have no usable options to employ for disarming {{R|罠}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "Disarming")]
    [TestCase(
        "You have no usable options to employ for hacking {{C|古い端末}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "Hacking")]
    [TestCase(
        "You have no usable options to employ for examining {{Y|謎めいた装置}}, giving you no chance of success. You can remedy this situation by improving your Intelligence and tinkering skills, or by obtaining items useful for tinkering.",
        "Examine")]
    [TestCase(
        "You have no usable options to employ for proselytizing {{Y|砂漠の隠者}}, giving you no chance of success. You can remedy this situation by improving your Ego and social skills, or by obtaining items useful in social situations.",
        "Proselytization")]
    [TestCase(
        "You have no usable options to employ for {{M|精神戦}}, giving you no chance of performing well. You can remedy this situation by improving your Ego, Willpower, Intelligence, and esoteric skills.",
        "PsychicCombat")]
    [TestCase(
        "You have no usable options to employ for {{Y|現実の歪曲}}, giving you no chance of performing well. You can remedy this situation by improving your Ego, Willpower, Intelligence, and esoteric skills.",
        "RealityDistortion")]
    public void Patch_DoesNotTranslateConstructorOwnerPopup_WhenOwnerAbsent(string source, string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                DummyPopupShow.ShowFail(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount(detail), Is.Zero);
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
    public void Patch_DoesNotClaimReverseEngineeringCriticalFailurePopup_WhenFinishOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahPureOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySifrahPureOwnerPopupProducerTarget.ReverseEngineeringFinish)),
            () =>
            {
                const string source = "You think you've made a terrible mistake...";
                const string expected = "とんでもない間違いをした気がする...";
                var target = new DummySifrahPureOwnerPopupProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ReverseEngineeringFinish(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount("ReverseEngineeringFinish"), Is.Zero);
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

    private static string PopupMethodForPureOwnerPopup(string methodName)
    {
        return methodName is nameof(DummySifrahPureOwnerPopupProducerTarget.ProselytizationSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.DisarmingSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.ExamineSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.HackingSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.RebukingSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.RepairSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.PsychicCombatSifrah)
            or nameof(DummySifrahPureOwnerPopupProducerTarget.RealityDistortionSifrah)
            ? nameof(DummyPopupShow.ShowFail)
            : nameof(DummyPopupShow.Show);
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }
}
