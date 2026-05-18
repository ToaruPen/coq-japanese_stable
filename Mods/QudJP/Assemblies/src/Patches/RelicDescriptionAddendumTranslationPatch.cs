using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RelicDescriptionAddendumTranslationPatch
{
    internal const string Context = nameof(RelicDescriptionAddendumTranslationPatch);
    internal const string Family = Context + ".DescriptionShort";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.RelicGenerator");
        var snapshotType = AccessTools.TypeByName("HistoryKit.HistoricEntitySnapshot");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || snapshotType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var listStringType = typeof(List<>).MakeGenericType(typeof(string));
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), listStringType);
        var method = AccessTools.Method(
            targetType,
            "GenerateRelic",
            [
                typeof(string),
                typeof(int),
                snapshotType,
                listStringType,
                dictionaryType,
                typeof(string),
                typeof(string),
                typeof(string),
            ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateRelic target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is null
                || !DescriptionPartReflectionHelpers.TryGetDescriptionPart(__result, Context, logFallback: true, out var descriptionPart))
            {
                return;
            }

            var source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "Short");
            if (source is null)
            {
                Trace.TraceWarning("QudJP: {0} falling back from Description.Short to _Short.", Context);
                source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "_Short");
            }

            if (!RelicDescriptionAddendumTranslator.TryTranslate(source, out var translated))
            {
                return;
            }

            if (DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "Short", translated)
                || DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "_Short", translated))
            {
                DynamicTextObservability.RecordTransform(Context, Family, source!, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

}
