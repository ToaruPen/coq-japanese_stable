using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DescriptionInspectStatusPatch
{
    private const string StatusDictionaryFile = "ui-phase3c-labels.ja.json";

    private static readonly IReadOnlyDictionary<string, string> FallbackStatusTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Friendly"] = "友好",
            ["Hostile"] = "敵対",
            ["Neutral"] = "中立",
            ["Impossible"] = "不可能",
            ["Very Tough"] = "非常に困難",
            ["Tough"] = "困難",
            ["Average"] = "普通",
            ["Easy"] = "容易",
            ["Trivial"] = "取るに足りない",
            ["Badly Wounded"] = "瀕死",
            ["Wounded"] = "負傷",
            ["Injured"] = "軽傷",
            ["Fine"] = "良好",
            ["Perfect"] = "完全",
            ["Badly Damaged"] = "大破",
            ["Damaged"] = "損傷",
            ["Lightly Damaged"] = "軽微な損傷",
        };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: DescriptionInspectStatusPatch target GameObject type not found.");
            return targets;
        }

        AddTargetMethod(targets, "XRL.World.Parts.Description", "GetFeelingDescription", gameObjectType);
        AddTargetMethod(targets, "XRL.World.Parts.Description", "GetDifficultyDescription", gameObjectType);
        AddTargetMethod(targets, "XRL.Rules.Strings", "WoundLevel", gameObjectType);

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: DescriptionInspectStatusPatch resolved no target methods.");
        }

        return targets;
    }

    private static void AddTargetMethod(List<MethodBase> targets, string typeName, string methodName, Type gameObjectType)
    {
        var method = AccessTools.Method(typeName + ":" + methodName, new[] { gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: DescriptionInspectStatusPatch failed to resolve {0}.{1}(GameObject).", typeName, methodName);
            return;
        }

        targets.Add(method);
    }

    public static void Postfix(MethodBase __originalMethod, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            var context = nameof(DescriptionInspectStatusPatch) + "." + __originalMethod.Name;
            var translated = TranslateInspectStatusText(__result, context);
            if (string.Equals(translated, __result, StringComparison.Ordinal))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: DescriptionInspectStatusPatch.Postfix failed: {0}", ex);
        }
    }

    internal static string TranslateInspectStatusTextForTests(string source)
    {
        return TranslateInspectStatusText(source, nameof(DescriptionInspectStatusPatch));
    }

    private static string TranslateInspectStatusText(string source, string context)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return MessageFrameTranslator.MarkDirectTranslation(TranslateInspectStatusText(markedText, context));
        }

        using var _ = Translator.PushLogContext(context);
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleStatus(visible, out var visibleTranslated)
                ? visibleTranslated
                : visible);
        return translated;
    }

    private static bool TryTranslateVisibleStatus(string source, out string translated)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, StatusDictionaryFile);
        if (!string.IsNullOrEmpty(scoped))
        {
            translated = scoped!;
            return !string.Equals(translated, source, StringComparison.Ordinal);
        }

        if (FallbackStatusTranslations.TryGetValue(source, out var fallback))
        {
            translated = fallback;
            return true;
        }

        translated = source;
        return false;
    }
}
