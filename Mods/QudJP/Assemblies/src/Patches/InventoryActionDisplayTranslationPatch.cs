using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryActionDisplayTranslationPatch
{
    private const string Context = nameof(InventoryActionDisplayTranslationPatch);
    private const string InventoryActionContext = "XRL.World.IInventoryActionsEvent";
    private const string InventoryActionDictionaryFile = "ui-inventory-actions.ja.json";

    private static readonly Regex RechargeDisplayPattern = new(
        "^recharge (?<cell>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var ownerGetInventoryActionsEventType = AccessTools.TypeByName("XRL.World.OwnerGetInventoryActionsEvent");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionType = AccessTools.TypeByName("XRL.World.InventoryAction");
        if (ownerGetInventoryActionsEventType is null || gameObjectType is null || inventoryActionType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var actionTableType = typeof(Dictionary<,>).MakeGenericType(typeof(string), inventoryActionType);
        var method = AccessTools.Method(
            ownerGetInventoryActionsEventType,
            "Send",
            new[] { gameObjectType, gameObjectType, actionTableType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.OwnerGetInventoryActionsEvent.Send target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __2)
    {
        try
        {
            TranslateActionTable(__2);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateActionTableForTests(object? actionTable)
    {
        TranslateActionTable(actionTable);
    }

    private static void TranslateActionTable(object? actionTable)
    {
        if (actionTable is null || actionTable is string || actionTable is not IEnumerable entries)
        {
            return;
        }

        foreach (var entry in entries)
        {
            var action = GetActionFromEntry(entry);
            if (action is null)
            {
                continue;
            }

            TranslateActionDisplay(action);
        }
    }

    private static object? GetActionFromEntry(object? entry)
    {
        if (entry is null)
        {
            return null;
        }

        var valueProperty = AccessTools.Property(entry.GetType(), "Value");
        return valueProperty?.GetValue(entry);
    }

    private static void TranslateActionDisplay(object action)
    {
        var display = GetStringMember(action, "Display");
        if (string.IsNullOrWhiteSpace(display))
        {
            return;
        }

        var original = display!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(original, out var unmarked))
        {
            SetStringMember(action, "Display", unmarked);
            DynamicTextObservability.RecordTransform(Context, "InventoryAction.Display", original, unmarked);
            return;
        }

        if (!TryTranslateDisplay(action, original, out var translated)
            || string.Equals(translated, original, StringComparison.Ordinal))
        {
            return;
        }

        SetStringMember(action, "Display", translated);
        DynamicTextObservability.RecordTransform(Context, "InventoryAction.Display", original, translated);
    }

    private static bool TryTranslateDisplay(object action, string display, out string translated)
    {
        var exact = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
            display,
            InventoryActionContext,
            InventoryActionDictionaryFile);
        if (exact is not null)
        {
            translated = ApplyHotkeyPrefix(action, exact);
            return true;
        }

        var match = RechargeDisplayPattern.Match(ColorAwareTranslationComposer.GetVisibleText(display));
        if (!match.Success)
        {
            translated = display;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(display);
        var strippedMatch = RechargeDisplayPattern.Match(stripped);
        if (!strippedMatch.Success)
        {
            translated = display;
            return false;
        }

        var cell = ColorAwareTranslationComposer.RestoreCapture(
            strippedMatch.Groups["cell"].Value,
            spans,
            strippedMatch.Groups["cell"]).Trim();
        if (cell.Length == 0)
        {
            translated = display;
            return false;
        }

        var translatedCell = ColorAwareTranslationComposer.TranslatePreservingColors(
            cell,
            GetDisplayNameRouteTranslator.TranslateScopedExactPreservingColors);
        translated = ApplyHotkeyPrefix(action, translatedCell + "を充電する");
        return true;
    }

    private static string ApplyHotkeyPrefix(object action, string translated)
    {
        var key = GetCharMember(action, "Key");
        if (key.HasValue
            && key.Value != '\0'
            && key.Value != ' '
            && translated.IndexOf("{{hotkey|", StringComparison.Ordinal) < 0)
        {
            return "{{hotkey|" + key.Value + "}}" + translated;
        }

        return translated;
    }

    private static string? GetStringMember(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            return property.GetValue(instance) as string;
        }

        var field = AccessTools.Field(type, memberName);
        return field?.FieldType == typeof(string)
            ? field.GetValue(instance) as string
            : null;
    }

    private static void SetStringMember(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        if (field?.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
        }
    }

    private static char? GetCharMember(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(char))
        {
            return (char?)property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.FieldType == typeof(char)
            ? (char?)field.GetValue(instance)
            : null;
    }
}
