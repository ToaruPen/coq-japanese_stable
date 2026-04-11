using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CherubimSpawnerReplaceDescriptionPatch
{
    private const string TargetTypeName = "XRL.World.Parts.CherubimSpawner";
    private const string GameObjectTypeName = "XRL.World.GameObject";
    private const string DescriptionPartTypeName = "XRL.World.Parts.Description";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var gameObjectType = AccessTools.TypeByName(GameObjectTypeName);
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: CherubimSpawnerReplaceDescriptionPatch failed to resolve CherubimSpawner or GameObject.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ReplaceDescription", [gameObjectType, typeof(string), typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: CherubimSpawnerReplaceDescriptionPatch.ReplaceDescription(GameObject,string,string) not found.");
        }

        return method;
    }

    public static bool Prefix(object? __0, string Description, string Features)
    {
        try
        {
            if (__0 is null || HasAlternateCreatureType(__0))
            {
                return true;
            }

            var displayName = GetDisplayName(__0);
            if (displayName is null || displayName.Length == 0)
            {
                return false;
            }

            if (displayName.Any(static c => c == ' '))
            {
                return true;
            }

            if (!TryReplaceDescription(__0, Description, Features, displayName))
            {
                Trace.TraceError("QudJP: CherubimSpawnerReplaceDescriptionPatch failed to apply guarded no-space replacement.");
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CherubimSpawnerReplaceDescriptionPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static bool TryReplaceDescription(object gameObject, string description, string features, string creatureType)
    {
        var descriptionPart = GetDescriptionPart(gameObject);
        if (descriptionPart is null)
        {
            return false;
        }

        var skin = InvokeStringMethod(gameObject, "GetxTag", "TextFragments", "Skin");
        if (skin is null)
        {
            Trace.TraceWarning("QudJP: CherubimSpawnerReplaceDescriptionPatch could not resolve TextFragments/Skin.");
            return false;
        }

        var shortDescription = description
            .Replace("*skin*", skin)
            .Replace("*creatureType*", creatureType)
            .Replace("*features*", features);

        return SetStringMemberValue(descriptionPart, "_Short", shortDescription);
    }

    private static bool HasAlternateCreatureType(object gameObject)
    {
        return InvokeBoolMethod(gameObject, "HasTag", "AlternateCreatureType");
    }

    private static string? GetDisplayName(object gameObject)
    {
        var render = GetMemberValue(gameObject, "Render");
        return render is null ? null : GetMemberValue(render, "DisplayName") as string;
    }

    private static object? GetDescriptionPart(object gameObject)
    {
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
                return part;
            }
        }

        var descriptionPart = GetMemberValue(gameObject, "DescriptionPart");
        if (descriptionPart is not null)
        {
            return descriptionPart;
        }

        Trace.TraceWarning("QudJP: CherubimSpawnerReplaceDescriptionPatch falling back to Description member lookup.");
        return GetMemberValue(gameObject, "Description");
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static bool SetStringMemberValue(object instance, string memberName, string value)
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

    private static bool InvokeBoolMethod(object instance, string methodName, params object[] args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName, args.Select(static arg => arg.GetType()).ToArray());
        return method?.Invoke(instance, args) as bool? ?? false;
    }

    private static string? InvokeStringMethod(object instance, string methodName, params object[] args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName, args.Select(static arg => arg.GetType()).ToArray());
        return method?.Invoke(instance, args) as string;
    }
}
