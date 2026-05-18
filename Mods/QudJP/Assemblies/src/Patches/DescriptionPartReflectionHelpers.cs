using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;

namespace QudJP.Patches;

internal static class DescriptionPartReflectionHelpers
{
    private const string DescriptionPartTypeName = "XRL.World.Parts.Description";

    internal static bool TryGetParentObject(object instance, out object parentObject)
    {
        var parent = GetMemberValue(instance, "ParentObject");
        if (parent is not null)
        {
            parentObject = parent;
            return true;
        }

        parentObject = null!;
        return false;
    }

    internal static bool TryGetDescriptionPart(object gameObject, string context, bool logFallback, out object descriptionPart)
    {
        if (HasStringMember(gameObject, "Short") || HasStringMember(gameObject, "_Short"))
        {
            descriptionPart = gameObject;
            return true;
        }

        var descriptionPartType = AccessTools.TypeByName(DescriptionPartTypeName);
        if (descriptionPartType is not null)
        {
            var getPartMethod = AccessTools.GetDeclaredMethods(gameObject.GetType())
                .FirstOrDefault(static method => method.IsGenericMethodDefinition
                    && string.Equals(method.Name, "GetPart", StringComparison.Ordinal)
                    && method.GetParameters().Length == 0);
            var part = getPartMethod?.MakeGenericMethod(descriptionPartType).Invoke(gameObject, null);
            if (part is not null)
            {
                descriptionPart = part;
                return true;
            }
        }

        var fallback = GetMemberValue(gameObject, "DescriptionPart");
        if (fallback is null)
        {
            if (logFallback)
            {
                Trace.TraceWarning("QudJP: {0} falling back from DescriptionPart to Description member lookup.", context);
            }

            fallback = GetMemberValue(gameObject, "Description");
        }

        if (fallback is not null)
        {
            descriptionPart = fallback;
            return true;
        }

        descriptionPart = null!;
        return false;
    }

    internal static object? GetMemberValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        return AccessTools.Field(type, memberName)?.GetValue(instance);
    }

    internal static string? GetStringMemberValue(object? instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    internal static bool SetStringMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return true;
        }

        return false;
    }

    private static bool HasStringMember(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.PropertyType == typeof(string))
        {
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        return field is not null && field.FieldType == typeof(string);
    }
}
