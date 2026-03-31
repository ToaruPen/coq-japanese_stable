using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QuestsStatusScreenTabTranslationPatch
{
    private const string Context = nameof(QuestsStatusScreenTabTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.QuestsStatusScreen", "QuestsStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: QuestsStatusScreenTabTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GetTabString", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: QuestsStatusScreenTabTranslationPatch.GetTabString() not found.");
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
                "QuestsStatusScreen.TabString",
                __result,
                translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QuestsStatusScreenTabTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
