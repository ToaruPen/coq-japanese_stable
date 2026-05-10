using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringDetailsLineTranslationPatch
{
    private const string Context = nameof(TinkeringDetailsLineTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(
            Context,
            "Qud.UI.TinkeringDetailsLine",
            "TinkeringDetailsLine");
    }

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null || GetBoolMemberValue(data, "category"))
            {
                return;
            }

            var modBitCostText = UiBindingTranslationHelpers.GetMemberValue(__instance, "modBitCostText");
            var current = UITextSkinReflectionAccessor.GetCurrentText(modBitCostText, Context);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var translated = current!;
            translated = ReplaceTranslatedFragment(translated, "{{K|| Bit Cost |}}");
            translated = ReplaceTranslatedFragment(translated, "{{K || Bit Cost |}}");
            translated = ReplaceTranslatedFragment(translated, "{{K|| Ingredients |}}");
            translated = ReplaceTranslatedFragment(translated, "-or-");
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                return;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "field=modBitCostText");
            DynamicTextObservability.RecordTransform(route, "TinkeringDetails.ModBitCostText", current!, translated);
            OwnerTextSetter.SetTranslatedText(
                modBitCostText,
                current!,
                translated,
                Context,
                typeof(TinkeringDetailsLineTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TinkeringDetailsLineTranslationPatch.Postfix failed: {0}", ex);
        }
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
