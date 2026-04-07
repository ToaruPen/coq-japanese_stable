using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace QudJP;

internal static class InventoryRenderObservability
{
    private const int MaxLogsPerBucket = 5;
    private const string ProbeVersion = "v6";

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildInventoryLineState(object? inventoryLineInstance, object? data, out string? logLine)
    {
        logLine = null;
        if (inventoryLineInstance is null || data is null)
        {
            return false;
        }

        if (!TryGetBooleanPropertyOrField(data, "category", out var isCategory) || isCategory)
        {
            return false;
        }

        var displayName = TryGetStringPropertyOrField(data, "displayName");
        var dataTypeName = data.GetType().FullName;
        if (string.IsNullOrEmpty(dataTypeName))
        {
            dataTypeName = data.GetType().Name;
        }

        var itemValue = GetPropertyOrFieldValue(data, "item");
        if (itemValue is null)
        {
            itemValue = GetPropertyOrFieldValue(data, "Item");
        }

        var itemSummary = DescribeObject(itemValue);
        var textSkin = GetPropertyOrFieldValue(inventoryLineInstance, "text");
        var rawText = TryGetStringPropertyOrField(textSkin, "text");
        var formattedText = TryGetStringPropertyOrField(textSkin, "formattedText");
        var tmp = textSkin is null ? null : GetFieldValue(textSkin, "_tmp");
        var tmpText = TryGetStringPropertyOrField(tmp, "text");
        var tmpEnabled = tmp is not null && TryGetBooleanPropertyOrField(tmp, "enabled", out var isEnabled) ? isEnabled : (bool?)null;
        var tmpGameObject = tmp is null ? null : GetPropertyOrFieldValue(tmp, "gameObject");
        var tmpActive = tmpGameObject is not null && TryGetBooleanPropertyOrField(tmpGameObject, "activeInHierarchy", out var isActive)
            ? isActive
            : (bool?)null;
        var fontObject = GetPropertyOrFieldValue(tmp, "font");
        var fontName = TryGetStringPropertyOrField(fontObject, "name");
        var fontMaterial = GetPropertyOrFieldValue(tmp, "fontMaterial");
        if (fontMaterial is null)
        {
            fontMaterial = GetPropertyOrFieldValue(tmp, "fontSharedMaterial");
        }

        var materialName = TryGetStringPropertyOrField(fontMaterial, "name");
        var tmpAlpha = TryGetFloatPropertyOrField(tmp, "alpha");
        var tmpColor = GetPropertyOrFieldValue(tmp, "color");
        var tmpColorAlpha = TryGetFloatPropertyOrField(tmpColor, "a");
        var canvasRenderer = GetPropertyOrFieldValue(tmp, "canvasRenderer");
        var canvasAlpha = TryInvokeFloatMethod(canvasRenderer, "GetAlpha");
        var canvasCull = canvasRenderer is not null && TryGetBooleanPropertyOrField(canvasRenderer, "cull", out var isCulled)
            ? isCulled
            : (bool?)null;
        var rectTransform = GetPropertyOrFieldValue(tmp, "rectTransform");
        var rect = GetPropertyOrFieldValue(rectTransform, "rect");
        var rectWidth = TryGetFloatPropertyOrField(rect, "width");
        var rectHeight = TryGetFloatPropertyOrField(rect, "height");
        var atlasPopulationMode = GetPropertyOrFieldValue(fontObject, "atlasPopulationMode")?.ToString();
        var atlasTextureCount = TryGetIntPropertyOrField(fontObject, "atlasTextureCount");
        var sampleChar = FindRepresentativeCharacter(displayName, rawText, tmpText);
        var hasSampleCharacter = sampleChar is null ? (bool?)null : TryCallHasCharacter(fontObject, sampleChar.Value);

        var bucket = ClassifyBucket(displayName, rawText, formattedText);
        var hitCount = BucketCounts.AddOrUpdate(
            bucket,
            1,
            static (_, currentValue) => currentValue < int.MaxValue ? currentValue + 1 : int.MaxValue);
        if (hitCount > MaxLogsPerBucket)
        {
            return false;
        }

        logLine = BuildLogLine(
            bucket,
            displayName,
            dataTypeName,
            itemSummary,
            rawText,
            formattedText,
            tmpText,
            fontName,
            materialName,
            tmpAlpha,
            tmpColorAlpha,
            canvasAlpha,
            canvasCull,
            rectWidth,
            rectHeight,
            atlasPopulationMode,
            atlasTextureCount,
            sampleChar,
            hasSampleCharacter,
            tmpEnabled,
            tmpActive);
        return true;
    }

