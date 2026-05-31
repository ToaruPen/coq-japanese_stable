using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HiddenRenderTranslationPatch
{
    private const string Context = nameof(HiddenRenderTranslationPatch);
    private static readonly Regex RevealedPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) (?:is|are) revealed (?<direction>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? queuedDirectPassthroughMessage;

    [ThreadStatic]
    private static Stack<string?>? queuedDirectPassthroughStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        AddTarget(targets, "XRL.World.Parts.HiddenRender", "Reveal", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.Hidden", "RevealInternal", new[] { typeof(bool) });
        return targets;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    public static void Prefix()
    {
        try
        {
            queuedDirectPassthroughStack ??= new Stack<string?>();
            queuedDirectPassthroughStack.Push(queuedDirectPassthroughMessage);
            queuedDirectPassthroughMessage = null;
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
            if (queuedDirectPassthroughStack is { Count: > 0 })
            {
                queuedDirectPassthroughMessage = queuedDirectPassthroughStack.Pop();
            }
            else if (!OwnerTranslationScope.IsActive(activeDepth))
            {
                queuedDirectPassthroughMessage = null;
            }

            if (!OwnerTranslationScope.IsActive(activeDepth)
                && queuedDirectPassthroughStack is { Count: 0 })
            {
                queuedDirectPassthroughStack = null;
            }
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            queuedDirectPassthroughMessage = markedText;
            return true;
        }

        if (!TryTranslateRevealMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslateMessageLogMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (queuedDirectPassthroughMessage is not null
            && string.Equals(message, queuedDirectPassthroughMessage, StringComparison.Ordinal))
        {
            queuedDirectPassthroughMessage = null;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateRevealMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(nameof(MessageLogPatch), Context + ".MessageLog", message, translated);
        message = translated;
        return true;
    }

    private static bool TryTranslateRevealMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RevealedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{TranslateDirection(match, spans)}に{TranslateSubject(match, spans)}が現れた！";
        return true;
    }

    private static string TranslateSubject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return DisplayNameCaptureTranslator.TranslatePreservingColors(
            RestoreCapture(match, spans, "subject"),
            Context);
    }

    private static string TranslateDirection(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["direction"];
        var translated = group.Value switch
        {
            "here" => "ここ",
            "nearby" => "近く",
            "to the north" => "北側",
            "to the south" => "南側",
            "to the east" => "東側",
            "to the west" => "西側",
            "to the northeast" => "北東側",
            "to the northwest" => "北西側",
            "to the southeast" => "南東側",
            "to the southwest" => "南西側",
            _ => null,
        };

        return translated ?? ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
