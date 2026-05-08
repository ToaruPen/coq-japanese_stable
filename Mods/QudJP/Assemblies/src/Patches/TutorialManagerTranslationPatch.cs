using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class TutorialManagerTranslationHelpers
{
    public static string Translate(string source, string context, string routeSuffix, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return UiBindingTranslationHelpers.TranslateVisibleText(
            source,
            ObservabilityHelpers.ComposeContext(context, routeSuffix),
            family);
    }

    public static bool IsControlSentinel(string? source)
    {
        return source is not null
            && (source.Contains("<noframe>")
                || source.Contains("<no message>")
                || source.Contains("<nohighlight>"));
    }
}

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
            text = TutorialManagerTranslationHelpers.Translate(text, Context, "arg=text", "TutorialManager.PopupText");
            buttonText = TutorialManagerTranslationHelpers.Translate(buttonText, Context, "arg=buttonText", "TutorialManager.ButtonText");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TutorialManagerTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}

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

        method ??= AccessTools.Method(targetType, "ShowCellPopup");
        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerCellPopupTranslationPatch.ShowCellPopup(...) not found.");
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

        method ??= AccessTools.Method(targetType, "Highlight");
        if (method is null)
        {
            Trace.TraceError("QudJP: TutorialManagerDirectHighlightTranslationPatch.Highlight(...) not found.");
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
