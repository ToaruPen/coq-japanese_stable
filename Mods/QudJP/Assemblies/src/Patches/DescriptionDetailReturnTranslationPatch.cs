using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

internal enum DescriptionDetailReturnKind
{
    Unknown,
    CyberneticsChoiceDescription,
    CyberneticsChoiceLongDescription,
    TinkerDataDescription,
    GameObjectUnitDescription,
}

[HarmonyPatch]
public static class DescriptionDetailReturnTranslationPatch
{
    internal const string Context = nameof(DescriptionDetailReturnTranslationPatch);
    internal const string Family = "DescriptionDetail.Return";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var cyberneticsChoiceType = AccessTools.TypeByName("XRL.CharacterBuilds.Qud.QudCyberneticsModule+CyberneticsChoice");
        if (cyberneticsChoiceType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve CyberneticsChoice.", Context);
        }
        else
        {
            foreach (var methodName in new[] { "GetDescription", "GetLongDescription" })
            {
                var method = AccessTools.Method(cyberneticsChoiceType, methodName, Type.EmptyTypes);
                if (method is not null)
                {
                    yield return method;
                }
                else
                {
                    Trace.TraceError("QudJP: {0} failed to resolve CyberneticsChoice.{1}().", Context, methodName);
                }
            }
        }

        var tinkerDataType = AccessTools.TypeByName("XRL.World.Tinkering.TinkerData");
        if (tinkerDataType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve TinkerData.", Context);
        }
        else
        {
            foreach (var propertyName in new[] { "UnclippedDescription", "Description" })
            {
                var method = AccessTools.PropertyGetter(tinkerDataType, propertyName);
                if (method is not null)
                {
                    yield return method;
                }
                else
                {
                    Trace.TraceError("QudJP: {0} failed to resolve TinkerData.{1} getter.", Context, propertyName);
                }
            }
        }

