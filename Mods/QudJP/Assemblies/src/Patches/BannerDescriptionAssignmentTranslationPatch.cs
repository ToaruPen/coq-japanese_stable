using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BannerDescriptionAssignmentTranslationPatch
{
    private const string Context = nameof(BannerDescriptionAssignmentTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var bannerType = AccessTools.TypeByName("XRL.World.Parts.Banner");
        var shortDescriptionEventType = AccessTools.TypeByName("XRL.World.GetShortDescriptionEvent");
        if (bannerType is null || shortDescriptionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(bannerType, "HandleEvent", new[] { shortDescriptionEventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(GetShortDescriptionEvent) not found.", Context);
        }

        return method;
    }

    public static void Postfix(object __instance, object E)
    {
        try
        {
            DescriptionAssignmentOwnerTranslationPatch.TranslateBannerDescription(__instance, E);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
