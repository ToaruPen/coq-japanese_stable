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

        var cacheKey = string.Concat(instance.GetType().FullName, ".", fieldName);
        var field = FieldCache.GetOrAdd(cacheKey, _ => AccessTools.Field(instance.GetType(), fieldName));
        if (field is null)
        {
            return;
        }

        var uiTextSkin = field.GetValue(instance);
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
            DynamicTextObservability.RecordTransform(
                context, "SinkPrereq.FieldTranslation", currentText, translated);
        }
    }

    internal static void TranslateChainedField(
        object? instance, string parentFieldName, string textSkinFieldName, string context)
    {
        if (instance is null)
        {
            return;
        }

        var parentCacheKey = string.Concat(instance.GetType().FullName, ".", parentFieldName);
        var parentField = FieldCache.GetOrAdd(parentCacheKey, _ => AccessTools.Field(instance.GetType(), parentFieldName));

        object? parent = null;
        if (parentField is not null)
        {
            parent = parentField.GetValue(instance);
        }
        else
        {
            var prop = AccessTools.Property(instance.GetType(), parentFieldName);
            if (prop is not null && prop.CanRead)
            {
                parent = prop.GetValue(instance);
            }
        }

        if (parent is not null)
        {
            TranslateField(parent, textSkinFieldName, context);
        }
    }

    internal static void ResetForTests()
    {
        FieldCache.Clear();
    }
}
