using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DecoyHologramDescriptionTranslationPatch
{
    private const string Context = nameof(DecoyHologramDescriptionTranslationPatch);
    private const string SourcePrefix = "Light stammers in parallax to form the image of an object. ";
    private const string TranslatedPrefix = "光が視差の中で明滅し、物体の像を形作っている。 ";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var emitterType = AccessTools.TypeByName("XRL.World.Parts.DecoyHologramEmitter");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (emitterType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(emitterType, "CreateHologramOf", new[] { gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.CreateHologramOf(GameObject) not found.", Context);
        }

        return method;
    }

    public static void Postfix(object __result)
    {
        try
        {
            var descriptionPart = GetPartOrMember(__result, "Description", "DescriptionPart");
            TranslateHologramDescriptionForTests(descriptionPart);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateHologramDescriptionForTests(object? descriptionPart)
    {
        if (descriptionPart is null
            || !TryGetStringMemberValue(descriptionPart, "Short", out var current)
            || string.IsNullOrEmpty(current)
            || current!.StartsWith("\u0001", StringComparison.Ordinal)
            || !current.StartsWith(SourcePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var translated = TranslatedPrefix + current.Substring(SourcePrefix.Length);
        if (TrySetStringMemberValue(descriptionPart, "Short", translated))
        {
            DynamicTextObservability.RecordTransform(Context, Context + ".Short", current!, translated);
        }
    }

    private static object? GetPartOrMember(object target, string partName, string memberName)
    {
        var getPart = AccessTools.Method(target.GetType(), "GetPart", new[] { typeof(string) });
        if (getPart is not null)
        {
            var part = getPart.Invoke(target, new object[] { partName });
            if (part is not null)
            {
                return part;
            }
        }

        return AccessTools.Property(target.GetType(), memberName)?.GetValue(target)
            ?? AccessTools.Field(target.GetType(), memberName)?.GetValue(target);
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        value = AccessTools.Property(target.GetType(), memberName)?.GetValue(target) as string
            ?? AccessTools.Field(target.GetType(), memberName)?.GetValue(target) as string;
        return value is not null;
    }

    private static bool TrySetStringMemberValue(object target, string memberName, string value)
    {
        var property = AccessTools.Property(target.GetType(), memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(target, value);
            return true;
        }

        var field = AccessTools.Field(target.GetType(), memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
            return true;
        }

        return false;
    }
}
