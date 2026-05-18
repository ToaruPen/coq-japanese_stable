using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageDynamicQuestItemNameMutationTranslationPatch
{
    private const string Context = nameof(VillageDynamicQuestItemNameMutationTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.World.VillageDynamicQuestContext",
            "VillageDynamicQuestContext");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "getQuestItemNameMutation", [typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.getQuestItemNameMutation(string) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!DynamicQuestItemNameMutationTranslator.TryTranslate(source, out var translated))
            {
                __result = translated;
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Context + ".getQuestItemNameMutation", source, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
