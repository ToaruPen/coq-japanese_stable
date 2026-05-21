using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CombatAndLogMessageQueuePatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-combat-log-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        QuestLifecyclePopupTranslationPatch.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        MessagePatternTranslator.InvalidatePatternFileCacheForTests(patternFilePath);
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        DummyPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        QuestLifecyclePopupTranslationPatch.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void PhysicsApplyDischarge_TranslatesMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^(?:An|The) electrical arc leaps from (?:the |a |an )?(.+?) toward (.+?)!$", "電弧が{0}から{1}へ走った！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyPhysicsApplyDischargeTarget),
                    nameof(DummyPhysicsApplyDischargeTarget.ApplyDischarge),
                    typeof(DummyCell),
                    typeof(DummyCell),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    typeof(object),
                    typeof(DummyGameObject),
                    typeof(List<DummyCell>),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(List<DummyGameObject>),
                    typeof(bool?),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(bool)),
                typeof(PhysicsApplyDischargeTranslationPatch));

            var target = new DummyPhysicsApplyDischargeTarget
            {
                MessageToSend = "An {{electrical|electrical arc}} leaps from a chrome turret toward you!",
            };

            target.ApplyDischarge(new DummyCell(), new DummyCell(), 3);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{electrical|電弧}}がchrome turretからyouへ走った！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PhysicsProcessTakeDamage_TranslatesDamageFrames_WhenOwnerPatched()
    {
        var cases = new (string Source, string Expected)[]
        {
            ("{{r|You take 7 damage from the acid!}}", "{{r|酸で7ダメージを受けた！}}"),
            ("{{r|You take 7 damage from the acid}}", "{{r|酸で7ダメージを受けた}}"),
            ("{{r|You take 7 damage while phased!}}", "{{r|You take 7 damage while phased!}}"),
            ("{{r|You take no damage from the acid!}}", "{{r|酸でダメージを受けなかった！}}"),
            ("The snapjaw takes 4 damage from the acid!", "snapjawは酸で4ダメージを受けた！"),
            ("The snapjaw takes no damage from the acid!", "snapjawは酸でダメージを受けなかった！"),
            ("snapjaws take 4 damage from the acid!", "snapjawsは酸で4ダメージを受けた！"),
            ("{{r|You take 3 damage being run over by the chrome pyramid!}}", "{{r|chrome pyramidに轢かれて3ダメージを受けた！}}"),
            ("{{r|You take 5 heat from the blaze!}}", "{{r|blazeで5熱ダメージを受けた！}}"),
            ("{{r|You take 5 {{icy|cold damage}} from the shard!}}", "{{r|shardで5{{icy|冷気ダメージ}}を受けた！}}"),
            ("{{r|You take 8 damage from your laser beam! {{R|(x2)}}}}", "{{r|あなたのレーザービームで8ダメージを受けた！ {{R|(x2)}}}}"),
            ("The {{B|濡れた}}グロウフィッシュ takes 7 damage from your laser beam! {{&r|(x3)}}", "{{B|濡れた}}グロウフィッシュはあなたのレーザービームで7ダメージを受けた！ {{&r|(x3)}}"),
            ("{{r|You take 6 damage {{R|(x2)}} from colliding with the chrome wall.}}", "{{r|{{R|(x2)}} chrome wallとの衝突で6ダメージを受けた。}}"),
            ("The {{B|濡れた}}光葉 takes 1 damage from leaking.", "{{B|濡れた}}光葉は液漏れで1ダメージを受けた。"),
            ("The 樹液まみれの濡れた光葉 takes no damage from oozing.", "樹液まみれの濡れた光葉は滲出でダメージを受けなかった。"),
            ("{{r|You take 1 damage from fluxing.}}", "{{r|フラックス漏れで1ダメージを受けた。}}"),
            ("The 落葉剤グレネード mk I miner mk I takes 4 damage from your freezing effect!", "落葉剤グレネード mk I miner mk Iはあなたの凍結効果で4ダメージを受けた！"),
            ("{{r|You take 9 damage from your plasma you started by you near your ally and You!}}", "{{r|あなたのplasma you started by you near your ally and Youで9ダメージを受けた！}}"),
        };

        AssertPhysicsProcessTakeDamageQueuedMessages(cases);
    }

    [Test]
    public void PhysicsProcessTakeDamage_TranslatesStaticProducerDamageSources_WhenOwnerPatched()
    {
        var cases = new (string Source, string Expected)[]
        {
            ("The target takes 4 damage from your pyrokinesis!", "targetはあなたの熱念動で4ダメージを受けた！"),
            ("The target takes 4 damage from 監視官イラメの cryokinesis!", "targetは監視官イラメの冷気操作で4ダメージを受けた！"),
            ("The target takes 4 damage from snapjaw's life drain!", "targetはsnapjawの生命吸収で4ダメージを受けた！"),
            ("The target takes 4 damage from your disintegration!", "targetはあなたの分解で4ダメージを受けた！"),
            ("The target takes 4 damage from your stunning force!", "targetはあなたの衝撃念力で4ダメージを受けた！"),
            ("The target takes 4 damage from your freezing weapon!", "targetはあなたの凍てつく武器で4ダメージを受けた！"),
            ("The target takes 4 damage from your flaming weapon!", "targetはあなたの火炎武器で4ダメージを受けた！"),
            ("The target takes 4 damage from your damage reflection!", "targetはあなたのダメージ反射で4ダメージを受けた！"),
            ("The target takes 4 damage from your pummeling!", "targetはあなたの殴打で4ダメージを受けた！"),
            ("The target takes 4 damage from your shield slam!", "targetはあなたのシールドスラムで4ダメージを受けた！"),
            ("The target takes 4 damage from your explosion!", "targetはあなたの爆発で4ダメージを受けた！"),
            ("The target takes 4 damage from your electrical discharge!", "targetはあなたの放電で4ダメージを受けた！"),
            ("The target takes 4 damage from an electrical discharge!", "targetは放電で4ダメージを受けた！"),
            ("The target takes 4 damage from your flames!", "targetはあなたの炎で4ダメージを受けた！"),
            ("The target takes 4 damage from your freeze!", "targetはあなたの凍結で4ダメージを受けた！"),
            ("The target takes 4 damage from your passage!", "targetはあなたの通過で4ダメージを受けた！"),
            ("The target takes 4 damage from your digestive enzymes!", "targetはあなたの消化酵素で4ダメージを受けた！"),
            ("The target takes 4 damage from your tiny spines!", "targetはあなたの小さな棘で4ダメージを受けた！"),
            ("The target takes 4 damage from your projectile.", "targetはあなたの投射物で4ダメージを受けた。"),
            ("The target takes 4 damage from your attack.", "targetはあなたの攻撃で4ダメージを受けた。"),
            ("The target takes 4 damage from your carbide armor!", "targetはあなたのcarbide装甲で4ダメージを受けた！"),
            ("The target takes 4 damage from your freezing effect armor!", "targetはあなたの凍結効果装甲で4ダメージを受けた！"),
            ("The target takes 4 damage from your spores!", "targetはあなたの胞子で4ダメージを受けた！"),
            ("The target takes 4 damage from your thorns.", "targetはあなたの棘で4ダメージを受けた。"),
            ("The target takes 4 damage from your impalement.", "targetはあなたの串刺しで4ダメージを受けた。"),
            ("{{r|You take 4 damage from your {{C|freezing effect}}!}}", "{{r|あなたの{{C|凍結効果}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{G|acid}}!}}", "{{r|{{G|酸}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from your {{g|poison}}!}}", "{{r|あなたの{{g|毒}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{W|plasma}}!}}", "{{r|{{W|プラズマ}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{y|normality gas}}!}}", "{{r|{{y|正常化ガス}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{y|defoliant}}!}}", "{{r|{{y|落葉剤}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{y|fungicide}}!}}", "{{r|{{y|殺真菌剤}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from a plume of acid!}}", "{{r|酸の噴煙で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from a {{fiery|jet of flames}}!}}", "{{r|{{fiery|火炎噴流}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from the {{icy|cryogenic mist}}.}}", "{{r|{{icy|極低温の霧}}で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from your {{Y|scalding steam}}!}}", "{{r|あなたの{{Y|灼熱の蒸気}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from 骨灰の {{K|choking ash}}!}}", "{{r|骨灰の{{K|窒息性の灰}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from falling rocks! {{R|(x2)}}}}", "{{r|落石で4ダメージを受けた！ {{R|(x2)}}}}"),
            ("{{r|You take 4 damage from being crushed by a machine press.}}", "{{r|機械プレスに押し潰されたことで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from being forced into phase.}}", "{{r|位相に押し込まれたことで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from slamming into {{W|two}} walls!}}", "{{r|{{W|2}}枚の壁に叩きつけられたことで4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from an electrical shock delivered by your defibrillator.}}", "{{r|あなたのdefibrillatorからの電気ショックで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from an {{electrical|electrical shock}} delivered by your defibrillator.}}", "{{r|あなたのdefibrillatorからの{{electrical|電気ショック}}で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from a sharp edge!}}", "{{r|鋭利な刃で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from the device.}}", "{{r|装置で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from sitting!}}", "{{r|座ったことで4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from a nosebleed.}}", "{{r|鼻血で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from a processor leak.}}", "{{r|プロセッサ漏れで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from a hemorrhage.}}", "{{r|出血で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from falling rock falling on you.}}", "{{r|falling rockがあなたに落下したことで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from its fall.}}", "{{r|その落下で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from geomagnetic disc flying into you!}}", "{{r|geomagnetic discがあなたに飛び込んだことで4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from scourging yourself.}}", "{{r|自分を鞭打ったことで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from using your body as raw materials.}}", "{{r|あなたの体を原材料にしたことで4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from the cumulative trauma of your mental assault!}}", "{{r|あなたの精神攻撃による累積外傷で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from the cumulative trauma of a goatfolk's mental assault!}}", "{{r|goatfolkの精神攻撃による累積外傷で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from your failed assault on the structure of spacetime.}}", "{{r|あなたの時空構造への干渉失敗で4ダメージを受けた。}}"),
            ("{{r|You take 4 damage from the fire you started!}}", "{{r|あなたが起こした火で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from the fire a laser turret started!}}", "{{r|laser turretが起こした火で4ダメージを受けた！}}"),
            ("The snapjaw takes 4 damage from the fire itself started!", "snapjawは自身が起こした火で4ダメージを受けた！"),
            ("The target takes 4 damage from the fire started by snapjaw!", "targetはsnapjawが起こした火で4ダメージを受けた！"),
            ("The snapjaw takes 4 damage from the fire started by itself!", "snapjawは自身が起こした火で4ダメージを受けた！"),
            ("The target takes 4 damage from the fire started by the snapjaw!", "targetはsnapjawが起こした火で4ダメージを受けた！"),
            ("{{r|You take 4 damage from {{G|drinking acid}}!}}", "{{r|{{G|酸を飲んだこと}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{lava|drinking lava}}!}}", "{{r|{{lava|溶岩を飲んだこと}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from {{K|drinking asphalt}}!}}", "{{r|{{K|アスファルトを飲んだこと}}で4ダメージを受けた！}}"),
            ("{{r|You take 4 damage from the {{G|hulk}} {{w|honey}}!}}", "{{r|{{G|ハルク}} {{w|ハニー}}で4ダメージを受けた！}}"),
        };

        AssertPhysicsProcessTakeDamageQueuedMessages(cases);
    }

    [Test]
    public void PhysicsProcessTakeDamage_PreservesColorWrappers_WhenOwnerPatched()
    {
        AssertPhysicsProcessTakeDamageQueuedMessage(
            "{{r|You take 6 damage from {{G|glowfish}}!}}",
            "{{r|{{G|glowfish}}で6ダメージを受けた！}}");
    }

    [Test]
    public void PhysicsProcessTakeDamage_TranslatesDamageFramePopup_WhenUsePopups()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPhysicsProcessTakeDamageTarget), nameof(DummyPhysicsProcessTakeDamageTarget.ProcessTakeDamage), typeof(DummyGameEvent)),
                typeof(PhysicsProcessTakeDamageTranslationPatch));

            var eventObject = new DummyGameEvent();
            eventObject.SetFlag("UsePopups");
            var target = new DummyPhysicsProcessTakeDamageTarget
            {
                PopupMessageToSend = "{{r|You take 3 damage from the acid!}}",
            };

            target.ProcessTakeDamage(eventObject);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{r|酸で3ダメージを受けた！}}"));
            Assert.That(DummyMessageQueue.LastMessage, Is.Null.Or.Empty);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PhysicsProcessTakeDamage_DoesNotTranslateNoDamageMessagePassThrough_WhenOwnerPatched()
    {
        var eventObject = new DummyGameEvent();
        eventObject.SetFlag("NoDamageMessage");

        AssertPhysicsProcessTakeDamageQueuedMessage(
            "{{r|You sunder {{G|glowfish}}'s mind{{R|(x2)}} for 5 damage!}}",
            "{{r|You sunder {{G|glowfish}}'s mind{{R|(x2)}} for 5 damage!}}",
            eventObject);
    }

    [Test]
    public void PhysicsProcessTakeDamage_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("{{r|You take 7 damage from the acid!}}", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{r|You take 7 damage from the acid!}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PhysicsProcessTakeDamage_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertPhysicsProcessTakeDamageQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("{{r|You take 7 damage from the acid!}}"),
            "{{r|You take 7 damage from the acid!}}");
    }

    [Test]
    public void PhysicsProcessTakeDamage_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertPhysicsProcessTakeDamageQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void PhysicsObjectEnteringCell_TranslatesBlockedMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^The way is blocked by (?:the |a |an |some )?(.+?)[.!]?$", "{0}に道を塞がれている。"));

        AssertPhysicsObjectEnteringCellQueuedMessage(
            "The way is blocked by an chrome pyramid.",
            "chrome pyramidに道を塞がれている。");
    }

    [Test]
    public void PhysicsObjectEnteringCell_TranslatesCollisionMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^OUCH! You collide with (?:the |a |an |some )?(.+?)[.]$", "{0}にぶつかった！"));

        AssertPhysicsObjectEnteringCellQueuedMessage(
            "OUCH! You collide with a chrome pyramid.",
            "chrome pyramidにぶつかった！");
    }

    [TestCase(
        "OUCH! You collide with a chrome pyramid.",
        "痛っ！chrome pyramidに衝突した。")]
    [TestCase(
        "The way is blocked by an chrome pyramid.",
        "chrome pyramidに道を塞がれている。")]
    [TestCase(
        "{{Y|the shale wall}} are too difficult to traverse via the world map. You'll have to find your way on the surface.",
        "{{Y|shale wall}}はワールドマップでは通り抜けられないほど険しい。地表から道を探す必要がある。")]
    public void PhysicsObjectEnteringCell_TranslatesInventoriedQueuedShapes_WithRepositoryPatterns(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();

        AssertPhysicsObjectEnteringCellQueuedMessage(source, expected);
    }

    [Test]
    public void PhysicsObjectEnteringCell_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The way is blocked by an chrome pyramid.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The way is blocked by an chrome pyramid."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PhysicsObjectEnteringCell_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertPhysicsObjectEnteringCellQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The way is blocked by an chrome pyramid."),
            "The way is blocked by an chrome pyramid.");
    }

    [Test]
    public void PhysicsObjectEnteringCell_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertPhysicsObjectEnteringCellQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void CrippleApply_TranslatesDurationMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));
        WriteLeafDictionary(("5 turns", "5ターン"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCrippleApplyTarget), nameof(DummyCrippleApplyTarget.Apply), typeof(DummyGameObject)),
                typeof(CrippleApplyTranslationPatch));

            var target = new DummyCrippleApplyTarget
            {
                MessageToSend = "You are crippled for 5 turns!",
                ColorToSend = "R",
            };

            target.Apply(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("5ターンのあいだ手足が不自由になった！"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CrippleApply_LeavesUnknownOwnerMessageUnchanged_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCrippleApplyTarget), nameof(DummyCrippleApplyTarget.Apply), typeof(DummyGameObject)),
                typeof(CrippleApplyTranslationPatch));

            var target = new DummyCrippleApplyTarget
            {
                MessageToSend = "You are poisoned for 5 turns!",
            };

            target.Apply(new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You are poisoned for 5 turns!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CrippleApply_LeavesEmptyMessageUnchanged_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCrippleApplyTarget), nameof(DummyCrippleApplyTarget.Apply), typeof(DummyGameObject)),
                typeof(CrippleApplyTranslationPatch));

            var target = new DummyCrippleApplyTarget
            {
                MessageToSend = string.Empty,
                ColorToSend = "R",
            };

            target.Apply(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(string.Empty));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CrippleApply_PreservesColorTaggedDurationAndQueueColor_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));
        WriteLeafDictionary(("5 turns", "5ターン"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCrippleApplyTarget), nameof(DummyCrippleApplyTarget.Apply), typeof(DummyGameObject)),
                typeof(CrippleApplyTranslationPatch));

            var target = new DummyCrippleApplyTarget
            {
                MessageToSend = "You are crippled for {{C|5 turns}}!",
                ColorToSend = "R",
            };

            target.Apply(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{C|5ターン}}のあいだ手足が不自由になった！"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CrippleApply_DirectMarkerPassesThroughWithoutRetranslation_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCrippleApplyTarget), nameof(DummyCrippleApplyTarget.Apply), typeof(DummyGameObject)),
                typeof(CrippleApplyTranslationPatch));

            var target = new DummyCrippleApplyTarget
            {
                MessageToSend = "\u0001You are crippled for 5 turns!",
                ColorToSend = "R",
            };

            target.Apply(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You are crippled for 5 turns!"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CrippleApply_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You are crippled for 5 turns!", "R", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You are crippled for 5 turns!"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessageQueueSemanticPipeline_TranslatesActiveOwnerMessage()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));
        WriteLeafDictionary(("5 turns", "5ターン"));

        var message = "You are crippled for 5 turns!";

        CrippleApplyTranslationPatch.Prefix();
        try
        {
            var translated = MessageQueueSemanticPipeline.TryTranslateQueuedMessage(ref message, "R");

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.True);
                Assert.That(message, Is.EqualTo("\u00015ターンのあいだ手足が不自由になった！"));
            });
        }
        finally
        {
            CrippleApplyTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void MessageQueueSemanticPipeline_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));

        var message = "You are crippled for 5 turns!";

        var translated = MessageQueueSemanticPipeline.TryTranslateQueuedMessage(ref message, "R");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(message, Is.EqualTo("You are crippled for 5 turns!"));
        });
    }

    [Test]
    public void MessageQueueSemanticPipeline_DoesNotRetranslateDirectMarkedMessage()
    {
        WritePatternDictionary(
            ("^You are crippled for (.+?)!$", "{t0}のあいだ手足が不自由になった！"));

        var message = "\u0001You are crippled for 5 turns!";

        CrippleApplyTranslationPatch.Prefix();
        try
        {
            var translated = MessageQueueSemanticPipeline.TryTranslateQueuedMessage(ref message, "R");

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.False);
                Assert.That(message, Is.EqualTo("\u0001You are crippled for 5 turns!"));
            });
        }
        finally
        {
            CrippleApplyTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void ExperienceAwardXp_TranslatesColorizedXpGain_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyExperienceTarget), nameof(DummyExperienceTarget.HandleEvent), typeof(DummyAwardXPEvent)),
                typeof(ExperienceAwardXpTranslationPatch));

            var target = new DummyExperienceTarget
            {
                MessageToSend = "You gain {{C|75}} XP!",
                ColorToSend = "C",
            };

            target.HandleEvent(new DummyAwardXPEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたは経験値を{{C|75}}獲得した"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("You gain 75 XP", "あなたは経験値を75獲得した")]
    [TestCase("You gain 75 XP.", "あなたは経験値を75獲得した")]
    [TestCase("You gain 75 XP!", "あなたは経験値を75獲得した")]
    public void ExperienceAwardXp_TranslatesXpGainPunctuationVariants_WhenPatched(string source, string expected)
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyExperienceTarget), nameof(DummyExperienceTarget.HandleEvent), typeof(DummyAwardXPEvent)),
                typeof(ExperienceAwardXpTranslationPatch));

            var target = new DummyExperienceTarget
            {
                MessageToSend = source,
            };

            target.HandleEvent(new DummyAwardXPEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ExperienceAwardXp_LeavesUnknownOwnerMessageUnchanged_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyExperienceTarget), nameof(DummyExperienceTarget.HandleEvent), typeof(DummyAwardXPEvent)),
                typeof(ExperienceAwardXpTranslationPatch));

            var target = new DummyExperienceTarget
            {
                MessageToSend = "You gain renown!",
                ColorToSend = "C",
            };

            target.HandleEvent(new DummyAwardXPEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You gain renown!"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ExperienceAwardXp_LeavesEmptyMessageUnchanged_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyExperienceTarget), nameof(DummyExperienceTarget.HandleEvent), typeof(DummyAwardXPEvent)),
                typeof(ExperienceAwardXpTranslationPatch));

            var target = new DummyExperienceTarget
            {
                MessageToSend = string.Empty,
                ColorToSend = "C",
            };

            target.HandleEvent(new DummyAwardXPEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(string.Empty));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ExperienceAwardXp_DirectMarkerPassesThroughWithoutRetranslation_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyExperienceTarget), nameof(DummyExperienceTarget.HandleEvent), typeof(DummyAwardXPEvent)),
                typeof(ExperienceAwardXpTranslationPatch));

            var target = new DummyExperienceTarget
            {
                MessageToSend = "\u0001You gain {{C|75}} XP!",
                ColorToSend = "C",
            };

            target.HandleEvent(new DummyAwardXPEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You gain {{C|75}} XP!"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ExperienceAwardXp_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You gain {{C|75}} XP!", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You gain {{C|75}} XP!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectHeal_TranslatesHealMessage_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectHealTarget), nameof(DummyGameObjectHealTarget.Heal), typeof(int), typeof(bool), typeof(bool), typeof(bool)),
                typeof(GameObjectHealTranslationPatch));

            var target = new DummyGameObjectHealTarget
            {
                MessageToSend = "You heal for 5 hit points.",
            };

            target.Heal(5, message: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたは5HP回復した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectHeal_TranslatesHpLossMessage_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectHealTarget), nameof(DummyGameObjectHealTarget.Heal), typeof(int), typeof(bool), typeof(bool), typeof(bool)),
                typeof(GameObjectHealTranslationPatch));

            var target = new DummyGameObjectHealTarget
            {
                MessageToSend = "You lose 1 hit point.",
            };

            target.Heal(-1, message: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたは1HP失った。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectHeal_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        WritePatternDictionary(
            ("^You heal for (\\d+) hit points?\\.$", "あなたは{0}HP回復した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You heal for 5 hit points.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You heal for 5 hit points."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectHeal_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        WritePatternDictionary(
            ("^You heal for (\\d+) hit points?\\.$", "あなたは{0}HP回復した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectHealTarget), nameof(DummyGameObjectHealTarget.Heal), typeof(int), typeof(bool), typeof(bool), typeof(bool)),
                typeof(GameObjectHealTranslationPatch));

            var source = MessageFrameTranslator.MarkDirectTranslation("You heal for 5 hit points.");
            var target = new DummyGameObjectHealTarget
            {
                MessageToSend = source,
            };

            target.Heal(5, message: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You heal for 5 hit points."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectHeal_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectHealTarget), nameof(DummyGameObjectHealTarget.Heal), typeof(int), typeof(bool), typeof(bool), typeof(bool)),
                typeof(GameObjectHealTranslationPatch));

            var target = new DummyGameObjectHealTarget
            {
                MessageToSend = string.Empty,
            };

            target.Heal(5, message: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("You are healed for 5 by the cold.", "冷気により5回復した。")]
    [TestCase("You are healed for 12 by the heat.", "熱により12回復した。")]
    public void MutationAbsorptionHealing_TranslatesGeneratedHealingMessage_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertMutationAbsorptionHealingQueuedMessage(source, expected, expectedColor: "C");
    }

    [TestCase("You are healed for 5 by the cold.")]
    [TestCase("You are healed for 12 by the heat.")]
    public void MutationAbsorptionHealing_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "C", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("C"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MutationAbsorptionHealing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertMutationAbsorptionHealingQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You are healed for 5 by the cold."),
            "You are healed for 5 by the cold.",
            expectedColor: "C");
    }

    [TestCase("")]
    [TestCase("You are healed for 5 by the acid.")]
    [TestCase("You are healed by the cold.")]
    public void MutationAbsorptionHealing_LeavesUnsupportedMessageUnchanged_WhenOwnerPatched(string source)
    {
        AssertMutationAbsorptionHealingQueuedMessage(source, source);
    }

    [TestCase("You gain 1 mutation point!", "変異ポイントを1獲得した！")]
    [TestCase("You gain 3 mutation points!", "変異ポイントを3獲得した！")]
    [TestCase("You suddenly feel ready to use Sprint again.", "急にSprintを再使用できそうな気がしてきた。")]
    [TestCase("You suddenly feel ready to use {{G|Phase}} again.", "急に{{G|Phase}}を再使用できそうな気がしてきた。")]
    public void OnEatReward_TranslatesGeneratedRewardMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOnEatRewardQueuedMessage(source, expected, expectedColor: "G");
    }

    [TestCase("You gain 1 mutation point!")]
    [TestCase("You gain 3 mutation points!")]
    [TestCase("You suddenly feel ready to use Sprint again.")]
    public void OnEatReward_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "G", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("G"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void OnEatReward_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertOnEatRewardQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You gain 1 mutation point!"),
            "You gain 1 mutation point!",
            expectedColor: "G");
    }

    [TestCase("")]
    [TestCase("You gain mutation points!")]
    [TestCase("You suddenly feel ready again.")]
    public void OnEatReward_LeavesUnsupportedMessageUnchanged_WhenOwnerPatched(string source)
    {
        AssertOnEatRewardQueuedMessage(source, source);
    }

    [Test]
    public void GameObjectMove_TranslatesSingularStuckMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stuck[.!]?$", "{0}は動けなくなった。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectMoveTarget),
                    nameof(DummyGameObjectMoveTarget.Move),
                    typeof(string),
                    typeof(DummyGameObject).MakeByRefType(),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(int?),
                    typeof(string),
                    typeof(int?),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(int)),
                typeof(GameObjectMoveTranslationPatch));

            var target = new DummyGameObjectMoveTarget
            {
                MessageToSend = "The crate is stuck.",
            };

            target.Move("N", out _);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("crateは動けなくなった。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectMove_TranslatesConfirmationMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^Are you sure you want to move into (.+?)\\? Move (.+?) again to confirm\\.$", "{0}に入ってもよいか？ 確認するにはもう一度{1}へ移動する。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectMoveTarget),
                    nameof(DummyGameObjectMoveTarget.Move),
                    typeof(string),
                    typeof(DummyGameObject).MakeByRefType(),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(int?),
                    typeof(string),
                    typeof(int?),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(int)),
                typeof(GameObjectMoveTranslationPatch));

            var target = new DummyGameObjectMoveTarget
            {
                MessageToSend = "Are you sure you want to move into lava? Move north again to confirm.",
            };

            target.Move("N", out _);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("lavaに入ってもよいか？ 確認するにはもう一度northへ移動する。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("You cannot go that way.", "そちらには行けない。")]
    [TestCase("You are stopped short by the snapjaw.", "snapjawに行く手を阻まれた。")]
    [TestCase("The boulder cannot be moved.", "boulderは動かせない")]
    [TestCase("The crate is stuck.", "crateは動けなくなった。")]
    [TestCase("You can't budge the boulder.", "boulderを押し動かせない。")]
    [TestCase("There is deep water that way. Move north again to enter it and start swimming.", "その先にはdeep waterがある。もう一度northへ移動すると中に入り、泳ぎ始める。")]
    [TestCase("Are you sure you want to move into lava? Move north again to confirm.", "lavaに入ってもよいか？ 確認するにはもう一度northへ移動する。")]
    [TestCase("Are you sure you want to drop down a level? Move down again to confirm.", "1階層下へ降りてもよいか？ 確認するにはもう一度downへ移動する。")]
    public void GameObjectMove_TranslatesInventoriedQueuedShapes_WithRepositoryPatterns(string message, string expected)
    {
        UseRepositoryPatternDictionary();
        AssertGameObjectMoveQueuedMessage(message, expected);
    }

    [Test]
    public void GameObjectMove_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The crate is stuck.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The crate is stuck."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectMove_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertGameObjectMoveQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The crate is stuck."),
            "The crate is stuck.");
    }

    [Test]
    public void GameObjectMove_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        AssertGameObjectMoveQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void GameObjectMove_TranslatesSwimmingPopup_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchOwner(harmony, GameObjectMoveMethod(), typeof(GameObjectMoveTranslationPatch));

            var target = new DummyGameObjectMoveTarget
            {
                PopupMessageToSend = "There is deep water that way. Do you want to go north and start swimming?",
            };

            target.Move("N", out _);

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("その先にはdeep waterがある。northへ進み、泳ぎ始めるか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    [TestCase("You cannot open the 扉.", "扉を開けられない")]
    [TestCase("You are out of phase with the 扉.", "扉と位相がずれている")]
    [TestCase("You cannot reach the 扉.", "扉に手が届かない")]
    [TestCase("You can't unlock the 扉 from a distance.", "離れた位置から扉の鍵を開けることはできない")]
    [TestCase("You can't unlock the 扉.", "扉の鍵を開けられない")]
    [TestCase("You interface with the 扉 and unlock it.", "扉にインターフェースで接続して鍵を開けた")]
    [TestCase("You lay your hand upon the 扉 and draw forth its passcode. You enter the code and the 扉 unlocks.", "扉に手を当ててパスコードを読み取った。コードを入力すると扉の鍵が開いた")]
    [TestCase("You interface with the 扉 but nothing happens.", "You interface with the 扉 but nothing happens.")]
    [TestCase("", "")]
    [TestCase("You cannot open the <color=#ff0>扉</color>.", "<color=#ff0>扉</color>を開けられない")]
    [TestCase("\u0001扉を開けられない", "扉を開けられない")]
    public void DoorAttemptOpen_TranslatesAndPreservesExpectedMessages_WhenPatched(string message, string expected)
    {
        UseRepositoryPatternDictionary();
        AssertDoorAttemptOpenMessage(message, expected);
    }

    [Test]
    public void GameObjectPerformThrow_TranslatesHitMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You hit (?:the |a |an )?(.+?) with (?:the |a |an )?(.+?) \\(x(\\d+)\\) for (\\d+) damage!$", "{1}で{0}に{3}ダメージを与えた！ (x{2})"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectPerformThrowTarget),
                    nameof(DummyGameObjectPerformThrowTarget.PerformThrow),
                    typeof(DummyGameObject),
                    typeof(DummyCell),
                    typeof(DummyGameObject),
                    typeof(DummyMissilePath),
                    typeof(int),
                    typeof(int?),
                    typeof(int?),
                    typeof(int?)),
                typeof(GameObjectPerformThrowTranslationPatch));

            var target = new DummyGameObjectPerformThrowTarget
            {
                MessageToSend = "You hit the snapjaw with the iron javelin (x2) for 7 damage!",
            };

            target.PerformThrow(new DummyGameObject(), new DummyCell());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("iron javelinでsnapjawに7ダメージを与えた！ (x2)"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("You hit the snapjaw with the iron javelin (x2) for 7 damage!", "iron javelinでsnapjawに7ダメージを与えた！ (x2)")]
    [TestCase("The snapjaw hits with an iron javelin (x2) for 7 damage!", "snapjawはiron javelinで7ダメージを与えた！ (x2)")]
    [TestCase("The snapjaw hits the eyeless crab with an iron javelin (x2) for 7 damage!", "snapjawがiron javelinでeyeless crabに7ダメージを与えた！ (x2)")]
    public void GameObjectPerformThrow_TranslatesInventoriedQueuedShapes_WithRepositoryPatterns(string message, string expected)
    {
        UseRepositoryPatternDictionary();
        AssertGameObjectPerformThrowQueuedMessage(message, expected);
    }

    [TestCase("The snapjaw hits you with an iron arrow (x2) for 7 damage!", "snapjawのiron arrowで7ダメージを受けた！ (x2)")]
    [TestCase("The {{Y|snapjaw}} hits you with an {{B|iron arrow}} (x2) for 7 damage!", "{{Y|snapjaw}}の{{B|iron arrow}}で7ダメージを受けた！ (x2)")]
    [TestCase("You hit the snapjaw (x2) with an iron arrow for 7 damage!", "iron arrowでsnapjawに7ダメージを与えた！ (x2)")]
    [TestCase("You critically hit the snapjaw (x2) with an iron arrow for 7 damage!", "iron arrowでsnapjawに会心の一撃、7ダメージを与えた！ (x2)")]
    [TestCase("The snapjaw hits with an iron arrow (x2) for 7 damage!", "snapjawはiron arrowで7ダメージを与えた！ (x2)")]
    [TestCase("The snapjaw hits the eyeless crab with an iron arrow (x2) for 7 damage!", "snapjawがiron arrowでeyeless crabに7ダメージを与えた！ (x2)")]
    [TestCase("The snapjaw swings an iron arrow.", "The snapjaw swings an iron arrow.")]
    [TestCase("", "")]
    [TestCase("\u0001The snapjaw hits you with an iron arrow (x2) for 7 damage!", "The snapjaw hits you with an iron arrow (x2) for 7 damage!")]
    public void MissileWeaponHit_TranslatesInventoriedMultiplierDamageShapes_WhenOwnerPatched(
        string message,
        string expected)
    {
        UseRepositoryPatternDictionary();
        AssertMissileWeaponHitQueuedMessage(message, expected);
    }

    [Test]
    public void GameObjectPerformThrow_TranslatesSelfTargetPopup_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNoCancel(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectPerformThrowTarget),
                    nameof(DummyGameObjectPerformThrowTarget.PerformThrow),
                    typeof(DummyGameObject),
                    typeof(DummyCell),
                    typeof(DummyGameObject),
                    typeof(DummyMissilePath),
                    typeof(int),
                    typeof(int?),
                    typeof(int?),
                    typeof(int?)),
                typeof(GameObjectPerformThrowTranslationPatch));

            var target = new DummyGameObjectPerformThrowTarget
            {
                PopupMessageToSend = "Are you sure you want to target yourself?",
            };

            target.PerformThrow(new DummyGameObject(), new DummyCell());

            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("自分自身を標的にしてもよいか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectPerformThrow_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        WritePatternDictionary(
            ("^You hit (?:the |a |an )?(.+?) with (?:the |a |an )?(.+?) \\(x(\\d+)\\) for (\\d+) damage!$", "{1}で{0}に{3}ダメージを与えた！ (x{2})"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You hit the snapjaw with the iron javelin (x2) for 7 damage!", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You hit the snapjaw with the iron javelin (x2) for 7 damage!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectPerformThrow_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        WritePatternDictionary(
            ("^You hit (?:the |a |an )?(.+?) with (?:the |a |an )?(.+?) \\(x(\\d+)\\) for (\\d+) damage!$", "{1}で{0}に{3}ダメージを与えた！ (x{2})"));

        var source = MessageFrameTranslator.MarkDirectTranslation("You hit the snapjaw with the iron javelin (x2) for 7 damage!");
        AssertGameObjectPerformThrowQueuedMessage(source, "You hit the snapjaw with the iron javelin (x2) for 7 damage!");
    }

    [Test]
    public void GameObjectPerformThrow_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        WritePatternDictionary(
            ("^You hit (?:the |a |an )?(.+?) with (?:the |a |an )?(.+?) \\(x(\\d+)\\) for (\\d+) damage!$", "{1}で{0}に{3}ダメージを与えた！ (x{2})"));

        AssertGameObjectPerformThrowQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void GameObjectToggleActivatedAbility_TranslatesToggleMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You toggle (.+?) on\\.$", "{0}をオンにした。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectToggleActivatedAbilityTarget), nameof(DummyGameObjectToggleActivatedAbilityTarget.ToggleActivatedAbility), typeof(Guid), typeof(bool), typeof(bool?)),
                typeof(GameObjectToggleActivatedAbilityTranslationPatch));

            var target = new DummyGameObjectToggleActivatedAbilityTarget
            {
                MessageToSend = "You toggle Force Bubble on.",
            };

            target.ToggleActivatedAbility(Guid.NewGuid());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Force Bubbleをオンにした。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectToggleActivatedAbility_TranslatesOffMessage_WithRepositoryPatterns()
    {
        UseRepositoryPatternDictionary();
        AssertGameObjectToggleActivatedAbilityQueuedMessage("You toggle {{c|Akimbo}} off.", "{{c|二挺拳銃}}をオフにした。");
    }

    [Test]
    public void GameObjectToggleActivatedAbility_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You toggle {{c|Akimbo}} on.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You toggle {{c|Akimbo}} on."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectToggleActivatedAbility_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertGameObjectToggleActivatedAbilityQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You toggle Force Bubble on."),
            "You toggle Force Bubble on.");
    }

    [Test]
    public void GameObjectToggleActivatedAbility_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        AssertGameObjectToggleActivatedAbilityQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void GameObjectPopup_TranslatesConfirmUseImportantAsyncPlural_WhenOwnerPatched()
    {
        AssertGameObjectPopupShowYesNoAsync(
            "bronze daggers are important. Are you sure you want to disassemble them for bits?",
            "bronze daggersは重要だ。本当にそれらをdisassemble for bitsしますか？");
    }

    [Test]
    public void GameObjectPopup_TranslatesConfirmUseImportantSingular_WhenOwnerPatched()
    {
        AssertGameObjectPopupShowYesNo(
            "bronze dagger is important. Are you sure you want to use it?",
            "bronze daggerは重要だ。本当にuseしますか？");
    }

    [TestCase("bronze dagger doesn't want a new name.", "bronze daggerは新しい名前を望んでいない。")]
    [TestCase("You start calling bronze dagger by the name 'Edge'.", "bronze daggerを「Edge」と呼び始めた。")]
    public void GameObjectPopup_TranslatesHandleRenameMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertGameObjectPopupHandleRename(source, expected, useShowFail: source.EndsWith("new name.", StringComparison.Ordinal));
    }

    [TestCase("Irudad's Force Bubble ability is now toggled on.", "IrudadのForce Bubble能力はオンに切り替わった。")]
    [TestCase("Irudad's Force Bubble ability is now toggled off.", "IrudadのForce Bubble能力はオフに切り替わった。")]
    [TestCase("Irudad's Force Bubble ability cannot be toggled at this time.", "IrudadのForce Bubble能力は今は切り替えられない。")]
    [TestCase("Irudad's Force Bubble ability is now forbidden.", "IrudadのForce Bubble能力は禁止された。")]
    [TestCase("Irudad's Force Bubble ability is now allowed.", "IrudadのForce Bubble能力は許可された。")]
    public void GameObjectPopup_TranslatesChangeCompanionAbilityUseMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertGameObjectPopupChangeCompanionAbilityUse(source, expected);
    }

    [Test]
    public void GameObjectPopup_TranslatesCheckCompanionDirectionMessage_WhenOwnerPatched()
    {
        AssertGameObjectPopupCheckCompanionDirection("Irudad can't hear you!", "Irudadにはあなたの声が聞こえない！");
    }

    [Test]
    public void GameObjectPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You start calling bronze dagger by the name 'Edge'.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You start calling bronze dagger by the name 'Edge'."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertGameObjectPopupHandleRename(
            MessageFrameTranslator.MarkDirectTranslation("You start calling bronze dagger by the name 'Edge'."),
            "You start calling bronze dagger by the name 'Edge'.",
            useShowFail: false);
    }

    [Test]
    public void GameObjectPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertGameObjectPopupHandleRename(string.Empty, string.Empty, useShowFail: false);
    }

    [TestCase(
        nameof(DummyRealityStabilizedInterdictTarget.ShowGenericInterdictMessage),
        "You cannot alter spacetime through the normality lattice in the local region.",
        "局所領域のノーマリティ格子により、時空を変えることはできない。")]
    [TestCase(
        nameof(DummyRealityStabilizedInterdictTarget.ShowDistantInterdictMessage),
        "You cannot alter spacetime through the normality lattice in the local region, in order to teleport.",
        "局所領域のノーマリティ格子により、時空を変えることはできない。目的: teleport。")]
    [TestCase(
        nameof(DummyRealityStabilizedInterdictTarget.ShowDualInterdictMessage),
        "You cannot alter spacetime through either the normality lattice in your local region or the local region you're trying to interact with, in order to phase.",
        "あなたの局所領域か干渉しようとしている局所領域のノーマリティ格子により、時空を変えることはできない。目的: phase。")]
    public void RealityStabilizedInterdict_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertRealityStabilizedInterdictPopup(methodName, source, expected);
    }

    [Test]
    public void RealityStabilizedInterdict_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You cannot alter spacetime through the normality lattice in the local region.");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("You cannot alter spacetime through the normality lattice in the local region."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void RealityStabilizedInterdict_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertRealityStabilizedInterdictPopup(
            nameof(DummyRealityStabilizedInterdictTarget.ShowGenericInterdictMessage),
            MessageFrameTranslator.MarkDirectTranslation("You cannot alter spacetime through the normality lattice in the local region."),
            "You cannot alter spacetime through the normality lattice in the local region.");
    }

    [Test]
    public void RealityStabilizedInterdict_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertRealityStabilizedInterdictPopup(
            nameof(DummyRealityStabilizedInterdictTarget.ShowGenericInterdictMessage),
            string.Empty,
            string.Empty);
    }

    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultSuccess),
        "You hack bronze door.",
        "bronze doorをハックした。")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultExceptionalSuccess),
        "You hack bronze door and find tinkering bits <{{|ABCD}}> in it!",
        "bronze doorをハックし、itの中に修理ビット<{{|ABCD}}>を見つけた！")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultExceptionalSuccess),
        "You hack bronze door and find a weird artifact stuck in it!",
        "bronze doorをハックし、itの中に挟まっているa weird artifactを見つけた！")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultPartialSuccess),
        "You feel like you're making progress on hacking bronze door open.",
        "bronze doorを開くハックが進んでいる気がする。")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultPartialSuccess),
        "You feel like you're making progress on hacking power switch.",
        "power switchのハックが進んでいる気がする。")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultFailure),
        "You cannot seem to work out how to hack bronze door.",
        "bronze doorをハックする方法がわからない。")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultCriticalFailure),
        "Your attempt to hack bronze door has gone very wrong.",
        "bronze doorのハックはひどく失敗した。")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultExceptionalSuccess),
        "You hack phylactery, and find a way to reduce its power consumption in the process!",
        "phylacteryをハックし、その過程でitsの電力消費を減らす方法を見つけた！")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultExceptionalSuccess),
        "In the course of the hack, you are able to insert instructions into cybernetics terminal granting you an extra license point!",
        "ハックの過程でcybernetics terminalに命令を挿入し、追加のlicense pointを得た！")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultFailure),
        "The hack fails, and alert lights on cybernetics terminal begin pulsing rhythmically...",
        "ハックは失敗し、cybernetics terminalの警告灯が規則的に点滅し始めた...")]
    [TestCase(
        nameof(DummyHackingSifrahResultTarget.HackingResultCriticalFailure),
        "The hack fails, and alert lights on cybernetics terminal begin pulsing urgently...",
        "ハックは失敗し、cybernetics terminalの警告灯が緊急に点滅し始めた...")]
    public void HackingSifrahResult_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertHackingSifrahResultPopup(methodName, source, expected);
    }

    [Test]
    public void HackingSifrahResult_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You hack bronze door.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You hack bronze door."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HackingSifrahResult_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertHackingSifrahResultPopup(
            nameof(DummyHackingSifrahResultTarget.HackingResultSuccess),
            MessageFrameTranslator.MarkDirectTranslation("You hack bronze door."),
            "You hack bronze door.");
    }

    [Test]
    public void HackingSifrahResult_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertHackingSifrahResultPopup(
            nameof(DummyHackingSifrahResultTarget.HackingResultSuccess),
            string.Empty,
            string.Empty);
    }

    [TestCase(
        nameof(DummyQuestLifecyclePopupTarget.ShowStartPopup),
        "You have received a new quest, {{W|Aiding {{&Y|ドリンクス}} to Find the ポリセフian 祖父角の角笛}}!",
        "新しいクエスト「{{W|{{&Y|ドリンクス}}がポリセフian 祖父角の角笛を探すのを助ける}}」を受けた！")]
    [TestCase(
        nameof(DummyQuestLifecyclePopupTarget.ShowFailPopup),
        "You have failed the quest {{W|O Glorious Shekhinah!}}!",
        "クエスト「{{W|O Glorious Shekhinah!}}」に失敗した！")]
    [TestCase(
        nameof(DummyQuestLifecyclePopupTarget.ShowFailStepPopup),
        "You have failed the step, {{R|Travel to Red Rock}}, of the quest {{W|What's Eating the Watervine?}}!",
        "クエスト「{{W|What's Eating the Watervine?}}」のステップ「{{R|ジョッパから北へ2パラサング進み、レッドロックへ向かう。}}」に失敗した！")]
    [TestCase(
        nameof(DummyQuestLifecyclePopupTarget.ShowFinishPopup),
        "You have completed the quest {{W|O Glorious Shekhinah!}}!",
        "クエスト「{{W|O Glorious Shekhinah!}}」を完了した！")]
    public void QuestLifecyclePopup_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertQuestLifecyclePopup(methodName, source, expected);
    }

    [Test]
    public void QuestLifecyclePopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You have failed the quest O Glorious Shekhinah!!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You have failed the quest O Glorious Shekhinah!!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestLifecyclePopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertQuestLifecyclePopup(
            nameof(DummyQuestLifecyclePopupTarget.ShowFailPopup),
            MessageFrameTranslator.MarkDirectTranslation("You have failed the quest O Glorious Shekhinah!!"),
            "You have failed the quest O Glorious Shekhinah!!");
    }

    [Test]
    public void QuestLifecyclePopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertQuestLifecyclePopup(
            nameof(DummyQuestLifecyclePopupTarget.ShowStartPopup),
            string.Empty,
            string.Empty);
    }

    [Test]
    public void QuestLifecycleFinishStep_TranslatesShowBlockAndQueue_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowBlock(harmony);
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyQuestLifecyclePopupTarget), nameof(DummyQuestLifecyclePopupTarget.ShowFinishStepPopup)),
                typeof(QuestLifecyclePopupTranslationPatch));

            var target = new DummyQuestLifecyclePopupTarget
            {
                PopupMessageToSend = "You have finished the step, {{R|Travel to Red Rock}}, of the quest {{W|What's Eating the Watervine?}}!",
                StepXpToSend = 75,
            };

            target.ShowFinishStepPopup();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupTarget.LastShowBlockMessage,
                    Is.EqualTo("クエスト「{{W|What's Eating the Watervine?}}」のステップ「{{R|ジョッパから北へ2パラサング進み、レッドロックへ向かう。}}」を完了した！\nあなたは経験値を{{C|75}}獲得した"));
                Assert.That(
                    DummyMessageQueue.LastMessage,
                    Is.EqualTo("クエスト「{{W|What's Eating the Watervine?}}」のステップ「{{R|ジョッパから北へ2パラサング進み、レッドロックへ向かう。}}」を完了した！"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestLifecycleFinishStep_TranslatesShowBlockWithoutXp_WhenOwnerPatched()
    {
        AssertQuestLifecycleFinishStepShowBlock(
            "You have finished the step, {{R|Travel to Red Rock}}, of the quest {{W|What's Eating the Watervine?}}!",
            0,
            "クエスト「{{W|What's Eating the Watervine?}}」のステップ「{{R|ジョッパから北へ2パラサング進み、レッドロックへ向かう。}}」を完了した！");
    }

    [Test]
    public void QuestLifecycleFinishStep_DoesNotTranslateShowBlockOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowBlock(harmony);

            DummyPopupTarget.ShowBlock("You have finished the step, Travel to Red Rock, of the quest What's Eating the Watervine?!");

            Assert.That(
                DummyPopupTarget.LastShowBlockMessage,
                Is.EqualTo("You have finished the step, Travel to Red Rock, of the quest What's Eating the Watervine?!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestLifecycleFinishStep_DoesNotTranslateQueuedTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You have finished the step, Travel to Red Rock, of the quest What's Eating the Watervine?!");

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("You have finished the step, Travel to Red Rock, of the quest What's Eating the Watervine?!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestLifecycleFinishStep_DoesNotRetranslateDirectMarkedShowBlock_WhenOwnerPatched()
    {
        AssertQuestLifecycleFinishStepShowBlock(
            MessageFrameTranslator.MarkDirectTranslation("You have finished the step, Travel to Red Rock, of the quest What's Eating the Watervine?!"),
            0,
            "You have finished the step, Travel to Red Rock, of the quest What's Eating the Watervine?!");
    }

    [Test]
    public void QuestLifecycleFinishStep_LeavesEmptyShowBlockUnchanged_WhenOwnerPatched()
    {
        AssertQuestLifecycleFinishStepShowBlock(string.Empty, 0, string.Empty);
    }

    [TestCase(nameof(DummyFlightTarget.StartFlying), "You begin flying!", null, "飛行を開始した！")]
    [TestCase(nameof(DummyFlightTarget.StartFlying), "{{G|chrome hoverer}} begins flying.", null, "{{G|chrome hoverer}}が飛行を開始した。")]
    [TestCase(nameof(DummyFlightTarget.StartFlying), "{{G|The chrome hoverer}} begins flying.", null, "{{G|chrome hoverer}}が飛行を開始した。")]
    [TestCase(nameof(DummyFlightTarget.StartFlying), "You begin using an additional flight capability.", null, "追加の飛行手段を使い始めた。")]
    [TestCase(nameof(DummyFlightTarget.StopFlying), "You return to the ground.", null, "地上に戻った。")]
    [TestCase(nameof(DummyFlightTarget.StopFlying), "{{G|chrome hoverer}} returns to the ground.", null, "{{G|chrome hoverer}}が地上に戻った。")]
    [TestCase(nameof(DummyFlightTarget.StopFlying), "You cease using one of your flight capabilities.", null, "飛行手段の1つの使用をやめた。")]
    [TestCase(nameof(DummyFlightTarget.Land), "You return to the ground.", null, "地上に戻った。")]
    [TestCase(nameof(DummyFlightTarget.Land), "{{G|chrome hoverer}} returns to the ground.", null, "{{G|chrome hoverer}}が地上に戻った。")]
    [TestCase(nameof(DummyFlightTarget.FailFlying), "You fall to the ground!", "R", "地面に落下した！")]
    [TestCase(nameof(DummyFlightTarget.FailFlying), "{{G|chrome hoverer}} falls to the ground.", null, "{{G|chrome hoverer}}が地面に落下した。")]
    [TestCase(nameof(DummyFlightTarget.FailFlying), "One of your flight capabilities fails.", "R", "飛行能力のひとつが失われた。")]
    public void Flight_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertFlightMessage(methodName, source, color, expected);
    }

    [Test]
    public void Flight_TranslatesDoesVerbMarkedQueuedMessages_WhenOwnerPatched()
    {
        UseRepositoryMessageFrames();

        var subject = "The 巨大トンボ";
        var source = DoesVerbRouteTranslator.MarkDoesFragment(
            subject + " begins",
            "begin",
            subject.Length,
            null) + " flying.";

        AssertFlightMessage(
            nameof(DummyFlightTarget.StartFlying),
            source,
            null,
            "巨大トンボが飛翔し始めた。");
    }

    [Test]
    public void Flight_StripsLeadingEnglishArticleFromThirdPersonSubject_WhenOwnerPatched()
    {
        AssertFlightMessage(
            nameof(DummyFlightTarget.StartFlying),
            "The 巨大トンボ begins flying.",
            null,
            "巨大トンボが飛行を開始した。");
    }

    [Test]
    public void Flight_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You begin flying!", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You begin flying!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Flight_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertFlightMessage(
            nameof(DummyFlightTarget.StartFlying),
            MessageFrameTranslator.MarkDirectTranslation("You begin flying!"),
            null,
            "You begin flying!");
    }

    [Test]
    public void Flight_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertFlightMessage(nameof(DummyFlightTarget.StartFlying), string.Empty, null, string.Empty);
    }

    [TestCase(nameof(DummyBodyTarget.CheckUnsupportedPartLoss), "You have lost the use of your {{R|left arm}}.", "R", "{{R|left arm}}が使えなくなった。")]
    [TestCase(nameof(DummyBodyTarget.CheckPartRecovery), "You have recovered the use of your {{G|left arm}}.", "G", "{{G|left arm}}の使用が回復した。")]
    [TestCase(nameof(DummyBodyTarget.RegenerateLimb), "You regenerate your {{G|left arm}}!", "G", "{{G|left arm}}を再生した！")]
    public void Body_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertBodyQueuedMessage(methodName, source, color, expected);
    }

    [TestCase("Your {{R|left arm}} is dismembered!", "{{R|left arm}}が切断された！")]
    [TestCase("Your {{R|feet}} are dismembered!", "{{R|feet}}が切断された！")]
    public void Body_TranslatesDismemberPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertBodyPopup(nameof(DummyBodyTarget.Dismember), source, expected);
    }

    [Test]
    public void Body_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You have lost the use of your left arm.", "R", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You have lost the use of your left arm."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Body_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("Your left arm is dismembered!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Your left arm is dismembered!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Body_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertBodyQueuedMessage(
            nameof(DummyBodyTarget.CheckUnsupportedPartLoss),
            MessageFrameTranslator.MarkDirectTranslation("You have lost the use of your left arm."),
            "R",
            "You have lost the use of your left arm.");
    }

    [Test]
    public void Body_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertBodyPopup(
            nameof(DummyBodyTarget.Dismember),
            MessageFrameTranslator.MarkDirectTranslation("Your left arm is dismembered!"),
            "Your left arm is dismembered!");
    }

    [Test]
    public void Body_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertBodyQueuedMessage(nameof(DummyBodyTarget.CheckUnsupportedPartLoss), string.Empty, "R", string.Empty);
        AssertBodyPopup(nameof(DummyBodyTarget.Dismember), string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyItemModdingSifrahTarget.ResultFailure),
        "You abjectly failed to mod {{G|sturdy phase cannon}}.",
        "{{G|sturdy phase cannon}}の改造に完全に失敗した。")]
    [TestCase(
        nameof(DummyItemModdingSifrahTarget.ResultPartialSuccess),
        "Your work modding {{G|sturdy phase cannon}} was passable.",
        "{{G|sturdy phase cannon}}の改造作業はまずまずだった。")]
    [TestCase(
        nameof(DummyItemModdingSifrahTarget.ResultSuccess),
        "Your work modding {{G|sturdy phase cannon}} was solid and craftsmanlike.",
        "{{G|sturdy phase cannon}}の改造作業は堅実で職人らしい仕上がりだった。")]
    [TestCase(
        nameof(DummyItemModdingSifrahTarget.ResultCriticalSuccess),
        "Your work modding {{G|sturdy phase cannon}} was outstanding.",
        "{{G|sturdy phase cannon}}の改造作業は見事だった。")]
    public void ItemModdingSifrah_TranslatesResultPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertItemModdingSifrahPopup(methodName, source, expected);
    }

    [Test]
    public void ItemModdingSifrah_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You abjectly failed to mod sturdy phase cannon.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You abjectly failed to mod sturdy phase cannon."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ItemModdingSifrah_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertItemModdingSifrahPopup(
            nameof(DummyItemModdingSifrahTarget.ResultFailure),
            MessageFrameTranslator.MarkDirectTranslation("You abjectly failed to mod sturdy phase cannon."),
            "You abjectly failed to mod sturdy phase cannon.");
    }

    [Test]
    public void ItemModdingSifrah_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertItemModdingSifrahPopup(
            nameof(DummyItemModdingSifrahTarget.ResultFailure),
            string.Empty,
            string.Empty);
    }

    [TestCase(
        nameof(DummySunderMindTarget.CancelSunder),
        "Your concentration slips and the channel between you and {{G|glowfish}} dissipates into aether.",
        null,
        "集中が途切れ、あなたと{{G|glowfish}}の間の回路が霊気へ霧散した。")]
    [TestCase(
        nameof(DummySunderMindTarget.CancelSunder),
        "Your concentration slips and the channel dissipates.",
        null,
        "集中が途切れ、回路が霧散した。")]
    [TestCase(
        nameof(DummySunderMindTarget.PenetrationFailure),
        "Your attack fails to penetrate {{G|glowfish}}'s mental defenses.",
        "r",
        "{{G|glowfish}}'s mental defensesを突破できなかった。")]
    [TestCase(
        nameof(DummySunderMindTarget.Tick),
        "{{G|glowfish}}'s head explodes!",
        null,
        "{{G|glowfish}}の頭が爆発した！")]
    [TestCase(
        nameof(DummySunderMindTarget.Tick),
        "Your head explodes!",
        null,
        "あなたの頭が爆発した！")]
    [TestCase(
        nameof(DummySunderMindTarget.Tick),
        "{{r|You sunder {{G|glowfish}}'s mind{{R|(x2)}} for 5 damage!}}",
        null,
        "{{r|あなたは{{G|glowfish}}の精神を{{R|(x2)}}破壊し、5ダメージを与えた！}}")]
    [TestCase(
        nameof(DummySunderMindTarget.Tick),
        "{{r|glowfish sunders your mind{{R|(x1)}} for 3 damage!}}",
        null,
        "{{r|glowfishはあなたの精神を{{R|(x1)}}破壊し、3ダメージを与えた！}}")]
    [TestCase(
        nameof(DummySunderMindTarget.Tick),
        "{{G|glowfish}} sunders your mind{{R|(x1)}} for {{C|3}} damage!",
        null,
        "{{G|glowfish}}はあなたの精神を{{R|(x1)}}破壊し、{{C|3}}ダメージを与えた！")]
    [TestCase(
        nameof(DummySunderMindTarget.Nosebleed),
        "{{G|glowfish}}'s nose begins to bleed.",
        null,
        "{{G|glowfish}}の鼻血が出始めた。")]
    [TestCase(
        nameof(DummySunderMindTarget.Nosebleed),
        "Your nose begins to bleed.",
        null,
        "あなたの鼻血が出始めた。")]
    [TestCase(
        nameof(DummySunderMindTarget.Nosebleed),
        "{{G|chrome idol}}'s core begins to leak.",
        null,
        "{{G|chrome idol}}のコアが漏れ始めた。")]
    [TestCase(
        nameof(DummySunderMindTarget.Nosebleed),
        "Your core begins to leak.",
        null,
        "あなたのコアが漏れ始めた。")]
    [TestCase(
        nameof(DummySunderMindTarget.Nosebleed),
        "{{G|glowfish}}'s brain begins to hemorrhage.",
        null,
        "{{G|glowfish}}の脳が出血し始めた。")]
    [TestCase(
        nameof(DummySunderMindTarget.Nosebleed),
        "Your brain begins to hemorrhage.",
        null,
        "あなたの脳が出血し始めた。")]
    public void SunderMind_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertSunderMindQueuedMessage(methodName, source, color, expected);
    }

    [TestCase(
        "You burrow a channel through the psychic aether to {{G|glowfish}} and begin to sunder its mind!",
        "精神の霊界に穿ち{{G|glowfish}}へ通路を掘り、その精神を破壊し始めた！")]
    [TestCase(
        "You burrow a channel through the psychic aether to the pair and begin to sunder their mind!",
        "精神の霊界に穿ちthe pairへ通路を掘り、彼らの精神を破壊し始めた！")]
    public void SunderMind_TranslatesBeginSunderQueuedMessage_WhenOwnerPatched(string source, string expected)
    {
        AssertSunderMindBeginSunderQueuedMessage(source, expected);
    }

    [Test]
    public void SunderMind_TranslatesBeginSunderPopup_WhenOwnerPatched()
    {
        AssertSunderMindBeginSunderPopup(
            "{{G|glowfish}} north burrows a channel through the psychic aether and begins to sunder your mind!",
            "{{G|glowfish}} north 精神の霊界に通路を掘り、あなたの精神を破壊し始めた！");
    }

    [Test]
    public void SunderMind_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your concentration slips and the channel dissipates.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your concentration slips and the channel dissipates."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SunderMind_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("glowfish north burrows a channel through the psychic aether and begins to sunder your mind!");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("glowfish north burrows a channel through the psychic aether and begins to sunder your mind!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SunderMind_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertSunderMindQueuedMessage(
            nameof(DummySunderMindTarget.CancelSunder),
            MessageFrameTranslator.MarkDirectTranslation("Your concentration slips and the channel dissipates."),
            null,
            "Your concentration slips and the channel dissipates.");
    }

    [Test]
    public void SunderMind_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertSunderMindBeginSunderPopup(
            MessageFrameTranslator.MarkDirectTranslation("glowfish burrows a channel through the psychic aether and begins to sunder your mind!"),
            "glowfish burrows a channel through the psychic aether and begins to sunder your mind!");
    }

    [Test]
    public void SunderMind_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertSunderMindQueuedMessage(nameof(DummySunderMindTarget.CancelSunder), string.Empty, null, string.Empty);
        AssertSunderMindBeginSunderPopup(string.Empty, string.Empty);
    }

    [TestCase("Your head explodes!")]
    [TestCase("Your sense of self is pulled apart by what feels like a billion years of geologic pressure.")]
    public void SunderMind_LeavesFixedTickPopupsUnchanged_WhenOwnerPatched(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySunderMindTarget), nameof(DummySunderMindTarget.Tick)),
                typeof(SunderMindTranslationPatch));

            var target = new DummySunderMindTarget
            {
                PopupMessageToSend = source,
            };

            target.Tick();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
        "{{W|{{G|glowfish}}'s mind starts to fluctuate in and out of coherence.}}",
        "{{W|{{G|glowfish}}の精神が一貫性を失って揺らぎ始めた。}}")]
    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
        "{{W|Your mind starts to fluctuate in and out of coherence.}}",
        "{{W|あなたの精神が一貫性を失って揺らぎ始めた。}}")]
    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchMutations),
        "{{W|{{G|glowfish}}'s genome fluctuates and genes start turning on and off at random.}}",
        "{{W|{{G|glowfish}}のゲノムが揺らぎ、遺伝子が無作為にオンオフし始めた。}}")]
    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchMutations),
        "{{W|Your genome fluctuates and genes start turning on and off at random.}}",
        "{{W|あなたのゲノムが揺らぎ、遺伝子が無作為にオンオフし始めた。}}")]
    public void LiquidWarmStatic_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertLiquidWarmStaticQueuedMessage(methodName, source, expected);
    }

    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
        "{{G|glowfish}}'s knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}.",
        "{{G|glowfish}}の{{rules|Long Blade}}の知識が{{rules|Cudgel}}の知識へ歪んだ。")]
    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
        "Your knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}.",
        "あなたの{{rules|Long Blade}}の知識が{{rules|Cudgel}}の知識へ歪んだ。")]
    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchMutations),
        "{{G|glowfish}}'s mutation {{rules|Spinnerets}} transmutes into the mutation {{rules|Light Manipulation}}.",
        "{{G|glowfish}}の変異{{rules|Spinnerets}}が変異{{rules|Light Manipulation}}へ変質した。")]
    [TestCase(
        nameof(DummyLiquidWarmStaticTarget.GlitchMutations),
        "Your mutation {{rules|Spinnerets}} transmutes into the mutation {{rules|Light Manipulation}}.",
        "あなたの変異{{rules|Spinnerets}}が変異{{rules|Light Manipulation}}へ変質した。")]
    public void LiquidWarmStatic_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertLiquidWarmStaticPopupMessage(methodName, source, expected);
    }

    [Test]
    public void LiquidWarmStatic_TranslatesMultilinePopupMessage_WhenOwnerPatched()
    {
        AssertLiquidWarmStaticPopupMessage(
            nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
            "{{G|glowfish}}'s knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}.\n"
            + "{{G|glowfish}}'s knowledge of {{rules|Axe}} distorts into knowledge of {{rules|Short Blade}}.",
            "{{G|glowfish}}の{{rules|Long Blade}}の知識が{{rules|Cudgel}}の知識へ歪んだ。\n"
            + "{{G|glowfish}}の{{rules|Axe}}の知識が{{rules|Short Blade}}の知識へ歪んだ。");
    }

    [Test]
    public void LiquidWarmStatic_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(
                "{{W|{{G|glowfish}}'s mind starts to fluctuate in and out of coherence.}}",
                null,
                Capitalize: false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("{{W|{{G|glowfish}}'s mind starts to fluctuate in and out of coherence.}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidWarmStatic_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show(
                "{{G|glowfish}}'s knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}.");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("{{G|glowfish}}'s knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidWarmStatic_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertLiquidWarmStaticQueuedMessage(
            nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
            MessageFrameTranslator.MarkDirectTranslation("{{G|glowfish}}'s mind starts to fluctuate in and out of coherence."),
            "{{G|glowfish}}'s mind starts to fluctuate in and out of coherence.");
    }

    [Test]
    public void LiquidWarmStatic_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertLiquidWarmStaticPopupMessage(
            nameof(DummyLiquidWarmStaticTarget.GlitchSkills),
            MessageFrameTranslator.MarkDirectTranslation(
                "{{G|glowfish}}'s knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}."),
            "{{G|glowfish}}'s knowledge of {{rules|Long Blade}} distorts into knowledge of {{rules|Cudgel}}.");
    }

    [Test]
    public void LiquidWarmStatic_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertLiquidWarmStaticQueuedMessage(nameof(DummyLiquidWarmStaticTarget.GlitchSkills), string.Empty, string.Empty);
        AssertLiquidWarmStaticPopupMessage(nameof(DummyLiquidWarmStaticTarget.GlitchMutations), string.Empty, string.Empty);
    }

    [Test]
    public void LiquidWarmStatic_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched()
    {
        const string queued = "{{W|This warm static message is unsupported.}}";
        const string popup = "An unknown warm static event happens.";
        AssertLiquidWarmStaticQueuedMessage(nameof(DummyLiquidWarmStaticTarget.GlitchSkills), queued, queued);
        AssertLiquidWarmStaticPopupMessage(nameof(DummyLiquidWarmStaticTarget.GlitchMutations), popup, popup);
    }

    [TestCase(
        nameof(DummyKeybindsScreenConflictTarget.ConfirmConflictBind),
        "{{W|Ctrl+F}} is already bound to {{C|Fire}} and {{C|Force Bubble}}.\r\n\r\nDo you want to bind it to {{C|Fly}} instead?",
        "{{W|Ctrl+F}}はすでに{{C|Fire}} and {{C|Force Bubble}}に割り当てられています。\r\n\r\n代わりに{{C|Fly}}へ割り当てますか？")]
    [TestCase(
        nameof(DummyKeybindsScreenConflictTarget.ConfirmDynamicConflictBind),
        "{{W|Ctrl+F}} is already bound to {{C|Fire}}.\r\n\r\nDo you want to bind it to {{C|Fly}} anyway?",
        "{{W|Ctrl+F}}はすでに{{C|Fire}}に割り当てられています。\r\n\r\nそれでも{{C|Fly}}へ割り当てますか？")]
    public void KeybindsScreenConflict_TranslatesConfirmPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertKeybindsScreenConflictYesNoAsync(methodName, source, expected);
    }

    [Test]
    public void KeybindsScreenConflict_TranslatesRequiredConflictPopup_WhenOwnerPatched()
    {
        AssertKeybindsScreenConflictShowAsync(
            "{{W|Ctrl+F}} is already bound to {{C|Fire}}.  This is a required bind and can't be removed.\r\n\r\nChoose a new bind for {{C|Fire}} first, and then rebind {{W|Ctrl+F}}.",
            "{{W|Ctrl+F}}はすでに{{C|Fire}}に割り当てられています。これは必須の割り当てなので削除できません。\r\n\r\n先に{{C|Fire}}の新しい割り当てを選んでから、{{W|Ctrl+F}}を割り当て直してください。");
    }

    [Test]
    public void KeybindsScreenConflict_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNoAsync(harmony);

            _ = DummyPopupShow.ShowYesNoAsync("{{W|Ctrl+F}} is already bound to {{C|Fire}}.\r\n\r\nDo you want to bind it to {{C|Fly}} anyway?")
                .GetAwaiter()
                .GetResult();

            Assert.That(
                DummyPopupShow.LastShowYesNoAsyncMessage,
                Is.EqualTo("{{W|Ctrl+F}} is already bound to {{C|Fire}}.\r\n\r\nDo you want to bind it to {{C|Fly}} anyway?"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void KeybindsScreenConflict_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertKeybindsScreenConflictYesNoAsync(
            nameof(DummyKeybindsScreenConflictTarget.ConfirmDynamicConflictBind),
            MessageFrameTranslator.MarkDirectTranslation(
                "{{W|Ctrl+F}} is already bound to {{C|Fire}}.\r\n\r\nDo you want to bind it to {{C|Fly}} anyway?"),
            "{{W|Ctrl+F}} is already bound to {{C|Fire}}.\r\n\r\nDo you want to bind it to {{C|Fly}} anyway?");
    }

    [Test]
    public void KeybindsScreenConflict_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertKeybindsScreenConflictYesNoAsync(
            nameof(DummyKeybindsScreenConflictTarget.ConfirmDynamicConflictBind),
            string.Empty,
            string.Empty);
    }

    [TestCase(
        nameof(DummyRealityStabilizedEventTarget.TryContest),
        "You feel a psychic whiff as {{G|glowfish}} pushes past resistance in the structure of spacetime.",
        "{{G|glowfish}}が時空構造の抵抗を押し通る、精神的なかすかな感触を覚えた。")]
    [TestCase(
        nameof(DummyRealityStabilizedEventTarget.FailedToContest),
        "You feel a psychic thud as {{G|glowfish}} pushes against the structure of spacetime and fails to break through.",
        "{{G|glowfish}}が時空構造を押して突破に失敗した、精神的な鈍い衝撃を感じた。")]
    [TestCase(
        nameof(DummyRealityStabilizedEventTarget.FailedToContest),
        "You feel a psychic thud as someone pushes against the structure of spacetime and fails to break through.",
        "誰かが時空構造を押して突破に失敗した、精神的な鈍い衝撃を感じた。")]
    [TestCase(
        nameof(DummyRealityStabilizedEventTarget.FailedToContest),
        "{{G|glowfish}} winces.",
        "{{G|glowfish}}が顔をしかめた。")]
    [TestCase(
        nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice),
        "{{G|phase cannon}} showers sparks everywhere.",
        "{{G|phase cannon}}があたり一面に火花を散らした。")]
    public void RealityStabilizedEvent_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertRealityStabilizedEventQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void RealityStabilizedEvent_TranslatesShortCircuitPopup_WhenOwnerPatched()
    {
        AssertRealityStabilizedEventPopup(
            nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice),
            "{{G|phase cannon}} emits a shower of sparks!",
            "{{G|phase cannon}}が火花の雨を放った！");
    }

    [TestCase(
        "A normality lattice prevents you from altering spacetime in both your local region and the local region you're trying to interact with. You can try to push through at some risk. Your feeling is that success would be challenging. Do you want to try?",
        "ノーマリティ格子により、あなたは自分の局所領域と干渉しようとしている局所領域の両方で時空を変えられない。危険を冒して押し通ることはできる。成功は困難だと感じる。試しますか？")]
    [TestCase(
        "A normality lattice prevents you from altering spacetime in the local region. You can try to push through at some risk. You estimate less than a {{R|20%}}R chance of success. Do you want to try?",
        "ノーマリティ格子により、あなたはこの局所領域で時空を変えられない。危険を冒して押し通ることはできる。成功率は{{R|20%}}R未満と見積もっている。試しますか？")]
    [TestCase(
        "A normality lattice prevents you from altering spacetime in that local region. You can try to push through at some risk. You estimate about a {{G|75%}}G chance of success. Do you want to try?",
        "ノーマリティ格子により、あなたはその局所領域で時空を変えられない。危険を冒して押し通ることはできる。成功率は約{{G|75%}}Gと見積もっている。試しますか？")]
    [TestCase(
        "A normality lattice crackles nearby.",
        "A normality lattice crackles nearby.")]
    public void RealityStabilizedEvent_TranslatesOptionToContestPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertRealityStabilizedEventPopup(
            nameof(DummyRealityStabilizedEventTarget.OptionToContest),
            source,
            expected);
    }

    [TestCase(
        "You try to push through the normality lattice, but it snaps back into place.",
        "あなたはノーマリティ格子を押し通ろうとしたが、それは跳ね返って元に戻った。")]
    [TestCase(
        "You try to push through the normality lattice, but it snaps back into place. You wince in pain.",
        "あなたはノーマリティ格子を押し通ろうとしたが、それは跳ね返って元に戻った。あなたは痛みに顔をしかめた。")]
    [TestCase(
        "You push against the normality lattice, but nothing happens.",
        "You push against the normality lattice, but nothing happens.")]
    [TestCase("", "")]
    [TestCase(
        "\x01You try to push through the normality lattice, but it snaps back into place.",
        "You try to push through the normality lattice, but it snaps back into place.")]
    [TestCase(
        "{{R|You try to push through the normality lattice, but it snaps back into place.}}",
        "{{R|あなたはノーマリティ格子を押し通ろうとしたが、それは跳ね返って元に戻った。}}")]
    public void RealityStabilizedEvent_TranslatesFailedContestSelfPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertRealityStabilizedEventPopup(
            nameof(DummyRealityStabilizedEventTarget.FailedToContestPopup),
            source,
            expected);
    }

    [TestCase("You feel a psychic whiff as glowfish pushes past resistance in the structure of spacetime.")]
    [TestCase("You feel a psychic thud as glowfish pushes against the structure of spacetime and fails to break through.")]
    [TestCase("glowfish winces.")]
    public void RealityStabilizedEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.Null);
                Assert.That(DummyMessageQueue.LastCapitalize, Is.False);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void RealityStabilizedEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("phase cannon emits a shower of sparks!");
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("phase cannon emits a shower of sparks!"));

            var optionPopup = "A normality lattice prevents you from altering spacetime in the local region. You can try to push through at some risk. You estimate about a {{G|75%}}G chance of success. Do you want to try?";
            DummyPopupShow.Show(optionPopup);
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(optionPopup));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void RealityStabilizedEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertRealityStabilizedEventQueuedMessage(
            nameof(DummyRealityStabilizedEventTarget.TryContest),
            MessageFrameTranslator.MarkDirectTranslation(
                "You feel a psychic whiff as glowfish pushes past resistance in the structure of spacetime."),
            "You feel a psychic whiff as glowfish pushes past resistance in the structure of spacetime.");
    }

    [Test]
    public void RealityStabilizedEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertRealityStabilizedEventPopup(
            nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice),
            MessageFrameTranslator.MarkDirectTranslation("phase cannon emits a shower of sparks!"),
            "phase cannon emits a shower of sparks!");
        AssertRealityStabilizedEventPopup(
            nameof(DummyRealityStabilizedEventTarget.OptionToContest),
            MessageFrameTranslator.MarkDirectTranslation(
                "A normality lattice prevents you from altering spacetime in the local region. You can try to push through at some risk. You estimate about a {{G|75%}}G chance of success. Do you want to try?"),
            "A normality lattice prevents you from altering spacetime in the local region. You can try to push through at some risk. You estimate about a {{G|75%}}G chance of success. Do you want to try?");
    }

    [Test]
    public void RealityStabilizedEvent_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertRealityStabilizedEventQueuedMessage(nameof(DummyRealityStabilizedEventTarget.TryContest), string.Empty, string.Empty);
        AssertRealityStabilizedEventPopup(nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice), string.Empty, string.Empty);
        AssertRealityStabilizedEventPopup(nameof(DummyRealityStabilizedEventTarget.OptionToContest), string.Empty, string.Empty);
    }

    [TestCase(
        "You feel a small ripple in space and time.",
        "時空に小さな波紋を感じた。")]
    [TestCase(
        "{{R|Someone reaches through the aggregate mind and exhausts your power!}}",
        "{{R|誰かが集合精神を通じて手を伸ばし、あなたの力を消耗させた！}}")]
    [TestCase(
        "{{G|You innervate your mind at someone's expense.}}",
        "{{G|誰かを犠牲にして精神を活性化した。}}")]
    public void MassMind_TranslatesQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertMassMindQueuedMessage(source, expected);
    }

    [TestCase("You feel a small ripple in space and time.")]
    [TestCase("{{R|Someone reaches through the aggregate mind and exhausts your power!}}")]
    [TestCase("{{G|You innervate your mind at someone's expense.}}")]
    public void MassMind_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MassMind_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertMassMindQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You feel a small ripple in space and time."),
            "You feel a small ripple in space and time.");
    }

    [Test]
    public void MassMind_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertMassMindQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyCyberneticRejectionSyndromeTarget.Apply),
        "Your feverish feeling is getting worse.",
        "r",
        "発熱感が悪化している。")]
    [TestCase(
        nameof(DummyCyberneticRejectionSyndromeTarget.Apply),
        "{{r|You feel feverish.}}",
        "r",
        "{{r|熱っぽく感じる。}}")]
    [TestCase(
        nameof(DummyCyberneticRejectionSyndromeTarget.Remove),
        "You feel less feverish.",
        "g",
        "熱っぽさが少し和らいだ。")]
    [TestCase(
        nameof(DummyCyberneticRejectionSyndromeTarget.Reduce),
        "Your feverish feeling eases up a bit.",
        "g",
        "発熱感が少し和らいだ。")]
    public void CyberneticRejectionSyndrome_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertCyberneticRejectionSyndromeQueuedMessage(methodName, source, color, expected);
    }

    [Test]
    public void CyberneticRejectionSyndrome_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You feel feverish.", "r", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You feel feverish."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CyberneticRejectionSyndrome_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertCyberneticRejectionSyndromeQueuedMessage(
            nameof(DummyCyberneticRejectionSyndromeTarget.Apply),
            MessageFrameTranslator.MarkDirectTranslation("You feel feverish."),
            "r",
            "You feel feverish.");
    }

    [Test]
    public void CyberneticRejectionSyndrome_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertCyberneticRejectionSyndromeQueuedMessage(
            nameof(DummyCyberneticRejectionSyndromeTarget.Apply),
            string.Empty,
            "r",
            string.Empty);
    }

    [TestCase(
        nameof(DummyGeomagneticDiscTarget.SignalFailure),
        "A loud buzz is emitted. The failure glyph flashes on the side of {{Y|geomagnetic disc}}.",
        "{{Y|geomagnetic disc}}",
        "大きなブザー音が鳴り、{{Y|geomagnetic disc}}の側面で故障のグリフが点滅した。")]
    [TestCase(
        nameof(DummyGeomagneticDiscTarget.SignalLowPower),
        "A loud buzz is emitted. The low power glyph flashes on the side of {{Y|geomagnetic disc}}.",
        "{{Y|geomagnetic disc}}",
        "大きなブザー音が鳴り、{{Y|geomagnetic disc}}の側面で低電力のグリフが点滅した。")]
    [TestCase(
        nameof(DummyGeomagneticDiscTarget.ExamineFailure),
        "{{Y|The geomagnetic disc}} suddenly starts flying around!",
        "{{Y|The geomagnetic disc}}",
        "{{Y|The geomagnetic disc}}が突然飛び回り始めた！")]
    public void GeomagneticDisc_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string captureToken,
        string expected)
    {
        AssertGeomagneticDiscPopup(methodName, source, captureToken, expected);
    }

    [Test]
    public void GeomagneticDisc_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail(
                "A loud buzz is emitted. The failure glyph flashes on the side of geomagnetic disc.");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("A loud buzz is emitted. The failure glyph flashes on the side of geomagnetic disc."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GeomagneticDisc_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertGeomagneticDiscPopup(
            nameof(DummyGeomagneticDiscTarget.SignalFailure),
            MessageFrameTranslator.MarkDirectTranslation(
                "A loud buzz is emitted. The failure glyph flashes on the side of geomagnetic disc."),
            "geomagnetic disc",
            "A loud buzz is emitted. The failure glyph flashes on the side of geomagnetic disc.");
    }

    [Test]
    public void GeomagneticDisc_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertGeomagneticDiscPopup(
            nameof(DummyGeomagneticDiscTarget.SignalFailure),
            string.Empty,
            string.Empty,
            string.Empty);
    }

    [TestCase(
        "{{Y|The campfire}} is turned off.",
        "{{Y|The campfire}}",
        "{{Y|The campfire}}はオフになっている。")]
    [TestCase(
        "{{Y|The campfire}} does not have enough charge to operate.",
        "{{Y|The campfire}}",
        "{{Y|The campfire}}には動作に必要な充電が足りない。")]
    [TestCase(
        "{{Y|The campfire}} needs to be hung up first.",
        "{{Y|The campfire}}",
        "{{Y|The campfire}}は先につり下げる必要がある。")]
    [TestCase(
        "{{Y|The campfire}} does not seem to be working.",
        "{{Y|The campfire}}",
        "{{Y|The campfire}}は動作していないようだ。")]
    public void CampfireCookAvailability_TranslatesPopupMessages_WhenOwnerPatched(
        string source,
        string captureToken,
        string expected)
    {
        AssertCampfireCookAvailabilityPopup(source, captureToken, expected);
    }

    [Test]
    public void CampfireCookAvailability_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("The campfire is turned off.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("The campfire is turned off."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CampfireCookAvailability_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertCampfireCookAvailabilityPopup(
            MessageFrameTranslator.MarkDirectTranslation("The campfire is turned off."),
            "The campfire",
            "The campfire is turned off.");
    }

    [Test]
    public void CampfireCookAvailability_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertCampfireCookAvailabilityPopup(string.Empty, string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyTeleprojectorTarget.HandleEvent),
        "{{Y|The teleprojector}} attunes to your physiology.",
        "{{Y|The teleprojector}}",
        "{{Y|The teleprojector}}があなたの生理機能に同調した。")]
    [TestCase(
        nameof(DummyTeleprojectorTarget.ActivateTeleprojector),
        "There is nothing there that {{Y|the teleprojector}} can uplink with.",
        "{{Y|the teleprojector}}",
        "そこには{{Y|the teleprojector}}がアップリンクできるものが何もない。")]
    [TestCase(
        nameof(DummyTeleprojectorTarget.RoboDom),
        "You take control of {{G|scrap shoveler}}!",
        "{{G|scrap shoveler}}",
        "{{G|scrap shoveler}}を支配した！")]
    public void Teleprojector_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string captureToken,
        string expected)
    {
        AssertTeleprojectorPopup(methodName, source, captureToken, expected);
    }

    [Test]
    public void Teleprojector_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("The teleprojector attunes to your physiology.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("The teleprojector attunes to your physiology."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Teleprojector_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertTeleprojectorPopup(
            nameof(DummyTeleprojectorTarget.HandleEvent),
            MessageFrameTranslator.MarkDirectTranslation("The teleprojector attunes to your physiology."),
            "The teleprojector",
            "The teleprojector attunes to your physiology.");
    }

    [Test]
    public void Teleprojector_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertTeleprojectorPopup(nameof(DummyTeleprojectorTarget.HandleEvent), string.Empty, string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyTombAnchorSystemTarget.OnEndTurn),
        "&MThe Bell of Rest tolls! The dead will be recalled in 12 rounds.",
        null,
        "&M安息の鐘が鳴る！死者は12ラウンド後に呼び戻される。")]
    [TestCase(
        nameof(DummyTombAnchorSystemTarget.Recall),
        "You've been recalled to a resting place.",
        "M",
        "安息の場所へ呼び戻された。")]
    [TestCase(
        nameof(DummyTombAnchorSystemTarget.AnchorCall),
        "You were not recalled as you're already in a resting place.",
        "M",
        "すでに安息の地にいるため呼び戻されなかった。")]
    public void TombAnchorSystem_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertTombAnchorSystemQueuedMessage(methodName, source, color, expected);
    }

    [Test]
    public void TombAnchorSystem_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You've been recalled to a resting place.", "M", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You've been recalled to a resting place."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TombAnchorSystem_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertTombAnchorSystemQueuedMessage(
            nameof(DummyTombAnchorSystemTarget.Recall),
            MessageFrameTranslator.MarkDirectTranslation("You've been recalled to a resting place."),
            "M",
            "You've been recalled to a resting place.");
    }

    [Test]
    public void TombAnchorSystem_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertTombAnchorSystemQueuedMessage(
            nameof(DummyTombAnchorSystemTarget.Recall),
            string.Empty,
            "M",
            string.Empty);
    }

    [TestCase(
        "You slot {{G|salve tonic}} into {{Y|your medassist module}}.",
        "{{G|salve tonic}}",
        "{{G|salve tonic}}を{{Y|your medassist module}}に装填した。")]
    [TestCase(
        "You eject {{G|the injectors}} from {{Y|your medassist module}}.",
        "{{G|the injectors}}",
        "{{G|the injectors}}を{{Y|your medassist module}}から排出した。")]
    public void CyberneticsMedassistModule_TranslatesPopupMessages_WhenOwnerPatched(
        string source,
        string captureToken,
        string expected)
    {
        AssertCyberneticsMedassistModulePopup(source, captureToken, expected);
    }

    [TestCase(
        "Your {{Y|medassist module}} injects you with {{G|a salve tonic}}.",
        "{{G|a salve tonic}}",
        "あなたの{{Y|medassist module}}が{{G|a salve tonic}}を注射した。")]
    [TestCase("The injection fails.", "injection", "注射は失敗に終わった。")]
    public void CyberneticsMedassistModule_TranslatesQueuedMessages_WhenOwnerPatched(
        string source,
        string captureToken,
        string expected)
    {
        AssertCyberneticsMedassistModuleQueuedMessage(source, captureToken, expected);
    }

    [Test]
    public void CyberneticsMedassistModule_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You slot salve tonic into your medassist module.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You slot salve tonic into your medassist module."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CyberneticsMedassistModule_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The injection fails.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The injection fails."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CyberneticsMedassistModule_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertCyberneticsMedassistModulePopup(
            MessageFrameTranslator.MarkDirectTranslation("You slot salve tonic into your medassist module."),
            "salve tonic",
            "You slot salve tonic into your medassist module.");
        AssertCyberneticsMedassistModuleQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The injection fails."),
            "injection",
            "The injection fails.");
    }

    [Test]
    public void CyberneticsMedassistModule_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertCyberneticsMedassistModulePopup(string.Empty, string.Empty, string.Empty);
        AssertCyberneticsMedassistModuleQueuedMessage(string.Empty, string.Empty, string.Empty);
    }

    [TestCase("{{Y|The biodynamic power plant}} is already full.", "{{Y|biodynamic power plant}}はすでに満杯だ。")]
    [TestCase("{{Y|The liquid-cooled chain pistol}} is already full of {{B|water}}.", "{{Y|liquid-cooled chain pistol}}はすでに{{B|water}}で満杯だ。")]
    [TestCase("You have no {{B|water}} for {{Y|the chain pistol}}.", "{{Y|chain pistol}}用の{{B|water}}がない。")]
    [TestCase("You dump the {{B|water}} out of {{Y|the chain pistol}}.", "{{Y|chain pistol}}から{{B|water}}を捨てた。")]
    [TestCase("You partially fill {{Y|the chain pistol}} with {{B|water}}.", "{{Y|chain pistol}}を{{B|water}}で部分的に満たした。")]
    [TestCase("You fill {{Y|the chain pistol}} with {{B|water}}.", "{{Y|chain pistol}}を{{B|water}}で満たした。")]
    [TestCase("You inspect the chain pistol.", "You inspect the chain pistol.")]
    [TestCase("", "")]
    [TestCase("\u0001You have no water for the chain pistol.", "You have no water for the chain pistol.")]
    public void LiquidLoader_TranslatesQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertLiquidLoaderQueuedMessage(source, expected);
    }

    [TestCase(typeof(DummyCheckLoadAmmoEvent))]
    [TestCase(typeof(DummyLoadAmmoEvent))]
    [TestCase(typeof(DummyGetNotReadyToFireMessageEvent))]
    public void LiquidLoader_TranslatesBioAmmoEventMessages_WhenOwnerPatched(Type eventType)
    {
        AssertLiquidLoaderEventMessage(
            eventType,
            "{{Y|The bio ammo rack}} is exhausted!",
            "{{Y|bio ammo rack}}は疲弊した！");
    }

    [TestCase(typeof(DummyCheckLoadAmmoEvent), "The bio ammo rack is exhausted!", "\u0001bio ammo rackは疲弊した！")]
    [TestCase(typeof(DummyCheckLoadAmmoEvent), "The bio ammo rack hums.", "The bio ammo rack hums.")]
    [TestCase(typeof(DummyCheckLoadAmmoEvent), "", "")]
    [TestCase(typeof(DummyCheckLoadAmmoEvent), "\u0001The bio ammo rack is exhausted!", "\u0001The bio ammo rack is exhausted!")]
    [TestCase(typeof(DummyLoadAmmoEvent), "The bio ammo rack is exhausted!", "\u0001bio ammo rackは疲弊した！")]
    [TestCase(typeof(DummyLoadAmmoEvent), "The bio ammo rack hums.", "The bio ammo rack hums.")]
    [TestCase(typeof(DummyLoadAmmoEvent), "", "")]
    [TestCase(typeof(DummyLoadAmmoEvent), "\u0001The bio ammo rack is exhausted!", "\u0001The bio ammo rack is exhausted!")]
    [TestCase(typeof(DummyGetNotReadyToFireMessageEvent), "The bio ammo rack is exhausted!", "\u0001bio ammo rackは疲弊した！")]
    [TestCase(typeof(DummyGetNotReadyToFireMessageEvent), "The bio ammo rack hums.", "The bio ammo rack hums.")]
    [TestCase(typeof(DummyGetNotReadyToFireMessageEvent), "", "")]
    [TestCase(typeof(DummyGetNotReadyToFireMessageEvent), "\u0001The bio ammo rack is exhausted!", "\u0001The bio ammo rack is exhausted!")]
    public void LiquidLoader_HandlesBioAmmoEventMessageEdgeCases_WhenOwnerPatched(
        Type eventType,
        string source,
        string expectedFieldValue)
    {
        AssertLiquidLoaderEventMessage(eventType, source, expectedFieldValue, expectedIsMarked: false);
    }

    [TestCase("You have no {{B|water}} to supply {{Y|the host}} with.", "{{Y|host}}に供給する{{B|water}}がない。")]
    [TestCase("{{Y|The host}} has no room for more {{B|water}}.", "{{Y|host}}にはこれ以上{{B|water}}を入れる余地がない。")]
    [TestCase("You inspect the host.", "You inspect the host.")]
    [TestCase("", "")]
    [TestCase("\u0001You have no water to supply the host with.", "You have no water to supply the host with.")]
    public void LiquidLoader_TranslatesPopupMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertLiquidLoaderPopup(source, expected);
    }

    [Test]
    public void LiquidLoader_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You have no water for the chain pistol.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You have no water for the chain pistol."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidLoader_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You have no water to supply the host with.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You have no water to supply the host with."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidLoader_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertLiquidLoaderQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You have no water for the chain pistol."),
            "You have no water for the chain pistol.");
        AssertLiquidLoaderPopup(
            MessageFrameTranslator.MarkDirectTranslation("You have no water to supply the host with."),
            "You have no water to supply the host with.");
    }

    [Test]
    public void LiquidLoader_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertLiquidLoaderQueuedMessage(string.Empty, string.Empty);
        AssertLiquidLoaderPopup(string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyTrollKingTarget.CheckSpawn),
        "A grotesque protuberance swells from {{G|the troll king's back}} as {{G|he}} begins to bud!",
        "{{G|the troll king's back}}",
        "{{G|the troll king's back}}からグロテスクな突起が膨らみ、{{G|he}}が芽吹き始めた！")]
    [TestCase(
        nameof(DummyTrollKingTarget.CheckSpawn),
        "The protuberance on {{G|the troll king's back}} shrinks.",
        "{{G|the troll king's back}}",
        "{{G|the troll king's back}}の突起が縮んだ。")]
    [TestCase(
        nameof(DummyTrollKingTarget.StopBudding),
        "The protuberance on {{G|the troll king}} shrinks.",
        "{{G|the troll king}}",
        "{{G|the troll king}}の突起が縮んだ。")]
    public void TrollKing_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string captureToken,
        string expected)
    {
        AssertTrollKingQueuedMessage(methodName, source, captureToken, expected);
    }

    [Test]
    public void TrollKing_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(
                "The protuberance on the troll king shrinks.",
                null,
                Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The protuberance on the troll king shrinks."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TrollKing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertTrollKingQueuedMessage(
            nameof(DummyTrollKingTarget.StopBudding),
            MessageFrameTranslator.MarkDirectTranslation("The protuberance on the troll king shrinks."),
            "the troll king",
            "The protuberance on the troll king shrinks.");
    }

    [Test]
    public void TrollKing_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertTrollKingQueuedMessage(
            nameof(DummyTrollKingTarget.StopBudding),
            string.Empty,
            string.Empty,
            string.Empty);
    }

    [TestCase(
        nameof(DummyMutatingTarget.Apply),
        "You start to feel unstable.",
        "M",
        "不安定になり始めた。")]
    [TestCase(
        nameof(DummyMutatingTarget.HandleEvent),
        "You feel increasingly unstable.",
        "M",
        "ますます不安定になってきた。")]
    public void Mutating_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertMutatingQueuedMessage(methodName, source, color, expected);
    }

    [TestCase(
        "Your genome destabilizes and you gain a new mutation:\n\n{{W|Wings}}",
        "{{W|Wings}}",
        "ゲノムが不安定化し、新しい変異を得た:\n\n{{W|Wings}}")]
    [TestCase(
        "Your genome destabilizes and you gain a new defect:\n\n{{W|Amphibious}}",
        "{{W|Amphibious}}",
        "ゲノムが不安定化し、新しい欠陥を得た:\n\n{{W|Amphibious}}")]
    [TestCase(
        "Your genome destabilizes and you gain 2 mutation points.",
        "2",
        "ゲノムが不安定化し、変異ポイントを2得た。")]
    public void Mutating_TranslatesPopupMessages_WhenOwnerPatched(
        string source,
        string captureToken,
        string expected)
    {
        AssertMutatingPopup(source, captureToken, expected);
    }

    [Test]
    public void Mutating_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You start to feel unstable.", "M", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You start to feel unstable."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Mutating_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("Your genome destabilizes and you gain 2 mutation points.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Your genome destabilizes and you gain 2 mutation points."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Mutating_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertMutatingQueuedMessage(
            nameof(DummyMutatingTarget.Apply),
            MessageFrameTranslator.MarkDirectTranslation("You start to feel unstable."),
            "M",
            "You start to feel unstable.");
        AssertMutatingPopup(
            MessageFrameTranslator.MarkDirectTranslation("Your genome destabilizes and you gain 2 mutation points."),
            "2",
            "Your genome destabilizes and you gain 2 mutation points.");
    }

    [Test]
    public void Mutating_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertMutatingQueuedMessage(nameof(DummyMutatingTarget.Apply), string.Empty, "M", string.Empty);
        AssertMutatingPopup(string.Empty, string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyQuillsTarget.HandleEvent),
        "{{G|The snapjaw}} impales {{G|itself}} on {{Y|your quills}} and takes 3 damage!",
        "{{Y|your quills}}",
        "{{G|The snapjaw}}は{{G|itself}}を{{Y|your quills}}に突き刺し、3ダメージを受けた！")]
    [TestCase(
        nameof(DummyQuillsTarget.FireEvent),
        "The attack breaks {{W|two}} {{Y|quills}}!",
        "{{Y|quills}}",
        "攻撃で{{W|two}}本の{{Y|quills}}が折れた！")]
    public void Quills_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string captureToken,
        string expected)
    {
        AssertQuillsQueuedMessage(methodName, source, captureToken, expected);
    }

    [Test]
    public void Quills_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(
                "The attack breaks two quills!",
                null,
                Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The attack breaks two quills!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Quills_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQuillsQueuedMessage(
            nameof(DummyQuillsTarget.FireEvent),
            MessageFrameTranslator.MarkDirectTranslation("The attack breaks two quills!"),
            "quills",
            "The attack breaks two quills!");
    }

    [Test]
    public void Quills_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertQuillsQueuedMessage(nameof(DummyQuillsTarget.FireEvent), string.Empty, string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyLightManipulationTarget.HandleEvent),
        "The darkness absorbs the laser beam.",
        null,
        "闇がレーザービームを吸収した。")]
    [TestCase(
        nameof(DummyLightManipulationTarget.HandleEvent),
        "You must wait {{C|7 turns}} before you can enable ambient light.",
        null,
        "環境光を有効化するには{{C|7 turns}}待つ必要がある。")]
    [TestCase(
        nameof(DummyLightManipulationTarget.Lase),
        "Your laser beam doesn't penetrate {{Y|the snapjaw's armor}}.",
        "r",
        "あなたのレーザービームは{{Y|the snapjaw's armor}}を貫通しなかった。")]
    [TestCase(
        nameof(DummyLightManipulationTarget.Lase),
        "{{Y|The snapjaw's laser beam}} doesn't penetrate your armor.",
        "g",
        "{{Y|The snapjaw's laser beam}}はあなたの装甲を貫通しなかった。")]
    public void LightManipulation_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string? color,
        string expected)
    {
        AssertLightManipulationQueuedMessage(methodName, source, color, expected);
    }

    [Test]
    public void LightManipulation_TranslatesPopupMessage_WhenOwnerPatched()
    {
        AssertLightManipulationPopup(
            "You must wait {{C|7 turns}} before you can enable ambient light.",
            "環境光を有効化するには{{C|7 turns}}待つ必要がある。");
    }

    [Test]
    public void LightManipulation_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The darkness absorbs the laser beam.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The darkness absorbs the laser beam."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LightManipulation_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail("You must wait {{C|7 turns}} before you can enable ambient light.");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("You must wait {{C|7 turns}} before you can enable ambient light."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LightManipulation_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertLightManipulationQueuedMessage(
            nameof(DummyLightManipulationTarget.HandleEvent),
            MessageFrameTranslator.MarkDirectTranslation("The darkness absorbs the laser beam."),
            null,
            "The darkness absorbs the laser beam.");
        AssertLightManipulationPopup(
            MessageFrameTranslator.MarkDirectTranslation("You must wait {{C|7 turns}} before you can enable ambient light."),
            "You must wait {{C|7 turns}} before you can enable ambient light.");
    }

    [Test]
    public void LightManipulation_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertLightManipulationQueuedMessage(nameof(DummyLightManipulationTarget.HandleEvent), string.Empty, null, string.Empty);
        AssertLightManipulationPopup(string.Empty, string.Empty);
    }

    [TestCase(
        "You kick at {{G|phase spider}}, but the kick passes through {{G|it}}.",
        "{{G|phase spider}}を蹴ろうとしたが、蹴りは{{G|it}}を通り抜けた。")]
    [TestCase(
        "snapjaw kicks at you, but the kick passes through you.",
        "snapjawがあなたを蹴ろうとしたが、蹴りはあなたを通り抜けた。")]
    [TestCase(
        "snapjaw kicks at phase spider, but the kick passes through it.",
        "snapjawがphase spiderを蹴ろうとしたが、蹴りはitを通り抜けた。")]
    [TestCase(
        "You kick at snapjaw, but snapjaw holds its ground.",
        "snapjawを蹴ろうとしたが、snapjawは踏みとどまった。")]
    [TestCase(
        "snapjaw kicks at you, but you hold your ground.",
        "snapjawがあなたを蹴ろうとしたが、あなたは踏みとどまった。")]
    [TestCase(
        "snapjaw kicks at {{G|glowfish}}, but {{G|glowfish}} holds its ground.",
        "snapjawが{{G|glowfish}}を蹴ろうとしたが、{{G|glowfish}}は踏みとどまった。")]
    [TestCase("You kick snapjaw backwards.", "snapjawを後ろへ蹴り飛ばした。")]
    [TestCase("snapjaw kicks you backwards.", "snapjawがあなたを後ろへ蹴り飛ばした。")]
    [TestCase("snapjaw kicks glowfish backwards.", "snapjawがglowfishを後ろへ蹴り飛ばした。")]
    [TestCase(
        "The momentum from your charge causes your {{Y|battle axe}} to cleave deeper through {{R|snapjaw's armor}}.",
        "突撃の勢いで{{Y|battle axe}}が{{R|snapjawのarmor}}をさらに深く切り裂いた。")]
    [TestCase("You cleave through snapjaw's armor.", "snapjawのarmorを切り裂いた。")]
    [TestCase("snapjaw cleaves through your armor.", "snapjawがあなたのarmorを切り裂いた。")]
    [TestCase("snapjaw cleaves through glowfish's armor.", "snapjawがglowfishのarmorを切り裂いた。")]
    [TestCase("You shook off the stun.", "スタンを振り払った。")]
    [TestCase("You shook off the dazing.", "朦朧を振り払った。")]
    [TestCase("The snapjaw shook off the stun.", "snapjawはスタンを振り払った。")]
    [TestCase("The snapjaw shook off the dazing.", "snapjawは朦朧を振り払った。")]
    [TestCase(
        "A supernal force helps you shake off the effect!",
        "超自然的な力が効果を振り払う助けとなった！")]
    [TestCase(
        "A supernal force helps you shake off being confused!",
        "超自然的な力がconfused状態を振り払う助けとなった！")]
    [TestCase(
        "A supernal force helps you shake off a mental state!",
        "超自然的な力が精神状態を振り払う助けとなった！")]
    [TestCase("You backswing with {{Y|your cudgel}}.", "{{Y|あなたのcudgel}}で返し打ちした。")]
    [TestCase(
        "{{G|You prepare {{Y|your cudgel}} for demolition.}}",
        "{{G|{{Y|あなたのcudgel}}を破壊のために構えた。}}")]
    [TestCase("The snapjaw backswings with {{Y|its cudgel}}.", "snapjawが{{Y|そのcudgel}}で返し打ちした。")]
    [TestCase(
        "You muster your will and shake off some of your confusion.",
        "意志の力で混乱の一部を振り払った。")]
    [TestCase(
        "You muster your will and shake off your confusion.",
        "意志の力で混乱を振り払った。")]
    [TestCase("You lose sight of your mark.", "標的を見失った。")]
    [TestCase("Your tracking of your mark has been disrupted.", "印付けの追跡が乱された。")]
    [TestCase("The snapjaw resists your shield slam.", "snapjawはあなたのシールドスラムに抵抗した。")]
    [TestCase("You resist {{R|the snapjaw's shield slam}}.", "{{R|snapjawのシールドスラム}}に抵抗した。")]
    [TestCase("You rejoinder with {{Y|your dagger}}.", "{{Y|あなたのdagger}}で反撃した。")]
    [TestCase("The snapjaw rejoinders with {{Y|its dagger}}.", "snapjawが{{Y|そのdagger}}で反撃した。")]
    public void CombatSkillMessages_TranslateInventoriedQueuedShapes_WhenOwnerPatched(string source, string expected)
    {
        AssertCombatSkillQueuedMessage(source, expected);
    }

    [TestCase("snapjaw kicks at you, but the kick passes through you.")]
    [TestCase("You cleave through snapjaw's armor.")]
    [TestCase("You shook off the stun.")]
    [TestCase("A supernal force helps you shake off the effect!")]
    [TestCase("You lose sight of your mark.")]
    [TestCase("{{G|You prepare {{Y|your cudgel}} for demolition.}}")]
    [TestCase("You rejoinder with {{Y|your dagger}}.")]
    public void CombatSkillMessages_DoNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatSkillMessages_LeavesUnknownQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertCombatSkillQueuedMessage("snapjaw prepares a fancy combat maneuver.", "snapjaw prepares a fancy combat maneuver.");
    }

    [Test]
    public void CombatSkillMessages_DoNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertCombatSkillQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You cleave through snapjaw's armor."),
            "You cleave through snapjaw's armor.");
    }

    [Test]
    public void CombatSkillMessages_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertCombatSkillQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void CombatSkillMessages_RestoresOuterOwnerScopeAfterNestedScopeExit()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            CombatSkillMessageTranslationPatch.Prefix();
            try
            {
                CombatSkillMessageTranslationPatch.Prefix();
                CombatSkillMessageTranslationPatch.Finalizer(null);

                DummyMessageQueue.AddPlayerMessage("You cleave through snapjaw's armor.", null, Capitalize: false);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjawのarmorを切り裂いた。"));
                    Assert.That(CombatSkillHitCount(), Is.EqualTo(1));
                });
            }
            finally
            {
                CombatSkillMessageTranslationPatch.Finalizer(null);
            }

            DummyMessageQueue.AddPlayerMessage("You cleave through snapjaw's armor.", null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You cleave through snapjaw's armor."));
                Assert.That(CombatSkillHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        nameof(DummyLatchesOnTarget.HandleEvent),
        "Since {{R|the hook}} is still latched onto {{G|the snapjaw}}, releasing {{R|it}} leaves {{R|it}} in {{G|its possession}}!",
        "{{G|the snapjaw}}",
        "{{R|the hook}}はまだ{{G|the snapjaw}}に噛み付いているため、{{R|it}}を放すと{{R|it}}は{{G|its possession}}に残る！")]
    [TestCase(
        nameof(DummyLatchesOnTarget.HandleEvent),
        "Since {{R|the hook}} is still latched onto you, {{G|the snapjaw}} releasing {{R|it}} leaves {{R|it}} in your possession!",
        "{{R|the hook}}",
        "{{R|the hook}}はまだあなたに噛み付いているため、{{G|the snapjaw}}が{{R|it}}を放すと{{R|it}}はあなたの所有物として残る！")]
    [TestCase(
        nameof(DummyLatchesOnTarget.FireEvent),
        "{{R|The barbed hook}}{{R| latches}} onto your {{G|steel shield}}{{R|!}}",
        "{{G|steel shield}}",
        "{{R|The barbed hook}}がyour {{G|steel shield}}に噛み付いた！")]
    [TestCase(
        nameof(DummyLatchesOnTarget.FireEvent),
        "{{R|The snapjaw's barbed hook}}{{R| latches}} onto {{G|the watervine}}{{R|!}}",
        "{{G|the watervine}}",
        "{{R|The snapjaw's barbed hook}}が{{G|the watervine}}に噛み付いた！")]
    public void LatchesOn_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string captureToken,
        string expected)
    {
        AssertLatchesOnQueuedMessage(methodName, source, captureToken, expected);
    }

    [Test]
    public void LatchesOn_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The barbed hook latches onto you!", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The barbed hook latches onto you!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LatchesOn_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertLatchesOnQueuedMessage(
            nameof(DummyLatchesOnTarget.FireEvent),
            MessageFrameTranslator.MarkDirectTranslation("The barbed hook latches onto you!"),
            "you",
            "The barbed hook latches onto you!");
    }

    [Test]
    public void LatchesOn_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertLatchesOnQueuedMessage(nameof(DummyLatchesOnTarget.FireEvent), string.Empty, string.Empty, string.Empty);
    }

    [TestCase(
        "Apply",
        "You enter {{C|sleep mode}}.",
        "あなたは{{C|スリープモード}}に入った。")]
    [TestCase(
        "Apply",
        "{{G|The robot}} goes into {{C|sleep mode}}.",
        "{{G|The robot}}は{{C|スリープモード}}に入った。")]
    [TestCase(
        "Apply",
        "You fall {{C|asleep}}!",
        "あなたは{{C|眠り}}に落ちた。")]
    [TestCase(
        "BeginTakeAction",
        "You are asleep.",
        "眠っている。")]
    [TestCase(
        "InventoryAction",
        "{{G|The snapjaw}} presses your activation panel.",
        "{{G|The snapjaw}}はあなたの起動パネルを押した。")]
    [TestCase(
        "InventoryAction",
        "{{G|The snapjaw}} gently shakes you awake.",
        "{{G|The snapjaw}}はyouをやさしく揺り起こした。")]
    public void AsleepOwner_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodKey,
        string source,
        string expected)
    {
        AssertAsleepOwnerQueuedMessage(methodKey, source, expected);
    }

    [TestCase(
        "You press {{C|the robot's activation panel}}.",
        "あなたは{{C|the robot's activation panel}}を押した。")]
    [TestCase(
        "You gently shake {{G|the snapjaw}} awake.",
        "あなたは{{G|the snapjaw}}をやさしく揺り起こした。")]
    [TestCase(
        "You can't figure out how to wake {{G|the robot}}.",
        "あなたには{{G|the robot}}を起こす方法がわからない。")]
    public void AsleepOwner_TranslatesPopupMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertAsleepOwnerPopup(source, expected);
    }

    [Test]
    public void AsleepOwner_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You are asleep.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You are asleep."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AsleepOwner_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You gently shake the snapjaw awake.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You gently shake the snapjaw awake."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AsleepOwner_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertAsleepOwnerQueuedMessage(
            "BeginTakeAction",
            MessageFrameTranslator.MarkDirectTranslation("You are asleep."),
            "You are asleep.");
        AssertAsleepOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation("You gently shake the snapjaw awake."),
            "You gently shake the snapjaw awake.");
    }

    [Test]
    public void AsleepOwner_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertAsleepOwnerQueuedMessage("BeginTakeAction", string.Empty, string.Empty);
        AssertAsleepOwnerPopup(string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyBuddingTarget.Apply),
        "A grotesque protuberance swells from {{G|the snapjaw's back}} as {{G|it}} begins to bud!",
        "{{G|the snapjaw's back}}",
        "{{G|the snapjaw's back}}からグロテスクな突起が膨らみ、{{G|it}}が芽吹き始めた！")]
    [TestCase(
        nameof(DummyBuddingTarget.Apply),
        "A grotesque protuberance swells from {{G|the snapjaw}} as {{G|it}} begins to bud!",
        "{{G|the snapjaw}}",
        "{{G|the snapjaw}}からグロテスクな突起が膨らみ、{{G|it}}が芽吹き始めた！")]
    [TestCase(
        nameof(DummyBuddingTarget.Remove),
        "The grotesque protuberance on {{G|the snapjaw's back}} subsides.",
        "{{G|the snapjaw's back}}",
        "{{G|the snapjaw's back}}のグロテスクな突起が引っ込んだ。")]
    [TestCase(
        nameof(DummyBuddingTarget.Remove),
        "{{G|The snapjaw's grotesque protuberance}} subsides.",
        "{{G|The snapjaw's grotesque protuberance}}",
        "{{G|The snapjaw's grotesque protuberance}}が引っ込んだ。")]
    public void Budding_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string captureToken,
        string expected)
    {
        AssertBuddingQueuedMessage(methodName, source, captureToken, expected);
    }

    [Test]
    public void Budding_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(
                "The grotesque protuberance on the snapjaw's back subsides.",
                null,
                Capitalize: false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("The grotesque protuberance on the snapjaw's back subsides."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Budding_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertBuddingQueuedMessage(
            nameof(DummyBuddingTarget.Remove),
            MessageFrameTranslator.MarkDirectTranslation("The grotesque protuberance on the snapjaw's back subsides."),
            "the snapjaw's back",
            "The grotesque protuberance on the snapjaw's back subsides.");
    }

    [Test]
    public void Budding_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertBuddingQueuedMessage(nameof(DummyBuddingTarget.Remove), string.Empty, string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyBeguilingTarget.Cast),
        "You can't beguile {{R|yourself}}!",
        "{{R|yourself}}を魅了できない！")]
    [TestCase(
        nameof(DummyBeguilingTarget.Cast),
        "{{G|The snapjaw}} seems utterly impervious to your charms.",
        "{{G|The snapjaw}}はあなたの魅力にまったく動じない。")]
    [TestCase(
        nameof(DummyBeguilingTarget.Cast),
        "You have already beguiled {{G|the snapjaw}}.",
        "すでに{{G|the snapjaw}}を魅了している。")]
    [TestCase(
        nameof(DummyBeguilingTarget.Cast),
        "You fail to outshine the current object of {{G|the snapjaw's affection}}.",
        "{{G|the snapjaw's affection}}の現在の想い人を上回れなかった。")]
    [TestCase(
        nameof(DummyBeguilingTarget.Beguile),
        "Your coquetry infuriates {{R|the snapjaw}}.",
        "{{R|the snapjaw}}を口説こうとして怒らせた。")]
    public void Beguiling_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertBeguilingQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void Beguiling_TranslatesPopupMessage_WhenOwnerPatched()
    {
        AssertBeguilingPopup(
            "{{G|The snapjaw}} is already your follower. Do you want to beguile {{G|it}} anyway?",
            "{{G|The snapjaw}}はすでにあなたの仲間だ。それでも{{G|it}}を魅了しますか？");
    }

    [Test]
    public void Beguiling_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your coquetry infuriates the snapjaw.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your coquetry infuriates the snapjaw."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Beguiling_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);

            DummyPopupShow.ShowYesNo("The snapjaw is already your follower. Do you want to beguile it anyway?");

            Assert.That(
                DummyPopupShow.LastShowYesNoMessage,
                Is.EqualTo("The snapjaw is already your follower. Do you want to beguile it anyway?"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Beguiling_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        AssertBeguilingQueuedMessage(
            nameof(DummyBeguilingTarget.Beguile),
            MessageFrameTranslator.MarkDirectTranslation("Your coquetry infuriates the snapjaw."),
            "Your coquetry infuriates the snapjaw.");
        AssertBeguilingPopup(
            MessageFrameTranslator.MarkDirectTranslation("The snapjaw is already your follower. Do you want to beguile it anyway?"),
            "The snapjaw is already your follower. Do you want to beguile it anyway?");
    }

    [Test]
    public void Beguiling_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        AssertBeguilingQueuedMessage(nameof(DummyBeguilingTarget.Beguile), string.Empty, string.Empty);
        AssertBeguilingPopup(string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummyAscensionCableTarget.TryAscend),
        "You don't have the capacity to ascend {{G|the cable}}.",
        "{{G|the cable}}を上昇する能力がない。")]
    [TestCase(
        nameof(DummyAscensionCableTarget.TryAscend),
        "You can't safely ascend {{G|the cable}} right now.",
        "今は安全に{{G|the cable}}を上昇できない。")]
    [TestCase(
        nameof(DummyAscensionCableTarget.TryDescend),
        "You don't have the capacity to descend {{G|the cable}}.",
        "{{G|the cable}}を下降する能力がない。")]
    [TestCase(
        nameof(DummyAscensionCableTarget.TryDescend),
        "You can't safely descend {{G|the cable}} right now.",
        "今は安全に{{G|the cable}}を下降できない。")]
    public void AscensionCable_TranslatesPopupMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertAscensionCablePopup(methodName, source, expected);
    }

    [Test]
    public void AscensionCable_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You can't safely ascend the cable right now.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You can't safely ascend the cable right now."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AscensionCable_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertAscensionCablePopup(
            nameof(DummyAscensionCableTarget.TryAscend),
            MessageFrameTranslator.MarkDirectTranslation("You can't safely ascend the cable right now."),
            "You can't safely ascend the cable right now.");
    }

    [Test]
    public void AscensionCable_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertAscensionCablePopup(nameof(DummyAscensionCableTarget.TryAscend), string.Empty, string.Empty);
    }

    [TestCase(
        "You tighten your carapace. Your AV increases by {{G|3}}.",
        "甲羅を引き締めた。AVが{{G|3}}増加する。")]
    [TestCase(
        "You tighten {{W|your shell}}. Your AV increases by {{G|2}}.",
        "{{W|your shell}}を引き締めた。AVが{{G|2}}増加する。")]
    public void CarapaceTighten_TranslatesPopupMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertCarapaceTightenPopup(source, expected);
    }

    [Test]
    public void CarapaceTighten_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You tighten your carapace. Your AV increases by {{G|3}}.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You tighten your carapace. Your AV increases by {{G|3}}."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CarapaceTighten_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertCarapaceTightenPopup(
            MessageFrameTranslator.MarkDirectTranslation("You tighten your carapace. Your AV increases by {{G|3}}."),
            "You tighten your carapace. Your AV increases by {{G|3}}.");
    }

    [Test]
    public void CarapaceTighten_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertCarapaceTightenPopup(string.Empty, string.Empty);
    }

    [TestCase(
        "Your carapace loosens. Your AV decreases by {{R|3}}.",
        "甲羅が緩んだ。AVが{{R|3}}低下する。")]
    [TestCase(
        "{{W|your shell}} loosens. Your AV decreases by {{R|2}}.",
        "{{W|your shell}}が緩んだ。AVが{{R|2}}低下する。")]
    public void CarapaceLoosen_TranslatesPopupMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertCarapaceLoosenPopup(source, expected);
    }

    [Test]
    public void CarapaceLoosen_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("Your carapace loosens. Your AV decreases by {{R|3}}.");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("Your carapace loosens. Your AV decreases by {{R|3}}."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CarapaceLoosen_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertCarapaceLoosenPopup(
            MessageFrameTranslator.MarkDirectTranslation("Your carapace loosens. Your AV decreases by {{R|3}}."),
            "Your carapace loosens. Your AV decreases by {{R|3}}.");
    }

    [TestCase("")]
    [TestCase("Your carapace loosens.")]
    public void CarapaceLoosen_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched(string source)
    {
        AssertCarapaceLoosenPopup(source, source);
    }

    [TestCase(
        nameof(DummySvardymSystemTarget.BeginStorm),
        "Your hear a swelling thpthp sound.",
        "thpthpという音が大きくなっていくのが聞こえる。")]
    [TestCase(
        nameof(DummySvardymSystemTarget.BeginStorm),
        "The sky begins to darken.",
        "空が暗くなり始める。")]
    [TestCase(
        nameof(DummySvardymSystemTarget.Tick),
        "The thpthp sound wanes.",
        "thpthpという音が弱まる。")]
    [TestCase(
        nameof(DummySvardymSystemTarget.Tick),
        "The sky begins to brighten.",
        "空が明るくなり始める。")]
    public void SvardymSystem_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertSvardymSystemQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void SvardymSystem_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The sky begins to darken.");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The sky begins to darken."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SvardymSystem_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertSvardymSystemQueuedMessage(
            nameof(DummySvardymSystemTarget.BeginStorm),
            MessageFrameTranslator.MarkDirectTranslation("The sky begins to darken."),
            "The sky begins to darken.");
    }

    [Test]
    public void SvardymSystem_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertSvardymSystemQueuedMessage(nameof(DummySvardymSystemTarget.BeginStorm), string.Empty, string.Empty);
    }

    [TestCase("EffectApplied", "You phase out.", "位相がずれた。")]
    [TestCase("BeginTakeAction", "You will phase back in in {{C|3 rounds}}.", "{{C|3 rounds}}後に位相が戻る。")]
    [TestCase("Remove", "You phase back in.", "位相が戻った。")]
    public void Phased_TranslatesQueuedMessages_WhenOwnerPatched(
        string methodKey,
        string source,
        string expected)
    {
        AssertPhasedQueuedMessage(methodKey, source, expected);
    }

    [Test]
    public void Phased_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You phase out.");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You phase out."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Phased_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertPhasedQueuedMessage(
            "EffectApplied",
            MessageFrameTranslator.MarkDirectTranslation("You phase out."),
            "You phase out.");
    }

    [Test]
    public void Phased_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertPhasedQueuedMessage("EffectApplied", string.Empty, string.Empty);
    }

    [Test]
    public void PersuasionRebukeRobot_TranslatesRebukeFailureMessage_WhenOwnerPatched()
    {
        AssertPersuasionRebukeRobotQueuedMessage(
            "Your argument does not compute.",
            "あなたの論理は処理されなかった。");
    }

    [Test]
    public void PersuasionRebukeRobot_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your argument does not compute.");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your argument does not compute."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PersuasionRebukeRobot_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertPersuasionRebukeRobotQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your argument does not compute."),
            "Your argument does not compute.");
    }

    [Test]
    public void PersuasionRebukeRobot_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertPersuasionRebukeRobotQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        "{{G|Saad Amus}} slouches in pacification and radiates a chord of light.",
        "{{G|Saad Amus}}は平伏し、光の和音を放った。")]
    [TestCase(
        "you slouch in pacification and radiate a chord of light.",
        "youは平伏し、光の和音を放った。")]
    public void NephalPropertiesTryPacify_TranslatesPopupMessage_WhenOwnerPatched(string source, string expected)
    {
        AssertNephalPropertiesTryPacifyPopup(source, expected);
    }

    [Test]
    public void NephalPropertiesTryPacify_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("Saad Amus slouches in pacification and radiates a chord of light.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Saad Amus slouches in pacification and radiates a chord of light."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void NephalPropertiesTryPacify_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertNephalPropertiesTryPacifyPopup(
            MessageFrameTranslator.MarkDirectTranslation("Saad Amus slouches in pacification and radiates a chord of light."),
            "Saad Amus slouches in pacification and radiates a chord of light.");
    }

    [Test]
    public void NephalPropertiesTryPacify_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertNephalPropertiesTryPacifyPopup(string.Empty, string.Empty);
    }

    [TestCase(
        "You have no ammunition to supply {{Y|chain turret}} with. It may be ineffective unless stocked.",
        "{{Y|chain turret}}に補給する弾薬がない。補充しない限りItは効果が薄いかもしれない。")]
    [TestCase(
        "You have no ammunition to supply the turret with. it may be ineffective unless stocked.",
        "the turretに補給する弾薬がない。補充しない限りitは効果が薄いかもしれない。")]
    public void IntegratedWeaponHostsGenerateTurret_TranslatesNoAmmoPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertIntegratedWeaponHostsGenerateTurretPopup(source, expected);
    }

    [Test]
    public void IntegratedWeaponHostsGenerateTurret_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You have no ammunition to supply chain turret with. It may be ineffective unless stocked.");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("You have no ammunition to supply chain turret with. It may be ineffective unless stocked."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void IntegratedWeaponHostsGenerateTurret_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertIntegratedWeaponHostsGenerateTurretPopup(
            MessageFrameTranslator.MarkDirectTranslation("You have no ammunition to supply chain turret with. It may be ineffective unless stocked."),
            "You have no ammunition to supply chain turret with. It may be ineffective unless stocked.");
    }

    [Test]
    public void IntegratedWeaponHostsGenerateTurret_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertIntegratedWeaponHostsGenerateTurretPopup(string.Empty, string.Empty);
    }

    [TestCase(
        "Could not generate turret from blueprint \"PhaseCannon\"\n\nSystem.Exception: missing blueprint",
        "blueprint \"PhaseCannon\" からタレットを生成できなかった\n\nSystem.Exception: missing blueprint")]
    [TestCase(
        "Could not generate turret from blueprint \"{{C|ArcWinder}}\"\n\nSystem.Exception: invalid tier",
        "blueprint \"{{C|ArcWinder}}\" からタレットを生成できなかった\n\nSystem.Exception: invalid tier")]
    public void IntegratedWeaponHostsHandleTurretWish_TranslatesShowFail_WhenOwnerPatched(string source, string expected)
    {
        AssertIntegratedWeaponHostsHandleTurretWishPopup(source, expected);
    }

    [Test]
    public void IntegratedWeaponHostsHandleTurretWish_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail("Could not generate turret from blueprint \"PhaseCannon\"\n\nSystem.Exception: missing blueprint");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("Could not generate turret from blueprint \"PhaseCannon\"\n\nSystem.Exception: missing blueprint"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void IntegratedWeaponHostsHandleTurretWish_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertIntegratedWeaponHostsHandleTurretWishPopup(
            MessageFrameTranslator.MarkDirectTranslation("Could not generate turret from blueprint \"PhaseCannon\"\n\nSystem.Exception: missing blueprint"),
            "Could not generate turret from blueprint \"PhaseCannon\"\n\nSystem.Exception: missing blueprint");
    }

    [Test]
    public void IntegratedWeaponHostsHandleTurretWish_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertIntegratedWeaponHostsHandleTurretWishPopup(string.Empty, string.Empty);
    }

    [TestCase(nameof(DummyBoostStatisticTarget.Apply), "Your Strength increases.", "筋力が上昇した。")]
    [TestCase(nameof(DummyBoostStatisticTarget.Apply), "Your {{C|Agility}} decreases.", "{{C|敏捷}}が低下した。")]
    [TestCase(nameof(DummyBoostStatisticTarget.Remove), "Your Toughness returns to normal.", "頑健が通常に戻った。")]
    public void BoostStatistic_TranslatesQueuedMessages_WhenOwnerPatched(string methodName, string source, string expected)
    {
        AssertBoostStatisticQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void BoostStatistic_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your Strength increases.", "g", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your Strength increases."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void BoostStatistic_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertBoostStatisticQueuedMessage(
            nameof(DummyBoostStatisticTarget.Apply),
            MessageFrameTranslator.MarkDirectTranslation("Your Strength increases."),
            "Your Strength increases.");
    }

    [Test]
    public void BoostStatistic_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertBoostStatisticQueuedMessage(nameof(DummyBoostStatisticTarget.Apply), string.Empty, string.Empty);
    }

    [TestCase(nameof(DummyEmboldenedTarget.Apply), "Your Hitpoints increase!", "ヒットポイントが増加した！")]
    [TestCase(nameof(DummyEmboldenedTarget.Apply), "Your {{C|Agility}} increase!", "{{C|敏捷}}が増加した！")]
    [TestCase(nameof(DummyEmboldenedTarget.Remove), "Your Hitpoints return to normal.", "ヒットポイントが通常に戻った。")]
    [TestCase(nameof(DummyEmboldenedTarget.Remove), "Your Strength returns to normal.", "筋力が通常に戻った。")]
    public void Emboldened_TranslatesQueuedMessages_WhenOwnerPatched(string methodName, string source, string expected)
    {
        AssertEmboldenedQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void Emboldened_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your Hitpoints increase!", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your Hitpoints increase!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Emboldened_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertEmboldenedQueuedMessage(
            nameof(DummyEmboldenedTarget.Apply),
            MessageFrameTranslator.MarkDirectTranslation("Your Hitpoints increase!"),
            "Your Hitpoints increase!");
    }

    [Test]
    public void Emboldened_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertEmboldenedQueuedMessage(nameof(DummyEmboldenedTarget.Apply), string.Empty, string.Empty);
    }

    [TestCase(
        "You've contracted {{G|glowcrust}} on your left arm.",
        "left armに{{G|glowcrust}}を発症した。")]
    [TestCase(
        "You've contracted waxflab on your {{C|right hand}}.",
        "{{C|right hand}}にwaxflabを発症した。")]
    public void FungalSporeInfectionApplyFungalInfection_TranslatesContractedPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertFungalSporeInfectionPopup(source, expected);
    }

    [Test]
    public void FungalSporeInfectionApplyFungalInfection_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You've contracted glowcrust on your left arm.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You've contracted glowcrust on your left arm."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FungalSporeInfectionApplyFungalInfection_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertFungalSporeInfectionPopup(
            MessageFrameTranslator.MarkDirectTranslation("You've contracted glowcrust on your left arm."),
            "You've contracted glowcrust on your left arm.");
    }

    [Test]
    public void FungalSporeInfectionApplyFungalInfection_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertFungalSporeInfectionPopup(string.Empty, string.Empty);
    }

    [Test]
    public void FungalSporeInfectionFireEvent_TranslatesSkinItchesQueuedMessage_WhenOwnerPatched()
    {
        AssertFungalSporeInfectionQueuedMessage("Your skin itches.", "肌がむずむずする。");
    }

    [Test]
    public void GasFungalSporesApplyGas_TranslatesSkinItchesQueuedMessage_WhenOwnerPatched()
    {
        AssertFungalSporeInfectionQueuedMessage(
            nameof(DummyFungalSporeInfectionTarget.ApplyGas),
            "Your skin itches.",
            "肌がむずむずする。");
    }

    [TestCase(
        nameof(DummyFungalSporeInfectionTarget.PaxFireEvent),
        "Your left arm spews a cloud of spores.",
        "あなたのleft armから胞子の雲が噴き出した。")]
    [TestCase(
        nameof(DummyFungalSporeInfectionTarget.PaxFireEvent),
        "Your left hands spew a cloud of spores.",
        "あなたのleft handsから胞子の雲が噴き出した。")]
    [TestCase(
        nameof(DummyFungalSporeInfectionTarget.PaxFireEvent),
        "snapjaw's right hand spews a cloud of spores.",
        "snapjaw's right handから胞子の雲が噴き出した。")]
    [TestCase(
        nameof(DummyFungalSporeInfectionTarget.PuffFireEvent),
        "&yYour left arm spews a cloud of spores.",
        "&yあなたのleft armから胞子の雲が噴き出した。")]
    [TestCase(
        nameof(DummyFungalSporeInfectionTarget.PuffFireEvent),
        "snapjaw's&y right hand spews a cloud of spores.",
        "snapjaw's&y right handから胞子の雲が噴き出した。")]
    public void FungalSporeInfectionFireEvent_TranslatesSporeCloudQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertFungalSporeInfectionQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void FungalSporeInfectionFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your skin itches.", Capitalize: false);
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your skin itches."));

            DummyMessageQueue.AddPlayerMessage("Your left arm spews a cloud of spores.", Capitalize: false);
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your left arm spews a cloud of spores."));

            DummyMessageQueue.AddPlayerMessage("&yYour left arm spews a cloud of spores.", Capitalize: false);
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("&yYour left arm spews a cloud of spores."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FungalSporeInfectionFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertFungalSporeInfectionQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your skin itches."),
            "Your skin itches.");
        AssertFungalSporeInfectionQueuedMessage(
            nameof(DummyFungalSporeInfectionTarget.PaxFireEvent),
            MessageFrameTranslator.MarkDirectTranslation("Your left arm spews a cloud of spores."),
            "Your left arm spews a cloud of spores.");
        AssertFungalSporeInfectionQueuedMessage(
            nameof(DummyFungalSporeInfectionTarget.PuffFireEvent),
            MessageFrameTranslator.MarkDirectTranslation("&yYour left arm spews a cloud of spores."),
            "&yYour left arm spews a cloud of spores.");
    }

    [Test]
    public void FungalSporeInfectionFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertFungalSporeInfectionQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(nameof(DummyFungalSporeInfectionTarget.PaxFireEvent))]
    [TestCase(nameof(DummyFungalSporeInfectionTarget.PuffFireEvent))]
    public void FungalSporeInfectionSporeCloudRoutes_LeaveEmptyQueuedMessageUnchanged_WhenOwnerPatched(string methodName)
    {
        AssertFungalSporeInfectionQueuedMessage(methodName, string.Empty, string.Empty);
    }

    [TestCase(nameof(DummyHealingTarget.HandleEvent))]
    [TestCase(nameof(DummyHealingTarget.FireEvent))]
    public void Healing_TranslatesInterruptedQueuedMessage_WhenOwnerPatched(string methodName)
    {
        AssertHealingQueuedMessage(methodName, "Your healing is interrupted!", "治癒が中断された！");
    }

    [Test]
    public void Healing_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your healing is interrupted!", "r", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your healing is interrupted!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Healing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertHealingQueuedMessage(
            nameof(DummyHealingTarget.HandleEvent),
            MessageFrameTranslator.MarkDirectTranslation("Your healing is interrupted!"),
            "Your healing is interrupted!");
    }

    [Test]
    public void Healing_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertHealingQueuedMessage(nameof(DummyHealingTarget.HandleEvent), string.Empty, string.Empty);
    }

    [TestCase(nameof(DummyStressedTarget.Apply), "Your body flushes with adrenaline!", "体にアドレナリンがほとばしった！")]
    [TestCase(nameof(DummyStressedTarget.Remove), "Your adrenaline level returns to normal!", "アドレナリンの分泌が落ち着いた！")]
    public void Stressed_TranslatesQueuedMessages_WhenOwnerPatched(string methodName, string source, string expected)
    {
        AssertStressedQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void Stressed_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your body flushes with adrenaline!", "g", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your body flushes with adrenaline!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Stressed_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertStressedQueuedMessage(
            nameof(DummyStressedTarget.Apply),
            MessageFrameTranslator.MarkDirectTranslation("Your body flushes with adrenaline!"),
            "Your body flushes with adrenaline!");
    }

    [Test]
    public void Stressed_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertStressedQueuedMessage(nameof(DummyStressedTarget.Apply), string.Empty, string.Empty);
    }

    [TestCase("You feel a bit better.", "少し気分が良くなった。")]
    [TestCase("Your vision blurs.", "視界がぼやける。")]
    [TestCase("Your vision clears up.", "視界が晴れた。")]
    public void MonochromeOnsetFireEvent_TranslatesQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertMonochromeOnsetQueuedMessage(source, expected);
    }

    [Test]
    public void MonochromePoisonOnDamageFireEvent_TranslatesVisionBlurQueuedMessage_WhenOwnerPatched()
    {
        AssertMonochromeOnsetQueuedMessage(
            nameof(DummyGameObjectFireEventTarget.MonochromePoisonOnDamageFireEvent),
            "Your vision blurs.",
            "視界がぼやける。");
    }

    [Test]
    public void MonochromeOnsetFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your vision blurs.", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your vision blurs."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MonochromeOnsetFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertMonochromeOnsetQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your vision blurs."),
            "Your vision blurs.");
    }

    [Test]
    public void MonochromeOnsetFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertMonochromeOnsetQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase("You feel a bit better.", "少し気分が良くなった。")]
    [TestCase("Your throat feels sore.", "喉がひりひりする。")]
    [TestCase("You feel better.", "気分が良くなった。")]
    public void GlotrotOnsetFireEvent_TranslatesQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertGlotrotOnsetQueuedMessage(source, expected);
    }

    [Test]
    public void GlotrotOnsetFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your throat feels sore.", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your throat feels sore."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GlotrotOnsetFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertGlotrotOnsetQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your throat feels sore."),
            "Your throat feels sore.");
    }

    [Test]
    public void GlotrotOnsetFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertGlotrotOnsetQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void IronshankFireEvent_TranslatesCartilageQueuedMessage_WhenOwnerPatched()
    {
        AssertIronshankQueuedMessage(
            "You feel the cartilage stretch as your leg bones grind together at the joints.",
            "足の骨が軋み、軟骨が伸びるのを感じた。");
    }

    [Test]
    public void IronshankFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(
                "You feel the cartilage stretch as your leg bones grind together at the joints.",
                Capitalize: false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("You feel the cartilage stretch as your leg bones grind together at the joints."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void IronshankFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertIronshankQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation(
                "You feel the cartilage stretch as your leg bones grind together at the joints."),
            "You feel the cartilage stretch as your leg bones grind together at the joints.");
    }

    [Test]
    public void IronshankFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertIronshankQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase("You feel a bit better.", "少し気分が良くなった。")]
    [TestCase("Your legs ache at the joints.", "脚の関節が痛む。")]
    [TestCase("You feel better.", "気分が良くなった。")]
    public void IronshankOnsetFireEvent_TranslatesQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertIronshankOnsetQueuedMessage(source, expected);
    }

    [Test]
    public void IronshankOnsetFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your legs ache at the joints.", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your legs ache at the joints."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void IronshankOnsetFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertIronshankOnsetQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your legs ache at the joints."),
            "Your legs ache at the joints.");
    }

    [Test]
    public void IronshankOnsetFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertIronshankOnsetQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase("Your adrenaline subsides.", "アドレナリンが引いていく。")]
    [TestCase("{{G|Your adrenaline starts to flow.}}", "{{G|アドレナリンが流れ始めた。}}")]
    public void AdrenalControlFireEvent_TranslatesQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertAdrenalControlQueuedMessage(source, expected);
    }

    [Test]
    public void AdrenalControlFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Your adrenaline subsides.", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Your adrenaline subsides."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AdrenalControlFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertAdrenalControlQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your adrenaline subsides."),
            "Your adrenaline subsides.");
    }

    [Test]
    public void AdrenalControlFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertAdrenalControlQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(nameof(DummyAmnesiaTarget.HandleEvent), typeof(DummySecretVisibilityChangedEvent), "You feel like you forgot something important.", "大事な何かを忘れた気がする。")]
    [TestCase(nameof(DummyAmnesiaTarget.HandleEvent), typeof(DummyEnteredCellEvent), "This place feels vaguely familiar.", "この場所にはどこか見覚えがある。")]
    public void AmnesiaHandleEvent_TranslatesQueuedMessages_WhenOwnerPatched(string methodName, Type eventType, string source, string expected)
    {
        AssertAmnesiaQueuedMessage(methodName, eventType, source, expected);
    }

    [Test]
    public void AmnesiaHandleEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("This place feels vaguely familiar.", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("This place feels vaguely familiar."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AmnesiaHandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertAmnesiaQueuedMessage(
            nameof(DummyAmnesiaTarget.HandleEvent),
            typeof(DummyEnteredCellEvent),
            MessageFrameTranslator.MarkDirectTranslation("This place feels vaguely familiar."),
            "This place feels vaguely familiar.");
    }

    [Test]
    public void AmnesiaHandleEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertAmnesiaQueuedMessage(nameof(DummyAmnesiaTarget.HandleEvent), typeof(DummyEnteredCellEvent), string.Empty, string.Empty);
    }

    [TestCase(typeof(BlinkingTicTranslationPatch), "You lurch suddenly!", "突然ぐらりとよろめいた！")]
    [TestCase(typeof(BrittleBonesTranslationPatch), "You feel your bones fracture.", "骨がひび割れるのを感じた。")]
    [TestCase(typeof(ElectromagneticImpulseTranslationPatch), "{{r|You surge with energy!}}", "{{r|エネルギーが満ちあふれる！}}")]
    [TestCase(typeof(RegenerationTranslationPatch), "{{G|You were decapitated, but a new head regrew immediately!}}", "{{G|首を刎ねられたが、すぐに新しい頭が生えた！}}")]
    public void SimpleFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched(Type patchType, string source, string expected)
    {
        AssertSimpleFireEventQueuedMessage(patchType, source, expected);
    }

    [TestCase(typeof(BlinkingTicTranslationPatch), "You lurch suddenly!")]
    [TestCase(typeof(BrittleBonesTranslationPatch), "You feel your bones fracture.")]
    [TestCase(typeof(ElectromagneticImpulseTranslationPatch), "{{r|You surge with energy!}}")]
    [TestCase(typeof(RegenerationTranslationPatch), "{{G|You were decapitated, but a new head regrew immediately!}}")]
    public void SimpleFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched(Type patchType, string source)
    {
        AssertSimpleFireEventQueuedMessage(patchType, MessageFrameTranslator.MarkDirectTranslation(source), source);
    }

    [TestCase(typeof(BlinkingTicTranslationPatch))]
    [TestCase(typeof(BrittleBonesTranslationPatch))]
    [TestCase(typeof(ElectromagneticImpulseTranslationPatch))]
    [TestCase(typeof(RegenerationTranslationPatch))]
    public void SimpleFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched(Type patchType)
    {
        AssertSimpleFireEventQueuedMessage(patchType, string.Empty, string.Empty);
    }

    [TestCase("You lurch suddenly!")]
    [TestCase("You feel your bones fracture.")]
    [TestCase("{{r|You surge with energy!}}")]
    [TestCase("{{G|You were decapitated, but a new head regrew immediately!}}")]
    [TestCase("You feel uneasy.")]
    [TestCase("You stop meditating and feel refreshed.")]
    [TestCase("You stop meditating.")]
    [TestCase("You start to feel sluggish.")]
    [TestCase("The hurdles that separate the will and the way begin to collapse.")]
    [TestCase("You feel stiff as a stone.")]
    [TestCase("You begin itching for a trigger.")]
    [TestCase("You start to prowl.")]
    [TestCase("You are {{K|exhausted}}!")]
    [TestCase("You are {{C|paralyzed}}.")]
    [TestCase("Your attack bounces harmlessly off of {{Y|stasis field}}.")]
    [TestCase("snapjaw's attack bounces harmlessly off of {{Y|stasis field}}.")]
    [TestCase("The 熊 resists your life drain!")]
    [TestCase("You resist snapjaw's life drain!")]
    [TestCase("The 装置 was cracked.")]
    [TestCase("The {{blaze|blaze}} tonic burns out of your system.")]
    [TestCase("{{R|The barbed hook}}{{R| releases}} you.")]
    [TestCase("{{R|The barbed hook}}{{R| releases}} {{G|the snapjaw}}{{R|.}}")]
    [TestCase("You hear a shloop and then a hitch. Nothing happens.")]
    [TestCase("You hear a shloop and the world around you shifts.")]
    [TestCase("1 turn remains until your berserker rage ends.")]
    [TestCase("2 turns remain until your berserker rage ends.")]
    [TestCase("1 turn remains until you stop demolishing.")]
    [TestCase("3 turns remain until you stop demolishing.")]
    [TestCase("You're going to collapse from exhaustion in one round.")]
    [TestCase("You're going to collapse from exhaustion in three rounds.")]
    [TestCase("Checkpointing enabled")]
    [TestCase("You feel a sense of holiness here.")]
    [TestCase("&CA flash of insight overcomes you!")]
    [TestCase("The ground shakes violently!")]
    [TestCase("The ground shakes violently and loose rock falls from the ceiling!")]
    [TestCase("The security door unlocks with a loud clank and swings open.")]
    [TestCase("The security door swings closed and locks with a loud clank.")]
    [TestCase("Nothing seems to happen when you hit the switch.")]
    [TestCase("The membrane of the egg sac snots apart.")]
    [TestCase("The svardym eggs hatch.")]
    [TestCase("The svardym egg hatches.")]
    [TestCase("You are shunted to another location!")]
    [TestCase("You teleport!")]
    [TestCase("You are teleported to an exit.")]
    [TestCase("You do that with ease.")]
    [TestCase("That creature is of too high a level to duplicate!")]
    [TestCase("{{G|You sunder spacetime.}}")]
    [TestCase("You are sucked through the surface of the sphere!")]
    [TestCase("Your focus slips, causing you to dent spacetime in the local region.")]
    public void FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FearAuraApplyFear_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSimpleApplyFearQueuedMessage("You feel uneasy.", "不安を感じる。");
    }

    [Test]
    public void FearAuraApplyFear_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertSimpleApplyFearQueuedMessage(MessageFrameTranslator.MarkDirectTranslation("You feel uneasy."), "You feel uneasy.");
    }

    [Test]
    public void FearAuraApplyFear_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertSimpleApplyFearQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase("You stop meditating and feel refreshed.", "瞑想を終え、気分がすっきりした。")]
    [TestCase("You stop meditating.", "瞑想をやめた。")]
    public void MeditatingRemove_TranslatesFixedQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertMeditatingRemoveQueuedMessage(source, expected);
    }

    [Test]
    public void MeditatingRemove_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertMeditatingRemoveQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You stop meditating."),
            "You stop meditating.");
    }

    [Test]
    public void MeditatingRemove_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertMeditatingRemoveQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase("You start to feel sluggish.", "体がだるくなってきた。")]
    [TestCase(
        "The hurdles that separate the will and the way begin to collapse.",
        "志と道を隔てていた障害が崩れ始める。")]
    [TestCase("You begin itching for a trigger.", "引き金を求めてうずうずしてきた。")]
    [TestCase("You start to prowl.", "うろつき始めた。")]
    [TestCase("You are {{K|exhausted}}!", "{{K|疲労困憊}}している！")]
    public void EffectStaticApply_TranslatesFixedQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertEffectStaticApplyQueuedMessage(source, expected);
    }

    [Test]
    public void EffectStaticFireEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectStaticFireEventQueuedMessage("You feel stiff as a stone.", "石のように体がこわばる。");
    }

    [Test]
    public void EffectStaticBeginTakeAction_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectStaticBeginTakeActionQueuedMessage("You are {{C|paralyzed}}.", "{{C|麻痺}}している。");
    }

    [TestCase(
        "1 turn remains until your berserker rage ends.",
        "バーサークの怒りが終わるまであと1ターン。")]
    [TestCase(
        "2 turns remain until your berserker rage ends.",
        "バーサークの怒りが終わるまであと2ターン。")]
    [TestCase(
        "You're going to collapse from exhaustion in one round.",
        "疲労で倒れるまであと1ラウンド。")]
    [TestCase(
        "You're going to collapse from exhaustion in three rounds.",
        "疲労で倒れるまであと3ラウンド。")]
    public void EffectStaticBeginTakeAction_TranslatesCountdownQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertEffectStaticBeginTakeActionQueuedMessage(source, expected);
    }

    [TestCase(
        "1 turn remains until you stop demolishing.",
        "解体をやめるまであと1ターン。")]
    [TestCase(
        "3 turns remain until you stop demolishing.",
        "解体をやめるまであと3ターン。")]
    public void EffectStaticFireEvent_TranslatesCountdownQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertEffectStaticFireEventQueuedMessage(source, expected);
    }

    [Test]
    public void EffectStaticApply_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectStaticApplyQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You start to feel sluggish."),
            "You start to feel sluggish.");
    }

    [Test]
    public void EffectStaticFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectStaticFireEventQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You feel stiff as a stone."),
            "You feel stiff as a stone.");
    }

    [Test]
    public void EffectStaticBeginTakeAction_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectStaticBeginTakeActionQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("1 turn remains until your berserker rage ends."),
            "1 turn remains until your berserker rage ends.");
    }

    [Test]
    public void EffectStaticApply_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertEffectStaticApplyQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void EffectStaticFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertEffectStaticFireEventQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void EffectStaticBeginTakeAction_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertEffectStaticBeginTakeActionQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        "Your attack bounces harmlessly off of {{Y|stasis field}}.",
        "あなたの攻撃は{{Y|stasis field}}に当たって無害に跳ね返った。")]
    [TestCase(
        "snapjaw's attack bounces harmlessly off of {{Y|stasis field}}.",
        "snapjawの攻撃は{{Y|stasis field}}に当たって無害に跳ね返った。")]
    public void StasisHandleEvent_TranslatesAttackBounceQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertStasisQueuedMessage(source, expected);
    }

    [Test]
    public void StasisHandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertStasisQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Your attack bounces harmlessly off of {{Y|stasis field}}."),
            "Your attack bounces harmlessly off of {{Y|stasis field}}.");
    }

    [Test]
    public void StasisHandleEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertStasisQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        "The 熊 resists your life drain!",
        "熊はあなたの生命吸収に抵抗した！")]
    [TestCase(
        "{{r|The 熊 resists your life drain!}}",
        "{{r|熊はあなたの生命吸収に抵抗した！}}")]
    [TestCase(
        "You resist snapjaw's life drain!",
        "あなたはsnapjawの生命吸収に抵抗した！")]
    public void EffectGeneratedHandleEvent_TranslatesLifeDrainQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertEffectGeneratedHandleEventQueuedMessage(source, expected);
    }

    [TestCase(
        "The 装置 was cracked.",
        "装置にひびが入った")]
    [TestCase(
        "{{R|The 装置 was cracked.}}",
        "{{R|装置にひびが入った}}")]
    public void EffectGeneratedApply_TranslatesShatteredArmorQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertEffectGeneratedApplyQueuedMessage(source, expected);
    }

    [Test]
    public void EffectGeneratedHandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectGeneratedHandleEventQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The 熊 resists your life drain!"),
            "The 熊 resists your life drain!");
    }

    [Test]
    public void EffectGeneratedApply_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertEffectGeneratedApplyQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The 装置 was cracked."),
            "The 装置 was cracked.");
    }

    [Test]
    public void EffectGeneratedHandleEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertEffectGeneratedHandleEventQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void EffectGeneratedApply_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertEffectGeneratedApplyQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.GelatenousPalmFireEvent),
        "The steel sword is lost in the goop!",
        "steel swordは粘液の中に沈んだ！")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.GraveMossTrigger),
        "The 苔 starts to fizz hungrily.",
        "苔は飢えたように泡立ち始めた")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.QuantumRipplerHandleEvent),
        "The 装置 collapses under the pressure of normality and implodes.",
        "装置は正常性の圧力に耐えきれず崩壊し、内破した")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.PerformReclamationOf),
        "The 回収装置 reclaims a 金属片.",
        "回収装置は金属片を回収した。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.DropOffStolenGoodsMoveToDropoff),
        "The snapjaw drops a {{Y|folded carbide dagger}} down the {{y|shaft}}.",
        "{{Y|folded carbide dagger}}を{{y|shaft}}に落とした。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.PaxKlanqMadnessTakeAction),
        "The snapjaw shouts shouts {{O|KLANQ}}!",
        "snapjawは{{O|KLANQ}}と叫んだ！")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.PaxKlanqMadnessTakeAction),
        "{{R|The snapjaw shouts shouts {{O|KLANQ}}!}}",
        "{{R|snapjawは{{O|KLANQ}}と叫んだ！}}")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.BodyPartUnequipPartAndChildren),
        "Your {{Y|carbide dagger}} falls to the ground.",
        "{{Y|carbide dagger}}は地面に倒れた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ExtradimensionalLootFireEvent),
        "The hunter drops an {{Y|eigenrifle}}, and by sheer chance it quantum tunnels and fully materializes in this dimension.",
        "hunterは{{Y|eigenrifle}}を落とし、偶然にもそれは量子トンネルを通ってこの次元に完全実体化した。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.GarbageAttemptRifle),
        "The 熊 rifles through the ゴミ山.",
        "熊はゴミ山を漁った")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.GarbageAttemptRifle),
        "Somebody rifles through the ゴミ山.",
        "誰かがゴミ山を漁った")]
    public void GeneratedQueueDoesVerb_TranslatesDoesVerbMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertGeneratedQueueDoesVerbMessage(methodName, source, expected);
    }

    [TestCase("The snapjaw has no limbs.", "スナップジョーには四肢がない")]
    [TestCase("{{Y|The snapjaw}} has no limbs.", "{{Y|スナップジョー}}には四肢がない")]
    [TestCase("{{Y|The snapjaw}} has no limbs that can be amputated.", "{{Y|スナップジョー}}には切断できる四肢がない")]
    [TestCase("You have no limbs.", "あなたには四肢がない")]
    [TestCase("{{Y|You}} have no limbs.", "{{Y|あなた}}には四肢がない")]
    [TestCase("You can't perform field amputations with hostiles nearby!", "敵対者が近くにいると野外切断は行えない！")]
    [TestCase(
        "You must have an axe or a weapon capable of dismemberment equipped in order to perform a field amputation.",
        "野外切断を行うには、斧か切断可能な武器を装備していなければならない。")]
    [TestCase("You cannot reach {{Y|the snapjaw}} to amputate its limb.", "{{Y|スナップジョー}}に手が届かず、四肢を切断できない。")]
    [TestCase("There is no one there for you to amputate their limb.", "そこには四肢を切断できる相手がいない。")]
    [TestCase("{{Y|the snapjaw}} won't let you do that.", "{{Y|スナップジョー}}はそれを許さない。")]
    [TestCase("You cannot amputate {{Y|the snapjaw's limbs}}.", "{{Y|スナップジョーの四肢}}は切断できない。")]
    [TestCase("You cannot bring yourself to amputate your {{Y|left arm}}.", "自分の{{Y|左腕}}を切断する気にはなれない。")]
    [TestCase(
        "You cannot amputate the {{Y|right hand}} holding {{C|the chem cell}}.",
        "{{C|ケムセル}}を持っている{{Y|右手}}は切断できない。")]
    [TestCase(
        "You cannot amputate the {{Y|left hand}} holding {{C|the obsidian idol}}.",
        "{{C|obsidian idol}}を持っている{{Y|左手}}は切断できない。")]
    [TestCase(
        "{{Y|the snapjaw}} sees no reason for you to amputate its {{R|left arm}}.",
        "{{Y|スナップジョー}}はあなたが{{R|左腕}}を切断する理由がないと考えている。")]
    [TestCase("The snapjaw keeps fighting.", "The snapjaw keeps fighting.")]
    [TestCase("", "")]
    [TestCase("\u0001The snapjaw has no limbs.", "The snapjaw has no limbs.")]
    public void PhysicAmputateLimb_TranslatesOwnerPopups_WhenOwnerPatched(string source, string expected)
    {
        AssertPhysicAmputateLimbPopup(source, expected);
    }

    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.DropOffStolenGoodsMoveToDropoff),
        "The snapjaw drops",
        "drop",
        "The snapjaw",
        " a {{Y|folded carbide dagger}} down the {{y|shaft}}.",
        "{{Y|folded carbide dagger}}を{{y|shaft}}に落とした。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.PaxKlanqMadnessTakeAction),
        "The snapjaw shouts",
        "shout",
        "The snapjaw",
        " shouts {{O|KLANQ}}!",
        "snapjawは{{O|KLANQ}}と叫んだ！")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.BodyPartUnequipPartAndChildren),
        "Your {{Y|carbide dagger}} falls",
        "fall",
        "Your {{Y|carbide dagger}}",
        " to the ground.",
        "{{Y|carbide dagger}}は地面に倒れた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ExtradimensionalLootFireEvent),
        "The hunter drops",
        "drop",
        "The hunter",
        " an {{Y|eigenrifle}}, and by sheer chance it quantum tunnels and fully materializes in this dimension.",
        "hunterは{{Y|eigenrifle}}を落とし、偶然にもそれは量子トンネルを通ってこの次元に完全実体化した。")]
    public void GeneratedQueueDoesVerb_TranslatesMarkedDoesVerbMessages_WhenOwnerPatched(
        string methodName,
        string fragment,
        string verb,
        string subject,
        string tail,
        string expected)
    {
        var source = DoesVerbRouteTranslator.MarkDoesFragment(fragment, verb, subject.Length, null) + tail;

        AssertGeneratedQueueDoesVerbMessage(methodName, source, expected);
    }

    [Test]
    public void GeneratedQueueDoesVerb_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryMessageFrames();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("The steel sword is lost in the goop.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The steel sword is lost in the goop."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GeneratedQueueDoesVerb_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched()
    {
        AssertGeneratedQueueDoesVerbMessage(
            nameof(DummySimpleOwnerQueueTarget.GelatenousPalmFireEvent),
            MessageFrameTranslator.MarkDirectTranslation("The steel sword is lost in the goop!"),
            "The steel sword is lost in the goop!");
    }

    [Test]
    public void GeneratedQueueDoesVerb_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertGeneratedQueueDoesVerbMessage(nameof(DummySimpleOwnerQueueTarget.GraveMossTrigger), string.Empty, string.Empty);
    }

    [TestCase(
        "You must wait {{C|7 turns}} to use that ability again.",
        "その能力を再び使うには{{C|7 turns}}待つ必要がある。")]
    public void AbilityManagerShow_TranslatesCooldownQueuedMessage_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertAbilityManagerShowQueuedMessage(source, expected);
    }

    [Test]
    public void AbilityManagerShow_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You must wait {{C|7 turns}} to use that ability again.", null, Capitalize: false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("You must wait {{C|7 turns}} to use that ability again."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerShow_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched()
    {
        AssertAbilityManagerShowQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("その能力はまだ使えない。"),
            "その能力はまだ使えない。");
    }

    [Test]
    public void AbilityManagerShow_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertAbilityManagerShowQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        "The {{blaze|blaze}} tonic burns out of your system.",
        "{{blaze|ブレイズ}}トニックが体内から燃え尽きた。")]
    [TestCase(
        "{{R|The {{blaze|blaze}} tonic burns out of your system.}}",
        "{{R|{{blaze|ブレイズ}}トニックが体内から燃え尽きた。}}")]
    public void BlazeTonicRemove_TranslatesBurnoutQueuedMessage_WhenOwnerPatched(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();
        AssertBlazeTonicRemoveQueuedMessage(source, expected);
    }

    [Test]
    public void BlazeTonicRemove_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertBlazeTonicRemoveQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The {{blaze|blaze}} tonic burns out of your system."),
            "The {{blaze|blaze}} tonic burns out of your system.");
    }

    [Test]
    public void BlazeTonicRemove_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertBlazeTonicRemoveQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        "{{R|The barbed hook}}{{R| releases}} you.",
        "{{R|The barbed hook}}があなたを放した。")]
    [TestCase(
        "{{R|The barbed hook}}{{R| releases}} {{G|the snapjaw}}{{R|.}}",
        "{{R|The barbed hook}}が{{G|the snapjaw}}を放した。")]
    public void LatchedOntoExpired_TranslatesReleaseQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertLatchedOntoExpiredQueuedMessage(source, expected);
    }

    [Test]
    public void LatchedOntoExpired_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertLatchedOntoExpiredQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("{{R|The barbed hook}}{{R| releases}} you."),
            "{{R|The barbed hook}}{{R| releases}} you.");
    }

    [Test]
    public void LatchedOntoExpired_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertLatchedOntoExpiredQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.TeleportToClamWorld),
        "You hear a shloop and then a hitch. Nothing happens.",
        "シュループという音がして、それから引っかかるような音がした。何も起こらない。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.TeleportFromClamWorld),
        "You hear a shloop and then a hitch. Nothing happens.",
        "シュループという音がして、それから引っかかるような音がした。何も起こらない。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.TeleportJoppaWorld),
        "You hear a shloop and then a hitch. Nothing happens.",
        "シュループという音がして、それから引っかかるような音がした。何も起こらない。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.TeleportJoppaWorld),
        "You hear a shloop and the world around you shifts.",
        "シュループという音がして、周囲の世界がずれた。")]
    public void GiantClamTeleport_TranslatesShloopQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertGiantClamTeleportQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void GiantClamTeleport_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertGiantClamTeleportQueuedMessage(
            nameof(DummySimpleOwnerQueueTarget.TeleportJoppaWorld),
            MessageFrameTranslator.MarkDirectTranslation("You hear a shloop and the world around you shifts."),
            "You hear a shloop and the world around you shifts.");
    }

    [Test]
    public void GiantClamTeleport_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertGiantClamTeleportQueuedMessage(nameof(DummySimpleOwnerQueueTarget.TeleportJoppaWorld), string.Empty, string.Empty);
    }

    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter),
        "The {{B|force bubble}} snaps off.",
        "{{B|フォースバブル}}が消えた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter),
        "The {{B|force bubble}} around {{Y|the snapjaw}} snaps off.",
        "{{Y|the snapjaw}}の周りの{{B|フォースバブル}}が消えた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter),
        "{{G|A {{B|force bubble}} pops into being around you.}}",
        "{{G|あなたの周りに{{B|フォースバブル}}が出現した。}}")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter),
        "A {{B|force bubble}} pops into being around {{Y|the snapjaw}}.",
        "{{Y|the snapjaw}}の周りに{{B|フォースバブル}}が出現した。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateStopsvalinn),
        "The {{R|force bubble}} snaps off.",
        "{{R|フォースバブル}}が消えた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateStopsvalinn),
        "The {{R|force bubble}} in front of {{Y|the snapjaw}} snaps off.",
        "{{Y|the snapjaw}}の前の{{R|フォースバブル}}が消えた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateStopsvalinn),
        "A {{R|force bubble}} pops into being in front of you!",
        "あなたの前に{{R|フォースバブル}}が出現した！")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.ActivateStopsvalinn),
        "A {{R|force bubble}} pops into being in front of {{Y|the snapjaw}}.",
        "{{Y|the snapjaw}}の前に{{R|フォースバブル}}が出現した。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.DestroyBubble),
        "The {{B|force bubble}} snaps off.",
        "{{B|フォースバブル}}が消えた。")]
    [TestCase(
        nameof(DummySimpleOwnerQueueTarget.DestroyBubble),
        "The {{B|force bubble}} around {{Y|the snapjaw}} snaps off.",
        "{{Y|the snapjaw}}の周りの{{B|フォースバブル}}が消えた。")]
    public void ForceBubbleOwner_TranslatesForceBubbleQueuedMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertForceBubbleOwnerQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void ForceBubbleOwner_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("A {{B|force bubble}} pops into being around you.", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("A {{B|force bubble}} pops into being around you."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ForceBubbleOwner_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertForceBubbleOwnerQueuedMessage(
            nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter),
            MessageFrameTranslator.MarkDirectTranslation("The {{B|force bubble}} snaps off."),
            "The {{B|force bubble}} snaps off.");
    }

    [Test]
    public void ForceBubbleOwner_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertForceBubbleOwnerQueuedMessage(nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter), string.Empty, string.Empty);
    }

    [Test]
    public void SystemStaticCheckpointOn_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticCheckpointQueuedMessage("Checkpointing enabled", "チェックポイント機能を有効化した。");
    }

    [TestCase("You feel a sense of holiness here.", "この場所には神聖さを感じる。")]
    public void SystemStaticSetHolyZone_TranslatesFixedQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertSystemStaticSetHolyZoneQueuedMessage(source, expected);
    }

    [TestCase("&CA flash of insight overcomes you!", "&Cひらめきがあなたを満たした！")]
    public void SystemStaticFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertSystemStaticFireEventQueuedMessage(source, expected);
    }

    [TestCase("You do that with ease.", "難なくやってのけた。")]
    [TestCase("That creature is of too high a level to duplicate!", "そのクリーチャーは複製するには強すぎる！")]
    public void SystemStaticMutationFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertSystemStaticFireEventQueuedMessage(source, expected);
    }

    [Test]
    public void SystemStaticWorldTeleporterFireEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticFireEventQueuedMessage(
            "You are sucked through the surface of the sphere!",
            "球の表面に吸い込まれた！");
    }

    [Test]
    public void SystemStaticQuantumJittersSunder_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticSunderQueuedMessage(
            "Your focus slips, causing you to dent spacetime in the local region.",
            "集中が途切れ、この周囲の時空がへこむ。");
    }

    [TestCase("The ground shakes violently!", "地面が激しく揺れた！")]
    [TestCase(
        "The ground shakes violently and loose rock falls from the ceiling!",
        "地面が激しく揺れ、天井から岩が崩れ落ちた！")]
    public void SystemStaticQuake_TranslatesFixedQueuedMessages_WhenOwnerPatched(string source, string expected)
    {
        AssertSystemStaticQuakeQueuedMessage(source, expected);
    }

    [TestCase("The security door unlocks with a loud clank and swings open.", "頑丈なドアが大きな音とともに解錠され開いた。")]
    [TestCase("The security door swings closed and locks with a loud clank.", "頑丈なドアが閉じて大きな音で施錠された。")]
    [TestCase("Nothing seems to happen when you hit the switch.", "スイッチを押しても何も起こらない。")]
    public void SystemStaticDoorSwitchFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertSystemStaticFireEventQueuedMessage(source, expected);
    }

    [TestCase("The membrane of the egg sac snots apart.", "卵嚢の膜がぐしゃりと裂けた。")]
    [TestCase("The svardym eggs hatch.", "スヴァーディムの卵が孵化した。")]
    [TestCase("The svardym egg hatches.", "スヴァーディムの卵が孵化した。")]
    public void SystemStaticSpawningEggSacTickEgg_TranslatesFixedQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertSystemStaticTickEggQueuedMessage(source, expected);
    }

    [TestCase("You are shunted to another location!", "別の場所へ弾き飛ばされた！")]
    [TestCase("You teleport!", "テレポートした！")]
    public void SystemStaticTeleportationCast_TranslatesFixedQueuedMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertSystemStaticCastQueuedMessage(source, expected);
    }

    [Test]
    public void SystemStaticCatacombsExitTeleporterHandleEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticEnteredCellQueuedMessage("You are teleported to an exit.", "出口へ転送された。");
    }

    [Test]
    public void SystemStaticLuminousInfectionTryGrowMushroom_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticTryGrowMushroomQueuedMessage(
            "You sprout a {{C|luminous hoarshroom}}.",
            "あなたに{{C|発光ホアシュルーム}}が生えた。");
    }

    [Test]
    public void SystemStaticTorchPropertiesHandleEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticTorchPropertiesHandleEventQueuedMessage("Your torch burns out!", "たいまつが燃え尽きた！");
    }

    [Test]
    public void SystemStaticSpacetimeVortex_TranslatesFixedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticVortexQueuedMessage("{{G|You sunder spacetime.}}", "{{G|時空を切り裂いた。}}");
    }

    [Test]
    public void SystemStaticQuake_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticQuakeQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The ground shakes violently!"),
            "The ground shakes violently!");
    }

    [Test]
    public void SystemStaticQuake_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertSystemStaticQuakeQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void SystemStaticQuake_LeavesUnknownQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertSystemStaticQuakeQueuedMessage("The cavern walls rumble softly.", "The cavern walls rumble softly.");
    }

    [Test]
    public void SystemStatic_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertSystemStaticCheckpointQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Checkpointing enabled"),
            "Checkpointing enabled");
    }

    [Test]
    public void SystemStatic_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertSystemStaticCheckpointQueuedMessage(string.Empty, string.Empty);
    }

    [TestCase(
        "{{G|salve tonic}} fails to penetrate {{R|snapjaw's armor}} and is destroyed.",
        "{{G|salve tonic}}は{{R|snapjaw's armor}}を貫通できず、破壊された。")]
    [TestCase(
        "phase cannon tonic fails to penetrate your armor and is destroyed.",
        "phase cannon tonicはyour armorを貫通できず、破壊された。")]
    public void TonicFireEvent_TranslatesArmorFailureMessage_WhenOwnerPatched(string source, string expected)
    {
        AssertTonicFireEventQueuedMessage(source, expected);
    }

    [Test]
    public void TonicFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("salve tonic fails to penetrate snapjaw's armor and is destroyed.");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("salve tonic fails to penetrate snapjaw's armor and is destroyed."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TonicFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertTonicFireEventQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("salve tonic fails to penetrate snapjaw's armor and is destroyed."),
            "salve tonic fails to penetrate snapjaw's armor and is destroyed.");
    }

    [Test]
    public void TonicFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertTonicFireEventQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void XrlGameFinishQuestStep_TranslatesErrorMessage_WhenOwnerPatched()
    {
        AssertXrlGameFinishQuestStepQueuedMessage(
            "Error finishing quest step Quest_Demo @ Step1~Step2 : System.Exception: boom",
            "クエストステップ完了エラー Quest_Demo @ Step1~Step2 : System.Exception: boom");
    }

    [Test]
    public void XrlGameFinishQuestStep_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Error finishing quest step Quest_Demo @ Step1 : System.Exception: boom", "R");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Error finishing quest step Quest_Demo @ Step1 : System.Exception: boom"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void XrlGameFinishQuestStep_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertXrlGameFinishQuestStepQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Error finishing quest step Quest_Demo @ Step1 : System.Exception: boom"),
            "Error finishing quest step Quest_Demo @ Step1 : System.Exception: boom");
    }

    [Test]
    public void XrlGameFinishQuestStep_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        AssertXrlGameFinishQuestStepQueuedMessage(string.Empty, string.Empty);
    }

    [Test]
    public void GameObjectDie_TranslatesCompanionDeathMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^Your companion, (.+?), (.+?)\\.$", "あなたの仲間である{0}は{1}。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectDieTarget),
                    nameof(DummyGameObjectDieTarget.Die),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(bool),
                    typeof(string),
                    typeof(string),
                    typeof(string)),
                typeof(GameObjectDieTranslationPatch));

            var target = new DummyGameObjectDieTarget
            {
                MessageToSend = "Your companion, Irudad, dies.",
            };

            target.Die();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたの仲間であるIrudadはdies。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectRegenera_TranslatesCureMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^Your regenerative metabolism cures you of (.+?)\\.$", "あなたの再生代謝が{0}を治した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyGameObjectFireEventTarget), nameof(DummyGameObjectFireEventTarget.FireEvent), typeof(DummyGameEvent)),
                prefix: new HarmonyMethod(RequireMethod(typeof(GameObjectRegeneraTranslationPatch), nameof(GameObjectRegeneraTranslationPatch.Prefix), typeof(object))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(GameObjectRegeneraTranslationPatch), nameof(GameObjectRegeneraTranslationPatch.Finalizer), typeof(Exception))));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = "Your regenerative metabolism cures you of glotrot.",
            };

            target.FireEvent(new DummyGameEvent { ID = "Regenera" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたの再生代謝がglotrotを治した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectRegenera_TranslatesRegeneratedLimbMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyGameObjectFireEventTarget), nameof(DummyGameObjectFireEventTarget.FireEvent), typeof(DummyGameEvent)),
                prefix: new HarmonyMethod(RequireMethod(typeof(GameObjectRegeneraTranslationPatch), nameof(GameObjectRegeneraTranslationPatch.Prefix), typeof(object))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(GameObjectRegeneraTranslationPatch), nameof(GameObjectRegeneraTranslationPatch.Finalizer), typeof(Exception))));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = "You regenerate your {{G|arm}}!",
            };

            target.FireEvent(new DummyGameEvent { ID = "Regenera" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{G|arm}}を再生した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectSpot_TranslatesSpotMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You see (?:the |a |an )?(.+?) to the (.+?) and stop (.+?)\\.$", "{1}の{0}を見つけ、{2}をやめた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectSpotTarget),
                    nameof(DummyGameObjectSpotTarget.ArePerceptibleHostilesNearby),
                    typeof(bool),
                    typeof(bool),
                    typeof(string),
                    typeof(object),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)),
                typeof(GameObjectSpotTranslationPatch));

            var target = new DummyGameObjectSpotTarget
            {
                MessageToSend = "You see a snapjaw to the north and stop auto-exploring.",
            };

            target.ArePerceptibleHostilesNearby(logSpot: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("northのsnapjawを見つけ、auto-exploringをやめた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectEmitMessage_TranslatesVariableReplaceOutput_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are surrounded by (.+?)\\.$", "{0}に包囲されている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectEmitMessageTarget), nameof(DummyGameObjectEmitMessageTarget.EmitMessage), typeof(string), typeof(DummyGameObject), typeof(string), typeof(bool)),
                typeof(GameObjectEmitMessageTranslationPatch));

            var target = new DummyGameObjectEmitMessageTarget
            {
                MessageToSend = "You are surrounded by baboons.",
            };

            target.EmitMessage("unused");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("baboonsに包囲されている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesVariableReplaceOutput_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are surrounded by (.+?)\\.$", "{0}に包囲されている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "You are surrounded by baboons.";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("baboonsに包囲されている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesMixedJapaneseAndEnglishCombatMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^(?:The )?(.+) hits \\((x\\d+)\\) for (\\d+) damage with (?:his|her|its) (.+?)[.!] \\[(.+?)\\]$", "{0}の{3}で{2}ダメージを受けた。({1}) [{4}]"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "The ウォーターヴァイン農家 hits (x2) for 4 damage with his 鉄の蔓刈り斧. [17]";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ウォーターヴァイン農家の鉄の蔓刈り斧で4ダメージを受けた。(x2) [17]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesPlayerWeaponHitWithRoll_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "You hit (x1) for 1 damage with your レンチ! [18]";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("レンチで1ダメージを与えた。(x1) [18]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesMissileVitalAreaHits_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "The 熊 hits the スナップジョー in a vital area.";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("熊がスナップジョーの急所に命中させた"));

            DummyMessagingEmitMessageTarget.MessageToSend = "You hit the スナップジョー in a vital area.";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("スナップジョーの急所に命中した"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesPlayerAcidDamageMessage_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "You take 1 damage from the 腐食性ガスの acid!";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("腐食性ガスの酸で1ダメージを受けた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_PreservesNestedColorWrappersForPlayerHitWithRoll_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "{{g|You hit {{&w|(x1)}} for 1 damage with your {{fiery|燃え盛る}} {{w|青銅の短剣}}! [9]}}";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("{{g|{{fiery|燃え盛る}} {{w|青銅の短剣}}で1ダメージを与えた。({{&w|x1}}) [9]}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_PreservesNestedColorWrappersForPlayerHitWithRoll_WithControlHeader_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "\u0002hit\u001F7\u001F18\u001F\u0003{{g|You hit {{&w|(x1)}} for 1 damage with your {{w|青銅の短剣}}! [18]}}";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("{{g|{{w|青銅の短剣}}で1ダメージを与えた。({{&w|x1}}) [18]}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesThirdPersonAcidDamageMessage_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            DummyMessagingEmitMessageTarget.MessageToSend = "The ワニ takes 1 damage from the 腐食性ガスの acid!";
            DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ワニは腐食性ガスの酸で1ダメージを受けた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessagingEmitMessage_TranslatesStableRepositoryFamilies_WhenPatched()
    {
        var cases = new (string Message, string Expected)[]
        {
            ("You take 1 damage from bleeding.", "あなたは出血で1ダメージを受けた。"),
            ("The ワニ hits (x1) for 2 damage with his 噛みつき. [18]", "ワニの噛みつきで2ダメージを受けた。(x1) [18]"),
            ("The ワニ critically hits (x1) for 2 damage with his 噛みつき. [18]", "ワニの噛みつきが会心し、2ダメージを受けた。(x1) [18]"),
            ("You hit glowfish for 3 damage.", "glowfishに3ダメージを与えた"),
            ("You miss with your レンチ! [10 vs 10]", "レンチでの攻撃は外れた。[10 vs 10]"),
            ("The タム fails to penetrate your armor [17]!", "タムはあなたの装甲を貫けなかった！ [17]"),
            ("Your 鉛スラッグ fails to penetrate the フォームクリートの armor!", "あなたの鉛スラッグはフォームクリートの装甲を貫けなかった！"),
            ("The ワニ cannot reach the スナップジョー.", "ワニはスナップジョーに届かない"),
            ("Your attack passes through the ワニ!", "あなたの攻撃はワニをすり抜けた！"),
            ("One of タムの wounds stops bleeding.", "タムの傷のひとつの出血が止まった。"),
            ("The タム's nose begins to bleed.", "タムの鼻から血が流れ始めた"),
            ("The タム's brain begins to hemorrhage.", "タムの脳から出血が始まった"),
            ("The ウォーターヴァイン農家 misses you with his 鉄の蔓刈り斧! [3 vs 7]", "ウォーターヴァイン農家の鉄の蔓刈り斧は外れた。[3 vs 7]"),
            ("Poisonous goo burns your eyes.", "有毒な粘液が目に染みた。"),
            ("Putrid ooze splashes into your mouth. You gag at the awful taste.", "腐った軟泥が口に入った。ひどい味に吐き気を催した。"),
            ("Brown sludge splashes into your mouth. You wince at the metallic taste.", "茶色い汚泥が口に入った。金属の味に顔をしかめた。"),
            ("The liquids stop reacting.", "液体の反応が止まった"),
            ("The reacting liquids congeal into a SoupSludge.", "反応した液体が凝固しSoupSludgeになった"),
            ("The primordial soup nearby starts reacting with the water.", "近くの原初のスープが水と反応を始めた"),
            ("You receive tinkering bits <{{|AB}}>.", "修理ビット<{{|AB}}>を受け取った。"),
            ("You receive 奇妙な小物!", "奇妙な小物を受け取った"),
            ("You make some progress disarming 地雷.", "地雷の解除が少し進んだ。"),
            ("You reload your クローム・リボルバー with 鉛スラッグ x6.", "クローム・リボルバーに鉛スラッグ x6を装填した"),
            ("You toggle {{c|Akimbo}} on.", "{{c|二挺拳銃}}をオンにした。"),
            ("You toggle {{c|Akimbo}} off.", "{{c|二挺拳銃}}をオフにした。"),
            ("An image of タム disappears.", "タムの映像が消えた。"),
            ("The 熊's carapace loosens.", "熊の甲殻が緩んだ"),
            ("熊の carapace loosens.", "熊の甲殻が緩んだ"),
            ("The zealot mumbles inaudibly, encased in ice.", "氷に閉じ込められた狂信者が、聞き取れないほどに呟いた。"),
            ("The infected crust of skin on 熊の left arm loosens and breaks away.", "熊の left armの感染した皮膚の痂皮が緩んで剥がれ落ちた。"),
            ("You lose 3 HP.", "あなたは3HPを失った"),
            ("You recover 5 HP.", "あなたは5HP回復した"),
            ("You take 14 damage from 監視官イラメの freezing effect!", "監視官イラメの凍結効果で14ダメージを受けた！"),
            ("You take 6 damage from ドリンクスの pyrokinesis!", "ドリンクスの熱念動で6ダメージを受けた！"),
            ("The air here starts to shimmer with heat!", "このあたりの空気が熱で揺らめき始めた！"),
            ("The air here ceases shimmering with heat.", "このあたりの空気の熱による揺らめきが収まった。"),
            ("You harvest a ヴァインウェハー from the ウォーターヴァイン.", "ウォーターヴァインからヴァインウェハーを収穫した"),
            ("カロク begins flying.", "カロクが飛翔し始めた。"),
            ("シュウラシュウォレム harvests a スターアップル.", "シュウラシュウォレムはスターアップルを収穫した。"),
        };

        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyMessagingEmitMessageTarget),
                    nameof(DummyMessagingEmitMessageTarget.EmitMessage),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(char),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject)),
                typeof(GameObjectEmitMessageTranslationPatch));

            Assert.Multiple(() =>
            {
                foreach (var (message, expected) in cases)
                {
                    DummyMessageQueue.Reset();
                    DummyMessagingEmitMessageTarget.MessageToSend = message;
                    DummyMessagingEmitMessageTarget.EmitMessage(new DummyGameObject(), "unused", 'W', false, false, false);

                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected), $"source: {message}");
                }
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DeployableInfrastructurePatch_TranslatesDeployMessage_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyDeployableInfrastructureTarget),
                    nameof(DummyDeployableInfrastructureTarget.DeployOne),
                    typeof(DummyGameObject),
                    typeof(DummyCell),
                    typeof(bool),
                    typeof(bool)),
                typeof(DeployableInfrastructureTranslationPatch));

            var target = new DummyDeployableInfrastructureTarget
            {
                MessageToSend = "The 技師 deploys a タレット.",
            };

            target.DeployOne(new DummyGameObject(), new DummyCell(), message: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("技師はタレットを展開した"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerTryThawZone_TranslatesLeafMessage_WhenPatched()
    {
        WriteLeafDictionary(("ThawZone exception", "ゾーン解凍エラー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerTryThawZoneTarget), nameof(DummyZoneManagerTryThawZoneTarget.TryThawZone), typeof(string), typeof(DummyZone).MakeByRefType()),
                typeof(ZoneManagerTryThawZoneTranslationPatch));

            var target = new DummyZoneManagerTryThawZoneTarget
            {
                MessageToSend = "ThawZone exception",
            };

            target.TryThawZone("JoppaWorld.1.1.1.1.10", out _);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ゾーン解凍エラー"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("W")]
    [TestCase("r")]
    public void ZoneManagerTryThawZone_TranslatesInventoriedColorShapes_WithRepositoryLeafDictionary(string color)
    {
        UseRepositoryPatternDictionary();
        AssertZoneManagerTryThawZoneMessage("ThawZone exception", color, "ゾーン解凍エラー");
    }

    [Test]
    public void ZoneManagerTryThawZone_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("ThawZone exception", "W", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ThawZone exception"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerTryThawZone_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertZoneManagerTryThawZoneMessage(
            MessageFrameTranslator.MarkDirectTranslation("ThawZone exception"),
            "W",
            "ThawZone exception");
    }

    [Test]
    public void ZoneManagerTryThawZone_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        AssertZoneManagerTryThawZoneMessage(string.Empty, "W", string.Empty);
    }

    [Test]
    public void ZoneManagerTick_TranslatesWarning_WhenPatched()
    {
        WriteLeafDictionary(
            ("WARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.",
             "警告: ゾーンキャッシュ無効オプションが有効なため、時間の経過とともに大量のメモリを消費する。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerTickTarget), nameof(DummyZoneManagerTickTarget.Tick), typeof(bool)),
                typeof(ZoneManagerTickTranslationPatch));

            var target = new DummyZoneManagerTickTarget
            {
                MessageToSend = "&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.",
            };

            target.Tick(allowFreeze: true);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("&R警告: ゾーンキャッシュ無効オプションが有効なため、時間の経過とともに大量のメモリを消費する。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerTick_TranslatesInlineColorWarning_WithRepositoryLeafDictionary()
    {
        UseRepositoryPatternDictionary();

        AssertZoneManagerTickMessage(
            "&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.",
            null,
            "&R警告: ゾーンキャッシュ無効オプションが有効なため、時間の経過とともに大量のメモリを消費する。");
    }

    [Test]
    public void ZoneManagerTick_TranslatesColorArgumentWarning_WithRepositoryLeafDictionary()
    {
        UseRepositoryPatternDictionary();

        AssertZoneManagerTickMessage(
            "WARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.",
            "R",
            "警告: ゾーンキャッシュ無効オプションが有効なため、時間の経過とともに大量のメモリを消費する。");
    }

    [Test]
    public void ZoneManagerTick_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(
                "&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.",
                null,
                Capitalize: false);

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerTick_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertZoneManagerTickMessage(
            MessageFrameTranslator.MarkDirectTranslation(
                "&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time."),
            null,
            "&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.");
    }

    [Test]
    public void ZoneManagerTick_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        AssertZoneManagerTickMessage(string.Empty, "R", string.Empty);
    }

    [Test]
    public void ZoneManagerSetActiveZoneMapNotes_TranslatesMapNotesUsingRepositoryPattern_WhenPatched()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerMapNotesTarget), nameof(DummyZoneManagerMapNotesTarget.SetActiveZone), typeof(DummyZone)),
                typeof(ZoneManagerSetActiveZoneMapNotesTranslationPatch));

            var target = new DummyZoneManagerMapNotesTarget
            {
                MessageToSend = "Notes: ancient bones",
            };

            target.SetActiveZone(new DummyZone());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("注記: ancient bones"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerSetActiveZoneMapNotes_PreservesMixedLocalizedNotes_WhenPatched()
    {
        WritePatternDictionary(
            ("^Notes: (.+)$", "注記: {0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerMapNotesTarget), nameof(DummyZoneManagerMapNotesTarget.SetActiveZone), typeof(DummyZone)),
                typeof(ZoneManagerSetActiveZoneMapNotesTranslationPatch));

            var target = new DummyZoneManagerMapNotesTarget
            {
                MessageToSend = "注記: ancient bones",
            };

            target.SetActiveZone(new DummyZone());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("注記: ancient bones"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerGenerateZone_TranslatesBuildFailure_WhenPatched()
    {
        WritePatternDictionary(
            ("^Zone build failure:(.+)$", "ゾーン構築失敗:{0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerGenerateZoneTarget), nameof(DummyZoneManagerGenerateZoneTarget.GenerateZone), typeof(string)),
                typeof(ZoneManagerGenerateZoneTranslationPatch));

            var target = new DummyZoneManagerGenerateZoneTarget
            {
                MessageToSend = "Zone build failure:<none>",
            };

            target.GenerateZone("JoppaWorld.1.1.1.1.10");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("ゾーン構築失敗:<none>"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerGenerateZone_TranslatesBuildFailure_WithRepositoryPattern()
    {
        UseRepositoryPatternDictionary();

        AssertZoneManagerGenerateZoneMessage(
            "Zone build failure:<none>",
            "R",
            "ゾーン構築失敗:<none>");
    }

    [Test]
    public void ZoneManagerGenerateZone_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Zone build failure:<none>", "R", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Zone build failure:<none>"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneManagerGenerateZone_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();

        AssertZoneManagerGenerateZoneMessage(
            MessageFrameTranslator.MarkDirectTranslation("Zone build failure:<none>"),
            "R",
            "Zone build failure:<none>");
    }

    [Test]
    public void ZoneManagerGenerateZone_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        UseRepositoryPatternDictionary();
        AssertZoneManagerGenerateZoneMessage(string.Empty, "R", string.Empty);
    }

    [Test]
    public void CombatGetDefenderHitDice_TranslatesShieldBlockMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You block with (.+)! \\(\\+(\\d+) AV\\)$", "{0}で防御した！ (+{1} AV)"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = "You block with iron buckler! (+2 AV)",
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("iron bucklerで防御した！ (+2 AV)"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatGetDefenderHitDice_TranslatesColorTaggedShieldBlockMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You block with (.+)! \\(\\+(\\d+) AV\\)$", "{0}で防御した！ (+{1} AV)"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = "You block with {{R|iron buckler}}! (+2 AV)",
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{R|iron buckler}}で防御した！ (+2 AV)"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatGetDefenderHitDice_TranslatesStaggerMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You stagger (.+)!$", "{0}をよろめかせた！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = "You stagger Snapjaw Scavenger!",
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Snapjaw Scavengerをよろめかせた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatGetDefenderHitDice_TranslatesShieldBlockStaggerMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You stagger (.+) with your shield block!$", "盾で受け止めて{0}をよろめかせた！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = "You stagger Snapjaw Scavenger with your shield block!",
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("盾で受け止めてSnapjaw Scavengerをよろめかせた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatGetDefenderHitDice_TranslatesStaggeredByMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You are staggered by (.+)!$", "{0}によってよろめかされた！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = "You are staggered by {{G|Girsh Nephilim}}!",
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{G|Girsh Nephilim}}によってよろめかされた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("You block with iron buckler! (+2 AV)", "iron bucklerで防御した！ (+2 AV)")]
    [TestCase("You stagger Snapjaw Scavenger with your shield block!", "盾で受け止めてSnapjaw Scavengerをよろめかせた！")]
    [TestCase("You are staggered by iron buckler's block!", "iron bucklerの防御でよろめいた！")]
    public void CombatGetDefenderHitDice_TranslatesInventoriedShapes_WithRepositoryPatterns(string source, string expected)
    {
        AssertCombatShieldQueuedMessageWithRepositoryPatterns(source, expected);
    }

    [Test]
    public void CombatMeleeAttack_TranslatesMissMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You miss! \\[(.+?) vs (.+?)\\]$", "攻撃は外れた！ [{0} vs {1}]"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyCombatMeleeAttackTarget),
                    nameof(DummyCombatMeleeAttackTarget.MeleeAttackWithWeaponInternal),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyCombatBodyPart),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatMeleeAttackTarget
            {
                MessageToSend = "{{r|You miss!}} [10 vs 14]",
                ColorToSend = null
            };

            _ = target.MeleeAttackWithWeaponInternal(new DummyGameObject(), new DummyGameObject(), new DummyGameObject(), new DummyCombatBodyPart());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{r|攻撃は外れた！}} [10 vs 14]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatMeleeAttack_TranslatesFailDamageMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^You fail to deal damage with your attack! \\[(.+?)\\]$", "あなたの攻撃はダメージを与えられなかった！ [{0}]"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyCombatMeleeAttackTarget),
                    nameof(DummyCombatMeleeAttackTarget.MeleeAttackWithWeaponInternal),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyCombatBodyPart),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatMeleeAttackTarget
            {
                MessageToSend = "You fail to deal damage with your attack! [17]",
                ColorToSend = null
            };

            _ = target.MeleeAttackWithWeaponInternal(new DummyGameObject(), new DummyGameObject(), new DummyGameObject(), new DummyCombatBodyPart());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("あなたの攻撃はダメージを与えられなかった！ [17]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatMeleeAttack_TranslatesInventoriedShapes_WithRepositoryPatterns()
    {
        var cases = new (string Source, string Expected)[]
        {
            ("You miss!", "攻撃は外れた！"),
            ("You miss with your bronze dagger! [10 vs 14]", "bronze daggerでの攻撃は外れた。[10 vs 14]"),
            ("You miss! [10 vs 14]", "攻撃は外れた！ [10 vs 14]"),
            ("Snapjaw Scavenger misses you!", "Snapjaw Scavengerの攻撃は外れた"),
            ("Snapjaw Scavenger misses you with its bronze dagger! [10 vs 14]", "Snapjaw Scavengerのbronze daggerは外れた。[10 vs 14]"),
            ("Snapjaw Scavenger misses you! [10 vs 14]", "Snapjaw Scavengerの攻撃は外れた！ [10 vs 14]"),
            ("Your mental attack does not affect Snapjaw Scavenger.", "あなたの精神攻撃はSnapjaw Scavengerに効かない。"),
            ("You fail to deal damage with your attack! [17]", "あなたの攻撃はダメージを与えられなかった！ [17]"),
            ("Snapjaw Scavenger fails to deal damage with its attack! [17]", "Snapjaw Scavengerの攻撃はダメージを与えられなかった！ [17]"),
            ("You don't penetrate Snapjaw Scavenger's armor.", "Snapjaw Scavengerの装甲を貫けなかった！"),
            ("You don't penetrate Snapjaw Scavenger's armor with your bronze dagger. [17]", "bronze daggerではSnapjaw Scavengerの装甲を貫けなかった！ [17]"),
            ("You don't penetrate Snapjaw Scavenger's armor. [17]", "Snapjaw Scavengerの装甲を貫けなかった！ [17]"),
            ("Snapjaw Scavenger doesn't penetrate your armor.", "Snapjaw Scavengerはあなたの装甲を貫けなかった！"),
            ("Snapjaw Scavenger doesn't penetrate your armor with its bronze dagger! [17]", "Snapjaw Scavengerはbronze daggerであなたの装甲を貫けなかった！ [17]"),
            ("Snapjaw Scavenger doesn't penetrate your armor! [17]", "Snapjaw Scavengerはあなたの装甲を貫けなかった！ [17]"),
        };

        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyCombatMeleeAttackTarget),
                    nameof(DummyCombatMeleeAttackTarget.MeleeAttackWithWeaponInternal),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyCombatBodyPart),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatMeleeAttackTarget();

            Assert.Multiple(() =>
            {
                foreach (var (source, expected) in cases)
                {
                    DummyMessageQueue.Reset();
                    target.MessageToSend = source;

                    _ = target.MeleeAttackWithWeaponInternal(new DummyGameObject(), new DummyGameObject(), new DummyGameObject(), new DummyCombatBodyPart());

                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected), $"source: {source}");
                }
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatTextSurface_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You miss!", null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You miss!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatTextSurface_DoesNotTranslateMeleeShapeInShieldOwnerScope()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = "You miss!",
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You miss!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatTextSurface_DoesNotTranslateShieldShapeInMeleeOwnerScope()
    {
        AssertCombatMeleeAttackQueuedMessageWithRepositoryPatterns(
            "You block with iron buckler! (+2 AV)",
            "You block with iron buckler! (+2 AV)");
    }

    [Test]
    public void CombatTextSurface_RestoresOuterOwnerRouteAfterNestedOwnerScope()
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        var shieldOwner = RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent));
        var meleeOwner = RequireMethod(
            typeof(DummyCombatMeleeAttackTarget),
            nameof(DummyCombatMeleeAttackTarget.MeleeAttackWithWeaponInternal),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(DummyCombatBodyPart),
            typeof(string),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(bool));

        try
        {
            PatchQueue(harmony);

            CombatTextSurfaceTranslationPatch.Prefix(shieldOwner);
            try
            {
                DummyMessageQueue.AddPlayerMessage("You block with iron buckler! (+2 AV)", null, Capitalize: false);
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("iron bucklerで防御した！ (+2 AV)"));

                CombatTextSurfaceTranslationPatch.Prefix(meleeOwner);
                try
                {
                    DummyMessageQueue.AddPlayerMessage("You miss!", null, Capitalize: false);
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("攻撃は外れた！"));

                    DummyMessageQueue.AddPlayerMessage("You block with iron buckler! (+2 AV)", null, Capitalize: false);
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You block with iron buckler! (+2 AV)"));
                }
                finally
                {
                    CombatTextSurfaceTranslationPatch.Finalizer(null);
                }

                DummyMessageQueue.AddPlayerMessage("You block with iron buckler! (+2 AV)", null, Capitalize: false);
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("iron bucklerで防御した！ (+2 AV)"));

                DummyMessageQueue.AddPlayerMessage("You miss!", null, Capitalize: false);
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You miss!"));
            }
            finally
            {
                CombatTextSurfaceTranslationPatch.Finalizer(null);
            }

            DummyMessageQueue.AddPlayerMessage("You block with iron buckler! (+2 AV)", null, Capitalize: false);
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You block with iron buckler! (+2 AV)"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CombatTextSurface_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertCombatShieldQueuedMessageWithRepositoryPatterns(
            MessageFrameTranslator.MarkDirectTranslation("You block with iron buckler! (+2 AV)"),
            "You block with iron buckler! (+2 AV)");
        AssertCombatMeleeAttackQueuedMessageWithRepositoryPatterns(
            MessageFrameTranslator.MarkDirectTranslation("You miss!"),
            "You miss!");
    }

    [Test]
    public void CombatTextSurface_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyCombatMeleeAttackTarget),
                    nameof(DummyCombatMeleeAttackTarget.MeleeAttackWithWeaponInternal),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyCombatBodyPart),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)),
                typeof(CombatTextSurfaceTranslationPatch));

            var shieldTarget = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = string.Empty,
            };
            var meleeTarget = new DummyCombatMeleeAttackTarget
            {
                MessageToSend = string.Empty,
            };

            shieldTarget.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(string.Empty));

            DummyMessageQueue.Reset();

            meleeTarget.MeleeAttackWithWeaponInternal(new DummyGameObject(), new DummyGameObject(), new DummyGameObject(), new DummyCombatBodyPart());
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void AssertCombatShieldQueuedMessageWithRepositoryPatterns(string source, string expected)
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCombatGetDefenderHitDiceTarget), nameof(DummyCombatGetDefenderHitDiceTarget.HandleEvent), typeof(DummyCombatGetDefenderHitDiceEvent)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatGetDefenderHitDiceTarget
            {
                MessageToSend = source,
            };

            target.HandleEvent(new DummyCombatGetDefenderHitDiceEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void AssertCombatMeleeAttackQueuedMessageWithRepositoryPatterns(string source, string expected)
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyCombatMeleeAttackTarget),
                    nameof(DummyCombatMeleeAttackTarget.MeleeAttackWithWeaponInternal),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(DummyCombatBodyPart),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)),
                typeof(CombatTextSurfaceTranslationPatch));

            var target = new DummyCombatMeleeAttackTarget
            {
                MessageToSend = source,
            };

            _ = target.MeleeAttackWithWeaponInternal(new DummyGameObject(), new DummyGameObject(), new DummyGameObject(), new DummyCombatBodyPart());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertDoorAttemptOpenMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyDoorAttemptOpenTarget),
                    nameof(DummyDoorAttemptOpenTarget.AttemptOpen),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(object)),
                typeof(DoorAttemptOpenTranslationPatch));

            var target = new DummyDoorAttemptOpenTarget
            {
                MessageToSend = message,
            };

            target.AttemptOpen();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectMoveQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, GameObjectMoveMethod(), typeof(GameObjectMoveTranslationPatch));

            var target = new DummyGameObjectMoveTarget
            {
                MessageToSend = message,
            };

            target.Move("N", out _);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectPerformThrowQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyGameObjectPerformThrowTarget),
                    nameof(DummyGameObjectPerformThrowTarget.PerformThrow),
                    typeof(DummyGameObject),
                    typeof(DummyCell),
                    typeof(DummyGameObject),
                    typeof(DummyMissilePath),
                    typeof(int),
                    typeof(int?),
                    typeof(int?),
                    typeof(int?)),
                typeof(GameObjectPerformThrowTranslationPatch));

            var target = new DummyGameObjectPerformThrowTarget
            {
                MessageToSend = message,
            };

            target.PerformThrow(new DummyGameObject(), new DummyCell());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMissileWeaponHitQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.MissileWeaponHit)),
                typeof(MissileWeaponHitTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.MissileWeaponHit();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectToggleActivatedAbilityQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectToggleActivatedAbilityTarget), nameof(DummyGameObjectToggleActivatedAbilityTarget.ToggleActivatedAbility), typeof(Guid), typeof(bool), typeof(bool?)),
                typeof(GameObjectToggleActivatedAbilityTranslationPatch));

            var target = new DummyGameObjectToggleActivatedAbilityTarget
            {
                MessageToSend = message,
            };

            target.ToggleActivatedAbility(Guid.NewGuid());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectPopupShowYesNoAsync(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNoAsync(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectPopupTarget), nameof(DummyGameObjectPopupTarget.ConfirmUseImportantAsync)),
                typeof(GameObjectPopupTranslationPatch));

            var target = new DummyGameObjectPopupTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.ConfirmUseImportantAsync().GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectPopupShowYesNo(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectPopupTarget), nameof(DummyGameObjectPopupTarget.ConfirmUseImportant)),
                typeof(GameObjectPopupTranslationPatch));

            var target = new DummyGameObjectPopupTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.ConfirmUseImportant();

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectPopupHandleRename(string message, string expected, bool useShowFail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            if (useShowFail)
            {
                PatchPopupShowFail(harmony);
            }
            else
            {
                PatchPopupShow(harmony);
            }

            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectPopupTarget), nameof(DummyGameObjectPopupTarget.HandleRename)),
                typeof(GameObjectPopupTranslationPatch));

            var target = new DummyGameObjectPopupTarget
            {
                PopupMessageToSend = message,
                UseShowFail = useShowFail,
            };

            target.HandleRename();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectPopupChangeCompanionAbilityUse(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectPopupTarget), nameof(DummyGameObjectPopupTarget.ChangeCompanionAbilityUse)),
                typeof(GameObjectPopupTranslationPatch));

            var target = new DummyGameObjectPopupTarget
            {
                PopupMessageToSend = message,
            };

            target.ChangeCompanionAbilityUse();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGameObjectPopupCheckCompanionDirection(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectPopupTarget), nameof(DummyGameObjectPopupTarget.CheckCompanionDirection)),
                typeof(GameObjectPopupTranslationPatch));

            var target = new DummyGameObjectPopupTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.CheckCompanionDirection();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertRealityStabilizedInterdictPopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyRealityStabilizedInterdictTarget), methodName),
                typeof(RealityStabilizedInterdictTranslationPatch));

            var target = new DummyRealityStabilizedInterdictTarget
            {
                PopupMessageToSend = message,
            };

            RequireMethod(typeof(DummyRealityStabilizedInterdictTarget), methodName).Invoke(target, null);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertHackingSifrahResultPopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyHackingSifrahResultTarget), methodName),
                typeof(HackingSifrahResultTranslationPatch));

            var target = new DummyHackingSifrahResultTarget
            {
                PopupMessageToSend = message,
            };

            RequireMethod(typeof(DummyHackingSifrahResultTarget), methodName).Invoke(target, null);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertQuestLifecyclePopup(string methodName, string message, string expected)
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyQuestLifecyclePopupTarget), methodName),
                typeof(QuestLifecyclePopupTranslationPatch));

            var target = new DummyQuestLifecyclePopupTarget
            {
                PopupMessageToSend = message,
            };

            RequireMethod(typeof(DummyQuestLifecyclePopupTarget), methodName).Invoke(target, null);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertQuestLifecycleFinishStepShowBlock(string message, int stepXp, string expected)
    {
        UseRepositoryPatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowBlock(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyQuestLifecyclePopupTarget), nameof(DummyQuestLifecyclePopupTarget.ShowFinishStepPopup)),
                typeof(QuestLifecyclePopupTranslationPatch));

            var target = new DummyQuestLifecyclePopupTarget
            {
                PopupMessageToSend = message,
                StepXpToSend = stepXp,
            };

            target.ShowFinishStepPopup();

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertFlightMessage(string methodName, string message, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyFlightTarget), methodName),
                typeof(FlightTranslationPatch));

            var target = new DummyFlightTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            RequireMethod(typeof(DummyFlightTarget), methodName).Invoke(target, null);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertBodyQueuedMessage(string methodName, string message, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyBodyTarget), methodName),
                typeof(BodyTranslationPatch));

            var target = new DummyBodyTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            RequireMethod(typeof(DummyBodyTarget), methodName).Invoke(target, null);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertBodyPopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyBodyTarget), methodName),
                typeof(BodyTranslationPatch));

            var target = new DummyBodyTarget
            {
                PopupMessageToSend = message,
            };

            RequireMethod(typeof(DummyBodyTarget), methodName).Invoke(target, null);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertItemModdingSifrahPopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyItemModdingSifrahTarget), methodName),
                typeof(ItemModdingSifrahTranslationPatch));

            var target = new DummyItemModdingSifrahTarget
            {
                PopupMessageToSend = message,
            };

            RequireMethod(typeof(DummyItemModdingSifrahTarget), methodName).Invoke(target, null);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSunderMindQueuedMessage(string methodName, string message, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySunderMindTarget), methodName),
                typeof(SunderMindTranslationPatch));

            var target = new DummySunderMindTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            RequireMethod(typeof(DummySunderMindTarget), methodName).Invoke(target, null);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSunderMindBeginSunderQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySunderMindTarget), nameof(DummySunderMindTarget.BeginSunder), typeof(bool)),
                typeof(SunderMindTranslationPatch));

            var target = new DummySunderMindTarget
            {
                MessageToSend = message,
            };

            target.BeginSunder(usePopup: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSunderMindBeginSunderPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySunderMindTarget), nameof(DummySunderMindTarget.BeginSunder), typeof(bool)),
                typeof(SunderMindTranslationPatch));

            var target = new DummySunderMindTarget
            {
                PopupMessageToSend = message,
            };

            target.BeginSunder(usePopup: true);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLiquidWarmStaticQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidWarmStaticTarget), methodName, typeof(bool)),
                typeof(LiquidWarmStaticTranslationPatch));

            var target = new DummyLiquidWarmStaticTarget
            {
                MessageToSend = message,
            };

            RequireMethod(typeof(DummyLiquidWarmStaticTarget), methodName, typeof(bool)).Invoke(target, new object[] { false });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLiquidWarmStaticPopupMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidWarmStaticTarget), methodName, typeof(bool)),
                typeof(LiquidWarmStaticTranslationPatch));

            var target = new DummyLiquidWarmStaticTarget
            {
                PopupMessageToSend = message,
            };

            RequireMethod(typeof(DummyLiquidWarmStaticTarget), methodName, typeof(bool)).Invoke(target, new object[] { true });

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertKeybindsScreenConflictYesNoAsync(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNoAsync(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyKeybindsScreenConflictTarget), methodName),
                typeof(KeybindsScreenConflictTranslationPatch));

            var target = new DummyKeybindsScreenConflictTarget
            {
                PopupMessageToSend = message,
            };

            _ = ((Task<bool>)RequireMethod(typeof(DummyKeybindsScreenConflictTarget), methodName).Invoke(target, null)!).GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertKeybindsScreenConflictShowAsync(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowAsync(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyKeybindsScreenConflictTarget), nameof(DummyKeybindsScreenConflictTarget.RequiredConflictBind)),
                typeof(KeybindsScreenConflictTranslationPatch));

            var target = new DummyKeybindsScreenConflictTarget
            {
                PopupMessageToSend = message,
            };

            target.RequiredConflictBind().GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertRealityStabilizedEventQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyRealityStabilizedEventTarget), methodName, typeof(bool))
                : RequireMethod(typeof(DummyRealityStabilizedEventTarget), methodName);
            PatchOwner(harmony, original, typeof(RealityStabilizedEventTranslationPatch));

            var target = new DummyRealityStabilizedEventTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice), StringComparison.Ordinal))
            {
                target.ShortCircuitDevice(usePopup: false);
            }
            else if (string.Equals(methodName, nameof(DummyRealityStabilizedEventTarget.FailedToContest), StringComparison.Ordinal))
            {
                target.FailedToContest();
            }
            else
            {
                target.TryContest();
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertRealityStabilizedEventPopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            MethodInfo original = methodName switch
            {
                nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice) =>
                    RequireMethod(typeof(DummyRealityStabilizedEventTarget), methodName, typeof(bool)),
                nameof(DummyRealityStabilizedEventTarget.FailedToContestPopup) =>
                    RequireMethod(typeof(DummyRealityStabilizedEventTarget), methodName),
                nameof(DummyRealityStabilizedEventTarget.OptionToContest) =>
                    RequireMethod(typeof(DummyRealityStabilizedEventTarget), methodName),
                _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
            };
            PatchOwner(harmony, original, typeof(RealityStabilizedEventTranslationPatch));

            var target = new DummyRealityStabilizedEventTarget
            {
                PopupMessageToSend = message,
            };

            switch (methodName)
            {
                case nameof(DummyRealityStabilizedEventTarget.ShortCircuitDevice):
                    target.ShortCircuitDevice(usePopup: true);
                    break;
                case nameof(DummyRealityStabilizedEventTarget.FailedToContestPopup):
                    target.FailedToContestPopup();
                    break;
                case nameof(DummyRealityStabilizedEventTarget.OptionToContest):
                    target.OptionToContest();
                    break;
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMassMindQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                typeof(MassMindTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertCyberneticRejectionSyndromeQueuedMessage(
        string methodName,
        string message,
        string? color,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                methodName switch
                {
                    nameof(DummyCyberneticRejectionSyndromeTarget.Apply) => RequireMethod(
                        typeof(DummyCyberneticRejectionSyndromeTarget),
                        methodName,
                        typeof(DummyGameObject)),
                    nameof(DummyCyberneticRejectionSyndromeTarget.Remove) => RequireMethod(
                        typeof(DummyCyberneticRejectionSyndromeTarget),
                        methodName,
                        typeof(DummyGameObject)),
                    _ => RequireMethod(typeof(DummyCyberneticRejectionSyndromeTarget), methodName, typeof(int)),
                },
                typeof(CyberneticRejectionSyndromeTranslationPatch));

            var target = new DummyCyberneticRejectionSyndromeTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            switch (methodName)
            {
                case nameof(DummyCyberneticRejectionSyndromeTarget.Apply):
                    _ = target.Apply(new DummyGameObject());
                    break;
                case nameof(DummyCyberneticRejectionSyndromeTarget.Remove):
                    target.Remove(new DummyGameObject());
                    break;
                default:
                    target.Reduce(1);
                    break;
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGeomagneticDiscPopup(
        string methodName,
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            if (string.Equals(methodName, nameof(DummyGeomagneticDiscTarget.ExamineFailure), StringComparison.Ordinal))
            {
                PatchPopupShow(harmony);
            }
            else
            {
                PatchPopupShowFail(harmony);
            }

            MethodInfo original = string.Equals(methodName, nameof(DummyGeomagneticDiscTarget.ExamineFailure), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyGeomagneticDiscTarget), methodName, typeof(DummyExamineEvent), typeof(int))
                : RequireMethod(typeof(DummyGeomagneticDiscTarget), methodName, typeof(DummyGameObject));
            PatchOwner(harmony, original, typeof(GeomagneticDiscTranslationPatch));

            var target = new DummyGeomagneticDiscTarget
            {
                PopupMessageToSend = message,
            };

            switch (methodName)
            {
                case nameof(DummyGeomagneticDiscTarget.SignalFailure):
                    target.SignalFailure(new DummyGameObject());
                    break;
                case nameof(DummyGeomagneticDiscTarget.SignalLowPower):
                    target.SignalLowPower(new DummyGameObject());
                    break;
                default:
                    _ = target.ExamineFailure(new DummyExamineEvent(), 100);
                    break;
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyPopupShow.LastShowMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertCampfireCookAvailabilityPopup(
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCampfireCookTarget), nameof(DummyCampfireCookTarget.Cook)),
                typeof(CampfireCookAvailabilityTranslationPatch));

            var target = new DummyCampfireCookTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.Cook();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyPopupShow.LastShowMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertTeleprojectorPopup(
        string methodName,
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            if (string.Equals(methodName, nameof(DummyTeleprojectorTarget.ActivateTeleprojector), StringComparison.Ordinal))
            {
                PatchPopupShowFail(harmony);
            }
            else
            {
                PatchPopupShow(harmony);
            }

            MethodInfo original = methodName switch
            {
                nameof(DummyTeleprojectorTarget.HandleEvent) => RequireMethod(
                    typeof(DummyTeleprojectorTarget),
                    methodName,
                    typeof(DummyBootSequenceDoneEvent)),
                nameof(DummyTeleprojectorTarget.RoboDom) => RequireMethod(
                    typeof(DummyTeleprojectorTarget),
                    methodName,
                    typeof(DummyMentalAttackEvent)),
                _ => RequireMethod(typeof(DummyTeleprojectorTarget), methodName),
            };
            PatchOwner(harmony, original, typeof(TeleprojectorTranslationPatch));

            var target = new DummyTeleprojectorTarget
            {
                PopupMessageToSend = message,
            };

            switch (methodName)
            {
                case nameof(DummyTeleprojectorTarget.HandleEvent):
                    _ = target.HandleEvent(new DummyBootSequenceDoneEvent());
                    break;
                case nameof(DummyTeleprojectorTarget.RoboDom):
                    _ = target.RoboDom(new DummyMentalAttackEvent());
                    break;
                default:
                    _ = target.ActivateTeleprojector();
                    break;
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyPopupShow.LastShowMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertTombAnchorSystemQueuedMessage(
        string methodName,
        string message,
        string? color,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyTombAnchorSystemTarget.Recall), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyTombAnchorSystemTarget), methodName, typeof(DummyZone))
                : RequireMethod(typeof(DummyTombAnchorSystemTarget), methodName);
            PatchOwner(harmony, original, typeof(TombAnchorSystemTranslationPatch));

            var target = new DummyTombAnchorSystemTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            switch (methodName)
            {
                case nameof(DummyTombAnchorSystemTarget.OnEndTurn):
                    target.OnEndTurn();
                    break;
                case nameof(DummyTombAnchorSystemTarget.AnchorCall):
                    target.AnchorCall();
                    break;
                default:
                    target.Recall(new DummyZone());
                    break;
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertCyberneticsMedassistModulePopup(
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCyberneticsMedassistModuleTarget), nameof(DummyCyberneticsMedassistModuleTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(CyberneticsMedassistModuleTranslationPatch));

            var target = new DummyCyberneticsMedassistModuleTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyPopupShow.LastShowMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertCyberneticsMedassistModuleQueuedMessage(
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCyberneticsMedassistModuleTarget), nameof(DummyCyberneticsMedassistModuleTarget.AttemptMedicalAssistance), typeof(DummyDamage)),
                typeof(CyberneticsMedassistModuleTranslationPatch));

            var target = new DummyCyberneticsMedassistModuleTarget
            {
                MessageToSend = message,
            };

            target.AttemptMedicalAssistance(new DummyDamage());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0 && expected.Contains(captureToken, StringComparison.Ordinal))
            {
                Assert.That(DummyMessageQueue.LastMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLiquidLoaderQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidLoaderTarget), nameof(DummyLiquidLoaderTarget.HandleEvent), typeof(DummyCommandReloadEvent)),
                typeof(LiquidLoaderTranslationPatch));

            var target = new DummyLiquidLoaderTarget
            {
                MessageToSend = message,
            };

            _ = target.HandleEvent(new DummyCommandReloadEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLiquidLoaderEventMessage(
        Type eventType,
        string message,
        string expected,
        bool expectedIsMarked = true)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            var method = RequireMethod(typeof(DummyLiquidLoaderTarget), nameof(DummyLiquidLoaderTarget.HandleEvent), eventType);
            harmony.Patch(
                original: method,
                prefix: new HarmonyMethod(RequireMethod(typeof(LiquidLoaderTranslationPatch), nameof(LiquidLoaderTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(LiquidLoaderTranslationPatch), nameof(LiquidLoaderTranslationPatch.Postfix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(LiquidLoaderTranslationPatch), nameof(LiquidLoaderTranslationPatch.Finalizer))));

            var target = new DummyLiquidLoaderTarget
            {
                MessageToSend = message,
            };
            var eventObject = Activator.CreateInstance(eventType)
                ?? throw new InvalidOperationException("Dummy liquid loader event could not be created.");

            _ = method.Invoke(target, [eventObject]);

            var field = eventType.GetField("Message")
                ?? throw new InvalidOperationException("Dummy liquid loader event lacks Message field.");
            var expectedFieldValue = expectedIsMarked
                ? MessageFrameTranslator.MarkDirectTranslation(expected)
                : expected;
            Assert.That(field.GetValue(eventObject), Is.EqualTo(expectedFieldValue));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLiquidLoaderPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidLoaderTarget), nameof(DummyLiquidLoaderTarget.FireEvent), typeof(DummyEvent)),
                typeof(LiquidLoaderTranslationPatch));

            var target = new DummyLiquidLoaderTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPhysicAmputateLimbPopup(string message, string expected)
    {
        UseRepositoryPatternDictionary();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.PhysicAmputateLimbFireEvent), typeof(DummyEvent)),
                typeof(PhysicAmputateLimbTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.PhysicAmputateLimbFireEvent(new DummyEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertTrollKingQueuedMessage(
        string methodName,
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyTrollKingTarget), methodName, typeof(int)),
                typeof(TrollKingTranslationPatch));

            var target = new DummyTrollKingTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyTrollKingTarget.CheckSpawn), StringComparison.Ordinal))
            {
                target.CheckSpawn(1);
            }
            else
            {
                target.StopBudding(1);
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyMessageQueue.LastMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMutatingQueuedMessage(
        string methodName,
        string message,
        string? color,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyMutatingTarget.Apply), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyMutatingTarget), methodName, typeof(DummyGameObject))
                : RequireMethod(typeof(DummyMutatingTarget), methodName, typeof(DummyEndTurnEvent), typeof(bool));
            PatchOwner(harmony, original, typeof(MutatingTranslationPatch));

            var target = new DummyMutatingTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            if (string.Equals(methodName, nameof(DummyMutatingTarget.Apply), StringComparison.Ordinal))
            {
                _ = target.Apply(new DummyGameObject());
            }
            else
            {
                _ = target.HandleEvent(new DummyEndTurnEvent(), usePopup: false);
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMutatingPopup(
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyMutatingTarget), nameof(DummyMutatingTarget.HandleEvent), typeof(DummyEndTurnEvent), typeof(bool)),
                typeof(MutatingTranslationPatch));

            var target = new DummyMutatingTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.HandleEvent(new DummyEndTurnEvent(), usePopup: true);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyPopupShow.LastShowMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertQuillsQueuedMessage(
        string methodName,
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyQuillsTarget.HandleEvent), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyQuillsTarget), methodName, typeof(DummyTookDamageEvent))
                : RequireMethod(typeof(DummyQuillsTarget), methodName, typeof(DummyEvent));
            PatchOwner(harmony, original, typeof(QuillsTranslationPatch));

            var target = new DummyQuillsTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyQuillsTarget.HandleEvent), StringComparison.Ordinal))
            {
                _ = target.HandleEvent(new DummyTookDamageEvent());
            }
            else
            {
                _ = target.FireEvent(new DummyEvent());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyMessageQueue.LastMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLightManipulationQueuedMessage(
        string methodName,
        string message,
        string? color,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyLightManipulationTarget.Lase), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyLightManipulationTarget), methodName, typeof(DummyCell), typeof(int))
                : RequireMethod(typeof(DummyLightManipulationTarget), methodName, typeof(DummyCommandEvent), typeof(bool));
            PatchOwner(harmony, original, typeof(LightManipulationTranslationPatch));

            var target = new DummyLightManipulationTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            if (string.Equals(methodName, nameof(DummyLightManipulationTarget.Lase), StringComparison.Ordinal))
            {
                _ = target.Lase(new DummyCell(), 0);
            }
            else
            {
                _ = target.HandleEvent(new DummyCommandEvent(), usePopup: false);
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLightManipulationPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLightManipulationTarget), nameof(DummyLightManipulationTarget.HandleEvent), typeof(DummyCommandEvent), typeof(bool)),
                typeof(LightManipulationTranslationPatch));

            var target = new DummyLightManipulationTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.HandleEvent(new DummyCommandEvent(), usePopup: true);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLatchesOnQueuedMessage(
        string methodName,
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyLatchesOnTarget.HandleEvent), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyLatchesOnTarget), methodName, typeof(DummyUnequippedEvent))
                : RequireMethod(typeof(DummyLatchesOnTarget), methodName, typeof(DummyEvent));
            PatchOwner(harmony, original, typeof(LatchesOnTranslationPatch));

            var target = new DummyLatchesOnTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyLatchesOnTarget.HandleEvent), StringComparison.Ordinal))
            {
                _ = target.HandleEvent(new DummyUnequippedEvent());
            }
            else
            {
                _ = target.FireEvent(new DummyEvent());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyMessageQueue.LastMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertAsleepOwnerQueuedMessage(string methodKey, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, RequireAsleepOwnerMethod(methodKey), typeof(AsleepOwnerTranslationPatch));

            var target = new DummyAsleepOwnerTarget
            {
                MessageToSend = message,
            };

            switch (methodKey)
            {
                case "Apply":
                    _ = target.Apply(new DummyGameObject());
                    break;
                case "BeginTakeAction":
                    _ = target.HandleEvent(new DummyBeginTakeActionEvent());
                    break;
                case "InventoryAction":
                    _ = target.HandleEvent(new DummyInventoryActionEvent(), usePopup: false);
                    break;
                default:
                    Assert.Fail($"Unknown Asleep owner method key: {methodKey}");
                    break;
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertAsleepOwnerPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyAsleepOwnerTarget), nameof(DummyAsleepOwnerTarget.HandleEvent), typeof(DummyInventoryActionEvent), typeof(bool)),
                typeof(AsleepOwnerTranslationPatch));

            var target = new DummyAsleepOwnerTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent(), usePopup: true);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireAsleepOwnerMethod(string methodKey)
    {
        return methodKey switch
        {
            "Apply" => RequireMethod(typeof(DummyAsleepOwnerTarget), nameof(DummyAsleepOwnerTarget.Apply), typeof(DummyGameObject)),
            "BeginTakeAction" => RequireMethod(typeof(DummyAsleepOwnerTarget), nameof(DummyAsleepOwnerTarget.HandleEvent), typeof(DummyBeginTakeActionEvent)),
            "InventoryAction" => RequireMethod(typeof(DummyAsleepOwnerTarget), nameof(DummyAsleepOwnerTarget.HandleEvent), typeof(DummyInventoryActionEvent), typeof(bool)),
            _ => throw new ArgumentOutOfRangeException(nameof(methodKey), methodKey, "Unknown Asleep owner method key."),
        };
    }

    private static void AssertBuddingQueuedMessage(
        string methodName,
        string message,
        string captureToken,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyBuddingTarget), methodName, typeof(DummyGameObject)),
                typeof(BuddingTranslationPatch));

            var target = new DummyBuddingTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyBuddingTarget.Apply), StringComparison.Ordinal))
            {
                _ = target.Apply(new DummyGameObject());
            }
            else
            {
                target.Remove(new DummyGameObject());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            if (captureToken.Length > 0)
            {
                Assert.That(DummyMessageQueue.LastMessage, Does.Contain(captureToken));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertBeguilingQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            MethodInfo original = string.Equals(methodName, nameof(DummyBeguilingTarget.Cast), StringComparison.Ordinal)
                ? RequireMethod(
                    typeof(DummyBeguilingTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyBeguilingTarget),
                    typeof(DummyEvent),
                    typeof(int))
                : RequireMethod(typeof(DummyBeguilingTarget), methodName, typeof(DummyMentalAttackEvent));
            PatchOwner(harmony, original, typeof(BeguilingTranslationPatch));

            if (string.Equals(methodName, nameof(DummyBeguilingTarget.Cast), StringComparison.Ordinal))
            {
                DummyBeguilingTarget.StaticMessageToSend = message;
                DummyBeguilingTarget.StaticColorToSend = null;
                DummyBeguilingTarget.StaticPopupMessageToSend = null;
                _ = DummyBeguilingTarget.Cast(new DummyGameObject(), new DummyBeguilingTarget(), new DummyEvent(), 1);
            }
            else
            {
                var target = new DummyBeguilingTarget
                {
                    MessageToSend = message,
                };
                _ = target.Beguile(new DummyMentalAttackEvent());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyBeguilingTarget.StaticMessageToSend = string.Empty;
            DummyBeguilingTarget.StaticColorToSend = null;
            DummyBeguilingTarget.StaticPopupMessageToSend = null;
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertBeguilingPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyBeguilingTarget),
                    nameof(DummyBeguilingTarget.Cast),
                    typeof(DummyGameObject),
                    typeof(DummyBeguilingTarget),
                    typeof(DummyEvent),
                    typeof(int)),
                typeof(BeguilingTranslationPatch));

            DummyBeguilingTarget.StaticMessageToSend = string.Empty;
            DummyBeguilingTarget.StaticPopupMessageToSend = message;
            _ = DummyBeguilingTarget.Cast(new DummyGameObject(), new DummyBeguilingTarget(), new DummyEvent(), 1);

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyBeguilingTarget.StaticMessageToSend = string.Empty;
            DummyBeguilingTarget.StaticColorToSend = null;
            DummyBeguilingTarget.StaticPopupMessageToSend = null;
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertAscensionCablePopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyAscensionCableTarget), methodName, typeof(DummyGameObject), typeof(bool)),
                typeof(AscensionCableTranslationPatch));

            var target = new DummyAscensionCableTarget
            {
                PopupMessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyAscensionCableTarget.TryAscend), StringComparison.Ordinal))
            {
                _ = target.TryAscend(new DummyGameObject(), fromDialog: true);
            }
            else
            {
                _ = target.TryDescend(new DummyGameObject(), fromDialog: true);
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertCarapaceTightenPopup(string message, string expected)
    {
        AssertCarapacePopup(nameof(DummyCarapaceTarget.Tighten), message, expected);
    }

    private static void AssertCarapaceLoosenPopup(string message, string expected)
    {
        AssertCarapacePopup(nameof(DummyCarapaceTarget.Loosen), message, expected);
    }

    private static void AssertCarapacePopup(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyCarapaceTarget), methodName, typeof(bool)),
                typeof(CarapaceTranslationPatch));

            var target = new DummyCarapaceTarget
            {
                PopupMessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyCarapaceTarget.Tighten), StringComparison.Ordinal))
            {
                target.Tighten(message: true);
            }
            else
            {
                target.Loosen(message: true);
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSvardymSystemQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySvardymSystemTarget), methodName),
                typeof(SvardymSystemTranslationPatch));

            var target = new DummySvardymSystemTarget
            {
                FirstMessageToSend = message,
                SecondMessageToSend = string.Empty,
            };

            if (string.Equals(methodName, nameof(DummySvardymSystemTarget.BeginStorm), StringComparison.Ordinal))
            {
                target.BeginStorm();
            }
            else
            {
                target.Tick();
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPhasedQueuedMessage(string methodKey, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, RequirePhasedMethod(methodKey), typeof(PhasedTranslationPatch));

            var target = new DummyPhasedTarget
            {
                MessageToSend = message,
            };

            switch (methodKey)
            {
                case "EffectApplied":
                    _ = target.HandleEvent(new DummyEffectAppliedEvent());
                    break;
                case "BeginTakeAction":
                    _ = target.HandleEvent(new DummyBeginTakeActionEvent());
                    break;
                case "Remove":
                    target.Remove(new DummyGameObject());
                    break;
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequirePhasedMethod(string methodKey)
    {
        return methodKey switch
        {
            "EffectApplied" => RequireMethod(typeof(DummyPhasedTarget), nameof(DummyPhasedTarget.HandleEvent), typeof(DummyEffectAppliedEvent)),
            "BeginTakeAction" => RequireMethod(typeof(DummyPhasedTarget), nameof(DummyPhasedTarget.HandleEvent), typeof(DummyBeginTakeActionEvent)),
            "Remove" => RequireMethod(typeof(DummyPhasedTarget), nameof(DummyPhasedTarget.Remove), typeof(DummyGameObject)),
            _ => throw new ArgumentOutOfRangeException(nameof(methodKey), methodKey, "Unknown phased method key."),
        };
    }

    private static void AssertPersuasionRebukeRobotQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPersuasionRebukeRobotTarget), nameof(DummyPersuasionRebukeRobotTarget.Rebuke), typeof(DummyMentalAttackEvent)),
                typeof(PersuasionRebukeRobotTranslationPatch));

            var target = new DummyPersuasionRebukeRobotTarget
            {
                MessageToSend = message,
            };

            _ = target.Rebuke(new DummyMentalAttackEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertNephalPropertiesTryPacifyPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyNephalPropertiesTarget), nameof(DummyNephalPropertiesTarget.TryPacify)),
                typeof(NephalPropertiesTranslationPatch));

            var target = new DummyNephalPropertiesTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.TryPacify();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertIntegratedWeaponHostsGenerateTurretPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyIntegratedWeaponHostsTarget),
                    nameof(DummyIntegratedWeaponHostsTarget.GenerateTurret),
                    typeof(DummyGameObject),
                    typeof(DummyGameObject),
                    typeof(bool)),
                typeof(IntegratedWeaponHostsTranslationPatch));

            var target = new DummyIntegratedWeaponHostsTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.GenerateTurret(new DummyGameObject(), new DummyGameObject(), overrideSupply: false);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertIntegratedWeaponHostsHandleTurretWishPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyIntegratedWeaponHostsTarget),
                    nameof(DummyIntegratedWeaponHostsTarget.HandleTurretWish),
                    typeof(System.Text.RegularExpressions.Match)),
                typeof(IntegratedWeaponHostsTranslationPatch));

            var target = new DummyIntegratedWeaponHostsTarget
            {
                PopupMessageToSend = message,
            };

            _ = target.HandleTurretWish(System.Text.RegularExpressions.Regex.Match("turret:PhaseCannon", "^turret:\\s*(.*?)\\s*$"));

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertBoostStatisticQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyBoostStatisticTarget), methodName, typeof(DummyGameObject)),
                typeof(BoostStatisticTranslationPatch));

            var target = new DummyBoostStatisticTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyBoostStatisticTarget.Apply), StringComparison.Ordinal))
            {
                _ = target.Apply(new DummyGameObject());
            }
            else
            {
                target.Remove(new DummyGameObject());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertEmboldenedQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyEmboldenedTarget), methodName, typeof(DummyGameObject)),
                typeof(EmboldenedTranslationPatch));

            var target = new DummyEmboldenedTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyEmboldenedTarget.Apply), StringComparison.Ordinal))
            {
                _ = target.Apply(new DummyGameObject());
            }
            else
            {
                target.Remove(new DummyGameObject());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertFungalSporeInfectionPopup(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyFungalSporeInfectionTarget),
                    nameof(DummyFungalSporeInfectionTarget.ApplyFungalInfection),
                    typeof(DummyGameObject),
                    typeof(string),
                    typeof(DummyBodyPart)),
                typeof(FungalSporeInfectionTranslationPatch));

            DummyFungalSporeInfectionTarget.PopupMessageToSend = message;

            _ = DummyFungalSporeInfectionTarget.ApplyFungalInfection(new DummyGameObject(), "WaxInfection", new DummyBodyPart());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyFungalSporeInfectionTarget.PopupMessageToSend = string.Empty;
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertFungalSporeInfectionQueuedMessage(string message, string expected)
    {
        AssertFungalSporeInfectionQueuedMessage(nameof(DummyFungalSporeInfectionTarget.FireEvent), message, expected);
    }

    private static void AssertFungalSporeInfectionQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            var targetMethod = string.Equals(methodName, nameof(DummyFungalSporeInfectionTarget.ApplyGas), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyFungalSporeInfectionTarget), methodName, typeof(DummyGameObject))
                : RequireMethod(typeof(DummyFungalSporeInfectionTarget), methodName, typeof(DummyGameEvent));
            PatchOwner(
                harmony,
                targetMethod,
                typeof(FungalSporeInfectionTranslationPatch));

            var target = new DummyFungalSporeInfectionTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyFungalSporeInfectionTarget.PaxFireEvent), StringComparison.Ordinal))
            {
                _ = target.PaxFireEvent(new DummyGameEvent { ID = "BeforeApplyDamage" });
            }
            else if (string.Equals(methodName, nameof(DummyFungalSporeInfectionTarget.PuffFireEvent), StringComparison.Ordinal))
            {
                _ = target.PuffFireEvent(new DummyGameEvent { ID = "BeforeApplyDamage" });
            }
            else if (string.Equals(methodName, nameof(DummyFungalSporeInfectionTarget.FireEvent), StringComparison.Ordinal))
            {
                _ = target.FireEvent(new DummyGameEvent { ID = "EndTurn" });
            }
            else if (string.Equals(methodName, nameof(DummyFungalSporeInfectionTarget.ApplyGas), StringComparison.Ordinal))
            {
                _ = target.ApplyGas(new DummyGameObject());
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null);
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertHealingQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            var original = string.Equals(methodName, nameof(DummyHealingTarget.HandleEvent), StringComparison.Ordinal)
                ? RequireMethod(typeof(DummyHealingTarget), methodName, typeof(DummyUseEnergyEvent))
                : RequireMethod(typeof(DummyHealingTarget), methodName, typeof(DummyGameEvent));
            PatchOwner(harmony, original, typeof(HealingTranslationPatch));

            var target = new DummyHealingTarget
            {
                MessageToSend = message,
                ColorToSend = "r",
            };

            if (string.Equals(methodName, nameof(DummyHealingTarget.HandleEvent), StringComparison.Ordinal))
            {
                _ = target.HandleEvent(new DummyUseEnergyEvent { Passive = false });
            }
            else
            {
                _ = target.FireEvent(new DummyGameEvent { ID = "TakeDamage" });
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertStressedQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyStressedTarget), methodName, typeof(DummyGameObject)),
                typeof(StressedTranslationPatch));

            var target = new DummyStressedTarget
            {
                MessageToSend = message,
            };

            if (string.Equals(methodName, nameof(DummyStressedTarget.Apply), StringComparison.Ordinal))
            {
                _ = target.Apply(new DummyGameObject());
            }
            else
            {
                target.Remove(new DummyGameObject());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMonochromeOnsetQueuedMessage(string message, string expected)
    {
        AssertMonochromeOnsetQueuedMessage(nameof(DummyGameObjectFireEventTarget.FireEvent), message, expected);
    }

    private static void AssertMonochromeOnsetQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectFireEventTarget), methodName, typeof(DummyGameEvent)),
                typeof(MonochromeOnsetTranslationPatch));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = message,
            };

            _ = RequireMethod(typeof(DummyGameObjectFireEventTarget), methodName, typeof(DummyGameEvent))
                .Invoke(target, new object[] { new DummyGameEvent { ID = "EndTurn" } });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGlotrotOnsetQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectFireEventTarget), nameof(DummyGameObjectFireEventTarget.FireEvent), typeof(DummyGameEvent)),
                typeof(GlotrotOnsetTranslationPatch));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyGameEvent { ID = "EndTurn" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertIronshankQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectFireEventTarget), nameof(DummyGameObjectFireEventTarget.FireEvent), typeof(DummyGameEvent)),
                typeof(IronshankTranslationPatch));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyGameEvent { ID = "EndTurn" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertIronshankOnsetQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectFireEventTarget), nameof(DummyGameObjectFireEventTarget.FireEvent), typeof(DummyGameEvent)),
                typeof(IronshankOnsetTranslationPatch));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyGameEvent { ID = "EndTurn" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertAdrenalControlQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyGameObjectFireEventTarget), nameof(DummyGameObjectFireEventTarget.FireEvent), typeof(DummyGameEvent)),
                typeof(AdrenalControlTranslationPatch));

            var target = new DummyGameObjectFireEventTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyGameEvent { ID = "BeginTakeAction" });

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertAmnesiaQueuedMessage(string methodName, Type eventType, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyAmnesiaTarget), methodName, eventType),
                typeof(AmnesiaTranslationPatch));

            var target = new DummyAmnesiaTarget
            {
                MessageToSend = message,
            };

            if (eventType == typeof(DummySecretVisibilityChangedEvent))
            {
                _ = target.HandleEvent(new DummySecretVisibilityChangedEvent());
            }
            else
            {
                _ = target.HandleEvent(new DummyEnteredCellEvent());
            }

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSimpleFireEventQueuedMessage(Type patchType, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                patchType);

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertCombatSkillQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                typeof(CombatSkillMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int CombatSkillHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(CombatSkillMessageTranslationPatch));
    }

    private static void AssertSimpleApplyFearQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.ApplyFear), typeof(DummyMentalAttackEvent)),
                typeof(FearAuraTranslationPatch));

            DummySimpleOwnerQueueTarget.StaticMessageToSend = message;

            _ = DummySimpleOwnerQueueTarget.ApplyFear(new DummyMentalAttackEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummySimpleOwnerQueueTarget.StaticMessageToSend = string.Empty;
            DummySimpleOwnerQueueTarget.StaticColorToSend = null;
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMeditatingRemoveQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Remove), typeof(DummyGameObject)),
                typeof(MeditatingTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.Remove(new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertEffectStaticApplyQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Apply), typeof(DummyGameObject)),
                typeof(EffectStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.Apply(new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertEffectStaticFireEventQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                typeof(EffectStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertEffectStaticBeginTakeActionQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.HandleEvent), typeof(DummyBeginTakeActionEvent)),
                typeof(EffectStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.HandleEvent(new DummyBeginTakeActionEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertStasisQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummySimpleOwnerQueueTarget),
                    nameof(DummySimpleOwnerQueueTarget.HandleEvent),
                    typeof(DummyBeforeApplyDamageEvent)),
                typeof(StasisTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.HandleEvent(new DummyBeforeApplyDamageEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertEffectGeneratedHandleEventQueuedMessage(string message, string expected)
    {
        UseRepositoryMessageFrames();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummySimpleOwnerQueueTarget),
                    nameof(DummySimpleOwnerQueueTarget.HandleEvent),
                    typeof(DummyEndTurnEvent)),
                typeof(EffectGeneratedMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.HandleEvent(new DummyEndTurnEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertEffectGeneratedApplyQueuedMessage(string message, string expected)
    {
        UseRepositoryMessageFrames();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Apply), typeof(DummyGameObject)),
                typeof(EffectGeneratedMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.Apply(new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertBlazeTonicRemoveQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Remove), typeof(DummyGameObject)),
                typeof(BlazeTonicRemoveTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.Remove(new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertLatchedOntoExpiredQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Expired)),
                typeof(LatchedOntoExpiredTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.Expired();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGiantClamTeleportQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), methodName, typeof(DummyGameObject)),
                typeof(GiantClamTeleportTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = methodName switch
            {
                nameof(DummySimpleOwnerQueueTarget.TeleportToClamWorld) => InvokeTeleportToClamWorld(target),
                nameof(DummySimpleOwnerQueueTarget.TeleportFromClamWorld) => InvokeTeleportFromClamWorld(target),
                nameof(DummySimpleOwnerQueueTarget.TeleportJoppaWorld) => InvokeTeleportJoppaWorld(target),
                _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected teleport method."),
            };

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static bool InvokeTeleportToClamWorld(DummySimpleOwnerQueueTarget target)
    {
        target.TeleportToClamWorld(new DummyGameObject());
        return true;
    }

    private static bool InvokeTeleportFromClamWorld(DummySimpleOwnerQueueTarget target)
    {
        target.TeleportFromClamWorld(new DummyGameObject());
        return true;
    }

    private static bool InvokeTeleportJoppaWorld(DummySimpleOwnerQueueTarget target)
    {
        target.TeleportJoppaWorld(new DummyGameObject());
        return true;
    }

    private static void AssertForceBubbleOwnerQueuedMessage(string methodName, string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), methodName),
                typeof(ForceBubbleOwnerTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = methodName switch
            {
                nameof(DummySimpleOwnerQueueTarget.ActivateForceEmitter) => target.ActivateForceEmitter(),
                nameof(DummySimpleOwnerQueueTarget.ActivateStopsvalinn) => target.ActivateStopsvalinn(),
                nameof(DummySimpleOwnerQueueTarget.DestroyBubble) => InvokeDestroyBubble(target),
                _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected force bubble method."),
            };

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static bool InvokeDestroyBubble(DummySimpleOwnerQueueTarget target)
    {
        target.DestroyBubble();
        return true;
    }

    private static void AssertSystemStaticCheckpointQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.CheckpointOn)),
                typeof(SystemStaticMessageTranslationPatch));

            DummySimpleOwnerQueueTarget.StaticMessageToSend = message;

            _ = DummySimpleOwnerQueueTarget.CheckpointOn();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummySimpleOwnerQueueTarget.StaticMessageToSend = string.Empty;
            DummySimpleOwnerQueueTarget.StaticColorToSend = null;
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertMutationAbsorptionHealingQueuedMessage(
        string message,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                typeof(MutationAbsorptionHealingTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
                ColorToSend = expectedColor,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertOnEatRewardQueuedMessage(
        string message,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                typeof(OnEatRewardMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
                ColorToSend = expectedColor,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticSetHolyZoneQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummySimpleOwnerQueueTarget),
                    nameof(DummySimpleOwnerQueueTarget.SetHolyZone),
                    typeof(DummyZone),
                    typeof(DummyFaction)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.SetHolyZone(new DummyZone(), new DummyFaction());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticFireEventQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.FireEvent), typeof(DummyEvent)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticQuakeQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Quake)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.Quake();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticTickEggQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.tickEgg)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.tickEgg();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticCastQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Cast)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.Cast();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticEnteredCellQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.HandleEvent), typeof(DummyEnteredCellEvent)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.HandleEvent(new DummyEnteredCellEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticSunderQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Sunder)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.Sunder();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticVortexQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.Vortex)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.Vortex();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticTryGrowMushroomQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.TryGrowMushroom)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            target.TryGrowMushroom();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertSystemStaticTorchPropertiesHandleEventQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.TorchPropertiesHandleEvent), typeof(DummyEndTurnEvent)),
                typeof(SystemStaticMessageTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.TorchPropertiesHandleEvent(new DummyEndTurnEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertGeneratedQueueDoesVerbMessage(string methodName, string message, string expected)
    {
        UseRepositoryMessageFrames();
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                GeneratedQueueDoesVerbMethod(methodName),
                typeof(GeneratedQueueDoesVerbTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = methodName switch
            {
                nameof(DummySimpleOwnerQueueTarget.GelatenousPalmFireEvent) => target.GelatenousPalmFireEvent(new DummyEvent()),
                nameof(DummySimpleOwnerQueueTarget.GraveMossTrigger) => InvokeGraveMossTrigger(target),
                nameof(DummySimpleOwnerQueueTarget.QuantumRipplerHandleEvent) => target.QuantumRipplerHandleEvent(new DummyEvent()),
                nameof(DummySimpleOwnerQueueTarget.PerformReclamationOf) => target.PerformReclamationOf(new DummyGameObject()),
                nameof(DummySimpleOwnerQueueTarget.DropOffStolenGoodsMoveToDropoff) => InvokeDropOffStolenGoodsMoveToDropoff(target),
                nameof(DummySimpleOwnerQueueTarget.PaxKlanqMadnessTakeAction) => InvokePaxKlanqMadnessTakeAction(target),
                nameof(DummySimpleOwnerQueueTarget.BodyPartUnequipPartAndChildren) => InvokeBodyPartUnequipPartAndChildren(target),
                nameof(DummySimpleOwnerQueueTarget.ExtradimensionalLootFireEvent) => target.ExtradimensionalLootFireEvent(new DummyEvent()),
                nameof(DummySimpleOwnerQueueTarget.GarbageAttemptRifle) => target.GarbageAttemptRifle(),
                _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
            };

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo GeneratedQueueDoesVerbMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummySimpleOwnerQueueTarget.GelatenousPalmFireEvent) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.GelatenousPalmFireEvent),
                typeof(DummyEvent)),
            nameof(DummySimpleOwnerQueueTarget.GraveMossTrigger) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.GraveMossTrigger)),
            nameof(DummySimpleOwnerQueueTarget.QuantumRipplerHandleEvent) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.QuantumRipplerHandleEvent),
                typeof(DummyEvent)),
            nameof(DummySimpleOwnerQueueTarget.PerformReclamationOf) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.PerformReclamationOf),
                typeof(DummyGameObject)),
            nameof(DummySimpleOwnerQueueTarget.DropOffStolenGoodsMoveToDropoff) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.DropOffStolenGoodsMoveToDropoff)),
            nameof(DummySimpleOwnerQueueTarget.PaxKlanqMadnessTakeAction) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.PaxKlanqMadnessTakeAction)),
            nameof(DummySimpleOwnerQueueTarget.BodyPartUnequipPartAndChildren) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.BodyPartUnequipPartAndChildren)),
            nameof(DummySimpleOwnerQueueTarget.ExtradimensionalLootFireEvent) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.ExtradimensionalLootFireEvent),
                typeof(DummyEvent)),
            nameof(DummySimpleOwnerQueueTarget.GarbageAttemptRifle) => RequireMethod(
                typeof(DummySimpleOwnerQueueTarget),
                nameof(DummySimpleOwnerQueueTarget.GarbageAttemptRifle)),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
        };
    }

    private static bool InvokeGraveMossTrigger(DummySimpleOwnerQueueTarget target)
    {
        target.GraveMossTrigger();
        return true;
    }

    private static bool InvokeDropOffStolenGoodsMoveToDropoff(DummySimpleOwnerQueueTarget target)
    {
        target.DropOffStolenGoodsMoveToDropoff();
        return true;
    }

    private static bool InvokePaxKlanqMadnessTakeAction(DummySimpleOwnerQueueTarget target)
    {
        target.PaxKlanqMadnessTakeAction();
        return true;
    }

    private static bool InvokeBodyPartUnequipPartAndChildren(DummySimpleOwnerQueueTarget target)
    {
        target.BodyPartUnequipPartAndChildren();
        return true;
    }

    private static void AssertAbilityManagerShowQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummySimpleOwnerQueueTarget), nameof(DummySimpleOwnerQueueTarget.AbilityManagerShow)),
                typeof(AbilityManagerShowTranslationPatch));

            var target = new DummySimpleOwnerQueueTarget
            {
                MessageToSend = message,
            };

            _ = target.AbilityManagerShow();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertTonicFireEventQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyTonicTarget), nameof(DummyTonicTarget.FireEvent), typeof(DummyEvent)),
                typeof(TonicTranslationPatch));

            var target = new DummyTonicTarget
            {
                MessageToSend = message,
            };

            _ = target.FireEvent(new DummyEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertXrlGameFinishQuestStepQueuedMessage(string message, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyXrlGameTarget),
                    nameof(DummyXrlGameTarget.FinishQuestStep),
                    typeof(DummyQuest),
                    typeof(string),
                    typeof(int),
                    typeof(bool),
                    typeof(string)),
                typeof(XrlGameTranslationPatch));

            var target = new DummyXrlGameTarget
            {
                MessageToSend = message,
            };

            _ = target.FinishQuestStep(new DummyQuest(), "Step1", -1, true, null);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertZoneManagerTryThawZoneMessage(string message, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerTryThawZoneTarget), nameof(DummyZoneManagerTryThawZoneTarget.TryThawZone), typeof(string), typeof(DummyZone).MakeByRefType()),
                typeof(ZoneManagerTryThawZoneTranslationPatch));

            var target = new DummyZoneManagerTryThawZoneTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            target.TryThawZone("JoppaWorld.1.1.1.1.10", out _);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertZoneManagerTickMessage(string message, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerTickTarget), nameof(DummyZoneManagerTickTarget.Tick), typeof(bool)),
                typeof(ZoneManagerTickTranslationPatch));

            var target = new DummyZoneManagerTickTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            target.Tick(allowFreeze: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertZoneManagerGenerateZoneMessage(string message, string? color, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneManagerGenerateZoneTarget), nameof(DummyZoneManagerGenerateZoneTarget.GenerateZone), typeof(string)),
                typeof(ZoneManagerGenerateZoneTranslationPatch));

            var target = new DummyZoneManagerGenerateZoneTarget
            {
                MessageToSend = message,
                ColorToSend = color,
            };

            target.GenerateZone("JoppaWorld.1.1.1.1.10");

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo GameObjectMoveMethod()
    {
        return RequireMethod(
            typeof(DummyGameObjectMoveTarget),
            nameof(DummyGameObjectMoveTarget.Move),
            typeof(string),
            typeof(DummyGameObject).MakeByRefType(),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(bool),
            typeof(int?),
            typeof(string),
            typeof(int?),
            typeof(bool),
            typeof(bool),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(int));
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowYesNoAsync(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoAsync)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowYesNoCancel(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancel)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowBlock(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowAsync(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowAsync)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void AssertPhysicsObjectEnteringCellQueuedMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyPhysicsObjectEnteringCellTarget),
                    nameof(DummyPhysicsObjectEnteringCellTarget.HandleEvent),
                    typeof(DummyObjectEnteringCellEvent)),
                typeof(PhysicsObjectEnteringCellTranslationPatch));

            var target = new DummyPhysicsObjectEnteringCellTarget
            {
                MessageToSend = source,
            };

            target.HandleEvent(new DummyObjectEnteringCellEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPhysicsProcessTakeDamageQueuedMessage(
        string source,
        string expected,
        DummyGameEvent? eventObject = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyPhysicsProcessTakeDamageTarget),
                    nameof(DummyPhysicsProcessTakeDamageTarget.ProcessTakeDamage),
                    typeof(DummyGameEvent)),
                typeof(PhysicsProcessTakeDamageTranslationPatch));

            var target = new DummyPhysicsProcessTakeDamageTarget
            {
                MessageToSend = source,
            };

            target.ProcessTakeDamage(eventObject ?? new DummyGameEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPhysicsProcessTakeDamageQueuedMessages(
        IReadOnlyList<(string Source, string Expected)> cases)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyPhysicsProcessTakeDamageTarget),
                    nameof(DummyPhysicsProcessTakeDamageTarget.ProcessTakeDamage),
                    typeof(DummyGameEvent)),
                typeof(PhysicsProcessTakeDamageTranslationPatch));

            Assert.Multiple(() =>
            {
                foreach (var testCase in cases)
                {
                    DummyMessageQueue.Reset();
                    var target = new DummyPhysicsProcessTakeDamageTarget
                    {
                        MessageToSend = testCase.Source,
                    };

                    target.ProcessTakeDamage(new DummyGameEvent());

                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(testCase.Expected), testCase.Source);
                }
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original, Type patchType)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, "Prefix")),
            finalizer: new HarmonyMethod(RequireMethod(patchType, "Finalizer")));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
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

    private void WritePatternDictionary(params (string pattern, string template)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(entries[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(entries[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        MessagePatternTranslator.InvalidatePatternFileCacheForTests(patternFilePath);
    }

    private void WriteLeafDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-messagelog-leaf.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        MessagePatternTranslator.SetLeafFileForTests(Path.Combine(tempDirectory, "ui-messagelog-leaf.ja.json"));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void UseRepositoryPatternDictionary()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        MessagePatternTranslator.SetPatternFileForTests(null);
    }

    private static void UseRepositoryMessageFrames()
    {
        MessageFrameTranslator.SetDictionaryPathForTests(
            Path.Combine(
                TestProjectPaths.GetRepositoryRoot(),
                "Mods",
                "QudJP",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));
    }
}
