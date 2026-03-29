using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PickGameObjectScreenTranslationPatch
{
    private const string Context = nameof(PickGameObjectScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.PickGameObjectScreen", "PickGameObjectScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: PickGameObjectScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: PickGameObjectScreenTranslationPatch.UpdateViewFromData(bool) not found.");
        }

        return method;
    }

    public static void Prefix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var type = __instance.GetType();
            TranslateStringFieldsInCollection(
                AccessTools.Field(type, "defaultMenuOptions")?.GetValue(__instance),
                "PickGameObject.Description",
                "Description");
            TranslateStringFieldsInCollection(
                AccessTools.Field(type, "getItemMenuOptions")?.GetValue(__instance),
                "PickGameObject.Description",
                "Description");
            TranslateStringField(
                AccessTools.Field(type, "TAKE_ALL")?.GetValue(__instance),
                "PickGameObject.Description",
                "Description");
            TranslateStringField(
                AccessTools.Field(type, "STORE_ITEM")?.GetValue(__instance),
                "PickGameObject.Description",
                "Description");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PickGameObjectScreenTranslationPatch.Prefix failed: {0}", ex);
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
