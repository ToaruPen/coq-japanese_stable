using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FriendOrFoeReasonTranslationPatch
{
    private const string Context = nameof(FriendOrFoeReasonTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        AddTarget(targets, "XRL.World.Parts.GenerateFriendOrFoe");
        AddTarget(targets, "XRL.World.Parts.GenerateFriendOrFoe_HEB");
        return targets;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!FriendOrFoeReasonTranslator.TryTranslate(source, out var translated))
            {
                return;
            }

            __result = translated;
            DynamicTextObservability.RecordTransform(
                Context,
                Context + ".replacePlaceholders",
                source,
                translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(List<MethodBase> targets, string typeName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, "replacePlaceholders", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.replacePlaceholders(string) target not found.", Context, typeName);
            return;
        }

        targets.Add(method);
    }
}
