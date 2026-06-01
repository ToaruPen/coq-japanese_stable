using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class KeybindBoxTranslationPatch
{
    internal const string Context = nameof(KeybindBoxTranslationPatch);
    internal const string Family = Context + ".Text";

    private const string DictionaryFile = "ui-keybinds.ja.json";
    private const string DictionaryContext = "Qud.UI.KeybindBox";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.KeybindBox", "KeybindBox");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Update", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Update() target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? ___textSkin)
    {
        try
        {
            var source = UITextSkinReflectionAccessor.GetCurrentText(___textSkin, Context);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
            {
                _ = UITextSkinReflectionAccessor.SetCurrentText(___textSkin, markedText, Context);
                return;
            }

            var sourceText = source!;
            var translated = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                sourceText,
                DictionaryContext,
                DictionaryFile);
            if (!string.IsNullOrEmpty(translated) && !string.Equals(sourceText, translated, StringComparison.Ordinal))
            {
                var exactTranslated = translated!;
                _ = UITextSkinReflectionAccessor.SetCurrentText(___textSkin, exactTranslated, Context);
                DynamicTextObservability.RecordTransform(Context, Family, sourceText, exactTranslated);
                return;
            }

            var hasColorMarkup = ColorAwareTranslationComposer.HasColorMarkup(sourceText);
            var visible = hasColorMarkup
                ? ColorAwareTranslationComposer.GetVisibleText(sourceText)
                : sourceText;
            translated = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                visible,
                DictionaryContext,
                DictionaryFile);
            if (string.IsNullOrEmpty(translated) || string.Equals(visible, translated, StringComparison.Ordinal))
            {
                return;
            }

            var visibleTranslated = translated!;
            var finalText = hasColorMarkup
                ? ColorAwareTranslationComposer.TranslatePreservingColors(sourceText, _ => visibleTranslated)
                : visibleTranslated;
            _ = UITextSkinReflectionAccessor.SetCurrentText(___textSkin, finalText, Context);
            DynamicTextObservability.RecordTransform(Context, Family, sourceText, finalText);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
