using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfirePreserveTranslationPatch
{
    private const string Context = nameof(CampfirePreserveTranslationPatch);

    private static readonly Regex PreservedLinePattern = new(
        "^(?<source>.+?) into (?<count>\\d+) (?<serving>.+?) of (?<result>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var campfireType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        if (campfireType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, campfireType, "Preserve", Type.EmptyTypes);
        AddTarget(targets, campfireType, "PreserveExotic", Type.EmptyTypes);
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

        if (!TryTranslatePreservedResult(source, out translated))
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

    private static bool TryTranslatePreservedResult(string source, out string translated)
    {
        const string header = "You preserved:\n\n";
        if (!source.StartsWith(header, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var body = source.Substring(header.Length);
        if (body.Length == 0)
        {
            translated = source;
            return false;
        }

        var lines = body.Split('\n');
        var builder = new StringBuilder("保存した:\n\n");
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append(TranslatePreservedLine(lines[index]));
        }

        translated = builder.ToString();
        return true;
    }

    private static string TranslatePreservedLine(string source)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PreservedLinePattern.Match(stripped);
        if (!match.Success)
        {
            return source;
        }

        var sourceItem = Restore(match, spans, "source");
        var count = match.Groups["count"].Value;
        var serving = Restore(match, spans, "serving");
        var result = Restore(match, spans, "result");
        return $"{sourceItem}を{count} {serving}の{result}に保存した。";
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
