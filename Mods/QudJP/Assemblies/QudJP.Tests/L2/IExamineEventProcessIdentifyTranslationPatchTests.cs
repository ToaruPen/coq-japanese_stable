using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class IExamineEventProcessIdentifyTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        InventoryActionMenuCloseTimingObservability.ResetForTests();
        IExamineEventProcessIdentifyTranslationPatch.SetInventoryScreenRefreshHooksForTests(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        InventoryActionMenuCloseTimingObservability.ResetForTests();
        IExamineEventProcessIdentifyTranslationPatch.SetInventoryScreenRefreshHooksForTests(null, null);
    }

    [Test]
    public void ProcessIdentify_TranslatesVisibleItemIdentifyPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "You realize {{Y|strange artifact}} is a {{C|laser pistol}}!",
            "{{Y|strange artifact}}は{{C|laser pistol}}だとわかった！");
    }

    [Test]
    public void ProcessIdentify_TranslatesDestroyedItemIdentifyPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "You realize the {{Y|strange artifact}} was a {{C|laser pistol}}!",
            "{{Y|strange artifact}}は{{C|laser pistol}}だったとわかった！");
    }

    [Test]
    public void ProcessIdentify_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You realize {{Y|strange artifact}} is a {{C|laser pistol}}!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ProcessIdentify_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You realize strange artifact is a laser pistol!"),
            "You realize strange artifact is a laser pistol!");
    }

    [TestCase("")]
    [TestCase("You fail to identify {{Y|strange artifact}}.")]
    [TestCase("You realize {{Y|strange artifact}}!")]
    public void ProcessIdentify_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source);
    }

    [Test]
    public void ProcessIdentify_RefreshesInventoryScreen_WhenIdentifySucceeded()
    {
        var screen = new DummyInventoryStatusScreen();
        IExamineEventProcessIdentifyTranslationPatch.SetInventoryScreenRefreshHooksForTests(
            () => screen,
            target => ((DummyInventoryStatusScreen)target).UpdateViewFromData());

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyIExamineEventProcessIdentifyTarget.PopupMessageToShow = "You realize strange artifact is a laser pistol!";
            DummyIExamineEventProcessIdentifyTarget.Result = true;
            DummyIExamineEventProcessIdentifyTarget.ProcessIdentify();

            Assert.That(screen.RefreshCount, Is.EqualTo(1));
        }
        finally
        {
            DummyIExamineEventProcessIdentifyTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ProcessIdentify_RefreshesInventoryScreen_WhenRecentInventoryActionMenuCancelWouldSuppressNormalRefresh()
    {
        var screen = new DummyInventoryStatusScreen();
        IExamineEventProcessIdentifyTranslationPatch.SetInventoryScreenRefreshHooksForTests(
            () => screen,
            target => ((DummyInventoryStatusScreen)target).UpdateViewFromData());

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);
            PatchInventoryScreenRefresh(harmony);

            var menuScope = InventoryActionMenuCloseTimingObservability.BeginMenu(actionCount: 1);
            InventoryActionMenuCloseTimingObservability.EndMenu(menuScope, canceled: true);
            Assert.That(
                InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancelForTests(),
                Is.True);

            DummyIExamineEventProcessIdentifyTarget.PopupMessageToShow = "You realize strange artifact is a laser pistol!";
            DummyIExamineEventProcessIdentifyTarget.Result = true;
            DummyIExamineEventProcessIdentifyTarget.ProcessIdentify();

            Assert.Multiple(() =>
            {
                Assert.That(screen.RefreshCount, Is.EqualTo(1));
                Assert.That(
                    InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancelForTests(),
                    Is.True);
            });
        }
        finally
        {
            DummyIExamineEventProcessIdentifyTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ProcessIdentify_DoesNotRefreshInventoryScreen_WhenIdentifyDidNotChangeState()
    {
        var screen = new DummyInventoryStatusScreen();
        IExamineEventProcessIdentifyTranslationPatch.SetInventoryScreenRefreshHooksForTests(
            () => screen,
            target => ((DummyInventoryStatusScreen)target).UpdateViewFromData());

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyIExamineEventProcessIdentifyTarget.PopupMessageToShow = string.Empty;
            DummyIExamineEventProcessIdentifyTarget.Result = false;
            DummyIExamineEventProcessIdentifyTarget.ProcessIdentify();

            Assert.That(screen.RefreshCount, Is.Zero);
        }
        finally
        {
            DummyIExamineEventProcessIdentifyTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyIExamineEventProcessIdentifyTarget.PopupMessageToShow = source;
            DummyIExamineEventProcessIdentifyTarget.ProcessIdentify();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyIExamineEventProcessIdentifyTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyIExamineEventProcessIdentifyTarget), nameof(DummyIExamineEventProcessIdentifyTarget.ProcessIdentify)),
            prefix: new HarmonyMethod(RequireMethod(typeof(IExamineEventProcessIdentifyTranslationPatch), nameof(IExamineEventProcessIdentifyTranslationPatch.Prefix))),
            postfix: new HarmonyMethod(RequireMethod(typeof(IExamineEventProcessIdentifyTranslationPatch), nameof(IExamineEventProcessIdentifyTranslationPatch.Postfix), typeof(bool))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(IExamineEventProcessIdentifyTranslationPatch), nameof(IExamineEventProcessIdentifyTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void PatchInventoryScreenRefresh(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyInventoryStatusScreen), nameof(DummyInventoryStatusScreen.UpdateViewFromData)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuUpdateViewTimingPatch),
                nameof(InventoryActionMenuUpdateViewTimingPatch.Prefix),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuUpdateViewTimingPatch),
                nameof(InventoryActionMenuUpdateViewTimingPatch.Postfix),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope))));
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static class DummyIExamineEventProcessIdentifyTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        public static bool Result { get; set; } = true;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ProcessIdentify()
        {
            DummyPopupShow.Show(PopupMessageToShow);
            return Result;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
            Result = true;
        }
    }

    private sealed class DummyInventoryStatusScreen
    {
        public int RefreshCount { get; private set; }

        public void UpdateViewFromData()
        {
            RefreshCount++;
        }
    }
}
