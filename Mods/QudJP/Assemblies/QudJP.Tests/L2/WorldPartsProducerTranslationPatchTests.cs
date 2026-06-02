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
        WriteLiquidDictionaries();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyPopupTarget.Reset();
        DummyPopupGenericTarget.Reset();
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
    public void LiquidVolumePatch_TranslatesPopupYesNoCancelMessage_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(
                harmony,
                nameof(DummyPopupShow.ShowYesNoCancel),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int));
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                PopupMessageToShow = "The {{Y|canteen}} is not owned by you. Are you sure you want to pour from {{Y|it}}?",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo("{{Y|canteen}}はあなたの所有物ではない。本当にそこから注ぎますか？"));
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
                PopupMethod = nameof(DummyPopupTarget.ShowBlock),
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
    public void LiquidVolumePatch_TranslatesQueuedMessages_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyLiquidVolumeProducerTarget),
                    nameof(DummyLiquidVolumeProducerTarget.Pour),
                    typeof(bool).MakeByRefType(),
                    typeof(DummyGameObject),
                    typeof(DummyCell),
                    typeof(bool),
                    typeof(bool),
                    typeof(int),
                    typeof(bool)),
                typeof(LiquidVolumeTranslationPatch));

            var requestExit = false;
            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "2 drams of {{C|water}} pours out all over snapjaw!",
            };

            target.Pour(ref requestExit, new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{C|水}} 2ドラムがsnapjawの全身にかかった！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesCleanAllItemsMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "You clean the slime and rust from your {{Y|boots}} and {{Y|bronze dagger}} with a dram of {{C|water}}.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("{{Y|boots}}と{{Y|bronze dagger}}から粘液と錆を{{C|水}}1ドラムで洗い落とした。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesCleanAllItemsMessageLog_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageLog(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "You clean the stains from {{C|high-tech toolkit}}、your {{Y|steel}} buckler、とyour pair of {{Y|steel}} boots with a dram of {{B|fresh water}} from カムシュルウールの 水筒.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo("{{C|high-tech toolkit}}、{{Y|steel}} buckler、とpair of {{Y|steel}} bootsから染みを{{B|真水}}1ドラムで洗い落とした（カムシュルウールの 水筒から）。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesCollectMessageFromContainerHere_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageLog(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "You collect 60 dram of fresh water from the 水袋 here in your 水筒と水袋.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("水袋（ここ）から真水を60ドラム集めた（水筒と水袋に入れた）。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesHandleEventOwnerMessages_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(
                harmony,
                nameof(DummyPopupShow.ShowYesNoCancel),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int));
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
            };

            target.PopupMessageToShow = "The {{Y|canteen}} is not owned by you. Are you sure you want to drink from {{Y|it}}?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var ownershipDrink = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "The {{Y|canteen}} is not owned by you. Are you sure you want to drain {{Y|it}}?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var ownershipDrain = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "Are you sure you want to drain {{Y|canteen}}?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var drainConfirm = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "The {{Y|canteen}} is not owned by you. Are you sure you want to fill {{Y|it}}?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var ownershipFill = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "Do you want to empty {{Y|canteen}} first?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var emptyFirst = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "The {{Y|canteen}} is not owned by you. Are you sure you want to collect from {{Y|it}}?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var ownershipCollect = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "You are able to collect 129 drams of {{B|fresh water}}. Are you sure you want to?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var collectConfirm = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = "The {{Y|canteen}} is not owned by you. Are you sure you want to use {{B|fresh water}} from {{Y|it}}?";
            target.HandleEvent(new DummyInventoryActionEvent());
            var ownershipUseLiquid = DummyPopupShow.LastShowYesNoCancelMessage;

            target.PopupMessageToShow = string.Empty;
            target.QueuedMessageToSend = "It's fizzy.";
            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(ownershipDrink, Is.EqualTo("{{Y|canteen}}はあなたの所有物ではない。本当にそこから飲みますか？"));
                Assert.That(ownershipDrain, Is.EqualTo("{{Y|canteen}}はあなたの所有物ではない。本当に排出しますか？"));
                Assert.That(drainConfirm, Is.EqualTo("{{Y|canteen}}を本当に排出しますか？"));
                Assert.That(ownershipFill, Is.EqualTo("{{Y|canteen}}はあなたの所有物ではない。本当に満たしますか？"));
                Assert.That(emptyFirst, Is.EqualTo("{{Y|canteen}}を先に空にしますか？"));
                Assert.That(ownershipCollect, Is.EqualTo("{{Y|canteen}}はあなたの所有物ではない。本当にそこから集めますか？"));
                Assert.That(collectConfirm, Is.EqualTo("{{B|真水}}を129ドラム集められる。本当にそうしますか？"));
                Assert.That(ownershipUseLiquid, Is.EqualTo("{{Y|canteen}}はあなたの所有物ではない。{{B|真水}}を本当にそこから使いますか？"));
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("シュワシュワしている。"));
                Assert.That(LiquidVolumePopupHitCount("OwnershipDrink"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("OwnershipDrain"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("DrainConfirm"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("OwnershipFill"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("EmptyFirst"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("OwnershipCollect"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("CollectConfirm"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupHitCount("OwnershipUseLiquid"), Is.EqualTo(1));
                Assert.That(LiquidVolumeQueuedHitCount("Fizzy"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesRemainingHandleEventSurfaces_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchAskNumber(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "You collect 2 drams of {{C|water}} from the canteen to the north in your waterskin.",
                AskNumberMessageToShow = "How many drams? (max=7)",
                PickItemTitleToShow = "[Select a container to fill from]",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("canteen（北側）から{{C|水}}を2ドラム集めた（waterskinに入れた）。"));
                Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("何ドラム？(最大=7)"));
                Assert.That(target.LastPickItemTitle, Is.EqualTo("[注ぎ元の容器を選択]"));
                Assert.That(LiquidVolumeQueuedHitCount("CollectMessage"), Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupAskNumberTranslationPatch),
                        "Popup.ProducerText.LiquidVolumeTranslationPatch.HowManyDrams"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        "PickItem.ShowPicker",
                        "LiquidVolumeTranslationPatch.PickItemTitle.SelectContainerToFillFrom"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_TranslatesPourDestinationPickOption_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPickOption(harmony);
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyLiquidVolumeProducerTarget),
                    nameof(DummyLiquidVolumeProducerTarget.Pour),
                    typeof(bool).MakeByRefType(),
                    typeof(DummyGameObject),
                    typeof(DummyCell),
                    typeof(bool),
                    typeof(bool),
                    typeof(int),
                    typeof(bool)),
                typeof(LiquidVolumeTranslationPatch));

            var requestExit = false;
            var target = new DummyLiquidVolumeProducerTarget
            {
                PickOptionIntroToShow = "Where do you want to pour your 水筒?",
                PickOptionOptionsToShow =
                [
                    "Pour it into another container.",
                    "Pour it nearby.",
                    "Pour it on yourself.",
                ],
            };

            target.Pour(ref requestExit, new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("水筒をどこに注ぎますか？"));
                Assert.That(
                    DummyPopupGenericTarget.LastPickOptionOptions,
                    Is.EqualTo(new[]
                    {
                        "別の容器に注ぐ。",
                        "近くに注ぐ。",
                        "自分に注ぐ。",
                    }));
                Assert.That(LiquidVolumePopupPickOptionHitCount("WherePour"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupPickOptionHitCount("PourIntoAnotherContainerOption", "Popup.ProducerMenuItem"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupPickOptionHitCount("PourNearbyOption", "Popup.ProducerMenuItem"), Is.EqualTo(1));
                Assert.That(LiquidVolumePopupPickOptionHitCount("PourOnSelfOption", "Popup.ProducerMenuItem"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_DoesNotTranslatePopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(
                harmony,
                nameof(DummyPopupShow.ShowYesNoCancel),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int));

            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                PopupMessageToShow = "The canteen is not owned by you. Are you sure you want to pour from it?",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(target.PopupMessageToShow));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "It's fizzy.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("It's fizzy."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_StripsDirectMarkerOnMessageLog_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageLog(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation("It's fizzy."),
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("It's fizzy."));
                Assert.That(LiquidVolumeMessageLogHitCount("Fizzy"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_DoesNotTranslateMessageLogTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageLog(harmony);

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = "It's fizzy.",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("It's fizzy."));
                Assert.That(LiquidVolumeMessageLogHitCount("Fizzy"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
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
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("翻訳済みの液体メッセージ"),
            };

            target.PerformFill(new DummyGameObject(), ref requestExit);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("翻訳済みの液体メッセージ"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation("翻訳済みの液体キューメッセージ"),
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("翻訳済みの液体キューメッセージ"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void LiquidVolumePatch_LeavesEmptyMessagesUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyLiquidVolumeProducerTarget), nameof(DummyLiquidVolumeProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(LiquidVolumeTranslationPatch));

            var target = new DummyLiquidVolumeProducerTarget
            {
                PopupMessageToShow = string.Empty,
                QueuedMessageToSend = string.Empty,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.Null);
                Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
            });
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
    public void DesalinationPelletPatch_TranslatesFixedFailurePopup_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony, nameof(DummyPopupShow.ShowFail));
            PatchOwner(
                harmony,
                RequireMethod(
                    typeof(DummyDesalinationPelletProducerTarget),
                    nameof(DummyDesalinationPelletProducerTarget.HandleFailureEvent),
                    typeof(DummyInventoryActionEvent)),
                typeof(DesalinationPelletTranslationPatch));

            var target = new DummyDesalinationPelletProducerTarget
            {
                PopupMessageToShow = "It doesn't seem to do anything.",
            };

            target.HandleFailureEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("何も起こらないようだ。"));
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
    public void XrlCoreHotloadConfigurationPatch_TranslatesQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyXrlCoreRenderTarget), nameof(DummyXrlCoreRenderTarget.HotloadConfiguration), typeof(bool)),
                typeof(XrlCoreHotloadConfigurationTranslationPatch));

            var target = new DummyXrlCoreRenderTarget
            {
                MessageToSend = "Configuration hotloaded...",
            };

            target.HotloadConfiguration();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("設定をホットロードした..."));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(XrlCoreHotloadConfigurationTranslationPatch),
                        "HotloadConfiguration"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void XrlCoreHotloadConfigurationPatch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("Configuration hotloaded...", null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Configuration hotloaded..."));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(XrlCoreHotloadConfigurationTranslationPatch),
                        "HotloadConfiguration"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void XrlCoreHotloadConfigurationPatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyXrlCoreRenderTarget), nameof(DummyXrlCoreRenderTarget.HotloadConfiguration), typeof(bool)),
                typeof(XrlCoreHotloadConfigurationTranslationPatch));

            var target = new DummyXrlCoreRenderTarget
            {
                MessageToSend = MessageFrameTranslator.MarkDirectTranslation("Configuration hotloaded..."),
            };

            target.HotloadConfiguration();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Configuration hotloaded..."));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(XrlCoreHotloadConfigurationTranslationPatch),
                        "HotloadConfiguration"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void XrlCoreHotloadConfigurationPatch_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyXrlCoreRenderTarget), nameof(DummyXrlCoreRenderTarget.HotloadConfiguration), typeof(bool)),
                typeof(XrlCoreHotloadConfigurationTranslationPatch));

            var target = new DummyXrlCoreRenderTarget();

            target.HotloadConfiguration();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(XrlCoreHotloadConfigurationTranslationPatch),
                        "HotloadConfiguration"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.EnterEnclosure),
        "You fail to get yourself into stasis pod.",
        "stasis podに入れなかった。")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "You extricate yourself from stasis pod.",
        "stasis podから抜け出した。")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "It is not stasis pod that you are enclosed by.",
        "閉じ込めているのはstasis podではない。")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "You fail to extricate yourself from stasis pod!",
        "stasis podから抜け出せなかった！")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.EnclosureExitImpeded),
        "You cannot do that while enclosed by stasis pod.",
        "stasis podに閉じ込められている間はそれをできない。")]
    public void EnclosingPatch_TranslatesOwnerPopup_WhenPatched(string methodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                EnclosingMethod(methodName),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                PopupMessageToShow = source,
            };

            InvokeEnclosingMethod(target, methodName);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        typeof(DummyStairsDownProducerTarget),
        nameof(DummyStairsDownProducerTarget.HandleEvent),
        typeof(StairsDownTranslationPatch),
        "Use {{W|Shift+D}} to descend.",
        "{{W|Shift+D}}で下に降りてください。")]
    [TestCase(
        typeof(DummyStairsUpProducerTarget),
        nameof(DummyStairsUpProducerTarget.HandleEvent),
        typeof(StairsUpTranslationPatch),
        "Use {{W|Shift+U}} to ascend.",
        "{{W|Shift+U}}で上に昇ってください。")]
    public void StairsPatch_TranslatesInventoryActionPopup_WhenPatched(
        Type targetType,
        string methodName,
        Type patchType,
        string source,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(targetType, methodName, typeof(DummyInventoryActionEvent)),
                patchType);

            InvokeStairsHandleEvent(targetType, source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.EnterEnclosure),
        "snapjaw tries to get itself into the stasis pod, but fails.",
        "snapjawはそれ自身をthe stasis podの中に入れようとしたが、失敗した。")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "snapjaw tries to extricate itself from stasis pod, but fails!",
        "snapjawはそれ自身をstasis podから引き出そうとしたが、失敗した！")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "snapjaw extricates itself from stasis pod.",
        "snapjawはそれ自身をstasis podから引き出した。")]
    public void EnclosingPatch_TranslatesQueuedMessage_WhenPatched(string methodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                EnclosingMethod(methodName),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                QueuedMessageToSend = source,
            };

            InvokeEnclosingMethod(target, methodName);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EnclosingPatch_DoesNotTranslateOwnerPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            var target = new DummyEnclosingProducerTarget
            {
                PopupMessageToShow = "You fail to get yourself into stasis pod.",
            };

            _ = target.EnterEnclosure(new DummyGameObject(), new DummyGameEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You fail to get yourself into stasis pod."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EnclosingPatch_DoesNotTranslateQueuedMessage_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            const string source = "snapjaw tries to get itself into the stasis pod, but fails.";
            var target = new DummyEnclosingProducerTarget
            {
                QueuedMessageToSend = source,
            };

            _ = target.EnterEnclosure(new DummyGameObject(), new DummyGameEvent());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.EnterEnclosure),
        "snapjaw tries to get itself into the stasis pod, but fails.")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "snapjaw tries to extricate itself from stasis pod, but fails!")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "snapjaw extricates itself from stasis pod.")]
    public void EnclosingPatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched(string methodName, string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                EnclosingMethod(methodName),
                typeof(EnclosingTranslationPatch));

            var markedSource = MessageFrameTranslator.MarkDirectTranslation(source);
            var target = new DummyEnclosingProducerTarget
            {
                QueuedMessageToSend = markedSource,
            };

            InvokeEnclosingMethod(target, methodName);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    [TestCase(nameof(DummyEnclosingProducerTarget.EnterEnclosure))]
    [TestCase(nameof(DummyEnclosingProducerTarget.ExitEnclosure))]
    public void EnclosingPatch_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched(string methodName)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                EnclosingMethod(methodName),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                QueuedMessageToSend = string.Empty,
            };

            InvokeEnclosingMethod(target, methodName);

            Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        nameof(DummyEnclosingProducerTarget.EnterEnclosure),
        "You fail to get yourself into stasis pod.")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "You fail to extricate yourself from stasis pod!")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.ExitEnclosure),
        "You extricate yourself from stasis pod.")]
    [TestCase(
        nameof(DummyEnclosingProducerTarget.EnclosureExitImpeded),
        "You cannot do that while enclosed by stasis pod.")]
    public void EnclosingPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched(string methodName, string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                EnclosingMethod(methodName),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            };

            InvokeEnclosingMethod(target, methodName);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(nameof(DummyEnclosingProducerTarget.EnterEnclosure))]
    [TestCase(nameof(DummyEnclosingProducerTarget.ExitEnclosure))]
    [TestCase(nameof(DummyEnclosingProducerTarget.EnclosureExitImpeded))]
    public void EnclosingPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched(string methodName)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                EnclosingMethod(methodName),
                typeof(EnclosingTranslationPatch));

            var target = new DummyEnclosingProducerTarget
            {
                PopupMessageToShow = string.Empty,
            };

            InvokeEnclosingMethod(target, methodName);

            Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        "This artifact is too complex for you to decipher its function.",
        "このアーティファクトは複雑すぎてあなたにはその機能を解読できない。")]
    [TestCase(
        "These artifacts are too complex for you to decipher their method of construction.",
        "これらのアーティファクトは複雑すぎてあなたにはその製法を解読できない。")]
    [TestCase(
        "You flush with understanding of the artifact's past and determine it to be {{Y|weird artifact}}.",
        "あなたはそのアーティファクトの過去を理解し、それが{{Y|weird artifact}}だと判明した。")]
    [TestCase(
        "You must disassemble {{G|phase cannon}} in order to unlock its secrets.",
        "秘密を解き明かすには{{G|phase cannon}}を分解しなければならない。")]
    [TestCase(
        "You must learn the way of the Reverse Engineer and disassemble {{G|phase cannon}} in order to unlock its secrets.",
        "秘密を解き明かすにはリバースエンジニアの道を学び、{{G|phase cannon}}を分解しなければならない。")]
    [TestCase(
        "{{R|You must disassemble {{G|phase cannon}} in order to unlock its secrets.}}",
        "{{R|秘密を解き明かすには{{G|phase cannon}}を分解しなければならない。}}")]
    [TestCase(
        "You abide the memory of the {{Y|bronze dagger}}'s creation. You learn to build bronze daggers.",
        "{{Y|bronze dagger}}の創造の記憶に身を委ねた。bronze daggersを作れるようになった。")]
    public void PsychometryPatch_TranslatesOwnerPopups_WhenPatched(string source, string expected)
    {
        AssertPsychometryPopup(source, expected);
    }

    [Test]
    public void PsychometryPatch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony, nameof(DummyPopupShow.ShowFail));

            const string source = "You must disassemble phase cannon in order to unlock its secrets.";
            DummyPopupShow.ShowFail(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PsychometryPatch_DoesNotClaimFixedContinuePrompt_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony, nameof(DummyPopupShow.ShowYesNo));
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPsychometryProducerTarget), nameof(DummyPsychometryProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(PsychometryTranslationPatch));

            var target = new DummyPsychometryProducerTarget
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNo),
                PopupMessageToShow = "Do you want to continue despite being unable to use Psychometry?",
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("Do you want to continue despite being unable to use Psychometry?"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        "Popup.ShowYesNo",
                        "Popup.Show.PsychometryTranslationPatch"),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PsychometryPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPsychometryPopup(
            MessageFrameTranslator.MarkDirectTranslation("秘密を解き明かすにはphase cannonを分解しなければならない。"),
            "秘密を解き明かすにはphase cannonを分解しなければならない。");
    }

    [Test]
    public void PsychometryPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPsychometryPopup(string.Empty, string.Empty);
    }

    [TestCase(
        typeof(DummyStairsDownProducerTarget),
        nameof(DummyStairsDownProducerTarget.HandleEvent),
        typeof(StairsDownTranslationPatch),
        "Use {{W|Shift+D}} to descend.")]
    [TestCase(
        typeof(DummyStairsUpProducerTarget),
        nameof(DummyStairsUpProducerTarget.HandleEvent),
        typeof(StairsUpTranslationPatch),
        "Use {{W|Shift+U}} to ascend.")]
    public void StairsPatch_DoesNotTranslateOwnerPopup_WhenOwnerAbsent(Type targetType, string methodName, Type patchType, string source)
    {
        _ = methodName;
        _ = patchType;
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            InvokeStairsHandleEvent(targetType, source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        typeof(DummyStairsDownProducerTarget),
        nameof(DummyStairsDownProducerTarget.HandleEvent),
        typeof(StairsDownTranslationPatch),
        "Use {{W|Shift+D}} to descend.")]
    [TestCase(
        typeof(DummyStairsUpProducerTarget),
        nameof(DummyStairsUpProducerTarget.HandleEvent),
        typeof(StairsUpTranslationPatch),
        "Use {{W|Shift+U}} to ascend.")]
    public void StairsPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched(
        Type targetType,
        string methodName,
        Type patchType,
        string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(targetType, methodName, typeof(DummyInventoryActionEvent)),
                patchType);

            InvokeStairsHandleEvent(targetType, MessageFrameTranslator.MarkDirectTranslation(source));

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        typeof(DummyStairsDownProducerTarget),
        nameof(DummyStairsDownProducerTarget.HandleEvent),
        typeof(StairsDownTranslationPatch))]
    [TestCase(
        typeof(DummyStairsUpProducerTarget),
        nameof(DummyStairsUpProducerTarget.HandleEvent),
        typeof(StairsUpTranslationPatch))]
    public void StairsPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched(Type targetType, string methodName, Type patchType)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(
                harmony,
                RequireMethod(targetType, methodName, typeof(DummyInventoryActionEvent)),
                patchType);

            InvokeStairsHandleEvent(targetType, string.Empty);

            Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
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

            const string source = "temporal clone implodes.";
            var markedSource = MessageFrameTranslator.MarkDirectTranslation(source);
            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = markedSource,
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

    [TestCase(
        typeof(DummyHologramInvulnerabilityProducerTarget),
        nameof(DummyHologramInvulnerabilityProducerTarget.HandleEvent),
        "glowfish's attack passes harmlessly through hologram.",
        "glowfishの攻撃はhologramを無害に通り抜けた。")]
    [TestCase(
        typeof(DummyDecarbonizerProducerTarget),
        nameof(DummyDecarbonizerProducerTarget.ShutDownTargeting),
        "{{C|decarbonizer}}'s molecular cannon goes offline.",
        "{{C|decarbonizer}}の分子砲がオフラインになった。")]
    [TestCase(
        typeof(DummyPetEitherOrProducerTarget),
        nameof(DummyPetEitherOrProducerTarget.trigger),
        "{{Y|Either}} starts to flicker.",
        "{{Y|Either}}がちらつき始めた。")]
    [TestCase(
        typeof(DummyModPaddedProducerTarget),
        nameof(DummyModPaddedProducerTarget.FireEvent),
        "{{C|leather boots}}'s padding softened the blow.",
        "{{C|leather boots}}の詰め物が衝撃を和らげた。")]
    [TestCase(
        typeof(DummyModPaddedProducerTarget),
        nameof(DummyModPaddedProducerTarget.FireEvent),
        "Your padding softened the blow.",
        "あなたの詰め物が衝撃を和らげた。")]
    [TestCase(
        typeof(DummyMotePropertiesProducerTarget),
        nameof(DummyMotePropertiesProducerTarget.HandleEvent),
        "{{Y|Your glimmer mote}} dissipates.",
        "{{Y|Your glimmer mote}}は霧散した。")]
    public void GeneratedSubjectQueuePatch_TranslatesInventoriedMessages_WhenOwnerPatched(
        Type targetType,
        string methodName,
        string source,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(targetType, methodName),
                typeof(GeneratedSubjectQueueTranslationPatch));

            InvokeGeneratedSubjectQueueTarget(targetType, methodName, source);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GeneratedSubjectQueuePatch_PreservesWholeMessageColorBoundary_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyHologramInvulnerabilityProducerTarget), nameof(DummyHologramInvulnerabilityProducerTarget.HandleEvent)),
                typeof(GeneratedSubjectQueueTranslationPatch));

            InvokeGeneratedSubjectQueueTarget(
                typeof(DummyHologramInvulnerabilityProducerTarget),
                nameof(DummyHologramInvulnerabilityProducerTarget.HandleEvent),
                "{{R|glowfish's attack passes harmlessly through hologram.}}");

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{R|glowfishの攻撃はhologramを無害に通り抜けた。}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GeneratedSubjectQueuePatch_DoesNotTranslateQueuedMessage_WhenOwnerPatchIsAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            const string source = "glowfish's attack passes harmlessly through hologram.";
            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GeneratedSubjectQueuePatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPetEitherOrProducerTarget), nameof(DummyPetEitherOrProducerTarget.trigger)),
                typeof(GeneratedSubjectQueueTranslationPatch));

            var source = MessageFrameTranslator.MarkDirectTranslation("{{Y|Either}}がちらつき始めた。");
            InvokeGeneratedSubjectQueueTarget(
                typeof(DummyPetEitherOrProducerTarget),
                nameof(DummyPetEitherOrProducerTarget.trigger),
                source);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{Y|Either}}がちらつき始めた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GeneratedSubjectQueuePatch_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyDecarbonizerProducerTarget), nameof(DummyDecarbonizerProducerTarget.ShutDownTargeting)),
                typeof(GeneratedSubjectQueueTranslationPatch));

            InvokeGeneratedSubjectQueueTarget(
                typeof(DummyDecarbonizerProducerTarget),
                nameof(DummyDecarbonizerProducerTarget.ShutDownTargeting),
                string.Empty);

            Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
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

            const string source = "The wind becomes still.";
            var markedSource = MessageFrameTranslator.MarkDirectTranslation(source);
            var target = new DummyZoneWindChangeProducerTarget
            {
                QueuedMessageToSend = markedSource,
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
        PatchPopupShow(harmony, nameof(DummyPopupShow.Show));
    }

    private static void PatchPopupShow(Harmony harmony, string methodName, params Type[] parameters)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), methodName, parameters),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchAskNumber(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.AskNumber)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupAskNumberTranslationPatch), nameof(PopupAskNumberTranslationPatch.Prefix))));
    }

    private static void PatchPickOption(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
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

    private static MethodInfo EnclosingMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummyEnclosingProducerTarget.EnterEnclosure) => RequireMethod(
                typeof(DummyEnclosingProducerTarget),
                methodName,
                typeof(DummyGameObject),
                typeof(DummyGameEvent)),
            nameof(DummyEnclosingProducerTarget.ExitEnclosure) => RequireMethod(
                typeof(DummyEnclosingProducerTarget),
                methodName,
                typeof(DummyGameObject),
                typeof(DummyGameEvent),
                typeof(DummyEnclosedEffect)),
            nameof(DummyEnclosingProducerTarget.EnclosureExitImpeded) => RequireMethod(
                typeof(DummyEnclosingProducerTarget),
                methodName,
                typeof(DummyGameObject),
                typeof(bool),
                typeof(DummyEnclosedEffect)),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
        };
    }

    private static void InvokeEnclosingMethod(DummyEnclosingProducerTarget target, string methodName)
    {
        switch (methodName)
        {
            case nameof(DummyEnclosingProducerTarget.EnterEnclosure):
                _ = target.EnterEnclosure(new DummyGameObject(), new DummyGameEvent());
                break;
            case nameof(DummyEnclosingProducerTarget.ExitEnclosure):
                _ = target.ExitEnclosure(new DummyGameObject(), new DummyGameEvent(), new DummyEnclosedEffect());
                break;
            case nameof(DummyEnclosingProducerTarget.EnclosureExitImpeded):
                _ = target.EnclosureExitImpeded(new DummyGameObject(), showMessage: true, new DummyEnclosedEffect());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null);
        }
    }

    private static void AssertPsychometryPopup(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony, nameof(DummyPopupShow.ShowFail));
            PatchOwner(
                harmony,
                RequireMethod(typeof(DummyPsychometryProducerTarget), nameof(DummyPsychometryProducerTarget.HandleEvent), typeof(DummyInventoryActionEvent)),
                typeof(PsychometryTranslationPatch));

            var target = new DummyPsychometryProducerTarget
            {
                PopupMessageToShow = source,
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void InvokeStairsHandleEvent(Type targetType, string source)
    {
        if (targetType == typeof(DummyStairsDownProducerTarget))
        {
            var target = new DummyStairsDownProducerTarget
            {
                PopupMessageToShow = source,
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());
            return;
        }

        if (targetType == typeof(DummyStairsUpProducerTarget))
        {
            var target = new DummyStairsUpProducerTarget
            {
                PopupMessageToShow = source,
            };

            _ = target.HandleEvent(new DummyInventoryActionEvent());
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
    }

    private static void InvokeGeneratedSubjectQueueTarget(Type targetType, string methodName, string source)
    {
        if (targetType == typeof(DummyHologramInvulnerabilityProducerTarget))
        {
            var target = new DummyHologramInvulnerabilityProducerTarget
            {
                QueuedMessageToSend = source,
            };

            _ = target.HandleEvent();
            return;
        }

        if (targetType == typeof(DummyDecarbonizerProducerTarget))
        {
            var target = new DummyDecarbonizerProducerTarget
            {
                QueuedMessageToSend = source,
            };

            _ = target.ShutDownTargeting();
            return;
        }

        if (targetType == typeof(DummyPetEitherOrProducerTarget) && methodName == nameof(DummyPetEitherOrProducerTarget.trigger))
        {
            var target = new DummyPetEitherOrProducerTarget
            {
                QueuedMessageToSend = source,
            };

            target.trigger();
            return;
        }

        if (targetType == typeof(DummyModPaddedProducerTarget))
        {
            var target = new DummyModPaddedProducerTarget
            {
                QueuedMessageToSend = source,
            };

            _ = target.FireEvent();
            return;
        }

        if (targetType == typeof(DummyMotePropertiesProducerTarget))
        {
            var target = new DummyMotePropertiesProducerTarget
            {
                QueuedMessageToSend = source,
            };

            _ = target.HandleEvent();
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
    }

    private static int LiquidVolumePopupHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.LiquidVolumeTranslationPatch." + detail);
    }

    private static int LiquidVolumeQueuedHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(LiquidVolumeTranslationPatch) + ".Queued." + detail);
    }

    private static int LiquidVolumeMessageLogHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(MessageLogPatch),
            nameof(LiquidVolumeTranslationPatch) + ".MessageLog." + detail);
    }

    private static int LiquidVolumePopupPickOptionHitCount(string detail, string family = "Popup.ProducerText")
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupPickOptionTranslationPatch),
            family + ".LiquidVolumeTranslationPatch." + detail);
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

    private void WriteLiquidDictionaries()
    {
        File.WriteAllText(
            Path.Combine(tempDirectory, "ui-liquids.ja.json"),
            "{\"entries\":[{\"key\":\"water\",\"context\":\"XRL.Liquids\",\"text\":\"水\"},{\"key\":\"fresh water\",\"context\":\"XRL.Liquids\",\"text\":\"真水\"}]}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
