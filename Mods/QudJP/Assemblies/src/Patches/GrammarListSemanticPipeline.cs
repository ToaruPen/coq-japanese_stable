using System.Collections.Generic;

namespace QudJP.Patches;

internal static class GrammarListSemanticPipeline
{
    private const string AndConjunction = "と";
    private const string OrConjunction = "または";
    private const string MakeAndListFamily = "MakeAndList";
    private const string MakeOrListFamily = "MakeOrList";

    internal static string TranslateAndList(IEnumerable<string> source)
    {
        return TranslateList(source, AndConjunction, MakeAndListFamily);
    }

    internal static string TranslateOrList(IEnumerable<string> source)
    {
        return TranslateList(source, OrConjunction, MakeOrListFamily);
    }

    private static string TranslateList(IEnumerable<string> source, string conjunction, string family)
    {
        var items = GrammarPatchHelpers.EnsureList(source);
        var translated = GrammarPatchHelpers.BuildJapaneseList(items, conjunction);
        GrammarPatchHelpers.LogTransform(
            GrammarPatchHelpers.DescribeCountFamily(family, items.Count),
            string.Join(" | ", items),
            translated);
        return translated;
    }
}
