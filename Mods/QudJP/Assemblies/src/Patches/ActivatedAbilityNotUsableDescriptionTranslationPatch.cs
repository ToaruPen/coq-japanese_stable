using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActivatedAbilityNotUsableDescriptionTranslationPatch
{
    internal const string Family = "ActivatedAbilityEntry.NotUsableDescription";

    private const string Context = nameof(ActivatedAbilityNotUsableDescriptionTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var activatedAbilityEntryType = AccessTools.TypeByName("XRL.World.Parts.ActivatedAbilityEntry");
        if (activatedAbilityEntryType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var getter = AccessTools.PropertyGetter(activatedAbilityEntryType, "NotUsableDescription");
        if (getter is null)
        {
            Trace.TraceError("QudJP: {0}.NotUsableDescription getter not found.", Context);
        }

        return getter;
    }

    public static void Postfix(ref string? __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result)
                || MessageFrameTranslator.TryStripDirectTranslationMarker(__result, out _))
            {
                return;
            }

            var source = __result!;
            if (!ActivatedAbilityCooldownTranslator.TryTranslateRawCooldown(
                    source,
                    Context,
                    Family,
                    out var translated))
            {
                return;
            }

            __result = MessageFrameTranslator.MarkDirectTranslation(translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
