using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class InventoryActionMenuPatchIntegrationTests
{
    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        InventoryActionMenuCloseTimingObservability.ResetForTests();
        InventoryActionMenuCursorSoundPatch.SetPlayCursorSoundRequestObserverForTests(null);
        InventoryActionMenuPopupUpdateTimingPatch.ResetForTests();
    }

    [Test]
    public void CloseTimingHarmonyPatches_SuppressCancelRefreshUntilPopupHideDelayCompletes()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var menu = new DummyInventoryActionMenuTarget();
        var popup = new DummyPopupMessageTarget
        {
            PopupID = "InventoryActionMenu:(noid)",
        };
        menu.PopupToHide = popup;
        var inventory = new DummyInventoryStatusScreenTarget();

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = menu.Show(new ArrayList { "drop" }, cancel: true);
            inventory.UpdateViewFromData();
            popup.Update();
            popup.Update();
            inventory.UpdateViewFromData();
        });

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(output, Does.Contain("phase=popup-hide-request"));
            Assert.That(output, Does.Contain("phase=inventory-refresh-suppressed"));
            Assert.That(output, Does.Contain("phase=popup-hidden-after-frame-delay"));
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_RestoreOuterActiveMenuAfterNestedActionMenu()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var outerMenu = new DummyInventoryActionMenuTarget();
        var innerMenu = new DummyInventoryActionMenuTarget();
        var popup = new DummyPopupMessageTarget
        {
            PopupID = "InventoryActionMenu:outer",
        };
        outerMenu.NestedMenu = innerMenu;
        outerMenu.PopupToHide = popup;

        var output = TestTraceHelper.CaptureTrace(() =>
            _ = outerMenu.Show(new ArrayList { "outer" }, cancel: true));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("seq=2;phase=menu-return"));
            Assert.That(output, Does.Contain("result\\=action"));
            Assert.That(output, Does.Contain("seq=1;phase=popup-hide-request"));
            Assert.That(output, Does.Contain("seq=1;phase=menu-return"));
            Assert.That(output, Does.Contain("result\\=cancel"));
        });
    }

    [Test]
    public void PopupUpdateTimingPatch_SkipsReflection_WhenNoSuppressionAndVerboseProbesAreDisabled()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(false);
        using var patches = PatchPopupUpdateOnly(typeof(DummyPopupMessageWithoutHideNextFrameTarget));
        var popup = new DummyPopupMessageWithoutHideNextFrameTarget();

        var output = TestTraceHelper.CaptureTrace(popup.Update);

        Assert.That(output, Does.Not.Contain("could not read HideNextFrame"));
    }

    [Test]
    public void PopupUpdateTimingPatch_WarnsOnce_WhenHideNextFrameCannotBeRead()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchPopupUpdateOnly(typeof(DummyPopupMessageWithoutHideNextFrameTarget));
        var popup = new DummyPopupMessageWithoutHideNextFrameTarget();

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            popup.Update();
            popup.Update();
        });

        Assert.That(CountOccurrences(output, "could not read HideNextFrame"), Is.EqualTo(1));
    }

    [Test]
    public void CursorSoundHarmonyPatches_RestoreOuterPopupContextAfterNestedPopupHide()
    {
        using var patches = PatchCursorSound();
        var observedPopupIds = new List<string?>();
        InventoryActionMenuCursorSoundPatch.SetPlayCursorSoundRequestObserverForTests(
            (_, popupId) => observedPopupIds.Add(popupId));
        var controller = new DummyMenuControllerTarget();
        var popup = new DummyPopupMessageTarget
        {
            controller = controller,
            PopupID = "InventoryActionMenu:outer",
        };

        popup.ShowPopup();
        popup.PopupID = "Popup:inner";
        popup.ShowPopup();

        Assert.Multiple(() =>
        {
            Assert.That(popup.ShowCount, Is.EqualTo(2));
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var innerPopupId),
                Is.True);
            Assert.That(innerPopupId, Is.EqualTo("Popup:inner"));
        });

        controller.PlayClick();

        Assert.That(observedPopupIds, Is.EqualTo(new[] { "Popup:inner" }));

        popup.Hide();

        Assert.Multiple(() =>
        {
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var restoredPopupId),
                Is.True);
            Assert.That(restoredPopupId, Is.EqualTo("InventoryActionMenu:outer"));
        });

        popup.Hide();

        Assert.That(
            InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out _),
            Is.False);
    }

    private static IDisposable PatchCloseTiming()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyInventoryActionMenuTarget), nameof(DummyInventoryActionMenuTarget.Show)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuShowTimingPatch),
                nameof(InventoryActionMenuShowTimingPatch.Prefix),
                typeof(object),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuShowTimingPatch),
                nameof(InventoryActionMenuShowTimingPatch.Postfix),
                typeof(object),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.Hide)),
            prefix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuPopupHideTimingPatch), nameof(InventoryActionMenuPopupHideTimingPatch.Prefix), typeof(object))));
        PatchPopupUpdate(harmony, typeof(DummyPopupMessageTarget));
        harmony.Patch(
            original: RequireMethod(typeof(DummyInventoryStatusScreenTarget), nameof(DummyInventoryStatusScreenTarget.UpdateViewFromData)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuUpdateViewTimingPatch),
                nameof(InventoryActionMenuUpdateViewTimingPatch.Prefix),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuUpdateViewTimingPatch),
                nameof(InventoryActionMenuUpdateViewTimingPatch.Postfix),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static IDisposable PatchPopupUpdateOnly(Type popupType)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        PatchPopupUpdate(harmony, popupType);
        return new HarmonyScope(harmony, harmonyId);
    }

    private static void PatchPopupUpdate(Harmony harmony, Type popupType)
    {
        harmony.Patch(
            original: RequireMethod(popupType, "Update"),
            prefix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuPopupUpdateTimingPatch), nameof(InventoryActionMenuPopupUpdateTimingPatch.Prefix), typeof(object), typeof(int).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuPopupUpdateTimingPatch), nameof(InventoryActionMenuPopupUpdateTimingPatch.Postfix), typeof(object), typeof(int))));
    }

    private static IDisposable PatchCursorSound()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
            postfix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuCursorSoundPopupContextPatch), nameof(InventoryActionMenuCursorSoundPopupContextPatch.Postfix), typeof(object))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.Hide)),
            prefix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuCursorSoundPopupHideContextPatch), nameof(InventoryActionMenuCursorSoundPopupHideContextPatch.Prefix), typeof(object))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMenuControllerTarget), nameof(DummyMenuControllerTarget.PlayClick)),
            postfix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuCursorSoundPlayClickPatch), nameof(InventoryActionMenuCursorSoundPlayClickPatch.Postfix), typeof(object))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}")
            : AccessTools.Method(type, methodName, parameterTypes) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var matchIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + value.Length;
        }
    }

    private sealed class DummyInventoryActionMenuTarget
    {
        public DummyPopupMessageTarget? PopupToHide { get; set; }

        public DummyInventoryActionMenuTarget? NestedMenu { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public object? Show(object actionTable, bool cancel)
        {
            _ = NestedMenu?.Show(new ArrayList { "nested" }, cancel: false);
            if (cancel)
            {
                PopupToHide?.Hide();
            }

            return cancel ? null : new object();
        }
    }

    private sealed class DummyPopupMessageTarget
    {
        public object? controller;
        public string? PopupID;
        public int HideNextFrame;
        public int ShowCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowPopup()
        {
            ShowCount++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Hide()
        {
            HideNextFrame = 2;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Update()
        {
            if (HideNextFrame > 0)
            {
                HideNextFrame--;
            }
        }
    }

    private sealed class DummyPopupMessageWithoutHideNextFrameTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Update()
        {
        }
    }

    private sealed class DummyInventoryStatusScreenTarget
    {
        public int RefreshCount { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateViewFromData()
        {
            RefreshCount++;
        }
    }

    private sealed class DummyMenuControllerTarget
    {
        public int ClickCount { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PlayClick()
        {
            ClickCount++;
        }
    }

    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        internal HarmonyScope(Harmony harmony, string harmonyId)
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
