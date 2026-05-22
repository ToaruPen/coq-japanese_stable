using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CherubimSpawnerBestowElementTranslationPatch
{
    private const string TargetTypeName = "XRL.World.Parts.CherubimSpawner";
    private const string GameObjectTypeName = "XRL.World.GameObject";
    private const string Context = nameof(CherubimSpawnerBestowElementTranslationPatch);
    private const string DisplayNameFamily = "CherubimSpawner.BestowElement.DisplayName";
    private const string RulesFamily = "CherubimSpawner.BestowElement.RulesDescription";

    private static readonly IReadOnlyDictionary<string, ElementTranslation> ElementTranslations =
        new Dictionary<string, ElementTranslation>(StringComparer.Ordinal)
        {
            ["glass"] = new(
                "glass",
                "ガラスの",
                "\nThis creature belongs to the caste of glass cherubim.\n• Attacks have a 10% chance to dismember.\n• Reflects 25% damage back at attackers.",
                "\nこのクリーチャーはガラスの智天使の階級に属している。\n• 攻撃は10%の確率で四肢を切断する。\n• 攻撃者に受けたダメージの25%を反射する。"),
            ["jewels"] = new(
                "jeweled",
                "宝石の",
                "\nThis creature belongs to the caste of jeweled cherubim.\n• +10 Ego.\n• Attacks have a small chance to transmute opponents into gemstones.",
                "\nこのクリーチャーは宝石の智天使の階級に属している。\n• 自我 +10。\n• 攻撃は低確率で敵を宝石へ変成する。"),
            ["stars"] = new(
                "star",
                "星の",
                "\nThis creature belongs to the caste of star cherubim.\n• Light Manipulation 10",
                "\nこのクリーチャーは星の智天使の階級に属している。\n• 光操作 10"),
            ["time"] = new(
                "time",
                "時の",
                "\nThis creature belongs to the caste of time cherubim.\n• Temporal Fugue 10",
                "\nこのクリーチャーは時の智天使の階級に属している。\n• 時間遁走 10"),
            ["salt"] = new(
                "salt",
                "塩の",
                "\nThis creature belongs to the caste of salt cherubim.\n• +10 Willpower\n• +100% HP",
                "\nこのクリーチャーは塩の智天使の階級に属している。\n• 意志力 +10\n• HP +100%"),
            ["ice"] = new(
                "ice",
                "氷の",
                "\nThis creature belongs to the caste of ice cherubim.\n• +100 Cold Resist\n• Ice Breath 10",
                "\nこのクリーチャーは氷の智天使の階級に属している。\n• 冷気耐性 +100\n• 氷の吐息 10"),
            ["scholarship"] = new(
                "learned",
                "博識の",
                "\nThis creature belongs to the caste of learned cherubim.\n• +10 Intelligence\n• Attacks discharge clockwork beetles.",
                "\nこのクリーチャーは博識の智天使の階級に属している。\n• 知性 +10\n• 攻撃は機械仕掛けの甲虫を放出する。"),
            ["might"] = new(
                "mighty",
                "剛力の",
                "\nThis creature belongs to the caste of mighty cherubim.\n• +20 Strength",
                "\nこのクリーチャーは剛力の智天使の階級に属している。\n• 筋力 +20"),
            ["chance"] = new(
                "chaotic",
                "混沌の",
                "\nThis creature belongs to the caste of chaotic cherubim.\n• Whenever this creature is about to take damage, there's a 25% chance they blink away instead.\n• Whenever this creature attacks, 50% of the time the Fates have their way.",
                "\nこのクリーチャーは混沌の智天使の階級に属している。\n• このクリーチャーがダメージを受けようとすると、25%の確率で代わりに瞬間移動する。\n• このクリーチャーが攻撃するとき、50%の確率で運命が成り行きを決める。"),
            ["circuitry"] = new(
                "electric",
                "電撃の",
                "\nThis creature belongs to the caste of electric cherubim.\n• +100 Electrical Resist\n• Electrical Generation 10",
                "\nこのクリーチャーは電撃の智天使の階級に属している。\n• 電撃耐性 +100\n• 発電 10"),
            ["travel"] = new(
                "quickened",
                "加速した",
                "\nThis creature belongs to the caste of quickened cherubim.\n• +5 Quickness\n• Teleportation 10",
                "\nこのクリーチャーは加速した智天使の階級に属している。\n• クイックネス +5\n• テレポーテーション 10"),
        };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var gameObjectType = AccessTools.TypeByName(GameObjectTypeName);
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: CherubimSpawnerBestowElementTranslationPatch failed to resolve CherubimSpawner or GameObject.");
            return null;
        }

        var method = AccessTools.Method(targetType, "BestowElement", [gameObjectType, typeof(string), typeof(bool)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: CherubimSpawnerBestowElementTranslationPatch.BestowElement(GameObject,string,bool) not found.");
        }

        return method;
    }

    public static void Prefix(object? __0, out CherubimSpawnerRulesDescriptionState __state)
    {
        try
        {
            __state = new CherubimSpawnerRulesDescriptionState(GetRulesDescriptionParts(__0).Count);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CherubimSpawnerBestowElementTranslationPatch.Prefix failed: {0}", ex);
            __state = new CherubimSpawnerRulesDescriptionState(0);
        }
    }

    public static void Postfix(object? __0, string __1, bool __2, CherubimSpawnerRulesDescriptionState __state)
    {
        try
        {
            if (__0 is null || !ElementTranslations.TryGetValue(__1, out var translation))
            {
                return;
            }

            if (__2)
            {
                TranslateDisplayName(__0, translation);
            }

            TranslateAddedRulesDescriptions(__0, translation, __state.Count);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CherubimSpawnerBestowElementTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateDisplayName(object gameObject, ElementTranslation translation)
    {
        var render = DescriptionPartReflectionHelpers.GetMemberValue(gameObject, "Render");
        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(render, "DisplayName");
        var sourcePrefix = translation.SourceDisplayPrefix + " ";
        if (render is null || source is null || !source.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var translated = translation.JapaneseDisplayPrefix + source.Substring(sourcePrefix.Length);
        if (DescriptionPartReflectionHelpers.SetStringMemberValue(render, "DisplayName", translated))
        {
            TryResetNameCache(gameObject);
            DynamicTextObservability.RecordTransform(Context, DisplayNameFamily, source, translated);
        }
    }

    private static void TranslateAddedRulesDescriptions(object gameObject, ElementTranslation translation, int existingCount)
    {
        var parts = GetRulesDescriptionParts(gameObject);
        for (var index = Math.Max(existingCount, 0); index < parts.Count; index++)
        {
            TranslateRulesDescription(parts[index], translation);
        }
    }

    private static void TranslateRulesDescription(object part, ElementTranslation translation)
    {
        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(part, "Text");
        if (!string.Equals(source, translation.SourceRulesText, StringComparison.Ordinal))
        {
            return;
        }

        if (DescriptionPartReflectionHelpers.SetStringMemberValue(part, "Text", translation.JapaneseRulesText))
        {
            DynamicTextObservability.RecordTransform(Context, RulesFamily, source, translation.JapaneseRulesText);
        }
    }

    private static List<object> GetRulesDescriptionParts(object? gameObject)
    {
        var parts = new List<object>();
        if (DescriptionPartReflectionHelpers.GetMemberValue(gameObject, "PartsList") is not IEnumerable partsList)
        {
            return parts;
        }

        foreach (var part in partsList)
        {
            if (part is not null && string.Equals(part.GetType().Name, "RulesDescription", StringComparison.Ordinal))
            {
                parts.Add(part);
            }
        }

        return parts;
    }

    private static void TryResetNameCache(object gameObject)
    {
        AccessTools.Method(gameObject.GetType(), "ResetNameCache", Type.EmptyTypes)?.Invoke(gameObject, null);
    }

    private readonly record struct ElementTranslation(
        string SourceDisplayPrefix,
        string JapaneseDisplayPrefix,
        string SourceRulesText,
        string JapaneseRulesText);
}

public readonly record struct CherubimSpawnerRulesDescriptionState(int Count);
