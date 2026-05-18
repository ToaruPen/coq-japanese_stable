using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP.UI;
#if HAS_GAME_DLL
using XRL.UI;
#endif

namespace QudJP.Patches;

[HarmonyPatch]
public static class LookTooltipInformationWrapPatch
{
    private const string TargetTypeName = "XRL.UI.Look";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            var targetType = AccessTools.TypeByName(TargetTypeName);
            if (targetType is null)
            {
                Trace.TraceError("QudJP: Failed to resolve XRL.UI.Look. Tooltip information wrap patch will not apply.");
                return null;
            }

            var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
            if (gameObjectType is null)
            {
                Trace.TraceError("QudJP: Failed to resolve XRL.World.GameObject. Tooltip information wrap patch will not apply.");
                return null;
            }

            var method = AccessTools.Method(targetType, "GenerateTooltipInformation", new[] { gameObjectType });
            if (method is not null)
            {
                return method;
            }

            var methods = AccessTools.GetDeclaredMethods(targetType);
            for (var index = 0; index < methods.Count; index++)
            {
                var candidate = methods[index];
                var parameters = candidate.GetParameters();
                if (string.Equals(candidate.Name, "GenerateTooltipInformation", StringComparison.Ordinal)
                    && parameters.Length == 1
                    && parameters[0].ParameterType == gameObjectType)
                {
                    return candidate;
                }
            }

            Trace.TraceError("QudJP: Failed to resolve Look.GenerateTooltipInformation(GameObject). Tooltip information wrap patch will not apply.");
            return null;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LookTooltipInformationWrapPatch.TargetMethod failed: {0}", ex);
            return null;
        }
    }

#if HAS_GAME_DLL
    public static void Postfix(ref Look.TooltipInformation __result)
    {
        try
        {
            __result.DisplayName = TranslateTooltipDisplayName(__result.DisplayName);
            __result.LongDescription = TranslateTooltipLongDescription(__result.LongDescription);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LookTooltipInformationWrapPatch.Postfix failed: {0}", ex);
        }
    }
#else
    public static void Postfix(ref object? __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            var displayNameField = __result.GetType().GetField(
                "DisplayName",
                BindingFlags.Instance | BindingFlags.Public);
            if (displayNameField?.GetValue(__result) is string displayName)
            {
                displayNameField.SetValue(__result, TranslateTooltipDisplayName(displayName));
            }

            var longDescriptionField = __result.GetType().GetField(
                "LongDescription",
                BindingFlags.Instance | BindingFlags.Public);
            if (longDescriptionField?.GetValue(__result) is string source)
            {
                longDescriptionField.SetValue(__result, TranslateTooltipLongDescription(source));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LookTooltipInformationWrapPatch.Postfix failed: {0}", ex);
        }
    }
#endif

    internal static string TranslateTooltipDisplayName(string source)
    {
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(source, nameof(LookTooltipInformationWrapPatch));
    }

    internal static string TranslateTooltipLongDescription(string source)
    {
        var normalizedSource = MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText)
            ? markedText
            : source;
        if (!ContainsAsciiLetter(normalizedSource)
            && JapaneseBlockWrap.TryWrapTooltipLongDescription(normalizedSource, out var sourceWrapped))
        {
            return sourceWrapped;
        }

        var translated = LookTooltipContentPatch.TranslateTooltipContent(source);
        return JapaneseBlockWrap.TryWrapTooltipLongDescription(translated, out var wrapped)
            ? wrapped
            : translated;
    }

    private static bool ContainsAsciiLetter(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z'))
            {
                return true;
            }
        }

        return false;
    }
}
