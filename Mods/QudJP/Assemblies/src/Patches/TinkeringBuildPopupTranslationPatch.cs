using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringBuildPopupTranslationPatch
{
    private const string Context = nameof(TinkeringBuildPopupTranslationPatch);
    private const string MissingIngredientPrefix = "You don't have the required ingredient: ";
    private const string TinkerUpPrefix = "You tinker up ";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.TinkeringScreen", "TinkeringScreen");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var tinkerDataType = AccessTools.TypeByName("XRL.World.Tinkering.TinkerData");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (targetType is null || gameObjectType is null || tinkerDataType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "PerformUITinkerBuild",
            new[] { gameObjectType, tinkerDataType, eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.PerformUITinkerBuild() not found.", Context);
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

        if (TryTranslateMissingIngredient(source, out translated)
            || TryTranslateTinkerUp(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        translated = source;
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
            source.Length - MissingIngredientPrefix.Length - 1).Replace(" or ", "または");
        translated = $"必要な材料が足りない: {ingredient}！";
        return true;
    }

    private static bool TryTranslateTinkerUp(string source, out string translated)
    {
        if (!source.StartsWith(TinkerUpPrefix, StringComparison.Ordinal)
            || !source.EndsWith("!", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var item = source.Substring(TinkerUpPrefix.Length, source.Length - TinkerUpPrefix.Length - 1);
        if (item.StartsWith("{{", StringComparison.Ordinal))
        {
            translated = $"{item}を作った！";
            return true;
        }

        var separator = item.IndexOf(' ');
        if (separator > 0)
        {
            var count = item.Substring(0, separator);
            var rest = item.Substring(separator + 1);
            if (!IsIndefiniteArticle(count) && !string.IsNullOrWhiteSpace(rest))
            {
                translated = $"{rest}を{TranslateCount(count)}個作った！";
                return true;
            }
        }

        translated = $"{item}を作った！";
        return true;
    }

    private static bool IsIndefiniteArticle(string value)
    {
        return string.Equals(value, "a", StringComparison.Ordinal)
            || string.Equals(value, "an", StringComparison.Ordinal);
    }

    private static string TranslateCount(string value)
    {
        return value switch
        {
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            _ => value,
        };
    }
}
