using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DescriptionLongDescriptionPatch
{
    private const string TargetTypeName = "XRL.World.Parts.Description";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(TargetTypeName + ":GetLongDescription", new[] { typeof(StringBuilder) });
        if (method is not null)
        {
            return method;
        }

        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is not null)
        {
            var methods = AccessTools.GetDeclaredMethods(targetType);
            for (var index = 0; index < methods.Count; index++)
            {
                var candidate = methods[index];
                if (string.Equals(candidate.Name, "GetLongDescription", StringComparison.Ordinal)
                    && candidate.ReturnType == typeof(void))
                {
                    var parameters = candidate.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(StringBuilder))
                    {
                        return candidate;
                    }
                }
            }
        }

        Trace.TraceError("QudJP: Failed to resolve Description.GetLongDescription(StringBuilder). Patch will not apply.");
        return null;
    }

    public static void Prefix(StringBuilder SB, out int __state)
    {
        try
        {
            __state = SB?.Length ?? 0;
        }
        catch (Exception ex)
        {
            __state = 0;
            Trace.TraceError("QudJP: DescriptionLongDescriptionPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(StringBuilder SB, int __state)
    {
        try
        {
            if (SB is null || SB.Length <= __state)
            {
                return;
            }

            var appended = SB.ToString(__state, SB.Length - __state);
            var translated = TranslateLongDescription(appended);
            if (string.Equals(appended, translated, StringComparison.Ordinal))
            {
                return;
            }

            SB.Remove(__state, SB.Length - __state);
            SB.Append(translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: DescriptionLongDescriptionPatch.Postfix failed: {0}", ex);
        }
    }

    internal static string TranslateLongDescription(string source)
    {
        if (StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
                source,
                nameof(DescriptionLongDescriptionPatch),
                "DescriptionLong.CompareStatus",
                out var translated))
        {
            return translated;
        }

        return UITextSkinTranslationPatch.TranslatePreservingColors(source, nameof(DescriptionLongDescriptionPatch));
    }
}
