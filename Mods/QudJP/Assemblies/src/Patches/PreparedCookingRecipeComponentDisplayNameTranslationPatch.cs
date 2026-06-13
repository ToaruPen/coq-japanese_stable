using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PreparedCookingRecipeComponentDisplayNameTranslationPatch
{
    internal const string Context = nameof(PreparedCookingRecipeComponentDisplayNameTranslationPatch);
    internal const string Family = Context + ".ComponentDisplayName";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var typeName in new[]
                 {
                     "XRL.World.Skills.Cooking.PreparedCookingRecipieComponentBlueprint",
                     "XRL.World.Skills.Cooking.PreparedCookingRecipieComponentDomain",
                     "XRL.World.Skills.Cooking.PreparedCookingRecipieComponentLiquid",
                 })
        {
            var targetType = AccessTools.TypeByName(typeName);
            if (targetType is null)
            {
                Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
                continue;
            }

            var method = AccessTools.Method(targetType, "getDisplayName", Type.EmptyTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}.getDisplayName() target not found.", Context, typeName);
            }
        }
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!CookingIngredientFragmentTranslator.TryTranslate(source, out var translated)
                || string.Equals(source, translated, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
