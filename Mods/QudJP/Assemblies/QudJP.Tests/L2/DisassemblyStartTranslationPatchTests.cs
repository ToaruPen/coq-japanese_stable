using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DisassemblyStartTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [Test]
    public void DisassemblyContinue_TranslatesReverseEngineeringPrompt_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "Do you want to try to reverse engineer {{Y|strange artifact}}?",
            "{{Y|strange artifact}}をリバースエンジニアリングしてみる？");
    }

    [Test]
    public void DisassemblyContinue_TranslatesStartDisassemblingMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            "You start disassembling {{Y|strange artifact}}.",
            "{{Y|strange artifact}}の分解を始めた。",
            expectedColor: "white");
    }

    [Test]
    public void DisassemblyContinue_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string popup = "Do you want to try to reverse engineer {{Y|strange artifact}}?";
        const string queued = "You start disassembling {{Y|strange artifact}}.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchQueue(harmony);

            DummyPopupShow.ShowYesNo(popup);
            DummyMessageQueue.AddPlayerMessage(queued, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(popup));
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(queued));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DisassemblyContinue_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("Do you want to try to reverse engineer strange artifact?"),
            "Do you want to try to reverse engineer strange artifact?");
    }

    [Test]
    public void DisassemblyContinue_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("You start disassembling strange artifact."),
            "You start disassembling strange artifact.");
    }

    [TestCase("")]
    [TestCase("You finish disassembling {{Y|strange artifact}}.")]
    [TestCase("Do you want to disassemble {{Y|strange artifact}}?")]
    public void DisassemblyContinue_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source);
        AssertQueuedMessage(source, source);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchOwner(harmony);

            DummyDisassemblyStartTarget.PopupMessageToShow = source;
            DummyDisassemblyStartTarget.ContinuePopup();

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyDisassemblyStartTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertQueuedMessage(
        string source,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyDisassemblyStartTarget.MessageToSend = source;
            DummyDisassemblyStartTarget.ColorToSend = expectedColor;
            DummyDisassemblyStartTarget.ContinueQueue();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyDisassemblyStartTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
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

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyDisassemblyStartTarget.ContinuePopup),
            nameof(DummyDisassemblyStartTarget.ContinueQueue),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDisassemblyStartTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(DisassemblyStartTranslationPatch), nameof(DisassemblyStartTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(DisassemblyStartTranslationPatch), nameof(DisassemblyStartTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyDisassemblyStartTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;
        public static string MessageToSend { get; set; } = string.Empty;
        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ContinuePopup()
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ContinueQueue()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
