using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ItemNamingTranslationPatch
{
    private const string Context = nameof(ItemNamingTranslationPatch);

    private static readonly Regex OpportunityPattern =
        new Regex("^You swell with the inspiration to name your (?<item>.+?)\\. Do you wish to\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NameItemPattern =
        new Regex("^You name (?<item>.+?) '(?<name>.+)'\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InteractiveRenamePattern =
        new Regex("^Rename (?<item>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InteractiveAskStringPattern =
        new Regex("^Enter a new name for (?<item>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InteractiveCulturePattern =
        new Regex("^Choose a random name from (?<culture>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InteractiveColorPickerPattern =
        new Regex("^You select the name '(?<name>.+)' for (?<item>.+?)\\. Choose a color for (?<them>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WishDebugCreatedLinePattern =
        new Regex("^\\[Debug: Created (?<item>.+?) as (?<role>kill|InfluencedBy)\\.\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var itemNamingType = AccessTools.TypeByName("XRL.World.Capabilities.ItemNaming");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (itemNamingType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve ItemNaming or GameObject.", Context);
            yield break;
        }

        var opportunity = AccessTools.Method(
            itemNamingType,
            "Opportunity",
            [
                gameObjectType,
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool),
            ]);
        if (opportunity is not null)
        {
            yield return opportunity;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Opportunity(...) not found.", Context);
        }

        var checkBestowals = AccessTools.Method(
            itemNamingType,
            "CheckBestowals",
            [
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(bool).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType(),
            ]);
        if (checkBestowals is not null)
        {
            yield return checkBestowals;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.CheckBestowals(...) not found.", Context);
        }

        var nameItem = AccessTools.Method(
            itemNamingType,
            "NameItem",
            [
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(bool),
                typeof(int),
                typeof(bool),
            ]);
        if (nameItem is not null)
        {
            yield return nameItem;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.NameItem(...) not found.", Context);
        }

        var interactiveNameItem = AccessTools.Method(
            itemNamingType,
            "NameItem",
            [
                gameObjectType,
                gameObjectType,
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(string),
                typeof(bool),
            ]);
        if (interactiveNameItem is not null)
        {
            yield return interactiveNameItem;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.NameItem(interactive overload) not found.", Context);
        }

        var handleItemNamingWish = AccessTools.Method(
            itemNamingType,
            "HandleItemNamingWish",
            [
                typeof(Match),
            ]);
        if (handleItemNamingWish is not null)
        {
            yield return handleItemNamingWish;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleItemNamingWish(Match) not found.", Context);
        }
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        if (string.Equals(source, "[Debug: Naming failed.]", StringComparison.Ordinal))
        {
            translated = "[Debug: 命名に失敗した。]";
            Record(route, family, "WishDebugFailed", source, translated);
            return true;
        }

        if (TryTranslateWishDebugCreatedLines(source, out translated))
        {
            Record(route, family, "WishDebugCreated", source, translated);
            return true;
        }

        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out var doesVerbTranslated))
        {
            translated = doesVerbTranslated;
            Record(route, family, "CheckBestowals.DoesVerb", source, translated);
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = OpportunityPattern.Match(stripped);
        if (match.Success)
        {
            var item = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                match.Groups["item"].Value,
                spans,
                match.Groups["item"]).Trim();
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "あなたは" + item + "に名付けたい衝動に駆られた。そうしますか？",
                spans,
                stripped.Length);
            Record(route, family, "Opportunity", source, translated);
            return true;
        }

        match = NameItemPattern.Match(stripped);
        if (match.Success)
        {
            var item = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                match.Groups["item"].Value,
                spans,
                match.Groups["item"]).Trim();
            var name = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                match.Groups["name"].Value,
                spans,
                match.Groups["name"]).Trim();
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "あなたは" + item + "に「" + name + "」と名付けた。",
                spans,
                stripped.Length);
            Record(route, family, "NameItem", source, translated);
            return true;
        }

        match = InteractiveRenamePattern.Match(stripped);
        if (match.Success)
        {
            var item = RestoreCapture(match, spans, "item").Trim();
            translated = RestoreWhole(item + "の名前を変更する。", spans, stripped.Length);
            Record(route, family, "Interactive.Rename", source, translated);
            return true;
        }

        if (string.Equals(stripped, "Enter a name.", StringComparison.Ordinal))
        {
            translated = RestoreWhole("名前を入力する。", spans, stripped.Length);
            Record(route, family, "Interactive.EnterName", source, translated);
            return true;
        }

        if (string.Equals(stripped, "Name it based on its qualities.", StringComparison.Ordinal)
            || string.Equals(stripped, "Name them based on their qualities.", StringComparison.Ordinal))
        {
            translated = RestoreWhole("特質に基づいて名前を付ける。", spans, stripped.Length);
            Record(route, family, "Interactive.Qualities", source, translated);
            return true;
        }

        if (string.Equals(stripped, "Choose a random name from your own culture.", StringComparison.Ordinal))
        {
            translated = RestoreWhole("自分の文化からランダムな名前を選ぶ。", spans, stripped.Length);
            Record(route, family, "Interactive.OwnCulture", source, translated);
            return true;
        }

        match = InteractiveCulturePattern.Match(stripped);
        if (match.Success)
        {
            var culture = RestoreCapture(match, spans, "culture").Trim();
            translated = RestoreWhole(culture + "からランダムな名前を選ぶ。", spans, stripped.Length);
            Record(route, family, "Interactive.Culture", source, translated);
            return true;
        }

        match = InteractiveAskStringPattern.Match(stripped);
        if (match.Success)
        {
            var item = RestoreCapture(match, spans, "item").Trim();
            translated = RestoreWhole(item + "の新しい名前を入力する。", spans, stripped.Length);
            Record(route, family, "Interactive.AskString", source, translated);
            return true;
        }

        match = InteractiveColorPickerPattern.Match(stripped);
        if (match.Success)
        {
            var item = RestoreCapture(match, spans, "item").Trim();
            var name = RestoreCapture(match, spans, "name").Trim();
            translated = RestoreWhole(item + "の名前として「" + name + "」を選択した。色を選ぶ。", spans, stripped.Length);
            Record(route, family, "Interactive.ColorPicker", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateWishDebugCreatedLines(string source, out string translated)
    {
        var lines = source.Split('\n');
        var translatedLines = new List<string>(lines.Length);
        var anyTranslated = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var originalLine = lines[index];
            var hasCarriageReturn = originalLine.EndsWith("\r", StringComparison.Ordinal);
            var line = hasCarriageReturn ? originalLine.Substring(0, originalLine.Length - 1) : originalLine;
            if (index == lines.Length - 1 && line.Length == 0)
            {
                translatedLines.Add(originalLine);
                continue;
            }

            var match = WishDebugCreatedLinePattern.Match(line);
            if (!match.Success)
            {
                translated = source;
                return false;
            }

            var item = match.Groups["item"].Value;
            var role = match.Groups["role"].Value;
            translatedLines.Add("[Debug: " + item + " を " + role + " として作成した。]" + (hasCarriageReturn ? "\r" : string.Empty));
            anyTranslated = true;
        }

        translated = anyTranslated ? string.Join("\n", translatedLines) : source;
        return anyTranslated;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            match.Groups[groupName].Value,
            spans,
            match.Groups[groupName]);
    }

    private static string RestoreWhole(string translated, IReadOnlyList<ColorSpan> spans, int sourceLength)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            sourceLength);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
