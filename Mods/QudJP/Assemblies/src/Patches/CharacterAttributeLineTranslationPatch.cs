using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterAttributeLineTranslationPatch
{
    private const string Context = nameof(CharacterAttributeLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CharacterAttributeLine", "CharacterAttributeLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CharacterAttributeLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: CharacterAttributeLineTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            var stat = GetStringMemberValue(data, "stat");
            var statistic = GetMemberValue(data, "data");
            if (string.IsNullOrEmpty(stat) && statistic is null)
            {
                return true;
            }

            SetContextData(__instance, data);

            var shortDisplayName = statistic is null
                ? stat ?? string.Empty
                : ResolveShortDisplayName(statistic, stat);
            var route = ObservabilityHelpers.ComposeContext(Context, "field=attributeText");
            var translatedShortName = TranslateAttributeName(shortDisplayName, route);
            OwnerTextSetter.SetTranslatedText(
                GetMemberValue(__instance, "attributeText"),
                shortDisplayName,
                translatedShortName,
                Context,
                typeof(CharacterAttributeLineTranslationPatch));

            var color = "C";
            var value = 0;
            var baseValue = 0;
            if (statistic is not null)
            {
                value = GetIntMemberValue(statistic, "Value");
                baseValue = GetIntMemberValue(statistic, "BaseValue");
                if (value > baseValue)
                {
                    color = "G";
                }
                else if (value < baseValue)
                {
                    color = "r";
                }
            }

            var go = GetMemberValue(data, "go");
            var valueText = BuildValueText(stat ?? string.Empty, shortDisplayName, value, baseValue, color, go, __instance.GetType());
            _ = UITextSkinReflectionAccessor.SetCurrentText(
                GetMemberValue(__instance, "valueText"),
                valueText,
                Context);

            if (statistic is not null)
            {
                var modifier = GetIntMemberValue(statistic, "Modifier");
                var modifierText = ((modifier > -1) ? "{{G|[+" : "{{R|[") + modifier.ToString(CultureInfo.InvariantCulture) + "]}}";
                _ = UITextSkinReflectionAccessor.SetCurrentText(
                    GetMemberValue(__instance, "modifierText"),
                    modifierText,
                    Context);
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterAttributeLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static string TranslateAttributeName(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "CharacterStatus.AttributeName", source, translated);
        }

        return translated;
    }

    private static string BuildValueText(
        string stat,
        string shortDisplayName,
        int value,
        int baseValue,
        string color,
        object? go,
        Type instanceType)
    {
        if (string.Equals(stat, "CP", StringComparison.Ordinal))
        {
            color = "C";
            var screenType = GameTypeResolver.FindType("Qud.UI.CharacterStatusScreen", "CharacterStatusScreen");
            return $"{{{{{color}|{GetStaticIntValue(screenType ?? instanceType, "CP")}}}}}";
        }

        if (string.Equals(shortDisplayName, "MS", StringComparison.Ordinal))
        {
            if (value < baseValue)
            {
                color = "G";
            }
            else if (value > baseValue)
            {
                color = "r";
            }

            return "{{" + color + "|" + (200 - value).ToString(CultureInfo.InvariantCulture) + "}}";
        }

        if (string.Equals(shortDisplayName, "AV", StringComparison.Ordinal))
        {
            return $"{{{{{color}|{GetRulesStatValue("GetCombatAV", go, value)}}}}}";
        }

        if (string.Equals(shortDisplayName, "DV", StringComparison.Ordinal))
        {
            return $"{{{{{color}|{GetRulesStatValue("GetCombatDV", go, value)}}}}}";
        }

        if (string.Equals(shortDisplayName, "MA", StringComparison.Ordinal))
        {
            return $"{{{{{color}|{GetRulesStatValue("GetCombatMA", go, value)}}}}}";
        }

        return "{{" + color + "|" + value.ToString(CultureInfo.InvariantCulture) + "}}";
    }

    private static int GetStaticIntValue(Type? type, string fieldName)
    {
        if (type is null)
        {
            Trace.TraceError("QudJP: {0}.GetStaticIntValue: type is null for field '{1}'.", Context, fieldName);
            return 0;
        }

        var field = AccessTools.Field(type, fieldName);
        if (field?.FieldType == typeof(int))
        {
            var value = field.GetValue(null);
            return value is null ? 0 : (int)value;
        }

        Trace.TraceError("QudJP: {0}.GetStaticIntValue: field '{1}' not found on type '{2}'.", Context, fieldName, type.FullName);
        return 0;
    }

    private static object GetRulesStatValue(string methodName, object? go, int fallback)
    {
        if (go is null)
        {
            return fallback;
        }

        var rulesType = AccessTools.TypeByName("XRL.Rules.Stats");
        if (rulesType is null)
        {
            rulesType = AccessTools.TypeByName("Stats");
        }
        var method = rulesType is null ? null : AccessTools.Method(rulesType, methodName, new[] { go.GetType() });
        var value = method?.Invoke(null, new[] { go });
        return value ?? fallback;
    }

    private static string ResolveShortDisplayName(object statistic, string? stat)
    {
        var shortDisplayName = InvokeStringMethod(statistic, "GetShortDisplayName");
        if (!string.IsNullOrEmpty(shortDisplayName))
        {
            return shortDisplayName!;
        }

        return stat ?? string.Empty;
    }

    private static void SetContextData(object instance, object data)
    {
        var context = GetMemberValue(instance, "context");
        if (context is not null)
        {
            SetMemberValue(context, "data", data);
        }
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

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string? InvokeStringMethod(object instance, string methodName)
    {
        return AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, null) as string;
    }

    private static void SetMemberValue(object instance, string memberName, object? value)
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
