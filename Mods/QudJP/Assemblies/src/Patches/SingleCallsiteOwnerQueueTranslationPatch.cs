using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SingleCallsiteOwnerQueueTranslationPatch
{
    private const string Context = nameof(SingleCallsiteOwnerQueueTranslationPatch);
    private const string ModMorphogeneticOwner = "XRL.World.Parts.ModMorphogenetic|ApplyMorphicShock";
    private const string WeirdwireConduitOwner = "XRL.World.Quests.WeirdwireConduitSystem|HandleEvent";

    private static readonly Regex MorphogeneticShockPattern = new(
        "^A weird(?<painful>, painful)? shock reverberates through you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WeirdwireCopperWirePattern = new(
        "^You now have (?<length>\\d+) feet of copper wire\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<string>? ownerStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var tookEventType = AccessTools.TypeByName("XRL.World.TookEvent");
        if (gameObjectType is null || tookEventType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            "XRL.World.Parts.ModMorphogenetic",
            "ApplyMorphicShock",
            [gameObjectType, typeof(int), gameObjectType, typeof(int)]);
        AddTarget(
            targets,
            "XRL.World.Quests.WeirdwireConduitSystem",
            "HandleEvent",
            [tookEventType]);
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        return TryTranslateQueuedMessageForOwnerKey(ref message, color, CurrentOwnerKey());
    }

    internal static bool TryTranslateQueuedMessageForOwnerKey(ref string message, string? color, string? ownerKey)
    {
        _ = color;

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateCore(message, ownerKey, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateCore(string source, string? ownerKey, out string translated, out string detail)
    {
        var match = MorphogeneticShockPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ModMorphogeneticOwner))
        {
            if (match.Groups["painful"].Success)
            {
                translated = "「奇妙で痛い電撃」が全身を駆け抜けた。";
                detail = "ModMorphogeneticPainfulShock";
            }
            else
            {
                translated = "「奇妙な電撃」が全身を駆け抜けた。";
                detail = "ModMorphogeneticPainlessShock";
            }

            return true;
        }

        match = WeirdwireCopperWirePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, WeirdwireConduitOwner))
        {
            translated = "銅線を" + match.Groups["length"].Value + "フィート持っている。";
            detail = "WeirdwireCopperWireTotal";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
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
        var targetType = AccessTools.TypeByName(typeName);
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
}
