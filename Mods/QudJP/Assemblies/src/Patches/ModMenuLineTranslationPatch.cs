using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModMenuLineTranslationPatch
{
    private const string Context = nameof(ModMenuLineTranslationPatch);
    private const string TargetTypeName = "Qud.UI.ModMenuLine";
    private const string AuthorPrefix = "by ";
    private const string JapaneseAuthorPrefix = "作者: ";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            yield break;
        }

        var updateMethod = AccessTools.Method(targetType, "Update", Type.EmptyTypes);
        if (updateMethod is null)
        {
            Trace.TraceError("QudJP: {0}.Update() not found on '{1}'.", Context, TargetTypeName);
        }
        else
        {
            yield return updateMethod;
        }

        var setTagMethod = AccessTools.Method(
            targetType,
            "SetTag",
            new[] { typeof(string), typeof(int).MakeByRefType(), typeof(bool) });
        if (setTagMethod is null)
        {
            Trace.TraceError("QudJP: {0}.SetTag(string, ref int, bool) not found on '{1}'.", Context, TargetTypeName);
        }
        else
        {
            yield return setTagMethod;
        }
    }

    public static void Prefix(MethodBase? __originalMethod, object[]? __args)
    {
        try
        {
            if (!string.Equals(__originalMethod?.Name, "SetTag", StringComparison.Ordinal)
                || __args is null
                || __args.Length == 0
                || __args[0] is not string source
                || string.IsNullOrEmpty(source))
            {
                return;
            }

            var translated = TranslateTagText(source);
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

    public static void Postfix(MethodBase? __originalMethod, object? __instance)
    {
        try
        {
            if (!string.Equals(__originalMethod?.Name, "Update", StringComparison.Ordinal)
                || __instance is null)
            {
                return;
            }

            var authorText = UiBindingTranslationHelpers.GetMemberValue(__instance, "authorText");
            var current = UITextSkinReflectionAccessor.GetCurrentText(authorText, Context);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var translated = TranslateAuthorLabel(current!);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "ModMenuLine.AuthorText", current!, translated);
                OwnerTextSetter.SetTranslatedText(authorText, current!, translated, Context, typeof(ModMenuLineTranslationPatch));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateTagText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(Context, "ModMenuLine.TagText", source, translated);
        return translated;
    }

    internal static string TranslateAuthorLabel(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var colorMarkerIndex = source.IndexOf(AuthorPrefix, StringComparison.Ordinal);
        if (colorMarkerIndex < 0)
        {
            return source;
        }

        return source.Substring(0, colorMarkerIndex)
            + JapaneseAuthorPrefix
            + source.Substring(colorMarkerIndex + AuthorPrefix.Length);
    }
}
