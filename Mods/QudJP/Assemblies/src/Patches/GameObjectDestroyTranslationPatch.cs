using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectDestroyTranslationPatch
{
    private const string Context = nameof(GameObjectDestroyTranslationPatch);

    private static readonly IReadOnlyDictionary<string, string> FixedLiteralTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Your mind winks out of existence."] = "あなたの精神は存在からかき消えた。",
            ["Your mind winked out of existence."] = "あなたの精神は存在からかき消えた。",
            ["You die! (good job)"] = "あなたは死んだ！（よくできました）",
            ["You were "] = "あなたは",
            ["obliterated"] = "跡形もなく消滅した。",
            ["destroyed"] = "破壊された。",
        };

    private static readonly Regex CompanionDeathMessagePattern = new(
        "^Your companion, (?<name>.+), (?<verb>[^.]+)\\.(?: (?<reason>.+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var method = gameObjectType is null
            ? null
            : AccessTools.Method(
                gameObjectType,
                "Destroy",
                [typeof(string), typeof(bool), typeof(bool), typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
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

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr
                && instruction.operand is string source
                && FixedLiteralTranslations.TryGetValue(source, out var translated))
            {
                instruction.operand = translated;
            }

            yield return instruction;
        }
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || !message.StartsWith("Your companion, ", StringComparison.Ordinal)
            || !TryTranslateCompanionDeathMessage(message, out var translated))
        {
            return false;
        }

        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;
        _ = route;
        translated = source;

        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (FixedLiteralTranslations.TryGetValue(source, out var fixedTranslated))
        {
            translated = fixedTranslated;
            DynamicTextObservability.RecordTransform(route, Context + ".FixedLiteral", source, translated);
            return true;
        }

        if (source.StartsWith("Your companion, ", StringComparison.Ordinal)
            && TryTranslateCompanionDeathMessage(source, out var companionDeath))
        {
            translated = companionDeath;
            DynamicTextObservability.RecordTransform(route, Context + ".CompanionDeath", source, translated);
            return true;
        }

        return false;
    }

    internal static string TranslateFixedLiteralForTests(string source)
    {
        return FixedLiteralTranslations.TryGetValue(source, out var translated) ? translated : source;
    }

    internal static bool TryTranslateCompanionDeathMessage(string source, out string translated)
    {
        translated = source;
        var match = CompanionDeathMessagePattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var verb = match.Groups["verb"].Value;
        if (!TryTranslateCompanionDeathVerb(verb, out var translatedVerb))
        {
            return false;
        }

        var name = GetDisplayNameRouteTranslator.TranslatePreservingColors(match.Groups["name"].Value, Context);
        translated = "仲間の" + name + "は" + translatedVerb + "。";

        var reason = match.Groups["reason"];
        if (reason.Success && reason.Length > 0)
        {
            var translatedReason = GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReason(reason.Value);
            if (string.Equals(translatedReason, reason.Value, StringComparison.Ordinal))
            {
                translatedReason = DeathReasonTranslationPatch.TranslateDeathReason(reason.Value);
            }

            translated += translatedReason;
        }

        return true;
    }

    private static bool TryTranslateCompanionDeathVerb(string source, out string translated)
    {
        translated = source switch
        {
            "died" => "死亡した",
            "dies" => "死亡した",
            "destroyed" => "破壊された",
            "obliterated" => "跡形もなく消滅した",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }
}
