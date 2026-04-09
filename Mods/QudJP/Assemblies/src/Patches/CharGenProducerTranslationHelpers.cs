using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class CharGenProducerTranslationHelpers
{
    internal static readonly Func<string, string> TranslateStructuredText = ChargenStructuredTextTranslator.Translate;

    internal static string TranslateText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => ChargenStructuredTextTranslator.Translate(visible));
        return translated;
    }

    internal static void TranslateStringMember(object target, string memberName, string context)
    {
        TranslateStringMember(target, memberName, context, TranslateText);
    }

    internal static void TranslateStringMember(object target, string memberName, string context, Func<string, string> translateText)
    {
        if (!TryGetStringMemberValue(target, memberName, out var current)
            || string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = translateText(current!);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            SetStringMemberValue(target, memberName, translated, context);
        }
    }

    internal static IEnumerable MaterializeTranslatedEnumerable(IEnumerable values, string memberName, string context)
    {
        return MaterializeTranslatedEnumerable(values, memberName, context, TranslateText);
    }

    internal static IEnumerable MaterializeTranslatedEnumerable(
        IEnumerable values,
        string memberName,
        string context,
        Func<string, string> translateText)
    {
        var elementType = ResolveElementType(values.GetType());
        if (elementType is null)
        {
            Trace.TraceWarning(
                "QudJP: {0} could not resolve enumerable element type for '{1}', falling back to object.",
                context,
                values.GetType().FullName);
            elementType = typeof(object);
        }

        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
        var list = Activator.CreateInstance(listType);
        if (list is null)
        {
            Trace.TraceError("QudJP: {0} failed to create translated list for '{1}'.", context, listType.FullName);
            return values;
        }

        var addMethod = listType.GetMethod("Add", new[] { elementType });
        if (addMethod is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Add({1}) on '{2}'.", context, elementType.FullName, listType.FullName);
            return values;
        }

        foreach (var item in values)
        {
            if (item is not null)
            {
                TranslateStringMember(item, memberName, context, translateText);
            }

            addMethod.Invoke(list, new[] { item });
        }

        return (IEnumerable)list;
    }

    private static Type? ResolveElementType(Type sequenceType)
    {
        if (sequenceType.IsArray)
        {
            return sequenceType.GetElementType();
        }

        if (sequenceType.IsGenericType
            && string.Equals(sequenceType.GetGenericTypeDefinition().FullName, "System.Collections.Generic.IEnumerable`1", StringComparison.Ordinal))
        {
            return sequenceType.GetGenericArguments()[0];
        }

        var interfaces = sequenceType.GetInterfaces();
        for (var index = 0; index < interfaces.Length; index++)
        {
            var current = interfaces[index];
            if (current.IsGenericType
                && string.Equals(current.GetGenericTypeDefinition().FullName, "System.Collections.Generic.IEnumerable`1", StringComparison.Ordinal))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        value = null;
        var type = target.GetType();

        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            value = field.GetValue(target) as string;
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            value = property.GetValue(target) as string;
            return true;
        }

        return false;
    }

    private static void SetStringMemberValue(object target, string memberName, string value, string context)
    {
        var type = target.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
            return;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(target, value);
            return;
        }

        Trace.TraceWarning("QudJP: {0} could not set member '{1}' on '{2}'.", context, memberName, type.FullName);
    }
}
