using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace QudJP.Patches;

public static class InventoryActionMenuCursorSoundPatch
{
    private const string InventoryActionMenuPopupIdPrefix = "InventoryActionMenu:";
    private const string CursorSound = "Sounds/UI/ui_cursor_scroll";

    private static readonly object SyncRoot = new();
    private static readonly ConditionalWeakTable<object, PopupContext> PopupContexts = new();
    private static MethodInfo? playUiSoundMethod;
    private static Type? soundEffectType;
    private static Action<object?, string?>? playCursorSoundRequestObserverForTests;

    internal static void RememberPopupController(object? popupMessage)
    {
        var controller = GetControllerFromPopupMessage(popupMessage);
        if (controller is null)
        {
            return;
        }

        var popupId = GetPopupIdFromPopupMessage(popupMessage);
        lock (SyncRoot)
        {
            if (!PopupContexts.TryGetValue(controller, out var context))
            {
                context = new PopupContext();
                PopupContexts.Add(controller, context);
            }

            context.PopupIds.Push(popupId);
        }
    }

    internal static void ForgetPopupController(object? popupMessage)
    {
        var controller = GetControllerFromPopupMessage(popupMessage);
        if (controller is null)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (!PopupContexts.TryGetValue(controller, out var context))
            {
                return;
            }

            if (context.PopupIds.Count > 0)
            {
                _ = context.PopupIds.Pop();
            }

            if (context.PopupIds.Count == 0)
            {
                PopupContexts.Remove(controller);
            }
        }
    }

    internal static bool TryGetRememberedPopupId(object? controller, out string? popupId)
    {
        popupId = null;
        if (controller is null)
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (!PopupContexts.TryGetValue(controller, out var context))
            {
                return false;
            }

            if (context.PopupIds.Count == 0)
            {
                return false;
            }

            popupId = context.PopupIds.Peek();
            return true;
        }
    }

    internal static void PlayCursorSoundForInventoryActionMenuController(object? controller)
    {
        var hasPopupId = TryGetRememberedPopupId(controller, out var popupId);
        ObservePlayCursorSoundRequestForTests(controller, popupId);
        if (!hasPopupId
            || popupId is null
            || !popupId.StartsWith(InventoryActionMenuPopupIdPrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (PlayCursorSound())
        {
            LogCursorSoundPlayed(popupId);
        }
    }

    internal static void SetPlayCursorSoundRequestObserverForTests(Action<object?, string?>? observer)
    {
        lock (SyncRoot)
        {
            playCursorSoundRequestObserverForTests = observer;
        }
    }

    private static object? GetControllerFromPopupMessage(object? popupMessage)
    {
        if (popupMessage is null)
        {
            return null;
        }

        var field = GetControllerField(popupMessage.GetType());
        return field?.GetValue(popupMessage);
    }

    private static string? GetPopupIdFromPopupMessage(object? popupMessage)
    {
        if (popupMessage is null)
        {
            return null;
        }

        var field = GetPopupIdField(popupMessage.GetType());
        return field?.GetValue(popupMessage) as string;
    }

    private static FieldInfo? GetControllerField(Type popupMessageType)
    {
        return AccessTools.Field(popupMessageType, "controller");
    }

    private static FieldInfo? GetPopupIdField(Type popupMessageType)
    {
        return AccessTools.Field(popupMessageType, "PopupID");
    }

    private static void ObservePlayCursorSoundRequestForTests(object? controller, string? popupId)
    {
        Action<object?, string?>? observer;
        lock (SyncRoot)
        {
            observer = playCursorSoundRequestObserverForTests;
        }

        observer?.Invoke(controller, popupId);
    }

    private static bool PlayCursorSound()
    {
        var method = GetPlayUiSoundMethod();
        var effectType = GetSoundEffectType();
        if (method is null || effectType is null)
        {
            return false;
        }

        method.Invoke(null, new[] { CursorSound, 1f, false, true, Enum.ToObject(effectType, 0) });
        return true;
    }

    private static void LogCursorSoundPlayed(string? popupId)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] InventoryActionMenuCursorSound/v1: popup_id="
            + EscapeDetailValue(popupId ?? string.Empty)
            + ";source=play_click");
    }

    private static string EscapeDetailValue(string value)
    {
        return value.Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace("=", "\\=");
    }

    private static MethodInfo? GetPlayUiSoundMethod()
    {
        lock (SyncRoot)
        {
            if (playUiSoundMethod is not null)
            {
                return playUiSoundMethod;
            }

            var soundManagerType = AccessTools.TypeByName("SoundManager");
            var effectType = GetSoundEffectType();
            if (effectType is null)
            {
                return null;
            }

            playUiSoundMethod = AccessTools.Method(
                soundManagerType,
                "PlayUISound",
                new[] { typeof(string), typeof(float), typeof(bool), typeof(bool), effectType });
            return playUiSoundMethod;
        }
    }

    private static Type? GetSoundEffectType()
    {
        lock (SyncRoot)
        {
            soundEffectType ??= AccessTools.TypeByName("SoundRequest+SoundEffectType");
            return soundEffectType;
        }
    }

    private sealed class PopupContext
    {
        internal Stack<string?> PopupIds { get; } = new();
    }
}
