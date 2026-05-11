using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BodyTranslationPatch
{
    private const string Context = nameof(BodyTranslationPatch);

    private static readonly Regex LostUsePattern = new(
        "^You have lost the use of your (?<part>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RecoveredUsePattern = new(
        "^You have recovered the use of your (?<part>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RegenerateLimbPattern = new(
        "^You regenerate your (?<part>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DismemberedPattern = new(
        "^Your (?<part>.+?) (?:are|is) dismembered!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var bodyType = AccessTools.TypeByName("XRL.World.Parts.Body");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var bodyPartType = AccessTools.TypeByName("XRL.World.Anatomy.BodyPart");
        var inventoryType = AccessTools.TypeByName("XRL.World.IInventory");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        var dismemberedPartType = AccessTools.TypeByName("XRL.World.Parts.Body+DismemberedPart");
        if (bodyType is null || gameObjectType is null || bodyPartType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, bodyType, "CheckUnsupportedPartLoss", Type.EmptyTypes);
        AddTarget(targets, bodyType, "CheckPartRecovery", Type.EmptyTypes);
        if (inventoryType is not null)
        {
            AddTarget(
                targets,
                bodyType,
                "Dismember",
                new[] { bodyPartType, gameObjectType, inventoryType, typeof(bool), typeof(bool), eventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Dismember IInventory target type not found.", Context);
        }

        if (dismemberedPartType is not null)
        {
            AddTarget(
                targets,
                bodyType,
                "RegenerateLimb",
                new[]
                {
                    typeof(bool),
                    dismemberedPartType,
                    typeof(int?),
                    typeof(int?),
                    typeof(int[]),
                    typeof(int?),
                    typeof(int[]),
                    typeof(bool),
                });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.RegenerateLimb DismemberedPart target type not found.", Context);
        }

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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateBodyMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "Body.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

        if (!TryTranslateDismemberedPopup(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
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

    private static bool TryTranslateBodyMessage(string source, out string translated)
    {
        return TryTranslatePattern(LostUsePattern, source, part => part + "が使えなくなった。", out translated)
            || TryTranslatePattern(RecoveredUsePattern, source, part => part + "の使用が回復した。", out translated)
            || TryTranslatePattern(RegenerateLimbPattern, source, part => part + "を再生した！", out translated);
    }

    private static bool TryTranslateDismemberedPopup(string source, out string translated)
    {
        return TryTranslatePattern(DismemberedPattern, source, part => part + "が切断された！", out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var group = match.Groups["part"];
        var part = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(part),
            spans,
            stripped.Length,
            source);
        return true;
    }
}
