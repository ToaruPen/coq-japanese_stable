using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TonicApplicatorTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase(
        nameof(DummyTonicApplicatorTarget.LoveTonicFireEvent),
        "The {{Y|snapjaw}} looks you over and metabolizes the love tonic with no effect.",
        "{{Y|snapjaw}}はあなたをじろじろ見てからラブトニックを代謝したが、効果はなかった。")]
    [TestCase(
        nameof(DummyTonicApplicatorTarget.LoveTonicFireEvent),
        "{{Y|glowfish}} look you over and metabolize the love tonic with no effect.",
        "{{Y|glowfish}}はあなたをじろじろ見てからラブトニックを代謝したが、効果はなかった。")]
    [TestCase(
        nameof(DummyTonicApplicatorTarget.SphynxSaltFireEvent),
        "The {{Y|snapjaw}} applies {{C|a sphynx salt injector}}.",
        "{{Y|snapjaw}}は{{C|a sphynx salt injector}}を使った。")]
    [TestCase(
        nameof(DummyTonicApplicatorTarget.SphynxSaltFireEvent),
        "{{Y|glowfish}} apply {{C|some sphynx salt injectors}}.",
        "{{Y|glowfish}}は{{C|some sphynx salt injectors}}を使った。")]
    public void TonicApplicator_TranslatesQueuedMessage_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertQueuedMessage(methodName, source, expected);
    }

    [Test]
    public void TonicApplicator_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|snapjaw}} applies {{C|a sphynx salt injector}}.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TonicApplicator_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyTonicApplicatorTarget.SphynxSaltFireEvent),
            MessageFrameTranslator.MarkDirectTranslation("翻訳済みのトニック適用メッセージ"),
            "翻訳済みのトニック適用メッセージ");
    }

    [TestCase("")]
    [TestCase("The snapjaw drinks the love tonic.")]
    [TestCase("The snapjaw applies a tonic and vanishes.")]
    public void TonicApplicator_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(nameof(DummyTonicApplicatorTarget.LoveTonicFireEvent), source, source);
        AssertQueuedMessage(nameof(DummyTonicApplicatorTarget.SphynxSaltFireEvent), source, source);
    }

    private static void AssertQueuedMessage(string methodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyTonicApplicatorTarget.MessageToSend = source;
            InvokeOwnerMethod(methodName);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyTonicApplicatorTarget.Reset();
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

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyTonicApplicatorTarget.LoveTonicFireEvent),
            nameof(DummyTonicApplicatorTarget.SphynxSaltFireEvent),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTonicApplicatorTarget), methodName, typeof(DummyEvent)),
                prefix: new HarmonyMethod(RequireMethod(typeof(TonicApplicatorTranslationPatch), nameof(TonicApplicatorTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(TonicApplicatorTranslationPatch), nameof(TonicApplicatorTranslationPatch.Finalizer), typeof(Exception))));
        }
    }

    private static void InvokeOwnerMethod(string methodName)
    {
        _ = RequireMethod(typeof(DummyTonicApplicatorTarget), methodName, typeof(DummyEvent))
            .Invoke(null, new object?[] { new DummyEvent() });
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

    private static class DummyTonicApplicatorTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool LoveTonicFireEvent(DummyEvent e)
        {
            _ = e;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, null, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool SphynxSaltFireEvent(DummyEvent e)
        {
            _ = e;
            _ = nameof(SphynxSaltFireEvent);
            DummyMessageQueue.AddPlayerMessage(MessageToSend, null, Capitalize: false);
            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
        }
    }
}
