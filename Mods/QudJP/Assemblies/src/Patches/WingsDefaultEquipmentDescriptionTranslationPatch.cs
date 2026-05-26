using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WingsDefaultEquipmentDescriptionTranslationPatch
{
    private const string Context = nameof(WingsDefaultEquipmentDescriptionTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var wingsType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Wings");
        var bodyType = AccessTools.TypeByName("XRL.World.Parts.Body");
        if (wingsType is null || bodyType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(wingsType, "OnRegenerateDefaultEquipment", new[] { bodyType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.OnRegenerateDefaultEquipment(Body) not found.", Context);
        }

        return method;
    }

    public static void Postfix(object __instance, object body)
    {
        try
        {
            var part = ResolveWingsBodyPart(__instance, body);
            DescriptionAssignmentOwnerTranslationPatch.TranslateWingsPartForTests(part);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static object? ResolveWingsBodyPart(object instance, object body)
    {
        var slot = ResolveWornSlot(instance);
        var getFirstPart = AccessTools.Method(body.GetType(), "GetFirstPart", new[] { typeof(string) });
        if (getFirstPart is not null)
        {
            return getFirstPart.Invoke(body, new object[] { slot });
        }

        var getPart = AccessTools.Method(body.GetType(), "GetPart", new[] { typeof(string) });
        var parts = getPart?.Invoke(body, new object[] { slot }) as System.Collections.IEnumerable;
        if (parts is null)
        {
            return null;
        }

        var enumerator = parts.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current : null;
    }

    private static string ResolveWornSlot(object instance)
    {
        var blueprint = AccessTools.Property(instance.GetType(), "Blueprint")?.GetValue(instance);
        var getPartParameter = blueprint is null
            ? null
            : AccessTools.Method(
                blueprint.GetType(),
                "GetPartParameter",
                new[] { typeof(string), typeof(string), typeof(string) });
        if (getPartParameter is null)
        {
            return "Back";
        }

        return getPartParameter.Invoke(blueprint, new object[] { "Armor", "WornOn", "Back" }) as string ?? "Back";
    }
}
