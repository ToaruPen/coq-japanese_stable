using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TutorialManagerTranslationPatch
{
    private const string Context = nameof(TutorialManagerTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("TutorialManager", "TutorialManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: TutorialManagerTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "ShowCIDPopupAsync",
            new[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(float),
                typeof(Action),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerTranslationPatch.ShowCIDPopupAsync(...) not found.");
        }

        return method;
    }

    public static void Prefix(ref string text, ref string buttonText)
    {
        try
        {
            text = Translate(text, "arg=text", "TutorialManager.PopupText");
            buttonText = Translate(buttonText, "arg=buttonText", "TutorialManager.ButtonText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TutorialManagerTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static string Translate(string source, string routeSuffix, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return UiBindingTranslationHelpers.TranslateVisibleText(
            source,
            ObservabilityHelpers.ComposeContext(Context, routeSuffix),
            family);
    }
}
