using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FactionsLineDataTranslationPatch
{
    private const BindingFlags PublicInstanceFlags = BindingFlags.Instance | BindingFlags.Public;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.FactionsLineData", "FactionsLineData");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: FactionsLineDataTranslationPatch target type not found.");
            return null;
        }

        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.IRenderable");
        var method = renderableType is null
            ? null
            : AccessTools.Method(targetType, "set", new[] { typeof(string), typeof(string), renderableType, typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: FactionsLineDataTranslationPatch.set(string,string,IRenderable,bool) not found.");
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            TranslateLineData(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: FactionsLineDataTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static void TranslateLineData(object item)
    {
        var itemType = item.GetType();
        var labelField = itemType.GetField("label", PublicInstanceFlags);
        var idField = itemType.GetField("id", PublicInstanceFlags);
        var factionId = idField?.FieldType == typeof(string)
            ? idField.GetValue(item) as string
            : null;
        string? localizedLabel = null;

        if (labelField?.FieldType == typeof(string))
        {
            var currentLabel = labelField.GetValue(item) as string;
            if (!string.IsNullOrEmpty(currentLabel))
            {
                localizedLabel = currentLabel!;
                if (!FactionsStatusScreenTranslationPatch.IsAlreadyLocalizedFactionText(currentLabel!))
                {
                    localizedLabel = FactionsStatusScreenTranslationPatch.TranslateFactionText(
                        currentLabel!,
                        ObservabilityHelpers.ComposeContext(nameof(FactionsLineDataTranslationPatch), "field=label"));
                }

                if (string.Equals(currentLabel, localizedLabel, StringComparison.Ordinal)
                    && FactionsStatusScreenTranslationPatch.TryTranslateFactionLabelFromId(currentLabel!, factionId, out var fallbackLabel))
                {
                    localizedLabel = fallbackLabel;
                }

                if (!string.Equals(currentLabel, localizedLabel, StringComparison.Ordinal))
                {
                    labelField.SetValue(item, localizedLabel);
                }
            }
        }

        var searchTextProperty = itemType.GetProperty("searchText", PublicInstanceFlags);
        if (searchTextProperty?.PropertyType != typeof(string)
            || !searchTextProperty.CanRead
            || !searchTextProperty.CanWrite)
        {
            return;
        }

        var existingSearchText = searchTextProperty.GetValue(item) as string;
        if (string.IsNullOrEmpty(existingSearchText))
        {
            return;
        }

        var localizedFragments = new List<string?>();
        if (!string.IsNullOrWhiteSpace(localizedLabel))
        {
            localizedFragments.Add(localizedLabel);
        }

        if (FactionsStatusScreenTranslationPatch.TryGetLocalizedFactionSearchFragments(factionId, out var searchFragments))
        {
            localizedFragments.AddRange(searchFragments);
        }

        var rebuiltSearchText = LocalizedSearchTextBuilder.Build(existingSearchText, localizedFragments);
        if (!string.IsNullOrEmpty(rebuiltSearchText)
            && !string.Equals(existingSearchText, rebuiltSearchText, StringComparison.Ordinal))
        {
            searchTextProperty.SetValue(item, rebuiltSearchText);
        }
    }
}
