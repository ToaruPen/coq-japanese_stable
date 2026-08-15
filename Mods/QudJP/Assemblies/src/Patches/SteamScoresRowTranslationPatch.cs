using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SteamScoresRowTranslationPatch
{
    private const string Context = nameof(SteamScoresRowTranslationPatch);
    private const string Family = "SteamScoresRow.StatusMessage";
    private const string DictionaryFile = "ui-scores.ja.json";
    private const string ExpectedDataTypeFullName = "Qud.UI.HighScoresDataElement";

    private static readonly HashSet<string> ReviewedStatusMessages = new(StringComparer.Ordinal)
    {
        "{{R|The game is currently running offline.}}",
        "{{R|An error has occurred.}}",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("SteamScoresRow");
        var dataType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");
        if (targetType is null || dataType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return Array.Empty<MethodBase>();
        }

        var method = AccessTools.Method(targetType, "setData", new[] { dataType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.SteamScoresRow.setData target not found.", Context);
            return Array.Empty<MethodBase>();
        }

        return new[] { method };
    }

    internal static void Prefix(object? __0, out RowTranslationState? __state)
    {
        try
        {
            __state = null;
            if (__0 is null)
            {
                return;
            }

            if (!string.Equals(__0.GetType().FullName, ExpectedDataTypeFullName, StringComparison.Ordinal))
            {
                return;
            }

            var messageField = AccessTools.Field(__0.GetType(), "message");
            if (messageField?.FieldType != typeof(string)
                || messageField.GetValue(__0) is not string source
                || !ReviewedStatusMessages.Contains(source))
            {
                return;
            }

            var translated = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                source,
                Family,
                DictionaryFile);
            if (translated is null || string.Equals(translated, source, StringComparison.Ordinal))
            {
                return;
            }

            __state = new RowTranslationState(__0, messageField, source);
            messageField.SetValue(__0, translated);
            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
        }
        catch (Exception ex)
        {
            __state = null;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static Exception? Finalizer(Exception? __exception, RowTranslationState? __state)
    {
        try
        {
            __state?.MessageField.SetValue(__state.Data, __state.Source);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal sealed class RowTranslationState
    {
        public RowTranslationState(object data, FieldInfo messageField, string source)
        {
            Data = data;
            MessageField = messageField;
            Source = source;
        }

        public object Data { get; }

        public FieldInfo MessageField { get; }

        public string Source { get; }
    }
}
