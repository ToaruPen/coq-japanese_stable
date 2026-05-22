using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HexacherubimSpawnerHandleEventTranslationPatch
{
    private const string TargetTypeName = "XRL.World.Parts.HexacherubimSpawner";
    private const string BeforeObjectCreatedEventTypeName = "XRL.World.BeforeObjectCreatedEvent";
    private const string Context = nameof(HexacherubimSpawnerHandleEventTranslationPatch);
    private const string DisplayNameFamily = "HexacherubimSpawner.HandleEvent.DisplayName";
    private const string DescriptionFamily = "HexacherubimSpawner.HandleEvent.Description";

    private static readonly Regex BaseDescriptionPattern = new(
        "^Gallium veins press against the underside of =pronouns\\.possessive= crystalline (?<skin>.+?) and gleam warmly\\. " +
        "=pronouns\\.Possessive= body is perfect, and the whole of it is wet with amniotic slick; could " +
        "=pronouns\\.subjective= have just now peeled =pronouns\\.reflexive= off an oil canvas\\? " +
        "=verb:Were:afterpronoun= =pronouns\\.subjective= cast into the material realm by a dreaming, dripping brain\\? " +
        "Whatever the embryo, =pronouns\\.subjective= =verb:are:afterpronoun= now the archetypal (?<creatureType>.+?); " +
        "it's all there in impeccable simulacrum: (?<features>.+)\\. Perfection is realized\\.$",
        RegexOptions.CultureInvariant);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var eventType = AccessTools.TypeByName(BeforeObjectCreatedEventTypeName);
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: HexacherubimSpawnerHandleEventTranslationPatch failed to resolve HexacherubimSpawner or BeforeObjectCreatedEvent.");
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: HexacherubimSpawnerHandleEventTranslationPatch.HandleEvent(BeforeObjectCreatedEvent) not found.");
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

            TranslateDisplayName(replacementObject);
            TranslateDescription(replacementObject);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HexacherubimSpawnerHandleEventTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateDisplayName(object gameObject)
    {
        var render = DescriptionPartReflectionHelpers.GetMemberValue(gameObject, "Render");
        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(render, "DisplayName");
        if (render is null || string.IsNullOrEmpty(source))
        {
            return;
        }

        var sourceValue = source!;
        var translated = TranslateHexacherubimNameFragment(sourceValue);
        if (!string.Equals(sourceValue, translated, StringComparison.Ordinal)
            && DescriptionPartReflectionHelpers.SetStringMemberValue(render, "DisplayName", translated))
        {
            TryResetNameCache(gameObject);
            DynamicTextObservability.RecordTransform(Context, DisplayNameFamily, sourceValue, translated);
        }
    }

    private static void TranslateDescription(object gameObject)
    {
        if (!DescriptionPartReflectionHelpers.TryGetDescriptionPart(
                gameObject,
                Context,
                logFallback: true,
                out var descriptionPart))
        {
            return;
        }

        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "_Short");
        if (source is null)
        {
            Trace.TraceWarning("QudJP: {0} falling back from Description._Short to Description.Short.", Context);
            source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "Short");
        }

        var translated = TryTranslateBaseDescription(source);
        if (translated is null)
        {
            return;
        }

        var changed = false;
        changed |= DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "Short", translated);
        changed |= DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "_Short", translated);
        if (changed)
        {
            DynamicTextObservability.RecordTransform(Context, DescriptionFamily, source, translated);
        }
    }

    private static string? TryTranslateBaseDescription(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        var match = BaseDescriptionPattern.Match(source);
        if (!match.Success)
        {
            return null;
        }

        var skin = match.Groups["skin"].Value;
        var creatureType = TranslateHexacherubimNameFragment(match.Groups["creatureType"].Value);
        var features = match.Groups["features"].Value;
        return "ガリウムの脈が=pronouns.possessive=結晶質の" + skin + "の下側を押し上げ、暖かく輝いている。"
            + "=pronouns.Possessive=身体は完璧で、全身が羊水めいたぬめりに濡れている。"
            + "=pronouns.subjective=はたった今、油絵のキャンバスから=pronouns.reflexive=を剥がしてきたのだろうか？"
            + "=verb:Were:afterpronoun= =pronouns.subjective=は夢見る滴る脳によって物質界へ鋳込まれたのだろうか？"
            + "胚が何であれ、=pronouns.subjective= =verb:are:afterpronoun=いまや原型たる" + creatureType + "だ。"
            + "それは非の打ちどころのない模像としてすべて備わっている：" + features + "。完璧は実現した。";
    }

    private static void TryResetNameCache(object gameObject)
    {
        AccessTools.Method(gameObject.GetType(), "ResetNameCache", Type.EmptyTypes)?.Invoke(gameObject, null);
    }

    private static string TranslateHexacherubimNameFragment(string source)
    {
        var translated = source.Replace("hexacherub", "六智天使");
        if (translated.Contains("六智天使"))
        {
            return translated;
        }

        return translated.Replace("智天使", "六智天使");
    }
}
