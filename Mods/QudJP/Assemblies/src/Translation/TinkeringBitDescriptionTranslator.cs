using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class TinkeringBitDescriptionTranslator
{
    private static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scrap power systems"] = "スクラップ動力系",
            ["scrap crystal"] = "スクラップ結晶",
            ["scrap metal"] = "スクラップ金属",
            ["scrap electronics"] = "スクラップ電子部品",
            ["phasic power systems"] = "位相動力系",
            ["flawless crystal"] = "完全結晶",
            ["pure alloy"] = "純合金",
            ["pristine electronics"] = "精密電子部品",
            ["nanomaterials"] = "ナノマテリアル",
            ["photonics"] = "フォトニクス",
            ["AI microcontrollers"] = "AIマイクロコントローラ",
            ["metacrystal"] = "メタクリスタル",
        };

    private static readonly Regex InventoryLinePattern = new(
        "^(?<symbol>[A-D1-8?]) x(?<count>\\d+) - (?<description>scrap power systems|scrap crystal|scrap metal|scrap electronics|phasic power systems|flawless crystal|pure alloy|pristine electronics|nanomaterials|photonics|AI microcontrollers|metacrystal)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DescriptionPattern = new(
        "scrap power systems|scrap crystal|scrap metal|scrap electronics|phasic power systems|flawless crystal|pure alloy|pristine electronics|nanomaterials|photonics|AI microcontrollers|metacrystal",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryTranslateDescription(string source, out string translated)
    {
        return Descriptions.TryGetValue(source, out translated!);
    }

    public static bool TryTranslateInventoryLine(string source, out string translated)
    {
        var match = InventoryLinePattern.Match(source);
        if (!match.Success
            || !TryTranslateDescription(match.Groups["description"].Value, out var description))
        {
            translated = source;
            return false;
        }

        translated = string.Concat(
            match.Groups["symbol"].Value,
            " x",
            match.Groups["count"].Value,
            " - ",
            description);
        return true;
    }

    public static bool TryTranslateKnownDescriptionsInText(string source, out string translated)
    {
        var changed = false;
        translated = DescriptionPattern.Replace(
            source,
            match =>
            {
                if (!TryTranslateDescription(match.Value, out var description))
                {
                    return match.Value;
                }

                changed = true;
                return description;
            });
        return changed;
    }
}
