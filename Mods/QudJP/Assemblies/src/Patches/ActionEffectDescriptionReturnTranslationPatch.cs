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
        foreach (var target in new (string typeName, string methodName)[]
                 {
                     ("XRL.World.AI.GoalHandlers.Kill", "GetDetails"),
                     ("XRL.World.Tinkering.Disassembly", "GetDescription"),
                     ("XRL.OngoingAction", "GetDescription"),
                     ("XRL.World.Parts.Mutation.Metamorphed", "GetDetails"),
                     ("XRL.World.Parts.IStingerProperties", "GetDescription"),
                 })
        {
            var type = AccessTools.TypeByName(target.typeName);
            if (type is null)
            {
                Trace.TraceError("QudJP: {0} failed to resolve type '{1}'.", Context, target.typeName);
                continue;
            }

            var method = AccessTools.Method(type, target.methodName, Type.EmptyTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.{2}().", Context, target.typeName, target.methodName);
            }
        }
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
