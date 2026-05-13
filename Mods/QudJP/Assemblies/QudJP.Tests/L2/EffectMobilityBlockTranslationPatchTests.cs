using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EffectMobilityBlockTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TestCase("You are immobilized&y!", "移動不能だ！")]
    [TestCase("You are {{|immobilized}}!", "{{|移動不能}}だ！")]
    [TestCase("You are stuck!", "拘束されている！")]
    [TestCase("You are {{Y|stuck}}!", "{{Y|拘束}}されている！")]
    public void EffectMobilityBlock_TranslatesQueuedMobilityBlockMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "R");
    }

    [TestCase("You are immobilized&y!", "移動不能だ！", false)]
    [TestCase("You are {{|immobilized}}!", "{{|移動不能}}だ！", true)]
    [TestCase("You are stuck!", "拘束されている！", false)]
    [TestCase("You are {{Y|stuck}}!", "{{Y|拘束}}されている！", false)]
    public void EffectMobilityBlock_TranslatesPopupMobilityBlockMessages_WhenOwnerPatched(
        string source,
        string expected,
        bool showFail)
    {
        AssertPopupMessage(source, expected, showFail);
    }

    [TestCase("You are immobilized&y!")]
    [TestCase("You are stuck!")]
    public void EffectMobilityBlock_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "R", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EffectMobilityBlock_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You are stuck!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EffectMobilityBlock_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You are stuck!"),
            "You are stuck!");
    }

    [Test]
    public void EffectMobilityBlock_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You are stuck!"),
            "You are stuck!",
            showFail: false);
    }

    [TestCase("")]
    [TestCase("You are mobile!")]
    [TestCase("You feel stuck!")]
    public void EffectMobilityBlock_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(source, source);
        AssertPopupMessage(source, source, showFail: false);
    }

    private static void AssertQueuedMessage(string source, string expected, string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyEffectMobilityBlockTarget.MessageToSend = source;
            DummyEffectMobilityBlockTarget.ColorToSend = expectedColor;
            _ = DummyEffectMobilityBlockTarget.FireEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyEffectMobilityBlockTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPopupMessage(string source, string expected, bool showFail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowFail(harmony);
            PatchOwner(harmony);

            DummyEffectMobilityBlockTarget.PopupMessageToShow = source;
            DummyEffectMobilityBlockTarget.UseShowFail = showFail;
            _ = DummyEffectMobilityBlockTarget.FireEventPopup();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyEffectMobilityBlockTarget.Reset();
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

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyEffectMobilityBlockTarget), nameof(DummyEffectMobilityBlockTarget.FireEvent)),
            prefix: new HarmonyMethod(RequireMethod(typeof(EffectMobilityBlockTranslationPatch), nameof(EffectMobilityBlockTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(EffectMobilityBlockTranslationPatch), nameof(EffectMobilityBlockTranslationPatch.Finalizer), typeof(Exception))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyEffectMobilityBlockTarget), nameof(DummyEffectMobilityBlockTarget.FireEventPopup)),
            prefix: new HarmonyMethod(RequireMethod(typeof(EffectMobilityBlockTranslationPatch), nameof(EffectMobilityBlockTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(EffectMobilityBlockTranslationPatch), nameof(EffectMobilityBlockTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyEffectMobilityBlockTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        public static string PopupMessageToShow { get; set; } = string.Empty;

        public static bool UseShowFail { get; set; }

        public static bool FireEvent()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        public static bool FireEventPopup()
        {
            if (UseShowFail)
            {
                DummyPopupShow.ShowFail(PopupMessageToShow);
            }
            else
            {
                DummyPopupShow.Show(PopupMessageToShow);
            }

            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
            PopupMessageToShow = string.Empty;
            UseShowFail = false;
        }
    }
}
