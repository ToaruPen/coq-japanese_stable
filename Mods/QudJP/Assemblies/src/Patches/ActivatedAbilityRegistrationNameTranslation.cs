using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class ActivatedAbilityRegistrationNameTranslation
{
    internal static void TranslateRegisteredAbilityNames(object? instance, string context, string family)
    {
        if (instance is null)
        {
            return;
        }

        foreach (var id in GetActivatedAbilityIds(instance))
        {
            TranslateActivatedAbilityName(instance, id, context, family);
        }
    }

    private static IEnumerable<Guid> GetActivatedAbilityIds(object instance)
    {
        var seen = new HashSet<Guid>();
        var flags = BindingFlags.Instance | BindingFlags.Public;
        foreach (var field in instance.GetType().GetFields(flags))
        {
            if (IsActivatedAbilityIdMember(field.Name)
                && field.FieldType == typeof(Guid)
                && field.GetValue(instance) is Guid id
                && id != Guid.Empty
                && seen.Add(id))
            {
                yield return id;
            }
        }

        foreach (var property in instance.GetType().GetProperties(flags))
        {
            if (IsActivatedAbilityIdMember(property.Name)
                && property.PropertyType == typeof(Guid)
                && property.GetIndexParameters().Length == 0
                && property.GetValue(instance) is Guid id
                && id != Guid.Empty
                && seen.Add(id))
            {
                yield return id;
            }
        }
    }

    private static bool IsActivatedAbilityIdMember(string memberName)
    {
        return memberName.EndsWith("ActivatedAbilityID", StringComparison.Ordinal);
    }

    private static void TranslateActivatedAbilityName(object instance, Guid id, string context, string family)
    {
        var entry = GetActivatedAbilityEntry(instance, id);
        var source = entry is null ? null : GetStringMemberValue(entry, "DisplayName");
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        var translated = ActivatedAbilityNameTranslator.TranslatePreservingColors(source!, context, family);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        if (!SetActivatedAbilityDisplayName(instance, id, translated) && entry is not null)
        {
            _ = SetStringMemberValue(entry, "DisplayName", translated);
        }
    }

    private static object? GetActivatedAbilityEntry(object instance, Guid id)
    {
        var type = instance.GetType();
        var method = AccessTools.Method(type, "MyActivatedAbility", new[] { typeof(Guid) });
        if (method is null)
        {
            var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
            method = gameObjectType is null
                ? null
                : AccessTools.Method(type, "MyActivatedAbility", new[] { typeof(Guid), gameObjectType });
        }
        if (method is null)
        {
            return null;
        }

        var parameterCount = method.GetParameters().Length;
        return method.Invoke(instance, parameterCount == 1 ? new object?[] { id } : new object?[] { id, null });
    }

    private static bool SetActivatedAbilityDisplayName(object instance, Guid id, string displayName)
    {
        var type = instance.GetType();
        var method = AccessTools.Method(type, "SetMyActivatedAbilityDisplayName", new[] { typeof(Guid), typeof(string) });
        if (method is null)
        {
            var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
            method = gameObjectType is null
                ? null
                : AccessTools.Method(type, "SetMyActivatedAbilityDisplayName", new[] { typeof(Guid), typeof(string), gameObjectType });
        }
        if (method is null)
        {
            return false;
        }

        var parameterCount = method.GetParameters().Length;
        var result = method.Invoke(
            instance,
            parameterCount == 2
                ? new object?[] { id, displayName }
                : new object?[] { id, displayName, null });
        return result is not bool boolResult || boolResult;
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        return AccessTools.Field(type, memberName)?.GetValue(instance);
    }

    private static bool SetStringMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            if (!property.CanWrite || property.PropertyType != typeof(string))
            {
                return false;
            }

            property.SetValue(instance, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is null || field.FieldType != typeof(string))
        {
            return false;
        }

        field.SetValue(instance, value);
        return true;
    }
}
