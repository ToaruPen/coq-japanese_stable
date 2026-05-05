using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldPartsProducerTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-worldparts-producer-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyPopupTarget.Reset();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesPopupShowMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyLiquidVolumeProducerTarget),
                    nameof(DummyLiquidVolumeProducerTarget.PerformFill),
                    typeof(DummyGameObject),
                    typeof(bool).MakeByRefType(),
                    typeof(bool)),
                typeof(LiquidVolumeTranslationPatch));

            var requestExit = false;
            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMessageToShow = "Do you want to empty canteen first?",
            };

            target.PerformFill(new DummyGameObject(), ref requestExit);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("canteenを先に空にしますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesPopupBlockMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMessageToShow = "You are now {{B|hydrated}}.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは今、{{B|hydrated}}。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DesalinationPelletPatch_TranslatesCompositePopupPrefix_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyDesalinationPelletProducerTarget), nameof(DummyDesalinationPelletProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(DesalinationPelletTranslationPatch));

            var target = new DummyDesalinationPelletProducerTarget
            {
                PopupMessageToShow = "You drop desalination pellet into canteen.\n\nThe water is purified.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("desalination pelletをcanteenに入れた。\n\nThe water is purified."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ClonelingVehiclePatch_TranslatesPopupFailure_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyVehicleRepairProducerTarget), nameof(DummyVehicleRepairProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(ClonelingVehicleTranslationPatch));

            var target = new DummyVehicleRepairProducerTarget
            {
                PopupMessageToShow = "You do not have 1 dram of sunslag.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("sunslagを1ドラム持っていない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ClonelingVehiclePatch_TranslatesQueuedMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyClonelingProducerTarget), nameof(DummyClonelingProducerTarget.AttemptCloning)),
                typeof(ClonelingVehicleTranslationPatch));

            var target = new DummyClonelingProducerTarget
            {
                QueuedMessageToSend = "Your onboard systems are out of cloning draught.",
            };

            _ = target.AttemptCloning();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("搭載システムのcloning draughtが切れている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void XrlCoreLostSightPatch_RecordsOwnerRouteTransforms_WithoutMessageLogSinkObservation_WhenPatched()
    {
        WritePatternDictionary(("^You have lost sight of (.+?)[.!]?$", "{0}を見失った。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageLog(harmony);
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyXrlCoreRenderTarget), nameof(DummyXrlCoreRenderTarget.RenderBaseToBuffer), typeof(DummyScreenBuffer)),
                typeof(XrlCoreLostSightTranslationPatch));

            const string source = "You have lost sight of bloody Naruur.";
            var target = new DummyXrlCoreRenderTarget
            {
                MessageToSend = source,
            };

            target.RenderBaseToBuffer(new DummyScreenBuffer());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("bloody Naruurを見失った。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(XrlCoreLostSightTranslationPatch),
                        "LostSight"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(MessageLogPatch),
                        nameof(XrlCoreLostSightTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EnclosingPatch_TranslatesExtricatePopup_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyEnclosingProducerTarget),
                    nameof(DummyEnclosingProducerTarget.ExitEnclosure),
                    typeof(DummyGameObject),
                    typeof(DummyGameEvent),
                    typeof(DummyEnclosedEffect)),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                PopupMessageToShow = "You extricate yourself from stasis pod.",
            };

            _ = target.ExitEnclosure(new DummyGameObject(), new DummyGameEvent(), new DummyEnclosedEffect());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("stasis podから抜け出した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GivesRepPatch_TranslatesWaterBondedPostfix_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGivesRepProducerTarget), nameof(DummyGivesRepProducerTarget.HandleEvent), typeof(DummyGetShortDescriptionEvent)),
                prefix: new HarmonyMethod(RequireMethod(typeof(GivesRepShortDescriptionTranslationPatch), nameof(GivesRepShortDescriptionTranslationPatch.Prefix), typeof(object), typeof(int).MakeByRefType())),
                postfix: new HarmonyMethod(RequireMethod(typeof(GivesRepShortDescriptionTranslationPatch), nameof(GivesRepShortDescriptionTranslationPatch.Postfix), typeof(object), typeof(int))));

            var evt = new DummyGetShortDescriptionEvent();
            evt.Postfix.Append("既存の説明");
            var target = new DummyGivesRepProducerTarget
            {
                PostfixTextToAppend = "\nYou are water-bonded with Mehmet.",
            };

            _ = target.HandleEvent(evt);

            Assert.That(evt.Postfix.ToString(), Is.EqualTo("既存の説明\nMehmetと水の絆で結ばれている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("temporal clone implodes.", "temporal cloneは内破した。")]
    [TestCase("temporal clone is smeared into stone by the rasp of time.", "temporal cloneは時の軋みによって石へ塗り込められた。")]
    [TestCase("temporal clone crumbles into beetles.", "temporal cloneは崩れて甲虫になった。")]
    [TestCase("temporal clone is vacuumed to another place and time. The void that remains is filled with three important objects from one of your side lives.", "temporal cloneは別の場所と時間へ吸い込まれた。残された虚空は、あなたの横道の人生のひとつから来た3つの重要な物体で満たされた。")]
    [TestCase("temporal clone atomizes and recombines into a chrome pyramid.", "temporal cloneは原子化し、再結合してchrome pyramidになった。")]
    [TestCase("temporal clone atomizes and recombines into The chrome pyramid.", "temporal cloneは原子化し、再結合してchrome pyramidになった。")]
    [TestCase("temporal clone atomizes and recombines into An ice frog.", "temporal cloneは原子化し、再結合してice frogになった。")]
    [TestCase("temporal clone's consciousness dissipates.", "temporal cloneの意識は霧散した。")]
    [TestCase("temporal clone's consciousness dissipates into brass chair and granite statue.", "temporal cloneの意識はbrass chair and granite statueへ霧散した。")]
    [TestCase("temporal clone liquifies into several pools of slime&y.", "temporal cloneは液化してslimeの水たまりいくつかになった。")]
    [TestCase("temporal clone is folded a trillion times by the pressure of the nether, causing the local region of spacetime to lose contiguity.", "temporal cloneは冥界の圧力によって1兆回折り畳まれ、局所時空領域の連続性を失わせた。")]
    [TestCase("temporal clone is vectorized into a line of force.", "temporal cloneは力線へベクトル化された。")]
    [TestCase("temporal clone is vectorized into a line of normality.", "temporal cloneは正常性の線へベクトル化された。")]
    [TestCase("temporal clone is vectorized into a line of plants.", "temporal cloneは植物の列へベクトル化された。")]
    public void PetEitherOrExplodePatch_TranslatesQueuedExplodeMessages_WhenPatched(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.explode)),
                typeof(PetEitherOrExplodeTranslationPatch));

            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.explode();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PetEitherOrExplodePatch_DoesNotTranslateFlickerMessageOutsideExplodeFamily_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.explode)),
                typeof(PetEitherOrExplodeTranslationPatch));

            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = "temporal clone starts to flicker.",
            };

            target.explode();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("temporal clone starts to flicker."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PetEitherOrExplodePatch_DoesNotTranslateQueuedExplodeMessage_WhenOwnerPatchIsAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            const string source = "temporal clone implodes.";
            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PetEitherOrExplodeTranslationPatch),
                        "PetEitherOr.Explode"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PetEitherOrExplodePatch_PreservesColoredDynamicCaptures_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.explode)),
                typeof(PetEitherOrExplodeTranslationPatch));

            const string source = "{{R|temporal clone}} liquifies into several pools of {{G|slime&y}}.";
            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.explode();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{R|temporal clone}}は液化して{{G|slime&y}}の水たまりいくつかになった。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PetEitherOrExplodeTranslationPatch),
                        "PetEitherOr.Explode"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PetEitherOrExplodePatch_PreservesWholeMessageColorBoundary_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.explode)),
                typeof(PetEitherOrExplodeTranslationPatch));

            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = "{{R|temporal clone implodes.}}",
            };

            target.explode();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{R|temporal cloneは内破した。}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PetEitherOrExplodePatch_DoesNotTranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.explode)),
                typeof(PetEitherOrExplodeTranslationPatch));

            var source = MessageFrameTranslator.MarkDirectTranslation("temporal clone implodes.");
            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.explode();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PetEitherOrExplodeTranslationPatch),
                        "PetEitherOr.Explode"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PetEitherOrExplodePatch_DoesNotTranslateEmptyQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.explode)),
                typeof(PetEitherOrExplodeTranslationPatch));

            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = string.Empty,
            };

            target.explode();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PetEitherOrExplodeTranslationPatch),
                        "PetEitherOr.Explode"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("The wind changes direction.", "風向きが変わった。")]
    [TestCase("The wind becomes still.", "風が静まった。")]
    [TestCase("The wind changes direction from the north to the southeast.", "風向きが北から南東へ変わった。")]
    [TestCase("The wind begins blowing at a gentle breeze from the northeast.", "北東から弱い風が吹き始めた。")]
    [TestCase("The wind intensifies to a strong breeze, blowing from the west.", "西から吹く風が強い風まで強まった。")]
    [TestCase("The wind calms to a very gentle breeze, blowing from the south.", "南から吹く風がごく弱い風まで弱まった。")]
    [TestCase("The wind begins blowing at gale intensity.", "疾強風が吹き始めた。")]
    [TestCase("The wind intensifies to storm intensity.", "風が暴風まで強まった。")]
    [TestCase("The wind calms to a moderate breeze.", "風がほどよい風まで弱まった。")]
    public void ZoneWindChangePatch_TranslatesQueuedWindMessages_WhenOwnerPatched(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneWindChangeProducerTarget), nameof(DummyZoneWindChangeProducerTarget.WindChange), typeof(long)),
                typeof(ZoneWindChangeTranslationPatch));

            var target = new DummyZoneWindChangeProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.WindChange(1234);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneWindChangePatch_DoesNotTranslateQueuedWindMessage_WhenOwnerPatchIsAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            const string source = "The wind becomes still.";
            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ZoneWindChangeTranslationPatch),
                        "Zone.WindChange"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneWindChangePatch_PreservesColorTags_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneWindChangeProducerTarget), nameof(DummyZoneWindChangeProducerTarget.WindChange), typeof(long)),
                typeof(ZoneWindChangeTranslationPatch));

            var target = new DummyZoneWindChangeProducerTarget
            {
                QueuedMessageToSend = "{{C|The wind begins blowing at {{W|a gentle breeze}} from the north.}}",
            };

            target.WindChange(1234);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{C|北から{{W|弱い風}}が吹き始めた。}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("The wind begins blowing at an impossible zephyr from the north.")]
    [TestCase("The wind begins blowing at a gentle breeze from the upspin.")]
    public void ZoneWindChangePatch_DoesNotTranslateUnknownWindComponents_WhenOwnerPatched(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneWindChangeProducerTarget), nameof(DummyZoneWindChangeProducerTarget.WindChange), typeof(long)),
                typeof(ZoneWindChangeTranslationPatch));

            var target = new DummyZoneWindChangeProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.WindChange(1234);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneWindChangePatch_DoesNotTranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneWindChangeProducerTarget), nameof(DummyZoneWindChangeProducerTarget.WindChange), typeof(long)),
                typeof(ZoneWindChangeTranslationPatch));

            var source = MessageFrameTranslator.MarkDirectTranslation("The wind becomes still.");
            var target = new DummyZoneWindChangeProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.WindChange(1234);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ZoneWindChangeTranslationPatch),
                        "Zone.WindChange"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ZoneWindChangePatch_DoesNotTranslateEmptyQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyZoneWindChangeProducerTarget), nameof(DummyZoneWindChangeProducerTarget.WindChange), typeof(long)),
                typeof(ZoneWindChangeTranslationPatch));

            var target = new DummyZoneWindChangeProducerTarget
            {
                QueuedMessageToSend = string.Empty,
            };

            target.WindChange(1234);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ZoneWindChangeTranslationPatch),
                        "Zone.WindChange"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchMessageLog(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original, Type patchType)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, nameof(LiquidVolumeTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(patchType, nameof(LiquidVolumeTranslationPatch.Finalizer), typeof(Exception))));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
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
}
