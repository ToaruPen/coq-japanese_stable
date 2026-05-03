using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryAndEquipmentStatusScreenTranslationPatch
{
    private const string WeightUnit = "lbs.";

    private static readonly Regex HotkeyTogglePattern =
        new Regex(@"^(?<hotkey>\{\{hotkey\|\[[^\]]+\]\}\})\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WeightPattern =
        new Regex(@"^\{\{C\|(?<current>\d+)\{\{K\|/(?<max>\d+)\}\} lbs\. \}\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.InventoryAndEquipmentStatusScreen", "InventoryAndEquipmentStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenTranslationPatch.UpdateViewFromData() not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateMenuOptionDescription(__instance, "CMD_SHOWCYBERNETICS");
            TranslateMenuOptionDescription(__instance, "CMD_OPTIONS");
            TranslateMenuOptionDescription(__instance, "SET_PRIMARY_LIMB");
            TranslateMenuOptionDescription(__instance, "SHOW_TOOLTIP");
            TranslateMenuOptionDescription(__instance, "QUICK_DROP");
            TranslateMenuOptionDescription(__instance, "QUICK_EAT");
            TranslateMenuOptionDescription(__instance, "QUICK_DRINK");
            TranslateMenuOptionDescription(__instance, "QUICK_APPLY");
            TranslateWeightText(__instance, "weightText");
            TranslateHotkeyToggleText(__instance, "cyberneticsHotkeySkin");
            TranslateHotkeyToggleText(__instance, "cyberneticsHotkeySkinForList");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateMenuOptionDescription(object instance, string fieldName)
    {
        var menuOption = AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance);
        if (menuOption is null)
        {
            return;
        }

        var descriptionField = AccessTools.Field(menuOption.GetType(), "Description");
        if (descriptionField is null || descriptionField.FieldType != typeof(string))
        {
            return;
        }

        var current = descriptionField.GetValue(menuOption) as string;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = nameof(InventoryAndEquipmentStatusScreenTranslationPatch);
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            current!,
            visible => InventoryAndEquipmentStatusScreenTextTranslator.TryTranslateUiText(visible, route, out var candidate)
                ? candidate
                : visible);

        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            descriptionField.SetValue(menuOption, translated);
        }
    }

    private static void TranslateWeightText(object instance, string fieldName)
    {
        var uiTextSkin = GetMemberValue(instance, fieldName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(InventoryAndEquipmentStatusScreenTranslationPatch));
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateWeightTextValue(current!, ObservabilityHelpers.ComposeContext(nameof(InventoryAndEquipmentStatusScreenTranslationPatch), "field=" + fieldName));
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(uiTextSkin, current!, translated, nameof(InventoryAndEquipmentStatusScreenTranslationPatch), typeof(InventoryAndEquipmentStatusScreenTranslationPatch));
    }

    private static string TranslateWeightTextValue(string source, string route)
    {
        var match = WeightPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var translated = string.Format(
            CultureInfo.InvariantCulture,
            "{{{{C|{0}{{{{K|/{1}}}}} {2} }}}}",
            match.Groups["current"].Value,
            match.Groups["max"].Value,
            WeightUnit);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "InventoryAndEquipment.WeightText", source, translated);
        }

        return translated;
    }

    private static void TranslateHotkeyToggleText(object instance, string fieldName)
    {
        var uiTextSkin = GetMemberValue(instance, fieldName);
        if (uiTextSkin is null)
        {
            return;
        }

        var current = GetMemberValue(uiTextSkin, "text") as string;
        if (current is null)
        {
            current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(InventoryAndEquipmentStatusScreenTranslationPatch));
        }
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var match = HotkeyTogglePattern.Match(current!);
        if (!match.Success)
        {
            return;
        }

        var label = match.Groups["label"].Value;
        var translatedLabel = StringHelpers.TranslateExactOrLowerAscii(label);
        if (translatedLabel is null) { translatedLabel = label; }
        var translated = match.Groups["hotkey"].Value + " " + translatedLabel;
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(
            ObservabilityHelpers.ComposeContext(nameof(InventoryAndEquipmentStatusScreenTranslationPatch), "field=" + fieldName),
            "InventoryAndEquipment.CyberneticsToggle",
            current!,
            translated);
        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(InventoryAndEquipmentStatusScreenTranslationPatch));
        _ = AccessTools.Method(uiTextSkin.GetType(), "Apply")?.Invoke(uiTextSkin, null);
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
}
