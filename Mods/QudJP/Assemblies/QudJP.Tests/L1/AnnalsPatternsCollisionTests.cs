using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsCollisionTests
{
    private static string GetLocalizationRoot()
    {
        return Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
    }

    private static List<JournalPatternEntryDto> LoadPatterns(string path)
    {
        if (!File.Exists(path)) return new List<JournalPatternEntryDto>();
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocumentDto));
        var document = serializer.ReadObject(stream) as JournalPatternDocumentDto;
        return document?.Patterns ?? new List<JournalPatternEntryDto>();
    }

    [Test]
    public void NoAnnalsPattern_ExactlyDuplicatesAJournalPattern()
    {
        var localizationRoot = GetLocalizationRoot();
        var journal = LoadPatterns(Path.Combine(localizationRoot, "Dictionaries", "journal-patterns.ja.json"));
        var annals = LoadPatterns(Path.Combine(localizationRoot, "Dictionaries", "annals-patterns.ja.json"));
        var journalSet = new HashSet<string>();
        foreach (var p in journal) if (p?.Pattern is not null) journalSet.Add(p.Pattern);

        foreach (var a in annals)
        {
            if (a?.Pattern is null) continue;
            Assert.That(journalSet.Contains(a.Pattern), Is.False,
                $"annals pattern '{a.Pattern}' exact-duplicates a journal pattern; first-match-wins would never reach it.");
        }
    }

    [Test]
    public void NoAnnalsPattern_SwallowedByJournalPattern_ForItsOwnSampleHeuristic()
    {
        // For each annals pattern, build a "literal sample" by replacing each capture group
        // (.+?) / (.+) etc. with a placeholder string and check the journal patterns.
        // PR1 pragmatic approximation: if the annals pattern's literal anchors
        // (the parts between captures) are matched by any journal pattern, flag.
        var localizationRoot = GetLocalizationRoot();
        var journal = LoadPatterns(Path.Combine(localizationRoot, "Dictionaries", "journal-patterns.ja.json"));
        var annals = LoadPatterns(Path.Combine(localizationRoot, "Dictionaries", "annals-patterns.ja.json"));
        foreach (var a in annals)
        {
            if (a?.Pattern is null) continue;
            var literalSample = StripCapturesToPlaceholder(a.Pattern, "X");
            foreach (var j in journal)
            {
                if (j?.Pattern is null) continue;
                Regex re;
                try { re = new Regex(j.Pattern); } catch { continue; }
                Assert.That(re.IsMatch(literalSample), Is.False,
                    $"annals pattern '{a.Pattern}' (sample={literalSample}) is swallowed by journal pattern '{j.Pattern}'");
            }
        }
    }

    private static string StripCapturesToPlaceholder(string pattern, string placeholder)
    {
        // Remove anchors and replace any (...) with the placeholder. Crude but adequate for L1.
        // NOTE: The regex \([^)]*\) does not handle nested capture groups (e.g. `(a(b)c)`).
        // PR1's annals patterns do not use nested groups, so this is intentionally simple.
        // PR2+ should revisit if nested capture groups become necessary.
        var noAnchors = pattern.TrimStart('^').TrimEnd('$');
        return Regex.Replace(noAnchors, @"\([^)]*\)", placeholder);
    }
}
