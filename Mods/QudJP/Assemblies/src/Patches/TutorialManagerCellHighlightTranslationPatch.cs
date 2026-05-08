using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TutorialManagerCellHighlightTranslationPatch
{
    private const string Context = nameof(TutorialManagerCellHighlightTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("TutorialManager", "TutorialManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: TutorialManagerCellHighlightTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "HighlightCell",
            new[]
            {
                typeof(int),
                typeof(int),
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(float),
                typeof(float),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerCellHighlightTranslationPatch.HighlightCell(...) not found.");
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

            text = TutorialManagerTranslationHelpers.Translate(text, Context, "arg=text", "TutorialManager.CellHighlightText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TutorialManagerCellHighlightTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
