using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HookedOwnerTranslationPatchTests
{
    private const string PatchTypeName = "QudJP.Patches.HookedOwnerTranslationPatch";
    private const string Family = "HookedOwner";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyMessageQueue.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You break free from your steel battle axe!",
        "あなたはsteel battle axeから抜け出した！")]
    [TestCase(
        "{{R|snapjaw}} breaks free from your steel battle axe!",
        "{{R|snapjaw}}はsteel battle axeから抜け出した！")]
    [TestCase(
        "snapjaw breaks free from Issachar's carbide axe!",
        "snapjawはIssacharのcarbide axeから抜け出した！")]
    [TestCase(
        "The {{R|snapjaw}} breaks free from the snapjaw's carbide axe!",
        "{{R|snapjaw}}はsnapjawのcarbide axeから抜け出した！")]
    [TestCase(
        "You break free from the hook maneuver!",
        "あなたはフック技から抜け出した！")]
    public void HookedOwner_TranslatesBreakFreeMessage_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            var target = new DummyHookedOwner(source);

            target.HandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(expected)));
                Assert.That(HitCount("BreakFree"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HookedOwner_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        var source = "You break free from your steel battle axe!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "R", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("R"));
                Assert.That(HitCount("BreakFree"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HookedOwner_StripsDirectMarkedQueuedMessage_WithoutRetranslating()
    {
        WithPatchedOwnerAndQueue(() =>
        {
            var target = new DummyHookedOwner(
                MessageFrameTranslator.MarkDirectTranslation("You break free from your steel battle axe!"));

            target.HandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You break free from your steel battle axe!"));
                Assert.That(HitCount("BreakFree"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You remain hooked by your steel battle axe!")]
    public void HookedOwner_LeavesUnsupportedQueuedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            var target = new DummyHookedOwner(source);

            target.HandleEvent();

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
            harmony.Patch(
                original: RequireMethod(typeof(DummyHookedOwner), nameof(DummyHookedOwner.HandleEvent)),
                prefix: new HarmonyMethod(RequirePatchMethod("Prefix")),
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
            prefix: new HarmonyMethod(RequireMethod(typeof(DirectHookedOwnerSink), nameof(DirectHookedOwnerSink.QueuePrefix))));
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.hooked-owner.{Guid.NewGuid():N}";
    }

    private static class DirectHookedOwnerSink
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
    }
}

internal sealed class DummyHookedOwner
{
    private readonly string source;

    public DummyHookedOwner(string source)
    {
        this.source = source;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent()
    {
        DummyMessageQueue.AddPlayerMessage(source, "R", Capitalize: false);
        return true;
    }
}
