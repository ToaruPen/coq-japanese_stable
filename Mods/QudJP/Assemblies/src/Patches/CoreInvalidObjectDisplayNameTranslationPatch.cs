using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CoreInvalidObjectDisplayNameTranslationPatch
{
    internal const string Context = nameof(CoreInvalidObjectDisplayNameTranslationPatch);
    internal const string Family = Context + ".RenderDisplayName";

    private const string InvalidBlueprintPrefix = "[invalid blueprint:";
    private const string InvalidBlueprintSuffix = "]";
    private const string InvalidCacheObjectPrefix = "INVALID CACHE OBJECT: ";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var gameObjectActionType = gameObjectType is null ? null : typeof(Action<>).MakeGenericType(gameObjectType);
        var gameObjectListType = gameObjectType is null ? null : typeof(List<>).MakeGenericType(gameObjectType);

        var factoryType = AccessTools.TypeByName("XRL.World.GameObjectFactory");
        var factoryCreateFull = ResolveMethod(
            factoryType,
            "CreateObject",
            gameObjectActionType is null || gameObjectListType is null
                ? null
                : new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    gameObjectActionType,
                    gameObjectActionType,
                    typeof(string),
                    gameObjectListType,
                });
        if (factoryCreateFull is not null)
        {
            yield return factoryCreateFull;
        }

        var factoryCreateBefore = ResolveMethod(
            factoryType,
            "CreateObject",
            gameObjectActionType is null ? null : new[] { typeof(string), gameObjectActionType });
        if (factoryCreateBefore is not null)
        {
            yield return factoryCreateBefore;
        }

        var zoneManagerType = AccessTools.TypeByName("XRL.World.ZoneManager");
        var getCachedObjects = ResolveMethod(zoneManagerType, "GetCachedObjects", new[] { typeof(string) });
        if (getCachedObjects is not null)
        {
            yield return getCachedObjects;
        }
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is IEnumerable objects and not string)
            {
                foreach (var item in objects)
                {
                    TranslateRenderDisplayName(item);
                }

                return;
            }

            TranslateRenderDisplayName(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateInvalidObjectDisplayName(string source, out string translated)
    {
        if (source.StartsWith(InvalidBlueprintPrefix, StringComparison.Ordinal)
            && source.EndsWith(InvalidBlueprintSuffix, StringComparison.Ordinal))
        {
            var blueprint = source.Substring(
                InvalidBlueprintPrefix.Length,
                source.Length - InvalidBlueprintPrefix.Length - InvalidBlueprintSuffix.Length);
            translated = "[無効なブループリント:" + blueprint + "]";
            return true;
        }

        if (source.StartsWith(InvalidCacheObjectPrefix, StringComparison.Ordinal))
        {
            translated = "無効なキャッシュオブジェクト: " + source.Substring(InvalidCacheObjectPrefix.Length);
            return true;
        }

        translated = source;
        return false;
    }

    private static MethodBase? ResolveMethod(Type? targetType, string methodName, Type[]? parameterTypes)
    {
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0}: target type not found for method '{1}'.", Context, methodName);
            return null;
        }

        if (parameterTypes is null)
        {
            Trace.TraceError(
                "QudJP: {0}: parameter types not resolved for method '{1}.{2}'.",
                Context,
                targetType.FullName,
                methodName);
            return null;
        }

        var method = AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}: method '{1}.{2}' not found.", Context, targetType.FullName, methodName);
        }

        return method;
    }

    private static void TranslateRenderDisplayName(object? gameObject)
    {
        if (gameObject is null)
        {
            return;
        }

        var render = GetMemberValue(gameObject, "Render");
        if (render is null || GetStringMemberValue(render, "DisplayName") is not { } source)
        {
            return;
        }

        if (!TryTranslateInvalidObjectDisplayName(source, out var translated)
            || string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        SetMemberValue(render, "DisplayName", translated);
        DynamicTextObservability.RecordTransform(Context, Family, source, translated);
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

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static void SetMemberValue(object instance, string memberName, object value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }
}
