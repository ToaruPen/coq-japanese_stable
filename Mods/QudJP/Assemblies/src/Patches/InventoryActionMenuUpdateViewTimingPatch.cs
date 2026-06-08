using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionMenuUpdateViewTimingPatch
{
    private const string Context = nameof(InventoryActionMenuUpdateViewTimingPatch);
    private const string TargetTypeName = "Qud.UI.InventoryAndEquipmentStatusScreen";

    internal readonly struct RefreshState
    {
        internal RefreshState(object? inventoryScreen, InventoryActionMenuCloseTimingObservability.TimingScope timingScope)
        {
            InventoryScreen = inventoryScreen;
            TimingScope = timingScope;
        }

        internal static RefreshState Empty { get; } = new(null, InventoryActionMenuCloseTimingObservability.TimingScope.Empty);

        internal object? InventoryScreen { get; }

        internal InventoryActionMenuCloseTimingObservability.TimingScope TimingScope { get; }
    }

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.UpdateViewFromData target not found.", Context);
        }

        return method;
    }

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(object? __instance, ref RefreshState __state)
    {
        try
        {
            var resetNameCaches = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(__instance);
            var consumedInventoryLineRefresh =
                InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
            if (consumedInventoryLineRefresh)
            {
                InventoryLineRefreshCoordinator.ResetInventoryFiltersBeforeFullRefresh(__instance);
            }

            if (InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancel()
                && !resetNameCaches
                && !consumedInventoryLineRefresh
                && !InventoryNameRefreshCoordinator.HasPendingRefresh()
                && !InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction())
            {
                InventoryActionMenuCloseTimingObservability.LogInventoryRefreshSuppressed();
                __state = RefreshState.Empty;
                return false;
            }

            __state = new RefreshState(__instance, InventoryActionMenuCloseTimingObservability.BeginInventoryRefresh());
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = new RefreshState(__instance, InventoryActionMenuCloseTimingObservability.TimingScope.Empty);
            return true;
        }
    }

    public static void Postfix(RefreshState __state)
    {
        try
        {
            _ = InventoryLineRefreshCoordinator.TryResortInventoryLinesAfterFullRefresh(__state.InventoryScreen);
            InventoryActionMenuCloseTimingObservability.EndInventoryRefresh(__state.TimingScope);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
