using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterStatusScreenHighlightEffectPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CharacterStatusScreen", "CharacterStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenHighlightEffectPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "HandleHighlightEffect", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenHighlightEffectPatch.HandleHighlightEffect not found.");
        }

        return method;
    }

    public static void Postfix(object? ___mutationsDetails)
    {
        try
        {
            var currentText = UITextSkinReflectionAccessor.GetCurrentText(___mutationsDetails, nameof(CharacterStatusScreenHighlightEffectPatch));
            if (string.IsNullOrEmpty(currentText))
            {
                return;
            }

            if (!ActiveEffectTextTranslator.TryTranslateText(
                    currentText!,
                    nameof(CharacterStatusScreenHighlightEffectPatch),
                    "ActiveEffects.StatusDetail",
                    out var translated))
            {
                return;
            }

            _ = UITextSkinReflectionAccessor.SetCurrentText(___mutationsDetails, translated, nameof(CharacterStatusScreenHighlightEffectPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenHighlightEffectPatch.Postfix failed: {0}", ex);
        }
    }
}
