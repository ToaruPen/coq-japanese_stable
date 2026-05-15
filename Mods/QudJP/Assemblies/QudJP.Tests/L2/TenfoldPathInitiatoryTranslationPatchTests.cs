using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TenfoldPathInitiatoryTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "You feel a sense of infinite grace flow through your being as you are brought from the brink of death to miraculous health.",
        "無限の恩寵が身を満たし、死の淵から奇跡的な回復へと引き戻された。")]
    [TestCase(
        "The pilgrim{{white|shines with a supernal light}} as its injuries disappear.",
        "The pilgrimは{{white|超越的な光を放ち}}、傷が消えた。")]
    public void TenfoldPath_TranslatesQueuedInitiatoryMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void TenfoldPath_TranslatesAttackInhibition_WhenFireEventOwnerPatched()
    {
        AssertQueuedMessage(
            "You cannot bring yourself to attack {{Y|Q Girl}}.",
            "{{Y|Q Girl}}を攻撃する勇気が出ない。",
            expectedColor: "r",
            useFireEvent: true);
    }

    [TestCase("You gain 30 skill points.", "スキルポイントを30獲得した。")]
    [TestCase("You gain {{C|1}} skill point.", "スキルポイントを{{C|1}}獲得した。")]
    public void TenfoldPath_TranslatesPopupSkillPointReward_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [TestCase("You cannot bring yourself to attack {{Y|Q Girl}}.")]
    [TestCase("You gain 30 skill points.")]
    public void TenfoldPath_DoesNotTranslateTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchPopupShow(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);
            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TenfoldPath_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You cannot bring yourself to attack Q Girl."),
            "You cannot bring yourself to attack Q Girl.");
    }

    [Test]
    public void TenfoldPath_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You gain 30 skill points."),
            "You gain 30 skill points.");
    }

    [TestCase("")]
    [TestCase("You gain skill points.")]
    [TestCase("You cannot attack Q Girl.")]
    public void TenfoldPath_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(source, source);
        AssertPopupMessage(source, source);
    }

    private static void AssertQueuedMessage(
        string source,
        string expected,
        string? expectedColor = null,
        bool useFireEvent = false)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyTenfoldPathTarget.MessageToSend = source;
            DummyTenfoldPathTarget.ColorToSend = expectedColor;
            if (useFireEvent)
            {
                DummyTenfoldPathTarget.FireEvent();
            }
            else
            {
                _ = DummyTenfoldPathTarget.HandleBeforeDie();
            }

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyTenfoldPathTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyTenfoldPathTarget.PopupMessageToShow = source;
            _ = DummyTenfoldPathTarget.AddSkill();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyTenfoldPathTarget.Reset();
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

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyTenfoldPathTarget.HandleBeforeDie),
            nameof(DummyTenfoldPathTarget.FireEvent),
            nameof(DummyTenfoldPathTarget.AddSkill),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTenfoldPathTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(TenfoldPathInitiatoryTranslationPatch), nameof(TenfoldPathInitiatoryTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(TenfoldPathInitiatoryTranslationPatch), nameof(TenfoldPathInitiatoryTranslationPatch.Finalizer), typeof(Exception))));
        }
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyTenfoldPathTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        public static string PopupMessageToShow { get; set; } = string.Empty;

        public static bool HandleBeforeDie()
        {
            SendQueuedMessage();
            return true;
        }

        public static void FireEvent()
        {
            SendQueuedMessage();
        }

        public static bool AddSkill()
        {
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
            PopupMessageToShow = string.Empty;
        }

        private static void SendQueuedMessage()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }
    }
}
