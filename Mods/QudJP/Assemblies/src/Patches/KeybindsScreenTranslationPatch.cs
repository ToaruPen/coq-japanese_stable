using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class KeybindsScreenTranslationPatch
{
    private const string Context = nameof(KeybindsScreenTranslationPatch);
    private static readonly Regex InputTypeTextPattern =
        new Regex("^\\{\\{C\\|(?<prefix>[^}]*)\\}\\} \\{\\{c\\|(?<device>[^}]*)\\}\\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.KeybindsScreen", "KeybindsScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: KeybindsScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "QueryKeybinds", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: KeybindsScreenTranslationPatch.QueryKeybinds() not found.");
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

            TranslateInputTypeText(__instance);
            TranslateMenuItems(__instance);
            TranslateStaticMenuOption(__instance.GetType(), "REMOVE_BIND");
            TranslateStaticMenuOption(__instance.GetType(), "RESTORE_DEFAULTS");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: KeybindsScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateInputTypeText(object instance)
    {
        var inputTypeText = GetMemberValue(instance, "inputTypeText");
        var current = UITextSkinReflectionAccessor.GetCurrentText(inputTypeText, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=inputTypeText");
        var translated = TranslateInputTypeTextValue(current!, route);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(inputTypeText, current!, translated, Context, typeof(KeybindsScreenTranslationPatch));
    }

    private static string TranslateInputTypeTextValue(string source, string route)
    {
        var match = InputTypeTextPattern.Match(source);
        if (!match.Success)
        {
            return TranslateVisibleText(source, route, "KeybindsScreen.InputType");
        }

        var prefix = TranslateVisibleText(match.Groups["prefix"].Value, route, "KeybindsScreen.InputType");
        var device = TranslateVisibleText(match.Groups["device"].Value, route, "KeybindsScreen.InputDevice");
        var translated = "{{C|" + prefix + "}} {{c|" + device + "}}";
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "KeybindsScreen.InputTypeText", source, translated);
        }

        return translated;
    }

    private static void TranslateMenuItems(object instance)
    {
        if (GetMemberValue(instance, "menuItems") is not IEnumerable menuItems)
        {
            return;
        }

        foreach (var item in menuItems)
        {
            if (item is null)
            {
                continue;
            }

            var categoryDescription = GetStringMemberValue(item, "CategoryDescription");
            if (!string.IsNullOrEmpty(categoryDescription))
            {
                var route = ObservabilityHelpers.ComposeContext(Context, "field=CategoryDescription");
                var translated = UiBindingTranslationHelpers.TranslateCommandCategoryLabel(
                    categoryDescription!,
                    route,
                    "KeybindsScreen.CategoryDescription");
                SetMemberValue(item, "CategoryDescription", translated);
            }

            var keyDescription = GetStringMemberValue(item, "KeyDescription");
            if (!string.IsNullOrEmpty(keyDescription))
            {
                var route = ObservabilityHelpers.ComposeContext(Context, "field=KeyDescription");
                var translated = TranslateVisibleText(keyDescription!, route, "KeybindsScreen.KeyDescription");
                SetMemberValue(item, "KeyDescription", translated);
            }
        }
    }

    private static void TranslateStaticMenuOption(Type instanceType, string fieldName)
    {
        var field = AccessTools.Field(instanceType, fieldName);
        var menuOption = field?.GetValue(null);
        if (menuOption is null)
        {
            return;
        }

        TranslateMenuOptionMember(menuOption, "Description", fieldName + ".Description");
        TranslateMenuOptionMember(menuOption, "KeyDescription", fieldName + ".KeyDescription");
    }

    private static void TranslateMenuOptionMember(object menuOption, string memberName, string routeSuffix)
    {
        var current = GetStringMemberValue(menuOption, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = TranslateVisibleText(current!, route, "KeybindsScreen.MenuOption");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            SetMemberValue(menuOption, memberName, translated);
        }
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
