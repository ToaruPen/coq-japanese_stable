using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicsProcessTakeDamageTranslationPatch
{
    private const string Context = nameof(PhysicsProcessTakeDamageTranslationPatch);

    private static readonly Regex PlayerDamageFramePattern = new(
        "^You take (?:(?<amount>\\d+) (?<type>.+? damage|.+?)|(?<nodamage>no damage)) (?<tail>.+?)(?:(?<punct>[.!])(?<suffix>\\s+.+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThirdPersonDamageFramePattern = new(
        "^(?:The |the |[Aa]n? )?(?<subject>.+?) takes? (?:(?<amount>\\d+) (?<type>.+? damage|.+?)|(?<nodamage>no damage)) (?<tail>.+?)(?:(?<punct>[.!])(?<suffix>\\s+.+)?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TailMultiplierPrefixPattern = new(
        "^(?<prefix>(?:\\{\\{[^{}|]+\\|\\(x\\d+\\)\\}\\}|\\(x\\d+\\))\\s+)(?<tail>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    private static readonly object eventHasFlagMethodLock = new();

    private static Type? cachedEventHasFlagType;

    private static MethodInfo? cachedEventHasFlagMethod;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var physicsType = AccessTools.TypeByName("XRL.World.Parts.Physics");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (physicsType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(physicsType, "ProcessTakeDamage", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ProcessTakeDamage(Event) not found.", Context);
        }

        return method;
    }

    public static void Prefix(object? E, out int __state)
    {
        try
        {
            __state = activeDepth;
            if (!HasEventFlag(E, "NoDamageMessage"))
            {
                activeDepth++;
            }
        }
        catch (Exception ex)
        {
            __state = 0;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception, int __state)
    {
        try
        {
            activeDepth = __state;
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

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateDamageFrame(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "ProcessTakeDamage.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslateDamageFrame(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateDamageFrame(string source, out string translated)
    {
        var repositoryTranslated = MessagePatternTranslator.Translate(source, Context);
        if (!string.Equals(repositoryTranslated, source, StringComparison.Ordinal))
        {
            translated = repositoryTranslated;
            return true;
        }

        var hasPlayerWrapper = TryStripPlayerDamageWrapper(source, out var visibleSource);
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(visibleSource);

        if (TryTranslatePattern(PlayerDamageFramePattern, stripped, spans, TranslatePlayerDamageFrame, out translated)
            || TryTranslatePattern(ThirdPersonDamageFramePattern, stripped, spans, TranslateThirdPersonDamageFrame, out translated))
        {
            if (hasPlayerWrapper)
            {
                translated = "{{r|" + translated + "}}";
            }

            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        System.Collections.Generic.IReadOnlyList<ColorSpan> spans,
        Func<Match, System.Collections.Generic.IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            return false;
        }

        translated = translate(match, spans);
        return true;
    }

    private static string TranslatePlayerDamageFrame(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans)
    {
        var tail = TranslateTail(Restore(match, spans, "tail"));
        return match.Groups["nodamage"].Success
            ? tail + "ダメージを受けなかった" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix")
            : tail + Restore(match, spans, "amount") + TranslateDamageType(Restore(match, spans, "type")) + "を受けた" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix");
    }

    private static string TranslateThirdPersonDamageFrame(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans)
    {
        var subject = StripLeadingArticle(Restore(match, spans, "subject"));
        var tail = TranslateTail(Restore(match, spans, "tail"));
        return match.Groups["nodamage"].Success
            ? subject + "は" + tail + "ダメージを受けなかった" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix")
            : subject + "は" + tail + Restore(match, spans, "amount") + TranslateDamageType(Restore(match, spans, "type")) + "を受けた" + TranslatePunctuation(match.Groups["punct"].Value) + RestoreRaw(match, spans, "suffix");
    }

    private static bool TryStripPlayerDamageWrapper(string source, out string visibleSource)
    {
        const string prefix = "{{r|";
        if (source.StartsWith(prefix, StringComparison.Ordinal) && source.EndsWith("}}", StringComparison.Ordinal))
        {
            visibleSource = source.Substring(prefix.Length, source.Length - prefix.Length - 2);
            return true;
        }

        visibleSource = source;
        return false;
    }

    private static string Restore(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreRaw(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group);
    }

    private static string TranslateTail(string tail)
    {
        var prefixedTail = TailMultiplierPrefixPattern.Match(tail);
        if (prefixedTail.Success)
        {
            return prefixedTail.Groups["prefix"].Value + TranslateTail(prefixedTail.Groups["tail"].Value);
        }

        return tail switch
        {
            var value when value.StartsWith("from colliding with ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(20))) + "との衝突で",
            var value when value.StartsWith("from ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(5))) + "で",
            var value when value.StartsWith("by ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(3))) + "で",
            var value when value.StartsWith("because of ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(11))) + "により",
            var value when value.StartsWith("due to ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(7))) + "により",
            var value when value.StartsWith("being run over by ", StringComparison.Ordinal) => TranslateDamageSource(StripLeadingArticle(value.Substring(18))) + "に轢かれて",
            _ => tail + "で",
        };
    }

    private static string TranslateDamageSource(string source)
    {
        return source switch
        {
            "acid" => "酸",
            "cold" => "冷気",
            "electric" or "electrical" => "電撃",
            "fire" or "heat" => "熱",
            "mental" => "精神",
            "poison" => "毒",
            "sonic" => "音波",
            _ => source,
        };
    }

    private static string TranslateDamageType(string source)
    {
        var normalized = source.Trim();
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(normalized);
        if (!string.Equals(stripped, normalized, StringComparison.Ordinal))
        {
            return ColorAwareTranslationComposer.Restore(TranslateDamageType(stripped), spans);
        }

        return normalized switch
        {
            "damage" => "ダメージ",
            "acid" or "acid damage" => "酸ダメージ",
            "cold" or "cold damage" or "freezing" or "freezing damage" => "冷気ダメージ",
            "electric" or "electric damage" or "electrical" or "electrical damage" => "電撃ダメージ",
            "heat" or "heat damage" or "fire" or "fire damage" => "熱ダメージ",
            "mental" or "mental damage" => "精神ダメージ",
            "poison" or "poison damage" => "毒ダメージ",
            "sonic" or "sonic damage" => "音波ダメージ",
            _ when normalized.EndsWith(" damage", StringComparison.Ordinal) => normalized.Substring(0, normalized.Length - 7) + "ダメージ",
            _ => normalized + "ダメージ",
        };
    }

    private static string TranslatePunctuation(string punct)
    {
        if (string.IsNullOrEmpty(punct))
        {
            return string.Empty;
        }

        return punct == "!" ? "！" : "。";
    }

    private static string StripLeadingArticle(string source)
    {
        if (source.StartsWith("the ", StringComparison.Ordinal))
        {
            return source.Substring(4);
        }

        if (source.StartsWith("a ", StringComparison.Ordinal))
        {
            return source.Substring(2);
        }

        return source.StartsWith("an ", StringComparison.Ordinal)
            ? source.Substring(3)
            : source;
    }

    private static bool HasEventFlag(object? eventObject, string flag)
    {
        if (eventObject is null)
        {
            return false;
        }

        var method = GetEventHasFlagMethod(eventObject.GetType());
        return method?.Invoke(eventObject, new object[] { flag }) is true;
    }

    private static MethodInfo? GetEventHasFlagMethod(Type eventType)
    {
        lock (eventHasFlagMethodLock)
        {
            if (cachedEventHasFlagType != eventType)
            {
                cachedEventHasFlagMethod = eventType.GetMethod("HasFlag", new[] { typeof(string) });
                cachedEventHasFlagType = eventType;
            }

            return cachedEventHasFlagMethod;
        }
    }
}
