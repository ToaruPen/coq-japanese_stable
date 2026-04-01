using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class LegacyGamepadPromptTranslationHelpers
{
    private static readonly Regex HiddenByFilterPattern =
        new Regex(@"^(?<count>\d+) items hidden by filter$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RaisePromptPattern =
        new Regex(@"^(?<prefix>\s*\[\{\{W\|.+?\}\}\]\s*)Raise$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuyRandomPattern =
        new Regex(@"^(?<prefix>\{\{W\|M\}\} - )?Buy a new random (?<term>.+) for 4 MP$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SingleKeyToExitPattern =
        new Regex(@"^(?<prefix>\s*\{\{W\|.+?\}\}) to exit $", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DualKeyToExitPattern =
        new Regex(@"^(?<prefix>\s*\{\{W\|.+?\}\} or \{\{W\|.+?\}\}) to exit $", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex JournalDeletePattern =
        new Regex(@"^(?<prefix>\s*\{\{W\|.+?\}\} - )Delete $", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex JournalAddDeletePattern =
        new Regex(@"^(?<insert>\s*\{\{W\|.+?\}\} )Add\s+(?<delete>\{\{W\|.+?\}\} - )Delete $", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SkillsAndPowersBuyPattern =
        new Regex(@"^(?<prefix>\s*\[\{\{[WK]\|.+?\}\}-)Buy(?<suffix>\]\s*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SetPrimaryLimbPattern =
        new Regex(@"^\[\{\{(?<color>[WK])\|(?<glyph>.+?) - Set primary limb\}\}\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string TranslateXrlManualLiteral(string source) => source;

    public static string TranslateXrlManualRendered(string source)
    {
        return ReplaceOrdinal(
            ReplaceOrdinal(source, "Select Topic", "トピックを選択"),
            "Exit Help",
            "ヘルプを終了");
    }

    public static string TranslateInventoryLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Character | Equipment {{W|9}} >" => "< {{W|7}} キャラクター | 装備 {{W|9}} >",
            " Character | Equipment " => " キャラクター | 装備 ",
            _ => source,
        };
    }

    public static string TranslateInventoryRendered(string source)
    {
        var translated = TranslateFooter(source, "Character", "キャラクター", "Equipment", "装備");
        translated = TranslateToExit(translated);
        translated = translated switch
        {
            "<more...>" => "<続き…>",
            "<...more>" => "<…前へ>",
            _ => translated,
        };

        var hiddenByFilterMatch = HiddenByFilterPattern.Match(translated);
        if (!hiddenByFilterMatch.Success)
        {
            return translated;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "フィルターにより{0}個のアイテムが非表示",
            hiddenByFilterMatch.Groups["count"].Value);
    }

    public static string TranslateStatusLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Skills | Inventory {{W|9}} >" => "< {{W|7}} スキル | インベントリ {{W|9}} >",
            " Skills | Inventory " => " スキル | インベントリ ",
            _ => source,
        };
    }

    public static string TranslateStatusRendered(string source)
    {
        var translated = TranslateFooter(source, "Skills", "スキル", "Inventory", "インベントリ");
        translated = TranslateToExit(translated);

        var raiseMatch = RaisePromptPattern.Match(translated);
        if (raiseMatch.Success)
        {
            translated = raiseMatch.Groups["prefix"].Value + "上昇";
        }

        var buyMatch = BuyRandomPattern.Match(translated);
        if (buyMatch.Success)
        {
            translated = (buyMatch.Groups["prefix"].Success ? buyMatch.Groups["prefix"].Value : string.Empty)
                         + "新しいランダムな"
                         + buyMatch.Groups["term"].Value
                         + "を4 MPで購入";
        }

        return translated;
    }

    public static string TranslateJournalLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Quests | Tinkering {{W|9}} >" => "< {{W|7}} クエスト | ティンカリング {{W|9}} >",
            " Quests | Tinkering " => " クエスト | ティンカリング ",
            _ => source,
        };
    }

    public static string TranslateJournalRendered(string source)
    {
        var translated = TranslateFooter(source, "Quests", "クエスト", "Tinkering", "ティンカリング");
        translated = TranslateToExit(translated);

        var addDeleteMatch = JournalAddDeletePattern.Match(translated);
        if (addDeleteMatch.Success)
        {
            translated = addDeleteMatch.Groups["insert"].Value + "追加 " + addDeleteMatch.Groups["delete"].Value + "削除 ";
        }
        else
        {
            var deleteMatch = JournalDeletePattern.Match(translated);
            if (deleteMatch.Success)
            {
                translated = deleteMatch.Groups["prefix"].Value + "削除 ";
            }
        }

        return translated;
    }

    public static string TranslateTinkeringLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Journal | Skills {{W|9}} >" => "< {{W|7}} ジャーナル | スキル {{W|9}} >",
            " Journal | Skills " => " ジャーナル | スキル ",
            _ => source,
        };
    }

    public static string TranslateTinkeringRendered(string source)
    {
        var translated = TranslateFooter(source, "Journal", "ジャーナル", "Skills", "スキル");
        translated = translated switch
        {
            "{{Y|>}} {{W|Build}}    {{w|Mod}}" => "{{Y|>}} {{W|製作}}    {{w|改造}}",
            "  {{w|Build}}  {{Y|>}} {{W|Mod}}" => "  {{w|製作}}  {{Y|>}} {{W|改造}}",
            _ => translated,
        };

        if (ContainsOrdinal(translated, " Mod Item  ")
            && ContainsOrdinal(translated, " List Mods  ")
            && ContainsOrdinal(translated, " Exit "))
        {
            translated = ReplaceOrdinal(
                ReplaceOrdinal(
                    ReplaceOrdinal(translated, " Mod Item  ", " アイテム改造  "),
                    " List Mods  ",
                    " 改造一覧  "),
                " Exit ",
                " 終了 ");
        }

        if (ContainsOrdinal(translated, " Build  ")
            && ContainsOrdinal(translated, " Scroll  ")
            && ContainsOrdinal(translated, " Exit "))
        {
            translated = ReplaceOrdinal(
                ReplaceOrdinal(
                    ReplaceOrdinal(translated, " Build  ", " 製作  "),
                    " Scroll  ",
                    " スクロール  "),
                " Exit ",
                " 終了 ");
        }

        return translated;
    }

    public static string TranslateQuestLogLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Factions | Journal {{W|9}} >" => "< {{W|7}} 派閥 | ジャーナル {{W|9}} >",
            " Factions | Journal " => " 派閥 | ジャーナル ",
            _ => source,
        };
    }

    public static string TranslateQuestLogRendered(string source)
    {
        return TranslateToExit(TranslateFooter(source, "Factions", "派閥", "Journal", "ジャーナル"));
    }

    public static string TranslateFactionsLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Equipment | Quests {{W|9}} >" => "< {{W|7}} 装備 | クエスト {{W|9}} >",
            " Equipment | Quests " => " 装備 | クエスト ",
            _ => source,
        };
    }

    public static string TranslateFactionsRendered(string source)
    {
        return TranslateToExit(TranslateFooter(source, "Equipment", "装備", "Quests", "クエスト"));
    }

    public static string TranslateSkillsAndPowersLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Tinkering | Character {{W|9}} >" => "< {{W|7}} ティンカリング | キャラクター {{W|9}} >",
            " Tinkering | Character " => " ティンカリング | キャラクター ",
            _ => source,
        };
    }

    public static string TranslateSkillsAndPowersRendered(string source)
    {
        var translated = TranslateToExit(TranslateFooter(source, "Tinkering", "ティンカリング", "Character", "キャラクター"));
        var buyMatch = SkillsAndPowersBuyPattern.Match(translated);
        if (!buyMatch.Success)
        {
            return translated;
        }

        return buyMatch.Groups["prefix"].Value + "購入" + buyMatch.Groups["suffix"].Value;
    }

    public static string TranslateEquipmentLiteral(string source)
    {
        return source switch
        {
            "< {{W|7}} Inventory | Factions {{W|9}} >" => "< {{W|7}} インベントリ | 派閥 {{W|9}} >",
            " Inventory | Factions " => " インベントリ | 派閥 ",
            _ => source,
        };
    }

    public static string TranslateEquipmentRendered(string source)
    {
        var translated = TranslateToExit(TranslateFooter(source, "Inventory", "インベントリ", "Factions", "派閥"));
        var setPrimaryLimbMatch = SetPrimaryLimbPattern.Match(translated);
        if (!setPrimaryLimbMatch.Success)
        {
            return translated;
        }

        return "[{{" + setPrimaryLimbMatch.Groups["color"].Value + "|" + setPrimaryLimbMatch.Groups["glyph"].Value + " - 主要部位を設定}}]";
    }

    private static string TranslateFooter(string source, string leftEnglish, string leftJapanese, string rightEnglish, string rightJapanese)
    {
        var match = Regex.Match(
            source,
            "^< \\{\\{W\\|(?<left>.+?)\\}\\} " + Regex.Escape(leftEnglish) + " \\| " + Regex.Escape(rightEnglish) + " \\{\\{W\\|(?<right>.+?)\\}\\} >$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return source;
        }

        return "< {{W|" + match.Groups["left"].Value + "}} " + leftJapanese + " | " + rightJapanese + " {{W|" + match.Groups["right"].Value + "}} >";
    }

    private static string TranslateToExit(string source)
    {
        var dualMatch = DualKeyToExitPattern.Match(source);
        if (dualMatch.Success)
        {
            return dualMatch.Groups["prefix"].Value + " 終了 ";
        }

        var singleMatch = SingleKeyToExitPattern.Match(source);
        return singleMatch.Success
            ? singleMatch.Groups["prefix"].Value + " 終了 "
            : source;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2249:Use 'string.Contains' instead of 'string.IndexOf'",
        Justification = "Ordinal comparison is required on net48.")]
    private static bool ContainsOrdinal(string source, string value)
    {
        return source.IndexOf(value, StringComparison.Ordinal) >= 0;
    }

    private static string ReplaceOrdinal(string source, string oldValue, string newValue)
    {
        var index = source.IndexOf(oldValue, StringComparison.Ordinal);
        return index < 0
            ? source
            : source.Substring(0, index)
              + newValue
              + source.Substring(index + oldValue.Length);
    }
}
