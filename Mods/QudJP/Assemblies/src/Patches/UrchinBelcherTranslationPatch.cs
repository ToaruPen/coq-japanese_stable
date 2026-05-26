using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class UrchinBelcherTranslationPatch
{
    private const string Context = nameof(UrchinBelcherTranslationPatch);
    private const string Family = "UrchinBelcher.CtorText";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("XRL.World.Parts.Mutation.UrchinBelcher");
        if (type is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: XRL.World.Parts.Mutation.UrchinBelcher.", Context);
            return null;
        }

        var constructor = AccessTools.Constructor(type, Type.EmptyTypes);
        if (constructor is null)
        {
            Trace.TraceError("QudJP: {0} target constructor not found: UrchinBelcher().", Context);
        }

        return constructor;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            TranslateForTests(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateForTests(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        TranslateStringMember(instance, "Description", "Description");
        TranslateStringMember(instance, "CommandName", "CommandName");
        TranslateStringMember(instance, "CommandDescription", "CommandDescription");
    }

    private static void TranslateStringMember(object instance, string memberName, string detail)
    {
        if (!TryGetStringMemberValue(instance, memberName, out var current)
            || string.IsNullOrEmpty(current)
            || current!.StartsWith("\u0001", StringComparison.Ordinal))
        {
            return;
        }

        var translated = TranslateText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (TrySetStringMemberValue(instance, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, Family + "." + detail, current!, translated);
        }
    }

    private static string TranslateText(string source)
    {
        return source switch
        {
            "You belch forth various urchins." => "さまざまなウニを吐き出す。",
            "Belch Urchins" => "ウニを吐く",
            _ => source,
        };
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        value = null;
        var type = target.GetType();

        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            value = field.GetValue(target) as string;
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(target) as string;
            return true;
        }

        return false;
    }

    private static bool TrySetStringMemberValue(object target, string memberName, string value)
    {
        var type = target.GetType();

        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(target, value);
            return true;
        }

        Trace.TraceWarning("QudJP: {0} could not set member '{1}' on '{2}'.", Context, memberName, type.FullName);
        return false;
    }
}
