using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DynamicQuestGeneratedQuestTextTranslationPatch
{
    private const string Context = nameof(DynamicQuestGeneratedQuestTextTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 3);
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} GameObject type not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver",
            "fabricateFindASpecificItemQuest",
            gameObjectType,
            typeof(string));
        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver",
            "fabricateFindASpecificSiteQuest",
            gameObjectType);
        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver",
            "fabricateInteractWithAnObjectQuest",
            gameObjectType,
            typeof(string));
        return targets;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            TranslateStringMember(__result, "Name", "QuestName");
            var stepsById = GetMemberValue(__result, "StepsByID") as IDictionary;
            if (stepsById is null)
            {
                return;
            }

            foreach (var step in stepsById.Values)
            {
                if (step is null)
                {
                    continue;
                }

                TranslateStringMember(step, "Name", "QuestStepName");
                TranslateStringMember(step, "Text", "QuestStepText");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateStringMember(object instance, string memberName, string route)
    {
        var source = GetStringMemberValue(instance, memberName);
        if (!DynamicQuestGeneratedQuestTextTranslator.TryTranslate(source, out var translated))
        {
            if (!string.Equals(source, translated, StringComparison.Ordinal))
            {
                SetStringMemberValue(instance, memberName, translated);
            }

            return;
        }

        if (SetStringMemberValue(instance, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, Context + "." + route, source ?? string.Empty, translated);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, string typeName, string methodName, params Type[] parameterTypes)
    {
        var type = AccessTools.TypeByName(typeName);
        var method = type is null ? null : AccessTools.Method(type, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}({3}).", Context, typeName, methodName, string.Join(", ", Array.ConvertAll(parameterTypes, static type => type.FullName)));
            return;
        }

        targets.Add(method);
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static bool SetStringMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return true;
        }

        return false;
    }
}
