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

    public static void Postfix(object? __0, ref string __result)
    {
        try
        {
            var source = __result;
            if (!RelicGeneratedNameTranslator.TryTranslate(source, out var translated, includeBroadItemTypes: true))
            {
                return;
            }

            __result = translated;
            ClearArticle(__0, "IndefiniteArticle");
            ClearArticle(__0, "DefiniteArticle");
            ClearCachedDisplayNameForSort(__0);
            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void ClearArticle(object? obj, string articleProperty)
    {
        if (obj is null)
        {
            return;
        }

        var method = AccessTools.Method(obj.GetType(), "SetStringProperty", [typeof(string), typeof(string), typeof(bool)]);
        if (method is not null)
        {
            method.Invoke(obj, [articleProperty, string.Empty, false]);
        }
    }

    private static void ClearCachedDisplayNameForSort(object? obj)
    {
        if (obj is null)
        {
            return;
        }

        AccessTools.Field(obj.GetType(), "_CachedDisplayNameForSort")?.SetValue(obj, null);
    }
}
