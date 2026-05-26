using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SubmergedBurrowedOwnerTranslationPatchTests
{
    private const string PatchTypeName = "QudJP.Patches.SubmergedBurrowedOwnerTranslationPatch";
    private const string Family = "SubmergedBurrowedOwner";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.LastShowMessage = null;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        nameof(DummySubmergedBurrowedOwner.SubmergedApply),
        "You submerge.",
        "潜った。",
        "Submerged.Submerge")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.SubmergedRemove),
        "{{Y|snapjaw}} emerges from {{B|brackish water}}.",
        "{{Y|snapjaw}}が{{B|brackish water}}から浮上した。",
        "Submerged.EmergeFrom")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.SubmergedFireEvent),
        "You are forced to the surface.",
        "水面に押し出された。",
        "Submerged.ForcedToSurface")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.BurrowedApply),
        "You burrow into the ground.",
        "地面に潜った。",
        "Burrowed.BurrowIntoGround")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.BurrowedFireEvent),
        "{{R|snapjaw}} is forced to the surface.",
        "{{R|snapjaw}}は地表に押し出された。",
        "Burrowed.ForcedToSurface")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.BurrowedEmerge),
        "{{Y|snapjaw}} emerges from the ground.",
        "{{Y|snapjaw}}が地面から現れた。",
        "Burrowed.EmergeFromGround")]
    public void OwnerPatch_TranslatesQueuedMovementModeMessages_WhenOwnerPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedQueueOwner(ownerMethodName, () =>
        {
            var target = new DummySubmergedBurrowedOwner(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(expected)));
                Assert.That(HitCount("Queue", detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        nameof(DummySubmergedBurrowedOwner.SubmergedFireEventPopup),
        "You cannot do that while submerged.",
        "水中ではそんなことはできない。",
        "Submerged.CannotDoThat")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.BurrowedFireEventPopup),
        "You cannot do that while burrowed.",
        "潜伏中はそれはできない。",
        "Burrowed.CannotDoThat")]
    [TestCase(
        nameof(DummySubmergedBurrowedOwner.BurrowedFireEventPopup),
        "You cannot travel long distances while burrowed.",
        "潜伏中は長距離を移動できない。",
        "Burrowed.CannotTravel")]
    public void OwnerPatch_TranslatesPopupMovementModeMessages_WhenOwnerPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WithPatchedPopupOwner(ownerMethodName, () =>
        {
            var target = new DummySubmergedBurrowedOwner(source);

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("Popup", detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerPatch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = "qudjp.tests.submerged-burrowed.absent." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("You burrow into the ground.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You burrow into the ground."));
                Assert.That(HitCount("Queue", "Burrowed.BurrowIntoGround"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void OwnerPatch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = "qudjp.tests.submerged-burrowed.popup-absent." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony);

            DummyPopupShow.ShowFail("You cannot do that while submerged.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You cannot do that while submerged."));
                Assert.That(HitCount("Popup", "Submerged.CannotDoThat"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void OwnerPatch_StripsDirectMarkedQueuedMessage_WithoutRetranslating()
    {
        WithPatchedQueueOwner(nameof(DummySubmergedBurrowedOwner.BurrowedApply), () =>
        {
            var target = new DummySubmergedBurrowedOwner(
                MessageFrameTranslator.MarkDirectTranslation("You burrow into the ground."));

            target.BurrowedApply();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You burrow into the ground."));
                Assert.That(HitCount("Queue", "Burrowed.BurrowIntoGround"), Is.Zero);
            });
        });
    }

    [Test]
    public void OwnerPatch_StripsDirectMarkedPopupMessage_WithoutRetranslating()
    {
        WithPatchedPopupOwner(nameof(DummySubmergedBurrowedOwner.SubmergedFireEventPopup), () =>
        {
            var target = new DummySubmergedBurrowedOwner(
                MessageFrameTranslator.MarkDirectTranslation("You cannot do that while submerged."));

            target.SubmergedFireEventPopup();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You cannot do that while submerged."));
                Assert.That(HitCount("Popup", "Submerged.CannotDoThat"), Is.Zero);
            });
        });
    }

    [TestCase("You hover above the ground.")]
    [TestCase("")]
    public void OwnerPatch_LeavesUnsupportedQueuedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        WithPatchedQueueOwner(nameof(DummySubmergedBurrowedOwner.BurrowedApply), () =>
        {
            var target = new DummySubmergedBurrowedOwner(source);

            target.BurrowedApply();

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        });
    }

    private static void WithPatchedQueueOwner(string ownerMethodName, Action action)
    {
        WithPatchedOwner(ownerMethodName, PatchQueue, action);
    }

    private static void WithPatchedPopupOwner(string ownerMethodName, Action action)
    {
        WithPatchedOwner(ownerMethodName, PatchPopup, action);
    }

    private static void WithPatchedOwner(string ownerMethodName, Action<Harmony> patchSink, Action action)
    {
        var harmonyId = "qudjp.tests.submerged-burrowed." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            patchSink(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummySubmergedBurrowedOwner), ownerMethodName),
                prefix: new HarmonyMethod(RequirePatchMethod("Prefix", typeof(MethodBase))),
                finalizer: new HarmonyMethod(RequirePatchMethod("Finalizer", typeof(Exception))));
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
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DirectSubmergedBurrowedOwnerSink), nameof(DirectSubmergedBurrowedOwnerSink.QueuePrefix))));
    }

    private static void PatchPopup(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowFail),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DirectSubmergedBurrowedOwnerSink), nameof(DirectSubmergedBurrowedOwnerSink.PopupPrefix))));
    }

    private static void InvokeOwner(DummySubmergedBurrowedOwner target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummySubmergedBurrowedOwner), ownerMethodName).Invoke(target, null);
    }

    private static int HitCount(string sink, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            sink == "Queue" ? "MessageQueue.AddPlayerMessage" : "Popup.ShowFail",
            Family + "." + detail);
    }

    private static Type RequirePatchType()
    {
        var type = Type.GetType(PatchTypeName + ", QudJP.Tests");
        Assert.That(type, Is.Not.Null, PatchTypeName + " not found");
        return type!;
    }

    private static MethodInfo RequirePatchMethod(string methodName, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(RequirePatchType(), methodName, parameterTypes);
        Assert.That(method, Is.Not.Null, PatchTypeName + "." + methodName + " not found");
        return method!;
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, name)
            : AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static class DirectSubmergedBurrowedOwnerSink
    {
        public static void QueuePrefix(ref string Message, string? Color = null, bool Capitalize = true)
        {
            _ = Capitalize;
            var args = new object?[] { Message, Color };
            if ((bool)RequirePatchMethod("TryTranslateQueuedMessage", typeof(string).MakeByRefType(), typeof(string))
                .Invoke(null, args)!)
            {
                Message = (string)args[0]!;
            }
        }

        public static void PopupPrefix(ref string __0)
        {
            var args = new object?[] { __0, "Popup.ShowFail", "Popup.Show", null };
            if ((bool)RequirePatchMethod(
                    "TryTranslatePopupMessage",
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string).MakeByRefType())
                .Invoke(null, args)!)
            {
                __0 = (string)args[3]!;
            }
        }
    }
}

