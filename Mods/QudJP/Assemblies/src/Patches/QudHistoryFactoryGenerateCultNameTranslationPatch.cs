using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudHistoryFactoryGenerateCultNameTranslationPatch
{
    internal const string Context = nameof(QudHistoryFactoryGenerateCultNameTranslationPatch);
    internal const string Family = Context + ".GenerateCultName";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.Annals.QudHistoryFactory");
        var entityType = AccessTools.TypeByName("HistoryKit.HistoricEntity");
        var historyType = AccessTools.TypeByName("HistoryKit.History");
        if (targetType is null || entityType is null || historyType is null)
        {
            Trace.TraceError("QudJP: {0} target type, HistoricEntity type, or History type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateCultName", new[] { entityType, historyType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateCultName(...) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object sultan)
    {
        try
        {
            TranslateCultNameProperty(sultan);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateCultNameProperty(object? sultan)
    {
        if (sultan is null)
        {
            return;
        }

        var snapshot = AccessTools.Method(sultan.GetType(), "GetCurrentSnapshot")?.Invoke(sultan, Array.Empty<object>());
        if (snapshot is null)
        {
            return;
        }

        var source = AccessTools.Method(snapshot.GetType(), "GetProperty", new[] { typeof(string) })
            ?.Invoke(snapshot, new object[] { "cultName" }) as string;
        if (source is null
            || source.Length == 0
            || !HistoricSpiceGeneratedNameTranslator.TryTranslateSultanCultName(source, out var translated))
        {
            return;
        }

        var setter = AccessTools.Method(sultan.GetType(), "SetEntityPropertyAtCurrentYear", new[] { typeof(string), typeof(string) });
        if (setter is null)
        {
            Trace.TraceError("QudJP: {0} could not find SetEntityPropertyAtCurrentYear on {1}.", Context, sultan.GetType().FullName);
            return;
        }

        _ = setter.Invoke(sultan, new object[] { "cultName", translated });
        DynamicTextObservability.RecordTransform(Context, Family, source, translated);
    }
}
