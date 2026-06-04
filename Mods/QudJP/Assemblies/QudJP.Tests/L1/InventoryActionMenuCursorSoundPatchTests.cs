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
        InventoryActionMenuCursorSoundPatch.RememberPopupController(new FakePopupMessage
        {
            controller = controller,
            PopupID = "InventoryActionMenu:abc",
        });

        var output = TestTraceHelper.CaptureTrace(() =>
            InventoryActionMenuCursorSoundPatch.PlayCursorSoundForInventoryActionMenuController(controller));

        Assert.That(output, Does.Not.Contain("InventoryActionMenuCursorSound/v1"));
    }

    private sealed class FakePopupMessage
    {
        public object? controller;
        public string? PopupID;
    }
}
