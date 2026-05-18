using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class TombstoneDeathCauseTranslator
{
    private static readonly IReadOnlyDictionary<string, string> ExactFrames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Tricked into jumping into a pool of lava"] = "溶岩の池へ飛び込むようだまされて死亡した",
            ["Drowned in a lake"] = "湖で溺死した",
            ["Thrown into a pool of acid"] = "酸の池へ投げ込まれて死亡した",
            ["Died of old age"] = "老衰で死亡した",
            ["Died of natural causes"] = "自然死した",
            ["Succumbed to glotrot."] = "グロットロットに倒れた。",
        };

    private static readonly IReadOnlyDictionary<string, string> CauseKinds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["natural"] = "自然",
            ["unnatural"] = "不自然",
            ["mysterious"] = "謎",
            ["metaphysical"] = "形而上",
            ["mathematical"] = "数学的",
            ["chemical"] = "化学的",
            ["ceremonial"] = "儀礼的",
            ["unknown"] = "不明",
            ["magmatic"] = "マグマ性",
        };

    private static readonly IReadOnlyList<PrefixFrame> PrefixFrames =
    [
        new("Stabbed to death by ", target => target + "に刺殺された"),
        new("Shanked by ", target => target + "にナイフで刺殺された"),
        new("Gunned down by ", target => target + "に撃ち倒された"),
        new("Poisoned by ", target => target + "に毒殺された"),
        new("Pushed off a cliff by ", target => target + "に崖から突き落とされた"),
        new("Killed in a duel over ", target => target + "をめぐる決闘で死亡した"),
        new("Succumbed to despair after losing ", target => target + "を失った絶望で死亡した"),
        new("Crushed to death by a falling ", target => "落下してきた" + target + "に押し潰された"),
        new("Fell from a cliff trying to recover a lost ", target => "失くした" + target + "を取り戻そうとして崖から落ちた"),
        new("Murdered under mysterious circumstances by ", target => target + "に謎めいた状況で殺害された"),
        new("Eaten alive by ", target => target + "に生きたまま食べられた"),
        new("Sacrificed by ", target => target + "に生贄にされた"),
        new("Assassinated after disparaging ", target => target + "を侮辱した後に暗殺された"),
        new("Killed after cooking a rancid meal for ", target => target + "に腐った食事を作って殺された"),
        new("Burned at the stake by ", target => target + "に火刑にされた"),
        new("Thrown from a cliff by ", target => target + "に崖から投げ落とされた"),
        new("Buried alive by ", target => target + "に生き埋めにされた"),
        new("Brained in retaliation for stealing from ", target => target + "から盗んだ報復で頭を砕かれた"),
        new("Shanked in retaliation for stealing from ", target => target + "から盗んだ報復でナイフで刺された"),
        new("Shot in retaliation for stealing from ", target => target + "から盗んだ報復で撃たれた"),
        new("Cooked for sustenance by ", target => target + "に食料として調理された"),
        new("Mummified by ", target => target + "にミイラにされた"),
        new("Chopped into small pieces by ", target => target + "に細切れにされた"),
        new("Covered in molten wax by ", target => target + "に溶けた蝋をかけられた"),
        new("Drawn and quartered by ", target => target + "に八つ裂きにされた"),
        new("Choked on ", target => target + "を喉に詰まらせた"),
        new("Ate too much ", target => target + "を食べ過ぎた"),
        new("Died of malnutrition after eating only ", target => target + "だけを食べ続けて栄養失調で死亡した"),
        new("Swallowed ", target => target + "を飲み込んだ"),
        new("Overdosed on ", target => target + "を過剰摂取した"),
        new("Accidentally ingested ", target => target + "を誤飲した"),
        new("Choked to death on ", target => target + "で窒息死した"),
        new("Suffocated in ", target => target + "の中で窒息した"),
        new("Breathed too much ", target => target + "を吸い過ぎた"),
        new("Released a canister of ", target => target + "のキャニスターを放出した"),
        new("Sat on ", target => target + "の上に座った"),
        new("Forgot about ", target => target + "のことを忘れていた"),
        new("Knocked over ", target => target + "を倒した"),
        new("Knocked over the head with a copy of ", target => target + "の一冊で頭を殴られた"),
        new("Burned at the stake for promulgating ", target => target + "を広めたため火刑にされた"),
        new("Burned to death by ", target => target + "に焼き殺された"),
        new("Immolated in ", target => target + "で焼死した"),
        new("Engulfed in flame by ", target => target + "の炎に包まれた"),
        new("Fell asleep on ", target => target + "の上で眠り込んだ"),
        new("Drank from a poisoned ", target => "毒入りの" + target + "から飲んだ"),
        new("Made too many mocking sounds at ", target => target + "をからかう声を出し過ぎた"),
    ];

    private static readonly Regex InjectedTooManyPattern = new(
        "^Injected one (?<target>.+) too many$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ReleasedLockedRoomPattern = new(
        "^Released a canister of (?<target>.+) in a locked room$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ObsessedPattern = new(
        "^Became obsessed with (?<target>.+) and forgot to eat$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CauseKindPattern = new(
        "^Died of (?<kind>[a-z]+) causes$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        var normalized = stripped.TrimStart();
        if (TryTranslateCore(normalized, spans, out var translatedCore))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translatedCore,
                spans,
                stripped.Length,
                original);
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslateCore(string source, IReadOnlyList<ColorSpan> spans, out string translated)
    {
        if (ExactFrames.TryGetValue(source, out translated!))
        {
            return true;
        }

        var match = CauseKindPattern.Match(source);
        if (match.Success && CauseKinds.TryGetValue(match.Groups["kind"].Value, out var causeKind))
        {
            translated = causeKind + "の原因で死亡した";
            return true;
        }

        if (TryTranslateRegexCapture(InjectedTooManyPattern, source, spans, target => target + "を一本多く注射し過ぎた", out translated)
            || TryTranslateRegexCapture(ReleasedLockedRoomPattern, source, spans, target => "鍵のかかった部屋で" + target + "のキャニスターを放出した", out translated)
            || TryTranslateRegexCapture(ObsessedPattern, source, spans, target => target + "に取りつかれて食事を忘れた", out translated))
        {
            return true;
        }

        for (var index = 0; index < PrefixFrames.Count; index++)
        {
            var frame = PrefixFrames[index];
            if (!source.StartsWith(frame.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var target = RestoreCapture(source, frame.Prefix.Length, source.Length - frame.Prefix.Length, spans);
            translated = frame.Build(target);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateRegexCapture(
        Regex pattern,
        string source,
        IReadOnlyList<ColorSpan> spans,
        Func<string, string> build,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = build(RestoreCapture(match.Groups["target"], spans));
        return true;
    }

    private static string RestoreCapture(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateEntityReference(
            ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim());
    }

    private static string RestoreCapture(string source, int index, int length, IReadOnlyList<ColorSpan> spans)
    {
        var target = source.Substring(index, length).Trim();
        if (spans.Count == 0)
        {
            return TranslateEntityReference(target);
        }

        var match = Regex.Match(source, "^" + Regex.Escape(source.Substring(0, index)) + "(?<target>.+)$", RegexOptions.Singleline);
        if (!match.Success)
        {
            return TranslateEntityReference(target);
        }

        return RestoreCapture(match.Groups["target"], spans);
    }

    private static string TranslateEntityReference(string source)
    {
        try
        {
            return DeathWrapperFamilyTranslator.TranslateEntityReference(source, nameof(TombstoneDeathCauseTranslationPatch));
        }
        catch (System.IO.DirectoryNotFoundException)
        {
            return StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
        }
    }

    private readonly struct PrefixFrame
    {
        public PrefixFrame(string prefix, Func<string, string> build)
        {
            Prefix = prefix;
            Build = build;
        }

        public string Prefix { get; }

        public Func<string, string> Build { get; }
    }
}
