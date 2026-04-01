using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GivesRepShortDescriptionTranslationPatch
{
    private const string Context = nameof(GivesRepShortDescriptionTranslationPatch);

    private static readonly Regex WaterBondedPattern =
        new Regex(
            "^(?<prefix>\\s*)You are water-bonded with (?<target>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.GivesRep");
        var shortDescriptionEventType = AccessTools.TypeByName("XRL.World.GetShortDescriptionEvent");
        if (targetType is null || shortDescriptionEventType is null)
        {
            Trace.TraceError("QudJP: GivesRepShortDescriptionTranslationPatch failed to resolve GivesRep or GetShortDescriptionEvent.");
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [shortDescriptionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: GivesRepShortDescriptionTranslationPatch.HandleEvent(GetShortDescriptionEvent) not found.");
        }

        return method;
    }

    public static void Prefix(object? E, out int __state)
    {
        try
        {
            __state = 0;
            if (TryGetPostfixBuilder(E, out var postfix))
            {
                __state = postfix.Length;
            }
        }
        catch (Exception ex)
        {
            __state = 0;
            Trace.TraceError("QudJP: GivesRepShortDescriptionTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object? E, int __state)
    {
        try
        {
            if (!TryGetPostfixBuilder(E, out var postfix) || postfix.Length <= __state)
            {
                return;
            }

            var appended = postfix.ToString(__state, postfix.Length - __state);
            var translated = TranslateAppendedText(appended);
            if (string.Equals(appended, translated, StringComparison.Ordinal))
            {
                return;
            }

            postfix.Remove(__state, postfix.Length - __state);
            postfix.Append(translated);
            DynamicTextObservability.RecordTransform(Context, "GivesRep.WaterBonded", appended, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GivesRepShortDescriptionTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static string TranslateAppendedText(string source)
    {
        var match = WaterBondedPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        return string.Concat(
            match.Groups["prefix"].Value,
            match.Groups["target"].Value.Trim(),
            "と水の絆で結ばれている。");
    }

    private static bool TryGetPostfixBuilder(object? eventObject, out StringBuilder postfix)
    {
        postfix = null!;
        if (eventObject is null)
        {
            return false;
        }

        var property = AccessTools.Property(eventObject.GetType(), "Postfix");
        if (property?.GetValue(eventObject) is StringBuilder propertyBuilder)
        {
            postfix = propertyBuilder;
            return true;
        }

        var field = AccessTools.Field(eventObject.GetType(), "Postfix");
        if (field?.GetValue(eventObject) is StringBuilder fieldBuilder)
        {
            postfix = fieldBuilder;
            return true;
        }

        return false;
    }
}
