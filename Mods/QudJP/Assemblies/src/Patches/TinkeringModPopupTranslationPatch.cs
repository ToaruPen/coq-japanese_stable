using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringModPopupTranslationPatch
{
    private const string Context = nameof(TinkeringModPopupTranslationPatch);
    private const string MissingIngredientPrefix = "You don't have the required ingredient: ";

    private static readonly Regex UnstableIngredientPattern = new(
        "^(?<item>.+?) (?:is|are) too unstable to craft with\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MissingBitsPattern = new(
        "^You don't have the required (?<bits><.+?>) bits! You have:\\n\\n (?<held>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SifrahPromptPattern = new(
        "^Do you want to play a game of Sifrah to mod (?<item>.+?)\\? You can potentially improve the mod's performance and add capabilities to the item, and the cost of playing Sifrah will replace the normal modding cost\\.(?<suffix> You do not have the required <(?<bits>.+?) bits to perform the mod normally\\.)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CantUnequipPattern = new(
        "^You can't unequip (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuccessPattern = new(
        "^You mod (?<item>.+?) to be (?<mod>\\{\\{C\\|.+?\\}\\})\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.TinkeringScreen", "TinkeringScreen");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var tinkerDataType = AccessTools.TypeByName("XRL.World.Tinkering.TinkerData");
        var bitCostType = AccessTools.TypeByName("XRL.World.Tinkering.BitCost");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (targetType is null
            || gameObjectType is null
            || tinkerDataType is null
            || bitCostType is null
            || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "PerformUITinkerMod",
            [
                gameObjectType,
                gameObjectType,
                tinkerDataType,
                bitCostType,
                eventType,
                typeof(bool).MakeByRefType(),
                typeof(System.Collections.Generic.List<>).MakeGenericType(gameObjectType),
            ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.PerformUITinkerMod() not found.", Context);
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static void ResetForTests()
    {
        activeDepth = 0;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated))
        {
            Record(route, "DoesVerb", source, translated);
            return true;
        }

        if (TryTranslateCore(source, out translated, out var detail))
        {
            Record(route, detail, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        if (TryTranslateMissingIngredient(source, out translated))
        {
            detail = "MissingIngredient";
            return true;
        }

        var match = UnstableIngredientPattern.Match(source);
        if (match.Success)
        {
            var item = match.Groups["item"].Value;
            translated = item.StartsWith("Your ", StringComparison.Ordinal)
                ? $"あなたの{item[5..]}は不安定すぎて工作に使えない。"
                : $"{item}は不安定すぎて工作に使えない。";
            detail = "UnstableIngredient";
            return true;
        }

        match = MissingBitsPattern.Match(source);
        if (match.Success)
        {
            translated = $"必要な{match.Groups["bits"].Value}ビットが足りない！所持ビット:\n\n {match.Groups["held"].Value}";
            detail = "MissingBits";
            return true;
        }

        match = SifrahPromptPattern.Match(source);
        if (match.Success)
        {
            translated = BuildSifrahPrompt(match);
            detail = "SifrahPrompt";
            return true;
        }

        match = CantUnequipPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["item"].Value}を外せない。";
            detail = "CantUnequip";
            return true;
        }

        if (string.Equals(source, "You cannot use the ingredient!", StringComparison.Ordinal))
        {
            translated = "その材料は使えない！";
            detail = "CannotUseIngredient";
            return true;
        }

        match = SuccessPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["item"].Value}を{match.Groups["mod"].Value}に改造した。";
            detail = "Success";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateMissingIngredient(string source, out string translated)
    {
        if (!source.StartsWith(MissingIngredientPrefix, StringComparison.Ordinal)
            || !source.EndsWith("!", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var ingredient = source.Substring(
            MissingIngredientPrefix.Length,
            source.Length - MissingIngredientPrefix.Length - 1).Replace(" or ", "または", StringComparison.Ordinal);
        translated = $"必要な材料が足りない: {ingredient}！";
        return true;
    }

    private static string BuildSifrahPrompt(Match match)
    {
        var translated =
            $"{match.Groups["item"].Value}に改造を施すためにシフラーのゲームをプレイしますか？"
            + "シフラーで改造の性能を向上させたり、アイテムに能力を追加したりできることがあります。"
            + "シフラーのプレイコストは通常の改造コストの代わりになります。";
        return match.Groups["suffix"].Success
            ? translated + $"通常の改造を行うために必要な<{match.Groups["bits"].Value}ビットが足りません。"
            : translated;
    }

    private static void Record(string route, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }
}
