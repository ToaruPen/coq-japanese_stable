using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PlayerDanceRitualTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase("&KPlayer steps {{C|北東}}", "&Kあなたは{{C|北東}}へ一歩進んだ。")]
    [TestCase("&KOpponent steps east", "&K相手はeastへ一歩進んだ。")]
    [TestCase("&GYou executed that step correctly! [{{Y|拍子が合った}}]", "&Gそのステップを正しく実行した！ [{{Y|拍子が合った}}]")]
    [TestCase("&RYou executed that step incorrectly! [{{R|早すぎた}}]", "&Rそのステップを誤って実行した！ [{{R|早すぎた}}]")]
    public void TryTranslateMessage_PreservesDynamicCaptures(string source, string expected)
    {
        var translated = PlayerDanceRitualTranslationPatch.TryTranslateMessage(
            source,
            nameof(PlayerDanceRitualTranslationPatch),
            "PlayerDanceRitual.Queue",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [TestCase("The dance ended in failure! [{{R|相手が倒れた}}]", "踊りは失敗に終わった！ [{{R|相手が倒れた}}]")]
    [TestCase("{{G|The dance ended in success! [Your opponent perished!]}}", "{{G|踊りは成功に終わった！ [Your opponent perished!]}}")]
    public void TryTranslatePopup_PreservesDynamicReasonCaptures(string source, string expected)
    {
        var translated = PlayerDanceRitualTranslationPatch.TryTranslatePopup(
            source,
            nameof(PopupShowTranslationPatch),
            "Popup.Show.PlayerDanceRitual",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateMessage_ReturnsFalseForDirectMarkerAndEmptyInput()
    {
        var marked = MessageFrameTranslator.MarkDirectTranslation("&KPlayer steps east");

        Assert.Multiple(() =>
        {
            Assert.That(
                PlayerDanceRitualTranslationPatch.TryTranslateMessage(
                    marked,
                    nameof(PlayerDanceRitualTranslationPatch),
                    "PlayerDanceRitual.Queue",
                    out var markedResult),
                Is.False);
            Assert.That(markedResult, Is.EqualTo(marked));
            Assert.That(
                PlayerDanceRitualTranslationPatch.TryTranslateMessage(
                    string.Empty,
                    nameof(PlayerDanceRitualTranslationPatch),
                    "PlayerDanceRitual.Queue",
                    out var emptyResult),
                Is.False);
            Assert.That(emptyResult, Is.Empty);
        });
    }

    [Test]
    public void PlayerDanceRitualPatch_TranslatesExecuteMoveQueuedMessage_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget
        {
            QueuedMessageToSend = "&KPlayer steps {{C|北東}}",
        };

        RunQueuedOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.ExecuteMove),
            new object[] { "Player", "北東" },
            target,
            "&Kあなたは{{C|北東}}へ一歩進んだ。");
    }

    [Test]
    public void PlayerDanceRitualPatch_TranslatesPassStepQueuedMessage_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget
        {
            QueuedMessageToSend = "&GYou executed that step correctly! [{{Y|拍子が合った}}]",
        };

        RunQueuedOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.PassStep),
            new object[] { "拍子が合った" },
            target,
            "&Gそのステップを正しく実行した！ [{{Y|拍子が合った}}]");
    }

    [Test]
    public void PlayerDanceRitualPatch_TranslatesFailStepQueuedMessage_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget
        {
            QueuedMessageToSend = "&RYou executed that step incorrectly! [{{R|早すぎた}}]",
        };

        RunQueuedOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.FailStep),
            new object[] { "早すぎた" },
            target,
            "&Rそのステップを誤って実行した！ [{{R|早すぎた}}]");
    }

    [Test]
    public void PlayerDanceRitualPatch_TranslatesFailDancePopup_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget
        {
            PopupMessageToShow = "The dance ended in failure! [{{R|相手が倒れた}}]",
        };

        RunPopupOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.FailDance),
            new object[] { "相手が倒れた" },
            target,
            "踊りは失敗に終わった！ [{{R|相手が倒れた}}]");
    }

    [Test]
    public void PlayerDanceRitualPatch_TranslatesSuccessDancePopup_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget
        {
            PopupMessageToShow = "{{G|The dance ended in success! [Your opponent perished!]}}",
        };

        RunPopupOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.SuccessDance),
            new object[] { "Your opponent perished!" },
            target,
            "{{G|踊りは成功に終わった！ [Your opponent perished!]}}");
    }

    [Test]
    public void PlayerDanceRitualPatch_DoesNotTranslateQueuedMessage_WhenOwnerPatchIsAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchQueue(harmony);

            const string source = "&KPlayer steps north";
            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PlayerDanceRitualPatch_DoesNotTranslatePopup_WhenOwnerPatchIsAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            const string source = "The dance ended in success! [Your opponent perished!]";
            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PlayerDanceRitualPatch_DoesNotTranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget
        {
            QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation("&KPlayer steps east"),
        };

        RunQueuedOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.ExecuteMove),
            new object[] { "Player", "east" },
            target,
            target.QueuedMessageToSend);
    }

    [Test]
    public void PlayerDanceRitualPatch_DoesNotTranslateEmptyQueuedMessage_WhenOwnerPatched()
    {
        var target = new DummyPlayerDanceRitualProducerTarget();

        RunQueuedOwnerTest(
            nameof(DummyPlayerDanceRitualProducerTarget.PassStep),
            new object[] { string.Empty },
            target,
            string.Empty);
    }

    private static void RunQueuedOwnerTest(
        string methodName,
        object[] methodArgs,
        DummyPlayerDanceRitualProducerTarget target,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var parameterTypes = methodArgs.Select(static arg => arg.GetType()).ToArray();
            var ownerMethod = RequireMethod(typeof(DummyPlayerDanceRitualProducerTarget), methodName, parameterTypes);

            PatchQueue(harmony);
            PatchOwner(harmony, ownerMethod);

            ownerMethod.Invoke(target, methodArgs);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunPopupOwnerTest(
        string methodName,
        object[] methodArgs,
        DummyPlayerDanceRitualProducerTarget target,
        string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var parameterTypes = methodArgs.Select(static arg => arg.GetType()).ToArray();
            var ownerMethod = RequireMethod(typeof(DummyPlayerDanceRitualProducerTarget), methodName, parameterTypes);

            PatchPopupShow(harmony);
            PatchOwner(harmony, ownerMethod);

            ownerMethod.Invoke(target, methodArgs);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix), typeof(string).MakeByRefType())));
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(PlayerDanceRitualTranslationPatch), nameof(PlayerDanceRitualTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PlayerDanceRitualTranslationPatch), nameof(PlayerDanceRitualTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
