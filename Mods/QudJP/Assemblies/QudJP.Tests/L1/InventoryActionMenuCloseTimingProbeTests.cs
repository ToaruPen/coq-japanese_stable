namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class InventoryActionMenuCloseTimingProbeTests
{
    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        InventoryActionMenuCloseTimingObservability.ResetForTests();
    }

    [Test]
    public void BuildLogLineForTests_WritesStructuredInventoryActionMenuTimingMarker()
    {
        var line = InventoryActionMenuCloseTimingObservability.BuildLogLineForTests(
            sequence: 7,
            phase: "inventory-refresh-end",
            elapsed: TimeSpan.FromMilliseconds(12.3456),
            detail: "popup_id=InventoryActionMenu:abc;result=cancel");

        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Contain("[QudJP] InventoryActionMenuCloseTiming/v1:"));
            Assert.That(line, Does.Contain("seq=7"));
            Assert.That(line, Does.Contain("phase=inventory-refresh-end"));
            Assert.That(line, Does.Contain("elapsed_ms=12.346"));
            Assert.That(line, Does.Contain("detail=popup_id\\=InventoryActionMenu:abc\\;result\\=cancel"));
        });
    }

    [Test]
    public void LogForTests_DoesNotInvokeDetailFactory_WhenVerboseProbesAreDisabled()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(false);
        var factoryCalls = 0;

        var output = TestTraceHelper.CaptureTrace(() =>
            InventoryActionMenuCloseTimingObservability.LogForTests(
                "popup-hide-request",
                () =>
                {
                    factoryCalls++;
                    return "popup_id=InventoryActionMenu:abc";
                }));

        Assert.Multiple(() =>
        {
            Assert.That(factoryCalls, Is.Zero);
            Assert.That(output, Does.Not.Contain("InventoryActionMenuCloseTiming/v1"));
        });
    }

    [Test]
    public void ShouldSuppressInventoryRefreshAfterCancelForTests_SuppressesOnlyWhilePopupHideIsPending()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        var scope = InventoryActionMenuCloseTimingObservability.BeginMenu(actionCount: 7);

        InventoryActionMenuCloseTimingObservability.EndMenu(scope, canceled: true);

        Assert.That(
            InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancelForTests(),
            Is.True);

        InventoryActionMenuCloseTimingObservability.LogPopupHiddenAfterFrameDelay(
            "InventoryActionMenu:(noid)",
            previousHideNextFrame: 1,
            currentHideNextFrame: 0);

        Assert.That(
            InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancelForTests(),
            Is.False);
    }

    [Test]
    public void LogPopupHiddenAfterFrameDelay_UsesActiveSequenceElapsed_WhenNoRecentCancelExists()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        _ = InventoryActionMenuCloseTimingObservability.BeginMenu(actionCount: 7);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 15)
        {
            Thread.SpinWait(1000);
        }

        var output = TestTraceHelper.CaptureTrace(() =>
            InventoryActionMenuCloseTimingObservability.LogPopupHiddenAfterFrameDelay(
                "InventoryActionMenu:(noid)",
                previousHideNextFrame: 1,
                currentHideNextFrame: 0));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("phase=popup-hidden-after-frame-delay"));
            Assert.That(output, Does.Not.Contain("elapsed_ms=0.000"));
        });
    }

    [Test]
    public void EndMenu_ClearsRecentCancelContext_WhenMenuReturnsAction()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        var canceledScope = InventoryActionMenuCloseTimingObservability.BeginMenu(actionCount: 7);
        InventoryActionMenuCloseTimingObservability.EndMenu(canceledScope, canceled: true);

        var actionScope = InventoryActionMenuCloseTimingObservability.BeginMenu(actionCount: 7);
        InventoryActionMenuCloseTimingObservability.EndMenu(actionScope, canceled: false);

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            var refreshScope = InventoryActionMenuCloseTimingObservability.BeginInventoryRefresh();
            InventoryActionMenuCloseTimingObservability.EndInventoryRefresh(refreshScope);
        });

        Assert.Multiple(() =>
        {
            Assert.That(
                InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancelForTests(),
                Is.False);
            Assert.That(output, Does.Not.Contain("inventory-refresh-begin"));
            Assert.That(output, Does.Not.Contain("inventory-refresh-end"));
        });
    }
}
