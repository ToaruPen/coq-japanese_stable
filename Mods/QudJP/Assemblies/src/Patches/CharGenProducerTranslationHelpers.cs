using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class CharGenProducerTranslationHelpers
{
    internal static readonly Func<string, string> TranslateStructuredText = ChargenStructuredTextTranslator.Translate;

    private readonly struct StringMemberUpdate
    {
        internal StringMemberUpdate(object target, string memberName, string value)
        {
            Target = target;
            MemberName = memberName;
            Value = value;
        }

        internal object Target { get; }

        internal string MemberName { get; }

        internal string Value { get; }
    }

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

    private static string TranslateStructuredTextWithDictionaryFallback(string source)
    {
        var structured = TranslateStructuredText(source);
        return string.Equals(structured, source, StringComparison.Ordinal)
            ? TranslateText(source)
            : structured;
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

        var updates = new System.Collections.Generic.List<StringMemberUpdate>();
        foreach (var item in values)
        {
            if (item is not null)
            {
                CollectStringMemberTranslation(item, memberName, translateText, updates);
            }

            addMethod.Invoke(list, new[] { item });
        }

        ApplyStringMemberUpdates(updates, context);
        return (IEnumerable)list;
    }

    private static IEnumerable MaterializeTranslatedEnumerable(
        IEnumerable values,
        string context,
        Action<object, string, System.Collections.Generic.List<StringMemberUpdate>> collectItemUpdates)
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

        var updates = new System.Collections.Generic.List<StringMemberUpdate>();
        foreach (var item in values)
        {
            if (item is not null)
            {
                collectItemUpdates(item, context, updates);
            }

            addMethod.Invoke(list, new[] { item });
        }

        ApplyStringMemberUpdates(updates, context);
        return (IEnumerable)list;
    }

    internal static IEnumerable MaterializeTranslatedFrameworkDataEnumerable(IEnumerable values, string context)
    {
        return MaterializeTranslatedEnumerable(values, context, CollectFrameworkDataElementTreeTranslations);
    }

    internal static IEnumerable MaterializeTranslatedPregenSelectionEnumerable(IEnumerable values, string context)
    {
        return MaterializeTranslatedEnumerable(
            values,
            context,
            static (item, _, updates) =>
            {
                CollectStringMemberTranslation(item, "Title", TranslateText, updates);
                CollectStringMemberTranslation(item, "Description", TranslateStructuredText, updates);
            });
    }

    private static void CollectFrameworkDataElementTreeTranslations(
        object item,
        string context,
        System.Collections.Generic.List<StringMemberUpdate> updates)
    {
        CollectStringMemberTranslation(item, "Title", TranslateText, updates);
        CollectStringMemberTranslation(item, "Description", TranslateStructuredTextWithDictionaryFallback, updates);
        CollectStringMemberTranslation(item, "LongDescription", TranslateStructuredText, updates);

        CollectChildCollectionTranslations(item, "Choices", context, updates);
        CollectChildCollectionTranslations(item, "menuOptions", context, updates);
    }

    private static void CollectChildCollectionTranslations(
        object item,
        string memberName,
        string context,
        System.Collections.Generic.List<StringMemberUpdate> updates)
    {
        if (!TryGetMemberValue(item, memberName, out var raw)
            || raw is not IEnumerable children)
        {
            return;
        }

        foreach (var child in children.Cast<object>().Where(static child => child is not null))
        {
            CollectFrameworkDataElementTreeTranslations(child, context, updates);
        }
    }

    private static void CollectStringMemberTranslation(
        object target,
        string memberName,
        Func<string, string> translateText,
        System.Collections.Generic.List<StringMemberUpdate> updates)
    {
        if (!TryGetStringMemberValue(target, memberName, out var current)
            || string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = translateText(current!);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            updates.Add(new StringMemberUpdate(target, memberName, translated));
        }
    }

    private static void ApplyStringMemberUpdates(System.Collections.Generic.List<StringMemberUpdate> updates, string context)
    {
        for (var index = 0; index < updates.Count; index++)
        {
            var update = updates[index];
            SetStringMemberValue(update.Target, update.MemberName, update.Value, context);
        }
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

        if (!TryGetMemberValue(target, memberName, out var raw))
        {
            return false;
        }

        value = raw as string;
        return true;
    }

    private static bool TryGetMemberValue(object target, string memberName, out object? value)
    {
        var type = target.GetType();

        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            value = field.GetValue(target);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(target);
            return true;
        }

        value = null;
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
