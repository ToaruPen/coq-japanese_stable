using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicsProcessTakeDamageTranslationPatch
{
    private const string Context = nameof(PhysicsProcessTakeDamageTranslationPatch);

    private static readonly Regex PlayerDamageFramePattern = new(
        "^You take (?:(?<amount>\\d+) (?<type>.+? damage|.+?)|(?<nodamage>no damage)) (?<tail>.+?)(?:(?<punct>[.!])(?<suffix>\\s+.+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerDamageFrameWithKnownTailPattern = new(
        "^You take (?:(?<amount>\\d+) (?<type>.+?)|(?<nodamage>no damage)) (?<tail>(?:(?:\\(x\\d+\\)\\s+)?(?:from|by|because of|due to|being run over by|while) .+?))(?:(?<punct>[.!])(?<suffix>\\s+.+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThirdPersonDamageFramePattern = new(
        "^(?:The |the |[Aa]n? )?(?<subject>.+?) takes? (?:(?<amount>\\d+) (?<type>.+? damage|.+?)|(?<nodamage>no damage)) (?<tail>.+?)(?:(?<punct>[.!])(?<suffix>\\s+.+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThirdPersonDamageFrameWithKnownTailPattern = new(
        "^(?:The |the |[Aa]n? )?(?<subject>.+?) takes? (?:(?<amount>\\d+) (?<type>.+?)|(?<nodamage>no damage)) (?<tail>(?:(?:\\(x\\d+\\)\\s+)?(?:from|by|because of|due to|being run over by|while) .+?))(?:(?<punct>[.!])(?<suffix>\\s+.+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TailMultiplierPrefixPattern = new(
        "^(?<prefix>(?:\\{\\{[^{}|]+\\|\\(x\\d+\\)\\}\\}|\\(x\\d+\\))\\s+)(?<tail>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SlammingIntoWallsTailPattern = new(
        "^from slamming into (?<count>.+?) walls$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChargeWallSlamTailPattern = new(
        "^from being slammed into a wall by (?<owner>.+?) charge$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChargeWallsSlamTailPattern = new(
        "^from being slammed into (?<count>.+?) walls by (?<owner>.+?) charge$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ElectricalShockSourcePattern = new(
        "^electrical shock delivered by (?<source>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RawMaterialsSourcePattern = new(
        "^using (?<material>.+?) as raw materials$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CumulativeTraumaSourcePattern = new(
        "^cumulative trauma of (?<owner>.+?) mental assault$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FireStartedSourcePattern = new(
        "^fire (?<owner>.+?) started$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FireStartedBySourcePattern = new(
        "^fire started by (?<owner>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FallingOnSourcePattern = new(
        "^(?<source>.+?) falling on (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FlyingIntoSourcePattern = new(
        "^(?<source>.+?) flying into (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ArmorSourcePattern = new(
        "^(?<owner>.+?の)(?:\\s+)?(?<kind>.+?) armor$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TerminalColorMarkupPattern = new(
        "^(?<prefix>.*?)\\{\\{(?<color>[^{}|]+)\\|(?<visible>[^{}]+)\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MarkedElectricalShockSourcePattern = new(
        "^\\{\\{(?<color>[^{}|]+)\\|electrical shock\\}\\} delivered by (?<source>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly (string Source, string Translation)[] DamageSourcePhraseTranslations =
    {
        ("failed assault on the structure of spacetime", "時空構造への干渉失敗"),
        ("electrical discharge", "放電"),
        ("charge", "突撃"),
        ("freezing effect", "凍結効果"),
        ("freezing weapon", "凍てつく武器"),
        ("flaming weapon", "火炎武器"),
        ("damage reflection", "ダメージ反射"),
        ("digestive enzymes", "消化酵素"),
        ("stunning force", "衝撃念力"),
        ("shield slam", "シールドスラム"),
        ("disintegration", "分解"),
        ("pyrokinesis", "熱念動"),
        ("cryokinesis", "冷気操作"),
        ("laser beam", "レーザービーム"),
        ("life drain", "生命吸収"),
        ("tiny spines", "小さな棘"),
        ("scalding steam", "灼熱の蒸気"),
        ("choking ash", "窒息性の灰"),
        ("plume of acid", "酸の噴煙"),
        ("jet of flames", "火炎噴流"),
        ("cryogenic mist", "極低温の霧"),
        ("falling rocks", "落石"),
        ("drinking asphalt", "アスファルトを飲んだこと"),
        ("drinking acid", "酸を飲んだこと"),
        ("drinking lava", "溶岩を飲んだこと"),
        ("hulk honey", "ハルク ハニー"),
        ("sharp edge", "鋭利な刃"),
        ("normality gas", "正常化ガス"),
        ("processor leak", "プロセッサ漏れ"),
        ("defoliant", "落葉剤"),
        ("fungicide", "殺真菌剤"),
        ("pummeling", "殴打"),
        ("explosion", "爆発"),
        ("projectile", "投射物"),
        ("impalement", "串刺し"),
        ("passage", "通過"),
        ("flames", "炎"),
        ("freeze", "凍結"),
        ("spores", "胞子"),
        ("thorns", "棘"),
        ("sitting", "座ったこと"),
        ("nosebleed", "鼻血"),
        ("hemorrhage", "出血"),
        ("plasma", "プラズマ"),
        ("attack", "攻撃"),
        ("carbide", "カーバイド"),
        ("fall", "落下"),
        ("acid", "酸"),
        ("cold", "冷気"),
        ("electric", "電撃"),
        ("electrical", "電撃"),
        ("fire", "熱"),
        ("heat", "熱"),
        ("mental", "精神"),
        ("poison", "毒"),
        ("sonic", "音波"),
        ("device", "装置"),
    };

    [ThreadStatic]
    private static int activeDepth;

    private static readonly object eventHasFlagMethodLock = new();

    private static Type? cachedEventHasFlagType;

    private static MethodInfo? cachedEventHasFlagMethod;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var physicsType = AccessTools.TypeByName("XRL.World.Parts.Physics");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (physicsType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(physicsType, "ProcessTakeDamage", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ProcessTakeDamage(Event) not found.", Context);
        }

        return method;
    }

    public static void Prefix(object? E, out int __state)
    {
        try
        {
            __state = activeDepth;
            if (!HasEventFlag(E, "NoDamageMessage"))
            {
                activeDepth++;
            }
        }
        catch (Exception ex)
        {
            __state = activeDepth;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception, int __state)
    {
        try
        {
            activeDepth = __state;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateDamageFrame(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "ProcessTakeDamage.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslateDamageFrame(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateDamageFrame(string source, out string translated)
    {
        var repositoryTranslated = MessagePatternTranslator.TranslateIfPatternMatches(source, Context);
        if (!string.Equals(repositoryTranslated, source, StringComparison.Ordinal))
        {
            translated = repositoryTranslated;
            return true;
        }

        var hasPlayerWrapper = TryStripPlayerDamageWrapper(source, out var visibleSource);
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(visibleSource);

        if (TryTranslatePattern(PlayerDamageFrameWithKnownTailPattern, stripped, spans, TranslatePlayerDamageFrame, out translated)
            || TryTranslatePattern(ThirdPersonDamageFrameWithKnownTailPattern, stripped, spans, TranslateThirdPersonDamageFrame, out translated)
            || TryTranslatePattern(PlayerDamageFramePattern, stripped, spans, TranslatePlayerDamageFrame, out translated)
            || TryTranslatePattern(ThirdPersonDamageFramePattern, stripped, spans, TranslateThirdPersonDamageFrame, out translated))
        {
            if (hasPlayerWrapper)
            {
                translated = "{{r|" + translated + "}}";
            }

            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        System.Collections.Generic.IReadOnlyList<ColorSpan> spans,
        DamageFrameTranslator translate,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            return false;
        }

        if (translate(match, spans, out translated))
        {
            return true;
        }

        translated = stripped;
        return false;
    }

    private delegate bool DamageFrameTranslator(
        Match match,
        System.Collections.Generic.IReadOnlyList<ColorSpan> spans,
        out string translated);

    private static bool TranslatePlayerDamageFrame(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, out string translated)
    {
        if (!TryTranslateTail(Restore(match, spans, "tail"), out var tail))
        {
            translated = string.Empty;
            return false;
        }

        translated = match.Groups["nodamage"].Success
            ? tail + "ダメージを受けなかった" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix")
            : tail + Restore(match, spans, "amount") + TranslateDamageType(Restore(match, spans, "type")) + "を受けた" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix");
        return true;
    }

    private static bool TranslateThirdPersonDamageFrame(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, out string translated)
    {
        var subject = StripLeadingArticle(Restore(match, spans, "subject"));
        if (!TryTranslateTail(Restore(match, spans, "tail"), out var tail))
        {
            translated = string.Empty;
            return false;
        }

        translated = match.Groups["nodamage"].Success
            ? subject + "は" + tail + "ダメージを受けなかった" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix")
            : subject + "は" + tail + Restore(match, spans, "amount") + TranslateDamageType(Restore(match, spans, "type")) + "を受けた" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix");
        return true;
    }

    private static bool TryStripPlayerDamageWrapper(string source, out string visibleSource)
    {
        const string prefix = "{{r|";
        if (source.StartsWith(prefix, StringComparison.Ordinal) && source.EndsWith("}}", StringComparison.Ordinal))
        {
            visibleSource = source.Substring(prefix.Length, source.Length - prefix.Length - 2);
            return true;
        }

        visibleSource = source;
        return false;
    }

    private static string Restore(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreRaw(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group);
    }

    private static bool TryTranslateTail(string tail, out string translated)
    {
        var prefixedTail = TailMultiplierPrefixPattern.Match(tail);
        if (prefixedTail.Success)
        {
            if (TryTranslateTail(prefixedTail.Groups["tail"].Value, out var translatedTail))
            {
                translated = prefixedTail.Groups["prefix"].Value + translatedTail;
                return true;
            }

            translated = string.Empty;
            return false;
        }

        if (TryTranslateSpecialTail(tail, out translated))
        {
            return true;
        }

        translated = tail switch
        {
            var value when value.StartsWith("from colliding with ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(20))) + "との衝突で",
            var value when value.StartsWith("from ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(5))) + "で",
            var value when value.StartsWith("by ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(3))) + "で",
            var value when value.StartsWith("because of ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(11))) + "により",
            var value when value.StartsWith("due to ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(7))) + "により",
            var value when value.StartsWith("being run over by ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(18))) + "に轢かれて",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslateSpecialTail(string tail, out string translated)
    {
        if (tail.Equals("from being crushed by a machine press", StringComparison.Ordinal))
        {
            translated = "機械プレスに押し潰されたことで";
            return true;
        }

        if (tail.Equals("from being forced into phase", StringComparison.Ordinal))
        {
            translated = "位相に押し込まれたことで";
            return true;
        }

        if (tail.Equals("from slamming into a wall", StringComparison.Ordinal))
        {
            translated = "壁に叩きつけられたことで";
            return true;
        }

        var slammingIntoWalls = SlammingIntoWallsTailPattern.Match(tail);
        if (slammingIntoWalls.Success)
        {
            var count = TranslateWallCount(slammingIntoWalls.Groups["count"].Value);
            translated = count + "枚の壁に叩きつけられたことで";
            return true;
        }

        var chargeWallSlam = ChargeWallSlamTailPattern.Match(tail);
        if (chargeWallSlam.Success)
        {
            translated = TranslateChargeOwner(chargeWallSlam.Groups["owner"].Value)
                + "突撃で壁に叩きつけられたことで";
            return true;
        }

        var chargeWallsSlam = ChargeWallsSlamTailPattern.Match(tail);
        if (chargeWallsSlam.Success)
        {
            var count = TranslateWallCount(chargeWallsSlam.Groups["count"].Value);
            translated = TranslateChargeOwner(chargeWallsSlam.Groups["owner"].Value)
                + "突撃で"
                + count
                + "枚の壁に叩きつけられたことで";
            return true;
        }

        if (tail.Equals("from scourging yourself", StringComparison.Ordinal))
        {
            translated = "自分を鞭打ったことで";
            return true;
        }

        translated = string.Empty;
        return false;
    }

    private static string TranslateWallCount(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => visible switch
            {
                "two" => "2",
                "three" => "3",
                "four" => "4",
                "five" => "5",
                "six" => "6",
                "seven" => "7",
                "eight" => "8",
                "nine" => "9",
                _ => visible,
            });
    }

    private static string TranslateChargeOwner(string owner)
    {
        return NormalizeVisiblePossessiveOwner(owner);
    }

    private static string TranslateDamageSource(string source)
    {
        source = NormalizeDamageSourceLabel(source);
        if (CirculatoryLossTermTranslator.TryTranslateTermPhrase(source, out var circulatoryLossTerm))
        {
            return circulatoryLossTerm;
        }

        if (TryTranslateKnownDamageSourcePhrase(source, out var knownDamageSource))
        {
            return knownDamageSource;
        }

        return source;
    }

    private static bool TryTranslateKnownDamageSourcePhrase(string source, out string translated)
    {
        if (TryTranslateMarkedDamageSourcePhrase(source, out translated))
        {
            return true;
        }

        var translatedSource = ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateVisibleDamageSourcePhrase);
        if (!string.Equals(translatedSource, source, StringComparison.Ordinal))
        {
            translated = translatedSource;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateMarkedDamageSourcePhrase(string source, out string translated)
    {
        if (source.Equals("{{G|hulk}} {{w|honey}}", StringComparison.Ordinal))
        {
            translated = "{{G|ハルク}} {{w|ハニー}}";
            return true;
        }

        var markedElectricalShock = MarkedElectricalShockSourcePattern.Match(source);
        if (markedElectricalShock.Success)
        {
            translated = NormalizeDamageSourceLabel(markedElectricalShock.Groups["source"].Value)
                + "からの{{"
                + markedElectricalShock.Groups["color"].Value
                + "|電気ショック}}";
            return true;
        }

        var marked = TerminalColorMarkupPattern.Match(source);
        if (!marked.Success
            || !TryTranslateExactVisibleDamageSourcePhrase(marked.Groups["visible"].Value, out var phraseTranslation))
        {
            translated = source;
            return false;
        }

        var prefix = marked.Groups["prefix"].Value.TrimEnd();
        translated = prefix + "{{" + marked.Groups["color"].Value + "|" + phraseTranslation + "}}";
        return true;
    }

    private static string TranslateVisibleDamageSourcePhrase(string source)
    {
        if (TryTranslateDynamicVisibleDamageSourcePhrase(source, out var dynamicTranslation)
            || TryTranslateOwnedVisibleDamageSourcePhrase(source, out dynamicTranslation)
            || TryTranslateExactVisibleDamageSourcePhrase(source, out dynamicTranslation))
        {
            return dynamicTranslation;
        }

        return source;
    }

    private static bool TryTranslateDynamicVisibleDamageSourcePhrase(string source, out string translated)
    {
        var electricalShock = ElectricalShockSourcePattern.Match(source);
        if (electricalShock.Success)
        {
            translated = NormalizeVisibleDamageSourceLabel(electricalShock.Groups["source"].Value) + "からの電気ショック";
            return true;
        }

        var rawMaterials = RawMaterialsSourcePattern.Match(source);
        if (rawMaterials.Success)
        {
            translated = TranslateRawMaterialSource(rawMaterials.Groups["material"].Value) + "を原材料にしたこと";
            return true;
        }

        var cumulativeTrauma = CumulativeTraumaSourcePattern.Match(source);
        if (cumulativeTrauma.Success)
        {
            translated = NormalizeVisiblePossessiveOwner(cumulativeTrauma.Groups["owner"].Value) + "精神攻撃による累積外傷";
            return true;
        }

        var fireStarted = FireStartedSourcePattern.Match(source);
        if (fireStarted.Success)
        {
            var owner = TranslateFireStarter(fireStarted.Groups["owner"].Value);
            translated = owner.Equals("あなた", StringComparison.Ordinal)
                ? "あなたが起こした火"
                : owner + "が起こした火";
            return true;
        }

        var fireStartedBy = FireStartedBySourcePattern.Match(source);
        if (fireStartedBy.Success)
        {
            translated = TranslateFireStarter(fireStartedBy.Groups["owner"].Value) + "が起こした火";
            return true;
        }

        var fallingOn = FallingOnSourcePattern.Match(source);
        if (fallingOn.Success)
        {
            translated = fallingOn.Groups["source"].Value + "が" + TranslateObjectPronoun(fallingOn.Groups["target"].Value) + "に落下したこと";
            return true;
        }

        var flyingInto = FlyingIntoSourcePattern.Match(source);
        if (flyingInto.Success)
        {
            translated = flyingInto.Groups["source"].Value + "が" + TranslateObjectPronoun(flyingInto.Groups["target"].Value) + "に飛び込んだこと";
            return true;
        }

        var armor = ArmorSourcePattern.Match(source);
        if (armor.Success)
        {
            var kind = armor.Groups["kind"].Value;
            if (!TryTranslateExactVisibleDamageSourcePhrase(kind, out var translatedKind))
            {
                translatedKind = kind;
            }

            translated = armor.Groups["owner"].Value + translatedKind + "装甲";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateRawMaterialSource(string source)
    {
        var normalized = NormalizeVisibleDamageSourceLabel(source);
        return normalized switch
        {
            "あなたのbody" => "あなたの体",
            "body" => "体",
            _ => normalized,
        };
    }

    private static bool TryTranslateOwnedVisibleDamageSourcePhrase(string source, out string translated)
    {
        for (var index = 0; index < DamageSourcePhraseTranslations.Length; index++)
        {
            var (phrase, translation) = DamageSourcePhraseTranslations[index];
            if (!source.EndsWith(phrase, StringComparison.Ordinal))
            {
                continue;
            }

            var owner = source.Substring(0, source.Length - phrase.Length).TrimEnd();
            if (owner.EndsWith("の", StringComparison.Ordinal))
            {
                translated = owner + translation;
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactVisibleDamageSourcePhrase(string source, out string translated)
    {
        if (source.Equals("its fall", StringComparison.Ordinal))
        {
            translated = "その落下";
            return true;
        }

        for (var index = 0; index < DamageSourcePhraseTranslations.Length; index++)
        {
            var (phrase, translation) = DamageSourcePhraseTranslations[index];
            if (source.Equals(phrase, StringComparison.Ordinal))
            {
                translated = translation;
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static string NormalizeDamageSourceLabel(string source)
    {
        if (source.StartsWith("your ", StringComparison.Ordinal)
            || source.StartsWith("Your ", StringComparison.Ordinal))
        {
            return "あなたの" + source.Substring(5);
        }

        var possessiveIndex = source.IndexOf("'s ", StringComparison.Ordinal);
        if (possessiveIndex >= 0)
        {
            return source.Substring(0, possessiveIndex) + "の " + source.Substring(possessiveIndex + 3);
        }

        return StripLeadingArticle(source);
    }

    private static string NormalizeVisibleDamageSourceLabel(string visible)
    {
        var normalized = StringHelpers.StripLeadingEnglishArticle(
            visible,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);

        if (normalized.StartsWith("your ", StringComparison.Ordinal)
            || normalized.StartsWith("Your ", StringComparison.Ordinal))
        {
            return "あなたの" + normalized.Substring(5);
        }

        var possessiveIndex = normalized.IndexOf("'s ", StringComparison.Ordinal);
        return possessiveIndex < 0
            ? normalized
            : normalized.Substring(0, possessiveIndex) + "の " + normalized.Substring(possessiveIndex + 3);
    }

    private static string NormalizeVisiblePossessiveOwner(string owner)
    {
        owner = owner.Trim();
        if (owner.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            owner = owner.Substring(3).TrimStart();
        }
        else if (owner.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            owner = owner.Substring(2).TrimStart();
        }

        if (owner.Equals("your", StringComparison.Ordinal) || owner.Equals("Your", StringComparison.Ordinal))
        {
            return "あなたの";
        }

        if (owner.EndsWith("'s", StringComparison.Ordinal))
        {
            return owner.Substring(0, owner.Length - 2) + "の";
        }

        return owner.EndsWith("の", StringComparison.Ordinal)
            ? owner
            : owner + "の";
    }

    private static string TranslateObjectPronoun(string target)
    {
        return target switch
        {
            "you" => "あなた",
            "You" => "あなた",
            "them" => "相手",
            "it" => "それ",
            _ => target,
        };
    }

    private static string TranslateFireStarter(string owner)
    {
        return StripLeadingArticle(owner.Trim()) switch
        {
            "you" => "あなた",
            "You" => "あなた",
            "itself" => "自身",
            "himself" => "自身",
            "herself" => "自身",
            "themself" => "自身",
            "themselves" => "自身",
            var value => value,
        };
    }

    private static string TranslateDamageType(string source)
    {
        var normalized = source.Trim();
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(normalized);
        if (!string.Equals(stripped, normalized, StringComparison.Ordinal))
        {
            return ColorAwareTranslationComposer.Restore(TranslateDamageType(stripped), spans);
        }

        return normalized switch
        {
            "damage" => "ダメージ",
            "acid" or "acid damage" => "酸ダメージ",
            "cold" or "cold damage" or "freezing" or "freezing damage" => "冷気ダメージ",
            "electric" or "electric damage" or "electrical" or "electrical damage" => "電撃ダメージ",
            "heat" or "heat damage" or "fire" or "fire damage" => "熱ダメージ",
            "mental" or "mental damage" => "精神ダメージ",
            "poison" or "poison damage" => "毒ダメージ",
            "sonic" or "sonic damage" => "音波ダメージ",
            _ when normalized.EndsWith(" damage", StringComparison.Ordinal) => normalized.Substring(0, normalized.Length - 7) + "ダメージ",
            _ => normalized + "ダメージ",
        };
    }

    private static string TranslatePunctuation(string punct)
    {
        if (string.IsNullOrEmpty(punct))
        {
            return string.Empty;
        }

        return punct == "!" ? "！" : "。";
    }

    private static string StripLeadingArticle(string source)
    {
        if (source.StartsWith("the ", StringComparison.Ordinal))
        {
            return source.Substring(4);
        }

        if (source.StartsWith("a ", StringComparison.Ordinal))
        {
            return source.Substring(2);
        }

        return source.StartsWith("an ", StringComparison.Ordinal)
            ? source.Substring(3)
            : source;
    }

    private static bool HasEventFlag(object? eventObject, string flag)
    {
        if (eventObject is null)
        {
            return false;
        }

        var method = GetEventHasFlagMethod(eventObject.GetType());
        return method?.Invoke(eventObject, new object[] { flag }) is true;
    }

    private static MethodInfo? GetEventHasFlagMethod(Type eventType)
    {
        lock (eventHasFlagMethodLock)
        {
            if (cachedEventHasFlagType != eventType)
            {
                cachedEventHasFlagMethod = eventType.GetMethod("HasFlag", new[] { typeof(string) });
                cachedEventHasFlagType = eventType;
            }

            return cachedEventHasFlagMethod;
        }
    }
}