        foreach (var typeName in new[]
        {
            "XRL.World.Units.GameObjectCyberneticsUnit",
            "XRL.World.Units.GameObjectSkillUnit",
            "XRL.World.Units.GameObjectRelicUnit",
            "XRL.World.Units.GameObjectGolemQuestRandomUnit",
            "XRL.World.Units.GameObjectMetachromeUnit",
            "XRL.World.Units.GameObjectBodyPartUnit",
            "XRL.World.Units.GameObjectExperienceUnit",
            "XRL.World.Units.GameObjectMutationUnit",
            "XRL.World.Units.GameObjectAttributeUnit",
            "XRL.World.Units.GameObjectPartUnit",
            "XRL.World.Units.GameObjectPlaceholderUnit",
            "XRL.World.Units.GameObjectSaveModifierUnit",
            "XRL.World.Units.GameObjectTieredArmorUnit",
            "XRL.World.Units.GameObjectBaetylUnit",
            "XRL.World.Units.GameObjectCloneUnit",
            "XRL.World.Units.GameObjectReputationUnit",
            "XRL.World.Units.GameObjectSecretUnit",
            "XRL.World.Units.GameObjectUnit",
            "XRL.World.Units.GameObjectUnitAggregate",
        })
        {
            var targetType = AccessTools.TypeByName(typeName);
            var method = targetType is null ? null : AccessTools.Method(targetType, "GetDescription", new[] { typeof(bool) });
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.GetDescription(bool).", Context, typeName);
            }
        }
    }

    public static void Postfix(ref string __result, MethodBase __originalMethod)
    {
        try
        {
            var kind = ResolveKind(__originalMethod);
            if (!DescriptionDetailReturnTranslator.TryTranslate(__result, kind, out var translated, out var detail))
            {
                return;
            }

            if (detail.Length > 0)
            {
                DynamicTextObservability.RecordTransform(Context, Family + "." + detail, __result, translated);
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static DescriptionDetailReturnKind ResolveKind(MethodBase originalMethod)
    {
        string declaringTypeName;
        if (originalMethod.DeclaringType is null)
        {
            Trace.TraceWarning("QudJP: {0} original method has no declaring type: {1}.", Context, originalMethod.Name);
            declaringTypeName = string.Empty;
        }
        else
        {
            var fullName = originalMethod.DeclaringType.FullName;
            if (fullName is null)
            {
                Trace.TraceWarning("QudJP: {0} original method declaring type has no full name: {1}.", Context, originalMethod.Name);
                declaringTypeName = string.Empty;
            }
            else
            {
                declaringTypeName = fullName;
            }
        }
        if (declaringTypeName.StartsWith("XRL.World.Units.GameObject", StringComparison.Ordinal)
            || (originalMethod.Name.StartsWith("GameObject", StringComparison.Ordinal)
                && originalMethod.Name.EndsWith("GetDescription", StringComparison.Ordinal)))
        {
            return DescriptionDetailReturnKind.GameObjectUnitDescription;
        }

        return originalMethod.Name switch
        {
            "GetDescription" or "CyberneticsChoiceGetDescription" => DescriptionDetailReturnKind.CyberneticsChoiceDescription,
            "GetLongDescription" or "CyberneticsChoiceGetLongDescription" => DescriptionDetailReturnKind.CyberneticsChoiceLongDescription,
            "get_UnclippedDescription" or "TinkerDataGetUnclippedDescription" => DescriptionDetailReturnKind.TinkerDataDescription,
            "get_Description" or "TinkerDataGetDescription" => DescriptionDetailReturnKind.TinkerDataDescription,
            _ => DescriptionDetailReturnKind.Unknown,
        };
    }
}

internal static class DescriptionDetailReturnTranslator
{
    private static readonly Regex RulesWrapperPattern = new(
        "^\\{\\{rules\\|(?<body>.*)\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkerBatchPattern = new(
        "^\\{\\{rules\\|Makes a batch of (?<count>.+?)\\.\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasEverySkillPattern = new(
        "^Has every (?<skill>.+) skill$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasSkillPattern = new(
        "^Has the (?<skill>.+) skill$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RelicTierPattern = new(
        "^Spawns with a (?<tier>low|mid|high)-tier relic$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RandomEffectsPattern = new(
        "^(?<count>\\d+) random effects?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EquippedWithPattern = new(
        "^Equipped with (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExtraSlotPattern = new(
        "^Extra (?<slot>.+) slot$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LevelsPattern = new(
        "^\\+(?<count>\\d+) levels$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExperiencePattern = new(
        "^\\+(?<amount>\\d+) experience$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MutationLevelPattern = new(
        "^(?<mutation>.+) at level (?<level>\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttributeAllStatsPattern = new(
        "^(?<amount>[+-]\\d+%?) to all stats$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttributeStatPattern = new(
        "^(?<amount>[+-]\\d+%?) (?<stat>Strength|Agility|Toughness|Intelligence|Willpower|Ego|AV|DV|MA)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BaetylRewardsPattern = new(
        "^Spawns with (?<count>.+?) random baetyl rewards?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReputationPattern = new(
        "^(?<amount>[+-]\\d+) reputation with (?<faction>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SecretsPattern = new(
        "^Reveals (?<count>\\d+) secrets on creation$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TieredArmorPattern = new(
        "^Spawns with (?<count>\\d+) random pieces of (?<gigantic>gigantic, )?(?<tier>low|mid|high|low-to-mid|mid-to-high|low-to-high) tier armor$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(
        string source,
        DescriptionDetailReturnKind kind,
        out string translated,
        out string detail)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            detail = string.Empty;
            return true;
        }

        if (kind == DescriptionDetailReturnKind.Unknown)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = kind switch
        {
            DescriptionDetailReturnKind.CyberneticsChoiceDescription => ChargenStructuredTextTranslator.Translate(source),
            DescriptionDetailReturnKind.CyberneticsChoiceLongDescription => TranslateCyberneticsChoiceLongDescription(source),
            DescriptionDetailReturnKind.TinkerDataDescription => TranslateTinkerDataDescription(source),
            DescriptionDetailReturnKind.GameObjectUnitDescription => TranslateGameObjectUnitDescription(source),
            _ => source,
        };

        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            detail = string.Empty;
            return false;
        }

        detail = kind switch
        {
            DescriptionDetailReturnKind.CyberneticsChoiceDescription => "CyberneticsChoiceDescription",
            DescriptionDetailReturnKind.CyberneticsChoiceLongDescription => "CyberneticsChoiceLongDescription",
            DescriptionDetailReturnKind.TinkerDataDescription => "TinkerDataDescription",
            DescriptionDetailReturnKind.GameObjectUnitDescription => "GameObjectUnitDescription",
            _ => string.Empty,
        };
        return detail.Length > 0;
    }

    private static string TranslateCyberneticsChoiceLongDescription(string source)
    {
        if (string.Equals(source, "{{C|-2 License Tier\n+1 Toughness}}", StringComparison.Ordinal))
        {
            return "{{C|-2 ライセンスティア\n+1 頑健}}";
        }

        return TranslateLines(source, TranslateCyberneticsChoiceLongDescriptionLine);
    }

    private static string TranslateTinkerDataDescription(string source)
    {
        return TranslateLines(source, TranslateTinkerDataDescriptionLine);
    }

    private static string TranslateGameObjectUnitDescription(string source)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact)
            && !string.Equals(exact, source, StringComparison.Ordinal))
        {
            return exact;
        }

        if (string.Equals(source, "Cybernetic implant installed", StringComparison.Ordinal))
        {
            return "サイバネティック・インプラント装着済み";
        }

        if (string.Equals(source, "Spawns with a copy in a nearby cell", StringComparison.Ordinal))
        {
            return "近くのセルにコピー1体を伴って出現";
        }

        var hasEverySkillMatch = HasEverySkillPattern.Match(source);
        if (hasEverySkillMatch.Success)
        {
            return TranslateGameObjectUnitTerm(hasEverySkillMatch.Groups["skill"].Value) + "の全スキルを所持";
        }

        var hasSkillMatch = HasSkillPattern.Match(source);
        if (hasSkillMatch.Success)
        {
            return TranslateGameObjectUnitTerm(hasSkillMatch.Groups["skill"].Value) + "スキルを所持";
        }

        var relicTierMatch = RelicTierPattern.Match(source);
        if (relicTierMatch.Success)
        {
            return TranslateRelicTier(relicTierMatch.Groups["tier"].Value) + "ティアの聖遺物を所持して出現";
        }

        var randomEffectsMatch = RandomEffectsPattern.Match(source);
        if (randomEffectsMatch.Success)
        {
            return "ランダム効果" + randomEffectsMatch.Groups["count"].Value + "個";
        }

        var equippedWithMatch = EquippedWithPattern.Match(source);
        if (equippedWithMatch.Success)
        {
            return TranslateGameObjectUnitTerm(equippedWithMatch.Groups["item"].Value) + "を装備";
        }

        var extraSlotMatch = ExtraSlotPattern.Match(source);
        if (extraSlotMatch.Success)
        {
            return TranslateGameObjectUnitTerm(extraSlotMatch.Groups["slot"].Value) + "スロットを追加";
        }

        var levelsMatch = LevelsPattern.Match(source);
        if (levelsMatch.Success)
        {
            return "レベル+" + levelsMatch.Groups["count"].Value;
        }

        var experienceMatch = ExperiencePattern.Match(source);
        if (experienceMatch.Success)
        {
            return "経験値+" + experienceMatch.Groups["amount"].Value;
        }

        var mutationLevelMatch = MutationLevelPattern.Match(source);
        if (mutationLevelMatch.Success)
        {
            return TranslateGameObjectUnitTerm(mutationLevelMatch.Groups["mutation"].Value)
                + "（レベル"
                + mutationLevelMatch.Groups["level"].Value
                + "）";
        }

        var attributeAllStatsMatch = AttributeAllStatsPattern.Match(source);
        if (attributeAllStatsMatch.Success)
        {
            return "全能力値" + attributeAllStatsMatch.Groups["amount"].Value;
        }

        var attributeStatMatch = AttributeStatPattern.Match(source);
        if (attributeStatMatch.Success)
        {
            return TranslateGameObjectUnitTerm(attributeStatMatch.Groups["stat"].Value)
                + attributeStatMatch.Groups["amount"].Value;
        }

        var tieredArmorMatch = TieredArmorPattern.Match(source);
        if (tieredArmorMatch.Success)
        {
            var gigantic = tieredArmorMatch.Groups["gigantic"].Success ? "巨大な" : string.Empty;
            return gigantic
                + TranslateRelicTier(tieredArmorMatch.Groups["tier"].Value)
                + "ティアのランダムな防具"
                + tieredArmorMatch.Groups["count"].Value
                + "個を所持して出現";
        }

        var baetylRewardsMatch = BaetylRewardsPattern.Match(source);
        if (baetylRewardsMatch.Success)
        {
            return "ランダムなベイティル報酬" + baetylRewardsMatch.Groups["count"].Value + "個を所持して出現";
        }

        var reputationMatch = ReputationPattern.Match(source);
        if (reputationMatch.Success)
        {
            return TranslateGameObjectUnitTerm(reputationMatch.Groups["faction"].Value) + "との評判" + reputationMatch.Groups["amount"].Value;
        }

        var secretsMatch = SecretsPattern.Match(source);
        if (secretsMatch.Success)
        {
            return "生成時に秘密" + secretsMatch.Groups["count"].Value + "件を明かす";
        }

        return source;
    }

    private static string TranslateLines(string source, Func<string, string> translateLine)
    {
        var newline = source.Contains("\r\n") ? "\r\n" : "\n";
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var translatedLine = translateLine(lines[index]);
            if (string.Equals(translatedLine, lines[index], StringComparison.Ordinal))
            {
                continue;
            }

            lines[index] = translatedLine;
            changed = true;
        }

        return changed ? string.Join(newline, lines) : source;
    }

    private static string TranslateCyberneticsChoiceLongDescriptionLine(string source)
    {
        if (TryTranslateRulesWrappedCyberneticsBehavior(source, out var rulesTranslated))
        {
            return rulesTranslated;
        }

        var translated = TranslateDescriptionLine(source);
        return string.Equals(translated, source, StringComparison.Ordinal)
            ? ChargenStructuredTextTranslator.Translate(source)
            : translated;
    }

    private static string TranslateTinkerDataDescriptionLine(string source)
    {
        var batchMatch = TinkerBatchPattern.Match(source);
        if (batchMatch.Success)
        {
            return "{{rules|一度に" + TranslateBatchCount(batchMatch.Groups["count"].Value) + "個作成する。}}";
        }

        return TranslateDescriptionLine(source);
    }

    private static string TranslateDescriptionLine(string source)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exact)
            && !string.Equals(exact, source, StringComparison.Ordinal))
        {
            return exact;
        }

        return DescriptionTextTranslator.TranslateLongDescription(
            source,
            DescriptionDetailReturnTranslationPatch.Context);
    }

    private static bool TryTranslateRulesWrappedCyberneticsBehavior(string source, out string translated)
    {
        var match = RulesWrapperPattern.Match(source);
        if (!match.Success
            || !CyberneticsBehaviorDescriptionTranslationPatch.TryTranslate(match.Groups["body"].Value, out var bodyTranslated))
        {
            translated = source;
            return false;
        }

        translated = "{{rules|" + bodyTranslated + "}}";
        return true;
    }

    private static string TranslateBatchCount(string source)
    {
        return source switch
        {
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            "eleven" => "11",
            "twelve" => "12",
            "thirteen" => "13",
            "fourteen" => "14",
            "fifteen" => "15",
            "sixteen" => "16",
            "seventeen" => "17",
            "eighteen" => "18",
            "nineteen" => "19",
            "twenty" => "20",
            _ => source,
        };
    }

    private static string TranslateRelicTier(string source)
    {
        return source switch
        {
            "low" => "低",
            "mid" => "中",
            "high" => "高",
            "low-to-mid" => "低-中",
            "mid-to-high" => "中-高",
            "low-to-high" => "低-高",
            _ => source,
        };
    }

    private static string TranslateGameObjectUnitTerm(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var translated)
                ? translated
                : visible);
    }
}
