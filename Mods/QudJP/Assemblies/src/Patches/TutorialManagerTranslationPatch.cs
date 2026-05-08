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

        var route = ObservabilityHelpers.ComposeContext(context, routeSuffix);
        if (TryTranslateRawDictionaryKey(source, route, family, out var rawTranslated))
        {
            return rawTranslated;
        }

        if (TryTranslateExpandedHotkeyText(source, route, family, out var hotkeyTranslated))
        {
            return hotkeyTranslated;
        }

        return UiBindingTranslationHelpers.TranslateVisibleText(
            source,
            route,
            family);
    }

    private static bool TryTranslateRawDictionaryKey(string source, string route, string family, out string translated)
    {
        translated = source;
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var candidate)
            || string.Equals(candidate, source, StringComparison.Ordinal))
        {
            return false;
        }

        translated = candidate;
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateExpandedHotkeyText(string source, string route, string family, out string translated)
    {
        translated = source;
        if (source.IndexOf("{{hotkey|", StringComparison.Ordinal) < 0)
        {
            return false;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(stripped, out var candidate)
            || string.Equals(candidate, stripped, StringComparison.Ordinal))
        {
            return false;
        }

        translated = candidate;
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
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
