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

// Pre-pass: collapse if-branch siblings whose patterns turned out structurally identical so the
// `#if:then`/`#if:else` suffix only survives when the branches *differ*. We pair them by the
// "id with the if-suffix stripped" — if all members of the group share one hash, replace their
// ids with the stripped form and keep one. This realises Option A (dedupe identical shapes) for
// if-branches without losing distinctness in the divergent-shape case.
var groups = new Dictionary<string, List<CandidateEntry>>(StringComparer.Ordinal);
foreach (var candidate in extractor.Candidates)
{
    var key = StripIfBranchSuffix(candidate.Id);
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
        first.Id = StripIfBranchSuffix(first.Id);
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
            + "Resolve via branch/path id suffixes (`#case:`, `#arm:`, `#opt:`) or preserve both reasons.");
        return 1;
    }
    seenById[candidate.Id] = candidate;
    deduped.Add(candidate);
}

static string StripIfBranchSuffix(string id)
{
    // Remove ONLY the `#if:<label>` segment(s), preserving downstream suffixes like
    // `#arm:` or `#opt:`. Otherwise `foo#if:then#arm:0` and `foo#if:else#opt:with1`
    // would both collapse to `foo` and either falsely merge or false-collide.
    // Loop until no `#if:` remains so any future nested-if extraction stays in sync.
    while (true)
    {
        var idx = id.IndexOf(CandidateIdSuffix.If, StringComparison.Ordinal);
        if (idx < 0) return id;
        var nextSuffix = id.IndexOf('#', idx + CandidateIdSuffix.If.Length);
        id = nextSuffix < 0 ? id[..idx] : id[..idx] + id[nextSuffix..];
    }
}

var doc = new CandidateDocument
{
    SchemaVersion = "1",
    Candidates = deduped.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
};
CandidateWriter.WriteToFile(output, doc);

Console.Out.WriteLine($"[extract] wrote {doc.Candidates.Count} candidate(s) to {output}");
return 0;
