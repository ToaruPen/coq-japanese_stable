using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MetricsManagerLogErrorTranslationPatch
{
    private const string Context = nameof(MetricsManagerLogErrorTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("MetricsManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        foreach (var method in ResolveLogErrorTargets(targetType))
        {
            yield return method;
        }
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
        translated = source;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (string.Equals(source, "{{R|Error}}", StringComparison.Ordinal))
        {
            translated = "{{R|エラー}}";
            Record(route, family, "Title", source, translated);
            return true;
        }

        if (!IsDiagnosticBody(source))
        {
            return false;
        }

        Record(route, family, "DiagnosticBodyPreserved", source, translated, logWhenUnchanged: true);
        return true;
    }

    private static IEnumerable<MethodBase> ResolveLogErrorTargets(Type targetType)
    {
        yield return RequireTarget(targetType, new[] { typeof(string) });
        yield return RequireTarget(targetType, new[] { typeof(string), typeof(string) });
        yield return RequireTarget(targetType, new[] { typeof(string), typeof(Exception) });
    }

    private static MethodBase RequireTarget(Type targetType, Type[] parameterTypes)
    {
        var method = AccessTools.Method(targetType, "LogError", parameterTypes);
        if (method is null)
        {
            Trace.TraceError(
                "QudJP: {0}.LogError({1}) target not found.",
                Context,
                string.Join(", ", Array.ConvertAll(parameterTypes, static type => type.FullName)));
            throw new MissingMethodException(targetType.FullName, "LogError");
        }

        return method;
    }

    private static bool IsDiagnosticBody(string source)
    {
#pragma warning disable CA2249 // net48 has no string.Contains(char) overload.
        return source.IndexOf('\n') >= 0;
#pragma warning restore CA2249
    }

    private static void Record(
        string route,
        string family,
        string detail,
        string source,
        string translated,
        bool logWhenUnchanged = false)
    {
        DynamicTextObservability.RecordTransform(
            route,
            family + "." + Context + "." + detail,
            source,
            translated,
            logWhenUnchanged);
    }
}
