using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsMarkupInvariantTests
{
    // Markup tokens expected to appear verbatim in game text (templates).
    // For pattern strings (regex), caret-prefixed tokens are escaped as \^X in the regex source,
    // so we search for the escaped form in patterns and the literal form in templates.
    private static readonly string[] AmpersandMarkupTokens =
    {
        "&W", "&w", "&G", "&g", "&R", "&r", "&Y", "&y", "&K", "&k",
        "&&",
    };

    // Caret-prefixed markup: in a regex pattern string, literal ^ is written as \^, so the
    // escaped forms are used when counting occurrences inside the pattern string.
    private static readonly (string PatternForm, string TemplateForm)[] CaretMarkupTokens =
    {
        (@"\^W", "^W"),
        (@"\^w", "^w"),
        (@"\^k", "^k"),
        (@"\^K", "^K"),
        (@"\^\^", "^^"),
    };

    private static readonly Regex CurlyMarkupRe = new(@"\{\{[^|}]+\|[^}]*\}\}", RegexOptions.Compiled);
    private static readonly Regex ColorOpenRe = new(@"<color=#[0-9A-Fa-f]{6,8}>", RegexOptions.Compiled);
    private static readonly Regex ColorCloseRe = new(@"</color>", RegexOptions.Compiled);
    private static readonly Regex EqualsTokenRe = new(@"=[a-zA-Z]+=", RegexOptions.Compiled);
    private static readonly Regex CaptureRefRe = new(@"\{t?\d+\}", RegexOptions.Compiled);
    private static readonly Regex JapaneseTextRe = new(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]", RegexOptions.Compiled);

    private static string GetAssetPath()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        return Path.Combine(localizationRoot, "Dictionaries", "annals-patterns.ja.json");
    }

    public static IEnumerable<TestCaseData> AllPatterns()
    {
        var assetPath = GetAssetPath();
        if (!File.Exists(assetPath))
            throw new FileNotFoundException($"annals-patterns.ja.json not found: {assetPath}", assetPath);

        using var stream = File.OpenRead(assetPath);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocumentDto));
        var document = serializer.ReadObject(stream) as JournalPatternDocumentDto;
        if (document is null)
            throw new InvalidDataException($"Failed to deserialize annals-patterns.ja.json as JournalPatternDocumentDto");
        if (document.Patterns is null)
            throw new InvalidDataException("annals-patterns.ja.json: 'patterns' array is null");

        for (var i = 0; i < document.Patterns.Count; i++)
        {
            var p = document.Patterns[i];
            if (p is null)
                throw new InvalidDataException($"annals-patterns.ja.json: patterns[{i}] is null");
            if (string.IsNullOrWhiteSpace(p.Pattern))
                throw new InvalidDataException($"annals-patterns.ja.json: patterns[{i}].pattern must be non-empty");
            if (string.IsNullOrWhiteSpace(p.Template))
                throw new InvalidDataException($"annals-patterns.ja.json: patterns[{i}].template must be non-empty");
            yield return new TestCaseData(p.Pattern, p.Template)
                .SetName($"AnnalsPattern_{i:D3}");
        }
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void Pattern_CompilesWithoutError(string pattern, string template)
    {
        Assert.DoesNotThrow(() => new Regex(pattern));
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void Template_CaptureReferences_DoNotExceedPatternCaptureCount(string pattern, string template)
    {
        var captureCount = new Regex(pattern).GetGroupNumbers().Length - 1;
        foreach (Match m in CaptureRefRe.Matches(template))
        {
            // Strip {t prefix if present, then } suffix
            var raw = m.Value.TrimStart('{').TrimEnd('}');
            if (raw.StartsWith('t')) raw = raw[1..];
            Assert.That(int.TryParse(raw, out var idx), Is.True, $"unparsable index in {m.Value}");
            Assert.That(idx, Is.LessThan(captureCount),
                $"template references index {idx} which exceeds capture count {captureCount} of pattern {pattern}");
        }
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void MarkupTokens_PresentInPattern_ArePresentInTemplateMultiset(string pattern, string template)
    {
        // Ampersand tokens appear the same in both pattern (regex source) and template.
        AssertTokenMultisetParity(pattern, template, AmpersandMarkupTokens);

        // Caret tokens: inside a regex pattern string, a literal ^ is written as \^.
        // The template contains the unescaped form (^W, etc.) because it is game text, not regex.
        foreach (var (patternForm, templateForm) in CaretMarkupTokens)
        {
            var patternCount = CountSubstring(pattern, patternForm);
            var templateCount = CountSubstring(template, templateForm);
            Assert.That(patternCount, Is.EqualTo(templateCount),
                $"caret markup '{templateForm}' multiset parity violated: pattern (as '{patternForm}')={patternCount} template={templateCount}");
        }
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void CurlyMarkupTokens_ArePresentInTemplateMultiset(string pattern, string template)
    {
        var patternHits = CurlyMarkupRe.Matches(pattern).Cast<Match>().Select(m => m.Value).ToList();
        var templateHits = CurlyMarkupRe.Matches(template).Cast<Match>().Select(m => m.Value).ToList();
        AssertMultisetEqual(patternHits, templateHits, "curly markup");
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void ColorTags_BalancedAcrossPatternAndTemplate(string pattern, string template)
    {
        var patternOpens = ColorOpenRe.Count(pattern);
        var templateOpens = ColorOpenRe.Count(template);
        var patternCloses = ColorCloseRe.Count(pattern);
        var templateCloses = ColorCloseRe.Count(template);
        Assert.That(patternOpens, Is.EqualTo(templateOpens),
            $"<color=...> open-count differs: pattern={patternOpens} template={templateOpens}");
        Assert.That(patternCloses, Is.EqualTo(templateCloses),
            $"</color> close-count differs: pattern={patternCloses} template={templateCloses}");
        Assert.That(patternOpens, Is.EqualTo(patternCloses),
            $"<color=...> open/close imbalance in pattern: {patternOpens} opens / {patternCloses} closes");
        Assert.That(templateOpens, Is.EqualTo(templateCloses),
            $"<color=...> open/close imbalance in template: {templateOpens} opens / {templateCloses} closes");
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void Template_ContainsJapaneseText(string pattern, string template)
    {
        Assert.That(JapaneseTextRe.IsMatch(template), Is.True,
            $"template must contain Japanese text: {template}");
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void EqualsTokens_ArePresentInTemplateMultiset(string pattern, string template)
    {
        var patternHits = EqualsTokenRe.Matches(pattern).Cast<Match>().Select(m => m.Value).ToList();
        var templateHits = EqualsTokenRe.Matches(template).Cast<Match>().Select(m => m.Value).ToList();
        AssertMultisetEqual(patternHits, templateHits, "=token=");
    }

    private static void AssertTokenMultisetParity(string pattern, string template, IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            var patternCount = CountSubstring(pattern, token);
            var templateCount = CountSubstring(template, token);
            Assert.That(patternCount, Is.EqualTo(templateCount),
                $"token '{token}' multiset parity violated: pattern={patternCount} template={templateCount}");
        }
    }

    private static int CountSubstring(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    private static void AssertMultisetEqual(List<string> a, List<string> b, string label)
    {
        var sortedA = a.OrderBy(s => s).ToList();
        var sortedB = b.OrderBy(s => s).ToList();
        Assert.That(sortedA, Is.EqualTo(sortedB), $"{label} multiset differs: a={string.Join(",", sortedA)} b={string.Join(",", sortedB)}");
    }
}