internal sealed class DummySubmergedBurrowedOwner
{
    private readonly string source;

    public DummySubmergedBurrowedOwner(string source)
    {
        this.source = source;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool SubmergedApply()
    {
        DummyMessageQueue.AddPlayerMessage(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SubmergedRemove()
    {
        DummyMessageQueue.AddPlayerMessage(source);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S4144:Methods should not have identical implementations",
        Justification = "Distinct dummy owner method names are the behavior under test.")]
    public bool SubmergedFireEvent()
    {
        DummyMessageQueue.AddPlayerMessage(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool SubmergedFireEventPopup()
    {
        DummyPopupShow.ShowFail(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S4144:Methods should not have identical implementations",
        Justification = "Distinct dummy owner method names are the behavior under test.")]
    public bool BurrowedApply()
    {
        DummyMessageQueue.AddPlayerMessage(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S4144:Methods should not have identical implementations",
        Justification = "Distinct dummy owner method names are the behavior under test.")]
    public bool BurrowedFireEvent()
    {
        DummyMessageQueue.AddPlayerMessage(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S4144:Methods should not have identical implementations",
        Justification = "Distinct dummy owner method names are the behavior under test.")]
    public bool BurrowedFireEventPopup()
    {
        DummyPopupShow.ShowFail(source);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BurrowedEmerge()
    {
        DummyMessageQueue.AddPlayerMessage(source);
    }
}
