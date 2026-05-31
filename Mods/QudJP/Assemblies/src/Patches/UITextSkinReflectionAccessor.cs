using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using HarmonyLib;

namespace QudJP.Patches;

internal static class UITextSkinReflectionAccessor
{
    private const string CacheContext = nameof(UITextSkinReflectionAccessor);
    private static readonly ConcurrentDictionary<Type, TextReadStrategy> ReadStrategies = new();
    private static readonly ConcurrentDictionary<Type, TextWriteStrategy> WriteStrategies = new();

    internal static string? GetCurrentText(object? uiTextSkin, string context)
    {
        _ = context;
        if (uiTextSkin is null)
        {
            return null;
        }

        var strategy = ReadStrategies.GetOrAdd(uiTextSkin.GetType(), static type => BuildReadStrategy(type));
        return strategy.Get(uiTextSkin);
    }

    internal static bool SetCurrentText(object? uiTextSkin, string translated, string context)
    {
        _ = context;
        if (uiTextSkin is null)
        {
            return false;
        }

        var strategy = WriteStrategies.GetOrAdd(uiTextSkin.GetType(), static type => BuildWriteStrategy(type));
        return strategy.Set(uiTextSkin, translated);
    }

    internal static bool SetCurrentTextField(object? uiTextSkin, string translated)
    {
        if (uiTextSkin is null)
        {
            return false;
        }

        var textField = AccessTools.Field(uiTextSkin.GetType(), "text");
        if (textField?.FieldType != typeof(string))
        {
            return false;
        }

        textField.SetValue(uiTextSkin, translated);
        return true;
    }

    private static TextReadStrategy BuildReadStrategy(Type type)
    {
        var textField = AccessTools.Field(type, "text");
        if (textField?.FieldType == typeof(string))
        {
            return new TextReadStrategy(target => textField.GetValue(target) as string);
        }

        var textProperty = AccessTools.Property(type, "Text");
        if (textProperty is null)
        {
            Trace.TraceWarning(
                "QudJP: {0}.GetCurrentText falling back to property 'text' for {1}.",
                CacheContext,
                type.FullName);
            textProperty = AccessTools.Property(type, "text");
        }

        if (textProperty is not null && textProperty.CanRead && textProperty.PropertyType == typeof(string))
        {
            return new TextReadStrategy(target => textProperty.GetValue(target) as string);
        }

        return new TextReadStrategy(static _ => null);
    }

    private static TextWriteStrategy BuildWriteStrategy(Type type)
    {
        var setText = AccessTools.Method(type, "SetText", new[] { typeof(string) });
        if (setText is not null)
        {
            return new TextWriteStrategy((target, translated) =>
            {
                _ = setText.Invoke(target, new object[] { translated });
                return true;
            });
        }

        var textField = AccessTools.Field(type, "text");
        if (textField?.FieldType == typeof(string))
        {
            return new TextWriteStrategy((target, translated) =>
            {
                textField.SetValue(target, translated);
                return true;
            });
        }

        var textProperty = AccessTools.Property(type, "Text");
        if (textProperty is null)
        {
            Trace.TraceWarning(
                "QudJP: {0}.SetCurrentText falling back to property 'text' for {1}.",
                CacheContext,
                type.FullName);
            textProperty = AccessTools.Property(type, "text");
        }

        if (textProperty is not null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
        {
            return new TextWriteStrategy((target, translated) =>
            {
                textProperty.SetValue(target, translated);
                return true;
            });
        }

        return new TextWriteStrategy(static (_, _) => false);
    }

    private readonly struct TextReadStrategy
    {
        internal TextReadStrategy(Func<object, string?> get)
        {
            Get = get;
        }

        internal Func<object, string?> Get { get; }
    }

    private readonly struct TextWriteStrategy
    {
        internal TextWriteStrategy(Func<object, string, bool> set)
        {
            Set = set;
        }

        internal Func<object, string, bool> Set { get; }
    }
}
