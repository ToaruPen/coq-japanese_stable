using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RelicGeneratorGeneratedNameTranslationPatch
{
    internal const string Context = nameof(RelicGeneratorGeneratedNameTranslationPatch);
    internal const string Family = Context + ".GenerateRelicName";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.RelicGenerator");
        var snapshotType = AccessTools.TypeByName("HistoryKit.HistoricEntitySnapshot");
        if (targetType is null || snapshotType is null)
        {
            Trace.TraceError("QudJP: {0} target type or snapshot type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "GenerateRelicName",
            [typeof(string), snapshotType, typeof(string), typeof(string).MakeByRefType()]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateRelicName target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? SnapRegion, ref string Article, ref string __result)
    {
        try
        {
            var source = __result;
            if (!RelicGeneratedNameTranslator.TryTranslate(source, out var translated))
            {
                return;
            }

            Article = string.Empty;
            __result = translated;
            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
