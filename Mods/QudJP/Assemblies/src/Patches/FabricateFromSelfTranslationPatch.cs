using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FabricateFromSelfTranslationPatch
{
    private const string Context = nameof(FabricateFromSelfTranslationPatch);

    private static readonly Regex FabricationPattern = new(
        "^(?<actor>.+?) (?<verb>fabricate|fabricates|excavate|excavates) (?<object>.+?) from (?<source>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var fabricateType = AccessTools.TypeByName("XRL.World.Parts.FabricateFromSelf");
        if (fabricateType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var activate = AccessTools.Method(fabricateType, "Activate", [typeof(bool)]);
        if (activate is not null)
        {
            yield return activate;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Activate(bool) not found.", Context);
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
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslate(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".FabricateFromSelfActivate",
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslate(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = FabricationPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var actor = TranslateActor(Restore(match, spans, "actor"));
        var item = Restore(match, spans, "object");
        var materialSource = TranslateSource(Restore(match, spans, "source"));
        var verb = TranslateVerb(match.Groups["verb"].Value);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{actor}は{materialSource}から{item}を{verb}。",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateActor(string actor)
    {
        return actor == "You" ? "あなた" : actor;
    }

    private static string TranslateSource(string source)
    {
        return source switch
        {
            "the substance of your body" => "あなたの体の物質",
            "the substance of its body" => "その体の物質",
            "the substance of his body" => "彼の体の物質",
            "the substance of her body" => "彼女の体の物質",
            "the substance of their body" => "彼らの体の物質",
            _ => source,
        };
    }

    private static string TranslateVerb(string verb)
    {
        return verb is "excavate" or "excavates" ? "掘り出した" : "作製した";
    }
}
