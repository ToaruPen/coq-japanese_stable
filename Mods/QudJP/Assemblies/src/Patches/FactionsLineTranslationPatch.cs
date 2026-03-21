using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FactionsLineTranslationPatch
{
    private static readonly string[] TextFieldNames =
    {
        "barText",
        "barReputationText",
        "detailsText",
        "detailsText2",
        "detailsText3",
    };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.FactionsLine", "FactionsLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: FactionsLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: FactionsLineTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            _ = data;
            if (__instance is null)
            {
                return;
            }

            for (var index = 0; index < TextFieldNames.Length; index++)
            {
                TranslateTextField(__instance, TextFieldNames[index]);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: FactionsLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateTextField(object instance, string fieldName)
    {
        var field = AccessTools.Field(instance.GetType(), fieldName);
        if (field is null)
        {
            return;
        }

        var textComponent = field.GetValue(instance);
        if (textComponent is null)
        {
            return;
        }

        var textField = AccessTools.Field(textComponent.GetType(), "text");
        var current = textField?.GetValue(textComponent) as string;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(current!);
        if (FactionsStatusScreenTranslationPatch.IsAlreadyLocalizedFactionText(stripped))
        {
            return;
        }

        if (!FactionsStatusScreenTranslationPatch.TryTranslateFactionText(
                stripped,
                ObservabilityHelpers.ComposeContext(nameof(FactionsLineTranslationPatch), $"field={fieldName}"),
                out var translated))
        {
            return;
        }

        if (string.Equals(current, translated, StringComparison.Ordinal))
        {
            ConfigureDetailsWrapping(textComponent, fieldName);
            return;
        }

        var setText = AccessTools.Method(textComponent.GetType(), "SetText", new[] { typeof(string) });
        if (setText is not null)
        {
            _ = setText.Invoke(textComponent, new object[] { translated });
            ConfigureDetailsWrapping(textComponent, fieldName);
            return;
        }

        if (textField is not null)
        {
            textField.SetValue(textComponent, translated);
        }

        ConfigureDetailsWrapping(textComponent, fieldName);
    }

    private static void ConfigureDetailsWrapping(object textComponent, string fieldName)
    {
        if (!fieldName.StartsWith("detailsText", StringComparison.Ordinal))
        {
            return;
        }

        SetBooleanMember(textComponent, "enableWordWrapping", value: true);
        SetBooleanMember(textComponent, "textWrapping", value: true);
        SetEnumMember(textComponent, "textWrappingMode", preferredName: "Normal");
    }

    private static void SetBooleanMember(object instance, string memberName, bool value)
    {
        var property = AccessTools.Property(instance.GetType(), memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(bool))
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(instance.GetType(), memberName);
        if (field is not null && field.FieldType == typeof(bool))
        {
            field.SetValue(instance, value);
        }
    }

    private static void SetEnumMember(object instance, string memberName, string preferredName)
    {
        var property = AccessTools.Property(instance.GetType(), memberName);
        if (property is not null && property.CanWrite && property.PropertyType.IsEnum)
        {
            property.SetValue(instance, ResolveEnumValue(property.PropertyType, preferredName));
            return;
        }

        var field = AccessTools.Field(instance.GetType(), memberName);
        if (field is not null && field.FieldType.IsEnum)
        {
            field.SetValue(instance, ResolveEnumValue(field.FieldType, preferredName));
        }
    }

    private static object ResolveEnumValue(Type enumType, string preferredName)
    {
        if (Enum.IsDefined(enumType, preferredName))
        {
            return Enum.Parse(enumType, preferredName, ignoreCase: false);
        }

        var values = Enum.GetValues(enumType);
        return values.Length > 0 ? values.GetValue(0)! : Activator.CreateInstance(enumType)!;
    }
}
