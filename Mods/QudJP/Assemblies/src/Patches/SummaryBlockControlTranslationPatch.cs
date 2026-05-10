using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SummaryBlockControlTranslationPatch
{
    private const string Context = nameof(SummaryBlockControlTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(
            Context,
            "XRL.UI.Framework.SummaryBlockControl",
            "SummaryBlockControl");
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateTextSkinField(__instance, "text");
            TranslateTextSkinField(__instance, "title");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateTextSkinField(object instance, string fieldName)
    {
        var textSkin = UiBindingTranslationHelpers.GetMemberValue(instance, fieldName);
        var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            current!,
            static visible => ChargenStructuredTextTranslator.Translate(visible));
        if (string.Equals(current, translated, StringComparison.Ordinal))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + fieldName);
        DynamicTextObservability.RecordTransform(route, "Chargen.SummaryBlock", current!, translated);
        OwnerTextSetter.SetTranslatedText(
            textSkin,
            current!,
            translated,
            Context,
            typeof(SummaryBlockControlTranslationPatch));
    }
}
