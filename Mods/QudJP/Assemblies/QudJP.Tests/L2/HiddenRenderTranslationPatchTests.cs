using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HiddenRenderTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-hidden-render-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "empty.ja.json"), "{\"entries\":[]}");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "A {{Y|stone crevice}} is revealed to the north!",
        "北側に{{Y|stone crevice}}が現れた！")]
    [TestCase(
        "A {{Y|stone crevice}} is revealed nearby!",
        "近くに{{Y|stone crevice}}が現れた！")]
    [TestCase(
        "A {{Y|stone crevice}} is revealed here!",
        "ここに{{Y|stone crevice}}が現れた！")]
    public void HiddenRender_TranslatesRevealMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void Hidden_TranslatesRevealMessages_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            "A {{g|{{Y|塩気のある}}{{slimy|粘液質の}}若いアイボリー}} is revealed to the southeast!",
            "南東側に{{g|{{Y|塩気のある}}{{slimy|粘液質の}}若いアイボリー}}が現れた！",
            expectedColor: "white",
            useHiddenOwner: true);
    }

    [Test]
    public void HiddenRender_TranslatesGeneratedRevealSubject_WhenOwnerPatched()
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("stone crevice", "石の割れ目"));

        AssertQueuedMessage(
            "A {{Y|stone crevice}} is revealed to the north!",
            "北側に{{Y|石の割れ目}}が現れた！",
            expectedColor: "white");
    }

    [Test]
    public void HiddenRender_TranslatesRevealMessageLog_WhenOwnerScopeIsActive()
    {
        var message = "A {{B|ヨンダーブラッシュ}} is revealed to the east!";

        HiddenRenderTranslationPatch.Prefix();
        try
        {
            MessageLogPatch.Prefix(ref message);
        }
        finally
        {
            _ = HiddenRenderTranslationPatch.Finalizer(null);
        }

        Assert.That(message, Is.EqualTo("東側に{{B|ヨンダーブラッシュ}}が現れた！"));
    }

    [Test]
    public void HiddenRender_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "A {{Y|stone crevice}} is revealed to the north!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HiddenRender_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("A stone crevice is revealed nearby!"),
            "A stone crevice is revealed nearby!");
    }

    [Test]
    public void TryTranslateQueuedMessage_StripsDirectMarker_WhenOwnerScopeIsActive()
    {
        var message = MessageFrameTranslator.MarkDirectTranslation("A stone crevice is revealed nearby!");

        HiddenRenderTranslationPatch.Prefix();
        try
        {
            var translated = HiddenRenderTranslationPatch.TryTranslateQueuedMessage(ref message, "white");

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.True);
                Assert.That(message, Is.EqualTo("A stone crevice is revealed nearby!"));
            });
        }
        finally
        {
            _ = HiddenRenderTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void TryTranslateQueuedMessage_DoesNotReuseDirectMarkerPassthrough_AfterOwnerScopeExit()
    {
        var directMessage = MessageFrameTranslator.MarkDirectTranslation("A stone crevice is revealed nearby!");

        HiddenRenderTranslationPatch.Prefix();
        try
        {
            _ = HiddenRenderTranslationPatch.TryTranslateQueuedMessage(ref directMessage, "white");
        }
        finally
        {
            _ = HiddenRenderTranslationPatch.Finalizer(null);
        }

        var nextMessage = "A stone crevice is revealed nearby!";
        HiddenRenderTranslationPatch.Prefix();
        try
        {
            var translated = HiddenRenderTranslationPatch.TryTranslateQueuedMessage(ref nextMessage, "white");

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.True);
                Assert.That(nextMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation("近くにstone creviceが現れた！")));
            });
        }
        finally
        {
            _ = HiddenRenderTranslationPatch.Finalizer(null);
        }
    }

    [TestCase("")]
    [TestCase("A stone crevice is hidden nearby!")]
    [TestCase("A stone crevice revealed nearby!")]
    public void HiddenRender_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(source, source);
    }

    private static void AssertQueuedMessage(
        string source,
        string expected,
        string? expectedColor = null,
        bool useHiddenOwner = false)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, useHiddenOwner);

            DummyHiddenRenderTarget.MessageToSend = source;
            DummyHiddenRenderTarget.ColorToSend = expectedColor;
            if (useHiddenOwner)
            {
                DummyHiddenRenderTarget.HiddenRevealInternal(silent: false);
            }
            else
            {
                DummyHiddenRenderTarget.Reveal();
            }

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyHiddenRenderTarget.Reset();
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

    private static void PatchOwner(Harmony harmony, bool useHiddenOwner = false)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyHiddenRenderTarget),
                useHiddenOwner
                    ? nameof(DummyHiddenRenderTarget.HiddenRevealInternal)
                    : nameof(DummyHiddenRenderTarget.Reveal),
                useHiddenOwner ? new[] { typeof(bool) } : Type.EmptyTypes),
            prefix: new HarmonyMethod(RequireMethod(typeof(HiddenRenderTranslationPatch), nameof(HiddenRenderTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(HiddenRenderTranslationPatch), nameof(HiddenRenderTranslationPatch.Finalizer), typeof(Exception))));
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyHiddenRenderTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Reveal()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HiddenRevealInternal(bool silent = false)
        {
            _ = silent;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
