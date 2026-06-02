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

    private static Predicate<char>? keyMappedPredicateForTests;

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
            TranslateActionTable(__2, IsInventoryActionKeyMapped);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateActionTableForTests(object? actionTable)
    {
        TranslateActionTable(actionTable, keyMappedPredicateForTests ?? (_ => false));
    }

    internal static void SetInventoryActionKeyMappedPredicateForTests(Predicate<char>? predicate)
    {
        keyMappedPredicateForTests = predicate;
    }

    private static void TranslateActionTable(object? actionTable, Predicate<char> isKeyMapped)
    {
        if (actionTable is null || actionTable is string || actionTable is not IEnumerable entries)
        {
            return;
        }

        var actions = new List<object>();
        foreach (var entry in entries)
        {
            var action = GetActionFromEntry(entry);
            if (action is null)
            {
                continue;
            }

            actions.Add(action);
        }

        var states = new List<ActionTranslationState>(actions.Count);
        foreach (var action in actions)
        {
            states.Add(TranslateActionDisplay(action));
        }

        AssignFallbackHotkeys(states, isKeyMapped);
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

    private static ActionTranslationState TranslateActionDisplay(object action)
    {
        var display = GetStringMember(action, "Display");
        if (string.IsNullOrWhiteSpace(display))
        {
            return new ActionTranslationState(action, null);
        }

        var original = display!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(original, out var unmarked))
        {
            SetStringMember(action, "Display", unmarked);
            DynamicTextObservability.RecordTransform(Context, "InventoryAction.Display", original, unmarked);
            return new ActionTranslationState(action, null);
        }

        if (!TryTranslateDisplay(original, out var translated, out var fallbackKeyCandidates)
            || string.Equals(translated, original, StringComparison.Ordinal))
        {
            return new ActionTranslationState(action, null);
        }

        SetStringMember(action, "Display", translated);
        DynamicTextObservability.RecordTransform(Context, "InventoryAction.Display", original, translated);
        return new ActionTranslationState(action, fallbackKeyCandidates);
    }

    private static bool TryTranslateDisplay(string display, out string translated, out string? fallbackKeyCandidates)
    {
        fallbackKeyCandidates = BuildFallbackHotkeyCandidates(display);
        var exact = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
            display,
            InventoryActionContext,
            InventoryActionDictionaryFile);
        if (exact is not null)
        {
            translated = exact;
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
        translated = translatedCell + "を充電する";
        return true;
    }

    private static string BuildFallbackHotkeyCandidates(string display)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(display);
        var candidates = new List<char>();
        foreach (var character in visible)
        {
            if (!IsAsciiLetter(character))
            {
                continue;
            }

            var candidate = char.ToLowerInvariant(character);
            if (!candidates.Contains(candidate))
            {
                candidates.Add(candidate);
            }
        }

        return new string(candidates.ToArray());
    }

    private static void AssignFallbackHotkeys(IEnumerable<ActionTranslationState> states, Predicate<char> isKeyMapped)
    {
        var sortedStates = new List<ActionTranslationState>(states);
        sortedStates.Sort(static (left, right) => CompareInventoryActions(left.Action, right.Action));

        var assignedKeys = new HashSet<char>();
        foreach (var state in sortedStates)
        {
            var currentKey = GetCharMember(state.Action, "Key");
            if (!IsUsableInventoryActionKey(currentKey))
            {
                continue;
            }

            if (!isKeyMapped(currentKey!.Value) && !assignedKeys.Contains(currentKey.Value))
            {
                assignedKeys.Add(currentKey.Value);
                continue;
            }

            if (!state.HasFallbackHotkeyCandidates)
            {
                continue;
            }

            if (!TryAssignFallbackHotkey(state, assignedKeys, isKeyMapped))
            {
                SetCharMember(state.Action, "Key", ' ');
            }
        }
    }

    private static bool TryAssignFallbackHotkey(
        ActionTranslationState state,
        ISet<char> assignedKeys,
        Predicate<char> isKeyMapped)
    {
        foreach (var candidate in state.FallbackHotkeyCandidates!)
        {
            if (!IsUsableInventoryActionKey(candidate)
                || isKeyMapped(candidate)
                || IsReservedActionKey(candidate, assignedKeys))
            {
                continue;
            }

            SetCharMember(state.Action, "Key", candidate);
            assignedKeys.Add(candidate);
            return true;
        }

        return false;
    }

    private static int CompareInventoryActions(object left, object right)
    {
        var leftKeyValue = GetCharMember(left, "Key");
        var rightKeyValue = GetCharMember(right, "Key");
        var leftKey = leftKeyValue.HasValue ? leftKeyValue.Value : ' ';
        var rightKey = rightKeyValue.HasValue ? rightKeyValue.Value : ' ';
        var leftKeyMissing = leftKey == ' ';
        var rightKeyMissing = rightKey == ' ';
        if (!leftKeyMissing && !rightKeyMissing)
        {
            var keyComparison = char.ToUpperInvariant(leftKey).CompareTo(char.ToUpperInvariant(rightKey));
            if (keyComparison != 0)
            {
                return keyComparison;
            }

            if (leftKey != rightKey)
            {
                return -leftKey.CompareTo(rightKey);
            }

            var defaultComparison = GetIntMember(left, "Default").CompareTo(GetIntMember(right, "Default"));
            if (defaultComparison != 0)
            {
                return -defaultComparison;
            }
        }
        else if (leftKeyMissing || rightKeyMissing)
        {
            return leftKeyMissing.CompareTo(rightKeyMissing);
        }

        var priorityComparison = GetIntMember(left, "Priority").CompareTo(GetIntMember(right, "Priority"));
        if (priorityComparison != 0)
        {
            return -priorityComparison;
        }

        var leftDisplay = GetStringMember(left, "Display");
        var rightDisplay = GetStringMember(right, "Display");
        return -Comparer<string>.Default.Compare(leftDisplay is null ? string.Empty : leftDisplay, rightDisplay is null ? string.Empty : rightDisplay);
    }

    private static bool IsReservedActionKey(char key, IEnumerable<char> reservedKeys)
    {
        var upper = char.ToUpperInvariant(key);
        var lower = char.ToLowerInvariant(key);
        foreach (var reservedKey in reservedKeys)
        {
            if (reservedKey == key
                || char.ToUpperInvariant(reservedKey) == upper
                || char.ToLowerInvariant(reservedKey) == lower)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiLetter(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool IsUsableInventoryActionKey(char? key)
    {
        return key.HasValue && key.Value != '\0' && key.Value != ' ';
    }

    private static bool IsInventoryActionKeyMapped(char key)
    {
        try
        {
            var controlManagerType = AccessTools.TypeByName("ControlManager");
            var isKeyMappedMethod = AccessTools.Method(
                controlManagerType,
                "isKeyMapped",
                new[] { typeof(char), typeof(List<string>) });
            return isKeyMappedMethod?.Invoke(
                null,
                new object[] { key, new List<string> { "UINav", "Menus" } }) is true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: {0}.IsInventoryActionKeyMapped failed for '{1}': {2}", Context, key, ex);
            return false;
        }
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

    private static int GetIntMember(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(int))
        {
            var value = property.GetValue(instance);
            return value is int integer ? integer : 0;
        }

        var field = AccessTools.Field(type, memberName);
        if (field?.FieldType != typeof(int))
        {
            return 0;
        }

        var fieldValue = field.GetValue(instance);
        return fieldValue is int fieldInteger ? fieldInteger : 0;
    }

    private static void SetCharMember(object instance, string memberName, char value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(char))
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        if (field?.FieldType == typeof(char))
        {
            field.SetValue(instance, value);
        }
    }

    private sealed class ActionTranslationState
    {
        public ActionTranslationState(object action, string? fallbackHotkeyCandidates)
        {
            Action = action;
            FallbackHotkeyCandidates = fallbackHotkeyCandidates;
        }

        public object Action { get; }

        public string? FallbackHotkeyCandidates { get; }

        public bool HasFallbackHotkeyCandidates => !string.IsNullOrEmpty(FallbackHotkeyCandidates);
    }
}
