using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

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
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

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
    public void PhysicsObjectEnteringCell_TranslatesBlockedMessage_WhenPatched()
    {
        WritePatternDictionary(
            ("^The way is blocked by (?:the |a |an |some )?(.+?)[.!]?$", "{0}に道を塞がれている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPhysicsObjectEnteringCellTarget), nameof(DummyPhysicsObjectEnteringCellTarget.HandleEvent), typeof(DummyObjectEnteringCellEvent)),
                typeof(PhysicsObjectEnteringCellTranslationPatch));

            var target = new DummyPhysicsObjectEnteringCellTarget
            {
                MessageToSend = "The way is blocked by an chrome pyramid.",
            };

            target.HandleEvent(new DummyObjectEnteringCellEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("chrome pyramidに道を塞がれている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectHeal_TranslatesHealMessage_WhenPatched()
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
    public void ZoneManagerSetActiveZoneMapNotes_TranslatesMapNotes_WhenPatched()
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
                typeof(CombatGetDefenderHitDiceTranslationPatch));

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
                typeof(CombatGetDefenderHitDiceTranslationPatch));

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
                typeof(CombatGetDefenderHitDiceTranslationPatch));

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
                typeof(CombatGetDefenderHitDiceTranslationPatch));

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
                typeof(CombatMeleeAttackTranslationPatch));

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
                typeof(CombatMeleeAttackTranslationPatch));

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

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original, Type patchType)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, "Prefix")),
            finalizer: new HarmonyMethod(RequireMethod(patchType, "Finalizer", typeof(Exception))));
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
}
