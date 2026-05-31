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
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-disassembly-start-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "empty.ja.json"), "{\"entries\":[]}");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        PopupTranslatedMessageHandoff.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        PopupTranslatedMessageHandoff.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void DisassemblyContinue_TranslatesReverseEngineeringPrompt_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "Do you want to try to reverse engineer {{Y|strange artifact}}?",
            "{{Y|strange artifact}}をリバースエンジニアリングしてみる？");
    }

    [Test]
    public void DisassemblyContinue_TranslatesReverseEngineeringPromptItemCapture_WhenOwnerPatched()
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("strange artifact", "奇妙な遺物"));

        AssertPopupMessage(
            "Do you want to try to reverse engineer {{Y|strange artifact}}?",
            "{{Y|奇妙な遺物}}をリバースエンジニアリングしてみる？");
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
    public void DisassemblyContinue_TranslatesStartDisassemblingMessageItemCapture_WhenOwnerPatched()
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("strange artifact", "奇妙な遺物"));

        AssertQueuedMessage(
            "You start disassembling {{Y|strange artifact}}.",
            "{{Y|奇妙な遺物}}の分解を始めた。",
            expectedColor: "white");
    }

    [Test]
    public void DisassemblyContinue_TranslatesStartDisassemblingMessage_WithPossessiveItem_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            "You start disassembling your HEミサイル x29.",
            "HEミサイル x29の分解を始めた。",
            expectedColor: "white");
    }

    [Test]
    public void DisassemblyContinue_TranslatesEurekaBuildReceiptMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            "You disassemble your {{Y|HEミサイル}} x13. Eureka! You may now build {{Y|HEミサイル}}. You receive tinkering bits <1D1D11>.",
            "{{Y|HEミサイル}} x13を分解し、修理ビット<1D1D11>を受け取った。ひらめいた！ {{Y|HEミサイル}}を作れるようになった。",
            expectedColor: "white");
    }

    [Test]
    public void DisassemblyEnd_TranslatesEurekaBuildReceiptPopup_WhenOwnerPatched()
    {
        AssertPopupShowMessage(
            "You disassemble {{c|ケムセル}}. {{G|Eureka! You may now build {{c|ケムセル}}.}} You receive tinkering bits <{{|B{{r|1}}}}>.",
            "{{c|ケムセル}}を分解し、修理ビット<{{|B{{r|1}}}}>を受け取った。ひらめいた！ {{c|ケムセル}}を作れるようになった。");
    }

    [Test]
    public void DisassemblyEnd_TranslatesDisassembleBitsReceiptPopup_WithLocationSuffix_WhenOwnerPatched()
    {
        AssertPopupShowMessage(
            "You disassemble the ひび割れたレンズ here. You receive tinkering bits <{{|{{G|B}}}}>.",
            "ひび割れたレンズを分解し、修理ビット<{{|{{G|B}}}}>を受け取った。");
    }

    [Test]
    public void DisassemblyEnd_TranslatesRuntimeEurekaPopup_WithLocationSuffix_WhenOwnerPatched()
    {
        AssertPopupShowMessage(
            "You disassemble the {{Y|イサッカリライフル}} here. {{G|Eureka! You may now build {{Y|イサッカリライフル}}.}} You receive tinkering bits <{{|{{R|A}}{{C|D}}{{g|2}}}}>.",
            "{{Y|イサッカリライフル}}を分解し、修理ビット<{{|{{R|A}}{{C|D}}{{g|2}}}}>を受け取った。ひらめいた！ {{Y|イサッカリライフル}}を作れるようになった。");
    }

    [Test]
    public void DisassemblyEnd_TranslatesRuntimeEurekaPopup_WithDirectionalLocationSuffix_WhenOwnerPatched()
    {
        AssertPopupShowMessage(
            "You disassemble the {{electrical|帯電}} {{psionic|サイオニック}} {{K|フラーレンの短剣}} to the east. {{G|Eureka! You may now mod items with the 帯電 mod.}} You receive tinkering bits <{{|{{G|B}}{{G|B}}}}>.",
            "{{electrical|帯電}} {{psionic|サイオニック}} {{K|フラーレンの短剣}}を分解し、修理ビット<{{|{{G|B}}{{G|B}}}}>を受け取った。ひらめいた！ 帯電 modでアイテムを改造できるようになった。");
    }

    [Test]
    public void DisassemblyEnd_TranslatesEurekaModReceiptPopup_WhenOwnerPatched()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("liquid-cooled", "液冷式"));

        AssertPopupShowMessage(
            "You disassemble the {{B|liquid-cooled}} チェーンガン. {{G|Eureka! You may now mod items with the {{B|liquid-cooled}} mod.}} You receive tinkering bits <{{|B{{c|4}}}}>.",
            "{{B|液冷式}} チェーンガンを分解し、修理ビット<{{|B{{c|4}}}}>を受け取った。ひらめいた！ {{B|液冷式}} modでアイテムを改造できるようになった。");
    }

    [Test]
    public void DisassemblyEnd_TranslatesEurekaModReceiptPopup_WithColoredModAndColoredBaseItem_WhenOwnerPatched()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("liquid-cooled", "液冷式"));
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("chain gun", "チェーンガン"));

        AssertPopupShowMessage(
            "You disassemble the {{B|liquid-cooled}} {{Y|chain gun}}. {{G|Eureka! You may now mod items with the {{B|liquid-cooled}} mod.}} You receive tinkering bits <{{|B{{c|4}}}}>.",
            "{{B|液冷式}} {{Y|チェーンガン}}を分解し、修理ビット<{{|B{{c|4}}}}>を受け取った。ひらめいた！ {{B|液冷式}} modでアイテムを改造できるようになった。");
    }

    [Test]
    public void DisassemblyEnd_TranslatesPartiallyLocalizedEurekaModReceiptPopup_WhenOwnerPatched()
    {
        AssertPopupShowMessage(
            "{{B|液冷式}} チェーンガン. {{G|Eureka! You may now mod items with the {{B|液冷式}} mod.}} You receive tinkering bits <{{|B{{c|4}}}}>.",
            "{{B|液冷式}} チェーンガンを分解し、修理ビット<{{|B{{c|4}}}}>を受け取った。ひらめいた！ {{B|液冷式}} modでアイテムを改造できるようになった。");
    }

    [Test]
    public void DisassemblyContinue_MarksEurekaBuildReceipt_WhenOwnerPatched()
    {
        var message = "You disassemble your {{Y|HEミサイル}} x13. Eureka! You may now build {{Y|HEミサイル}}. You receive tinkering bits <1D1D11>.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchCombatQueue(harmony);
            PatchOwner(harmony);

            DummyDisassemblyStartTarget.MessageToSend = message;
            DummyDisassemblyStartTarget.ColorToSend = "white";
            DummyDisassemblyStartTarget.ContinueQueue();

            Assert.Multiple(() =>
            {
                Assert.That(
                    MessageFrameTranslator.TryStripDirectTranslationMarker(DummyMessageQueue.LastMessage, out var visible),
                    Is.True);
                Assert.That(
                    visible,
                    Is.EqualTo("{{Y|HEミサイル}} x13を分解し、修理ビット<1D1D11>を受け取った。ひらめいた！ {{Y|HEミサイル}}を作れるようになった。"));
            });
        }
        finally
        {
            DummyDisassemblyStartTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MessageLog_DoesNotConsumePopupHandoff_ForLegacyPopupLog()
    {
        const string source = "You disassemble the {{Y|イサッカリライフル}} here. {{G|Eureka! You may now build {{Y|イサッカリライフル}}.}} You receive tinkering bits <{{|{{R|A}}{{C|D}}{{g|2}}}}>.";
        const string expected = "{{Y|イサッカリライフル}}を分解し、修理ビット<{{|{{R|A}}{{C|D}}{{g|2}}}}>を受け取った。ひらめいた！ {{Y|イサッカリライフル}}を作れるようになった。";

        PopupTranslatedMessageHandoff.EnterScope();
        try
        {
            PopupTranslatedMessageHandoff.Remember(source, expected);
            var message = source;

            _ = MessageLogPatch.Prefix(ref message);

            Assert.Multiple(() =>
            {
                Assert.That(message, Is.EqualTo(source));
                Assert.That(PopupTranslatedMessageHandoff.TryGet(source, out var handedOff), Is.True);
                Assert.That(handedOff, Is.EqualTo(expected));
            });
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitCurrentScope();
        }
    }

    [Test]
    public void MessageLog_DoesNotConsumePopupHandoff_ForDisassembleBitsReceipt()
    {
        const string source = "You disassemble the ひび割れたレンズ here. You receive tinkering bits <{{|{{G|B}}}}>.";
        const string expected = "ひび割れたレンズを分解し、修理ビット<{{|{{G|B}}}}>を受け取った。";

        PopupTranslatedMessageHandoff.EnterScope();
        try
        {
            PopupTranslatedMessageHandoff.Remember(source, expected);
            var message = source;

            _ = MessageLogPatch.Prefix(ref message);

            Assert.Multiple(() =>
            {
                Assert.That(message, Is.EqualTo(source));
                Assert.That(PopupTranslatedMessageHandoff.TryGet(source, out var handedOff), Is.True);
                Assert.That(handedOff, Is.EqualTo(expected));
            });
        }
        finally
        {
            PopupTranslatedMessageHandoff.ExitCurrentScope();
        }
    }

    [Test]
    public void DisassemblyContinue_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string popup = "Do you want to try to reverse engineer {{Y|strange artifact}}?";
        const string queued = "You start disassembling {{Y|strange artifact}}.";
        const string eureka = "You disassemble your {{Y|HEミサイル}} x13. Eureka! You may now build {{Y|HEミサイル}}. You receive tinkering bits <1D1D11>.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchQueue(harmony);

            DummyPopupShow.ShowYesNo(popup);
            DummyMessageQueue.AddPlayerMessage(queued, "white", Capitalize: false);
            DummyMessageQueue.AddPlayerMessage(eureka, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(popup));
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(eureka));
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

    private static void AssertPopupShowMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyDisassemblyStartTarget.PopupMessageToShow = source;
            DummyDisassemblyStartTarget.EndPopup();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
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

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        PatchCombatQueue(harmony);
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchCombatQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyDisassemblyStartTarget.ContinuePopup),
            nameof(DummyDisassemblyStartTarget.ContinueQueue),
            nameof(DummyDisassemblyStartTarget.EndPopup),
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

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var contents = "{\"entries\":["
            + string.Join(
                ",",
                entries.Select(entry => $"{{\"key\":\"{EscapeJson(entry.key)}\",\"text\":\"{EscapeJson(entry.text)}\"}}"))
            + "]}";
        File.WriteAllText(Path.Combine(tempDirectory, fileName), contents);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool EndPopup()
        {
            DummyPopupShow.Show(PopupMessageToShow);
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
