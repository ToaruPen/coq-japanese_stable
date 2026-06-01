using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DominationProcessTargetTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DummyMessageQueue.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "There seems to be no mind in {{Y|the turret}} to dominate.",
        "{{Y|the turret}}には支配する心がないようだ。",
        "Domination.NoMind")]
    [TestCase(
        "You can't dominate yourself!",
        "自分自身は支配できない！",
        "Domination.SelfTarget")]
    [TestCase(
        "You can't dominate someone you are already dominating.",
        "すでに支配している相手は支配できない。",
        "Domination.AlreadyDominating")]
    [TestCase(
        "You can't do that.",
        "それはできない。",
        "Domination.CannotDoThat")]
    [TestCase(
        "{{Y|the turret}} does not have a consciousness you can make psychic contact with.",
        "{{Y|the turret}}には精神接触できる意識がない。",
        "Domination.NoConsciousness")]
    [TestCase(
        "Nothing happens.",
        "何も起こらない。",
        "Domination.NothingHappens")]
    public void ProcessTarget_TranslatesFailureMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        using var ownerPatch = PatchOwner();
        using var queuePatch = PatchQueue();
        var target = new DummyDominationProcessTarget
        {
            MessageToQueue = source,
        };

        target.ProcessTargetQueuedMessage();

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            Assert.That(HitCount(detail), Is.EqualTo(1));
        });
    }

    [Test]
    public void ProcessTarget_LeavesUnknownAndDirectMarkedTextUnchanged()
    {
        DominationProcessTargetTranslationPatch.Prefix();
        try
        {
            Assert.Multiple(() =>
            {
                var unknown = "Unknown domination failure.";
                Assert.That(
                    DominationProcessTargetTranslationPatch.TryTranslateQueuedMessage(
                        ref unknown,
                        null),
                    Is.False);
                Assert.That(unknown, Is.EqualTo("Unknown domination failure."));

                var marked = MessageFrameTranslator.MarkDirectTranslation("翻訳済み");
                Assert.That(
                    DominationProcessTargetTranslationPatch.TryTranslateQueuedMessage(
                        ref marked,
                        null),
                    Is.True);
                Assert.That(marked, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation("翻訳済み")));

                var empty = string.Empty;
                Assert.That(
                    DominationProcessTargetTranslationPatch.TryTranslateQueuedMessage(
                        ref empty,
                        null),
                    Is.False);
                Assert.That(empty, Is.Empty);
            });
        }
        finally
        {
            _ = DominationProcessTargetTranslationPatch.Finalizer(null);
        }
    }

    private static IDisposable PatchOwner()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyDominationProcessTarget),
                nameof(DummyDominationProcessTarget.ProcessTargetQueuedMessage)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(DominationProcessTargetTranslationPatch),
                nameof(DominationProcessTargetTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(DominationProcessTargetTranslationPatch),
                nameof(DominationProcessTargetTranslationPatch.Finalizer))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static IDisposable PatchQueue()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters.Length == 0 ? null : parameters)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(CombatAndLogMessageQueuePatch),
            "MessageQueue." + nameof(DominationProcessTargetTranslationPatch) + "." + detail);
    }

    private sealed class DummyDominationProcessTarget
    {
        public string MessageToQueue { get; init; } = string.Empty;

        public void ProcessTargetQueuedMessage()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToQueue);
        }
    }

    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
