using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FabricateFromSelfAbilityDescriptionTranslationPatch
{
    private const string Context = nameof(FabricateFromSelfAbilityDescriptionTranslationPatch);

    private static readonly Regex FabricateAbilityPattern = new(
        "^Fabricate (?<object>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var fabricateType = AccessTools.TypeByName("XRL.World.Parts.FabricateFromSelf");
        if (fabricateType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var getter = AccessTools.PropertyGetter(fabricateType, "AbilityDescription");
        if (getter is null)
        {
            Trace.TraceError("QudJP: {0}.AbilityDescription getter not found.", Context);
        }

        return getter;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            __result = TranslateAbilityDescriptionForTests(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateAbilityDescriptionForTests(string source)
    {
        if (string.IsNullOrEmpty(source) || source.StartsWith("\u0001", StringComparison.Ordinal))
        {
            return source;
        }

        var match = FabricateAbilityPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var sourceObject = match.Groups["object"].Value;
        var translatedObject = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sourceObject,
            nameof(FabricateFromSelfAbilityDescriptionTranslationPatch));
        if (string.Equals(translatedObject, sourceObject, StringComparison.Ordinal))
        {
            translatedObject = ColorAwareTranslationComposer.TranslatePreservingColors(
                sourceObject,
                visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var translated)
                    ? translated
                    : visible);
        }

        return translatedObject + "を生成する";
    }
}
