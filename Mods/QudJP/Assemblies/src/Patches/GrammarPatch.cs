using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class GrammarPatchTarget
{
    internal const string TypeName = "XRL.Language.Grammar";
}

internal static class GrammarPatchHelpers
{
    internal static string BuildJapaneseList(List<string> items, string conjunction)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        if (items.Count == 2)
        {
            return items[0] + conjunction + items[1];
        }

        var result = items[0];
        for (var index = 1; index < items.Count - 1; index++)
        {
            result += "、" + items[index];
        }

        return result + "、" + conjunction + items[items.Count - 1];
    }

    internal static List<string> SplitSentenceList(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        var normalized = text
            .Replace("、", ",")
            .Replace(", and ", ", ")
            .Replace(" and ", ", ");

        var fragments = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(fragments.Length);
        for (var index = 0; index < fragments.Length; index++)
        {
            var trimmed = fragments[index].Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}

[HarmonyPatch]
public static class GrammarAPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":A", new[] { typeof(string), typeof(bool) });
    }

    public static bool Prefix(string Word, bool Capitalize, ref string __result)
    {
        _ = Capitalize;
        __result = Word;
        return false;
    }
}

[HarmonyPatch]
public static class GrammarPluralizePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":Pluralize", new[] { typeof(string) });
    }

    public static bool Prefix(string word, ref string __result)
    {
        __result = word;
        return false;
    }
}

[HarmonyPatch]
public static class GrammarMakePossessivePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":MakePossessive", new[] { typeof(string) });
    }

    public static bool Prefix(string word, ref string __result)
    {
        __result = word.EndsWith("の", StringComparison.Ordinal) ? word : word + "の";
        return false;
    }
}

[HarmonyPatch]
public static class GrammarMakeAndListPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":MakeAndList", new[] { typeof(List<string>) });
    }

    public static bool Prefix(List<string> Items, ref string __result)
    {
        __result = GrammarPatchHelpers.BuildJapaneseList(Items, "と");
        return false;
    }
}

[HarmonyPatch]
public static class GrammarMakeOrListPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":MakeOrList", new[] { typeof(List<string>) });
    }

    public static bool Prefix(List<string> Items, ref string __result)
    {
        __result = GrammarPatchHelpers.BuildJapaneseList(Items, "または");
        return false;
    }
}

[HarmonyPatch]
public static class GrammarSplitOfSentenceListPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":SplitOfSentenceList");
    }

    public static bool Prefix(string Text, ref List<string> __result)
    {
        __result = GrammarPatchHelpers.SplitSentenceList(Text);
        return false;
    }
}

[HarmonyPatch]
public static class GrammarInitCapsPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":InitCaps", new[] { typeof(string) });
    }

    public static bool Prefix(string Text, ref string __result)
    {
        __result = Text;
        return false;
    }
}

[HarmonyPatch]
public static class GrammarCardinalNumberPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(GrammarPatchTarget.TypeName + ":CardinalNumber", new[] { typeof(int) });
    }

    public static bool Prefix(int Number, ref string __result)
    {
        __result = Number.ToString(CultureInfo.InvariantCulture);
        return false;
    }
}
