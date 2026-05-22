using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CherubimSpawnerHandleEventTranslationPatch
{
    private const string TargetTypeName = "XRL.World.Parts.CherubimSpawner";
    private const string BeforeObjectCreatedEventTypeName = "XRL.World.BeforeObjectCreatedEvent";
    private const string Context = nameof(CherubimSpawnerHandleEventTranslationPatch);
    private const string DisplayNameFamily = "CherubimSpawner.HandleEvent.MechanicalDisplayName";
    private const string MechanicalPrefix = "mechanical ";
    private const string JapaneseMechanicalPrefix = "機械仕掛けの";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var eventType = AccessTools.TypeByName(BeforeObjectCreatedEventTypeName);
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: CherubimSpawnerHandleEventTranslationPatch failed to resolve CherubimSpawner or BeforeObjectCreatedEvent.");
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: CherubimSpawnerHandleEventTranslationPatch.HandleEvent(BeforeObjectCreatedEvent) not found.");
        }

        return method;
    }

    public static void Postfix(object? __0)
    {
        try
        {
            var replacementObject = DescriptionPartReflectionHelpers.GetMemberValue(__0, "ReplacementObject");
            if (replacementObject is null)
            {
                return;
            }

            TranslateMechanicalDisplayName(replacementObject);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CherubimSpawnerHandleEventTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateMechanicalDisplayName(object gameObject)
    {
        var render = DescriptionPartReflectionHelpers.GetMemberValue(gameObject, "Render");
        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(render, "DisplayName");
        if (render is null || source is null || !source.StartsWith(MechanicalPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var translated = JapaneseMechanicalPrefix + source.Substring(MechanicalPrefix.Length);
        if (DescriptionPartReflectionHelpers.SetStringMemberValue(render, "DisplayName", translated))
        {
            TryResetNameCache(gameObject);
            DynamicTextObservability.RecordTransform(Context, DisplayNameFamily, source, translated);
        }
    }

    private static void TryResetNameCache(object gameObject)
    {
        AccessTools.Method(gameObject.GetType(), "ResetNameCache", Type.EmptyTypes)?.Invoke(gameObject, null);
    }
}
