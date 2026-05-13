using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PrefixedOwnerQueueTranslationPatch
{
    private const string Context = nameof(PrefixedOwnerQueueTranslationPatch);

    private static readonly PrefixedMessageTemplate[] Templates =
    [
        new(
            new Regex(
                "^You are fleeing from (?<target>.+)!$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            "You are fleeing from ",
            "{target}",
            "Flee.TakeAction"),
        new(
            new Regex(
                "^You are teleported by (?<source>.+)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            "You are teleported by ",
            "{source}",
            "Infiltrate.performInfiltrate"),
        new(
            new Regex(
                "^You set a target temperature of (?<temperature>.*)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            "You set a target temperature of ",
            "{temperature}",
            "TemperatureController.ConfigureTemperatureController"),
    ];

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");

        AddTarget(targets, "XRL.World.AI.GoalHandlers.Flee", "TakeAction", Type.EmptyTypes);

        if (cellType is not null)
        {
            AddTarget(targets, "XRL.World.Parts.Mutation.Infiltrate", "performInfiltrate", new[] { cellType, typeof(bool) });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Infiltrate.performInfiltrate parameter type not found.", Context);
        }

        if (gameObjectType is not null)
        {
            AddTarget(targets, "XRL.World.Parts.TemperatureController", "ConfigureTemperatureController", new[] { gameObjectType, typeof(bool) });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.TemperatureController.ConfigureTemperatureController parameter type not found.", Context);
        }

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
            return true;
        }

        if (!TryTranslatePrefixedMessage(message, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, detail, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslatePrefixedMessage(string source, out string translated, out string detail)
    {
        for (var index = 0; index < Templates.Length; index++)
        {
            var template = Templates[index];
            if (template.TryTranslate(source, out translated))
            {
                detail = template.Detail;
                return true;
            }
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
            return;
        }

        targets.Add(method);
    }

    private sealed class PrefixedMessageTemplate
    {
        private readonly Regex pattern;
        private readonly string dictionaryKey;
        private readonly string placeholder;
        private readonly string captureGroup;

        public PrefixedMessageTemplate(Regex pattern, string dictionaryKey, string placeholder, string detail)
        {
            this.pattern = pattern;
            this.dictionaryKey = dictionaryKey;
            this.placeholder = placeholder;
            captureGroup = placeholder.Substring(1, placeholder.Length - 2);
            Detail = detail;
        }

        public string Detail { get; }

        public bool TryTranslate(string source, out string translated)
        {
            var match = pattern.Match(source);
            if (!match.Success
                || !Translator.TryGetTranslation(dictionaryKey, out var template)
                || string.Equals(template, dictionaryKey, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            var capture = match.Groups[captureGroup].Value;
            translated = template.Replace(placeholder, capture);
            return true;
        }
    }
}
