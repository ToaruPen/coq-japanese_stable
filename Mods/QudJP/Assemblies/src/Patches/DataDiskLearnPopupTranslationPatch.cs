using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DataDiskLearnPopupTranslationPatch
{
    private const string Context = nameof(DataDiskLearnPopupTranslationPatch);

    private static readonly Regex ItemModificationPattern =
        new Regex(
            "^You learn the item modification \\{\\{W\\|(?<mod>[\\s\\S]+)\\}\\}\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuildRecipePattern =
        new Regex(
            "^You learn to build (?<item>[\\s\\S]+)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.DataDisk");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target types.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [inventoryActionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) not found.", Context);
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var itemModificationMatch = ItemModificationPattern.Match(source);
        if (itemModificationMatch.Success)
        {
            translated = string.Concat(
                "アイテム改造{{W|",
                itemModificationMatch.Groups["mod"].Value,
                "}}を習得した。");
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".ItemModification", source, translated);
            return true;
        }

        var buildRecipeMatch = BuildRecipePattern.Match(source);
        if (buildRecipeMatch.Success)
        {
            translated = string.Concat(
                buildRecipeMatch.Groups["item"].Value,
                "を作成する方法を習得した。");
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".BuildRecipe", source, translated);
            return true;
        }

        translated = source;
        return false;
    }
}
