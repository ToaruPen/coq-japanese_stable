using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

#pragma warning disable S2094 // Empty dummy types are marker parameter types for Harmony target signatures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsButcherableCyberneticTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(RepositoryMessagePatternPath());
        DummyMessageQueue.Reset();
        DummyCyberneticsButcherableCyberneticTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        DummyCyberneticsButcherableCyberneticTarget.Reset();
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "{{g|You butcher a サイバネティック from the 死体.}}",
        "{{g|死体からサイバネティックを解体した}}")]
    [TestCase(
        "{{r|You rip a サイバネティック out of the 死体, but destroy it in the process.}}",
        "{{r|死体からサイバネティックを引き抜いたが、その過程で壊してしまった}}")]
    public void AttemptButcher_TranslatesCyberneticButcherMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            DummyCyberneticsButcherableCyberneticTarget.MessageToSend = source;

            _ = DummyCyberneticsButcherableCyberneticTarget.AttemptButcher(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(QueueHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void AttemptButcher_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "{{g|You butcher a サイバネティック from the 死体.}}";

        WithPatchedQueueOnly(() => DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
            Assert.That(QueueHitCount(), Is.Zero);
        });
    }

    [Test]
    public void AttemptButcher_StripsDirectMarkedTraffic_WhenOwnerAbsent()
    {
        const string source = "{{g|既に翻訳済み}}";

        WithPatchedQueueOnly(() => DummyMessageQueue.AddPlayerMessage(
            MessageFrameTranslator.MarkDirectTranslation(source),
            "white",
            Capitalize: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
            Assert.That(QueueHitCount(), Is.Zero);
        });
    }

    [Test]
    public void AttemptButcher_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        const string source = "{{g|You butcher a サイバネティック from the 死体.}}";

        WithPatchedOwnerAndQueue(() =>
        {
            DummyCyberneticsButcherableCyberneticTarget.MessageToSend =
                MessageFrameTranslator.MarkDirectTranslation(source);

            _ = DummyCyberneticsButcherableCyberneticTarget.AttemptButcher(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount(), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You carefully remove a サイバネティック from the 死体.")]
    [TestCase("The corpse contains no cybernetics.")]
    public void AttemptButcher_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            DummyCyberneticsButcherableCyberneticTarget.MessageToSend = source;

            _ = DummyCyberneticsButcherableCyberneticTarget.AttemptButcher(new DummyGameObject());

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        });
    }

    private static void WithPatchedOwnerAndQueue(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedQueueOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        var target = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireOwnerMethod(),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CyberneticsButcherableCyberneticTranslationPatch),
                nameof(CyberneticsButcherableCyberneticTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(CyberneticsButcherableCyberneticTranslationPatch),
                nameof(CyberneticsButcherableCyberneticTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyCyberneticsButcherableCyberneticTarget),
            nameof(DummyCyberneticsButcherableCyberneticTarget.AttemptButcher),
            typeof(DummyGameObject),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(DummyCell),
            typeof(List<DummyGameObject>));
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

    private static int QueueHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(CyberneticsButcherableCyberneticTranslationPatch) + ".DoesVerb");
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.cybernetics-butcherable-cybernetic-l2." + Guid.NewGuid().ToString("N");
    }

    private static string RepositoryMessageFramePath()
    {
        return Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");
    }

    private static string RepositoryMessagePatternPath()
    {
        return Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "messages.ja.json");
    }

    private static class DummyCyberneticsButcherableCyberneticTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool AttemptButcher(
            DummyGameObject who,
            bool automatic = false,
            bool skipSkill = false,
            bool intoInventory = false,
            int chanceMod = 0,
            DummyCell? fromCell = null,
            List<DummyGameObject>? tracking = null)
        {
            _ = who;
            _ = automatic;
            _ = skipSkill;
            _ = intoInventory;
            _ = chanceMod;
            _ = fromCell;
            _ = tracking;

            DummyMessageQueue.AddPlayerMessage(MessageToSend, "white", Capitalize: false);
            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
        }
    }

    private sealed class DummyCell
    {
    }
}
