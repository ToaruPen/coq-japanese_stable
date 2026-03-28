using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameSummaryScreenTranslationPatch
{
    private const string Context = nameof(GameSummaryScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("Qud.UI.GameSummaryScreen:UpdateMenuBars");
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve GameSummaryScreen.UpdateMenuBars for GameSummaryScreenTranslationPatch.");
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            var field = AccessTools.Field(__instance.GetType(), "keyMenuOptions");
            TranslateMenuOptionDescriptions(field?.GetValue(__instance));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GameSummaryScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateMenuOptionDescriptions(object? maybeCollection)
    {
        if (maybeCollection is not IEnumerable collection)
        {
            return;
        }

        foreach (var item in collection)
        {
            if (item is null)
            {
                continue;
            }

            var descriptionField = AccessTools.Field(item.GetType(), "Description");
            if (descriptionField is null || descriptionField.FieldType != typeof(string))
            {
                continue;
            }

            if (descriptionField.GetValue(item) is not string current || current.Length == 0)
            {
                continue;
            }

            if (UITextSkinTranslationPatch.IsProbablyAlreadyLocalizedText(current))
            {
                continue;
            }

            var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
                current,
                static visibleText => StringHelpers.TranslateExactOrLowerAsciiFallback(visibleText));
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                continue;
            }

            descriptionField.SetValue(item, translated);
            DynamicTextObservability.RecordTransform(Context, "MenuOption.Description", current, translated);
        }
    }
}
