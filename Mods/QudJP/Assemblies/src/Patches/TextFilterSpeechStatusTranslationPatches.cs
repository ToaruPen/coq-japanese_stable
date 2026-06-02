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
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(originalText, out var markedOriginalText))
        {
            return markedOriginalText;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        if (string.IsNullOrEmpty(originalText))
        {
            return source;
        }

        var lookupText = ColorAwareTranslationComposer.HasColorMarkup(originalText)
            ? ColorAwareTranslationComposer.GetVisibleText(originalText)
            : originalText;
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(lookupText, out var translatedText)
            || string.Equals(translatedText, lookupText, StringComparison.Ordinal))
        {
            return source;
        }

        var replacement = ColorAwareTranslationComposer.HasColorMarkup(originalText)
            ? ColorAwareTranslationComposer.TranslatePreservingColors(originalText, _ => translatedText)
            : translatedText;
        return source.Replace(originalText, replacement);
    }
}

internal static class TextFilterSpeechStatusPatchHelpers
{
    public static MethodBase? GetTextFiltersMethod(string context, string methodName, Type[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName("XRL.Language.TextFilters");
        var method = targetType is null ? null : AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: TextFilters.{1}.", context, methodName);
        }

        return method;
    }

    public static void RecordIfChanged(string context, string family, string source, string translated, ref string result)
    {
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(context, family, source, translated);
        result = translated;
    }
}
