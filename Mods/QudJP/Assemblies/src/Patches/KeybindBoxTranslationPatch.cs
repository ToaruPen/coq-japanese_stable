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

            var translated = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                source!,
                DictionaryContext,
                DictionaryFile);
            if (string.IsNullOrEmpty(translated) || string.Equals(source, translated, StringComparison.Ordinal))
            {
                return;
            }

            _ = UITextSkinReflectionAccessor.SetCurrentText(___textSkin, translated!, Context);
            DynamicTextObservability.RecordTransform(Context, Family, source!, translated!);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
