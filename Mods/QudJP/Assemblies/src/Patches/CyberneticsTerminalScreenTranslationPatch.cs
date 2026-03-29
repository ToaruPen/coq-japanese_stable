using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsTerminalScreenTranslationPatch
{
    private const string Context = nameof(CyberneticsTerminalScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CyberneticsTerminalScreen", "CyberneticsTerminalScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CyberneticsTerminalScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "Show", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: CyberneticsTerminalScreenTranslationPatch.Show() not found.");
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

            TranslateFooter(__instance);
            TranslateMenuOptions(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CyberneticsTerminalScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateFooter(object instance)
    {
        var footerTextSkin = GetMemberValue(instance, "footerTextSkin");
        var current = UITextSkinReflectionAccessor.GetCurrentText(footerTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=footerText");
        var translated = TranslateVisibleText(current!, route, "CyberneticsTerminal.FooterText");
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        SetMemberValue(instance, "FooterText", translated);
        OwnerTextSetter.SetTranslatedText(
            footerTextSkin,
            current!,
            translated,
            Context,
            typeof(CyberneticsTerminalScreenTranslationPatch));
    }

    private static void TranslateMenuOptions(object instance)
    {
        if (GetMemberValue(instance, "keyMenuOptions") is not IEnumerable keyMenuOptions)
        {
            return;
        }

        var index = 0;
        foreach (var menuOption in keyMenuOptions)
        {
            if (menuOption is null)
            {
                index++;
                continue;
            }

            var current = GetStringMemberValue(menuOption, "Description");
            if (string.IsNullOrEmpty(current))
            {
                index++;
                continue;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "field=keyMenuOptions[" + index + "]");
            var translated = TranslateVisibleText(current!, route, "CyberneticsTerminal.MenuOption");
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                SetMemberValue(menuOption, "Description", translated);
            }

            index++;
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
