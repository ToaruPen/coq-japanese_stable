using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CookingRecipeGenerateRecipeTileTranslationScopePatch
{
    private const string Context = nameof(CookingRecipeGenerateRecipeTileTranslationScopePatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Skills.Cooking.CookingRecipe");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateRecipeTile", [targetType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateRecipeTile(CookingRecipe) target not found.", Context);
        }

        return method;
    }

    public static void Prefix(ref int __state)
    {
        try
        {
            CookingRecipeDisplayNameTranslationPatch.EnterGenerateRecipeTileScope(out __state);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static void Finalizer(int __state)
    {
        try
        {
            CookingRecipeDisplayNameTranslationPatch.ExitGenerateRecipeTileScope(__state);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }
    }
}
