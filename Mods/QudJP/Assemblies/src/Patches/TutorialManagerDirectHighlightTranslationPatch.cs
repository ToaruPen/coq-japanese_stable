using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TutorialManagerDirectHighlightTranslationPatch
{
    private const string Context = nameof(TutorialManagerDirectHighlightTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("TutorialManager", "TutorialManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: TutorialManagerDirectHighlightTranslationPatch target type not found.");
            return null;
        }

        var rectTransformType = AccessTools.TypeByName("UnityEngine.RectTransform");
        MethodBase? method = null;
        if (rectTransformType is not null)
        {
            method = AccessTools.Method(
                targetType,
                "Highlight",
                new[]
                {
                    rectTransformType,
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(string),
                });
        }

        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerDirectHighlightTranslationPatch.Highlight(...) exact signature not found.");
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

            text = TutorialManagerTranslationHelpers.Translate(text, Context, "arg=text", "TutorialManager.DirectHighlightText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TutorialManagerDirectHighlightTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
