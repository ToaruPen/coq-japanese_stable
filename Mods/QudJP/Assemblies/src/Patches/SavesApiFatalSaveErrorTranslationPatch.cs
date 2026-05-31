using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SavesApiFatalSaveErrorTranslationPatch
{
    private const string Context = nameof(SavesApiFatalSaveErrorTranslationPatch);
    private const string ExitWarning =
        "Caves of Qud will exit now since we cannot save games. Please check your directory’s permissions.";
    private const string ExitWarningJa =
        "ゲームを保存できないため、Caves of Qud を終了する。ディレクトリの権限を確認してください。";

    private static readonly Regex PermissionBodyPattern = new(
        "^There was a permission error while trying to access your save directory\\.\\r?\\n\\r?\\n(?<message>.*?)\\r?\\n\\r?\\n"
        + Regex.Escape(ExitWarning)
        + "\\r?\\n?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex GenericBodyPattern = new(
        "^There was an error while trying to access your save directory\\.\\r?\\n\\r?\\nDirectory: (?<path>.*?)\\r?\\n\\r?\\n"
        + Regex.Escape(ExitWarning)
        + "\\r?\\n?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("Qud.API.SavesAPI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Qud.API.SavesAPI.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "FatalSaveError", [typeof(Exception), typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.FatalSaveError(Exception,string) not found.", Context);
            yield break;
        }

        yield return method;
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

    internal static void ResetForTests()
    {
        activeDepth = 0;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        if (string.Equals(source, "Error reading save location.", StringComparison.Ordinal))
        {
            translated = "セーブ場所の読み取りエラー";
            Record(route, family, "Title", source, translated);
            return true;
        }

        if (string.Equals(source, "Quit", StringComparison.Ordinal))
        {
            translated = "終了";
            Record(route, family, "QuitButton", source, translated);
            return true;
        }

        var match = PermissionBodyPattern.Match(source);
        if (match.Success)
        {
            translated =
                "セーブディレクトリへのアクセス中に権限エラーが発生した。\n\n"
                + match.Groups["message"].Value
                + "\n\n"
                + ExitWarningJa
                + "\n";
            Record(route, family, "PermissionBody", source, translated);
            return true;
        }

        match = GenericBodyPattern.Match(source);
        if (match.Success)
        {
            translated =
                "セーブディレクトリへのアクセス中にエラーが発生した。\n\n"
                + "ディレクトリ: "
                + match.Groups["path"].Value
                + "\n\n"
                + ExitWarningJa
                + "\n";
            Record(route, family, "GenericBody", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
