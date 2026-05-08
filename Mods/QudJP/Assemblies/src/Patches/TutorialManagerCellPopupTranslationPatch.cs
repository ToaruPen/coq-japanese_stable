using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TutorialManagerCellPopupTranslationPatch
{
    private const string Context = nameof(TutorialManagerCellPopupTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("TutorialManager", "TutorialManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: TutorialManagerCellPopupTranslationPatch target type not found.");
            return null;
        }

        var location2DType = AccessTools.TypeByName("Genkit.Location2D");
        MethodBase? method = null;
        if (location2DType is not null)
        {
            method = AccessTools.Method(
                targetType,
                "ShowCellPopup",
                new[]
                {
                    location2DType,
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(Action),
                });
        }

        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerCellPopupTranslationPatch.ShowCellPopup(...) exact signature not found.");
        }

        return method;
    }

    public static void Prefix(ref string text)
    {
        try
        {
            text = TutorialManagerTranslationHelpers.Translate(text, Context, "arg=text", "TutorialManager.CellPopupText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TutorialManagerCellPopupTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
