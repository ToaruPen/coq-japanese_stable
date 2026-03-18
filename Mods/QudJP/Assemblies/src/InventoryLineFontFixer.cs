#if HAS_TMP
using TMPro;
using UnityEngine;
#endif
using System;

namespace QudJP;

internal static class InventoryLineFontFixer
{
#if HAS_TMP
    internal static bool TryApplyPrimaryFontToItemRow(object? inventoryLineInstance, object? data)
    {
        if (inventoryLineInstance is null || data is null)
        {
            return false;
        }

        if (!TryGetBooleanPropertyOrField(data, "category", out var isCategory) || isCategory)
        {
            return false;
        }

        var textSkin = GetPropertyOrFieldValue(inventoryLineInstance, "text");
        if (textSkin is not Component component)
        {
            return false;
        }

        var tmp = component.GetComponent<TextMeshProUGUI>();
        if (tmp is null)
        {
            return false;
        }

        var displayName = TryGetStringPropertyOrField(data, "displayName");
        _ = FontManager.TryWarmPrimaryFontCharactersForUi(displayName);
        FontManager.ApplyToText(tmp);
        return true;
    }

    internal static int TryApplyPrimaryFontToAllTextChildren(object? inventoryLineInstance)
    {
        if (inventoryLineInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var applied = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            FontManager.ApplyToText(texts[index]);
            applied++;
        }

        return applied;
    }

    private static string? TryGetStringPropertyOrField(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(instance) as string;
        }

        return Access(instance, memberName) as string;
    }

    private static bool TryGetBooleanPropertyOrField(object instance, string memberName, out bool value)
    {
        value = false;

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
        {
            value = property.GetValue(instance) as bool? ?? false;
            return true;
        }

        var field = Access(instance, memberName);
        if (field is bool fieldValue)
        {
            value = fieldValue;
            return true;
        }

        return false;
    }

    private static object? GetPropertyOrFieldValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(instance);
        }

        return Access(instance, memberName);
    }

    private static object? Access(object instance, string memberName)
    {
        var type = instance.GetType();
        var field = type.GetField(memberName);
        if (field is not null)
        {
            return field.GetValue(instance);
        }

#pragma warning disable S3011
        var nonPublicField = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
#pragma warning restore S3011
        return nonPublicField?.GetValue(instance);
    }
#endif
}
