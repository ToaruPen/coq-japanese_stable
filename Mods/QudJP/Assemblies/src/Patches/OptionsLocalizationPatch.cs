using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class OptionsLocalizationPatch
{
    private const string Context = nameof(OptionsLocalizationPatch);

    private const string TargetTypeName = "Qud.UI.OptionsScreen";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(TargetTypeName + ":Show");
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.OptionsScreen.Show(). Patch will not apply.");
        }

        return method;
    }

    public static void Prefix(object __instance)
    {
        try
        {
            if (__instance is null)
            {
                Trace.TraceError("QudJP: OptionsLocalizationPatch.Prefix received null __instance. Skipping translation.");
                return;
            }

            var type = __instance.GetType();

            var menuItemsField = AccessTools.Field(type, "menuItems");
            TranslateStringFieldsInCollection(menuItemsField?.GetValue(__instance), "Options.Title", "Title");
            TranslateStringFieldsInCollection(menuItemsField?.GetValue(__instance), "Options.HelpText", "HelpText");

            var filteredMenuItemsField = AccessTools.Field(type, "filteredMenuItems");
            TranslateStringFieldsInCollection(filteredMenuItemsField?.GetValue(__instance), "Options.Title", "Title");
            TranslateStringFieldsInCollection(filteredMenuItemsField?.GetValue(__instance), "Options.HelpText", "HelpText");

            var defaultMenuOptionsField = AccessTools.Field(type, "defaultMenuOptions");
            TranslateStringFieldsInCollection(defaultMenuOptionsField?.GetValue(null), "Options.Description", "Description");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: OptionsLocalizationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void TranslateStringFieldsInCollection(object? maybeCollection, string family, string fieldName)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        foreach (var item in enumerable)
        {
            TranslateStringField(item, family, fieldName);
        }
    }

    private static void TranslateStringField(object? item, string family, string fieldName)
    {
        if (item is null)
        {
            return;
        }

        var field = AccessTools.Field(item.GetType(), fieldName);
        if (field is null || field.FieldType != typeof(string))
        {
            return;
        }

        var current = field.GetValue(item) as string;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateProducerText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, family, current, translated);
        field.SetValue(item, translated);
    }

    private static string TranslateProducerText(string source)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0
            || UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(stripped, Context))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var translated)
                ? translated
                : visible);
    }
}
