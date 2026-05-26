using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ElementalPseudopodDisplayNameTranslationPatch
{
    internal const string Context = nameof(ElementalPseudopodDisplayNameTranslationPatch);
    internal const string Family = Context + ".SetupPod";

    private static readonly IReadOnlyDictionary<string, string> DisplayNameTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{R|flaming pseudopod}}"] = "{{R|燃える仮足}}",
            ["{{C|hoary pseudopod}}"] = "{{C|霜に覆われた仮足}}",
            ["{{G|acidic pseudopod}}"] = "{{G|酸性の仮足}}",
            ["{{W|sparking pseudopod}}"] = "{{W|火花を散らす仮足}}",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 2);
        AddTargetMethod(targets, "XRL.World.Parts.ElementalJelly");
        AddTargetMethod(targets, "XRL.World.Parts.Panhumor");
        return targets;
    }

    public static void Postfix(object? __0)
    {
        try
        {
            if (!TryTranslatePodDisplayName(__0, out var source, out var translated))
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

    internal static bool TryTranslatePodDisplayName(object? pod, out string source, out string translated)
    {
        source = string.Empty;
        translated = string.Empty;
        var render = pod is null ? null : GetMemberValue(pod, "Render");
        if (render is null)
        {
            return false;
        }

        source = GetMemberValue(render, "DisplayName") as string ?? string.Empty;
        if (source.Length == 0)
        {
            return false;
        }

        var lookupSource = source;
        var hadMarker = MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText);
        if (hadMarker)
        {
            lookupSource = markedText;
        }

        if (!DisplayNameTranslations.TryGetValue(lookupSource, out var translatedValue))
        {
            if (hadMarker)
            {
                translated = lookupSource;
                return SetMemberValue(render, "DisplayName", lookupSource);
            }

            return false;
        }

        translated = translatedValue;
        return SetMemberValue(render, "DisplayName", translated);
    }

    private static void AddTargetMethod(ICollection<MethodBase> targets, string typeName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var method = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Method(targetType, "SetupPod", [gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.SetupPod(GameObject).", Context, typeName);
            return;
        }

        targets.Add(method);
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
}
