using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MessageLogStatusScreenTranslationPatch
{
    private const string Context = nameof(MessageLogStatusScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.MessageLogStatusScreen", "MessageLogStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: MessageLogStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GetTabString", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: MessageLogStatusScreenTranslationPatch.GetTabString() not found.");
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            var translated = StringHelpers.TranslateExactOrLowerAscii(__result);
            if (translated is null || string.Equals(translated, __result, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(
                ObservabilityHelpers.ComposeContext(Context, "return"),
                "MessageLogStatusScreen.TabString",
                __result,
                translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageLogStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
