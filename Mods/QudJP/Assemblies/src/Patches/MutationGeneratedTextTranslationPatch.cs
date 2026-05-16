using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutationGeneratedTextTranslationPatch
{
    private const string Context = nameof(MutationGeneratedTextTranslationPatch);
    private const string PhotosyntheticSkinHandleEventOwner =
        "XRL.World.Parts.Mutation.PhotosyntheticSkin|HandleEvent";
    private const string LifeDrainFireEventOwner = "XRL.World.Parts.Mutation.LifeDrain|FireEvent";
    private const string PackRatFireEventOwner = "XRL.World.Parts.Mutation.PackRat|FireEvent";
    private const string BelcherCastOwner = "XRL.World.Parts.Mutation.Belcher|Cast";

    private static readonly Regex PhotosyntheticMetabolizePattern = new(
        "^You start to metabolize the meal, gaining the following effect for the rest of the day:\\n\\n(?<effect>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LifeDrainInvalidTargetPattern = new(
        "^You cannot syphon vim from (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PackRatDropCooldownPattern = new(
        "^You must wait (?<turns>\\d+) more turns? to work up the willpower to drop something!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BelcherOutOfRangePattern = new(
        "^That is out of range! \\((?<range>\\d+) squares?\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BelcherResultPattern = new(
        "^(?:(?<you>You)|(?<subject>.+?)) (?:belches|belch) forth (?<objects>.+?)(?<punct>[.!])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PackRatCollectMoreJunkPattern = new(
        "^You must collect more junk! \\(minimum: (?<weight>\\d+) lbs\\.\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<string>? ownerStack;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var commandEventType = FindGameType("XRL.World.CommandEvent");
        var eventType = FindGameType("XRL.World.Event");
        if (commandEventType is not null)
        {
            AddTarget(targets, "XRL.World.Parts.Mutation.PhotosyntheticSkin", "HandleEvent", [commandEventType]);
        }
        else
        {
            Trace.TraceError("QudJP: {0} CommandEvent target parameter type not found.", Context);
        }

        if (eventType is not null)
        {
            AddTarget(targets, "XRL.World.Parts.Mutation.LifeDrain", "FireEvent", [eventType]);
            AddTarget(targets, "XRL.World.Parts.Mutation.PackRat", "FireEvent", [eventType]);
        }
        else
        {
            Trace.TraceError("QudJP: {0} Event target parameter type not found.", Context);
        }

        AddSelfTarget(targets, "XRL.World.Parts.Mutation.Belcher", "Cast", [typeof(string), typeof(bool), typeof(bool)]);
        return targets;
    }

    public static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
            ownerStack ??= new Stack<string>();
            ownerStack.Push(FormatOwnerKey(__originalMethod));
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
            if (ownerStack is { Count: > 0 })
            {
                _ = ownerStack.Pop();
            }

            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(
            source,
            ref directMarkerPassThroughText,
            out translated))
        {
            return true;
        }

        var ownerKey = CurrentOwnerKey();
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslatePopupCore(source, stripped, spans, ownerKey, out translated, out var detail))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
        return true;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        var source = message;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslateQueuedCore(source, stripped, spans, CurrentOwnerKey(), out var translated, out var detail))
        {
            return false;
        }

        _ = color;
        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            source,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslatePopupCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string? ownerKey,
        out string translated,
        out string detail)
    {
        var match = PhotosyntheticMetabolizePattern.Match(stripped);
        if (match.Success && OwnerMatches(ownerKey, PhotosyntheticSkinHandleEventOwner))
        {
            translated = "食事を消化し始め、今日の残りの間、以下の効果を得る:\n\n"
                + RestoreCapture(match, spans, "effect");
            detail = "PhotosyntheticSkinMetabolize";
            return true;
        }

        match = LifeDrainInvalidTargetPattern.Match(stripped);
        if (match.Success && OwnerMatches(ownerKey, LifeDrainFireEventOwner))
        {
            translated = TranslateLifeDrainTarget(match, spans) + "からヴィムを吸い取れない。";
            detail = "LifeDrainInvalidTarget";
            return true;
        }

        match = PackRatDropCooldownPattern.Match(stripped);
        if (match.Success && OwnerMatches(ownerKey, PackRatFireEventOwner))
        {
            translated = "何かを落とす意志力を奮い立たせるにはあと"
                + match.Groups["turns"].Value
                + "ターン待たなければならない！";
            translated = RestoreWholeSourceBoundaryWrappers(translated, spans, stripped.Length, source);
            detail = "PackRatDropCooldown";
            return true;
        }

        match = BelcherOutOfRangePattern.Match(stripped);
        if (match.Success && OwnerMatches(ownerKey, BelcherCastOwner))
        {
            translated = "射程外だ！(" + match.Groups["range"].Value + "マス)";
            translated = RestoreWholeSourceBoundaryWrappers(translated, spans, stripped.Length, source);
            detail = "BelcherOutOfRange";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateQueuedCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string? ownerKey,
        out string translated,
        out string detail)
    {
        var match = PackRatCollectMoreJunkPattern.Match(stripped);
        if (match.Success && OwnerMatches(ownerKey, PackRatFireEventOwner))
        {
            translated = "もっとガラクタを集めろ！（最低 "
                + match.Groups["weight"].Value
                + " ポンド）";
            translated = RestoreWholeSourceBoundaryWrappers(translated, spans, stripped.Length, source);
            detail = "PackRatCollectMoreJunk";
            return true;
        }

        match = BelcherResultPattern.Match(stripped);
        if (match.Success && OwnerMatches(ownerKey, BelcherCastOwner))
        {
            translated = TranslateBelcherResult(source, stripped, spans, match);
            detail = "BelcherResult";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TranslateBelcherResult(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Match match)
    {
        var subject = match.Groups["you"].Success
            ? "あなた"
            : TranslateFunctionWordLabel(RestoreCapture(match, spans, "subject").Trim());
        var objects = TranslateFunctionWordLabel(RestoreCapture(match, spans, "objects").Trim());
        var punctuation = string.Equals(match.Groups["punct"].Value, "!", StringComparison.Ordinal) ? "！" : "。";
        var translated = subject + "は" + objects + "を吐き出した" + punctuation;
        return RestoreWholeSourceBoundaryWrappers(translated, spans, stripped.Length, source);
    }

    private static string TranslateFunctionWordLabel(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible =>
            {
                if (string.Equals(visible, "yourself", StringComparison.Ordinal))
                {
                    return "自分自身";
                }

                return StringHelpers.StripLeadingEnglishArticle(
                    visible,
                    includeCapitalizedDefiniteArticle: true,
                    includeCapitalizedIndefiniteArticle: true);
            });
    }

    private static string TranslateLifeDrainTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var targetGroup = match.Groups["target"];
        if (string.Equals(targetGroup.Value, "yourself", StringComparison.Ordinal))
        {
            return ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                "自分自身",
                spans,
                targetGroup);
        }

        return RestoreCapture(match, spans, "target");
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group);
    }

    private static string RestoreWholeSourceBoundaryWrappers(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int sourceLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            sourceLength,
            source);
    }

    private static string? CurrentOwnerKey()
    {
        return ownerStack is { Count: > 0 } ? ownerStack.Peek() : null;
    }

    private static bool OwnerMatches(string? actual, params string[] expected)
    {
        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (string.Equals(actual, expected[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatOwnerKey(MethodBase method)
    {
        return (method.DeclaringType?.FullName ?? string.Empty) + "|" + method.Name;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = FindGameType(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static void AddSelfTarget(List<MethodBase> targets, string typeName, string methodName, Type[] extraParameters)
    {
        var targetType = FindGameType(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
            return;
        }

        var parameters = new Type[extraParameters.Length + 1];
        parameters[0] = targetType;
        Array.Copy(extraParameters, 0, parameters, 1, extraParameters.Length);
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static string SimpleTypeName(string typeName)
    {
        var separator = typeName.LastIndexOf('.');
        return separator >= 0 ? typeName.Substring(separator + 1) : typeName;
    }

    private static Type? FindGameType(string fullTypeName)
    {
        var assemblyType = FindTypeInAssemblyCSharp(fullTypeName);
        if (assemblyType is not null)
        {
            return assemblyType;
        }

        var accessToolsType = AccessTools.TypeByName(fullTypeName);
        if (accessToolsType is not null)
        {
            return accessToolsType;
        }

        return GameTypeResolver.FindType(fullTypeName, SimpleTypeName(fullTypeName));
    }

    private static Type? FindTypeInAssemblyCSharp(string fullTypeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var index = 0; index < assemblies.Length; index++)
        {
            if (!string.Equals(assemblies[index].GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                continue;
            }

            var type = assemblies[index].GetType(fullTypeName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }
}
