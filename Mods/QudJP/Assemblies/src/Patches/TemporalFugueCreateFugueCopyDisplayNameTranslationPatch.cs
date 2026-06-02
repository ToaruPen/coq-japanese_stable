using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TemporalFugueCreateFugueCopyDisplayNameTranslationPatch
{
    private const string Context = nameof(TemporalFugueCreateFugueCopyDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.TemporalFugue");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var partType = AccessTools.TypeByName("XRL.World.IPart");
        var method = targetType is null || gameObjectType is null || cellType is null || partType is null
            ? null
            : AccessTools.Method(
                targetType,
                "CreateFugueCopyOf",
                [
                    gameObjectType,
                    gameObjectType,
                    cellType,
                    gameObjectType,
                    typeof(bool),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    partType,
                ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateTemporalFugueCopy(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
