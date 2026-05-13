using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CloningStartBuddedCloneTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [Test]
    public void CloningStartBuddedClone_TranslatesDetachPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "A {{Y|budded clone}} detaches from you!",
            "{{Y|budded clone}}があなたから分離した！");
    }

    [Test]
    public void CloningStartBuddedClone_TranslatesDetachQueueMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            "A {{Y|budded clone}} detaches from {{C|salt kraken}}!",
            "{{Y|budded clone}}が{{C|salt kraken}}から分離した！",
            expectedColor: "white");
    }

    [Test]
    public void CloningStartBuddedClone_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "A {{Y|budded clone}} detaches from you!";
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
    public void CloningStartBuddedClone_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("A clone detaches from salt kraken!"),
            "A clone detaches from salt kraken!");
    }

    [Test]
    public void CloningStartBuddedClone_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("A clone detaches from you!"),
            "A clone detaches from you!");
    }

    [TestCase("")]
    [TestCase("A budded clone attaches to you!")]
    [TestCase("A budded clone detaches.")]
    public void CloningStartBuddedClone_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyCloningStartBuddedCloneTarget.MessageToSend = source;
            DummyCloningStartBuddedCloneTarget.ColorToSend = expectedColor;
            DummyCloningStartBuddedCloneTarget.StartBuddedCloneQueue(new object(), new object());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyCloningStartBuddedCloneTarget.Reset();
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

            DummyCloningStartBuddedCloneTarget.PopupMessageToShow = source;
            DummyCloningStartBuddedCloneTarget.StartBuddedClonePopup(new object(), new object());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyCloningStartBuddedCloneTarget.Reset();
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
            nameof(DummyCloningStartBuddedCloneTarget.StartBuddedCloneQueue),
            nameof(DummyCloningStartBuddedCloneTarget.StartBuddedClonePopup),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCloningStartBuddedCloneTarget), methodName, typeof(object), typeof(object)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CloningStartBuddedCloneTranslationPatch), nameof(CloningStartBuddedCloneTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(CloningStartBuddedCloneTranslationPatch), nameof(CloningStartBuddedCloneTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyCloningStartBuddedCloneTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static object StartBuddedCloneQueue(object original, object clone)
        {
            _ = original;
            _ = clone;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return clone;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static object StartBuddedClonePopup(object original, object clone)
        {
            _ = original;
            _ = clone;
            DummyPopupShow.Show(PopupMessageToShow);
            return clone;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
            PopupMessageToShow = string.Empty;
        }
    }
}
