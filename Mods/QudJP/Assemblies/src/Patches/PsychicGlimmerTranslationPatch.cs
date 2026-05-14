using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PsychicGlimmerTranslationPatch
{
    private const string Context = nameof(PsychicGlimmerTranslationPatch);

    private static readonly Regex WatchedPattern = new(
        "^You are being watched\\.\n\nIt's a familiar feeling\\. When someone has watched you in the past, when it's light that's betrayed your presence, you made a friend of the darkness\\. You pulled your hat brim low over your eyes\\. You stepped behind the cover of a thatched wall\\. But those who watch you now watch in spite of such simple obstructions\\. Their sight isn't mediated by the rays of a gleaming star or torch but by something much older\\. If there are ways to conceal (?<target>.+?) from these seeing eyes, if there are new kinds of darknesses to befriend, you know nothing of them\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConcealSelfPattern = new(
        "^You've discovered a way to conceal (?<target>.+?)\\. For now\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConcealFromWatchersPattern = new(
        "^You've discovered a way to conceal (?<target>.+?) from extradimensional watchers\\. For now\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static int directMarkerPassThroughDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var psychicGlimmerType = AccessTools.TypeByName("XRL.World.Capabilities.PsychicGlimmer");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (psychicGlimmerType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var update = AccessTools.Method(psychicGlimmerType, "Update", [gameObjectType]);
        if (update is not null)
        {
            yield return update;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Update(GameObject) target not found.", Context);
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
            if (!OwnerTranslationScope.IsActive(activeDepth))
            {
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
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
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
        if (!TryTranslateCore(stripped, spans, out translated, out var detail))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
        return true;
    }

    private static bool TryTranslateCore(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = WatchedPattern.Match(stripped);
        if (match.Success)
        {
            translated = WrapK(
                "あなたは見られている。\n\n"
                + "それは馴染みのある感覚だ。過去に誰かに見られたとき、光があなたの存在を裏切ったとき、あなたは暗闇を友とした。"
                + "帽子のつばを目深に下ろした。茅葺きの壁の陰に身を潜めた。"
                + "だが今あなたを見ている者たちは、そんな単純な遮蔽など意に介さずに見ている。"
                + "その視線は輝く星や松明の光線を介したものではなく、もっと古い何かによるものだ。"
                + "この見つめる目から"
                + TranslateReflexiveTarget(match, spans)
                + "を隠す方法があるのか、新たに友とすべき暗闇があるのか、あなたには何もわからない。");
            detail = "Watched";
            return true;
        }

        match = ConcealFromWatchersPattern.Match(stripped);
        if (match.Success)
        {
            translated = WrapK(
                "あなたは"
                + TranslateReflexiveTarget(match, spans)
                + "を超次元の観測者たちから隠す方法を見つけた。今のところは。");
            detail = "ConcealFromWatchers";
            return true;
        }

        match = ConcealSelfPattern.Match(stripped);
        if (match.Success)
        {
            translated = WrapK(
                "あなたは"
                + TranslateReflexiveTarget(match, spans)
                + "を隠す方法を見つけた。今のところは。");
            detail = "ConcealSelf";
            return true;
        }

        translated = stripped;
        detail = string.Empty;
        return false;
    }

    private static string TranslateReflexiveTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = match.Groups["target"];
        var value = target.Value.Trim();
        if (value == "yourself")
        {
            return "自分自身";
        }

        if (value == "itself")
        {
            return "それ自身";
        }

        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(value, spans, target).Trim();
    }

    private static string WrapK(string text)
    {
        return "{{K|" + text + "}}";
    }
}
