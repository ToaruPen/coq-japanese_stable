using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryActionMenuCursorSoundPatch
{
    private const string Context = nameof(InventoryActionMenuCursorSoundPatch);
    private const string TargetTypeName = "Qud.UI.PopupMessage";
    private const string InventoryActionMenuPopupIdPrefix = "InventoryActionMenu:";
    private const string CursorSound = "Sounds/UI/ui_cursor_scroll";
    private const int UnknownSelectedOption = int.MinValue;

    private static readonly object SyncRoot = new();
    private static FieldInfo? controllerField;
    private static FieldInfo? popupIdField;
    private static MethodInfo? playUiSoundMethod;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, "Update", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} method 'Update()' not found on '{1}'.", Context, TargetTypeName);
        }

        return method;
    }

    public static void Prefix(object? __instance, ref int __state)
    {
        try
        {
            __state = GetSelectedOption(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = UnknownSelectedOption;
        }
    }

    public static void Postfix(object? __instance, int __state)
    {
        try
        {
            if (__state == UnknownSelectedOption)
            {
                return;
            }

            var currentSelectedOption = GetSelectedOption(__instance);
            var popupId = GetPopupId(__instance);
            var isActiveAndEnabled = GetBooleanMember(__instance, "isActiveAndEnabled");
            if (ShouldPlayCursorSound(popupId, __state, currentSelectedOption, isActiveAndEnabled))
            {
                PlayCursorSound();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool ShouldPlayCursorSoundForTests(
        string? popupId,
        int previousSelectedOption,
        int currentSelectedOption,
        bool isActiveAndEnabled)
    {
        return ShouldPlayCursorSound(popupId, previousSelectedOption, currentSelectedOption, isActiveAndEnabled);
    }

    private static bool ShouldPlayCursorSound(
        string? popupId,
        int previousSelectedOption,
        int currentSelectedOption,
        bool isActiveAndEnabled)
    {
        return isActiveAndEnabled
            && popupId is not null
            && popupId.StartsWith(InventoryActionMenuPopupIdPrefix, StringComparison.Ordinal)
            && currentSelectedOption != UnknownSelectedOption
            && previousSelectedOption != currentSelectedOption;
    }

    private static int GetSelectedOption(object? popupMessage)
    {
        var controller = GetController(popupMessage);
        if (controller is null)
        {
            return UnknownSelectedOption;
        }

        var controllerType = controller.GetType();
        var selectedOptionProperty = AccessTools.Property(controllerType, "selectedOption");
        var value = selectedOptionProperty?.GetValue(controller);
        return value is int selectedOption ? selectedOption : UnknownSelectedOption;
    }

    private static object? GetController(object? popupMessage)
    {
        if (popupMessage is null)
        {
            return null;
        }

        var field = GetControllerField(popupMessage.GetType());
        return field?.GetValue(popupMessage);
    }

    private static string? GetPopupId(object? popupMessage)
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
        lock (SyncRoot)
        {
            controllerField ??= AccessTools.Field(popupMessageType, "controller");
            return controllerField;
        }
    }

    private static FieldInfo? GetPopupIdField(Type popupMessageType)
    {
        lock (SyncRoot)
        {
            popupIdField ??= AccessTools.Field(popupMessageType, "PopupID");
            return popupIdField;
        }
    }

    private static bool GetBooleanMember(object? instance, string memberName)
    {
        if (instance is null)
        {
            return false;
        }

        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.PropertyType == typeof(bool))
        {
            return property.GetValue(instance) is true;
        }

        var field = AccessTools.Field(type, memberName);
        return field is not null && field.FieldType == typeof(bool) && field.GetValue(instance) is true;
    }

    private static void PlayCursorSound()
    {
        var method = GetPlayUiSoundMethod();
        method?.Invoke(null, new object[] { CursorSound, 1f, false, true });
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
            playUiSoundMethod = AccessTools.Method(
                soundManagerType,
                "PlayUISound",
                new[] { typeof(string), typeof(float), typeof(bool), typeof(bool) });
            return playUiSoundMethod;
        }
    }
}
