using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class KeybindsScreenConflictTranslationPatch
{
    private const string Context = nameof(KeybindsScreenConflictTranslationPatch);

    private static readonly Regex ConfirmConflictPattern = new(
        "^(?<key>.+) is already bound to (?<current>.+)\\.\\r?\\n\\r?\\nDo you want to bind it to (?<command>.+) instead\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConfirmDynamicConflictPattern = new(
        "^(?<key>.+) is already bound to (?<current>.+)\\.\\r?\\n\\r?\\nDo you want to bind it to (?<command>.+) anyway\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RequiredConflictPattern = new(
        "^(?<key>.+) is already bound to (?<command>.+)\\.  This is a required bind and can't be removed\\.\\r?\\n\\r?\\nChoose a new bind for (?<commandAgain>.+) first, and then rebind (?<keyAgain>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = FindTypeByName("Qud.UI.KeybindsScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var gameCommandType = FindTypeByName("XRL.UI.GameCommand");
        if (gameCommandType is null)
        {
            Trace.TraceError("QudJP: {0} game command type not found.", Context);
        }
        else
        {
            var gameCommandListType = typeof(List<>).MakeGenericType(gameCommandType);
            AddTarget(
                targets,
                targetType,
                "ConfirmConflictBind",
                new[] { typeof(string), gameCommandListType, typeof(string) });
            AddTarget(
                targets,
                targetType,
                "ConfirmDynamicConflictBind",
                new[] { typeof(string), gameCommandListType, typeof(string) });
        }

        AddTarget(
            targets,
            targetType,
            "RequiredConflictBind",
            new[] { typeof(string), typeof(string) });
        return targets;
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslateCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameterTypes)
    {
        var method = targetType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static Type? FindTypeByName(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        if (type is not null)
        {
            return type;
        }

        Trace.TraceWarning("QudJP: {0} falling back to loaded assembly lookup for {1}.", Context, fullName);
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(static candidate => candidate is not null);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        return TryTranslatePattern(
            ConfirmConflictPattern,
            source,
            (match, spans) =>
                $"{Restore(match, spans, "key")}はすでに{Restore(match, spans, "current")}に割り当てられています。\r\n\r\n代わりに{Restore(match, spans, "command")}へ割り当てますか？",
            out translated)
            || TryTranslatePattern(
                ConfirmDynamicConflictPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "key")}はすでに{Restore(match, spans, "current")}に割り当てられています。\r\n\r\nそれでも{Restore(match, spans, "command")}へ割り当てますか？",
                out translated)
            || TryTranslatePattern(
                RequiredConflictPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "key")}はすでに{Restore(match, spans, "command")}に割り当てられています。これは必須の割り当てなので削除できません。\r\n\r\n先に{Restore(match, spans, "commandAgain")}の新しい割り当てを選んでから、{Restore(match, spans, "keyAgain")}を割り当て直してください。",
                out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
