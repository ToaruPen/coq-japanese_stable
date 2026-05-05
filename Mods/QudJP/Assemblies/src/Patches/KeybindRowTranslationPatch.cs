using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class KeybindRowTranslationPatch
{
    private const string Context = nameof(KeybindRowTranslationPatch);
    private const string NoneDisplay = "{{K|None}}";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.KeybindRow", "KeybindRow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: KeybindRowTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: KeybindRowTranslationPatch.setData(FrameworkDataElement) not found.");
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

            if (GetMemberValue(data, "KeyDescription") is not null)
            {
                ApplyKeybindDataRow(__instance, data);
                return false;
            }

            if (GetMemberValue(data, "CategoryDescription") is not null)
            {
                ApplyCategoryRow(__instance, data);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: KeybindRowTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static void ApplyKeybindDataRow(object instance, object data)
    {
        TrySetActive(GetMemberValue(instance, "categoryDisplay"), active: false);
        TrySetActive(GetMemberValue(instance, "bindingDisplay"), active: true);
        SetMemberValue(instance, "categoryRow", null);
        SetMemberValue(instance, "dataRow", data);

        var descriptionSource = GetStringMemberValue(data, "KeyDescription");
        if (descriptionSource is null) { descriptionSource = string.Empty; }
        var descriptionRoute = ObservabilityHelpers.ComposeContext(Context, "field=description");
        var translatedDescription = TranslateVisibleText(descriptionSource, descriptionRoute, "KeybindRow.KeyDescription");
        SetAppliedTranslatedText(
            GetMemberValue(instance, "description"),
            "{{C|" + descriptionSource + "}}",
            "{{C|" + translatedDescription + "}}",
            typeof(KeybindRowTranslationPatch));

        var bind1 = GetStringMemberValue(data, "Bind1");
        var bind2 = GetStringMemberValue(data, "Bind2");
        var bind3 = GetStringMemberValue(data, "Bind3");
        var bind4 = GetStringMemberValue(data, "Bind4");
        if (string.IsNullOrEmpty(bind1))
        {
            SetNoneText(GetMemberValue(instance, "box1"));
            SetNoneText(GetMemberValue(instance, "box2"));
            SetNoneText(GetMemberValue(instance, "box3"));
            SetNoneText(GetMemberValue(instance, "box4"));
            TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box2"), "gameObject"), active: false);
            TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box3"), "gameObject"), active: false);
            TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box4"), "gameObject"), active: false);
        }
        else
        {
            SetBoxText(GetMemberValue(instance, "box1"), "{{w|" + bind1 + "}}");
            TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box2"), "gameObject"), active: true);
            if (string.IsNullOrEmpty(bind2))
            {
                SetNoneText(GetMemberValue(instance, "box2"));
                SetNoneText(GetMemberValue(instance, "box3"));
                SetNoneText(GetMemberValue(instance, "box4"));
                TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box3"), "gameObject"), active: false);
                TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box4"), "gameObject"), active: false);
            }
            else
            {
                SetBoxText(GetMemberValue(instance, "box2"), "{{w|" + bind2 + "}}");
                TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box3"), "gameObject"), active: true);
                if (string.IsNullOrEmpty(bind3))
                {
                    SetNoneText(GetMemberValue(instance, "box3"));
                    SetNoneText(GetMemberValue(instance, "box4"));
                    TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box4"), "gameObject"), active: false);
                }
                else
                {
                    SetBoxText(GetMemberValue(instance, "box3"), "{{w|" + bind3 + "}}");
                    TrySetActive(GetMemberValue(GetBoxOrFallback(instance, "box4"), "gameObject"), active: true);
                    SetBoxText(GetMemberValue(instance, "box4"), string.IsNullOrEmpty(bind4) ? BuildTranslatedNoneText() : "{{w|" + bind4 + "}}");
                }
            }
        }

        SetForceUpdate(GetMemberValue(instance, "box1"));
        SetForceUpdate(GetMemberValue(instance, "box2"));
        SetForceUpdate(GetMemberValue(instance, "box3"));
        SetForceUpdate(GetMemberValue(instance, "box4"));
        _ = AccessTools.Method(instance.GetType(), "GetNavigationContext")?.Invoke(instance, null);
    }

    private static object GetBoxOrFallback(object instance, string boxName)
    {
        var box = GetMemberValue(instance, boxName);
        if (box is null) { box = instance; }
        return box;
    }

    private static void ApplyCategoryRow(object instance, object data)
    {
        TrySetActive(GetMemberValue(instance, "categoryDisplay"), active: true);
        TrySetActive(GetMemberValue(instance, "bindingDisplay"), active: false);
        SetMemberValue(instance, "categoryRow", data);
        SetMemberValue(instance, "dataRow", null);

        var rawCategoryDescription = GetStringMemberValue(data, "CategoryDescription");
        if (rawCategoryDescription is null) { rawCategoryDescription = string.Empty; }
        var categorySource = rawCategoryDescription.ToUpperInvariant();
        var categoryRoute = ObservabilityHelpers.ComposeContext(Context, "field=categoryDescription");
        var translatedCategory = UiBindingTranslationHelpers.TranslateCommandCategoryLabel(
            categorySource,
            categoryRoute,
            "KeybindRow.CategoryDescription");
        SetAppliedTranslatedText(
            GetMemberValue(instance, "categoryDescription"),
            "{{C|" + categorySource + "}}",
            "{{C|" + translatedCategory + "}}",
            typeof(KeybindRowTranslationPatch));

        _ = UITextSkinReflectionAccessor.SetCurrentText(
            GetMemberValue(instance, "categoryExpander"),
            GetBoolMemberValue(data, "Collapsed") ? "{{C|[+]}}" : "{{C|[-]}}",
            Context);
        _ = AccessTools.Method(instance.GetType(), "GetNavigationContext")?.Invoke(instance, null);
    }

    private static void SetNoneText(object? box)
    {
        var source = NoneDisplay;
        var translated = BuildTranslatedNoneText();
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(
                ObservabilityHelpers.ComposeContext(Context, "field=boxText"),
                "KeybindRow.NoneBinding",
                source,
                translated);
        }

        SetBoxText(box, translated);
    }

    private static string BuildTranslatedNoneText()
    {
        var translatedNone = Translator.Translate("None");
        return string.Equals(translatedNone, "None", StringComparison.Ordinal)
            ? NoneDisplay
            : "{{K|" + translatedNone + "}}";
    }

    private static void SetBoxText(object? box, string value)
    {
        if (box is null)
        {
            return;
        }

        SetMemberValue(box, "boxText", value);
    }

    private static void SetForceUpdate(object? box)
    {
        if (box is null)
        {
            return;
        }

        SetMemberValue(box, "forceUpdate", true);
    }

    private static string TranslateVisibleText(string source, string route, string family)
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
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    private static void SetAppliedTranslatedText(object? uiTextSkin, string source, string translated, Type patchType)
    {
        if (uiTextSkin is null)
        {
            return;
        }

        var value = translated;
        if (!string.Equals(source, translated, StringComparison.Ordinal)
            && uiTextSkin.GetType().Assembly != patchType.Assembly)
        {
            value = MessageFrameTranslator.MarkDirectTranslation(translated);
        }

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, value, Context);
        _ = AccessTools.Method(uiTextSkin.GetType(), "Apply")?.Invoke(uiTextSkin, null);
    }

    private static void TrySetActive(object? target, bool active)
    {
        if (target is null)
        {
            return;
        }

        if (AccessTools.Method(target.GetType(), "SetActive", new[] { typeof(bool) }) is MethodInfo method)
        {
            _ = method.Invoke(target, new object[] { active });
            return;
        }

        SetMemberValue(target, "activeSelf", active);
        SetMemberValue(target, "Active", active);
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

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
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
