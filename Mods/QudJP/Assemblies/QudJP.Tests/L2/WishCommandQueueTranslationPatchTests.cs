using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // Helper aliases intentionally share implementation to keep test cases readable.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WishCommandQueueTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DynamicTextObservability.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase(
        nameof(DummyWishCommandProducerTarget.SlynthQuestWish),
        "No faction found by that name.",
        "その名前の派閥は見つからなかった。",
        "SlynthQuestNoFaction")]
    [TestCase(
        nameof(DummyWishCommandProducerTarget.WishTimer),
        "Turns until nephal arrives: 42",
        "ネファル到着までのターン数: 42",
        "ReclamationWishTimer")]
    [TestCase(
        nameof(DummyWishCommandProducerTarget.ClearStatShifts),
        "Clearing player body stat shifts...",
        "プレイヤー身体の能力値補正を消去中...",
        "ClearStatShifts")]
    [TestCase(
        nameof(DummyWishCommandProducerTarget.DynamicQuestWhere),
        "quest in JoppaWorld.10.22.1.1.10 secret id is secret-site-1 for quest Find the Ruin",
        "クエスト Find the Ruin の場所は JoppaWorld.10.22.1.1.10、秘密IDは secret-site-1。",
        "FindASiteDynamicQuestWhere")]
    public void WishCommandQueue_TranslatesOwnerMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerQueuedMessage(methodName, source, expected, detail);
    }

    [Test]
    public void WishCommandQueue_PreservesQueuedMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            nameof(DummyWishCommandProducerTarget.WishTimer),
            "Turns until nephal arrives: 42",
            "ネファル到着までのターン数: 42",
            "ReclamationWishTimer",
            color: "white");
    }

    [Test]
    public void WishCommandQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Turns until nephal arrives: 42";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("white"));
                Assert.That(HitCount("ReclamationWishTimer"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void WishCommandQueue_DoesNotTranslateDynamicQuestWhereTraffic_WhenOwnerAbsent()
    {
        const string source = "quest in JoppaWorld.10.22.1.1.10 secret id is secret-site-1 for quest Find the Ruin";

        AssertOwnerQueuedMessageWithoutOwner(
            nameof(DummyWishCommandProducerTarget.DynamicQuestWhere),
            source,
            source,
            "FindASiteDynamicQuestWhere");
    }

    [Test]
    public void WishCommandQueue_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "Turns until nephal arrives: 42";

        AssertOwnerQueuedMessage(
            nameof(DummyWishCommandProducerTarget.WishTimer),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "ReclamationWishTimer",
            expectedHits: 0);
    }

    [TestCase(
        "\u0001quest in JoppaWorld.10.22.1.1.10 secret id is secret-site-1 for quest Find the Ruin",
        "quest in JoppaWorld.10.22.1.1.10 secret id is secret-site-1 for quest Find the Ruin",
        0)]
    [TestCase("", "", 0)]
    [TestCase(
        "quest in {{Y|JoppaWorld.10.22.1.1.10}} secret id is {{C|secret-site-1}} for quest Find the Ruin",
        "クエスト Find the Ruin の場所は {{Y|JoppaWorld.10.22.1.1.10}}、秘密IDは {{C|secret-site-1}}。",
        1)]
    [TestCase("quest whereabouts are unknown", "quest whereabouts are unknown", 0)]
    public void WishCommandQueue_DynamicQuestWhere_HandlesFallbackAndEdgeCases_WhenOwnerPatched(
        string source,
        string expected,
        int expectedHits)
    {
        AssertOwnerQueuedMessage(
            nameof(DummyWishCommandProducerTarget.DynamicQuestWhere),
            source,
            expected,
            "FindASiteDynamicQuestWhere",
            expectedHits: expectedHits);
    }

    [TestCase("")]
    [TestCase("Turns until nephal arrives soon.")]
    [TestCase("No faction found.")]
    [TestCase("Clearing stat shifts...")]
    public void WishCommandQueue_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerQueuedMessage(
            nameof(DummyWishCommandProducerTarget.WishTimer),
            source,
            source,
            "ReclamationWishTimer",
            expectedHits: 0);
    }

    private static void AssertOwnerQueuedMessage(
        string methodName,
        string source,
        string expected,
        string detail,
        string? color = null,
        int expectedHits = 1)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, methodName);

            DummyWishCommandProducerTarget.MessageToSend = source;
            DummyWishCommandProducerTarget.ColorToSend = color;
            InvokeOwner(methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            DummyWishCommandProducerTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertOwnerQueuedMessageWithoutOwner(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyWishCommandProducerTarget.MessageToSend = source;
            InvokeOwner(methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.Zero);
            });
        }
        finally
        {
            DummyWishCommandProducerTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void InvokeOwner(string methodName)
    {
        _ = methodName switch
        {
            nameof(DummyWishCommandProducerTarget.SlynthQuestWish) => DummyWishCommandProducerTarget.SlynthQuestWish("missing"),
            nameof(DummyWishCommandProducerTarget.WishTimer) => DummyWishCommandProducerTarget.WishTimer(),
            nameof(DummyWishCommandProducerTarget.ClearStatShifts) => new DummyWishCommandProducerTarget().ClearStatShifts(),
            nameof(DummyWishCommandProducerTarget.DynamicQuestWhere) => DummyWishCommandProducerTarget.DynamicQuestWhere(),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown owner method."),
        };
    }

    private static void PatchQueue(Harmony harmony)
    {
        var original = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: RequireOwnerMethod(methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(WishCommandQueueTranslationPatch), nameof(WishCommandQueueTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(WishCommandQueueTranslationPatch), nameof(WishCommandQueueTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName == nameof(DummyWishCommandProducerTarget.SlynthQuestWish)
            ? RequireMethod(typeof(DummyWishCommandProducerTarget), methodName, typeof(string))
            : RequireMethod(typeof(DummyWishCommandProducerTarget), methodName);
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

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(WishCommandQueueTranslationPatch) + "." + detail);
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private sealed class DummyWishCommandProducerTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool SlynthQuestWish(string value)
        {
            _ = value;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool WishTimer()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ClearStatShifts()
        {
            _ = GetHashCode();
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool DynamicQuestWhere()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
