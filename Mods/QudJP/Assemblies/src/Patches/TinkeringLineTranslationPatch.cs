using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringLineTranslationPatch
{
    private const string Context = nameof(TinkeringLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(Context, "Qud.UI.TinkeringLine", "TinkeringLine");
    }

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return;
            }

            if (GetBoolMemberValue(data, "category"))
            {
                TranslateExactText(
                    UiBindingTranslationHelpers.GetMemberValue(__instance, "categoryText"),
                    "field=categoryText",
                    "TinkeringLine.CategoryText");
                return;
            }

            TranslateTextFieldWithFragments(
                UiBindingTranslationHelpers.GetMemberValue(__instance, "text"),
                "field=text",
                "TinkeringLine.Text",
                "<no applicable items>");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TinkeringLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateExactText(object? uiTextSkin, string routeDetail, string family)
    {
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current)
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(current!, out var translated)
            || string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, routeDetail);
        DynamicTextObservability.RecordTransform(route, family, current!, translated);
        OwnerTextSetter.SetTranslatedText(
            uiTextSkin,
            current!,
            translated,
            Context,
            typeof(TinkeringLineTranslationPatch));
    }

    private static void TranslateTextFieldWithFragments(object? uiTextSkin, string routeDetail, string family, params string[] fragments)
    {
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = current!;
        for (var index = 0; index < fragments.Length; index++)
        {
            translated = ReplaceTranslatedFragment(translated, fragments[index]);
        }

        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, routeDetail);
        DynamicTextObservability.RecordTransform(route, family, current!, translated);
        OwnerTextSetter.SetTranslatedText(
            uiTextSkin,
            current!,
            translated,
            Context,
            typeof(TinkeringLineTranslationPatch));
    }

    private static string ReplaceTranslatedFragment(string source, string fragment)
    {
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(fragment, out var translatedFragment)
            || string.Equals(translatedFragment, fragment, StringComparison.Ordinal))
        {
            return source;
        }

        return source.Replace(fragment, translatedFragment);
    }

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        return UiBindingTranslationHelpers.GetMemberValue(instance, memberName) as bool? ?? false;
    }
}
