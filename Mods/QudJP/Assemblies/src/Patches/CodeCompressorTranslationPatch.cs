using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CodeCompressorTranslationPatch
{
    private const string Context = nameof(CodeCompressorTranslationPatch);
    private const string TemplateKey = "Error decoding build code - Required Mod \"{0}\" not found.";

    private static readonly Regex RequiredModMissingPattern = new(
        "^Error decoding build code - Required Mod \\\"(?<mod>[\\s\\S]*)\\\" not found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.CharacterBuilds.CodeCompressor");
        var moduleType = AccessTools.TypeByName("XRL.CharacterBuilds.AbstractEmbarkBuilderModule");
        if (targetType is null || moduleType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var moduleListType = typeof(List<>).MakeGenericType(moduleType);
        var method = AccessTools.Method(targetType, "loadCode", new[] { typeof(string), moduleListType, typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.CodeCompressor.loadCode target not found.", Context);
            yield break;
        }

        yield return method;
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
        _ = family;

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

        var match = RequiredModMissingPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate(TemplateKey);
        if (string.Equals(template, TemplateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        try
        {
            translated = string.Format(
                CultureInfo.InvariantCulture,
                template,
                match.Groups["mod"].Value);
        }
        catch (FormatException ex)
        {
            Trace.TraceError("QudJP: {0} template format failed: {1}", Context, ex);
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".CodeCompressor.RequiredModMissing",
            source,
            translated);
        return true;
    }
}
