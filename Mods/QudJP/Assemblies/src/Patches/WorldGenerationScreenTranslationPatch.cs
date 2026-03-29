using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WorldGenerationScreenTranslationPatch
{
    private const string Context = nameof(WorldGenerationScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.WorldGenerationScreen", "WorldGenerationScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: WorldGenerationScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "_AddMessage", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: WorldGenerationScreenTranslationPatch._AddMessage(string) not found.");
        }

        return method;
    }

    public static void Prefix(ref string message)
    {
        try
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var stripped))
            {
                message = stripped;
                return;
            }

            var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
                message,
                static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
            if (!string.Equals(translated, message, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "WorldGenerationScreen.AddMessage", message, translated);
                message = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: WorldGenerationScreenTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
