using System.Reflection;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class InventoryActionMenuCursorSoundPatchTests
{
    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
    }

    [Test]
    public void RememberPopupController_StoresPopupIdForControllerInstance()
    {
        var controller = new object();
        var popup = new FakePopupMessage
        {
            controller = controller,
            PopupID = "InventoryActionMenu:abc",
        };

        InventoryActionMenuCursorSoundPatch.RememberPopupController(popup);

        var found = InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var popupId);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(popupId, Is.EqualTo("InventoryActionMenu:abc"));
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(new object(), out _),
                Is.False);
        });
    }

    [Test]
    public void ForgetPopupController_RestoresOuterPopupIdForNestedControllerContext()
    {
        var controller = new object();
        var outer = new FakePopupMessage
        {
            controller = controller,
            PopupID = "InventoryActionMenu:outer",
        };
        var inner = new FakePopupMessage
        {
            controller = controller,
            PopupID = "Popup:inner",
        };

        InventoryActionMenuCursorSoundPatch.RememberPopupController(outer);
        InventoryActionMenuCursorSoundPatch.RememberPopupController(inner);

        Assert.Multiple(() =>
        {
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var innerPopupId),
                Is.True);
            Assert.That(innerPopupId, Is.EqualTo("Popup:inner"));
        });

        InventoryActionMenuCursorSoundPatch.ForgetPopupController(inner);

        Assert.Multiple(() =>
        {
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var restoredPopupId),
                Is.True);
            Assert.That(restoredPopupId, Is.EqualTo("InventoryActionMenu:outer"));
        });

        InventoryActionMenuCursorSoundPatch.ForgetPopupController(outer);

        Assert.That(
            InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out _),
            Is.False);
    }

    [Test]
    public void PlayCursorSoundForInventoryActionMenuController_DoesNotLogPlayed_WhenSoundMethodIsUnavailable()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        var controller = new object();
        var popup = new FakePopupMessage
        {
            controller = controller,
            PopupID = "InventoryActionMenu:abc",
        };
        InventoryActionMenuCursorSoundPatch.RememberPopupController(popup);

        string output;
        try
        {
            var patchType = typeof(InventoryActionMenuCursorSoundPatch);
            var playMethodCache = patchType.GetField("playUiSoundMethod", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("PlayUISound method cache field not found.");
            var effectTypeCache = patchType.GetField("soundEffectType", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Sound effect type cache field not found.");
            var originalPlayMethod = playMethodCache.GetValue(null);
            var originalEffectType = effectTypeCache.GetValue(null);
            try
            {
                playMethodCache.SetValue(null, null);
                effectTypeCache.SetValue(null, typeof(object));
                output = TestTraceHelper.CaptureTrace(() =>
                    InventoryActionMenuCursorSoundPatch.PlayCursorSoundForInventoryActionMenuController(controller));
            }
            finally
            {
                playMethodCache.SetValue(null, originalPlayMethod);
                effectTypeCache.SetValue(null, originalEffectType);
            }
        }
        finally
        {
            InventoryActionMenuCursorSoundPatch.ForgetPopupController(popup);
        }

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Not.Contain("InventoryActionMenuCursorSound/v1"));
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out _),
                Is.False);
        });
    }

    [Test]
    public void ShouldPlayCursorSoundForPopupController_AllowsGenericPopupWithNullPopupId_WhenMenuItemsExist()
    {
        var controller = new FakeMenuController
        {
            menuData = new List<object> { new() },
        };

        Assert.That(
            InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForPopupControllerForTests(controller, null),
            Is.True);
    }

    [Test]
    public void ShouldPlayCursorSoundForPopupController_RejectsGenericPopupWithNullPopupId_WhenNoMenuItemsExist()
    {
        var controller = new FakeMenuController();

        Assert.That(
            InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForPopupControllerForTests(controller, null),
            Is.False);
    }

    [Test]
    public void ShouldPlayCursorSoundForPopupController_AllowsInventoryActionMenuPopupIdWithoutMenuItems()
    {
        var controller = new object();

        Assert.That(
            InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForPopupControllerForTests(
                controller,
                "InventoryActionMenu:abc"),
            Is.True);
    }

    private sealed class FakePopupMessage
    {
        public object? controller;
        public string? PopupID;
    }

    private sealed class FakeMenuController
    {
        public List<object>? menuData;
#pragma warning disable S1144
        public List<object> bottomContextOptions = new();
#pragma warning restore S1144
    }
}
