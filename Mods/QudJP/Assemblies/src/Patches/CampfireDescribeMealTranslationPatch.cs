using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfireDescribeMealTranslationPatch
{
    private const string Context = nameof(CampfireDescribeMealTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type or parameter type not found.", Context);
            return null;
        }

        var readOnlyListType = typeof(IReadOnlyList<>).MakeGenericType(gameObjectType);
        var method = AccessTools.Method(targetType, "DescribeMeal", [readOnlyListType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.DescribeMeal(IReadOnlyList<GameObject>) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (CookingMealDescriptionTranslator.TryTranslate(__result, out var translated))
            {
                DynamicTextObservability.RecordTransform(Context, Context + ".CookTemplate", __result, translated);
                __result = translated;
                return;
            }

            if (!string.Equals(__result, translated, StringComparison.Ordinal))
            {
                __result = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
