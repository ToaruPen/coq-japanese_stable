using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class TextFilterSpeechStatusTranslator
{
    public static string TranslateAngry(string source)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        return source
            .Replace("NO!", "いや！")
            .Replace("ARGH!", "ぐああ！");
    }

    public static string TranslateLallated(string source, string originalText)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        if (string.IsNullOrEmpty(originalText)
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(originalText, out var translatedText)
            || string.Equals(translatedText, originalText, StringComparison.Ordinal))
        {
            return source;
        }

        return source.Replace(originalText, translatedText);
    }
}

[HarmonyPatch]
public static class TextFiltersAngryTranslationPatch
{
    internal const string Context = nameof(TextFiltersAngryTranslationPatch);
    private const string Family = "TextFilters.Angry";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.Language.TextFilters");
        var method = targetType is null ? null : AccessTools.Method(targetType, "Angry", [typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: TextFilters.Angry(string).", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            var translated = TextFilterSpeechStatusTranslator.TranslateAngry(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, Family, source, translated);
                __result = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

[HarmonyPatch]
public static class TextFiltersLallatedTranslationPatch
{
    internal const string Context = nameof(TextFiltersLallatedTranslationPatch);
    private const string Family = "TextFilters.Lallated";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.Language.TextFilters");
        var method = targetType is null ? null : AccessTools.Method(targetType, "Lallated", [typeof(string), typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: TextFilters.Lallated(string, string).", Context);
        }

        return method;
    }

    public static void Postfix(string Text, ref string __result)
    {
        try
        {
            var source = __result;
            var translated = TextFilterSpeechStatusTranslator.TranslateLallated(source, Text);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, Family, source, translated);
                __result = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