    private static string ClassifyBucket(string? displayName, string? rawText, string? formattedText)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return "empty-display";
        }

        if (string.IsNullOrEmpty(rawText))
        {
            return "empty-uitext";
        }

        if (string.IsNullOrEmpty(formattedText))
        {
            return "empty-formatted";
        }

        return "formatted-present";
    }

    private static string BuildLogLine(
        string bucket,
        string? displayName,
        string? dataTypeName,
        string? itemSummary,
        string? rawText,
        string? formattedText,
        string? tmpText,
        string? fontName,
        string? materialName,
        float? tmpAlpha,
        float? tmpColorAlpha,
        float? canvasAlpha,
        bool? canvasCull,
        float? rectWidth,
        float? rectHeight,
        string? atlasPopulationMode,
        int? atlasTextureCount,
        char? sampleChar,
        bool? hasSampleCharacter,
        bool? tmpEnabled,
        bool? tmpActive)
    {
        var builder = new StringBuilder();
        builder.Append("[QudJP] InventoryRenderProbe/");
        builder.Append(ProbeVersion);
        builder.Append('[');
        builder.Append(bucket);
        builder.Append("]: display='");
        builder.Append(Truncate(displayName));
        builder.Append("' raw='");
        builder.Append(Truncate(rawText));
        builder.Append("' dataType='");
        builder.Append(dataTypeName ?? string.Empty);
        builder.Append("' item='");
        builder.Append(Truncate(itemSummary));
        builder.Append("' formatted='");
        builder.Append(Truncate(formattedText));
        builder.Append("' lens=");
        builder.Append(displayName?.Length ?? 0);
        builder.Append('/');
        builder.Append(rawText?.Length ?? 0);
        builder.Append('/');
        builder.Append(formattedText?.Length ?? 0);
        builder.Append(" tmp='");
        builder.Append(Truncate(tmpText));
        builder.Append("' font='");
        builder.Append(fontName ?? string.Empty);
        builder.Append("' material='");
        builder.Append(materialName ?? string.Empty);
        builder.Append("' tmpAlpha=");
        builder.Append(tmpAlpha?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(" colorA=");
        builder.Append(tmpColorAlpha?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(" canvasA=");
        builder.Append(canvasAlpha?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(" cull=");
        builder.Append(canvasCull?.ToString() ?? "<unknown>");
        builder.Append(" rect=");
        builder.Append(rectWidth?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append('x');
        builder.Append(rectHeight?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append("' atlasMode='");
        builder.Append(atlasPopulationMode ?? string.Empty);
        builder.Append("' atlasCount=");
        builder.Append(atlasTextureCount?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(" sample='");
        builder.Append(sampleChar?.ToString() ?? string.Empty);
        builder.Append("' hasSample=");
        builder.Append(hasSampleCharacter?.ToString() ?? "<unknown>");
        builder.Append(" active=");
        builder.Append(tmpActive?.ToString() ?? "<unknown>");
        builder.Append(" enabled=");
        builder.Append(tmpEnabled?.ToString() ?? "<unknown>");
        return builder.ToString();
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value!.Replace("\r", "\\r")
            .Replace("\n", "\\n");
#pragma warning disable CA1845
        return normalized.Length <= 80 ? normalized : normalized.Substring(0, 80) + "...";
#pragma warning restore CA1845
    }

    private static string? DescribeObject(object? value)
    {
        if (value is null)
        {
            return null;
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
            blueprint = TryGetStringPropertyOrField(value, "DisplayName");
        }

        if (string.IsNullOrEmpty(blueprint))
        {
            blueprint = TryGetStringPropertyOrField(value, "displayName");
        }

        return string.IsNullOrEmpty(blueprint) ? typeName : typeName + ":" + blueprint;
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

        var fieldValue = Access(instance, memberName);
        if (fieldValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return false;
    }

    private static int? TryGetIntPropertyOrField(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.PropertyType == typeof(int) && property.GetIndexParameters().Length == 0)
        {
            var value = property.GetValue(instance);
            return value is int intValue ? intValue : null;
        }

        var fieldValue = Access(instance, memberName);
        return fieldValue is int intFieldValue ? intFieldValue : null;
    }

    private static float? TryGetFloatPropertyOrField(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.PropertyType == typeof(float) && property.GetIndexParameters().Length == 0)
        {
            var value = property.GetValue(instance);
            return value is float floatValue ? floatValue : null;
        }

        var fieldValue = Access(instance, memberName);
        return fieldValue is float floatFieldValue ? floatFieldValue : null;
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

    private static float? TryInvokeFloatMethod(object? instance, string methodName)
    {
        if (instance is null)
        {
            return null;
        }

        var method = instance.GetType().GetMethod(methodName, Type.EmptyTypes);
        if (method is null || method.ReturnType != typeof(float))
        {
            return null;
        }

        var value = method.Invoke(instance, null);
        return value is float floatValue ? floatValue : null;
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

    private static object? GetFieldValue(object instance, string memberName)
    {
        return Access(instance, memberName);
    }

    private static char? FindRepresentativeCharacter(params string?[] candidates)
    {
        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            for (var j = 0; j < candidate!.Length; j++)
            {
                var current = candidate[j];
                if (!char.IsWhiteSpace(current) && current != '{' && current != '}' && current != '|')
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static bool? TryCallHasCharacter(object? fontObject, char character)
    {
        if (fontObject is null)
        {
            return null;
        }

        var type = fontObject.GetType();
        var boolMethod = type.GetMethod("HasCharacter", new[] { typeof(char) });
        if (boolMethod is not null && boolMethod.ReturnType == typeof(bool))
        {
            var result = boolMethod.Invoke(fontObject, new object[] { character });
            return result is bool boolValue ? boolValue : null;
        }

        var outMethod = type.GetMethod("HasCharacter", new[] { typeof(char), typeof(bool).MakeByRefType() });
        if (outMethod is not null && outMethod.ReturnType == typeof(bool))
        {
            var args = new object[] { character, false };
            var result = outMethod.Invoke(fontObject, args);
            return result is bool boolValue ? boolValue : null;
        }

        return null;
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
