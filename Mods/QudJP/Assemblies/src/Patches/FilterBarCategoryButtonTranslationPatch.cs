using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FilterBarCategoryButtonTranslationPatch
{
    private const string Context = nameof(FilterBarCategoryButtonTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.FilterBarCategoryButton", "FilterBarCategoryButton");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: FilterBarCategoryButtonTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "SetCategory", new[] { typeof(string), typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: FilterBarCategoryButtonTranslationPatch.SetCategory(string, string) not found.");
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

            TranslateTooltip(__instance);
            TranslateButtonText(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: FilterBarCategoryButtonTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateTooltip(object instance)
    {
        var tooltip = GetStringMemberValue(instance, "Tooltip");
        if (string.IsNullOrEmpty(tooltip))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=Tooltip");
        var translated = TranslateVisibleText(tooltip!, route, "FilterBarCategoryButton.Tooltip");
        if (!string.Equals(translated, tooltip, StringComparison.Ordinal))
        {
            SetMemberValue(instance, "Tooltip", translated);
            OwnerTextSetter.SetTranslatedText(
                GetMemberValue(instance, "tooltipText"),
                tooltip!,
                translated,
                Context,
                typeof(FilterBarCategoryButtonTranslationPatch));
        }
    }

    private static void TranslateButtonText(object instance)
    {
        var textSkin = GetMemberValue(instance, "text");
        var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = TranslateVisibleText(current!, route, "FilterBarCategoryButton.Text");
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            OwnerTextSetter.SetTranslatedText(
                textSkin,
                current!,
                translated,
                Context,
                typeof(FilterBarCategoryButtonTranslationPatch));
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
