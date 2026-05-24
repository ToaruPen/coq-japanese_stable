using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LocationFinderPopupTranslationPatch
{
    private const string Context = nameof(LocationFinderPopupTranslationPatch);

    private static readonly Regex DiscoverPattern = new(
        "^You discover (?<location>[\\s\\S]+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TravelPattern = new(
        "^You traveled to (?<location>[\\s\\S]+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = GameTypeResolver.FindType("XRL.World.Parts.LocationFinder", "LocationFinder");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "TriggerFind", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.TriggerFind target not found.", Context);
            return targets;
        }

        targets.Add(method);
        return targets;
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

        if (TryTranslateTemplate(
                source,
                route,
                family + ".LocationFinderDiscover",
                DiscoverPattern,
                "You discover {0}!",
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                route,
                family + ".LocationFinderTravel",
                TravelPattern,
                "You traveled to {0}!",
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string key,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate(key);
        if (string.Equals(template, key, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var location = TranslateLocationCapture(match.Groups["location"].Value);
        translated = string.Format(CultureInfo.InvariantCulture, template, location);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static string TranslateLocationCapture(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var translated)
                ? translated
                : visible);
    }
}
