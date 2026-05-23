using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActivatedAbilitiesAddAbilityPopupTranslationPatch
{
    private const string Context = nameof(ActivatedAbilitiesAddAbilityPopupTranslationPatch);

    private static readonly Regex GainedAbilityPattern = new(
        "^You have gained the activated ability (?<ability>.+?)\\.(?<hint>\\n\\(press a to use activated abilities\\))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.ActivatedAbilities");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "AddAbility");
        if (method is not null)
        {
            yield return method;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.AddAbility target not found.", Context);
        }
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = GainedAbilityPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var ability = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["ability"].Value,
            spans,
            match.Groups["ability"]);
        ability = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            ability,
            Context + ".AbilityName",
            "Popup.ProducerText." + Context + ".AbilityName");

        translated = "起動アビリティ " + ability + " を得た。";
        if (match.Groups["hint"].Success)
        {
            translated += "\n（起動アビリティを使うには{{W|a}}を押す）";
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".GainedAbility",
            source,
            translated);
        return true;
    }
}
