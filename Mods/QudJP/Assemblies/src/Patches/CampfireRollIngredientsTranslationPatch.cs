using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfireRollIngredientsTranslationPatch
{
    private const string Context = nameof(CampfireRollIngredientsTranslationPatch);
    private const string Family = Context + ".IngredientFragment";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type or game object type not found.", Context);
            return null;
        }

        var readOnlyListType = typeof(IReadOnlyList<>).MakeGenericType(gameObjectType);
        var method = AccessTools.Method(
            targetType,
            "RollIngredients",
            [typeof(int), readOnlyListType, typeof(System.Random)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.RollIngredients target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string[] __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            for (var index = 0; index < __result.Length; index++)
            {
                var source = __result[index];
                if (!CookingIngredientFragmentTranslator.TryTranslate(source, out var translated))
                {
                    continue;
                }

                __result[index] = translated;
                DynamicTextObservability.RecordTransform(Context, Family, source, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
