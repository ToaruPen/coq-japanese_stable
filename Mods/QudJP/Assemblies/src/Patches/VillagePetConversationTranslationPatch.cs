using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillagePetConversationTranslationPatch
{
    private const string Context = nameof(VillagePetConversationTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 2);
        AddTargetMethod(targets, "XRL.World.ZoneBuilders.VillageBase");
        AddTargetMethod(targets, "XRL.World.ZoneBuilders.VillageCodaBase");
        return targets;
    }

    public static void Prefix(ref string Q1, ref string A1)
    {
        try
        {
            var questionWasMarked = MessageFrameTranslator.TryStripDirectTranslationMarker(Q1, out var markedQuestion);
            if (questionWasMarked)
            {
                Q1 = markedQuestion;
            }

            var answerWasMarked = MessageFrameTranslator.TryStripDirectTranslationMarker(A1, out var markedAnswer);
            if (answerWasMarked)
            {
                A1 = markedAnswer;
            }

            var sourceQuestion = Q1;
            if (!questionWasMarked && VillagePetConversationTranslator.TryTranslateQuestion(Q1, out var translatedQuestion))
            {
                Q1 = translatedQuestion;
                DynamicTextObservability.RecordTransform(
                    Context,
                    Context + ".petQuestion",
                    sourceQuestion,
                    translatedQuestion);
            }

            var sourceAnswer = A1;
            if (!answerWasMarked && VillagePetConversationTranslator.TryTranslateAnswer(A1, out var translatedAnswer))
            {
                A1 = translatedAnswer;
                DynamicTextObservability.RecordTransform(
                    Context,
                    Context + ".originStory",
                    sourceAnswer,
                    translatedAnswer);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static void AddTargetMethod(ICollection<MethodBase> targets, string typeName)
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
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.AddVillagerConversation(...).", Context, typeName);
            return;
        }

        targets.Add(method);
    }
}
