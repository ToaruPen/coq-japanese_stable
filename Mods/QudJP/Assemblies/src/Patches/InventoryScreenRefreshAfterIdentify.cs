using System;
using System.Collections;
using System.Globalization;
using HarmonyLib;

namespace QudJP.Patches;

internal static class InventoryScreenRefreshAfterIdentify
{
    private static Func<object?>? inventoryScreenProviderForTests;
    private static Action<object>? inventoryScreenRefreshForTests;

    internal static void SetInventoryScreenRefreshHooksForTests(
        Func<object?>? screenProvider,
        Action<object>? screenRefresher)
    {
        inventoryScreenProviderForTests = screenProvider;
        inventoryScreenRefreshForTests = screenRefresher;
    }

    internal static bool TryRefresh()
    {
        var screen = inventoryScreenProviderForTests?.Invoke() ?? GetInventoryAndEquipmentStatusScreenInstance();
        if (screen is null)
        {
            LogRefreshProbe("screen_missing", null);
            return false;
        }

        var refreshForTests = inventoryScreenRefreshForTests;
        if (refreshForTests is not null)
        {
            InventoryActionMenuCloseTimingObservability.RunWithInventoryRefreshSuppressionBypassed(() => refreshForTests(screen));
            LogRefreshProbe("refreshed_test_hook", screen.GetType());
            return true;
        }

        var method = AccessTools.Method(screen.GetType(), "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            LogRefreshProbe("method_missing", screen.GetType());
            return false;
        }

        InventoryActionMenuCloseTimingObservability.RunWithInventoryRefreshSuppressionBypassed(() => method.Invoke(screen, null));
        LogRefreshProbe("refreshed", screen.GetType());
        return true;
    }

    private static object? GetInventoryAndEquipmentStatusScreenInstance()
    {
        var screenType = AccessTools.TypeByName("Qud.UI.InventoryAndEquipmentStatusScreen");
        if (screenType is null)
        {
            return null;
        }

        var screen = GetStaticInstance(screenType);
        if (screen is not null)
        {
            return screen;
        }

        var statusScreensType = AccessTools.TypeByName("Qud.UI.StatusScreensScreen");
        var statusScreens = statusScreensType is null ? null : GetStaticInstance(statusScreensType);
        return GetInventoryScreenFromStatusScreens(statusScreens, screenType);
    }

    internal static object? GetStaticInstanceForTests(Type type)
    {
        return GetStaticInstance(type);
    }

    internal static object? GetInventoryScreenFromStatusScreensForTests(object? statusScreensScreen, Type screenType)
    {
        return GetInventoryScreenFromStatusScreens(statusScreensScreen, screenType);
    }

    private static object? GetStaticInstance(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = AccessTools.Field(current, "instance");
            var fieldValue = field?.GetValue(null);
            if (fieldValue is not null)
            {
                return fieldValue;
            }

            var property = AccessTools.Property(current, "instance");
            var propertyValue = property?.GetValue(null);
            if (propertyValue is not null)
            {
                return propertyValue;
            }
        }

        return null;
    }

    private static object? GetInventoryScreenFromStatusScreens(object? statusScreensScreen, Type screenType)
    {
        if (statusScreensScreen is null)
        {
            return null;
        }

        var activeScreen = AccessTools.Field(statusScreensScreen.GetType(), "activeScreen")?.GetValue(statusScreensScreen);
        if (activeScreen is not null && screenType.IsInstanceOfType(activeScreen))
        {
            return activeScreen;
        }

        var screens = AccessTools.Field(statusScreensScreen.GetType(), "Screens")?.GetValue(statusScreensScreen)
            ?? AccessTools.Property(statusScreensScreen.GetType(), "Screens")?.GetValue(statusScreensScreen);
        if (screens is not IEnumerable enumerable)
        {
            return null;
        }

        foreach (var entry in enumerable)
        {
            var screen = ResolveScreenComponent(entry, screenType);
            if (screen is not null)
            {
                return screen;
            }
        }

        return null;
    }

    private static object? ResolveScreenComponent(object? entry, Type screenType)
    {
        if (entry is null)
        {
            return null;
        }

        if (screenType.IsInstanceOfType(entry))
        {
            return entry;
        }

        var getComponent = AccessTools.Method(entry.GetType(), "GetComponent", new[] { typeof(Type) });
        var component = getComponent?.Invoke(entry, new object[] { screenType });
        return component is not null && screenType.IsInstanceOfType(component) ? component : null;
    }

    private static void LogRefreshProbe(string result, Type? screenType)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] IdentifyInventoryRefresh/v1: result="
            + Escape(result)
            + ";screen_type="
            + Escape(screenType?.FullName ?? "<missing>")
            + ";timestamp_utc_ticks="
            + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace("=", "\\=")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}
