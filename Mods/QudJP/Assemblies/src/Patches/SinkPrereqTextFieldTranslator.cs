using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class SinkPrereqTextFieldTranslator
{
    private static readonly ConcurrentDictionary<(Type Type, string MemberName), MemberInfo?> MemberCache =
        new ConcurrentDictionary<(Type Type, string MemberName), MemberInfo?>();

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
        MemberCache.Clear();
    }

    private static bool TryGetMemberValue(object instance, string memberName, out object? value)
    {
        var member = MemberCache.GetOrAdd((instance.GetType(), memberName), static key =>
        {
            var field = AccessTools.Field(key.Type, key.MemberName);
            if (field is not null)
            {
                return field;
            }

            var property = AccessTools.Property(key.Type, key.MemberName);
            return property is not null && property.CanRead
                ? property
                : null;
        });

        if (member is FieldInfo field)
        {
            value = field.GetValue(instance);
            return true;
        }

        if (member is PropertyInfo property)
        {
            value = property.GetValue(instance);
            return true;
        }

        value = null;
        return false;
    }
}
