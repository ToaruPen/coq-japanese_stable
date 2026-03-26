using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class SinkPrereqTextFieldTranslator
{
    private static readonly ConcurrentDictionary<string, FieldInfo?> FieldCache =
        new ConcurrentDictionary<string, FieldInfo?>(StringComparer.Ordinal);

    internal static void TranslateField(object? instance, string fieldName, string context)
    {
        if (instance is null)
        {
            return;
        }

        if (!TryGetMemberValue(instance, fieldName, out var uiTextSkin))
        {
            return;
        }

        TranslateTextSkin(uiTextSkin, context);
    }

    internal static void TranslateTextSkin(object? uiTextSkin, string context)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, context);
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(currentText, context);
        if (!string.Equals(currentText, translated, StringComparison.Ordinal))
        {
            UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, context);
        }
    }

    internal static void TranslateChainedField(
        object? instance, string parentFieldName, string textSkinFieldName, string context)
    {
        if (instance is null)
        {
            return;
        }

        if (TryGetMemberValue(instance, parentFieldName, out var parent)
            && parent is not null
            && TryGetMemberValue(parent, textSkinFieldName, out var uiTextSkin))
        {
            TranslateTextSkin(uiTextSkin, context);
        }
    }

    internal static void ResetForTests()
    {
        FieldCache.Clear();
    }

    private static bool TryGetMemberValue(object instance, string memberName, out object? value)
    {
        var cacheKey = string.Concat(instance.GetType().FullName, ".", memberName);
        var field = FieldCache.GetOrAdd(cacheKey, _ => AccessTools.Field(instance.GetType(), memberName));
        if (field is not null)
        {
            value = field.GetValue(instance);
            return true;
        }

        var property = AccessTools.Property(instance.GetType(), memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(instance);
            return true;
        }

        value = null;
        return false;
    }
}
