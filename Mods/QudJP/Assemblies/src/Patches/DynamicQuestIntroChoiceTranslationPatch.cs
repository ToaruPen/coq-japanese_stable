using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DynamicQuestIntroChoiceTranslationPatch
{
    private const string Context = nameof(DynamicQuestIntroChoiceTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 3);
        var helperType = AccessTools.TypeByName("XRL.World.DynamicQuestConversationHelper");
        var conversationType = AccessTools.TypeByName("XRL.World.Conversations.ConversationXMLBlueprint");
        var questType = GameTypeResolver.FindType("Qud.API.Quest", "Quest");
        if (helperType is null || conversationType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        if (questType is not null)
        {
            AddTarget(
                targets,
                helperType,
                "fabricateIntroAcceptChoice",
                typeof(string),
                conversationType,
                questType);
        }

        AddTarget(targets, helperType, "fabricateIntroRejectChoice", typeof(string), conversationType);
        AddTarget(targets, helperType, "fabricateIntroAdditionalChoice", typeof(string), conversationType);
        return targets;
    }

    public static void Prefix(ref string text)
    {
        try
        {
            TranslateArgument(ref text, "IntroChoice");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateArgument(ref string text, string route)
    {
        try
        {
            var source = text;
            if (!DynamicQuestExplicitConversationTextTranslator.TryTranslate(source, out var translated))
            {
                text = translated;
                return;
            }

            text = translated;
            DynamicTextObservability.RecordTransform(Context, Context + "." + route, source, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateArgument failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, Type type, string methodName, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}({3}).", Context, type.FullName, methodName, string.Join(", ", Array.ConvertAll(parameterTypes, static type => type.FullName)));
            return;
        }

        targets.Add(method);
    }
}
