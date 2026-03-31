using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class JournalStatusScreenTranslationPatch
{
    private const string Context = nameof(JournalStatusScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.JournalStatusScreen", "JournalStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: JournalStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: JournalStatusScreenTranslationPatch.UpdateViewFromData not found.");
        }

        return method;
    }

    public static void Prefix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateCategoryInfos(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalStatusScreenTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateCategoryText(__instance);
            TranslateMenuOptionDescription(__instance, "CMD_INSERT");
            TranslateMenuOptionDescription(__instance, "CMD_DELETE");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateCategoryInfos(object instance)
    {
        if (UiBindingTranslationHelpers.GetMemberValue(instance, "categoryInfos") is not IEnumerable categoryInfos)
        {
            return;
        }

        var index = 0;
        foreach (var categoryInfo in categoryInfos)
        {
            if (categoryInfo is null)
            {
                index++;
                continue;
            }

            var current = UiBindingTranslationHelpers.GetStringMemberValue(categoryInfo, "Name");
            if (string.IsNullOrEmpty(current))
            {
                index++;
                continue;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "categoryInfos[" + index + "].Name");
            var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "JournalStatusScreen.CategoryInfo");
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                UiBindingTranslationHelpers.SetMemberValue(categoryInfo, "Name", translated);
            }

            index++;
        }
    }

    private static void TranslateCategoryText(object instance)
    {
        var textSkin = UiBindingTranslationHelpers.GetMemberValue(instance, "categoryText");
        var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=categoryText");
        var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "JournalStatusScreen.CategoryText");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            OwnerTextSetter.SetTranslatedText(
                textSkin,
                current!,
                translated,
                Context,
                typeof(JournalStatusScreenTranslationPatch));
        }
    }

    private static void TranslateMenuOptionDescription(object instance, string memberName)
    {
        var menuOption = UiBindingTranslationHelpers.GetMemberValue(instance, memberName);
        if (menuOption is null)
        {
            return;
        }

        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, "Description");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, memberName + ".Description");
        var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "JournalStatusScreen.MenuOption");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(menuOption, "Description", translated);
        }
    }
}
