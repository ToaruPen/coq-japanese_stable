using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PetEitherOrExplodeTranslationPatch
{
    private const string Context = nameof(PetEitherOrExplodeTranslationPatch);
    private const string Family = "PetEitherOr.Explode";

    private static readonly Regex ImplodesPattern =
        CreatePattern("^(?<subject>.+?) implodes\\.$");

    private static readonly Regex SmearedIntoStonePattern =
        CreatePattern("^(?<subject>.+?) is smeared into stone by the rasp of time\\.$");

    private static readonly Regex CrumblesIntoBeetlesPattern =
        CreatePattern("^(?<subject>.+?) crumbles? into beetles\\.$");

    private static readonly Regex VacuumedPattern =
        CreatePattern("^(?<subject>.+?) is vacuumed to another place and time\\. The void that remains is filled with three important objects from one of your side lives\\.$");

    private static readonly Regex AtomizesPattern =
        CreatePattern("^(?<subject>.+?) atomizes? and recombines? into (?<creature>.+?)\\.$");

    private static readonly Regex ConsciousnessDissipatesPattern =
        CreatePattern("^(?<subject>.+?)(?:'s|') consciousness dissipates\\.$");

    private static readonly Regex ConsciousnessDissipatesIntoPattern =
        CreatePattern("^(?<subject>.+?)(?:'s|') consciousness dissipates into (?<targets>.+?)\\.$");

    private static readonly Regex LiquifiesPattern =
        CreatePattern("^(?<subject>.+?) liquifies into several pools of\\s*(?<liquid>.+?)\\.$");

    private static readonly Regex FoldedPattern =
        CreatePattern("^(?<subject>.+?) is folded a trillion times by the pressure of the nether, causing the local region of spacetime to lose contiguity\\.$");

    private static readonly Regex VectorizedPattern =
        CreatePattern("^(?<subject>.+?) is vectorized into a line of (?<line>force|normality|plants)\\.$");

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var petEitherOrType = AccessTools.TypeByName("XRL.World.Parts.PetEitherOr");
        if (petEitherOrType is null)
        {
            Trace.TraceError("QudJP: PetEitherOrExplodeTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(petEitherOrType, "explode", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: PetEitherOrExplodeTranslationPatch.explode() not found.");
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PetEitherOrExplodeTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: PetEitherOrExplodeTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        try
        {
            _ = color;

            if (activeDepth <= 0 || string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (!TryTranslate(message, out var translated))
            {
                return false;
            }

            message = translated;
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PetEitherOrExplodeTranslationPatch.TryTranslateQueuedMessage failed: {0}", ex);
            return false;
        }
    }

    private static bool TryTranslate(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryBuild(ImplodesPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は内破した。", out translated)
            || TryBuild(SmearedIntoStonePattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は時の軋みによって石へ塗り込められた。", out translated)
            || TryBuild(CrumblesIntoBeetlesPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は崩れて甲虫になった。", out translated)
            || TryBuild(VacuumedPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は別の場所と時間へ吸い込まれた。残された虚空は、あなたの横道の人生のひとつから来た3つの重要な物体で満たされた。", out translated)
            || TryBuild(AtomizesPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は原子化し、再結合して" + CaptureWithoutLeadingArticle(match, localSpans, "creature") + "になった。", out translated)
            || TryBuild(ConsciousnessDissipatesPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "の意識は霧散した。", out translated)
            || TryBuild(ConsciousnessDissipatesIntoPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "の意識は" + Capture(match, localSpans, "targets") + "へ霧散した。", out translated)
            || TryBuild(LiquifiesPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は液化して" + Capture(match, localSpans, "liquid") + "の水たまりいくつかになった。", out translated)
            || TryBuild(FoldedPattern, stripped, spans, static (match, localSpans) => Subject(match, localSpans) + "は冥界の圧力によって1兆回折り畳まれ、局所時空領域の連続性を失わせた。", out translated)
            || TryBuild(VectorizedPattern, stripped, spans, BuildVectorizedTranslation, out translated))
        {
            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryBuild(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Func<Match, IReadOnlyList<ColorSpan>, string> build,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            return false;
        }

        var visible = build(match, spans);
        var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, stripped.Length, visible.Length);
        translated = boundarySpans.Count == 0
            ? visible
            : ColorAwareTranslationComposer.Restore(visible, boundarySpans);
        return true;
    }

    private static string BuildVectorizedTranslation(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var line = match.Groups["line"].Value switch
        {
            "force" => "力線",
            "normality" => "正常性の線",
            "plants" => "植物の列",
            _ => match.Groups["line"].Value,
        };

        return Subject(match, spans) + "は" + line + "へベクトル化された。";
    }

    private static string Subject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return Capture(match, spans, "subject");
    }

    private static string Capture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string CaptureWithoutLeadingArticle(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var value = Capture(match, spans, groupName);
        if (value.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(2);
        }

        if (value.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(3);
        }

        if (value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(4);
        }

        return value;
    }

    private static Regex CreatePattern(string pattern)
    {
        return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
