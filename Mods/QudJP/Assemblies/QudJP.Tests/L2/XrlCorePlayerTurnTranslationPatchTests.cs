using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class XrlCorePlayerTurnTranslationPatchTests
{
    private static readonly string[] PopupDetails =
    {
        "HpWarning",
        "AutoattackNonHostile",
        "FleePath",
        "ReachPath",
    };

    private static readonly string[] QueueDetails =
    {
        "InvalidInventoryObject",
        "InvalidWaitTurns",
        "NoNearbyHostiles",
        "SetTerseMessages",
        "SetVerboseMessages",
    };

    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "{{R|Your health has dropped below {{C|23%}}!}}",
        "{{R|HPが{{C|23%}}を下回った！}}",
        "HpWarning",
        PopupKind.ShowSpace)]
    [TestCase(
        "You do not autoattack {{G|snapjaw scavenger}} because it is not hostile to you.",
        "{{G|snapjaw scavenger}}は敵対していないため自動攻撃しない。",
        "AutoattackNonHostile",
        PopupKind.ShowFail)]
    [TestCase(
        "You can't find a way to flee from {{C|salt kraken}}.",
        "{{C|salt kraken}}から逃げる経路が見つからない。",
        "FleePath",
        PopupKind.ShowFail)]
    [TestCase(
        "You can't find a way to reach {{Y|the stairs}}.",
        "{{Y|the stairs}}に到達する経路が見つからない。",
        "ReachPath",
        PopupKind.ShowFail)]
    public void PlayerTurn_TranslatesOwnerPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail,
        PopupKind popupKind)
    {
        WithPatchedOwnerAndPopup(() =>
        {
            new DummyXrlCorePlayerTurnTarget
            {
                PopupMessageToShow = source,
                PopupKind = popupKind,
            }.PlayerTurn();

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupKind), Is.EqualTo(expected));
                Assert.That(PopupHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "Invalid inventory object: {{Y|floating short sword}}",
        "無効なインベントリオブジェクト: {{Y|floating short sword}}",
        "InvalidInventoryObject")]
    [TestCase(
        "0 is not a valid number of turns to wait.",
        "0は待機ターン数として無効だ。",
        "InvalidWaitTurns")]
    [TestCase(
        "You don't see any hostiles nearby.",
        "付近に敵対者はいない。",
        "NoNearbyHostiles")]
    [TestCase(
        "Set Terse messages",
        "簡潔なメッセージに設定した。",
        "SetTerseMessages")]
    [TestCase(
        "Set Verbose messages",
        "詳細なメッセージに設定した。",
        "SetVerboseMessages")]
    public void PlayerTurn_TranslatesOwnerQueuedMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyXrlCorePlayerTurnTarget
            {
                QueuedMessageToSend = source,
            }.PlayerTurn();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(QueueHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void PlayerTurn_DoesNotRecordOwnerPopupRoute_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() => DummyPopupShow.ShowFail("You can't find a way to flee from {{C|salt kraken}}."));

        Assert.That(TotalPopupHitCount(), Is.Zero);
    }

    [Test]
    public void PlayerTurn_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        PatchMessageQueueOnly(() => DummyMessageQueue.AddPlayerMessage("Set Terse messages", "white", Capitalize: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("Set Terse messages"));
            Assert.That(TotalQueueHitCount(), Is.Zero);
        });
    }

    [Test]
    public void PlayerTurn_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        const string popup = "You can't find a way to reach {{Y|the stairs}}.";
        const string queued = "Set Verbose messages";

        WithPatchedOwnerAndPopup(() =>
        {
            new DummyXrlCorePlayerTurnTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(popup),
                PopupKind = PopupKind.ShowFail,
            }.PlayerTurn();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(popup));
                Assert.That(TotalPopupHitCount(), Is.Zero);
            });
        });

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyXrlCorePlayerTurnTarget
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(queued),
            }.PlayerTurn();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(queued));
                Assert.That(TotalQueueHitCount(), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You cannot do that on the world map.")]
    [TestCase("Game saved!")]
    [TestCase("You cannot autoattack while you are confused.")]
    [TestCase("You may not {{Y|w}}alk into a hostile creature!")]
    public void PlayerTurn_DoesNotClaimDeferredFixedOrEmptyPopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndPopup(() =>
        {
            new DummyXrlCorePlayerTurnTarget
            {
                PopupMessageToShow = source,
                PopupKind = PopupKind.ShowFail,
            }.PlayerTurn();

            Assert.That(TotalPopupHitCount(), Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("custom player turn message")]
    public void PlayerTurn_DoesNotClaimEmptyOrUnsupportedQueuedMessages_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyXrlCorePlayerTurnTarget
            {
                QueuedMessageToSend = source,
            }.PlayerTurn();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(TotalQueueHitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwnerAndPopup(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowSpace(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedOwnerAndQueue(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueueOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageQueue(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        var prefix = new HarmonyMethod(RequireMethod(
            typeof(PopupShowTranslationPatch),
            nameof(PopupShowTranslationPatch.Prefix),
            typeof(string).MakeByRefType(),
            typeof(MethodBase)));
        var finalizer = new HarmonyMethod(RequireMethod(
            typeof(PopupShowTranslationPatch),
            nameof(PopupShowTranslationPatch.Finalizer),
            typeof(Exception)));

        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: prefix,
            finalizer: finalizer);
    }

    private static void PatchPopupShowSpace(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowSpace)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowSpaceTranslationPatch),
                nameof(PopupShowSpaceTranslationPatch.Prefix),
                typeof(object[]))));
    }

    private static void PatchMessageQueue(Harmony harmony)
    {
        var target = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyXrlCorePlayerTurnTarget), nameof(DummyXrlCorePlayerTurnTarget.PlayerTurn)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(XrlCorePlayerTurnTranslationPatch),
                nameof(XrlCorePlayerTurnTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(XrlCorePlayerTurnTranslationPatch),
                nameof(XrlCorePlayerTurnTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static string? LastPopupMessage(PopupKind popupKind)
    {
        return popupKind == PopupKind.ShowSpace
            ? DummyPopupShow.LastShowSpaceMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static int TotalPopupHitCount()
    {
        var total = 0;
        foreach (var detail in PopupDetails)
        {
            total += PopupHitCount(detail);
        }

        return total;
    }

    private static int TotalQueueHitCount()
    {
        var total = 0;
        foreach (var detail in QueueDetails)
        {
            total += QueueHitCount(detail);
        }

        return total;
    }

    private static int PopupHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(XrlCorePlayerTurnTranslationPatch) + "." + detail)
            + DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowSpaceTranslationPatch),
                "Popup.ProducerText." + nameof(XrlCorePlayerTurnTranslationPatch) + "." + detail);
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(XrlCorePlayerTurnTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            : AccessTools.Method(type, methodName, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} not found");
        return method!;
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.xrl-core-player-turn-l2." + Guid.NewGuid().ToString("N");
    }

    private sealed class DummyXrlCorePlayerTurnTarget
    {
        public string? PopupMessageToShow { get; set; }

        public PopupKind PopupKind { get; set; }

        public string? QueuedMessageToSend { get; set; }

        public void PlayerTurn()
        {
            if (PopupMessageToShow is not null)
            {
                if (PopupKind == PopupKind.ShowSpace)
                {
                    DummyPopupShow.ShowSpace(PopupMessageToShow);
                }
                else
                {
                    DummyPopupShow.ShowFail(PopupMessageToShow);
                }
            }

            if (QueuedMessageToSend is not null)
            {
                DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, "white", Capitalize: false);
            }
        }
    }

    public enum PopupKind
    {
        ShowFail,
        ShowSpace,
    }
}
