using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SteamWorkshopUploaderViewTranslationPatch
{
    private const string Context = nameof(SteamWorkshopUploaderViewTranslationPatch);
    private const string TargetTypeName = "SteamWorkshopUploaderView";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            yield break;
        }

        foreach (var methodName in new[] { "Popup", "ShowProgress", "SetProgress" })
        {
            var method = AccessTools.Method(targetType, methodName);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.{1}(...) not found on '{2}'.", Context, methodName, TargetTypeName);
                continue;
            }

            yield return method;
        }
    }

    public static void Prefix(object[]? __args)
    {
        try
        {
            if (__args is null
                || __args.Length == 0
                || __args[0] is not string source
                || string.IsNullOrEmpty(source))
            {
                return;
            }

            var translated = TranslateText(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                __args[0] = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(Context, "SteamWorkshopUploaderView.Text", source, translated);
        return translated;
    }
}
