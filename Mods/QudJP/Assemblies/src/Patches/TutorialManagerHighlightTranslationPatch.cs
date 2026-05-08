using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TutorialManagerHighlightTranslationPatch
{
    private const string Context = nameof(TutorialManagerHighlightTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("TutorialManager", "TutorialManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: TutorialManagerHighlightTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "HighlightByCID",
            new[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(float),
                typeof(string),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerHighlightTranslationPatch.HighlightByCID(...) not found.");
        }

        return method;
    }

    public static void Prefix(ref string text)
    {
        try
        {
            if (TutorialManagerTranslationHelpers.IsControlSentinel(text))
            {
                return;
            }

            text = TutorialManagerTranslationHelpers.Translate(text, Context, "arg=text", "TutorialManager.HighlightText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TutorialManagerHighlightTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
