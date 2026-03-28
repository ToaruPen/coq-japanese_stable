using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupShowSpaceTranslationPatch
{
    private const string Context = nameof(PopupShowSpaceTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var popupType = AccessTools.TypeByName("XRL.UI.Popup");
        if (popupType is null)
        {
            Trace.TraceError("QudJP: PopupShowSpaceTranslationPatch target type not found.");
            return null;
        }

        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.Renderable");
        var method = renderableType is null
            ? AccessTools.Method(popupType, "ShowSpace")
            : AccessTools.Method(
                popupType,
                "ShowSpace",
                new[] { typeof(string), typeof(string), typeof(string), renderableType, typeof(bool), typeof(bool), typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: PopupShowSpaceTranslationPatch.ShowSpace not found.");
        }

        return method;
    }

    public static void Prefix(ref string Message, ref string Title)
    {
        try
        {
            Message = TranslateMessage(Message);
            if (!string.IsNullOrEmpty(Title))
            {
                Title = PopupTranslationPatch.TranslatePopupTextForProducerRoute(Title, Context);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupShowSpaceTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string TranslateMessage(string? source)
    {
        var text = source ?? string.Empty;
        if (text.Length == 0)
        {
            return text;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(text, out var markedText))
        {
            return markedText;
        }

        if (DeathWrapperFamilyTranslator.TryTranslatePopup(text, out var deathTranslated))
        {
            DynamicTextObservability.RecordTransform(Context, "Popup.ShowSpace.DeathWrapper", text, deathTranslated);
            return deathTranslated;
        }

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(text, Context);
        if (!string.Equals(translated, text, StringComparison.Ordinal))
        {
            return translated;
        }

        var leadingLineBreakLength = GetLeadingLineBreakLength(text);
        var core = leadingLineBreakLength == 0 ? text : text.Substring(leadingLineBreakLength);
        if (core.Length > 0 && MessagePatternTranslator.TryTranslateWithoutLogging(core, out var translatedCore))
        {
            translated = leadingLineBreakLength == 0
                ? translatedCore
                : PrependLeadingLineBreaks(text, leadingLineBreakLength, translatedCore);
            DynamicTextObservability.RecordTransform(Context, "Popup.ShowSpace.Pattern", text, translated);
            return translated;
        }

        return text;
    }

    private static int GetLeadingLineBreakLength(string source)
    {
        var index = 0;
        while (index < source.Length && (source[index] == '\r' || source[index] == '\n'))
        {
            index++;
        }

        return index;
    }

    private static string PrependLeadingLineBreaks(string source, int leadingLineBreakLength, string translatedCore)
    {
        var builder = new StringBuilder(leadingLineBreakLength + translatedCore.Length);
        builder.Append(source, 0, leadingLineBreakLength);
        builder.Append(translatedCore);
        return builder.ToString();
    }
}
