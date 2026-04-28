using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QudJP.Tools.AnnalsPatternExtractor;

string? sourceRoot = null;
string? include = null;
string? output = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source-root":
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing value for --source-root"); return 2; }
            sourceRoot = args[++i]; break;
        case "--include":
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing value for --include"); return 2; }
            include = args[++i]; break;
        case "--output":
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing value for --output"); return 2; }
            output = args[++i]; break;
        case "--help":
            Console.Out.WriteLine("Usage: AnnalsPatternExtractor --source-root <dir> --include <glob> --output <json-path>");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (sourceRoot is null || include is null || output is null)
{
    Console.Error.WriteLine("Missing required argument. Use --help.");
    return 2;
}

if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"--source-root does not exist: {sourceRoot}");
    return 1;
}

var globPattern = include;
var files = Directory.GetFiles(sourceRoot, globPattern, SearchOption.TopDirectoryOnly)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"No files matched --include '{include}' under {sourceRoot}");
    return 1;
}

var extractor = new Extractor();
foreach (var file in files)
{
    Console.Out.WriteLine($"[extract] processing {Path.GetFileName(file)}");
    extractor.ProcessFile(file);
}

foreach (var diag in extractor.Diagnostics)
{
    Console.Error.WriteLine($"[warn] {diag}");
}

// Pre-pass: collapse branch siblings whose patterns turned out structurally identical so the
// `#if:then`/`#if:else` and `#bl:then`/`#bl:else` (etc.) suffixes only survive when the
// branches *differ*. We pair them by the "id with all branch-suffixes stripped" — if all
// members of the group share one hash, replace their ids with the stripped form and keep one.
// This realises Option A (dedupe identical shapes) for both setter-chain (#if:) and
// branched-local fanout (#bl:) without losing distinctness in the divergent-shape case.
var groups = new Dictionary<string, List<CandidateEntry>>(StringComparer.Ordinal);
foreach (var candidate in extractor.Candidates)
{
    // Bucket key keeps each branch-suffix MARKER (e.g. `#if:`, `#bl:`) but replaces the
    // label with `*` so independent fanout families (setter-chain vs branched-local) bucket
    // separately. Generators that emit BOTH families with overlapping downstream suffixes
    // (e.g. ChallengeSultan `#bl:caseN#arm:M` alongside `#if:then#arm:M`/`#if:else#arm:M`)
    // collapse identical-`#bl:` siblings without being held back by divergent `#if:` siblings.
    var key = MakeBucketKey(candidate.Id);
    if (!groups.TryGetValue(key, out var bucket))
    {
        bucket = new List<CandidateEntry>();
        groups[key] = bucket;
    }
    bucket.Add(candidate);
}

var collapsed = new List<CandidateEntry>();
foreach (var bucket in groups.Values)
{
    if (bucket.Count == 1)
    {
        collapsed.Add(bucket[0]);
        continue;
    }
    var firstHash = bucket[0].EnTemplateHash;
    var allSameShape = bucket.All(c => c.EnTemplateHash == firstHash);
    // Only collapse buckets where every member is `pending`. EnTemplateHash does not
    // include status/reason, so a bucket of `needs_manual` candidates with identical
    // empty patterns but DIFFERENT failure reasons would otherwise collapse to one,
    // losing the per-branch reason.
    var allPending = bucket.All(c => c.Status == "pending");
    if (allPending && allSameShape)
    {
        var first = bucket[0];
        first.Id = StripBranchSuffixes(first.Id);
        first.EnTemplateHash = HashHelper.ComputeEnTemplateHash(first);
        collapsed.Add(first);
    }
    else
    {
        collapsed.AddRange(bucket);
    }
}

var deduped = new List<CandidateEntry>();
var seenById = new Dictionary<string, CandidateEntry>(StringComparer.Ordinal);
foreach (var candidate in collapsed)
{
    if (seenById.TryGetValue(candidate.Id, out var prior))
    {
        // Hash equality alone is not enough to silently dedupe: EnTemplateHash does not
        // include Status/Reason, so two `needs_manual` candidates with identical empty
        // shapes but DIFFERENT failure reasons would otherwise merge and lose one reason.
        var sameShape = prior.EnTemplateHash == candidate.EnTemplateHash;
        var sameOutcome =
            string.Equals(prior.Status, candidate.Status, StringComparison.Ordinal)
            && string.Equals(prior.Reason, candidate.Reason, StringComparison.Ordinal);
        if (sameShape && sameOutcome) continue;

        Console.Error.WriteLine(
            $"[error] duplicate candidate id with divergent outcome: {candidate.Id} "
            + $"(priorHash={prior.EnTemplateHash}, newHash={candidate.EnTemplateHash}, "
            + $"priorStatus={prior.Status}, newStatus={candidate.Status}, "
            + $"priorReason={prior.Reason}, newReason={candidate.Reason}). "
            + "Resolve via branch/path id suffixes (`#if:`, `#bl:`, `#case:`, `#arm:`, `#opt:`) or preserve both reasons.");
        return 1;
    }
    seenById[candidate.Id] = candidate;
    deduped.Add(candidate);
}

// `MakeBucketKey` keeps each `#if:`/`#bl:` marker but normalizes its label to `*`, so siblings
// within ONE family bucket together while different markers stay in separate buckets.
// `StripBranchSuffixes` removes the entire `#marker:label` segment, preserving downstream
// suffixes like `#arm:`/`#opt:` so collapse pairs siblings across both families.
static string MakeBucketKey(string id) => RewriteBranchSegments(id, labelReplacement: "*");
static string StripBranchSuffixes(string id) => RewriteBranchSegments(id, labelReplacement: null);

static string RewriteBranchSegments(string id, string? labelReplacement)
{
    string[] markers = { CandidateIdSuffix.If, CandidateIdSuffix.BranchedLocal };
    var result = id;
    foreach (var marker in markers)
    {
        var searchFrom = 0;
        while (true)
        {
            var idx = result.IndexOf(marker, searchFrom, StringComparison.Ordinal);
            if (idx < 0) break;
            var labelStart = idx + marker.Length;
            var nextHash = result.IndexOf('#', labelStart);
            var labelEnd = nextHash < 0 ? result.Length : nextHash;
            if (labelReplacement is null)
            {
                // Strip the whole `#marker:label` segment.
                result = result[..idx] + result[labelEnd..];
                searchFrom = idx;
            }
            else
            {
                // Keep marker, replace just the label. Advance past the replacement so the loop
                // terminates: the marker text remains and re-searching from 0 would re-find it.
                result = result[..labelStart] + labelReplacement + result[labelEnd..];
                searchFrom = labelStart + labelReplacement.Length;
            }
        }
    }
    return result;
}

var doc = new CandidateDocument
{
    SchemaVersion = "1",
    Candidates = deduped.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
};
CandidateWriter.WriteToFile(output, doc);

Console.Out.WriteLine($"[extract] wrote {doc.Candidates.Count} candidate(s) to {output}");
return 0;
