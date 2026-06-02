using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AutomatedExternalDefibrillatorTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You don't know how to use {{Y|defibrillator}}.",
        "あなたは{{Y|除細動器}}の使い方を知らない。",
        "Defibrillator.NoSkill")]
    [TestCase(
        "{{Y|defibrillator}} is {{K|unpowered}}.",
        "{{Y|除細動器}}は{{K|無電力だ}}。",
        "Defibrillator.Status")]
    [TestCase(
        "There is no one there to use {{Y|defibrillator}} on.",
        "そこには{{Y|除細動器}}を使う相手がいない。",
        "Defibrillator.NoTarget")]
    [TestCase(
        "There is no one there you can use {{Y|defibrillator}} on.",
        "そこには{{Y|除細動器}}を使える相手がいない。",
        "Defibrillator.NoUsableTarget")]
    public void AttemptDefibrillate_TranslatesFailureMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        using var ownerPatch = PatchOwner();
        using var queuePatch = PatchQueue();
        var target = new DummyAutomatedExternalDefibrillatorTarget
        {
            MessageToQueue = source,
        };

        target.AttemptDefibrillateQueuedMessage();

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            Assert.That(DefibrillatorHitCount(detail), Is.EqualTo(1));
        });
    }

    [TestCase(
        "{{Y|snapjaw}} is not in cardiac arrest. Do you want to use {{Y|defibrillator}} on it anyway?",
        "{{Y|スナップジョー}}は心停止状態ではない。それでも{{Y|除細動器}}を使いますか？",
        "Defibrillator.TargetConfirm")]
    [TestCase(
        "You are not in cardiac arrest. Do you want to use {{Y|defibrillator}} on yourself anyway?",
        "あなたは心停止状態ではない。それでも{{Y|除細動器}}を自分自身に使いますか？",
        "Defibrillator.SelfConfirm")]
    [TestCase(
        "You are not in cardiac arrest. Do you want to use {{Y|defibrillator}} on {{Y|yourself}} anyway?",
        "あなたは心停止状態ではない。それでも{{Y|除細動器}}を{{Y|自分自身}}に使いますか？",
        "Defibrillator.SelfConfirm")]
    public void AttemptDefibrillate_TranslatesConfirmationPopup_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        using var ownerPatch = PatchOwner();
        using var popupPatch = PatchPopupShowYesNo();
        var target = new DummyAutomatedExternalDefibrillatorTarget
        {
            PopupMessageToShow = source,
        };

        target.AttemptDefibrillateConfirmationPopup();

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
            Assert.That(DefibrillatorHitCount(detail), Is.EqualTo(1));
        });
    }

    [Test]
    public void AttemptDefibrillate_LeavesUnknownAndDirectMarkedTextUnchanged()
    {
        using var ownerPatch = PatchOwner();
        using var popupPatch = PatchPopupShowYesNo();
        var target = new DummyAutomatedExternalDefibrillatorTarget();

        target.PopupMessageToShow = "Unknown defibrillator text.";
        target.AttemptDefibrillateConfirmationPopup();
        var unknown = DummyPopupShow.LastShowYesNoMessage;

        target.PopupMessageToShow = "{{Y|Unknown defibrillator text.}}";
        target.AttemptDefibrillateConfirmationPopup();
        var coloredUnknown = DummyPopupShow.LastShowYesNoMessage;

        target.PopupMessageToShow = string.Empty;
        target.AttemptDefibrillateConfirmationPopup();
        var empty = DummyPopupShow.LastShowYesNoMessage;

        target.PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("翻訳済み");
        target.AttemptDefibrillateConfirmationPopup();
        var marked = DummyPopupShow.LastShowYesNoMessage;

        target.PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("{{Y|翻訳済み}}");
        target.AttemptDefibrillateConfirmationPopup();
        var coloredMarked = DummyPopupShow.LastShowYesNoMessage;

        Assert.Multiple(() =>
        {
            Assert.That(unknown, Is.EqualTo("Unknown defibrillator text."));
            Assert.That(coloredUnknown, Is.EqualTo("{{Y|Unknown defibrillator text.}}"));
            Assert.That(empty, Is.Empty);
            Assert.That(marked, Is.EqualTo("翻訳済み"));
            Assert.That(coloredMarked, Is.EqualTo("{{Y|翻訳済み}}"));
            Assert.That(DefibrillatorHitCount("Defibrillator.TargetConfirm"), Is.Zero);
            Assert.That(DefibrillatorHitCount("Defibrillator.SelfConfirm"), Is.Zero);
        });
    }

    [Test]
    public void AttemptDefibrillate_QueuePathLeavesUnknownAndDirectMarkedTextUnchanged()
    {
        using var ownerPatch = PatchOwner();
        using var queuePatch = PatchQueue();
        var target = new DummyAutomatedExternalDefibrillatorTarget();

        target.MessageToQueue = "Unknown defibrillator text.";
        target.AttemptDefibrillateQueuedMessage();
        var unknown = DummyMessageQueue.LastMessage;

        target.MessageToQueue = "{{Y|Unknown defibrillator text.}}";
        target.AttemptDefibrillateQueuedMessage();
        var coloredUnknown = DummyMessageQueue.LastMessage;

        target.MessageToQueue = string.Empty;
        target.AttemptDefibrillateQueuedMessage();
        var empty = DummyMessageQueue.LastMessage;

        target.MessageToQueue = MessageFrameTranslator.MarkDirectTranslation("翻訳済み");
        target.AttemptDefibrillateQueuedMessage();
        var marked = DummyMessageQueue.LastMessage;

        target.MessageToQueue = MessageFrameTranslator.MarkDirectTranslation("{{Y|翻訳済み}}");
        target.AttemptDefibrillateQueuedMessage();
        var coloredMarked = DummyMessageQueue.LastMessage;

        Assert.Multiple(() =>
        {
            Assert.That(unknown, Is.EqualTo("Unknown defibrillator text."));
            Assert.That(coloredUnknown, Is.EqualTo("{{Y|Unknown defibrillator text.}}"));
            Assert.That(empty, Is.Empty);
            Assert.That(marked, Is.EqualTo("翻訳済み"));
            Assert.That(coloredMarked, Is.EqualTo("{{Y|翻訳済み}}"));
            Assert.That(DefibrillatorHitCount("Defibrillator.NoSkill"), Is.Zero);
            Assert.That(DefibrillatorHitCount("Defibrillator.Status"), Is.Zero);
            Assert.That(DefibrillatorHitCount("Defibrillator.NoTarget"), Is.Zero);
            Assert.That(DefibrillatorHitCount("Defibrillator.NoUsableTarget"), Is.Zero);
        });
    }

    private static IDisposable PatchOwner()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyAutomatedExternalDefibrillatorTarget),
                nameof(DummyAutomatedExternalDefibrillatorTarget.AttemptDefibrillateQueuedMessage)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(AutomatedExternalDefibrillatorTranslationPatch),
                nameof(AutomatedExternalDefibrillatorTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(AutomatedExternalDefibrillatorTranslationPatch),
                nameof(AutomatedExternalDefibrillatorTranslationPatch.Finalizer))));
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyAutomatedExternalDefibrillatorTarget),
                nameof(DummyAutomatedExternalDefibrillatorTarget.AttemptDefibrillateConfirmationPopup)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(AutomatedExternalDefibrillatorTranslationPatch),
                nameof(AutomatedExternalDefibrillatorTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(AutomatedExternalDefibrillatorTranslationPatch),
                nameof(AutomatedExternalDefibrillatorTranslationPatch.Finalizer))));
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

    private static IDisposable PatchPopupShowYesNo()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters.Length == 0 ? null : parameters)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static int DefibrillatorHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(CombatAndLogMessageQueuePatch),
                "MessageQueue." + nameof(AutomatedExternalDefibrillatorTranslationPatch) + "." + detail)
            + DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(AutomatedExternalDefibrillatorTranslationPatch) + "." + detail);
    }

    private sealed class DummyAutomatedExternalDefibrillatorTarget
    {
        public string MessageToQueue { get; set; } = string.Empty;

        public string PopupMessageToShow { get; set; } = string.Empty;

        public void AttemptDefibrillateQueuedMessage()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToQueue);
        }

        public void AttemptDefibrillateConfirmationPopup()
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
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
