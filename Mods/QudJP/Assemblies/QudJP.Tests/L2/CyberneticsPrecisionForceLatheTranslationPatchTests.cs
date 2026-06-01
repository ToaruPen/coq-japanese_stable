using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsPrecisionForceLatheTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
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
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You have no place available to hold {{Y|the force knife}}.",
        "{{Y|フォースナイフ}}を保持できる空き部位がない。",
        "NoHoldSlot")]
    [TestCase(
        "You have no place available to hold the result.",
        "結果を保持できる空き部位がない。",
        "NoHoldSlot")]
    [TestCase(
        "The precision force lathe is {{K|unpowered}}.",
        "精密フォース旋盤は{{K|電源が入っていない}}。",
        "StatusFailure")]
    public void ActivatePrecisionForceLathe_TranslatesOwnerFailures_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        using var ownerPatch = PatchOwner();
        using var popupPatch = PatchPopupShow();
        using var queuePatch = PatchQueue();
        var target = new DummyPrecisionForceLatheTarget
        {
            Message = source,
        };

        target.ActivatePrecisionForceLathe(new DummyGameObject(), new DummyGameObject(), new DummyEvent());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
            Assert.That(HitCount(detail), Is.EqualTo(1));
        });
    }

    [Test]
    public void ActivatePrecisionForceLathe_LeavesUnknownAndOwnerAbsentMessagesUnchanged()
    {
        using (PatchPopupShow())
        {
            DummyPopupShow.ShowFail("Unknown force lathe failure.");
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Unknown force lathe failure."));
        }

        using (PatchOwner())
        using (PatchPopupShow())
        using (PatchQueue())
        {
            var target = new DummyPrecisionForceLatheTarget
            {
                Message = "The precision force lathe is {{K|mysteriously jammed}}.",
            };

            target.ActivatePrecisionForceLathe(new DummyGameObject(), new DummyGameObject(), new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("The precision force lathe is {{K|mysteriously jammed}}."));
                Assert.That(
                    DummyMessageQueue.LastMessage,
                    Is.EqualTo("The precision force lathe is {{K|mysteriously jammed}}."));
                Assert.That(HitCount("StatusFailure"), Is.EqualTo(0));
            });
        }
    }

    [Test]
    public void ActivatePrecisionForceLathe_LeavesEmptyMessage_WhenOwnerPatched()
    {
        using var ownerPatch = PatchOwner();
        using var popupPatch = PatchPopupShow();
        using var queuePatch = PatchQueue();
        var target = new DummyPrecisionForceLatheTarget
        {
            Message = string.Empty,
        };

        target.ActivatePrecisionForceLathe(new DummyGameObject(), new DummyGameObject(), new DummyEvent());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
            Assert.That(DummyMessageQueue.LastMessage, Is.Empty);
            Assert.That(HitCount("NoHoldSlot"), Is.Zero);
            Assert.That(HitCount("StatusFailure"), Is.Zero);
        });
    }

    [Test]
    public void ActivatePrecisionForceLathe_StripsDirectMarkedVisibleMessage_WhenOwnerPatched()
    {
        using var ownerPatch = PatchOwner();
        using var popupPatch = PatchPopupShow();
        using var queuePatch = PatchQueue();
        var target = new DummyPrecisionForceLatheTarget
        {
            Message = MessageFrameTranslator.MarkDirectTranslation("翻訳済み"),
        };

        target.ActivatePrecisionForceLathe(new DummyGameObject(), new DummyGameObject(), new DummyEvent());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("翻訳済み"));
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("翻訳済み"));
        });
    }

    private static IDisposable PatchOwner()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPrecisionForceLatheTarget),
                nameof(DummyPrecisionForceLatheTarget.ActivatePrecisionForceLathe),
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(DummyEvent)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CyberneticsPrecisionForceLatheTranslationPatch),
                nameof(CyberneticsPrecisionForceLatheTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(CyberneticsPrecisionForceLatheTranslationPatch),
                nameof(CyberneticsPrecisionForceLatheTranslationPatch.Finalizer))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static IDisposable PatchPopupShow()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Finalizer))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static IDisposable PatchQueue()
    {
        var harmonyId = CreateHarmonyId();
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

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup." + nameof(CyberneticsPrecisionForceLatheTranslationPatch) + "." + detail)
            + DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(CombatAndLogMessageQueuePatch),
                "MessageQueue." + nameof(CyberneticsPrecisionForceLatheTranslationPatch) + "." + detail);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters.Length == 0 ? null : parameters)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyPrecisionForceLatheTarget
    {
        public string Message { get; init; } = string.Empty;

        public void ActivatePrecisionForceLathe(DummyGameObject actor, DummyGameObject obj, DummyEvent e)
        {
            _ = actor;
            _ = obj;
            _ = e;
            DummyPopupShow.ShowFail(Message);
            DummyMessageQueue.AddPlayerMessage(Message);
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
