using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
#if HAS_TMP
using UguiText = UnityEngine.UI.Text;
#endif

namespace QudJP.Patches;

[HarmonyPatch]
public static class MainMenuRowTranslationPatch
{
    private const string Context = nameof(MainMenuRowTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("MainMenuRow", "MainMenuRow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: MainMenuRowTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: MainMenuRowTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static void Prefix(object? data)
    {
        try
        {
            TranslateTextMember(data);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MainMenuRowTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            _ = TryApplyLegacyFontToRowText(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MainMenuRowTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateTextMember(object? data)
    {
        if (data is null)
        {
            return;
        }

        var current = GetStringMemberValue(data, "Text");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateProducerText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, "MainMenu.Text", current!, translated);
        SetMemberValue(data, "Text", translated);
    }

    private static string TranslateProducerText(string source)
    {
        return MainMenuLocalizationPatch.TranslateProducerText(source);
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            return property.GetValue(instance) as string;
        }

        var field = AccessTools.Field(type, memberName);
        return field?.FieldType == typeof(string)
            ? field.GetValue(instance) as string
            : null;
    }

    private static void SetMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        if (field?.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
        }
    }

    private static bool TryApplyLegacyFontToRowText(object? row)
    {
        return TryApplyLegacyFontToRowTextForTests(row, ApplyLegacyFont);
    }

    internal static bool TryApplyLegacyFontToRowTextForTests(object? row, Action<object> applyFont)
    {
        if (row is null)
        {
            return false;
        }

        var rowType = row.GetType();
        object? text = null;
        var textField = AccessTools.Field(rowType, "text");
        if (textField is not null)
        {
            text = textField.GetValue(row);
        }

        if (text is null)
        {
            var textProperty = AccessTools.Property(rowType, "text");
            if (textProperty is not null)
            {
                text = textProperty.GetValue(row);
            }
        }

        if (text is null)
        {
            return false;
        }

        applyFont(text);
        return true;
    }

    private static void ApplyLegacyFont(object text)
    {
#if HAS_TMP
        if (text is UguiText legacyText)
        {
            FontManager.ApplyToLegacyText(legacyText);
        }
#else
        _ = text;
#endif
    }
}
