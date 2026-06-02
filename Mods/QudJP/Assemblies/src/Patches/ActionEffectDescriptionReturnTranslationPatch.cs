using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActionEffectDescriptionReturnTranslationPatch
{
    internal const string Context = nameof(ActionEffectDescriptionReturnTranslationPatch);
    internal const string Family = "ActionEffect.DescriptionReturn";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var target in new (string typeName, string methodName, string[] parameterTypeNames)[]
                 {
                     ("XRL.World.AI.GoalHandlers.Kill", "GetDetails", Array.Empty<string>()),
                     ("XRL.World.Tinkering.Disassembly", "GetDescription", Array.Empty<string>()),
                     ("XRL.OngoingAction", "GetDescription", Array.Empty<string>()),
                     (
                         "XRL.World.Capabilities.AutoAct",
                         "GetDescription",
                         new[] { "System.String", "XRL.OngoingAction" }),
                     ("XRL.World.Parts.Mutation.Metamorphed", "GetDetails", Array.Empty<string>()),
                     ("XRL.World.Parts.IStingerProperties", "GetDescription", Array.Empty<string>()),
                 })
        {
            var type = AccessTools.TypeByName(target.typeName);
            if (type is null)
            {
                Trace.TraceError("QudJP: {0} failed to resolve type '{1}'.", Context, target.typeName);
                continue;
            }

            var parameterTypes = ResolveParameterTypes(target.typeName, target.methodName, target.parameterTypeNames);
            if (parameterTypes is null)
            {
                continue;
            }

            var method = AccessTools.Method(type, target.methodName, parameterTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError(
                    "QudJP: {0} failed to resolve {1}.{2}({3}).",
                    Context,
                    target.typeName,
                    target.methodName,
                    string.Join(", ", target.parameterTypeNames));
            }
        }
    }

    private static Type[]? ResolveParameterTypes(string typeName, string methodName, string[] parameterTypeNames)
    {
        if (parameterTypeNames.Length == 0)
        {
            return Type.EmptyTypes;
        }

        var parameterTypes = new Type[parameterTypeNames.Length];
        for (var i = 0; i < parameterTypeNames.Length; i++)
        {
            var parameterTypeName = parameterTypeNames[i];
            var parameterType = Type.GetType(parameterTypeName);
            if (parameterType is null)
            {
                Trace.TraceWarning(
                    "QudJP: {0} Type.GetType failed for parameter type '{1}' while resolving {2}.{3}; falling back to AccessTools.TypeByName.",
                    Context,
                    parameterTypeName,
                    typeName,
                    methodName);
                parameterType = AccessTools.TypeByName(parameterTypeName);
            }

            if (parameterType is null)
            {
                Trace.TraceError(
                    "QudJP: {0} failed to resolve parameter type '{1}' for {2}.{3}.",
                    Context,
                    parameterTypeName,
                    typeName,
                    methodName);
                return null;
            }

            parameterTypes[i] = parameterType;
        }

        return parameterTypes;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (!ActionEffectDescriptionReturnTranslator.TryTranslate(__result, out var translated, out var detail))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family + "." + detail, __result, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}

internal static class ActionEffectDescriptionReturnTranslator
{
    private static readonly Regex StingerDescriptionPattern = new(
        "^You bear a tail with a stinger that delivers (?<adjective>confusing|paralyzing|poisonous) venom to your enemies\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(string source, out string translated, out string detail)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            detail = string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        switch (stripped)
        {
            case "Player":
                translated = ColorAwareTranslationComposer.Restore("プレイヤー", spans);
                detail = "Player";
                return true;
            case "disassembling":
                translated = ColorAwareTranslationComposer.Restore("分解中", spans);
                detail = "Disassembling";
                return true;
            case "acting":
                translated = ColorAwareTranslationComposer.Restore("行動中", spans);
                detail = "Acting";
                return true;
            case "exploring":
                translated = ColorAwareTranslationComposer.Restore("探索中", spans);
                detail = "AutoActExploring";
                return true;
            case "waiting":
                translated = ColorAwareTranslationComposer.Restore("待機中", spans);
                detail = "AutoActWaiting";
                return true;
            case "digging":
                translated = ColorAwareTranslationComposer.Restore("掘削中", spans);
                detail = "AutoActDigging";
                return true;
            case "gathering":
                translated = ColorAwareTranslationComposer.Restore("収集中", spans);
                detail = "AutoActGathering";
                return true;
            case "resting":
                translated = ColorAwareTranslationComposer.Restore("休息中", spans);
                detail = "AutoActResting";
                return true;
            case "attacking":
                translated = ColorAwareTranslationComposer.Restore("攻撃中", spans);
                detail = "AutoActAttacking";
                return true;
            case "moving":
                translated = ColorAwareTranslationComposer.Restore("移動中", spans);
                detail = "AutoActMoving";
                return true;
            case "Assuming another creature's form.":
                translated = ColorAwareTranslationComposer.Restore("別の生物の姿をとっている。", spans);
                detail = "MetamorphedDetails";
                return true;
        }

        var stingerMatch = StingerDescriptionPattern.Match(stripped);
        if (stingerMatch.Success)
        {
            var adjective = stingerMatch.Groups["adjective"].Value switch
            {
                "confusing" => "混乱毒",
                "paralyzing" => "麻痺毒",
                "poisonous" => "毒",
                _ => string.Empty,
            };

            translated = ColorAwareTranslationComposer.Restore("臀部の" + adjective + "針を持つ。", spans);
            detail = "StingerDescription";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }
}
