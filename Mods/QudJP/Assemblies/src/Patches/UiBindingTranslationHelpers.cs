using System;
using System.Collections.Generic;
using HarmonyLib;

namespace QudJP.Patches;

internal static class UiBindingTranslationHelpers
{
    private static readonly IReadOnlyDictionary<string, string> CommandCategoryLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Ability Bar"] = "アビリティバー",
            ["ABILITY BAR"] = "アビリティバー",
            ["Advanced Adventuring"] = "高度な冒険操作",
            ["ADVANCED ADVENTURING"] = "高度な冒険操作",
            ["Adventuring"] = "冒険操作",
            ["ADVENTURING"] = "冒険操作",
            ["Basic Move / Attack"] = "基本移動／攻撃",
            ["BASIC MOVE / ATTACK"] = "基本移動／攻撃",
            ["Character Creation"] = "キャラクター作成",
            ["CHARACTER CREATION"] = "キャラクター作成",
            ["Character Sheet"] = "キャラクターシート",
            ["CHARACTER SHEET"] = "キャラクターシート",
            ["Debug"] = "デバッグ",
            ["DEBUG"] = "デバッグ",
            ["Menus"] = "メニュー操作",
            ["MENUS"] = "メニュー操作",
            ["Mouse-specific"] = "マウス操作",
            ["MOUSE-SPECIFIC"] = "マウス操作",
            ["Shortcuts to Character Sheet"] = "キャラクターシートショートカット",
            ["SHORTCUTS TO CHARACTER SHEET"] = "キャラクターシートショートカット",
            ["System"] = "システム",
            ["SYSTEM"] = "システム",
            ["Targeting"] = "ターゲティング",
            ["TARGETING"] = "ターゲティング",
            ["Trade"] = "取引",
            ["TRADE"] = "取引",
            ["UI"] = "UI",
        };

    public static string TranslateCommandCategoryLabel(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!CommandCategoryLabels.TryGetValue(source, out var translated))
        {
            return TranslateVisibleText(source, route, family);
        }

        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    public static string TranslateVisibleText(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    public static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    public static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    public static void SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }
}
