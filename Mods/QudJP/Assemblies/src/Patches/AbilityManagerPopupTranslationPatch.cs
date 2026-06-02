using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityManagerPopupTranslationPatch
{
    private const string Context = nameof(AbilityManagerPopupTranslationPatch);

    private static readonly Regex NoActivatedAbilitiesPattern = new(
        "^No activated abilites found for '(?<query>.*)'$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PressKeyPattern = new(
        "^Press the keyboard key to bind to (?<ability>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SystemMenuBindingPattern = new(
        "^(?<binding>.+) is already bound to the system menu\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AbilityPickerBindingPattern = new(
        "^(?<binding>.+) is already bound to the ability picker\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RemoveBindingPattern = new(
        "^Are you sure you wish to remove the binding for (?<ability>.+)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string NoActivatedAbilitiesMessage = "You have no activated abilities.";
    private const string NoActivatedAbilitiesTranslation = "発動できるアビリティがない。";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = GameTypeResolver.FindType("Qud.UI.AbilityManagerScreen", "AbilityManagerScreen");
        var abilityEntryType = GameTypeResolver.FindType("XRL.World.Parts.ActivatedAbilityEntry", "ActivatedAbilityEntry");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (targetType is null || abilityEntryType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target or dependent type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleFilterItems", Type.EmptyTypes);
        AddStateMachineTarget(targets, targetType, "showScreen", new[] { gameObjectType });
        AddStateMachineTarget(targets, targetType, "HandleRebindAsync", new[] { abilityEntryType, typeof(string) });
        AddStateMachineTarget(targets, targetType, "HandleRemoveBindAsync", new[] { abilityEntryType });
        return targets;
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            translated = source;
            return false;
        }

        if (string.Equals(source, NoActivatedAbilitiesMessage, StringComparison.Ordinal))
        {
            translated = NoActivatedAbilitiesTranslation;
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        if (TryTranslatePattern(
                NoActivatedAbilitiesPattern,
                source,
                match => $"'{match.Groups["query"].Value}' に一致する有効化能力は見つからなかった。",
                out translated)
            || TryTranslatePattern(
                PressKeyPattern,
                source,
                match => $"{match.Groups["ability"].Value} に割り当てるキーボードのキーを押してください。",
                out translated)
            || TryTranslatePattern(
                SystemMenuBindingPattern,
                source,
                match => $"{match.Groups["binding"].Value} はすでにシステムメニューに割り当てられている。",
                out translated)
            || TryTranslatePattern(
                AbilityPickerBindingPattern,
                source,
                match => $"{match.Groups["binding"].Value} はすでに能力ピッカーに割り当てられている。",
                out translated)
            || TryTranslatePattern(
                RemoveBindingPattern,
                source,
                match => $"{match.Groups["ability"].Value} の割り当てを削除しますか？",
                out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, string> translate,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = translate(match);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static void AddStateMachineTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var sourceMethod = AccessTools.Method(targetType, methodName, parameters);
        if (sourceMethod is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} async source target not found.", Context, targetType.FullName, methodName);
            return;
        }

        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        var moveNext = asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
        if (moveNext is not null)
        {
            targets.Add(moveNext);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} async state machine MoveNext not found.", Context, targetType.FullName, methodName);
    }
}
