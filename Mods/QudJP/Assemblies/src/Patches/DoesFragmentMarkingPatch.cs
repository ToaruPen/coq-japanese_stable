using System.Diagnostics;
using System;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DoesFragmentMarkingPatch
{
    [HarmonyTargetMethod]
    private static System.Reflection.MethodBase? TargetMethod()
    {
        return AccessTools.Method(
            "XRL.World.GameObject:Does",
            new[]
            {
                typeof(string),
                typeof(int),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool?),
                typeof(bool),
                AccessTools.TypeByName("XRL.World.GameObject"),
                typeof(bool),
            });
    }

    public static void Postfix(
        string Verb,
        string? Adverb,
        ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result) || string.IsNullOrWhiteSpace(Verb))
            {
                return;
            }

            if (!TryFindFragment(__result, Verb, Adverb, out var subjectLength))
            {
                return;
            }

            __result = DoesVerbRouteTranslator.MarkDoesFragment(__result, Verb, subjectLength, Adverb);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: DoesFragmentMarkingPatch.Postfix failed: {0}", ex);
        }
    }

    private static bool TryFindFragment(string result, string verb, string? adverb, out int subjectLength)
    {
        subjectLength = -1;
        foreach (var form in GetVerbForms(verb))
        {
            if (TryFindFragmentWithForm(result, form, adverb, out subjectLength))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindFragmentWithForm(string result, string verbForm, string? adverb, out int subjectLength)
    {
        subjectLength = -1;
        var adverbValue = string.IsNullOrWhiteSpace(adverb) ? null : adverb!.Trim();
        var candidates = adverbValue is null
            ? new[] { " " + verbForm, "," + verbForm }
            : new[] { " " + adverbValue + " " + verbForm, ", " + adverbValue + " " + verbForm };

        for (var index = 0; index < candidates.Length; index++)
        {
            var suffix = candidates[index];
            if (!result.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            subjectLength = result.Length - suffix.Length;
            return subjectLength > 0;
        }

        return false;
    }

    private static string[] GetVerbForms(string baseVerb)
    {
        if (baseVerb is null)
        {
            return new[] { string.Empty };
        }

        switch (baseVerb)
        {
            case "are":
                return new[] { "are", "is" };
            case "have":
                return new[] { "have", "has" };
            case "do":
                return new[] { "do", "does" };
            case "go":
                return new[] { "go", "goes" };
        }

        if (baseVerb.EndsWith("y", StringComparison.Ordinal)
            && baseVerb.Length > 1)
        {
            var previous = char.ToLowerInvariant(baseVerb[baseVerb.Length - 2]);
            if (previous is not 'a' and not 'e' and not 'i' and not 'o' and not 'u')
            {
                return new[] { baseVerb, baseVerb.Remove(baseVerb.Length - 1) + "ies" };
            }
        }

        if (baseVerb.EndsWith("s", StringComparison.Ordinal)
            || baseVerb.EndsWith("x", StringComparison.Ordinal)
            || baseVerb.EndsWith("z", StringComparison.Ordinal)
            || baseVerb.EndsWith("ch", StringComparison.Ordinal)
            || baseVerb.EndsWith("sh", StringComparison.Ordinal)
            || baseVerb.EndsWith("o", StringComparison.Ordinal))
        {
            return new[] { baseVerb, baseVerb + "es" };
        }

        return new[] { baseVerb, baseVerb + "s" };
    }
}
