using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CookbookDisplayNameTranslationPatch
{
    private const string Context = nameof(CookbookDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.World.Parts.Cookbook",
            "Cookbook");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateCookbook", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateCookbook() target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (!TryTranslateDisplayName(__instance))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateDisplayNameForTests(object? instance)
    {
        return TryTranslateDisplayName(instance);
    }

    private static bool TryTranslateDisplayName(object? instance)
    {
        if (instance is null
            || GetMemberValue(instance, "ParentObject") is not { } parentObject
            || GetMemberValue(parentObject, "Render") is not { } render
            || GetStringMemberValue(render, "DisplayName") is not { } source)
        {
            return false;
        }

        if (!CookbookDisplayNameTranslator.TryTranslate(source, out var translated))
        {
            SetStringMemberValue(render, "DisplayName", translated);
            return false;
        }

        if (!SetStringMemberValue(render, "DisplayName", translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, Context + ".RenderDisplayName", source, translated);
        return true;
    }

    private static object? GetMemberValue(object? instance, string memberName)
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

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static string? GetStringMemberValue(object? instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
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
}
