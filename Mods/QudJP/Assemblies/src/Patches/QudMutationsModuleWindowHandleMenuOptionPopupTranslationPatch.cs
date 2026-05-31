using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch
{
    private const string Context = nameof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch);
    private const string PointsRemainingTitlePrefix = "Points Remaining: ";
    private const string PointsRemainingDetail = "PointsRemainingTitle";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow",
            "QudMutationsModuleWindow");
        var menuOptionType = AccessTools.TypeByName("XRL.UI.Framework.MenuOption");
        if (targetType is null || menuOptionType is null)
        {
            Trace.TraceError("QudJP: {0} target or MenuOption type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleMenuOption", new[] { menuOptionType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleMenuOption(MenuOption) target not found.", Context);
        }

        return method;
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

        if (!source.StartsWith(PointsRemainingTitlePrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = "変異ポイント残り: " + source.Substring(PointsRemainingTitlePrefix.Length);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + PointsRemainingDetail, source, translated);
        return true;
    }
}
