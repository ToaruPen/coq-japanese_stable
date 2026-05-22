using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsBehaviorDescriptionTranslationPatch
{
    private const string Context = nameof(CyberneticsBehaviorDescriptionTranslationPatch);

    private static readonly Regex SchemasoftDescriptionPattern =
        new(
            "^You gain access to every schematic of (?<tier>low tier|mid tier|high tier) (?<category>ammo and energy cells|pistols|rifles|melee weapons|grenades|tonics|utility|armor|heavy weapons)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.World.GetCyberneticsBehaviorDescriptionEvent:GetFor",
            [AccessTools.TypeByName("XRL.World.GameObject"), typeof(string)]);
        if (method is not null)
        {
            return method;
        }

        Trace.TraceError("QudJP: Failed to resolve GetCyberneticsBehaviorDescriptionEvent.GetFor(...). Patch will not apply.");
        return null;
    }

    public static void Postfix(ref string? __result)
    {
        try
        {
            if (TryTranslate(__result, out var translated))
            {
                __result = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (source is null)
        {
            translated = string.Empty;
            return false;
        }

        if (source.Length == 0)
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var original = source;
        var lines = original.Split('\n');
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var text = hasCarriageReturn ? line.Substring(0, line.Length - 1) : line;
            if (!TryTranslateLine(text, out var translatedLine))
            {
                continue;
            }

            lines[index] = hasCarriageReturn ? translatedLine + "\r" : translatedLine;
            changed = true;
        }

        translated = changed ? string.Join("\n", lines) : original;
        if (changed)
        {
            DynamicTextObservability.RecordTransform(
                Context,
                "CyberneticsBehaviorDescription.Schemasoft",
                source,
                translated);
        }

        return changed;
    }

    private static bool TryTranslateLine(string source, out string translated)
    {
        var match = SchemasoftDescriptionPattern.Match(source);
        if (!match.Success
            || !TryTranslateTier(match.Groups["tier"].Value, out var tier)
            || !TryTranslateCategory(match.Groups["category"].Value, out var category))
        {
            translated = source;
            return false;
        }

        translated = tier + "の" + category + "の全設計図にアクセスできる。";
        return true;
    }

    private static bool TryTranslateTier(string source, out string translated)
    {
        translated = source switch
        {
            "low tier" => "下位",
            "mid tier" => "中位",
            "high tier" => "上位",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslateCategory(string source, out string translated)
    {
        translated = source switch
        {
            "ammo and energy cells" => "弾薬とエネルギーセル",
            "pistols" => "ピストル",
            "rifles" => "ライフル",
            "melee weapons" => "近接武器",
            "grenades" => "グレネード",
            "tonics" => "トニック",
            "utility" => "ユーティリティ",
            "armor" => "防具",
            "heavy weapons" => "重火器",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }
}
