using System;
using System.Diagnostics;
using HarmonyLib;

namespace QudJP.Patches;

internal static class UITextSkinReflectionAccessor
{
    internal static string? GetCurrentText(object? uiTextSkin, string context)
    {
        if (uiTextSkin is null)
        {
            return null;
        }

        var textField = AccessTools.Field(uiTextSkin.GetType(), "text");
        if (textField?.FieldType == typeof(string))
        {
            return textField.GetValue(uiTextSkin) as string;
        }

        var textProperty = AccessTools.Property(uiTextSkin.GetType(), "Text");
        if (textProperty is null)
        {
            Trace.TraceWarning(
                "QudJP: {0}.GetCurrentText falling back to property 'text' for {1}.",
                context,
                uiTextSkin.GetType().FullName);
            textProperty = AccessTools.Property(uiTextSkin.GetType(), "text");
        }

        return textProperty is not null && textProperty.CanRead && textProperty.PropertyType == typeof(string)
            ? textProperty.GetValue(uiTextSkin) as string
            : null;
    }

    internal static bool SetCurrentText(object? uiTextSkin, string translated, string context)
    {
        if (uiTextSkin is null)
        {
            return false;
        }

        var setText = AccessTools.Method(uiTextSkin.GetType(), "SetText", new[] { typeof(string) });
        if (setText is not null)
        {
            _ = setText.Invoke(uiTextSkin, new object[] { translated });
            return true;
        }

        var textField = AccessTools.Field(uiTextSkin.GetType(), "text");
        if (textField?.FieldType == typeof(string))
        {
            textField.SetValue(uiTextSkin, translated);
            return true;
        }

        var textProperty = AccessTools.Property(uiTextSkin.GetType(), "Text");
        if (textProperty is null)
        {
            Trace.TraceWarning(
                "QudJP: {0}.SetCurrentText falling back to property 'text' for {1}.",
                context,
                uiTextSkin.GetType().FullName);
            textProperty = AccessTools.Property(uiTextSkin.GetType(), "text");
        }

        if (textProperty is not null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
        {
            textProperty.SetValue(uiTextSkin, translated);
            return true;
        }

        return false;
    }
}
