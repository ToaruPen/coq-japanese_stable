using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectActivatedAbilityDescriptionTranslationPatch
{
    private const string Context = nameof(GameObjectActivatedAbilityDescriptionTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var statCollectorType = AccessTools.TypeByName("XRL.Templates+StatCollector");
        if (gameObjectType is null || statCollectorType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var statCollectorActionType = typeof(Action<>).MakeGenericType(statCollectorType);
        var method = AccessTools.Method(gameObjectType, "DescribeActivatedAbility", new[] { typeof(Guid), statCollectorActionType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.DescribeActivatedAbility(Guid, Action<StatCollector>) not found.", Context);
        }

        return method;
    }

    public static void Postfix(object __instance, Guid ID, bool __result)
    {
        try
        {
            if (!__result)
            {
                return;
            }

            var getActivatedAbility = AccessTools.Method(__instance.GetType(), "GetActivatedAbility", new[] { typeof(Guid) });
            var ability = getActivatedAbility?.Invoke(__instance, new object[] { ID });
            TranslateActivatedAbilityDescriptionForTests(ability);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateActivatedAbilityDescriptionForTests(object? ability)
    {
        if (ability is null
            || !TryGetStringMemberValue(ability, "Description", out var current)
            || string.IsNullOrEmpty(current))
        {
            return;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(current!, out var markedText))
        {
            _ = TrySetStringMemberValue(ability, "Description", markedText);
            return;
        }

        var translated = TranslateDescriptionText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (TrySetStringMemberValue(ability, "Description", translated))
        {
            DynamicTextObservability.RecordTransform(Context, Context + ".Description", current!, translated);
        }
    }

    private static string TranslateDescriptionText(string source)
    {
        return source
            .Replace("Cooldown:", "クールダウン:")
            .Replace("Range:", "射程:");
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        var property = AccessTools.Property(target.GetType(), memberName);
        if (property is not null)
        {
            value = property.GetValue(target) as string;
            if (value is not null)
            {
                return true;
            }
        }

        var field = AccessTools.Field(target.GetType(), memberName);
        value = field is null ? null : field.GetValue(target) as string;
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
