using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutationDisplayNameTranslationPatch
{
    internal const string Context = nameof(MutationDisplayNameTranslationPatch);
    internal const string Family = Context + ".GetDisplayName";
    private const string DefectSuffix = " ({{r|D}})";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baseMutationType = AccessTools.TypeByName("XRL.World.Parts.Mutation.BaseMutation");
        if (baseMutationType is not null)
        {
            foreach (var method in ResolveTargets(baseMutationType, "GetDisplayName", new[] { typeof(bool) }))
            {
                yield return method;
            }
        }
        else
        {
            Trace.TraceError("QudJP: {0} BaseMutation target type not found.", Context);
        }

        var mutationEntryType = AccessTools.TypeByName("XRL.MutationEntry");
        if (mutationEntryType is not null)
        {
            foreach (var method in ResolveTargets(mutationEntryType, "GetDisplayName", new[] { typeof(bool) }))
            {
                yield return method;
            }
        }
        else
        {
            Trace.TraceError("QudJP: {0} MutationEntry target type not found.", Context);
        }

        static IEnumerable<MethodBase> ResolveTargets(Type type, string methodName, Type[] parameters)
        {
            var method = AccessTools.Method(type, methodName, parameters);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, type.FullName, methodName);
            }
        }
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (MessageFrameTranslator.TryStripDirectTranslationMarker(__result, out var markedText))
            {
                __result = markedText;
                return;
            }

            if (!TryTranslateDisplayName(__result, out var translated)
                || string.Equals(__result, translated, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family, __result, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateDisplayName(string? source, out string translated)
    {
        if (source is null)
        {
            translated = string.Empty;
            return false;
        }

        translated = source;
        if (source.Length == 0)
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var stripped))
        {
            translated = stripped;
            return !string.Equals(source, stripped, StringComparison.Ordinal);
        }

        var sourceText = source;
        if (sourceText.EndsWith(DefectSuffix, StringComparison.Ordinal))
        {
            var baseName = sourceText.Substring(0, sourceText.Length - DefectSuffix.Length);
            var translatedBaseName = StatusScreenPopupTranslationPatch.TranslateMutationDisplayName(baseName);
            if (string.Equals(baseName, translatedBaseName, StringComparison.Ordinal))
            {
                return false;
            }

            translated = translatedBaseName + DefectSuffix;
            return true;
        }

        translated = StatusScreenPopupTranslationPatch.TranslateMutationDisplayName(sourceText);
        return !string.Equals(sourceText, translated, StringComparison.Ordinal);
    }
}
