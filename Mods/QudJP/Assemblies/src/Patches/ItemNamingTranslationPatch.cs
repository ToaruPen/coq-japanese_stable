using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ItemNamingTranslationPatch
{
    private const string Context = nameof(ItemNamingTranslationPatch);

    private static readonly Regex OpportunityPattern =
        new Regex("^You swell with the inspiration to name your (?<item>.+?)\\. Do you wish to\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NameItemPattern =
        new Regex("^You name (?<item>.+?) '(?<name>.+)'\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var itemNamingType = AccessTools.TypeByName("XRL.World.Capabilities.ItemNaming");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (itemNamingType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve ItemNaming or GameObject.", Context);
            yield break;
        }

        var opportunity = AccessTools.Method(
            itemNamingType,
            "Opportunity",
            [
                gameObjectType,
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool),
            ]);
        if (opportunity is not null)
        {
            yield return opportunity;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Opportunity(...) not found.", Context);
        }

        var checkBestowals = AccessTools.Method(
            itemNamingType,
            "CheckBestowals",
            [
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(bool).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType(),
            ]);
        if (checkBestowals is not null)
        {
            yield return checkBestowals;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.CheckBestowals(...) not found.", Context);
        }

        var nameItem = AccessTools.Method(
            itemNamingType,
            "NameItem",
            [
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(bool),
                typeof(int),
                typeof(bool),
            ]);
        if (nameItem is not null)
        {
            yield return nameItem;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.NameItem(...) not found.", Context);
        }
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out var doesVerbTranslated))
        {
            translated = doesVerbTranslated;
            Record(route, family, "CheckBestowals.DoesVerb", source, translated);
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = OpportunityPattern.Match(stripped);
        if (match.Success)
        {
            var item = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                match.Groups["item"].Value,
                spans,
                match.Groups["item"]).Trim();
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "あなたは" + item + "に名付けたい衝動に駆られた。そうしますか？",
                spans,
                stripped.Length);
            Record(route, family, "Opportunity", source, translated);
            return true;
        }

        match = NameItemPattern.Match(stripped);
        if (match.Success)
        {
            var item = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                match.Groups["item"].Value,
                spans,
                match.Groups["item"]).Trim();
            var name = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                match.Groups["name"].Value,
                spans,
                match.Groups["name"]).Trim();
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "あなたは" + item + "に「" + name + "」と名付けた。",
                spans,
                stripped.Length);
            Record(route, family, "NameItem", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
