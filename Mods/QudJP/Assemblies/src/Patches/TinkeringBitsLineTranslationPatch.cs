using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringBitsLineTranslationPatch
{
    private const string Context = nameof(TinkeringBitsLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(Context, "Qud.UI.TinkeringBitsLine", "TinkeringBitsLine");
    }

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return;
            }

            TranslateText(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TinkeringBitsLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateText(object instance)
    {
        var text = UiBindingTranslationHelpers.GetMemberValue(instance, "text");
        var current = UITextSkinReflectionAccessor.GetCurrentText(text, Context);
        if (string.IsNullOrEmpty(current)
            || !TinkeringBitDescriptionTranslator.TryTranslateKnownDescriptionsInText(current!, out var translated)
            || string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        DynamicTextObservability.RecordTransform(route, "TinkeringBitsLine.Text", current!, translated);
        OwnerTextSetter.SetTranslatedText(
            text,
            current!,
            translated,
            Context,
            typeof(TinkeringBitsLineTranslationPatch));
    }
}
