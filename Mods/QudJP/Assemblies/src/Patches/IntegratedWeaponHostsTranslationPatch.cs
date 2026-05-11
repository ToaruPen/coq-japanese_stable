using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class IntegratedWeaponHostsTranslationPatch
{
    private const string Context = nameof(IntegratedWeaponHostsTranslationPatch);

    private static readonly Regex NoAmmoPattern = new(
        "^(?<host>.+?) have no ammunition to supply (?<turret>.+?) with\\. (?<pronoun>.+?) may be ineffective unless stocked\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WishFailurePattern = new(
        "^Could not generate turret from blueprint \"(?<blueprint>.+?)\"\\n\\n(?<exception>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Capabilities.IntegratedWeaponHosts");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var matchType = typeof(Match);
        if (gameObjectType is not null)
        {
            AddTarget(targets, targetType, "GenerateTurret", new[] { gameObjectType, gameObjectType, typeof(bool) });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.GenerateTurret GameObject type not found.", Context);
        }

        AddTarget(targets, targetType, "HandleTurretWish", new[] { matchType });
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

        if (!TryTranslateCore(source, out translated))
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

    private static bool TryTranslateCore(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var noAmmoMatch = NoAmmoPattern.Match(stripped);
        if (noAmmoMatch.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{Restore(noAmmoMatch, spans, "turret")}に補給する弾薬がない。補充しない限り{Restore(noAmmoMatch, spans, "pronoun")}は効果が薄いかもしれない。",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var wishFailureMatch = WishFailurePattern.Match(stripped);
        if (wishFailureMatch.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"blueprint \"{Restore(wishFailureMatch, spans, "blueprint")}\" からタレットを生成できなかった\n\n{Restore(wishFailureMatch, spans, "exception")}",
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
