using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WindupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "You try to wind {{Y|music box}}, but it is unresponsive.",
        "{{Y|music box}}を巻こうとしたが、反応しない。")]
    [TestCase(
        "You wind {{Y|music box}}.",
        "{{Y|music box}}を巻いた。")]
    public void Windup_TranslatesPlayerPopups_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [TestCase(
        "The {{Y|snapjaw}} tries to wind a {{C|music box}}, but it is unresponsive.",
        "{{Y|snapjaw}}はa {{C|music box}}を巻こうとしたが、反応しなかった。")]
    [TestCase(
        "The {{Y|snapjaw}} winds a {{C|music box}}.",
        "{{Y|snapjaw}}はa {{C|music box}}を巻いた。")]
    public void Windup_TranslatesObserverQueueMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void Windup_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "You wind {{Y|music box}}.";
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
    public void Windup_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The snapjaw winds a music box."),
            "The snapjaw winds a music box.");
    }

    [Test]
    public void Windup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You wind the music box."),
            "You wind the music box.");
    }

    [TestCase("")]
    [TestCase("You crank {{Y|music box}}.")]
    [TestCase("The {{Y|snapjaw}} tries to crank a {{C|music box}}, but it is unresponsive.")]
    public void Windup_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyWindupTarget.MessageToSend = source;
            DummyWindupTarget.ColorToSend = expectedColor;
            DummyWindupTarget.HandleEventQueue();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyWindupTarget.Reset();
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

            DummyWindupTarget.PopupMessageToShow = source;
            DummyWindupTarget.HandleEventPopup();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyWindupTarget.Reset();
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
            nameof(DummyWindupTarget.HandleEventQueue),
            nameof(DummyWindupTarget.HandleEventPopup),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWindupTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(WindupTranslationPatch), nameof(WindupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(WindupTranslationPatch), nameof(WindupTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyWindupTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HandleEventQueue()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HandleEventPopup()
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
