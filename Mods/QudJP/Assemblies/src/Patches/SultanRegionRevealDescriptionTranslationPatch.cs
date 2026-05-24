using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SultanRegionRevealDescriptionTranslationPatch
{
    private const string Context = nameof(SultanRegionRevealDescriptionTranslationPatch);
    private const string SultanRevealEventId = "SultanReveal";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.World.Parts.SultanRegion",
            "SultanRegion");
        var eventType = GameTypeResolver.FindType(
            "XRL.World.Event",
            "Event");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance, object? E, bool __result)
    {
        try
        {
            if (!__result || __instance is null || !IsSultanRevealEvent(E))
            {
                return;
            }

            _ = TryTranslateRevealedDescription(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static bool TryTranslateRevealedDescription(object? instance)
    {
        if (instance is null
            || DescriptionPartReflectionHelpers.GetMemberValue(instance, "ParentObject") is not { } parentObject
            || !DescriptionPartReflectionHelpers.TryGetDescriptionPart(parentObject, Context, logFallback: false, out var descriptionPart))
        {
            return false;
        }

        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "Short");
        if (source is null)
        {
            source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "_Short");
        }

        if (!SultanRegionRevealDescriptionTranslator.TryTranslate(source, out var translated))
        {
            return false;
        }

        if (!DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "Short", translated)
            && !DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "_Short", translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, Context + ".DescriptionShort", source!, translated);
        return true;
    }

    private static bool IsSultanRevealEvent(object? eventObject)
    {
        return string.Equals(
            DescriptionPartReflectionHelpers.GetStringMemberValue(eventObject, "ID"),
            SultanRevealEventId,
            StringComparison.Ordinal);
    }
}
