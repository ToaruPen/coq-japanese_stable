using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LoadingStatusTranslationPatch
{
    private const string Context = nameof(LoadingStatusTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("XRL.UI.Loading");
        if (type is null)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch target type 'XRL.UI.Loading' not found.");
            return null;
        }

        var method = AccessTools.Method(type, "SetLoadingStatus", new[] { typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch method 'SetLoadingStatus' not found on 'XRL.UI.Loading'.");
        }

        return method;
    }

    public static void Prefix(ref string description)
    {
        try
        {
            description = TranslateStatusText(description, Context, "Loading.Exact");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string TranslateStatusText(string? source, string route, string family)
    {
        var text = source ?? string.Empty;
        if (text.Length == 0)
        {
            return text;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(text, out var stripped))
        {
            return stripped;
        }

        var (visible, _) = ColorAwareTranslationComposer.Strip(text);
        if (visible.Length == 0 || UITextSkinTranslationPatch.IsProbablyAlreadyLocalizedText(visible))
        {
            return text;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            text,
            static visibleText => StringHelpers.TranslateExactOrLowerAsciiFallback(visibleText));
        if (string.Equals(translated, text, StringComparison.Ordinal))
        {
            return text;
        }

        DynamicTextObservability.RecordTransform(route, family, text, translated);
        return translated;
    }
}
