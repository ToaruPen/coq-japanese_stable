using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationDisplayTextPatch
{
    private static readonly Regex TrailingActionMarkerPattern =
        new Regex(" (?<marker>\\[[^\\]]+\\])$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("XRL.World.Conversations.ConversationNode:GetDisplayText");
        if (method is not null)
        {
            return method;
        }

        foreach (var type in AccessTools.AllTypes())
        {
            if (type is null)
            {
                continue;
            }

            var fullName = type.FullName;
            if (string.IsNullOrEmpty(fullName) || !fullName.StartsWith("XRL.World.Conversations.", StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = AccessTools.Method(type, "GetDisplayText", new[] { typeof(bool) });
            if (candidate is null)
            {
                continue;
            }

            if (candidate.ReturnType == typeof(string))
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve ConversationNode.GetDisplayText(bool). Patch will not apply.");
        return null;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result = NormalizeConversationDisplayText(__result);
            __result = UITextSkinTranslationPatch.TranslatePreservingColors(__result, nameof(ConversationDisplayTextPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ConversationDisplayTextPatch.Postfix failed: {0}", ex);
        }
    }

    private static string NormalizeConversationDisplayText(string source)
    {
        var match = TrailingActionMarkerPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        return source.Substring(0, match.Index);
    }
}
