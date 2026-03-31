using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QuestsStatusScreenTranslationPatch
{
    private const string Context = nameof(QuestsStatusScreenTranslationPatch);
    private const string DictionaryFile = "ui-quests.ja.json";
    private static readonly Regex QuestPrefixPattern =
        new Regex(@"\{\{B\|(?<label>quest:)\}\}\s", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.QuestsStatusScreen", "QuestsStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: QuestsStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: QuestsStatusScreenTranslationPatch.UpdateViewFromData not found.");
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

            var mapController = UiBindingTranslationHelpers.GetMemberValue(__instance, "mapController");
            if (mapController is null)
            {
                return;
            }

            var pins = UiBindingTranslationHelpers.GetMemberValue(mapController, "pins");
            if (pins is null || pins is string || pins is not IEnumerable enumerable)
            {
                return;
            }

            var mapScrollerPinItemType = AccessTools.TypeByName("MapScrollerPinItem");
            var index = 0;
            foreach (var pin in enumerable)
            {
                TranslatePin(pin, mapScrollerPinItemType, index);
                index++;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QuestsStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslatePin(object? pin, Type? mapScrollerPinItemType, int index)
    {
        if (pin is null)
        {
            return;
        }

        var pinItem = ResolvePinItem(pin, mapScrollerPinItemType);
        if (pinItem is null)
        {
            return;
        }

        var detailsText = UiBindingTranslationHelpers.GetMemberValue(pinItem, "detailsText");
        var current = UITextSkinReflectionAccessor.GetCurrentText(detailsText, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=detailsText[" + index + "]");
        var translated = TranslateQuestMapPinDetails(current!, route);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(detailsText, current!, translated, Context, typeof(QuestsStatusScreenTranslationPatch));
    }

    private static object? ResolvePinItem(object pin, Type? mapScrollerPinItemType)
    {
        if (mapScrollerPinItemType is not null)
        {
            var getComponent = AccessTools.Method(pin.GetType(), "GetComponent", new[] { typeof(Type) });
            if (getComponent is not null)
            {
                var component = getComponent.Invoke(pin, new object[] { mapScrollerPinItemType });
                if (component is not null)
                {
                    return component;
                }
            }
        }

        var pinItem = UiBindingTranslationHelpers.GetMemberValue(pin, "pinItem");
        if (pinItem is not null)
        {
            return pinItem;
        }

        return UiBindingTranslationHelpers.GetMemberValue(pin, "PinItem");
    }

    internal static string TranslateQuestMapPinDetails(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var changed = false;
        var translated = QuestPrefixPattern.Replace(
            source,
            match =>
            {
                var label = match.Groups["label"].Value;
                var translatedLabel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(label, DictionaryFile);
                if (translatedLabel is null || string.Equals(translatedLabel, label, StringComparison.Ordinal))
                {
                    return match.Value;
                }

                changed = true;
                return "{{B|" + translatedLabel + "}} ";
            });

        if (changed)
        {
            DynamicTextObservability.RecordTransform(route, "QuestsStatusScreen.MapPinDetails", source, translated);
        }

        return translated;
    }
}
