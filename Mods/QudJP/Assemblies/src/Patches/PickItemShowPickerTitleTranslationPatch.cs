using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PickItemShowPickerTitleTranslationPatch
{
    private const string Context = nameof(PickItemShowPickerTitleTranslationPatch);
    private const string GetItemDialogStyleName = "GetItemDialog";

    private static readonly Regex ContainerTitlePattern =
        new Regex("^(?<verb>Opening|Examining) (?<object>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.PickItem", "PickItem");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: PickItemShowPickerTitleTranslationPatch target type not found.");
            return null;
        }

        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        var cellType = GameTypeResolver.FindType("XRL.World.Cell", "Cell");
        var styleType = AccessTools.Inner(targetType, "PickItemDialogStyle");
        if (gameObjectType is null || cellType is null || styleType is null)
        {
            Trace.TraceError("QudJP: PickItemShowPickerTitleTranslationPatch dependent type not found.");
            return null;
        }

        var gameObjectListType = typeof(System.Collections.Generic.IList<>).MakeGenericType(gameObjectType);
        var regenerateType = typeof(Func<>).MakeGenericType(typeof(System.Collections.Generic.List<>).MakeGenericType(gameObjectType));
        var method = AccessTools.Method(
            targetType,
            "ShowPicker",
            new[]
            {
                gameObjectListType,
                typeof(bool).MakeByRefType(),
                typeof(string),
                styleType,
                gameObjectType,
                gameObjectType,
                cellType,
                typeof(string),
                typeof(bool),
                regenerateType,
                typeof(bool),
                typeof(bool),
                typeof(bool),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: PickItemShowPickerTitleTranslationPatch.ShowPicker overload not found.");
        }

        return method;
    }

    public static void Prefix(object? __3, object? __5, ref string? __7)
    {
        try
        {
            __7 = TranslateTitleForGetItemDialog(__7, __3, __5);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PickItemShowPickerTitleTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string? TranslateTitleForGetItemDialog(string? source, object? style, object? container)
    {
        if (LiquidVolumeTranslationPatch.TryTranslatePickItemTitle(ref source))
        {
            return source;
        }

        if (string.IsNullOrEmpty(source)
            || container is null
            || !string.Equals(style?.ToString(), GetItemDialogStyleName, StringComparison.Ordinal))
        {
            return source;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ContainerTitlePattern.Match(stripped);
        if (!match.Success)
        {
            return source;
        }

        var objectName = StripEnglishArticle(match.Groups["object"].Value);
        var visibleTranslation = match.Groups["verb"].Value switch
        {
            "Opening" => objectName + "を開いています",
            "Examining" => objectName + "を調べています",
            _ => stripped,
        };
        if (string.Equals(visibleTranslation, stripped, StringComparison.Ordinal))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            visibleTranslation,
            spans,
            stripped.Length);
        DynamicTextObservability.RecordTransform(Context, "PickItem.ContainerTitle", stripped, translated);
        return translated;
    }

    private static string StripEnglishArticle(string source)
    {
        if (source.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(2);
        }

        if (source.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(3);
        }

        if (source.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(4);
        }

        return source;
    }
}
