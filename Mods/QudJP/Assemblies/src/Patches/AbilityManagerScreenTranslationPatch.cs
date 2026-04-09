using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityManagerScreenTranslationPatch
{
    private const string Context = nameof(AbilityManagerScreenTranslationPatch);

    private static readonly string[] RawFragmentKeys =
    {
        "Type: ",
        "search: ",
        "sort: ",
        "custom",
        "by class",
    };

    private static readonly string[] TypeLinePrefixes =
    {
        "{{y|Type: }}",
        "Type: ",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.AbilityManagerScreen", "AbilityManagerScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        foreach (var methodName in new[] { "FilterItems", "UpdateMenuBars", "HandleHighlightLeft" })
        {
            Type[] parameterTypes;
            if (string.Equals(methodName, "HandleHighlightLeft", StringComparison.Ordinal))
            {
                var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
                if (frameworkDataElementType is null)
                {
                    Trace.TraceWarning(
                        "QudJP: {0} could not resolve FrameworkDataElement while targeting HandleHighlightLeft; falling back to object.",
                        Context);
                    frameworkDataElementType = typeof(object);
                }

                parameterTypes = new[] { frameworkDataElementType };
            }
            else
            {
                parameterTypes = Type.EmptyTypes;
            }

            var method = AccessTools.Method(targetType, methodName, parameterTypes);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.{1} not found.", Context, methodName);
                continue;
            }

            yield return method;
        }
    }

    public static void Postfix(MethodBase? __originalMethod, object? __instance, object[]? __args = null)
    {
        try
        {
            if (__instance is null || __originalMethod is null)
            {
                return;
            }

            switch (__originalMethod.Name)
            {
                case "FilterItems":
                    TranslateFilteredItems(__instance);
                    TranslateFilterDescription(__instance);
                    return;

                case "UpdateMenuBars":
                    TranslateHotkeyBar(__instance);
                    return;

                case "HandleHighlightLeft":
                    TranslateDetailsPane(__instance, __args);
                    return;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateFilteredItems(object instance)
    {
        if (UiBindingTranslationHelpers.GetMemberValue(instance, "filteredItems") is not IEnumerable filteredItems)
        {
            return;
        }

        foreach (var item in filteredItems)
        {
            if (item is null)
            {
                continue;
            }

            if (UiBindingTranslationHelpers.GetStringMemberValue(item, "category") is { Length: > 0 } category)
            {
                var translatedCategory = TranslateFragment(category);
                if (!string.Equals(translatedCategory, category, StringComparison.Ordinal))
                {
                    UiBindingTranslationHelpers.SetMemberValue(item, "category", translatedCategory);
                }
            }

            var ability = UiBindingTranslationHelpers.GetMemberValue(item, "ability");
            if (ability is null)
            {
                continue;
            }

            TranslateAbilityMember(ability, "DisplayName");
            TranslateAbilityMember(ability, "Class");
        }
    }

    private static void TranslateFilterDescription(object instance)
    {
        var filterItems = GetStaticMemberValue(instance.GetType(), "FILTER_ITEMS");
        if (filterItems is null)
        {
            return;
        }

        TranslateMenuOptionMember(filterItems, "Description", "AbilityManagerScreen.Filter");
    }

    private static void TranslateHotkeyBar(object instance)
    {
        var hotkeyBar = UiBindingTranslationHelpers.GetMemberValue(instance, "hotkeyBar");
        var choices = hotkeyBar is null ? null : UiBindingTranslationHelpers.GetMemberValue(hotkeyBar, "choices");
        if (choices is null || choices is string || choices is not IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        var changed = false;
        foreach (var choice in enumerable)
        {
            if (choice is null)
            {
                index++;
                continue;
            }

            changed |= TranslateMenuOptionMember(choice, "Description", "AbilityManagerScreen.Hotkey[" + index + "].Description");
            changed |= TranslateMenuOptionMember(choice, "KeyDescription", "AbilityManagerScreen.Hotkey[" + index + "].KeyDescription");
            index++;
        }

        if (changed && hotkeyBar is not null)
        {
            InvokeBeforeShow(hotkeyBar, enumerable);
        }
    }

    private static void TranslateDetailsPane(object instance, object[]? args)
    {
        if (args is not { Length: > 0 } || args[0] is null)
        {
            TranslateTextField(instance, "rightSideHeaderText", "text", "AbilityManagerScreen.Header");
            TranslateTextField(instance, "rightSideDescriptionArea", "text", "AbilityManagerScreen.Details");
            return;
        }

        var element = args[0];
        var ability = UiBindingTranslationHelpers.GetMemberValue(element, "ability");
        if (ability is not null)
        {
            var translatedName = TranslateFragment(GetRequiredStringMemberValue(ability, "DisplayName"));
            var translatedClass = TranslateFragment(GetRequiredStringMemberValue(ability, "Class"));
            var translatedDescription = TranslateBindingText(
                GetRequiredStringMemberValue(ability, "Description"),
                "AbilityManagerScreen.Details.Body");

            SetTextField(instance, "rightSideHeaderText", translatedName);
            if (string.IsNullOrEmpty(translatedClass))
            {
                SetTextField(instance, "rightSideDescriptionArea", translatedDescription);
            }
            else
            {
                SetTextField(
                    instance,
                    "rightSideDescriptionArea",
                    "{{y|" + TranslateFragment("Type: ") + "}}" + translatedClass + "\n\n" + translatedDescription);
            }

            return;
        }

        var category = UiBindingTranslationHelpers.GetStringMemberValue(element, "category");
        SetTextField(instance, "rightSideHeaderText", TranslateFragment(category ?? string.Empty));
        SetTextField(instance, "rightSideDescriptionArea", string.Empty);
    }

    private static void TranslateTextField(object instance, string fieldName, string textMemberName, string family)
    {
        var field = UiBindingTranslationHelpers.GetMemberValue(instance, fieldName);
        if (field is null)
        {
            return;
        }

        var current = UiBindingTranslationHelpers.GetStringMemberValue(field, textMemberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateBindingText(current!, family);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(field, textMemberName, translated);
        }
    }

    private static void SetTextField(object instance, string fieldName, string value)
    {
        var field = UiBindingTranslationHelpers.GetMemberValue(instance, fieldName);
        if (field is null)
        {
            return;
        }

        UiBindingTranslationHelpers.SetMemberValue(field, "text", value);
    }

    private static bool TranslateMenuOptionMember(object menuOption, string memberName, string routeSuffix)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return false;
        }

        var translated = TranslateBindingText(current!, routeSuffix);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(menuOption, memberName, translated);
            return true;
        }

        return false;
    }

    private static void TranslateAbilityMember(object ability, string memberName)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(ability, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateFragment(current!);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(ability, memberName, translated);
        }
    }

    private static string GetRequiredStringMemberValue(object instance, string memberName)
    {
        var value = UiBindingTranslationHelpers.GetStringMemberValue(instance, memberName);
        if (value is not null)
        {
            return value;
        }

        Trace.TraceWarning("QudJP: {0} missing string member '{1}' on '{2}'. Falling back to empty string.", Context, memberName, instance.GetType().FullName);
        return string.Empty;
    }

    private static string TranslateBindingText(string source, string family)
    {
        var translated = TranslateRawFragments(source);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            translated = StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact)
                ? exact
                : source;
        }

        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(Context, family, source, translated);
        }

        return translated;
    }

    private static string TranslateRawFragments(string source)
    {
        var translated = source;
        translated = TranslateTypeLineClassToken(translated);

        foreach (var key in RawFragmentKeys)
        {
            if (translated.IndexOf(key, StringComparison.Ordinal) < 0
                || !StringHelpers.TryGetTranslationExactOrLowerAscii(key, out var fragmentTranslation))
            {
                continue;
            }

            translated = translated.Replace(key, fragmentTranslation);
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii("Maneuvers", out var maneuversTranslation))
        {
            translated = translated.Replace("Maneuvers", maneuversTranslation);
        }

        return translated;
    }

    private static string TranslateTypeLineClassToken(string source)
    {
        foreach (var prefix in TypeLinePrefixes)
        {
            var prefixIndex = source.IndexOf(prefix, StringComparison.Ordinal);
            if (prefixIndex < 0)
            {
                continue;
            }

            var classStart = prefixIndex + prefix.Length;
            var classEnd = source.IndexOf('\n', classStart);
            if (classEnd < 0)
            {
                classEnd = source.Length;
            }

            if (classEnd <= classStart)
            {
                return source;
            }

            var classToken = source.Substring(classStart, classEnd - classStart);
            if (!StringHelpers.TryGetTranslationExactOrLowerAscii(classToken, out var translatedClass)
                || string.Equals(translatedClass, classToken, StringComparison.Ordinal))
            {
                return source;
            }

            return source.Substring(0, classStart) + translatedClass + source.Substring(classEnd);
        }

        return source;
    }

    private static string TranslateFragment(string source)
    {
        return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            ? translated
            : source;
    }

    private static object? GetStaticMemberValue(Type targetType, string memberName)
    {
        var property = AccessTools.Property(targetType, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(targetType, memberName);
        return field?.GetValue(null);
    }

    private static void InvokeBeforeShow(object hotkeyBar, IEnumerable menuOptions)
    {
        var beforeShow = AccessTools.Method(hotkeyBar.GetType(), "BeforeShow");
        if (beforeShow is null)
        {
            return;
        }

        var parameterCount = beforeShow.GetParameters().Length;
        if (parameterCount == 0)
        {
            _ = beforeShow.Invoke(hotkeyBar, null);
            return;
        }

        if (parameterCount == 1)
        {
            _ = beforeShow.Invoke(hotkeyBar, new object?[] { menuOptions });
            return;
        }

        _ = beforeShow.Invoke(hotkeyBar, new object?[] { null, menuOptions });
    }
}
