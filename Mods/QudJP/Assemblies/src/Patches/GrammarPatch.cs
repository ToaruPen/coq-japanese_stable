using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

internal static class GrammarPatchTarget
{
    internal const string TypeName = "XRL.Language.Grammar";
}

internal static class GrammarPatchHelpers
{
    private static readonly Regex AssemblyQualificationPattern =
        new Regex(@",\s*[^\[\],]+,\s*Version=[^\]]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static void LogTransform(string family, string source, string translated, bool logWhenUnchanged = false)
    {
        DynamicTextObservability.RecordTransform("GrammarPatch", family, source, translated, logWhenUnchanged);
    }

    internal static string DescribeCountFamily(string family, int count)
    {
        return family + "/count=" + (count >= 3 ? "3+" : count.ToString(CultureInfo.InvariantCulture));
    }

    private static Type? ResolveTargetType()
    {
        var assemblyQualified = Type.GetType(GrammarPatchTarget.TypeName + ", Assembly-CSharp", throwOnError: false);
        if (assemblyQualified is not null)
        {
            return assemblyQualified;
        }

        return GameTypeResolver.FindType(GrammarPatchTarget.TypeName, simpleTypeName: "Grammar");
    }

    private static string NormalizeTypeName(string? typeName)
    {
        if (typeName is null)
        {
            return string.Empty;
        }

        return AssemblyQualificationPattern.Replace(typeName, string.Empty);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, string[] expectedParameterTypeNames)
    {
        if (parameters.Length != expectedParameterTypeNames.Length)
        {
            return false;
        }

        for (var index = 0; index < parameters.Length; index++)
        {
            var actual = NormalizeTypeName(parameters[index].ParameterType.FullName);
            if (!string.Equals(actual, expectedParameterTypeNames[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    internal static MethodBase? ResolveMethod(string methodName, string[] parameterTypeNames, string signature)
    {
        var targetType = ResolveTargetType();
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: Failed to resolve Grammar target type '{GrammarPatchTarget.TypeName}'. Patch will not apply.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        MethodBase? method = null;
        for (var index = 0; index < methods.Count; index++)
        {
            if (!string.Equals(methods[index].Name, methodName, StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = methods[index].GetParameters();
            if (!ParametersMatch(parameters, parameterTypeNames))
            {
                continue;
            }

            method = methods[index];
            break;
        }

        if (method is null)
        {
            Trace.TraceError($"QudJP: Failed to resolve Grammar.{signature}. Patch will not apply.");
        }

        return method;
    }

    internal static IEnumerable<MethodBase> ResolveMethods(string methodName, string[][] parameterTypeSets, string signature)
    {
        var any = false;
        for (var index = 0; index < parameterTypeSets.Length; index++)
        {
            var method = ResolveMethod(methodName, parameterTypeSets[index], signature);
            if (method is null)
            {
                continue;
            }

            any = true;
            yield return method;
        }

        if (!any)
        {
            Trace.TraceError($"QudJP: Failed to resolve any Grammar.{signature} overload. Patch will not apply.");
        }
    }

    internal static string BuildJapaneseList(List<string> items, string conjunction)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var normalizedItems = new string[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            normalizedItems[index] = StripLeadingIndefiniteArticle(items[index]);
        }

        if (items.Count == 1)
        {
            return normalizedItems[0];
        }

        if (items.Count == 2)
        {
            return normalizedItems[0] + conjunction + normalizedItems[1];
        }

        var result = new StringBuilder(normalizedItems[0]);
        for (var index = 1; index < items.Count - 1; index++)
        {
            result.Append('、');
            result.Append(normalizedItems[index]);
        }

        result.Append('、');
        result.Append(conjunction);
        result.Append(normalizedItems[items.Count - 1]);
        return result.ToString();
    }

    internal static List<string> EnsureList(IEnumerable<string> source)
    {
        return source is List<string> list ? list : new List<string>(source);
    }

    internal static string StripLeadingIndefiniteArticle(string source)
    {
        if (source.Length > 2 && source.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(2);
        }

        if (source.Length > 3 && source.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(3);
        }

        if (source.Length > 5 && source.StartsWith("some ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(5);
        }

        return source;
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
        return GrammarPatchHelpers.ResolveMethod(
            methodName: "A",
            parameterTypeNames: new[] { "System.String", "System.Boolean" },
            signature: "A(string, bool)");
    }

    public static bool Prefix(string Word, bool Capitalize, ref string __result)
    {
        try
        {
            _ = Capitalize;
            __result = GrammarPatchHelpers.StripLeadingIndefiniteArticle(Word);
            GrammarPatchHelpers.LogTransform("A", Word, __result, logWhenUnchanged: true);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarAPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

[HarmonyPatch]
public static class GrammarPluralizePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return GrammarPatchHelpers.ResolveMethod(
            methodName: "Pluralize",
            parameterTypeNames: new[] { "System.String" },
            signature: "Pluralize(string)");
    }

    public static bool Prefix(string word, ref string __result)
    {
        try
        {
            __result = word;
            GrammarPatchHelpers.LogTransform("Pluralize", word, __result, logWhenUnchanged: true);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarPluralizePatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

[HarmonyPatch]
public static class GrammarMakePossessivePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return GrammarPatchHelpers.ResolveMethod(
            methodName: "MakePossessive",
            parameterTypeNames: new[] { "System.String" },
            signature: "MakePossessive(string)");
    }

    public static bool Prefix(string word, ref string __result)
    {
        try
        {
            __result = word.EndsWith("の", StringComparison.Ordinal) ? word : word + "の";
            GrammarPatchHelpers.LogTransform("MakePossessive", word, __result, logWhenUnchanged: true);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarMakePossessivePatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

[HarmonyPatch]
public static class GrammarMakeAndListPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            return GrammarPatchHelpers.ResolveMethod(
                methodName: "MakeAndList",
                parameterTypeNames: new[] { "System.Collections.Generic.IReadOnlyList`1[[System.String]]", "System.Boolean" },
                signature: "MakeAndList(IReadOnlyList<string>, bool)");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarMakeAndListPatch.TargetMethod failed: {0}", ex);
            return null;
        }
    }

    public static bool Prefix(IEnumerable<string> __0, bool __1, ref string __result)
    {
        try
        {
            _ = __1;
            var items = GrammarPatchHelpers.EnsureList(__0);
            __result = GrammarPatchHelpers.BuildJapaneseList(items, "と");
            GrammarPatchHelpers.LogTransform(
                GrammarPatchHelpers.DescribeCountFamily("MakeAndList", items.Count),
                string.Join(" | ", items),
                __result);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarMakeAndListPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

[HarmonyPatch]
public static class GrammarMakeOrListPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return GrammarPatchHelpers.ResolveMethods(
            methodName: "MakeOrList",
            parameterTypeSets: new[]
            {
                new[] { "System.String[]", "System.Boolean" },
                new[] { "System.Collections.Generic.List`1[[System.String]]", "System.Boolean" },
            },
            signature: "MakeOrList(string[]/List<string>, bool)");
    }

    public static bool Prefix(IEnumerable<string> __0, bool __1, ref string __result)
    {
        try
        {
            _ = __1;
            var items = GrammarPatchHelpers.EnsureList(__0);
            __result = GrammarPatchHelpers.BuildJapaneseList(items, "または");
            GrammarPatchHelpers.LogTransform(
                GrammarPatchHelpers.DescribeCountFamily("MakeOrList", items.Count),
                string.Join(" | ", items),
                __result);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarMakeOrListPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

// Grammar.SplitOfSentenceList is absent in game version 2.0.4, so this helper stays
// test-covered but is not registered as a Harmony patch to avoid TargetMethod failures.
public static class GrammarSplitOfSentenceListPatch
{
    public static bool Prefix(string Text, ref List<string> __result)
    {
        try
        {
            __result = GrammarPatchHelpers.SplitSentenceList(Text);
            GrammarPatchHelpers.LogTransform(
                GrammarPatchHelpers.DescribeCountFamily("SplitOfSentenceList", __result.Count),
                Text,
                string.Join(" | ", __result));
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarSplitOfSentenceListPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

[HarmonyPatch]
public static class GrammarInitCapsPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            return GrammarPatchHelpers.ResolveMethod(
                methodName: "InitCap",
                parameterTypeNames: new[] { "System.String" },
                signature: "InitCap(string)");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarInitCapsPatch.TargetMethod failed: {0}", ex);
            return null;
        }
    }

    public static bool Prefix(string word, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(word))
            {
                __result = word ?? string.Empty;
                return false;
            }

            // Preserve ASCII first-character capitalization.
            // The game's XML loader uses InitCap to normalize element names
            // (e.g. "start" → "Start"), so skipping this breaks conversation loading.
            var first = word[0];
            if (first >= 'a' && first <= 'z')
            {
                __result = char.ToUpper(first, System.Globalization.CultureInfo.InvariantCulture) + word.Substring(1);
            }
            else
            {
                __result = word;
            }

            GrammarPatchHelpers.LogTransform("InitCap", word, __result, logWhenUnchanged: true);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarInitCapsPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}

[HarmonyPatch]
public static class GrammarCardinalNumberPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        try
        {
            return GrammarPatchHelpers.ResolveMethod(
                methodName: "Cardinal",
                parameterTypeNames: new[] { "System.Int32" },
                signature: "Cardinal(int)");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarCardinalNumberPatch.TargetMethod failed: {0}", ex);
            return null;
        }
    }

    public static bool Prefix(int num, ref string __result)
    {
        try
        {
            __result = num.ToString(CultureInfo.InvariantCulture);
            GrammarPatchHelpers.LogTransform("Cardinal", num.ToString(CultureInfo.InvariantCulture), __result, logWhenUnchanged: true);
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GrammarCardinalNumberPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
