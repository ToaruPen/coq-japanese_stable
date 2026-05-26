using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GasGenerationDescriptionTranslationPatch
{
    internal const string Context = nameof(GasGenerationDescriptionTranslationPatch);
    internal const string Family = Context + ".SyncFromBlueprint";

    private static readonly Regex GasBurstPattern = new(
        "^You release a burst of (?<gas>.+) around yourself\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> GasDisplayNameTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["corrosive gas"] = "腐食性ガス",
            ["confusion gas"] = "混乱ガス",
            ["normality gas"] = "正常化ガス",
            ["poison gas"] = "毒ガス",
            ["sleep gas"] = "睡眠ガス",
        };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.GasGeneration");
        var method = targetType is null ? null : AccessTools.Method(targetType, "SyncFromBlueprint", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: GasGeneration.SyncFromBlueprint().", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var source = DescriptionPartReflectionHelpers.GetStringMemberValue(__instance, "Description");
            if (!TryTranslateDescription(source, out var translated))
            {
                return;
            }

            if (DescriptionPartReflectionHelpers.SetStringMemberValue(__instance, "Description", translated))
            {
                DynamicTextObservability.RecordTransform(Context, Family, source!, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateDescriptionForTests(string source)
    {
        return TryTranslateDescription(source, out var translated) ? translated : source;
    }

    internal static bool TryTranslateDescription(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (string.Equals(source, "You release a gaseous burst around yourself.", StringComparison.Ordinal))
        {
            translated = "周囲にガスを噴出する。";
            return true;
        }

        var match = GasBurstPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        translated = "周囲に" + TranslateGasDisplayName(match.Groups["gas"].Value) + "を噴出する。";
        return true;
    }

    private static string TranslateGasDisplayName(string source)
    {
        var translated = DisplayNameCaptureTranslator.TranslatePreservingColors(source, Context);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return translated;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        return GasDisplayNameTranslations.TryGetValue(visible, out var mapped)
            ? ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => mapped)
            : source;
    }
}
