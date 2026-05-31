using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ObjectFinderDisplayNameTranslationPatch
{
    internal const string Context = nameof(ObjectFinderDisplayNameTranslationPatch);
    internal const string Family = Context + ".GetDisplayName";

    private static readonly IReadOnlyDictionary<string, string> FixedTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Nearby Items"] = "近くのアイテム",
            ["Id"] = "ID",
            ["Value"] = "価値",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var typeName in new[]
                 {
                     "XRL.UI.ObjectFinderContexts.AutogotItems",
                     "XRL.UI.ObjectFinderContexts.NearbyItems",
                     "XRL.UI.ObjectFinderSorters.IdSorter",
                     "XRL.UI.ObjectFinderSorters.ValueSorter",
                 })
        {
            var targetType = AccessTools.TypeByName(typeName);
            if (targetType is null)
            {
                Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
                continue;
            }

            var method = AccessTools.Method(targetType, "GetDisplayName", Type.EmptyTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}.GetDisplayName() target not found.", Context, typeName);
            }
        }
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result) || !FixedTranslations.TryGetValue(__result, out var translated))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family, __result, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
