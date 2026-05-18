using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageLeaderConversationTranslationPatch
{
    private const string Context = nameof(VillageLeaderConversationTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 3);
        AddVillageTargetMethod(targets, "XRL.World.ZoneBuilders.VillageBase");
        AddVillageTargetMethod(targets, "XRL.World.ZoneBuilders.VillageCodaBase");
        AddConversationApiTargetMethod(targets);
        return targets;
    }

    public static void Prefix([HarmonyArgument(1)] ref string text)
    {
        try
        {
            if (MessageFrameTranslator.TryStripDirectTranslationMarker(text, out var markedText))
            {
                text = markedText;
                return;
            }

            var source = text;
            if (!VillageLeaderConversationTranslator.TryTranslate(text, out var translated))
            {
                return;
            }

            text = translated;
            DynamicTextObservability.RecordTransform(Context, Context + ".leaderIntro", source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static void AddVillageTargetMethod(ICollection<MethodBase> targets, string typeName)
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Method(
                targetType,
                "AddVillagerConversation",
                new[]
                {
                    gameObjectType,
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                });
        AddResolvedTarget(targets, method, typeName + ".AddVillagerConversation(...)");
    }

    private static void AddConversationApiTargetMethod(ICollection<MethodBase> targets)
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var method = gameObjectType is null
            ? null
            : AccessTools.Method(
                "Qud.API.ConversationsAPI:addSimpleConversationToObject",
                new[]
                {
                    gameObjectType,
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                });
        AddResolvedTarget(targets, method, "Qud.API.ConversationsAPI.addSimpleConversationToObject(simple)");
    }

    private static void AddResolvedTarget(ICollection<MethodBase> targets, MethodBase? method, string label)
    {
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.", Context, label);
            return;
        }

        targets.Add(method);
    }
}
