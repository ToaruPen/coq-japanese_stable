using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SifrahTokenItemPopupTranslationPatch
{
    private const string Context = nameof(SifrahTokenItemPopupTranslationPatch);
    private const string DeferredKindOfItem = "of that kind of item";
    private const string SocialSifrahTokenGiftAnyMore = nameof(SocialSifrahTokenGiftAnyMore);
    private const string SocialSifrahTokenGiftHaveNone = nameof(SocialSifrahTokenGiftHaveNone);
    private const string SocialSifrahTokenItemAnyMore = nameof(SocialSifrahTokenItemAnyMore);
    private const string SocialSifrahTokenItemHaveNone = nameof(SocialSifrahTokenItemHaveNone);

    private static readonly Regex AnyMorePattern = new(
        "^You do not have any more (?<item>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HaveNonePattern = new(
        "^You do not have (?<item>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? activeDeclaringType;

    [ThreadStatic]
    private static string? activeMemberName;

    [ThreadStatic]
    private static int directMarkerPassThroughDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var sifrahGameType = AccessTools.TypeByName("XRL.SifrahGame");
        var sifrahSlotType = AccessTools.TypeByName("XRL.SifrahSlot");
        if (gameObjectType is null || sifrahGameType is null || sifrahSlotType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve a Sifrah support type.", Context);
            yield break;
        }

        var parameterTypes = new[] { sifrahGameType, sifrahSlotType, gameObjectType };
        foreach (var typeName in new[]
                 {
                     "XRL.World.SocialSifrahTokenGift",
                     "XRL.World.SocialSifrahTokenItem",
                 })
        {
            var type = AccessTools.TypeByName(typeName);
            if (type is null)
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.", Context, typeName);
                continue;
            }

            var method = AccessTools.Method(type, "CheckTokenUse", parameterTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.CheckTokenUse.", Context, typeName);
            }
        }
    }

    internal static void Prefix(MethodBase __originalMethod, out OwnerContextState? __state)
    {
        try
        {
            __state = new OwnerContextState(activeDeclaringType, activeMemberName);
            activeDepth++;
            activeDeclaringType = __originalMethod.DeclaringType?.FullName;
            activeMemberName = __originalMethod.Name;
        }
        catch (Exception ex)
        {
            __state = null;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static Exception? Finalizer(Exception? __exception, OwnerContextState? __state)
    {
        try
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }

            if (__state is not null)
            {
                activeDeclaringType = __state.PreviousDeclaringType;
                activeMemberName = __state.PreviousMemberName;
            }

            if (activeDepth == 0)
            {
                activeDeclaringType = null;
                activeMemberName = null;
                directMarkerPassThroughDepth = 0;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (directMarkerPassThroughDepth > 0)
        {
            directMarkerPassThroughDepth--;
            translated = source;
            return true;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            directMarkerPassThroughDepth++;
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (IsActiveOwner(
                "XRL.World.SocialSifrahTokenGift",
                "CheckTokenUse",
                "SocialSifrahTokenGiftCheckTokenUse")
            && TryTranslate(
                stripped,
                source,
                spans,
                route,
                family,
                SocialSifrahTokenGiftAnyMore,
                SocialSifrahTokenGiftHaveNone,
                out translated))
        {
            return true;
        }

        if (IsActiveOwner(
                "XRL.World.SocialSifrahTokenItem",
                "CheckTokenUse",
                "SocialSifrahTokenItemCheckTokenUse")
            && TryTranslate(
                stripped,
                source,
                spans,
                route,
                family,
                SocialSifrahTokenItemAnyMore,
                SocialSifrahTokenItemHaveNone,
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslate(
        string stripped,
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string anyMoreDetail,
        string haveNoneDetail,
        out string translated)
    {
        if (TryTranslatePattern(
            AnyMorePattern,
            stripped,
            source,
            spans,
            route,
            family,
            anyMoreDetail,
            item => item + "をもう持っていない。",
            out translated))
        {
            return true;
        }

        return TryTranslatePattern(
            HaveNonePattern,
            stripped,
            source,
            spans,
            route,
            family,
            haveNoneDetail,
            item => item + "を持っていない。",
            out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string detail,
        Func<string, string> translate,
        out string translated)
    {
        _ = family;

        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var itemGroup = match.Groups["item"];
        if (string.Equals(itemGroup.Value, DeferredKindOfItem, StringComparison.Ordinal)
            || itemGroup.Value.StartsWith("any more ", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var item = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(itemGroup.Value, spans, itemGroup).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(item),
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + "." + detail, source, translated);
        return true;
    }

    private static bool IsActiveOwner(string declaringType, string memberName, string dummyMemberName)
    {
        return string.Equals(activeDeclaringType, declaringType, StringComparison.Ordinal)
               && string.Equals(activeMemberName, memberName, StringComparison.Ordinal)
               || string.Equals(
                   activeDeclaringType,
                   "QudJP.Tests.DummyTargets.DummySifrahTokenItemPopupProducerTarget",
                   StringComparison.Ordinal)
               && string.Equals(activeMemberName, dummyMemberName, StringComparison.Ordinal);
    }

    internal sealed class OwnerContextState
    {
        internal OwnerContextState(string? previousDeclaringType, string? previousMemberName)
        {
            PreviousDeclaringType = previousDeclaringType;
            PreviousMemberName = previousMemberName;
        }

        internal string? PreviousDeclaringType { get; }

        internal string? PreviousMemberName { get; }
    }
}
