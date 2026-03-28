using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AddPlayerMessagePatternTranslationPatch
{
    private const string Context = nameof(AddPlayerMessagePatternTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool) for AddPlayerMessagePatternTranslationPatch.");
        }

        return method;
    }

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Color;
            _ = Capitalize;

            if (string.IsNullOrEmpty(Message) || !ShouldTranslate(Message))
            {
                return true;
            }

            Message = MessageLogProducerTranslationHelpers.PreparePatternMessage(Message, Context);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AddPlayerMessagePatternTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static bool ShouldTranslate(string source)
    {
        var trimmed = source.TrimStart('\r', '\n');
        if (trimmed.Length == 0)
        {
            return false;
        }

        return trimmed.StartsWith("You died.", StringComparison.Ordinal)
            || trimmed.StartsWith("You were killed by ", StringComparison.Ordinal)
            || trimmed.StartsWith("You were bitten to death by ", StringComparison.Ordinal)
            || trimmed.StartsWith("You were accidentally killed by ", StringComparison.Ordinal)
            || trimmed.StartsWith("Your health has dropped below ", StringComparison.Ordinal)
            || trimmed.StartsWith("You miss with your ", StringComparison.Ordinal)
            || trimmed.StartsWith("You hit (", StringComparison.Ordinal)
            || trimmed.StartsWith("You critically hit (", StringComparison.Ordinal)
            || ContainsOrdinal(trimmed, " misses you with ")
            || ContainsOrdinal(trimmed, " yells, '")
            || ContainsOrdinal(trimmed, " damage from bleeding")
            || ContainsOrdinal(trimmed, " no damage from bleeding")
            || (ContainsOrdinal(trimmed, " hits ")
                && ContainsOrdinal(trimmed, " with ")
                && ContainsOrdinal(trimmed, " damage"));
    }

    private static bool ContainsOrdinal(string source, string value)
    {
        return source.Contains(value);
    }
}
