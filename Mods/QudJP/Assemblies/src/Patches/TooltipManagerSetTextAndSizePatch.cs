using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TooltipManagerSetTextAndSizePatch
{
    private const string TargetTypeName = "ModelShark.TooltipManager";
    private const string TriggerTypeName = "ModelShark.TooltipTrigger";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var triggerType = AccessTools.TypeByName(TriggerTypeName);
        if (targetType is null || triggerType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve ModelShark TooltipManager types. Patch will not apply.");
            return null;
        }

        var method = AccessTools.Method(targetType, "SetTextAndSize", new[] { triggerType });
        if (method is not null)
        {
            return method;
        }

        Trace.TraceError("QudJP: Failed to resolve TooltipManager.SetTextAndSize(TooltipTrigger). Patch will not apply.");
        return null;
    }

    public static void Postfix(object trigger)
    {
        try
        {
#if HAS_TMP
            _ = QudJP.TooltipTextRepairer.TryRepairTooltip(trigger);
            QudJP.DelayedTooltipRepairScheduler.ScheduleRepair(trigger);
#else
            _ = trigger;
#endif
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TooltipManagerSetTextAndSizePatch.Postfix failed: {0}", ex);
        }
    }
}
