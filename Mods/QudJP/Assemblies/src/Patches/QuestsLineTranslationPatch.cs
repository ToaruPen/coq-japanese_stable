using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QuestsLineTranslationPatch
{
    private const string Context = nameof(QuestsLineTranslationPatch);
    private const string DictionaryFile = "ui-quests.ja.json";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.QuestsLine", "QuestsLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: QuestsLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: QuestsLineTranslationPatch.setData(FrameworkDataElement) not found.");
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

            var type = __instance.GetType();
            TranslateStaticMenuOptions(type);
            TranslateExactTextField(__instance, "titleText", "titleText", "QuestsLine.TitleText");
            TranslateGiverText(__instance, "giverText", "giverText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QuestsLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateStaticMenuOptions(Type instanceType)
    {
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "categoryExpandOptions"), "categoryExpandOptions");
        TranslateMenuOptionCollection(GetStaticMemberValue(instanceType, "categoryCollapseOptions"), "categoryCollapseOptions");
    }

    private static void TranslateMenuOptionCollection(object? maybeCollection, string routeSuffix)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                index++;
                continue;
            }

            var current = GetStringMemberValue(item, "Description");
            if (!string.IsNullOrEmpty(current))
            {
                var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix + "[" + index + "]");
                var translated = TranslateExactLeaf(current!, route, "QuestsLine.MenuOption");
                if (!string.Equals(translated, current, StringComparison.Ordinal))
                {
                    SetMemberValue(item, "Description", translated);
                }
            }

            index++;
        }
    }

    private static void TranslateExactTextField(object instance, string memberName, string routeSuffix, string family)
    {
        var uiTextSkin = GetMemberValue(instance, memberName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = TranslateExactLeaf(current!, route, family);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(uiTextSkin, current!, translated, Context, typeof(QuestsLineTranslationPatch));
    }

    private static void TranslateGiverText(object instance, string memberName, string routeSuffix)
    {
        var uiTextSkin = GetMemberValue(instance, memberName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var parts = current!.Split(new[] { " / " }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < parts.Length; index++)
        {
            var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix + "[" + index + "]");
            var translated = TranslateExactLeaf(parts[index], route, "QuestsLine.GiverText");
            if (string.Equals(translated, parts[index], StringComparison.Ordinal))
            {
                continue;
            }

            parts[index] = translated;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        var translatedText = string.Join(" / ", parts);
        OwnerTextSetter.SetTranslatedText(uiTextSkin, current!, translatedText, Context, typeof(QuestsLineTranslationPatch));
    }

    private static string TranslateExactLeaf(string source, string route, string family)
    {
        var translated = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DictionaryFile);
        if (translated is null || string.Equals(translated, source, StringComparison.Ordinal))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return translated;
    }

    private static object? GetStaticMemberValue(Type type, string memberName)
    {
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(null);
    }

    private static object? GetMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetMemberValue(instance, memberName);

    private static string? GetStringMemberValue(object instance, string memberName) => UiBindingTranslationHelpers.GetStringMemberValue(instance, memberName);

    private static void SetMemberValue(object instance, string memberName, object? value) => UiBindingTranslationHelpers.SetMemberValue(instance, memberName, value);
}
