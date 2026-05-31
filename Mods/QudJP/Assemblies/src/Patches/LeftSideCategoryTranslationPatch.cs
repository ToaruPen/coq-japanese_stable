using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LeftSideCategoryTranslationPatch
{
    private const string Context = nameof(LeftSideCategoryTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(
            Context,
            "Qud.UI.LeftSideCategory",
            "LeftSideCategory");
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var textSkin = UiBindingTranslationHelpers.GetMemberValue(__instance, "text");
            var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
            var translated = UiBindingTranslationHelpers.TranslateVisibleText(
                current!,
                route,
                "LeftSideCategory.Text");
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                return;
            }

            OwnerTextSetter.SetTranslatedText(
                textSkin,
                current!,
                translated,
                Context,
                typeof(LeftSideCategoryTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
