using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HagglingSifrahResultDescriptionTranslationPatch
{
    internal const string Context = nameof(HagglingSifrahResultDescriptionTranslationPatch);
    internal const string Family = "HagglingSifrah.ResultDescription";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.HagglingSifrah");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        foreach (var methodName in new[]
                 {
                     "ResultCriticalFailure",
                     "ResultFailure",
                     "ResultPartialSuccess",
                     "ResultSuccess",
                     "ResultExceptionalSuccess",
                 })
        {
            var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}() not found.", Context, methodName);
            }
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (__instance is null || !TryGetDescription(__instance, out var source))
            {
                return;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
            {
                _ = TrySetDescription(__instance, markedText);
                return;
            }

            if (!TryTranslateDescription(source, out var translated, out var detail))
            {
                return;
            }

            if (TrySetDescription(__instance, translated))
            {
                DynamicTextObservability.RecordTransform(Context, Family + "." + detail, source, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateDescription(string source, out string translated, out string detail)
    {
        switch (source)
        {
            case "Your haggling was an abysmal failure.":
                translated = "交渉は壊滅的な失敗だった。";
                detail = "CriticalFailure";
                return true;
            case "Your haggling went poorly.":
                translated = "交渉はうまくいかなかった。";
                detail = "Failure";
                return true;
            case "Your haggling was mediocre.":
                translated = "交渉はそこそこの結果だった。";
                detail = "PartialSuccess";
                return true;
            case "Your haggling went well.":
                translated = "交渉はうまくいった。";
                detail = "Success";
                return true;
            case "Your haggling was spectacular.":
                translated = "交渉は見事な成功だった。";
                detail = "ExceptionalSuccess";
                return true;
            default:
                translated = source;
                detail = string.Empty;
                return false;
        }
    }

    private static bool TryGetDescription(object instance, out string description)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, "Description");
        if (field?.GetValue(instance) is string fieldValue)
        {
            description = fieldValue;
            return true;
        }

        var property = AccessTools.Property(type, "Description");
        if (property?.GetValue(instance) is string propertyValue)
        {
            description = propertyValue;
            return true;
        }

        description = string.Empty;
        return false;
    }

    private static bool TrySetDescription(object instance, string description)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, "Description");
        if (field is not null)
        {
            field.SetValue(instance, description);
            return true;
        }

        var property = AccessTools.Property(type, "Description");
        if (property?.CanWrite == true)
        {
            property.SetValue(instance, description);
            return true;
        }

        return false;
    }
}
