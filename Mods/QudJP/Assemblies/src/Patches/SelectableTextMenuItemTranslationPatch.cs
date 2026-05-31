using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SelectableTextMenuItemTranslationPatch
{
    private const string Context = nameof(SelectableTextMenuItemTranslationPatch);
    private const int MaxDisplayTranslationCacheEntries = 2048;
    private const string TargetTypeName = "Qud.UI.SelectableTextMenuItem";
    private static readonly ConcurrentDictionary<DisplayTranslationCacheKey, string> DisplayTranslationCache = new();
    private static readonly ConcurrentQueue<DisplayTranslationCacheKey> DisplayTranslationCacheOrder = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> SetTextMethods = new();
    private static ConditionalWeakTable<object, AppliedDisplayTranslationState> AppliedDisplayTranslationStates = new();
    private static readonly Regex NestedHotkeyLabelPattern =
        new Regex(
            @"^\{\{(?<labelColor>[A-Za-z]+)\|\{\{(?<hotkeyColor>[A-Za-z]+)\|(?<hotkey>\[[^\]]+\])\}\}\s*(?<label>.*?)\}\}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: {Context} target type '{TargetTypeName}' not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "SelectChanged", new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError($"QudJP: {Context} method 'SelectChanged(bool)' not found on '{TargetTypeName}'.");
        }

        return method;
    }

    public static void Postfix(object? __instance, bool newState)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            ApplyDisplayTranslationIfChanged(__instance, newState, popupIdOverride: null, vanillaTextAlreadyApplied: true);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void ApplyDisplayTranslationIfChanged(
        object? instance,
        bool selected,
        string? popupIdOverride = null,
        bool vanillaTextAlreadyApplied = false)
    {
        if (instance is null)
        {
            return;
        }

        var itemText = ReflectionUtils.GetPropertyOrFieldValue(instance, "itemText") as string;
        if (itemText is null || itemText.Length == 0)
        {
            return;
        }

        var popupId = popupIdOverride ?? TryGetPopupIdFromParentPopup(instance);
        var key = new AppliedDisplayTranslationKey(itemText, popupId, selected);
        var state = AppliedDisplayTranslationStates.GetOrCreateValue(instance);
        if (state.HasLastKey && state.LastKey.Equals(key))
        {
            return;
        }

        var translated = TranslateMenuItemTextForDisplay(itemText, popupId);
        if (vanillaTextAlreadyApplied && string.Equals(translated, itemText, StringComparison.Ordinal))
        {
            state.SetLastKey(key);
            return;
        }

        var item = ReflectionUtils.GetPropertyOrFieldValue(instance, "item");
        var setText = item is null ? null : GetSetTextMethod(item.GetType());
        if (setText is null)
        {
            return;
        }

        setText.Invoke(item, new object[] { WrapForSelection(translated, selected) });
        state.SetLastKey(key);
    }

    internal static string TranslateMenuItemTextForDisplay(string source)
    {
        return TranslateMenuItemTextForDisplay(source, popupId: null);
    }

    internal static string TranslateMenuItemTextForDisplay(string source, string? popupId)
    {
        var key = new DisplayTranslationCacheKey(source, popupId);
        if (DisplayTranslationCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var translated = TranslateMenuItemTextForDisplayUncached(key);
        if (DisplayTranslationCache.TryAdd(key, translated))
        {
            RememberDisplayTranslationKey(key);
        }

        return translated;
    }

    private static MethodInfo? GetSetTextMethod(Type itemType)
    {
        if (SetTextMethods.TryGetValue(itemType, out var cached))
        {
            return cached;
        }

        var method = AccessTools.Method(itemType, "SetText", new[] { typeof(string) });
        if (method is not null)
        {
            SetTextMethods.TryAdd(itemType, method);
        }

        return method;
    }

    internal static void ClearDisplayTranslationCacheForTests()
    {
        DisplayTranslationCache.Clear();
        while (DisplayTranslationCacheOrder.TryDequeue(out var ignored))
        {
            _ = ignored;
        }

        AppliedDisplayTranslationStates = new ConditionalWeakTable<object, AppliedDisplayTranslationState>();
    }

    internal static int GetDisplayTranslationCacheCountForTests()
    {
        return DisplayTranslationCache.Count;
    }

    private static string TranslateMenuItemTextForDisplayUncached(DisplayTranslationCacheKey key)
    {
        var normalized = NormalizeNestedHotkeyLabelForDisplay(key.Source);
        return MessageFrameTranslator.StripAllDirectTranslationMarkers(
            PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(normalized, Context, key.PopupId));
    }

    private static void RememberDisplayTranslationKey(DisplayTranslationCacheKey key)
    {
        DisplayTranslationCacheOrder.Enqueue(key);
        while (DisplayTranslationCache.Count > MaxDisplayTranslationCacheEntries
            && DisplayTranslationCacheOrder.TryDequeue(out var oldest))
        {
            DisplayTranslationCache.TryRemove(oldest, out _);
        }
    }

    internal static string NormalizeNestedHotkeyLabelForDisplay(string source)
    {
        var match = NestedHotkeyLabelPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var hotkey = match.Groups["hotkey"].Value;
        var label = match.Groups["label"].Value;
        if (label.Length == 0)
        {
            return source;
        }

        return "{{"
            + match.Groups["hotkeyColor"].Value
            + "|"
            + hotkey
            + "}} {{"
            + match.Groups["labelColor"].Value
            + "|"
            + label
            + "}}";
    }

    internal static string WrapForSelection(string source, bool selected)
    {
        return selected ? "{{W|" + source + "}}" : "{{c|" + source + "}}";
    }

    private static string? TryGetPopupIdFromParentPopup(object instance)
    {
        var popupMessageType = AccessTools.TypeByName("Qud.UI.PopupMessage");
        if (popupMessageType is null)
        {
            return null;
        }

        var popupMessage = TryGetParentPopupMessage(instance, popupMessageType);
        if (popupMessage is null)
        {
            return TryGetLastPopupId(popupMessageType);
        }

        var popupId = AccessTools.Field(popupMessageType, "PopupID")?.GetValue(popupMessage) as string;
        if (popupId is null)
        {
            popupId = TryGetLastPopupId(popupMessageType);
        }

        return popupId;
    }

    private static object? TryGetParentPopupMessage(object instance, Type popupMessageType)
    {
        var componentType = AccessTools.TypeByName("UnityEngine.Component");
        if (componentType is null || !componentType.IsInstanceOfType(instance))
        {
            return null;
        }

        var includeInactiveMethod = AccessTools.Method(
            componentType,
            "GetComponentInParent",
            new[] { typeof(Type), typeof(bool) });
        if (includeInactiveMethod is not null)
        {
            return includeInactiveMethod.Invoke(instance, new object[] { popupMessageType, true });
        }

        var method = AccessTools.Method(componentType, "GetComponentInParent", new[] { typeof(Type) });
        return method?.Invoke(instance, new object[] { popupMessageType });
    }

    private static string? TryGetLastPopupId(Type popupMessageType)
    {
        return AccessTools.Field(popupMessageType, "lastPopupID")?.GetValue(null) as string;
    }

    private sealed class AppliedDisplayTranslationState
    {
        internal bool HasLastKey { get; private set; }

        internal AppliedDisplayTranslationKey LastKey { get; private set; }

        internal void SetLastKey(AppliedDisplayTranslationKey key)
        {
            LastKey = key;
            HasLastKey = true;
        }
    }

    private readonly struct AppliedDisplayTranslationKey : IEquatable<AppliedDisplayTranslationKey>
    {
        internal AppliedDisplayTranslationKey(string source, string? popupId, bool selected)
        {
            Source = source;
            PopupId = popupId;
            Selected = selected;
        }

        internal string Source { get; }

        internal string? PopupId { get; }

        internal bool Selected { get; }

        public bool Equals(AppliedDisplayTranslationKey other)
        {
            return string.Equals(Source, other.Source, StringComparison.Ordinal)
                && string.Equals(PopupId, other.PopupId, StringComparison.Ordinal)
                && Selected == other.Selected;
        }

        public override bool Equals(object? obj)
        {
            return obj is AppliedDisplayTranslationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(Source);
                hash = (hash * 397) ^ (PopupId is null ? 0 : StringComparer.Ordinal.GetHashCode(PopupId));
                hash = (hash * 397) ^ Selected.GetHashCode();
                return hash;
            }
        }
    }

    private readonly struct DisplayTranslationCacheKey : IEquatable<DisplayTranslationCacheKey>
    {
        internal DisplayTranslationCacheKey(string source, string? popupId)
        {
            Source = source;
            PopupId = popupId ?? string.Empty;
        }

        internal string Source { get; }

        internal string PopupId { get; }

        public bool Equals(DisplayTranslationCacheKey other)
        {
            return string.Equals(Source, other.Source, StringComparison.Ordinal)
                && string.Equals(PopupId, other.PopupId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is DisplayTranslationCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Source) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(PopupId);
            }
        }
    }

}
