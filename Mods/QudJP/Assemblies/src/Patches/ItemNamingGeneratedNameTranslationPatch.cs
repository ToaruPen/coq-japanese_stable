using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ItemNamingGeneratedNameTranslationPatch
{
    internal const string Context = nameof(ItemNamingGeneratedNameTranslationPatch);
    internal const string Family = Context + ".GenerateRelicStyleName";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Capabilities.ItemNaming");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type or game object type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "GenerateRelicStyleName",
            [
                gameObjectType,
                gameObjectType,
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string).MakeByRefType(),
                typeof(string).MakeByRefType(),
            ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GenerateRelicStyleName target not found.", Context);
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!RelicGeneratedNameTranslator.TryTranslate(source, out var translated, includeBroadItemTypes: true))
            {
                return;
            }

            __result = translated;
            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
