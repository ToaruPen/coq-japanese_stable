using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BrainOwnerTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        DummyBrainOwnerTarget.StaticPopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [Test]
    public void Think_TranslatesQueuedThought_WhenOwnerPatched()
    {
        WithPatchedQueueOwner(
            typeof(BrainThinkTranslationPatch),
            RequireBrainMethod(nameof(DummyBrainOwnerTarget.Think), typeof(string)),
            () =>
            {
                var target = new DummyBrainOwnerTarget
                {
                    MessageToSend = "snapjaw thinks: 'kill the intruder'",
                };

                target.Think("kill the intruder");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjawは考える:「kill the intruder」"));
                    Assert.That(QueueHitCount(nameof(BrainThinkTranslationPatch), "Think"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Think_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        WithPatchedQueueOnly(() => DummyMessageQueue.AddPlayerMessage("snapjaw thinks: 'kill the intruder'", null, Capitalize: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjaw thinks: 'kill the intruder'"));
            Assert.That(QueueHitCount(nameof(BrainThinkTranslationPatch), "Think"), Is.Zero);
        });
    }

    [Test]
    public void Think_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        WithPatchedQueueOwner(
            typeof(BrainThinkTranslationPatch),
            RequireBrainMethod(nameof(DummyBrainOwnerTarget.Think), typeof(string)),
            () =>
            {
                var target = new DummyBrainOwnerTarget
                {
                    MessageToSend = MessageFrameTranslator.MarkDirectTranslation("snapjaw thinks: 'kill the intruder'"),
                };

                target.Think("kill the intruder");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("snapjaw thinks: 'kill the intruder'"));
                    Assert.That(QueueHitCount(nameof(BrainThinkTranslationPatch), "Think"), Is.Zero);
                });
            });
    }

    [Test]
    public void Think_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched()
    {
        WithPatchedQueueOwner(
            typeof(BrainThinkTranslationPatch),
            RequireBrainMethod(nameof(DummyBrainOwnerTarget.Think), typeof(string)),
            () =>
            {
                var target = new DummyBrainOwnerTarget();

                target.Think(string.Empty);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
                    Assert.That(QueueHitCount(nameof(BrainThinkTranslationPatch), "Think"), Is.Zero);
                });
            });
    }

    [Test]
    public void WriteFeelingSamples_TranslatesPopup_WhenOwnerPatched()
    {
        WithPatchedPopupOwner(
            typeof(BrainWriteFeelingSamplesPopupTranslationPatch),
            RequireBrainMethod(nameof(DummyBrainOwnerTarget.WriteFeelingSamples), typeof(bool)),
            () =>
            {
                DummyBrainOwnerTarget.StaticPopupMessageToShow = "42 feelings written to AllFeelings.txt in /tmp/qud!";

                DummyBrainOwnerTarget.WriteFeelingSamples();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("/tmp/qudのAllFeelings.txtに42件の感情を書き出した！"));
                    Assert.That(PopupHitCount("WriteFeelingSamples"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void WriteFeelingSamples_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() => DummyPopupShow.Show("42 feelings written to AllFeelings.txt in /tmp/qud!"));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("42 feelings written to AllFeelings.txt in /tmp/qud!"));
            Assert.That(PopupHitCount("WriteFeelingSamples"), Is.Zero);
        });
    }

    [Test]
    public void WriteFeelingSamples_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        WithPatchedPopupOwner(
            typeof(BrainWriteFeelingSamplesPopupTranslationPatch),
            RequireBrainMethod(nameof(DummyBrainOwnerTarget.WriteFeelingSamples), typeof(bool)),
            () =>
            {
                const string source = "42 feelings written to AllFeelings.txt in /tmp/qud!";
                DummyBrainOwnerTarget.StaticPopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source);

                DummyBrainOwnerTarget.WriteFeelingSamples();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("WriteFeelingSamples"), Is.Zero);
                });
            });
    }

    [Test]
    public void WriteFeelingSamples_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedPopupOwner(
            typeof(BrainWriteFeelingSamplesPopupTranslationPatch),
            RequireBrainMethod(nameof(DummyBrainOwnerTarget.WriteFeelingSamples), typeof(bool)),
            () =>
            {
                DummyBrainOwnerTarget.WriteFeelingSamples();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
                    Assert.That(PopupHitCount("WriteFeelingSamples"), Is.Zero);
                });
            });
    }

    private static void WithPatchedQueueOwner(Type patchType, MethodInfo ownerMethod, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, patchType, ownerMethod);
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

    private static void WithPatchedPopupOwner(Type patchType, MethodInfo ownerMethod, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, patchType, ownerMethod);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, Type patchType, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(patchType, "Prefix")),
            finalizer: new HarmonyMethod(RequireMethod(patchType, "Finalizer", typeof(Exception))));
    }

    private static MethodInfo RequireBrainMethod(string methodName, params Type[] parameterTypes)
    {
        return RequireMethod(typeof(DummyBrainOwnerTarget), methodName, parameterTypes);
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

    private static int QueueHitCount(string patchName, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(patchName, detail);
    }

    private static int PopupHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(BrainWriteFeelingSamplesPopupTranslationPatch) + "." + detail);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
