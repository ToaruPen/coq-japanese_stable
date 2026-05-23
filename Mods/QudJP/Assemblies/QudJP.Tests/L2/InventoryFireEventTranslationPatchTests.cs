using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class InventoryFireEventTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void InventoryFireEvent_TranslatesGraveyardZoneQueuedMessage_WhenOwnerPatched()
    {
        WithPatchedInventoryOwnerAndQueue(() =>
        {
            new DummyInventoryFireEventProducerTarget
            {
                QueuedMessageToSend = "{{Y|chrome idol}}] Error dropping object, removing to graveyard zone! (Inventory.cs:CommandEquipObject)",
            }.FireEventQueuedMessage();

            Assert.That(
                DummyMessageQueue.LastMessage,
                Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(
                    "{{Y|chrome idol}}] オブジェクトを落とせません。墓地ゾーンに移動します！ (Inventory.cs:CommandEquipObject)")));
        });
    }

    [Test]
    public void InventoryFireEvent_TranslatesContainerOwnershipPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(InventoryFireEventTranslationPatch),
            RequireMethod(nameof(DummyInventoryFireEventProducerTarget.FireEventPopup)),
            () =>
            {
                new DummyInventoryFireEventProducerTarget
                {
                    PopupMessageToShow = "You don't own {{Y|the ornate chest}}. Are you sure you want to take {{W|the weird artifact}}?",
                }.FireEventPopup();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowYesNoMessage,
                        Is.EqualTo("{{Y|the ornate chest}}はあなたのものではない。本当に{{W|the weird artifact}}を取りますか？"));
                    Assert.That(PopupHitCount("ContainerOwnershipPrompt"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void InventoryFireEvent_TranslatesInventoryOwnedFailurePopups_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(InventoryFireEventTranslationPatch),
            RequireMethod(nameof(DummyInventoryFireEventProducerTarget.FireEventShowFail)),
            () =>
            {
                var target = new DummyInventoryFireEventProducerTarget();

                target.PopupMessageToShow = "You cannot equip items while stuck!";
                target.FireEventShowFail();
                var stuckEquip = DummyPopupShow.LastShowMessage;

                target.PopupMessageToShow = "You cannot equip {{Y|steel boots}}.";
                target.FireEventShowFail();
                var cannotEquip = DummyPopupShow.LastShowMessage;

                target.PopupMessageToShow = "You cannot equip {{Y|steel boots}} on your left hand.";
                target.FireEventShowFail();
                var cannotEquipOnSlot = DummyPopupShow.LastShowMessage;

                target.PopupMessageToShow = "You cannot remove items while stuck!";
                target.FireEventShowFail();
                var stuckRemove = DummyPopupShow.LastShowMessage;

                target.PopupMessageToShow = "You cannot budge {{Y|rusted chest}}.";
                target.FireEventShowFail();
                var cannotBudge = DummyPopupShow.LastShowMessage;

                Assert.Multiple(() =>
                {
                    Assert.That(stuckEquip, Is.EqualTo("動けない間はアイテムを装備できない！"));
                    Assert.That(cannotEquip, Is.EqualTo("{{Y|steel boots}}を装備できない。"));
                    Assert.That(cannotEquipOnSlot, Is.EqualTo("{{Y|steel boots}}をleft handに装備できない。"));
                    Assert.That(stuckRemove, Is.EqualTo("動けない間はアイテムを外せない！"));
                    Assert.That(cannotBudge, Is.EqualTo("{{Y|rusted chest}}を動かせない。"));
                    Assert.That(PopupHitCount("InventoryFailurePopup"), Is.EqualTo(5));
                });
            });
    }

    [Test]
    public void InventoryFireEvent_DoesNotClaimOwnerMessages_WhenOwnerAbsent()
    {
        var queueMessage = "{{Y|chrome idol}}] Error dropping object, removing to graveyard zone! (Inventory.cs:CommandEquipObject)";
        var popupMessage = "You don't own the ornate chest. Are you sure you want to take the weird artifact?";

        Assert.Multiple(() =>
        {
            Assert.That(InventoryFireEventTranslationPatch.TryTranslateQueuedMessage(ref queueMessage, null), Is.False);
            Assert.That(queueMessage, Does.Contain("Error dropping object"));

            Assert.That(InventoryFireEventTranslationPatch.TryTranslatePopupMessage(
                popupMessage,
                nameof(PopupShowTranslationPatch),
                nameof(InventoryFireEventTranslationPatch),
                out var translated), Is.False);
            Assert.That(translated, Is.EqualTo(popupMessage));
        });
    }

    [Test]
    public void InventoryFireEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You don't own the ornate chest. Are you sure you want to take the weird artifact?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(InventoryFireEventTranslationPatch),
            RequireMethod(nameof(DummyInventoryFireEventProducerTarget.FireEventPopup)),
            () =>
            {
                new DummyInventoryFireEventProducerTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.FireEventPopup();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("ContainerOwnershipPrompt"), Is.Zero);
                });
            });
    }

    [Test]
    public void InventoryFireEvent_LeavesRuntimeFailureMessagesUnchanged_WhenOwnerPatched()
    {
        WithPatchedInventoryOwnerAndQueue(() =>
        {
            new DummyInventoryFireEventProducerTarget
            {
                QueuedMessageToSend = "The equipped item refused to be unequipped.",
            }.FireEventQueuedMessage();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The equipped item refused to be unequipped."));
        });
    }

    private static void WithPatchedInventoryOwnerAndQueue(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(nameof(DummyInventoryFireEventProducerTarget.FireEventQueuedMessage)),
                prefix: new HarmonyMethod(RequirePatchMethod(nameof(InventoryFireEventTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequirePatchMethod(nameof(InventoryFireEventTranslationPatch.Finalizer), typeof(Exception))));
            harmony.Patch(
                original: OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(CombatAndLogMessageQueuePatch),
                    nameof(CombatAndLogMessageQueuePatch.Prefix))));

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyInventoryFireEventProducerTarget), methodName);
    }

    private static MethodInfo RequirePatchMethod(string methodName, params Type[] parameterTypes)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(InventoryFireEventTranslationPatch), methodName, parameterTypes);
    }

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(InventoryFireEventTranslationPatch), detail);
    }

    private sealed class DummyInventoryFireEventProducerTarget
    {
        public string QueuedMessageToSend { get; set; } = string.Empty;

        public string PopupMessageToShow { get; set; } = string.Empty;

        public void FireEventQueuedMessage()
        {
            DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        }

        public void FireEventPopup()
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }

        public void FireEventShowFail()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
    }
}
