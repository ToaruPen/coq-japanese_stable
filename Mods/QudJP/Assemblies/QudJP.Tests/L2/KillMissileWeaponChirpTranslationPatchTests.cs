using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class KillMissileWeaponChirpTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase("Something chirps here.", "ここで何かが鳴いた。")]
    [TestCase("Something chirps to the north.", "北側で何かが鳴いた。")]
    [TestCase("Something chirps to the southwest.", "南西側で何かが鳴いた。")]
    public void TryMissileWeapon_TranslatesAudibleChirpMessage_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerQueuedMessage(source, expected);
    }

    [Test]
    public void TryMissileWeapon_PreservesMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage("Something chirps to the east.", "東側で何かが鳴いた。", "Y");
    }

    [Test]
    public void TryMissileWeapon_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Something chirps to the north.";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "Y", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("Y"));
                Assert.That(HitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TryMissileWeapon_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched()
    {
        const string source = "Something chirps here.";

        AssertOwnerQueuedMessage(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [Test]
    public void TryMissileWeapon_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(string.Empty, string.Empty, expectedHits: 0);
    }

    [Test]
    public void TryMissileWeapon_LeavesUnsupportedDirectionUnchanged_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage("Something chirps beyond the horizon.", "Something chirps beyond the horizon.", expectedHits: 0);
    }

    private static void AssertOwnerQueuedMessage(
        string source,
        string expected,
        string? color = null,
        int expectedHits = 1)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony);

            var target = new DummyKillGoalHandlerTarget
            {
                MessageToSend = source,
                ColorToSend = color,
            };

            _ = target.TryMissileWeapon();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(HitCount(), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueue(Harmony harmony)
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
        harmony.Patch(
            original: RequireMethod(typeof(DummyKillGoalHandlerTarget), nameof(DummyKillGoalHandlerTarget.TryMissileWeapon)),
            prefix: new HarmonyMethod(RequireMethod(typeof(KillMissileWeaponChirpTranslationPatch), nameof(KillMissileWeaponChirpTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(KillMissileWeaponChirpTranslationPatch), nameof(KillMissileWeaponChirpTranslationPatch.Finalizer), typeof(Exception))));
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

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(KillMissileWeaponChirpTranslationPatch));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.kill-missile-chirp.{Guid.NewGuid():N}";
    }
}
