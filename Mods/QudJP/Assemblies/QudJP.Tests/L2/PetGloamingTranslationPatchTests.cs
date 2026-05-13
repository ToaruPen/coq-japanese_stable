using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PetGloamingTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "The {{Y|gloaming}}'s astral tether snaps and its binal specter substantiates as a {{C|snapjaw}}.",
        "{{Y|gloaming}}の星幽の繋ぎ紐が切れ、二元の幻影がa {{C|snapjaw}}として実体化した。")]
    [TestCase(
        "The {{Y|gloaming}} stops gleaming.",
        "{{Y|gloaming}}は輝くのをやめた。")]
    [TestCase(
        "The {{Y|gloaming}} starts to gleam with an {{K|unearthly light}}.",
        "{{Y|gloaming}}は{{K|この世ならぬ光}}で輝き始めた。")]
    public void PetGloaming_TranslatesQueuedStateMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void PetGloaming_TranslatesWisdomRevealPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "The {{Y|gloaming}} beats its wings, and the shattered voices of a trillion worlds ride the current of air and harmonize into one, revealing the following wisdom:\n\nThe location of {{Y|Bethesda Susa}}",
            "{{Y|gloaming}}は翼を羽ばたかせた。砕けた一兆の世界の声が気流に乗って一つに調和し、次の叡智を明かした:\n\nThe location of {{Y|Bethesda Susa}}");
    }

    [Test]
    public void PetGloaming_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|gloaming}} stops gleaming.";
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
    public void PetGloaming_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The gloaming stops gleaming."),
            "The gloaming stops gleaming.");
    }

    [Test]
    public void PetGloaming_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("The gloaming beats its wings."),
            "The gloaming beats its wings.");
    }

    [TestCase("")]
    [TestCase("The gloaming gleams.")]
    [TestCase("The gloaming starts glowing.")]
    public void PetGloaming_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyPetGloamingTarget.MessageToSend = source;
            DummyPetGloamingTarget.ColorToSend = expectedColor;
            DummyPetGloamingTarget.FireEventQueue();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyPetGloamingTarget.Reset();
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

            DummyPetGloamingTarget.PopupMessageToShow = source;
            DummyPetGloamingTarget.FireEventPopup();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyPetGloamingTarget.Reset();
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
            nameof(DummyPetGloamingTarget.FireEventQueue),
            nameof(DummyPetGloamingTarget.FireEventPopup),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPetGloamingTarget), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(PetGloamingTranslationPatch), nameof(PetGloamingTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(PetGloamingTranslationPatch), nameof(PetGloamingTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyPetGloamingTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FireEventQueue()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FireEventPopup()
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
