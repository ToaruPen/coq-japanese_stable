using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PseudoRelicGeneratedNameTranslationPatch
{
    internal const string Context = nameof(PseudoRelicGeneratedNameTranslationPatch);
    internal const string Family = Context + ".AfterPseudoRelicGenerated";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.AfterPseudoRelicGeneratedEvent");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type or game object type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "Send",
            [gameObjectType, typeof(string), typeof(string), typeof(string), typeof(int)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.AfterPseudoRelicGeneratedEvent.Send target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __0)
    {
        try
        {
            if (!TryTranslateObjectName(__0, out var source, out var translated))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateObjectName(object? obj, out string source, out string translated)
    {
        source = string.Empty;
        translated = string.Empty;
        if (obj is null)
        {
            return false;
        }

        var displayName = GetStringMemberValue(obj, "DisplayName");
        if (displayName is null)
        {
            Trace.TraceWarning("QudJP: {0} could not read pseudo relic DisplayName.", Context);
            return false;
        }

        source = displayName;
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (!RelicGeneratedNameTranslator.TryTranslate(source, out translated))
        {
            if (!string.Equals(source, translated, StringComparison.Ordinal)
                && SetMemberValue(obj, "DisplayName", translated))
            {
                ClearArticle(obj, "IndefiniteArticle");
                ClearArticle(obj, "DefiniteArticle");
                ClearCachedDisplayNameForSort(obj);
            }

            return false;
        }

        if (!SetMemberValue(obj, "DisplayName", translated))
        {
            translated = source;
            return false;
        }

        ClearArticle(obj, "IndefiniteArticle");
        ClearArticle(obj, "DefiniteArticle");
        ClearCachedDisplayNameForSort(obj);
        return true;
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            return property.GetValue(instance) as string;
        }

        return AccessTools.Field(type, memberName)?.GetValue(instance) as string;
    }

    private static bool SetMemberValue(object instance, string memberName, object? value)
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

    private static void ClearArticle(object obj, string articleProperty)
    {
        var method = AccessTools.Method(obj.GetType(), "SetStringProperty", [typeof(string), typeof(string), typeof(bool)]);
        if (method is not null)
        {
            method.Invoke(obj, [articleProperty, string.Empty, false]);
        }
    }

    private static void ClearCachedDisplayNameForSort(object obj)
    {
        AccessTools.Field(obj.GetType(), "_CachedDisplayNameForSort")?.SetValue(obj, null);
    }
}
