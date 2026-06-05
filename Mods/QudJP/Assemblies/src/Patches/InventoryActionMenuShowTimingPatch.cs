using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionMenuShowTimingPatch
{
    private const string Context = nameof(InventoryActionMenuShowTimingPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var equipmentApiType = AccessTools.TypeByName("Qud.API.EquipmentAPI");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionType = AccessTools.TypeByName("XRL.World.InventoryAction");
        if (equipmentApiType is null || gameObjectType is null || inventoryActionType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var actionTableType = typeof(Dictionary<,>).MakeGenericType(typeof(string), inventoryActionType);
        var comparerType = typeof(IComparer<>).MakeGenericType(inventoryActionType);
        var method = AccessTools.Method(
            equipmentApiType,
            "ShowInventoryActionMenu",
            new[]
            {
                actionTableType,
                gameObjectType,
                gameObjectType,
                typeof(bool),
                typeof(bool),
                typeof(string),
                comparerType,
                typeof(bool),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ShowInventoryActionMenu target not found.", Context);
        }

        return method;
    }

    public static void Prefix(object? __0, ref InventoryActionMenuCloseTimingObservability.TimingScope __state)
    {
        try
        {
            InventoryActionDisplayTranslationPatch.TranslateActionTableForInventoryActionMenu(__0);
            __state = InventoryActionMenuCloseTimingObservability.BeginMenu(GetCount(__0));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = InventoryActionMenuCloseTimingObservability.TimingScope.Empty;
        }
    }

    [HarmonyPrefix]
    public static void TranslateIntroPrefix(object? __2, ref string? __5)
    {
        try
        {
            TranslateFallbackIntroTitle(__2, ref __5);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateIntroPrefix failed: {1}", Context, ex);
        }
    }

    public static void Postfix(object? __result, InventoryActionMenuCloseTimingObservability.TimingScope __state)
    {
        try
        {
            InventoryActionMenuCloseTimingObservability.EndMenu(__state, __result is null);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static int GetCount(object? actionTable)
    {
        return actionTable is ICollection collection ? collection.Count : -1;
    }

    private static void TranslateFallbackIntroTitle(object? gameObject, ref string? intro)
    {
        if (!string.IsNullOrEmpty(intro) || gameObject is null)
        {
            return;
        }

        var displayName = AccessTools.Property(gameObject.GetType(), "DisplayName")?.GetValue(gameObject) as string
            ?? AccessTools.Field(gameObject.GetType(), "DisplayName")?.GetValue(gameObject) as string;
        if (string.IsNullOrEmpty(displayName))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=intro");
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            displayName,
            route);
        if (string.Equals(translated, displayName, StringComparison.Ordinal))
        {
            return;
        }

        intro = translated;
        DynamicTextObservability.RecordTransform(
            route,
            "InventoryActionMenu.Title",
            displayName,
            translated);
    }
}
