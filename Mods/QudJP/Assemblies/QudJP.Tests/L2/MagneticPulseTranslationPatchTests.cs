using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MagneticPulseTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(RepositoryDictionaryDirectory());
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "The {{Y|steel boots}} are ripped from your body!",
        "{{Y|steel boots}}があなたの体から引き剥がされた！")]
    [TestCase(
        "Your companion, {{Y|Q Girl}},has had a {{C|steel sword}} ripped from her body!",
        "{{Y|Q Girl}}の体から{{C|steel sword}}が引き剥がされた！")]
    public void MagneticPulse_TranslatesRippedEquipmentPopups_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [TestCase(
        "The {{Y|steel sword}} is pulled toward {{M|the magnet}}.",
        "{{Y|steel sword}}は{{M|the magnet}}に引き寄せられた。")]
    [TestCase(
        "The {{Y|steel sword}} is pulled toward something.",
        "{{Y|steel sword}}は何かに引き寄せられた。")]
    public void MagneticPulse_TranslatesPulledQueueMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void MagneticPulse_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|steel sword}} is pulled toward something.";
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
    public void MagneticPulse_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The steel sword is pulled toward something."),
            "The steel sword is pulled toward something.");
    }

    [Test]
    public void MagneticPulse_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("The steel boots are ripped from your body!"),
            "The steel boots are ripped from your body!");
    }

    [TestCase("")]
    [TestCase("The steel sword moves toward something.")]
    [TestCase("Your companion, Q Girl, had a steel sword ripped from her body!")]
    public void MagneticPulse_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(source, source);
        AssertPopupMessage(source, source);
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

            DummyMagneticPulseTarget.MessageToSend = source;
            DummyMagneticPulseTarget.ColorToSend = expectedColor;
            DummyMagneticPulseTarget.EmitMagneticPulseQueue();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyMagneticPulseTarget.Reset();
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

            DummyMagneticPulseTarget.PopupMessageToShow = source;
            DummyMagneticPulseTarget.EmitMagneticPulsePopup();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyMagneticPulseTarget.Reset();
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
            nameof(DummyMagneticPulseTarget.EmitMagneticPulseQueue),
            nameof(DummyMagneticPulseTarget.EmitMagneticPulsePopup),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMagneticPulseTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(MagneticPulseTranslationPatch), nameof(MagneticPulseTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(MagneticPulseTranslationPatch), nameof(MagneticPulseTranslationPatch.Finalizer), typeof(Exception))));
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

    private static string RepositoryDictionaryDirectory()
    {
        return Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");
    }

    private static string RepositoryMessageFramePath()
    {
        return Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "MessageFrames", "verbs.ja.json");
    }

    private static class DummyMagneticPulseTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void EmitMagneticPulseQueue()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void EmitMagneticPulsePopup()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
            PopupMessageToShow = string.Empty;
        }
    }
}
