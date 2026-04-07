using System;
using System.Collections.Concurrent;
using System.Text;

namespace QudJP;

internal static class InventoryActionObservability
{
    private const int MaxLogsPerCommand = 5;

    private static readonly ConcurrentDictionary<string, int> CommandCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildDescriptionHandleEventState(
        object? descriptionPartInstance,
        object? inventoryActionEvent,
        out string? logLine)
    {
        logLine = null;
        if (descriptionPartInstance is null || inventoryActionEvent is null)
        {
            return false;
        }

        var command = TryGetStringPropertyOrField(inventoryActionEvent, "Command");
        var actor = DescribeObject(GetPropertyOrFieldValue(inventoryActionEvent, "Actor"));
        var item = DescribeObject(GetPropertyOrFieldValue(inventoryActionEvent, "Item"));
        var objectTarget = DescribeObject(GetPropertyOrFieldValue(inventoryActionEvent, "ObjectTarget"));
        var parentObject = DescribeObject(GetPropertyOrFieldValue(descriptionPartInstance, "ParentObject"));

        var bucket = string.IsNullOrEmpty(command) ? "<empty>" : command!;
        var hitCount = CommandCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLogsPerCommand)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] DescriptionInventoryActionProbe: command='");
        builder.Append(Escape(command));
        builder.Append("' parent='");
        builder.Append(Escape(parentObject));
        builder.Append("' actor='");
        builder.Append(Escape(actor));
        builder.Append("' item='");
        builder.Append(Escape(item));
        builder.Append("' target='");
        builder.Append(Escape(objectTarget));
        builder.Append('\'');
        logLine = builder.ToString();
        return true;
    }

    internal static void ResetForTests()
    {
        CommandCounts.Clear();
    }

    private static string DescribeObject(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var type = value.GetType();
        var typeName = type.FullName ?? type.Name;
        var blueprint = TryGetStringPropertyOrField(value, "Blueprint");
        if (string.IsNullOrEmpty(blueprint))
        {
            blueprint = TryGetStringPropertyOrField(value, "BlueprintName");
        }

        if (string.IsNullOrEmpty(blueprint))
        {
            blueprint = TryGetStringPropertyOrField(value, "id");
        }

        if (string.IsNullOrEmpty(blueprint))
        {
            return typeName;
        }

        return typeName + ":" + blueprint;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value!
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
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
}
