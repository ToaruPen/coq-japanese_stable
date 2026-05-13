using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FireSuppressionDischargeTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        Translator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression),
        "2 drams of {{C|gel}} discharges all over you.",
        "{{C|gel}} 2ドラムがあなたの全身に放出された。",
        "FireSuppressionSelf")]
    [TestCase(
        nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression),
        "1 dram of {{C|gel}} discharges all over the snapjaw.",
        "{{C|gel}} 1ドラムがsnapjawの全身に放出された。",
        "FireSuppressionTarget")]
    [TestCase(
        nameof(DummyFireSuppressionDischargeProducer.TurnTick),
        "Your {{Y|fire suppression system}} discharges 2 drams of {{C|gel}} all over you.",
        "あなたの{{Y|fire suppression system}}が{{C|gel}} 2ドラムをあなたの全身に放出した。",
        "CyberneticsSelf")]
    [TestCase(
        nameof(DummyFireSuppressionDischargeProducer.TurnTick),
        "{{G|snapjaw}}'s {{Y|fire suppression system}} discharges 1 dram of {{C|gel}} all over it.",
        "{{G|snapjaw}}の{{Y|fire suppression system}}が{{C|gel}} 1ドラムをそれの全身に放出した。",
        "CyberneticsTarget")]
    public void Patch_TranslatesFireSuppressionDischargeMessages_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndQueue(methodName, () =>
        {
            var target = new DummyFireSuppressionDischargeProducer
            {
                QueuedMessageToSend = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                AssertMarkedDirectTranslation(DummyMessageQueue.LastMessage, expected);
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslateFireSuppressionDischargeMessage_WhenOwnerAbsent()
    {
        WithPatchedQueueOnly(() =>
        {
            DummyMessageQueue.AddPlayerMessage("2 drams of {{C|gel}} discharges all over you.", null, Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("2 drams of {{C|gel}} discharges all over you."));
                Assert.That(HitCount("FireSuppressionSelf"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation("2 drams of {{C|gel}} discharges all over you.");

        WithPatchedOwnerAndQueue(
            nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression),
            () =>
            {
                var target = new DummyFireSuppressionDischargeProducer
                {
                    QueuedMessageToSend = source,
                };

                target.CheckFireSuppression(null);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("2 drams of {{C|gel}} discharges all over you."));
                    Assert.That(HitCount("FireSuppressionSelf"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesUnknownMessageUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwnerAndQueue(
            nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression),
            () =>
            {
                var target = new DummyFireSuppressionDischargeProducer
                {
                    QueuedMessageToSend = "The fire suppression system sputters.",
                };

                target.CheckFireSuppression(null);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("The fire suppression system sputters."));
                    Assert.That(HitCount("FireSuppressionSelf"), Is.Zero);
                    Assert.That(HitCount("FireSuppressionTarget"), Is.Zero);
                    Assert.That(HitCount("CyberneticsSelf"), Is.Zero);
                    Assert.That(HitCount("CyberneticsTarget"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwnerAndQueue(
            nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression),
            () =>
            {
                var target = new DummyFireSuppressionDischargeProducer
                {
                    QueuedMessageToSend = string.Empty,
                };

                target.CheckFireSuppression(null);

                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(string.Empty));
            });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerMessage()
    {
        WithPatchedOwnerAndQueue(
            [
                nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression),
                nameof(DummyFireSuppressionDischargeProducer.TurnTick),
            ],
            () =>
            {
                var innerTarget = new DummyFireSuppressionDischargeProducer
                {
                    QueuedMessageToSend = "2 drams of {{C|gel}} discharges all over you.",
                };
                var outerTarget = new DummyFireSuppressionDischargeProducer
                {
                    QueuedMessageToSend = "Your {{Y|fire suppression system}} discharges 2 drams of {{C|gel}} all over you.",
                    BeforeQueue = () =>
                    {
                        innerTarget.CheckFireSuppression(null);

                        Assert.Multiple(() =>
                        {
                            AssertMarkedDirectTranslation(DummyMessageQueue.LastMessage, "{{C|gel}} 2ドラムがあなたの全身に放出された。");
                            Assert.That(HitCount("FireSuppressionSelf"), Is.EqualTo(1));
                            Assert.That(HitCount("CyberneticsSelf"), Is.Zero);
                        });
                    },
                };

                outerTarget.TurnTick(1, 1);

                Assert.Multiple(() =>
                {
                    AssertMarkedDirectTranslation(
                        DummyMessageQueue.LastMessage,
                        "あなたの{{Y|fire suppression system}}が{{C|gel}} 2ドラムをあなたの全身に放出した。");
                    Assert.That(HitCount("FireSuppressionSelf"), Is.EqualTo(1));
                    Assert.That(HitCount("CyberneticsSelf"), Is.EqualTo(1));
                });
            });
    }

    private static void WithPatchedOwnerAndQueue(string methodName, Action action)
    {
        WithPatchedOwnerAndQueue([methodName], action);
    }

    private static void WithPatchedOwnerAndQueue(string[] methodNames, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            foreach (var methodName in methodNames)
            {
                harmony.Patch(
                    original: RequireOwnerMethod(methodName),
                    prefix: new HarmonyMethod(RequireMethod(typeof(FireSuppressionDischargeTranslationPatch), nameof(FireSuppressionDischargeTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(FireSuppressionDischargeTranslationPatch), nameof(FireSuppressionDischargeTranslationPatch.Finalizer), typeof(Exception))));
            }

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedQueueOnly(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
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
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void InvokeOwnerMethod(DummyFireSuppressionDischargeProducer target, string methodName)
    {
        if (string.Equals(methodName, nameof(DummyFireSuppressionDischargeProducer.TurnTick), StringComparison.Ordinal))
        {
            target.TurnTick(1, 1);
            return;
        }

        target.CheckFireSuppression(null);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(FireSuppressionDischargeTranslationPatch) + "." + detail);
    }

    private static void AssertMarkedDirectTranslation(string? actual, string expected)
    {
        Assert.That(actual, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(expected)));
        Assert.That(MessageFrameTranslator.TryStripDirectTranslationMarker(actual!, out var stripped), Is.True);
        Assert.That(stripped, Is.EqualTo(expected));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummyFireSuppressionDischargeProducer.CheckFireSuppression) => RequireMethod(
                typeof(DummyFireSuppressionDischargeProducer),
                methodName,
                typeof(DummyGameObject)),
            nameof(DummyFireSuppressionDischargeProducer.TurnTick) => RequireMethod(
                typeof(DummyFireSuppressionDischargeProducer),
                methodName,
                typeof(long),
                typeof(int)),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
        };
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                   null,
                   parameters,
                   null)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyFireSuppressionDischargeProducer
    {
        public string QueuedMessageToSend { get; set; } = string.Empty;

        public Action? BeforeQueue { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool CheckFireSuppression(DummyGameObject? obj)
        {
            _ = obj;
            BeforeQueue?.Invoke();
            DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TurnTick(long timeTick, int amount)
        {
            _ = timeTick;
            _ = amount;
            BeforeQueue?.Invoke();
            DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        }
    }
}
