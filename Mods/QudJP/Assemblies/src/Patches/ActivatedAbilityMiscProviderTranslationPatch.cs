using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActivatedAbilityMiscProviderTranslationPatch
{
    internal const string Context = nameof(ActivatedAbilityMiscProviderTranslationPatch);
    internal const string Family = Context + ".RegisteredName";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 8);
        AddTarget(targets, "XRL.World.Parts.Cloneling", "Initialize", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.Digging", "Initialize", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.Engulfing", "Initialize", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.FabricateFromSelf", "Initialize", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.RecoilAbility", "Initialize", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.Run", "SyncAbility", [typeof(bool)]);
        AddTarget(targets, "XRL.World.Parts.RunOver", "Initialize", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.TrashRifling", "Initialize", Type.EmptyTypes);
        return targets;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var id = GetGuidMemberValue(__instance, "ActivatedAbilityID");
            if (id == Guid.Empty)
            {
                return;
            }

            var entry = GetActivatedAbilityEntry(__instance, id);
            var source = entry is null ? null : GetStringMemberValue(entry, "DisplayName");
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            var translated = ActivatedAbilityNameTranslator.TranslatePreservingColors(source!, Context, Family);
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return;
            }

            if (!SetActivatedAbilityDisplayName(__instance, id, translated) && entry is not null)
            {
                _ = SetStringMemberValue(entry, "DisplayName", translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, string typeName, string methodName, Type[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}.", Context, typeName, methodName);
            return;
        }

        targets.Add(method);
    }

    private static object? GetActivatedAbilityEntry(object instance, Guid id)
    {
        var type = instance.GetType();
        var method = AccessTools.Method(type, "MyActivatedAbility", [typeof(Guid)]);
        if (method is null)
        {
            var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
            method = gameObjectType is null
                ? null
                : AccessTools.Method(type, "MyActivatedAbility", [typeof(Guid), gameObjectType]);
        }
        if (method is null)
        {
            return null;
        }

        var parameterCount = method.GetParameters().Length;
        return method.Invoke(instance, parameterCount == 1 ? [id] : [id, null]);
    }

    private static bool SetActivatedAbilityDisplayName(object instance, Guid id, string displayName)
    {
        var type = instance.GetType();
        var method = AccessTools.Method(type, "SetMyActivatedAbilityDisplayName", [typeof(Guid), typeof(string)]);
        if (method is null)
        {
            var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
            method = gameObjectType is null
                ? null
                : AccessTools.Method(type, "SetMyActivatedAbilityDisplayName", [typeof(Guid), typeof(string), gameObjectType]);
        }
        if (method is null)
        {
            return false;
        }

        var parameterCount = method.GetParameters().Length;
        var result = method.Invoke(instance, parameterCount == 2 ? [id, displayName] : [id, displayName, null]);
        return result is not bool boolResult || boolResult;
    }

    private static Guid GetGuidMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is Guid guid ? guid : Guid.Empty;
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
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is null)
        {
            return false;
        }

        field.SetValue(instance, value);
        return true;
    }
}
