using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageDynamicQuestRewardGameObjectTranslationPatch
{
    private const string Context = nameof(VillageDynamicQuestRewardGameObjectTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.DynamicQuestRewardElement_GameObject");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var constructor = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Constructor(targetType, [gameObjectType]);
        if (constructor is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return constructor;
    }

    public static void Prefix(object? __0)
    {
        try
        {
            if (VillageDynamicQuestRewardDisplayNameTranslationPatch.IsActive)
            {
                _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateRewardGameObject(__0);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
